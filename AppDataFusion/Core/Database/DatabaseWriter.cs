using Npgsql;
using MySqlConnector;
using ExploradorArchivos.AppDataFusion.Models;
using System.Globalization;

namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Escribe registros DataItem en PostgreSQL o MariaDB exportando exactamente
/// las mismas columnas que aparecen en el dataset cargado (sin columnas fijas).
/// La lista de columnas y su mapeo se recibe desde MainForm._infoColumnas,
/// igual que hace FileExportService, para que la tabla en BD sea idéntica
/// a lo que se ve en pantalla y a lo que se exportaría a CSV/JSON/XML/TXT.
/// </summary>
public static class DatabaseWriter
{
    // --------------------------------------------------------------
    //  POSTGRESQL
    // --------------------------------------------------------------

    /// <summary>
    /// Exporta masivamente una lista de datos locales hacia una nueva tabla en PostgreSQL.
    /// Deduce dinámicamente si el ID es numérico o de texto y crea la tabla si no existe.
    /// </summary>
    public static WriteResult EscribirEnPostgreSQL(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<(string Display, string Clave)> infoColumnas,
        IProgress<int>? progreso = null)
    {
        var result = new WriteResult();
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion);
            conn.Open();

            // Construir lista de columnas definitiva (sin duplicados, en orden original)
            var columnas = BuildColumnasSanitizadas(infoColumnas);

            // Verificar si el ID contiene caracteres no numéricos
            bool idEsEntero = true;
            var colIdInfo = columnas.FirstOrDefault(c => c.Clave == "id");
            if (colIdInfo != default)
            {
                foreach (var item in datos)
                {
                    string rawVal = ObtenerValorExport(item, colIdInfo.Display, "id");
                    if (!string.IsNullOrEmpty(rawVal) && !int.TryParse(rawVal, out _))
                    {
                        idEsEntero = false;
                        break;
                    }
                }
            }

