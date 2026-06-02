using Npgsql;
using MySqlConnector;
using ExploradorArchivos.AppDataFusion.Models;
using System.Globalization;
using System.Linq;

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
        IProgress<int>? progreso = null)
    {
        var result = new WriteResult(); // Almacena el estado final, inserciones y errores.
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion); // Abre la conexión física a la base de datos PostgreSQL.
            conn.Open();

            var columnas = BuildColumnasSanitizadas(infoColumnas); // Sanitiza nombres y remueve columnas duplicadas.

            // Determina si los IDs suministrados son de tipo numérico (entero).
            bool idEsEntero = columnas.Count > 0 && datos.All(item =>
            {
                string rawVal = ObtenerValorExport(item, columnas[0].Display, columnas[0].Clave);
                return string.IsNullOrEmpty(rawVal) || int.TryParse(rawVal, out _);
            });

            CrearTablaPostgreSQL(conn, tabla, columnas, idEsEntero); // Ejecuta el script DDL de creación de la tabla.

            int total = datos.Count;
            int insertados = 0;
            int errores = 0;
            string primerError = "";

            using var tx = conn.BeginTransaction(); // Transacción SQL para agrupar las inserciones masivas de forma rápida.
            try
            {
                foreach (var item in datos)
                {
                    tx.Save("fila"); // Savepoint individual para descartar solo el registro actual si falla.
                    try
                    {
                        InsertarItemPostgreSQL(conn, tx, tabla, item, columnas, infoColumnas, idEsEntero);
                        insertados++;
                        
                        if (insertados % 100 == 0)
                            progreso?.Report((int)(insertados * 100.0 / total));
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback("fila"); // Revierte únicamente el registro actual en caso de error.
                        errores++;
                        if (string.IsNullOrEmpty(primerError))
                            primerError = ex.Message;
                        Console.WriteLine($"[PG Writer] Error fila {insertados + errores}: {ex.Message}");
                    }
                }
                tx.Commit(); // Confirma todas las inserciones exitosas.
            }
            catch
            {
                tx.Rollback(); // Revierte toda la transacción ante fallos generales catastróficos.
                throw;
            }

            progreso?.Report(100);
            result.Insertados = insertados;
            result.Errores = errores;
            result.Exito = true;
            result.Mensaje = $" {insertados} registros insertados en '{tabla}'. Errores: {errores}.";
            if (errores > 0 && !string.IsNullOrEmpty(primerError))
            {
                result.Mensaje += $"\n\nPrimer error detectado:\n{primerError}";
            }
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"? Error: {ex.Message}";
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
        IProgress<int>? progreso = null)
    {
        return await Task.Run(() =>
            EscribirEnPostgreSQL(cadenaConexion, tabla, datos, infoColumnas, progreso));
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
        bool idEsEntero)
    {
        string TipoSQL(string clave, int indice)
        {
            if (indice == 0) return (idEsEntero ? "INTEGER" : "TEXT") + " PRIMARY KEY";
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

    /// <summary>
    /// Método: InsertarItemPostgreSQL
    /// - Inicializa: NpgsqlCommand con la consulta parametrizada.
    /// - Operación: Vincula los campos del DataItem a los parámetros @p0, @p1, etc., y ejecuta la inserción.
    /// </summary>
    private static void InsertarItemPostgreSQL(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string tabla,
        DataItem item,
        List<(string NombreDB, string Clave, string Display)> columnas,
        List<(string Display, string Clave)> infoColumnas,
        bool idEsEntero)
    {
        var colNames = columnas.Select(c => $"\"{c.NombreDB}\"").ToList();
        var paramNames = columnas.Select((c, i) => $"@p{i}").ToList();

        string sql = $"INSERT INTO \"{tabla}\" ({string.Join(", ", colNames)}) " +
                     $"VALUES ({string.Join(", ", paramNames)});";

        using var cmd = new NpgsqlCommand(sql, conn, tx);

        for (int i = 0; i < columnas.Count; i++)
        {
            var (_, clave, display) = columnas[i];
            string rawVal = ObtenerValorExport(item, display, clave);
            object dbVal;

            if (i == 0)
            {
                if (idEsEntero)
                    dbVal = int.TryParse(rawVal, out int idV) ? idV : DBNull.Value;
                else
                    dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
            }
            else if (clave == "valor")
            {
                dbVal = double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblV) ? dblV : DBNull.Value;
            }
            else
            {
                dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
            }

            cmd.Parameters.AddWithValue($"@p{i}", dbVal);
        }

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
        IProgress<int>? progreso = null)
    {
        var result = new WriteResult();
        try
        {
            using var conn = new MySqlConnection(cadenaConexion);
            conn.Open();

            var columnas = BuildColumnasSanitizadas(infoColumnas);

            var colIdInfo = columnas.FirstOrDefault(c => c.Clave == "id");
            bool idEsEntero = colIdInfo == default || datos.All(item =>
            {
                string rawVal = ObtenerValorExport(item, colIdInfo.Display, "id");
                return string.IsNullOrEmpty(rawVal) || int.TryParse(rawVal, out _);
            });

            CrearTablaMariaDB(conn, tabla, columnas, idEsEntero);

            int total = datos.Count;
            int insertados = 0;
            int errores = 0;

            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var item in datos)
                {
                    try
                    {
                        InsertarItemMariaDB(conn, tx, tabla, item, columnas, infoColumnas, idEsEntero);
                        insertados++;
                        if (insertados % 100 == 0)
                            progreso?.Report((int)(insertados * 100.0 / total));
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        Console.WriteLine($"[MD Writer] Error fila {insertados + errores}: {ex.Message}");
                    }
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            progreso?.Report(100);
            result.Insertados = insertados;
            result.Errores = errores;
            result.Exito = true;
            result.Mensaje = $"? {insertados} registros insertados en `{tabla}`. Errores: {errores}.";
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"? Error: {ex.Message}";
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
        IProgress<int>? progreso = null)
    {
        return await Task.Run(() =>
            EscribirEnMariaDB(cadenaConexion, tabla, datos, infoColumnas, progreso));
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
        bool idEsEntero)
    {
        string TipoSQL(string clave, int indice)
        {
            if (indice == 0) return (idEsEntero ? "INT" : "VARCHAR(255)") + " PRIMARY KEY";
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

    /// <summary>
    /// Método: InsertarItemMariaDB
    /// - Inicializa: MySqlCommand parametrizado.
    /// - Operación: Asocia valores al comando y ejecuta la inserción de una fila en MariaDB.
    /// </summary>
    private static void InsertarItemMariaDB(
        MySqlConnection conn,
        MySqlTransaction tx,
        string tabla,
        DataItem item,
        List<(string NombreDB, string Clave, string Display)> columnas,
        List<(string Display, string Clave)> infoColumnas,
        bool idEsEntero)
    {
        var colNames = columnas.Select(c => $"`{c.NombreDB}`").ToList();
        var paramNames = columnas.Select((c, i) => $"@p{i}").ToList();

        string sql = $"INSERT INTO `{tabla}` ({string.Join(", ", colNames)}) " +
                     $"VALUES ({string.Join(", ", paramNames)});";

        using var cmd = new MySqlCommand(sql, conn, tx);

        for (int i = 0; i < columnas.Count; i++)
        {
            var (_, clave, display) = columnas[i];
            string rawVal = ObtenerValorExport(item, display, clave);
            object dbVal;

            if (i == 0)
            {
                if (idEsEntero)
                    dbVal = int.TryParse(rawVal, out int idV) ? idV : DBNull.Value;
                else
                    dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
            }
            else if (clave == "valor")
            {
                dbVal = double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblV) ? dblV : DBNull.Value;
            }
            else
            {
                dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
            }

            cmd.Parameters.AddWithValue($"@p{i}", dbVal);
        }

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
            Password = contrasena
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
    /// Método: ObtenerValorExport
    /// - Operación: Extrae el valor de un campo desde CamposExtra (priorizado por mayúsculas/minúsculas o rol) o properties del DataItem.
    /// </summary>
    private static string ObtenerValorExport(DataItem item, string display, string clave)
    {
        if (item.CamposExtra.TryGetValue(display, out var v1) && v1 != null)
            return v1;

        if (item.CamposExtra.TryGetValue(display.ToLowerInvariant(), out var v2) && v2 != null)
            return v2;

        if (item.CamposExtra.TryGetValue(clave, out var v3) && v3 != null)
            return v3;

        return clave switch
        {
            "id" => item.Id.ToString(),
            "nombre" => item.Nombre ?? "",
            "categoria" => item.Categoria ?? "",
            "valor" => item.Valor.ToString(CultureInfo.InvariantCulture),
            "fecha" => item.Fecha.ToString("yyyy-MM-dd"),
            "fuente" => item.Fuente ?? "",
            _ => ""
        };
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
}

/// <summary>
/// Modelo de datos que encapsula el resultado de las operaciones de escritura en base de datos.
/// </summary>
public class WriteResult
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = "";
    public int Insertados { get; set; }
    public int Errores { get; set; }
}
