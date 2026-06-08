using Npgsql;  // Biblioteca oficial para conectarse y ejecutar comandos en PostgreSQL desde .NET.
using MySqlConnector; // Biblioteca de terceros para conectarse y ejecutar comandos en MariaDB/MySQL desde .NET, compatible con MySql.Data pero con mejor rendimiento.
using ExploradorArchivos.AppDataFusion.Models;
using System.Globalization;
using System.Linq;
using System.Data;

namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Clase de utilidad para escribir y actualizar registros DataItem en PostgreSQL o MariaDB.
/// Exporta dinámicamente columnas basándose en la configuración de la UI.
/// </summary>
public static class DatabaseWriter
{
    // ==============================================================
    //  SECCIÓN DE POSTGRESQL (OPERACIONES DE ESCRITURA Y CREACIÓN)
    // ==============================================================

    /// <summary>
    /// Método: EscribirEnPostgreSQL
    /// - Inicializa: NpgsqlConnection y abre la conexión.
    /// - Objetos: WriteResult (resultado), NpgsqlTransaction (transacción), HashSet (columnas mapeadas).
    /// - Operación: Crea la tabla si no existe e inserta masivamente los datos locales en una transacción con Savepoints individuales por fila.
    /// </summary>
    public static WriteResult EscribirEnPostgreSQL(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<(string Display, string Clave)> infoColumnas,
        IProgress<int>? progreso = null,
        bool usarPrimaryKey = true)
    {
        var result = new WriteResult();
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion);
            conn.Open();

            var columnas = BuildColumnasSanitizadas(infoColumnas);
            var mappers = CrearMapeos(columnas, datos);

            bool idEsEntero = columnas.Count > 0 && datos.All(item =>
            {
                object val = mappers[0](item);
                if (val is int) return true;
                if (val == null || val == DBNull.Value || string.IsNullOrEmpty(val.ToString())) return true;
                return int.TryParse(val.ToString(), out _);
            });

            CrearTablaPostgreSQL(conn, tabla, columnas, idEsEntero, usarPrimaryKey);

            int total = datos.Count;
            int insertados = 0;

            var colNames = columnas.Select(c => $"\"{c.NombreDB}\"").ToList();
            string sqlCopy = $"COPY \"{tabla}\" ({string.Join(", ", colNames)}) FROM STDIN (FORMAT BINARY)";

