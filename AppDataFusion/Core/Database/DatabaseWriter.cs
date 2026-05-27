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

            CrearTablaPostgreSQL(conn, tabla, columnas);

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
                        InsertarItemPostgreSQL(conn, tx, tabla, item, columnas, infoColumnas);
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

    private static void CrearTablaPostgreSQL(
        NpgsqlConnection conn,
        string tabla,
        List<(string NombreDB, string Clave, string Display)> columnas)
    {
        // Determinar tipo SQL por clave semántica
        string TipoSQL(string clave) => clave switch
        {
            "id" => "INTEGER",
            "valor" => "DOUBLE PRECISION",
            "fecha" => "TEXT",   // TEXT es seguro para fechas de formato variable
            _ => "TEXT"
        };

        var defs = columnas.Select(c => $"    \"{c.NombreDB}\" {TipoSQL(c.Clave)}");
        string sql = $"CREATE TABLE IF NOT EXISTS \"{tabla}\" (\n{string.Join(",\n", defs)}\n);";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private static void InsertarItemPostgreSQL(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string tabla,
        DataItem item,
        List<(string NombreDB, string Clave, string Display)> columnas,
        List<(string Display, string Clave)> infoColumnas)
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
                if (int.TryParse(rawVal, out int idV))
                    dbVal = idV;
                else
                    dbVal = DBNull.Value;
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

            CrearTablaMariaDB(conn, tabla, columnas);

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
                        InsertarItemMariaDB(conn, tx, tabla, item, columnas, infoColumnas);
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

    private static void CrearTablaMariaDB(
        MySqlConnection conn,
        string tabla,
        List<(string NombreDB, string Clave, string Display)> columnas)
    {
        string TipoSQL(string clave) => clave switch
        {
            "id" => "INT",
            "valor" => "DOUBLE",
            _ => "TEXT"
        };

        var defs = columnas.Select(c => $"    `{c.NombreDB}` {TipoSQL(c.Clave)}");
        string sql = $"CREATE TABLE IF NOT EXISTS `{tabla}` (\n{string.Join(",\n", defs)}\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private static void InsertarItemMariaDB(
        MySqlConnection conn,
        MySqlTransaction tx,
        string tabla,
        DataItem item,
        List<(string NombreDB, string Clave, string Display)> columnas,
        List<(string Display, string Clave)> infoColumnas)
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
                if (int.TryParse(rawVal, out int idV))
                    dbVal = idV;
                else
                    dbVal = DBNull.Value;
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

    private static string SanitizarNombre(string nombre)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in nombre)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        string s = sb.ToString().Trim('_');
        if (s.Length == 0) return "campo";
        if (char.IsDigit(s[0])) s = "_" + s;
        return s.ToLowerInvariant();
    }
}

public class WriteResult
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = "";
    public int Insertados { get; set; }
    public int Errores { get; set; }
}