            CrearTablaPostgreSQL(conn, tabla, columnas, idEsEntero);

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
                        InsertarItemPostgreSQL(conn, tx, tabla, item, columnas, infoColumnas, idEsEntero);
                        insertados++;
                        if (insertados % 100 == 0)
                            progreso?.Report((int)(insertados * 100.0 / total));
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        Console.WriteLine($"[PG Writer] Error fila {insertados + errores}: {ex.Message}");
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
            result.Mensaje = $" {insertados} registros insertados en '{tabla}'. Errores: {errores}.";
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"? Error: {ex.Message}";
        }
        return result;
    }

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
    /// Crea dinámicamente una tabla en PostgreSQL basada en las columnas sanitizadas extraídas
    /// de la vista de datos actual, determinando automáticamente los tipos de dato (SERIAL, TEXT, DOUBLE PRECISION).
    /// </summary>
    private static void CrearTablaPostgreSQL(
        NpgsqlConnection conn,
        string tabla,
        List<(string NombreDB, string Clave, string Display)> columnas,
        bool idEsEntero)
    {
        // Determinar tipo SQL por clave semántica
        string TipoSQL(string clave) => clave switch
        {
            "id" => (idEsEntero ? "INTEGER" : "TEXT") + " PRIMARY KEY",
            "valor" => "DOUBLE PRECISION",
            "fecha" => "TEXT",   // TEXT es seguro para fechas de formato variable
            _ => "TEXT"
        };

        var defs = columnas.Select(c => $"    \"{c.NombreDB}\" {TipoSQL(c.Clave)}").ToList();
        if (!columnas.Any(c => string.Equals(c.Clave, "id", StringComparison.OrdinalIgnoreCase)))
        {
            defs.Insert(0, "    \"id\" SERIAL PRIMARY KEY");
        }
        string sql = $"CREATE TABLE IF NOT EXISTS \"{tabla}\" (\n{string.Join(",\n", defs)}\n);";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserta una única fila (DataItem) en una tabla PostgreSQL mapeando los valores desde el DataItem
    /// hacia los parámetros seguros del comando SQL (evita inyecciones SQL).
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
            if (clave == "id")
            {
                if (idEsEntero)
                {
                    if (int.TryParse(rawVal, out int idV))
                        dbVal = idV;
                    else
                        dbVal = DBNull.Value;
                }
                else
                {
                    dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
                }
            }
            else if (clave == "valor")
            {
                if (double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblV))
                    dbVal = dblV;
                else
                    dbVal = DBNull.Value;
            }
            else
            {
                dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
            }

            cmd.Parameters.AddWithValue($"@p{i}", dbVal);
        }

        cmd.ExecuteNonQuery();
    }

    // --------------------------------------------------------------
    //  MARIADB
    // --------------------------------------------------------------

    /// <summary>
    /// Exporta masivamente una lista de datos locales hacia una nueva tabla en MariaDB/MySQL.
    /// Genera la tabla automáticamente usando tipos de dato compatibles con MySQL (INT, VARCHAR, DOUBLE, TEXT).
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

            // Verificar si el ID contiene caracteres no numéricos
            bool idEsEntero = true;
            var colIdInfo = columnas.FirstOrDefault(c => c.Clave == "id");
            if (colIdInfo != default)
            {
                foreach (var item in datos)
                {
                    string rawVal = ObtenerValorExport(item, colIdInfo.Display, "id");
                    if (!string.IsNullOrEmpty(rawVal) && !int.TryParse(rawVal, out _))
                    {
                        idEsEntero = false;
                        break;
                    }
                }
            }

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
    /// Crea dinámicamente una tabla en MariaDB basada en las columnas sanitizadas extraídas de los datos,
    /// aplicando dialectos SQL propios de MySQL (ENGINE=InnoDB, AUTO_INCREMENT).
    /// </summary>
    private static void CrearTablaMariaDB(
        MySqlConnection conn,
        string tabla,
        List<(string NombreDB, string Clave, string Display)> columnas,
        bool idEsEntero)
    {
        string TipoSQL(string clave) => clave switch
        {
            "id" => (idEsEntero ? "INT" : "VARCHAR(255)") + " PRIMARY KEY",
            "valor" => "DOUBLE",
            _ => "TEXT"
        };

        var defs = columnas.Select(c => $"    `{c.NombreDB}` {TipoSQL(c.Clave)}").ToList();
        if (!columnas.Any(c => string.Equals(c.Clave, "id", StringComparison.OrdinalIgnoreCase)))
        {
            defs.Insert(0, "    `id` INT AUTO_INCREMENT PRIMARY KEY");
        }
        string sql = $"CREATE TABLE IF NOT EXISTS `{tabla}` (\n{string.Join(",\n", defs)}\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserta un único registro (DataItem) en MariaDB, asignando cuidadosamente los valores
    /// extraídos a parámetros seguros de la consulta para prevenir inyección SQL.
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
            if (clave == "id")
            {
                if (idEsEntero)
                {
                    if (int.TryParse(rawVal, out int idV))
                        dbVal = idV;
                    else
                        dbVal = DBNull.Value;
                }
                else
                {
                    dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
                }
            }
            else if (clave == "valor")
            {
                if (double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblV))
                    dbVal = dblV;
                else
                    dbVal = DBNull.Value;
            }
            else
            {
                dbVal = string.IsNullOrEmpty(rawVal) ? DBNull.Value : (object)rawVal;
            }

            cmd.Parameters.AddWithValue($"@p{i}", dbVal);
        }

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Construye de manera segura una cadena de conexión para PostgreSQL manejando valores por defecto
    /// para el puerto y el nombre de la base de datos maestra (postgres).
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
    /// Construye de manera segura una cadena de conexión para MariaDB/MySQL validando el puerto (default 3306).
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

    // --------------------------------------------------------------
    //  OBTENER BASES DE DATOS DISPONIBLES
    // --------------------------------------------------------------

    /// <summary>
    /// Enumera todas las bases de datos disponibles y con permiso de acceso en el servidor PostgreSQL remoto.
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
    /// Enumera todas las bases de datos (Schemas/Databases) alojadas en el servidor MariaDB remoto.
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

    // --------------------------------------------------------------
    //  OBTENER TABLAS DISPONIBLES
    // --------------------------------------------------------------

    /// <summary>
    /// Enumera todas las tablas base de usuario (no de sistema) dentro del esquema 'public' 
    /// en la base de datos PostgreSQL conectada.
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
    /// Extrae y enlista el nombre de todas las tablas base que residen en la base de datos MariaDB seleccionada.
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

    // --------------------------------------------------------------
    //  HELPERS COMPARTIDOS
    // --------------------------------------------------------------

    /// <summary>
    /// Convierte _infoColumnas en la lista definitiva de columnas para la BD.
    /// - Usa el Display como nombre de columna en BD (sanitizado).
    /// - Elimina duplicados por nombre sanitizado (el primero gana).
    /// Esto garantiza que la tabla en BD tenga exactamente las mismas columnas
    /// que se ven en el DataGridView y que se exportarían a CSV/JSON/etc.
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
            // Si el nombre sanitizado ya existe, saltar (evita columna duplicada)
            if (!yaAgregados.Add(nombreDB)) continue;
            resultado.Add((nombreDB, clave, display));
        }

        return resultado;
    }

    /// <summary>
    /// Obtiene el valor de una columna para un DataItem dado su Display y Clave.
    /// Usa exactamente la misma lógica que FileExportService.GetValorExport,
    /// buscando primero en CamposExtra (valor RAW original del archivo)
    /// y haciendo fallback a las propiedades estándar de DataItem.
    /// </summary>
    private static string ObtenerValorExport(DataItem item, string display, string clave)
    {
        // 1. Buscar en CamposExtra por Display original (como lo guarda TxtDataReader)
        if (item.CamposExtra.TryGetValue(display, out var v1) && v1 != null)
            return v1;

        // 2. Buscar en CamposExtra por Display en minúsculas
        if (item.CamposExtra.TryGetValue(display.ToLowerInvariant(), out var v2) && v2 != null)
            return v2;

        // 3. Buscar en CamposExtra por Clave
        if (item.CamposExtra.TryGetValue(clave, out var v3) && v3 != null)
            return v3;

        // 4. Fallback a propiedades estándar de DataItem
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

    public static string SanitizarNombre(string nombre)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in nombre)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        string s = sb.ToString().Trim('_');
        if (s.Length == 0) return "campo";
        if (char.IsDigit(s[0])) s = "_" + s;
        return s.ToLowerInvariant();
    }

    /// <summary>
    /// Realiza un UPDATE sincronizado de una sola celda/campo hacia PostgreSQL, descubriendo dinámicamente
    /// los tipos de la columna y el ID para enviar los datos con seguridad en parámetros fuertemente tipados.
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

            // Detectar el tipo de dato de las columnas en PostgreSQL para evitar errores de tipo en parámetros
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
                    if (int.TryParse(valStr, out int intVal))
                        dbValue = intVal;
                    else if (string.IsNullOrEmpty(valStr.Trim()))
                        dbValue = DBNull.Value;
                }
                else if (dtLower.Contains("double") || dtLower.Contains("real") || dtLower.Contains("numeric") || dtLower.Contains("decimal") || dtLower.Contains("precision"))
                {
                    if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                        dbValue = dblVal;
                    else if (string.IsNullOrEmpty(valStr.Trim()))
                        dbValue = DBNull.Value;
                }
                else if (dtLower.Contains("bool") || dtLower.Contains("boolean"))
                {
                    if (bool.TryParse(valStr, out bool boolVal))
                        dbValue = boolVal;
                    else if (string.IsNullOrEmpty(valStr.Trim()))
                        dbValue = DBNull.Value;
                }
            }

            object finalIdVal = idVal;
            if (idVal != null && idDataType != null)
            {
                string idStr = idVal.ToString() ?? "";
                string idDtLower = idDataType.ToLowerInvariant();
                if (idDtLower.Contains("text") || idDtLower.Contains("char"))
                {
                    finalIdVal = idStr; // PostgreSQL espera un texto
                }
                else if (idDtLower.Contains("int") || idDtLower.Contains("integer"))
                {
                    if (int.TryParse(idStr, out int intId))
                        finalIdVal = intId; // PostgreSQL espera un entero
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
    /// Ejecuta una actualización singular (UPDATE) en tiempo real hacia una celda específica de una tabla en MariaDB,
    /// mapeando el identificador para actualizar exactamente el registro correcto.
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



public class WriteResult
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = "";
    public int Insertados { get; set; }
    public int Errores { get; set; }
}