            using (var writer = conn.BeginBinaryImport(sqlCopy))
            {
                int nextId = 1;
                var seenIdsInt = new HashSet<int>();
                var seenIdsText = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var textSuffixTracker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in datos)
                {
                    writer.StartRow();
                    for (int i = 0; i < columnas.Count; i++)
                    {
                        var col = columnas[i];
                        object rawVal = mappers[i](item);

                        if (i == 0)
                        {
                            if (idEsEntero)
                            {
                                int idV;
                                if (rawVal is int iVal) idV = iVal;
                                else if (rawVal != null && rawVal != DBNull.Value && int.TryParse(rawVal.ToString(), out int pId)) idV = pId;
                                else idV = nextId++;
                                
                                if (usarPrimaryKey)
                                {
                                    if (!seenIdsInt.Add(idV))
                                    {
                                        idV = nextId;
                                        while (!seenIdsInt.Add(idV)) idV++;
                                        nextId = idV + 1;
                                    }
                                }
                                writer.Write(idV, NpgsqlTypes.NpgsqlDbType.Integer);
                            }
                            else
                            {
                                string strId = rawVal?.ToString();
                                if (string.IsNullOrWhiteSpace(strId)) strId = Guid.NewGuid().ToString();
                                else if (usarPrimaryKey)
                                {
                                    string orig = strId;
                                    if (!seenIdsText.Add(strId))
                                    {
                                        if (!textSuffixTracker.TryGetValue(orig, out int suffix)) suffix = 1;
                                        while (!seenIdsText.Add(strId)) strId = $"{orig}_{suffix++}";
                                        textSuffixTracker[orig] = suffix;
                                    }
                                }
                                writer.Write(strId, NpgsqlTypes.NpgsqlDbType.Text);
                            }
                        }
                        else if (col.Clave == "valor")
                        {
                            if (rawVal is double dblV) writer.Write(dblV, NpgsqlTypes.NpgsqlDbType.Double);
                            else if (rawVal != null && rawVal != DBNull.Value && double.TryParse(rawVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal))
                                writer.Write(parsedVal, NpgsqlTypes.NpgsqlDbType.Double);
                            else writer.Write(DBNull.Value);
                        }
                        else
                        {
                            string txt = rawVal?.ToString();
                            if (string.IsNullOrEmpty(txt)) writer.Write(DBNull.Value);
                            else writer.Write(txt, NpgsqlTypes.NpgsqlDbType.Text);
                        }
                    }
                    insertados++;
                    if (insertados % 10000 == 0)
                        progreso?.Report((int)(insertados * 100.0 / total));
                }
                writer.Complete();
            }

            progreso?.Report(100);
            result.Insertados = insertados;
            result.Errores = 0;
            result.Exito = true;
            result.Mensaje = $" {insertados} registros importados masivamente (Bulk) en la tabla '{tabla}' con éxito.";
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"? Error en importación masiva (Bulk): {ex.Message}";
        }
        return result;
    }

    /// <summary>
    /// Método: EscribirEnPostgreSQLAsync
    /// - Operación: Ejecuta el método EscribirEnPostgreSQL de forma asíncrona usando Task.Run.
    /// </summary>
    public static async Task<WriteResult> EscribirEnPostgreSQLAsync(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<(string Display, string Clave)> infoColumnas,
        IProgress<int>? progreso = null,
        bool usarPrimaryKey = true)
    {
        return await Task.Run(() =>
            EscribirEnPostgreSQL(cadenaConexion, tabla, datos, infoColumnas, progreso, usarPrimaryKey));
    }

    /// <summary>
    /// Método: CrearTablaPostgreSQL
    /// - Inicializa: NpgsqlCommand con la consulta DDL CREATE TABLE.
    /// - Operación: Crea físicamente la tabla e indexa su Clave Primaria en base al tipo de ID.
    /// </summary>
    private static void CrearTablaPostgreSQL(
        NpgsqlConnection conn,
        string tabla,
        List<(string NombreDB, string Clave, string Display)> columnas,
        bool idEsEntero,
        bool usarPrimaryKey)
    {
        string TipoSQL(string clave, int indice)
        {
            if (indice == 0) return (idEsEntero ? "INTEGER" : "TEXT") + (usarPrimaryKey ? " PRIMARY KEY" : "");
            return clave switch
            {
                "valor" => "DOUBLE PRECISION",
                "fecha" => "TEXT",
                _ => "TEXT"
            };
        }

        var defs = columnas.Select((c, i) => $"    \"{c.NombreDB}\" {TipoSQL(c.Clave, i)}").ToList();
        string sql = $"CREATE TABLE IF NOT EXISTS \"{tabla}\" (\n{string.Join(",\n", defs)}\n);";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }


    // ==============================================================
    //  SECCIÓN DE MARIADB / MYSQL (OPERACIONES DE ESCRITURA)
    // ==============================================================

    /// <summary>
    /// Método: EscribirEnMariaDB
    /// - Inicializa: MySqlConnection y abre la conexión.
    /// - Objetos: WriteResult (resultado), MySqlTransaction (transacción).
    /// - Operación: Crea la tabla y realiza la inserción masiva parametrizada en MariaDB/MySQL.
    /// </summary>
    public static WriteResult EscribirEnMariaDB(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<(string Display, string Clave)> infoColumnas,
        IProgress<int>? progreso = null,
        bool usarPrimaryKey = true)
    {
        var result = new WriteResult();
        try
        {
            using var conn = new MySqlConnection(cadenaConexion);
            conn.Open();

            var columnas = BuildColumnasSanitizadas(infoColumnas);
            var mappers = CrearMapeos(columnas, datos);

            bool idEsEntero = columnas.Count > 0 && datos.All(item =>
            {
                object val = mappers[0](item);
                if (val is int) return true;
                if (val == null || val == DBNull.Value || string.IsNullOrEmpty(val.ToString())) return true;
                return int.TryParse(val.ToString(), out _);
            });

            CrearTablaMariaDB(conn, tabla, columnas, idEsEntero, usarPrimaryKey);

            using var reader = new FastDataReader(datos, columnas, mappers, idEsEntero, progreso, usarPrimaryKey);

            var bulkCopy = new MySqlBulkCopy(conn)
            {
                DestinationTableName = tabla
            };

            var bulkResult = bulkCopy.WriteToServer(reader);

            progreso?.Report(100);
            result.Insertados = bulkResult.RowsInserted;
            result.Errores = 0;
            result.Exito = true;
            result.Mensaje = $" {bulkResult.RowsInserted} registros importados masivamente (Bulk) en la tabla '{tabla}' con éxito.";
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"? Error en importación masiva (Bulk): {ex.Message}";
        }
        return result;
    }

    /// <summary>
    /// Método: EscribirEnMariaDBAsync
    /// - Operación: Ejecuta EscribirEnMariaDB de forma asíncrona en segundo plano.
    /// </summary>
    public static async Task<WriteResult> EscribirEnMariaDBAsync(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<(string Display, string Clave)> infoColumnas,
        IProgress<int>? progreso = null,
        bool usarPrimaryKey = true)
    {
        return await Task.Run(() =>
            EscribirEnMariaDB(cadenaConexion, tabla, datos, infoColumnas, progreso, usarPrimaryKey));
    }

    /// <summary>
    /// Método: CrearTablaMariaDB
    /// - Inicializa: MySqlCommand con la consulta DDL.
    /// - Operación: Crea la tabla física en MariaDB usando motores InnoDB y CHARSET utf8mb4.
    /// </summary>
    private static void CrearTablaMariaDB(
        MySqlConnection conn,
        string tabla,
        List<(string NombreDB, string Clave, string Display)> columnas,
        bool idEsEntero,
        bool usarPrimaryKey)
    {
        string TipoSQL(string clave, int indice)
        {
            if (indice == 0) return (idEsEntero ? "INT" : "VARCHAR(255)") + (usarPrimaryKey ? " PRIMARY KEY" : "");
            return clave switch
            {
                "valor" => "DOUBLE",
                _ => "TEXT"
            };
        }

        var defs = columnas.Select((c, i) => $"    `{c.NombreDB}` {TipoSQL(c.Clave, i)}").ToList();
        string sql = $"CREATE TABLE IF NOT EXISTS `{tabla}` (\n{string.Join(",\n", defs)}\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }


    // ==============================================================
    //  SECCIÓN DE CONEXIONES Y STRINGS DE CONEXIÓN
    // ==============================================================

    /// <summary>
    /// Método: BuildPostgreSqlConnectionString
    /// - Inicializa: NpgsqlConnectionStringBuilder.
    /// - Operación: Construye y retorna la cadena de conexión de PostgreSQL.
    /// </summary>
    public static string BuildPostgreSqlConnectionString(string host, string puerto, string database, string usuario, string contrasena)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.TryParse(puerto, out int p) ? p : 5432,
            Database = string.IsNullOrWhiteSpace(database) ? "postgres" : database,
            Username = usuario,
            Password = contrasena
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Método: BuildMariaDbConnectionString
    /// - Inicializa: MySqlConnectionStringBuilder.
    /// - Operación: Construye y retorna la cadena de conexión para MariaDB/MySQL.
    /// </summary>
    public static string BuildMariaDbConnectionString(string host, string puerto, string database, string usuario, string contrasena)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = uint.TryParse(puerto, out uint p) ? p : 3306,
            UserID = usuario,
            Password = contrasena,
            AllowLoadLocalInfile = true
        };
        if (!string.IsNullOrWhiteSpace(database))
        {
            builder.Database = database;
        }
        return builder.ConnectionString;
    }

    // ==============================================================
    //  SECCIÓN DE DESCUBRIMIENTO (BASES DE DATOS Y TABLAS)
    // ==============================================================

    /// <summary>
    /// Método: ObtenerBasesDatosPostgreSQL
    /// - Inicializa: NpgsqlConnection y NpgsqlCommand.
    /// - Operación: Consulta el catálogo pg_database y retorna bases de datos accesibles en PostgreSQL.
    /// </summary>
    public static List<string> ObtenerBasesDatosPostgreSQL(string host, string puerto, string usuario, string contrasena)
    {
        var dbs = new List<string>();
        try
        {
            string connStr = BuildPostgreSqlConnectionString(host, puerto, "postgres", usuario, contrasena);
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            
            using var cmd = new NpgsqlCommand(
                "SELECT datname FROM pg_database WHERE datistemplate = false AND datallowconn = true ORDER BY datname;", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) dbs.Add(r.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ObtenerBasesDatos: {ex.Message}");
            throw;
        }
        return dbs;
    }

    /// <summary>
    /// Método: ObtenerBasesDatosMariaDB
    /// - Inicializa: MySqlConnection y MySqlCommand.
    /// - Operación: Ejecuta "SHOW DATABASES" y retorna bases de datos en MariaDB.
    /// </summary>
    public static List<string> ObtenerBasesDatosMariaDB(string host, string puerto, string usuario, string contrasena)
    {
        var dbs = new List<string>();
        try
        {
            string connStr = BuildMariaDbConnectionString(host, puerto, "", usuario, contrasena);
            using var conn = new MySqlConnection(connStr);
            conn.Open();
            
            using var cmd = new MySqlCommand("SHOW DATABASES;", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) dbs.Add(r.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MariaDB] ObtenerBasesDatos: {ex.Message}");
            throw;
        }
        return dbs;
    }

    /// <summary>
    /// Método: ObtenerTablasPostgreSQL
    /// - Inicializa: NpgsqlConnection y NpgsqlCommand.
    /// - Operación: Consulta tables del information_schema para listar tablas en el esquema public.
    /// </summary>
    public static List<string> ObtenerTablasPostgreSQL(string cadenaConexion)
    {
        var tablas = new List<string>();
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion);
            conn.Open();
            
            using var cmd = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables " +
                "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' " +
                "ORDER BY table_name;", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) tablas.Add(r.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ObtenerTablas: {ex.Message}");
        }
        return tablas;
    }

    /// <summary>
    /// Método: ObtenerTablasMariaDB
    /// - Inicializa: MySqlConnection y MySqlCommand.
    /// - Operación: Consulta tables del information_schema para listar tablas de la base de datos seleccionada.
    /// </summary>
    public static List<string> ObtenerTablasMariaDB(string cadenaConexion)
    {
        var tablas = new List<string>();
        try
        {
            using var conn = new MySqlConnection(cadenaConexion);
            conn.Open();
            
            using var cmd = new MySqlCommand(
                "SELECT table_name FROM information_schema.tables " +
                "WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE' " +
                "ORDER BY table_name;", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) tablas.Add(r.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MariaDB] ObtenerTablas: {ex.Message}");
        }
        return tablas;
    }

    // ==============================================================
    //  HELPERS COMPARTIDOS PARA SANITIZACIÓN Y MAPEO
    // ==============================================================

    /// <summary>
    /// Método: BuildColumnasSanitizadas
    /// - Declaración: HashSet (yaAgregados) para filtrar unicidad de columnas.
    /// - Operación: Sanitiza nombres de cabeceras UI y elimina duplicados conflictivos para SQL.
    /// </summary>
    private static List<(string NombreDB, string Clave, string Display)> BuildColumnasSanitizadas(
        List<(string Display, string Clave)> infoColumnas)
    {
        var resultado = new List<(string NombreDB, string Clave, string Display)>();
        var yaAgregados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (display, clave) in infoColumnas)
        {
            string nombreDB = SanitizarNombre(display);
            if (string.IsNullOrEmpty(nombreDB)) continue;
            
            if (!yaAgregados.Add(nombreDB)) continue;
            resultado.Add((nombreDB, clave, display));
        }

        return resultado;
    }


    /// <summary>
    /// Método: SanitizarNombre
    /// - Operación: Convierte a minúsculas, reemplaza caracteres no admitidos por guiones bajos y asegura que no empiece con dígitos.
    /// </summary>
    public static string SanitizarNombre(string nombre)
    {
        string clean = new string(nombre.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray()).Trim('_');
        if (string.IsNullOrEmpty(clean)) return "campo";
        return (char.IsDigit(clean[0]) ? "_" + clean : clean).ToLowerInvariant();
    }

    // ==============================================================
    //  SECCIÓN DE ACTUALIZACIÓN EN TIEMPO REAL (CELDA POR CELDA)
    // ==============================================================

    /// <summary>
    /// Método: ActualizarCampoPostgreSQL
    /// - Inicializa: NpgsqlConnection y NpgsqlCommand.
    /// - Operación: Consulta el catálogo para averiguar los tipos de columna, castea el ID y el valor al tipo correcto y ejecuta un UPDATE.
    /// </summary>
    public static (bool Exito, string Error) ActualizarCampoPostgreSQL(
        string cadenaConexion,
        string tabla,
        string colId,
        object idVal,
        string colNombre,
        object? nuevoValor)
    {
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion);
            conn.Open();

            string? dataType = null;
            string? idDataType = null;
            try
            {
                string typeSql = "SELECT column_name, data_type FROM information_schema.columns WHERE (table_name = @tab OR table_name = @tabLower) AND (column_name = @col OR column_name = @colId);";
                using var cmdType = new NpgsqlCommand(typeSql, conn);
                cmdType.Parameters.AddWithValue("@tab", tabla);
                cmdType.Parameters.AddWithValue("@tabLower", tabla.ToLowerInvariant());
                cmdType.Parameters.AddWithValue("@col", colNombre);
                cmdType.Parameters.AddWithValue("@colId", colId);
                using var r = cmdType.ExecuteReader();
                while (r.Read())
                {
                    string cName = r.GetString(0);
                    string dType = r.GetString(1);
                    if (string.Equals(cName, colNombre, StringComparison.OrdinalIgnoreCase)) dataType = dType;
                    if (string.Equals(cName, colId, StringComparison.OrdinalIgnoreCase)) idDataType = dType;
                }
            }
            catch { }

            object? dbValue = nuevoValor;
            if (dbValue != null && dataType != null)
            {
                string valStr = dbValue.ToString() ?? "";
                string dtLower = dataType.ToLowerInvariant();
                if (dtLower.Contains("int") || dtLower.Contains("integer"))
                {
                    dbValue = int.TryParse(valStr, out int intVal) ? intVal : (string.IsNullOrEmpty(valStr.Trim()) ? DBNull.Value : dbValue);
                }
                else if (dtLower.Contains("double") || dtLower.Contains("real") || dtLower.Contains("numeric") || dtLower.Contains("decimal") || dtLower.Contains("precision"))
                {
                    dbValue = double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal) ? dblVal : (string.IsNullOrEmpty(valStr.Trim()) ? DBNull.Value : dbValue);
                }
                else if (dtLower.Contains("bool") || dtLower.Contains("boolean"))
                {
                    dbValue = bool.TryParse(valStr, out bool boolVal) ? boolVal : (string.IsNullOrEmpty(valStr.Trim()) ? DBNull.Value : dbValue);
                }
            }

            object finalIdVal = idVal;
            if (idVal != null && idDataType != null)
            {
                string idStr = idVal.ToString() ?? "";
                string idDtLower = idDataType.ToLowerInvariant();
                if (idDtLower.Contains("text") || idDtLower.Contains("char"))
                {
                    finalIdVal = idStr;
                }
                else if (idDtLower.Contains("int") || idDtLower.Contains("integer"))
                {
                    if (int.TryParse(idStr, out int intId))
                        finalIdVal = intId;
                }
            }

            string sql = $"UPDATE \"{tabla}\" SET \"{colNombre}\" = @val WHERE \"{colId}\" = @id;";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@val", dbValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", finalIdVal);
            
            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                return (false, "No se encontró ningún registro en la base de datos con ese identificador.");
            }
            return (true, "");
        }
        catch (Exception ex)
        {
            string colsInfo = "";
            try
            {
                using var conn2 = new NpgsqlConnection(cadenaConexion);
                conn2.Open();
                using var cmdCols = new NpgsqlCommand(
                    "SELECT column_name FROM information_schema.columns WHERE table_name = @tab", conn2);
                cmdCols.Parameters.AddWithValue("@tab", tabla);
                using var rCols = cmdCols.ExecuteReader();
                var colList = new List<string>();
                while (rCols.Read()) colList.Add(rCols.GetString(0));
                colsInfo = "\nColumnas en BD: " + string.Join(", ", colList);
            }
            catch (Exception exCols)
            {
                colsInfo = "\n(No se pudo leer columnas de la tabla: " + exCols.Message + ")";
            }
            return (false, $"{ex.Message}.{colsInfo}");
        }
    }

    /// <summary>
    /// Método: ActualizarCampoPostgreSQLAsync
    /// - Operación: Ejecuta la actualización de PostgreSQL de forma asíncrona.
    /// </summary>
    public static async Task<(bool Exito, string Error)> ActualizarCampoPostgreSQLAsync(
        string cadenaConexion,
        string tabla,
        string colId,
        object idVal,
        string colNombre,
        object? nuevoValor)
    {
        return await Task.Run(() => ActualizarCampoPostgreSQL(cadenaConexion, tabla, colId, idVal, colNombre, nuevoValor));
    }

    /// <summary>
    /// Método: ActualizarCampoMariaDB
    /// - Inicializa: MySqlConnection y MySqlCommand.
    /// - Operación: Realiza un UPDATE parametrizado de una celda específica en una tabla de MariaDB.
    /// </summary>
    public static (bool Exito, string Error) ActualizarCampoMariaDB(
        string cadenaConexion,
        string tabla,
        string colId,
        object idVal,
        string colNombre,
        object? nuevoValor)
    {
        try
        {
            using var conn = new MySqlConnection(cadenaConexion);
            conn.Open();
            
            string sql = $"UPDATE `{tabla}` SET `{colNombre}` = @val WHERE `{colId}` = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@val", nuevoValor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", idVal);
            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                return (false, "No se encontró ningún registro en la base de datos con ese identificador.");
            }
            return (true, "");
        }
        catch (Exception ex)
        {
            string colsInfo = "";
            try
            {
                using var conn2 = new MySqlConnection(cadenaConexion);
                conn2.Open();
                using var cmdCols = new MySqlCommand(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tab AND TABLE_SCHEMA = @schema", conn2);
                cmdCols.Parameters.AddWithValue("@tab", tabla);
                cmdCols.Parameters.AddWithValue("@schema", conn2.Database);
                using var rCols = cmdCols.ExecuteReader();
                var colList = new List<string>();
                while (rCols.Read()) colList.Add(rCols.GetString(0));
                colsInfo = "\nColumnas en BD: " + string.Join(", ", colList);
            }
            catch (Exception exCols)
            {
                colsInfo = "\n(No se pudo leer columnas de la tabla: " + exCols.Message + ")";
            }
            return (false, $"{ex.Message}.{colsInfo}");
        }
    }

    /// <summary>
    /// Método: ActualizarCampoMariaDBAsync
    /// - Operación: Ejecuta la actualización de MariaDB de forma asíncrona.
    /// </summary>
    public static async Task<(bool Exito, string Error)> ActualizarCampoMariaDBAsync(
        string cadenaConexion,
        string tabla,
        string colId,
        object idVal,
        string colNombre,
        object? nuevoValor)
    {
        return await Task.Run(() => ActualizarCampoMariaDB(cadenaConexion, tabla, colId, idVal, colNombre, nuevoValor));
    }
    private static Func<DataItem, object>[] CrearMapeos(List<(string NombreDB, string Clave, string Display)> columnas, List<DataItem> datos)
    {
        var mappers = new Func<DataItem, object>[columnas.Count];
        
        var firstItem = datos.FirstOrDefault();
        var realKeys = new Dictionary<int, string>();
        if (firstItem != null)
        {
            for (int i = 0; i < columnas.Count; i++)
            {
                var (_, clave, display) = columnas[i];
                string? matchedKey = null;
                
                string dispLow = display.Trim().ToLowerInvariant();
                string claveLow = clave.Trim().ToLowerInvariant();

                if (firstItem.CamposExtra.ContainsKey(display)) matchedKey = display;
                else if (firstItem.CamposExtra.ContainsKey(clave)) matchedKey = clave;
                else
                {
                    foreach (var k in firstItem.CamposExtra.Keys)
                    {
                        string kLow = k.Trim().ToLowerInvariant();
                        if (kLow == dispLow || kLow == claveLow)
                        {
                            matchedKey = k;
                            break;
                        }
                    }
                }
                if (matchedKey != null) realKeys[i] = matchedKey;
            }
        }

        for (int i = 0; i < columnas.Count; i++)
        {
            var (_, clave, _) = columnas[i];
            bool hasRealKey = realKeys.TryGetValue(i, out string? exactKey);

            mappers[i] = item =>
            {
                if (hasRealKey && exactKey != null && item.CamposExtra.TryGetValue(exactKey, out var v) && v != null) 
                    return v;

                return clave switch
                {
                    "id" => item.Id,
                    "nombre" => item.Nombre ?? "",
                    "categoria" => item.Categoria ?? "",
                    "valor" => item.Valor,
                    "fecha" => item.Fecha.ToString("yyyy-MM-dd"),
                    "latitude" => (object?)item.Latitude ?? DBNull.Value,
                    "longitude" => (object?)item.Longitude ?? DBNull.Value,
                    "fuente" => item.Fuente ?? "",
                    _ => DBNull.Value
                };
            };
        }
        return mappers;
    }

    private class FastDataReader : IDataReader
    {
        private readonly List<DataItem> _datos;
        private readonly List<(string NombreDB, string Clave, string Display)> _columnas;
        private readonly Func<DataItem, object>[] _mappers;
        private readonly bool _idEsEntero;
        private readonly IProgress<int>? _progreso;
        private readonly bool _usarPrimaryKey;
        
        private int _currentIndex = -1;
        private int _nextId = 1;
        private HashSet<int> _seenIdsInt = new HashSet<int>();
        private HashSet<string> _seenIdsText = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _textSuffixTracker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public FastDataReader(List<DataItem> datos, List<(string, string, string)> columnas, Func<DataItem, object>[] mappers, bool idEsEntero, IProgress<int>? progreso, bool usarPrimaryKey)
        {
            _datos = datos;
            _columnas = columnas;
            _mappers = mappers;
            _idEsEntero = idEsEntero;
            _progreso = progreso;
            _usarPrimaryKey = usarPrimaryKey;
        }

        public bool Read()
        {
            _currentIndex++;
            if (_currentIndex % 10000 == 0 && _currentIndex > 0)
                _progreso?.Report((int)(_currentIndex * 100.0 / _datos.Count));
            return _currentIndex < _datos.Count;
        }

        public object GetValue(int i)
        {
            var rawVal = _mappers[i](_datos[_currentIndex]);
            
            if (i == 0)
            {
                if (_idEsEntero)
                {
                    int idV;
                    if (rawVal is int iVal) idV = iVal;
                    else if (rawVal != null && rawVal != DBNull.Value && int.TryParse(rawVal.ToString(), out int pId)) idV = pId;
                    else idV = _nextId++;
                    
                    if (_usarPrimaryKey)
                    {
                        if (!_seenIdsInt.Add(idV))
                        {
                            idV = _nextId;
                            while (!_seenIdsInt.Add(idV)) idV++;
                            _nextId = idV + 1;
                        }
                    }
                    return idV;
                }
                else
                {
                    string? strId = rawVal?.ToString();
                    if (string.IsNullOrWhiteSpace(strId)) strId = Guid.NewGuid().ToString();
                    else if (_usarPrimaryKey)
                    {
                        string orig = strId;
                        if (!_seenIdsText.Add(strId))
                        {
                            if (!_textSuffixTracker.TryGetValue(orig, out int suffix)) suffix = 1;
                            while (!_seenIdsText.Add(strId)) strId = $"{orig}_{suffix++}";
                            _textSuffixTracker[orig] = suffix;
                        }
                    }
                    return strId;
                }
            }
            else if (_columnas[i].Clave == "valor")
            {
                if (rawVal is double dblV) return dblV;
                if (rawVal != null && rawVal != DBNull.Value && double.TryParse(rawVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal))
                    return parsedVal;
                return DBNull.Value;
            }
            else
            {
                if (rawVal == null || (rawVal is string s && string.IsNullOrEmpty(s))) return DBNull.Value;
                return rawVal.ToString() ?? (object)DBNull.Value;
            }
        }

        public int FieldCount => _columnas.Count;
        public bool IsDBNull(int i) => GetValue(i) == DBNull.Value;
        public int GetValues(object[] values)
        {
            int count = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < count; i++) values[i] = GetValue(i);
            return count;
        }
        
        public object this[int i] => GetValue(i);
        public object this[string name] => throw new NotImplementedException();
        public void Close() {}
        public void Dispose() {}
        public DataTable GetSchemaTable() => throw new NotImplementedException();
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => -1;

        public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));
        public byte GetByte(int i) => Convert.ToByte(GetValue(i));
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public char GetChar(int i) => Convert.ToChar(GetValue(i));
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public IDataReader GetData(int i) => throw new NotImplementedException();
        public string GetDataTypeName(int i) => throw new NotImplementedException();
        public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));
        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
        public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
        public Type GetFieldType(int i) => typeof(object);
        public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
        public Guid GetGuid(int i) => (Guid)GetValue(i);
        public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
        public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
        public string GetName(int i) => throw new NotImplementedException();
        public int GetOrdinal(string name) => throw new NotImplementedException();
        public string GetString(int i) => Convert.ToString(GetValue(i)) ?? "";
    }
}
