using MySqlConnector;
using ExploradorArchivos.AppDataFusion.Models;
using System.Globalization;

namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Conector para base de datos MariaDB / MySQL.
/// Se encarga de abrir conexiones, recuperar la metadata de columnas,
/// buscar claves primarias, y mapear filas a objetos DataItem usando heurísticas semánticas.
/// </summary>
public class MariaDbConnector : IDbConnector
{
    public string CadenaConexion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int LimiteFilas { get; set; } = 0;

    public List<string> UltimasColumnas { get; private set; } = new();
    public Dictionary<string, string> MapeoColumnas { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Nombre real de la columna PRIMARY KEY en la tabla de la BD.
    /// Se detecta automáticamente al leer columnas o datos.
    /// </summary>
    public string? ColPrimaryKey { get; private set; }

    // Flag: el usuario ya eligió el mapeo manualmente.
    private bool _mapeoConfirmadoPorUsuario = false;

    public MariaDbConnector() { }
    public MariaDbConnector(string cadenaConexion, string tabla)
    {
        CadenaConexion = cadenaConexion;
        Tabla = tabla;
    }

    /// <summary>
    /// Método: ObtenerNombresColumnas
    /// - Inicializa: MySqlConnection y MySqlCommand.
    /// - Objetos: MySqlDataReader (lector de esquema).
    /// - Operación: Consulta la estructura de la tabla con LIMIT 0 para listar columnas y obtener la clave primaria.
    /// </summary>
    public List<string> ObtenerNombresColumnas()
    {
        var cols = new List<string>();
        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();

            using var cmd = new MySqlCommand($"SELECT * FROM `{Tabla}` LIMIT 0", conn);
            using var r = cmd.ExecuteReader();
            
            for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
            r.Close();

            ColPrimaryKey = ObtenerPrimaryKeyColumna(conn);
        }
        catch (Exception ex)
        { 
            Console.WriteLine($"[MariaDB] ObtenerNombresColumnas: {ex.Message}"); 
        }

        UltimasColumnas = [..cols];
        ActualizarMapeoAutomatico(cols);
        _mapeoConfirmadoPorUsuario = false;
        return cols;
    }

    /// <summary>
    /// Método: ObtenerPrimaryKeyColumna
    /// - Inicializa: MySqlCommand.
    /// - Operación: Consulta INFORMATION_SCHEMA para recuperar la columna Primary Key o un índice UNIQUE; usa la primera columna como fallback.
    /// </summary>
    private string? ObtenerPrimaryKeyColumna(MySqlConnection conn)
    {
        try
        {
            string? GetColumn(string sqlQuery)
            {
                using var cmd = new MySqlCommand(sqlQuery, conn);
                cmd.Parameters.AddWithValue("@schema", conn.Database);
                cmd.Parameters.AddWithValue("@tabla", Tabla);
                return cmd.ExecuteScalar()?.ToString();
            }

            string sqlPk = @"SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                WHERE TABLE_SCHEMA = @schema 
                  AND TABLE_NAME   = @tabla 
                  AND CONSTRAINT_NAME = 'PRIMARY' 
                ORDER BY ORDINAL_POSITION 
                LIMIT 1";
            if (GetColumn(sqlPk) is { } pk && !string.IsNullOrEmpty(pk))
            {
                Console.WriteLine($"[MariaDB] PK detectada (PRIMARY KEY): '{pk}'");
                return pk;
            }

            string sqlUnique = @"SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.STATISTICS 
                WHERE TABLE_SCHEMA = @schema 
                  AND TABLE_NAME   = @tabla 
                  AND NON_UNIQUE   = 0 
                  AND INDEX_NAME  != 'PRIMARY' 
                ORDER BY SEQ_IN_INDEX 
                LIMIT 1";
            if (GetColumn(sqlUnique) is { } uq && !string.IsNullOrEmpty(uq))
            {
                Console.WriteLine($"[MariaDB] PK detectada (UNIQUE index): '{uq}'");
                return uq;
            }

            if (UltimasColumnas.Count > 0)
            {
                string firstCol = UltimasColumnas[0];
                Console.WriteLine($"[MariaDB] ⚠ Sin PK/UNIQUE — usando primera columna como identificador: '{firstCol}'");
                return firstCol;
            }

            Console.WriteLine($"[MariaDB] ⚠ No se pudo determinar columna identificadora para '{Tabla}'");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MariaDB] ObtenerPrimaryKey error: {ex.Message}");
            if (UltimasColumnas.Count > 0)
            {
                Console.WriteLine($"[MariaDB] Fallback de emergencia: primera columna '{UltimasColumnas[0]}'");
                return UltimasColumnas[0];
            }
            return null;
        }
    }

    /// <summary>
    /// Método: SobreescribirMapeo
    /// - Operación: Asigna las columnas mapeadas por el usuario a los campos estándar de DataItem, anulando el auto-mapeo.
    /// </summary>
    public void SobreescribirMapeo(
        string colId,
        string colCategoria, string colValor,
        string colNombre, string colFecha)
    {
        if (string.IsNullOrWhiteSpace(colId))
        {
            var aliases = new[] { "id", "_id", "codigo", "code", "num", "numero", "idx", "index", "rank", "no", "student_id", "car_id", "usedcarskuid", "sku_id", "sku" };
            foreach (var a in aliases)
            {
                colId = UltimasColumnas.FirstOrDefault(c => string.Equals(c, a, StringComparison.OrdinalIgnoreCase)) ?? "";
                if (!string.IsNullOrEmpty(colId)) break;
            }
        }

        MapeoColumnas.Clear();
        if (!string.IsNullOrWhiteSpace(colId)) MapeoColumnas[colId] = "id";

        void Asignar(string? columna, string rol)
        {
            if (string.IsNullOrWhiteSpace(columna)) return;
            MapeoColumnas.TryAdd(columna, rol);
        }

        Asignar(colCategoria, "categoria");
        Asignar(colValor, "valor");
        Asignar(colNombre, "nombre");
        Asignar(colFecha, "fecha");

        _mapeoConfirmadoPorUsuario = true;
    }

    /// <summary>
    /// Método: LeerDatos
    /// - Inicializa: MySqlConnection, MySqlCommand, Dictionary (mapeo de índices).
    /// - Objetos: MySqlDataReader (lector de registros).
    /// - Operación: Recupera y mapea los registros de la tabla MariaDB a objetos DataItem rellenando CamposExtra heurísticamente.
    /// </summary>
    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();
        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[MariaDB] ✓  Conectado a {conn.Database}");

            ColPrimaryKey = ObtenerPrimaryKeyColumna(conn);

            string sql = LimiteFilas > 0
                ? $"SELECT * FROM `{Tabla}` LIMIT {LimiteFilas}"
                : $"SELECT * FROM `{Tabla}`";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            using var reader = cmd.ExecuteReader();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++) mapa[reader.GetName(i)] = i;
            UltimasColumnas = mapa.Keys.ToList();

            if (!_mapeoConfirmadoPorUsuario)
                ActualizarMapeoAutomatico(mapa.Keys);

            if (!string.IsNullOrEmpty(ColPrimaryKey) &&
                !MapeoColumnas.ContainsKey(ColPrimaryKey))
            {
                if (!MapeoColumnas.Values.Any(v => v == "id"))
                    MapeoColumnas[ColPrimaryKey] = "id";
            }

            string? colId = MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;
            string? colNom = MapeoColumnas.FirstOrDefault(kv => kv.Value == "nombre").Key;
            string? colCat = MapeoColumnas.FirstOrDefault(kv => kv.Value == "categoria").Key;
            string? colVal = MapeoColumnas.FirstOrDefault(kv => kv.Value == "valor").Key;
            string? colFec = MapeoColumnas.FirstOrDefault(kv => kv.Value == "fecha").Key;

            string? EncontrarColumnaBd(string? colUi)
            {
                if (string.IsNullOrEmpty(colUi)) return null;
                string norm = DataItem.NormalizarParaComparar(colUi);
                foreach (var k in mapa.Keys)
                {
                    if (DataItem.NormalizarParaComparar(k) == norm)
                        return k;
                }
                return null;
            }

            string? dbColId = EncontrarColumnaBd(colId);
            string? dbColNom = EncontrarColumnaBd(colNom);
            string? dbColCat = EncontrarColumnaBd(colCat);
            string? dbColVal = EncontrarColumnaBd(colVal);
            string? dbColFec = EncontrarColumnaBd(colFec);

            var mappedDbCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (dbColId != null) mappedDbCols.Add(dbColId);
            if (dbColNom != null) mappedDbCols.Add(dbColNom);
            if (dbColCat != null) mappedDbCols.Add(dbColCat);
            if (dbColVal != null) mappedDbCols.Add(dbColVal);
            if (dbColFec != null) mappedDbCols.Add(dbColFec);

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "mariadb" };

                bool parsedOk = false;
                int idV = 0;
                string? rawIdVal = null;
                if (dbColId != null && mapa.TryGetValue(dbColId, out int iId) && !reader.IsDBNull(iId))
                {
                    rawIdVal = reader[iId].ToString();
                    parsedOk = int.TryParse(rawIdVal, out idV);
                }

                item.Id = parsedOk ? idV : contador;

                if (dbColId != null)
                {
                    if (string.IsNullOrEmpty(rawIdVal) && mapa.TryGetValue(dbColId, out int iId2) && !reader.IsDBNull(iId2))
                        rawIdVal = reader[iId2]?.ToString();

                    if (!string.IsNullOrEmpty(rawIdVal))
                        item.CamposExtra[dbColId] = rawIdVal;
                }

                item.Nombre = LeerStr(reader, mapa, dbColNom) ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, dbColCat) ?? "Sin categoría";
                item.Valor = LeerDbl(reader, mapa, dbColVal) ?? 0;
                item.Fecha = LeerDate(reader, mapa, dbColFec) ?? DateTime.Now;

                foreach (var kv in mapa)
                {
                    if (mappedDbCols.Contains(kv.Key)) continue;
                    if (!reader.IsDBNull(kv.Value))
                        item.CamposExtra[kv.Key] = reader[kv.Value]?.ToString() ?? "";
                }

                lista.Add(item);
                contador++;
                if (contador % 10_000 == 0)
                    Console.WriteLine($"[MariaDB]    ... {contador} registros leídos");
            }
            Console.WriteLine($"[MariaDB] ✓  {lista.Count} registros leídos. " +
                $"Cat={dbColCat ?? "—"} Val={dbColVal ?? "—"} Nom={dbColNom ?? "—"}");
            
            EnriquecerCamposFaltantes(lista, dbColCat, dbColVal, dbColNom);
        }
        catch (Exception ex) { Console.WriteLine($"[MariaDB] ✗  Error: {ex.Message}"); }
        return lista;
    }

    /// <summary>
    /// Método: ProbarConexion
    /// - Inicializa: MySqlConnection y MySqlCommand.
    /// - Operación: Ejecuta SELECT COUNT(*) para verificar la conexión y la existencia de la tabla.
    /// </summary>
    public bool ProbarConexion(out string mensaje)
    {
        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `{Tabla}`", conn);
            long total = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
            mensaje = $"Conexión exitosa · DB: {conn.Database} · Servidor: {conn.DataSource} · Filas en '{Tabla}': {total:N0}";
            return true;
        }
        catch (Exception ex) { mensaje = $"Error de conexión: {ex.Message}"; return false; }
    }

    /// <summary>
    /// Método: ActualizarMapeoAutomatico
    /// - Operación: Compara los nombres de columnas contra arreglos de alias semánticos para asociar cada rol.
    /// </summary>
    private void ActualizarMapeoAutomatico(IEnumerable<string>? cols = null)
    {
        var src = (cols ?? UltimasColumnas).ToList();
        MapeoColumnas.Clear();

        string? Find(params string[] aliases) =>
            aliases.FirstOrDefault(alias => src.Any(col => string.Equals(col, alias, StringComparison.OrdinalIgnoreCase)));

        if (Find("id", "_id", "codigo", "code", "num", "numero", "idx", "index", "rank", "no", "student_id", "car_id", "usedcarskuid", "sku_id", "sku") is { } cId) MapeoColumnas[cId] = "id";
        if (Find("nombre", "name", "titulo", "title", "pais", "country", "jugador", "player", "empleado", "employee", "producto", "item", "micrositio", "descripcion", "description", "persona", "person", "autor", "author", "atleta", "athlete", "brand", "marca", "model", "modelo") is { } cNom) MapeoColumnas[cNom] = "nombre";
        if (Find("categoria", "category", "genero", "genre", "gender", "sexo", "sex", "region", "tipo", "type", "departamento", "department", "level", "nivel", "segmento", "segment", "grupo", "group_name", "group", "clasificacion", "clase", "class", "division") is { } cCat) MapeoColumnas[cCat] = "categoria";
        if (Find("valor", "value", "precio", "price", "ventas_global", "ventas", "sales", "score", "puntos", "salario", "puntaje", "total", "monto", "amount", "importe", "cost", "costo", "revenue", "ingreso", "metrica", "Numero_de_notas_transmitidas", "salary", "points", "frec", "frecuencia", "frequency", "count", "cantidad", "edad_media", "edad", "age", "promedio", "avg") is { } cVal) MapeoColumnas[cVal] = "valor";
        if (Find("fecha", "date", "fecha_lanzamiento", "anio", "year", "created_at", "updated_at", "timestamp", "fecha_registro", "fecha_reporte", "periodo", "fechaContratacion", "hireDate", "period") is { } cFec) MapeoColumnas[cFec] = "fecha";
    }

    private static string? LeerStr(MySqlDataReader r, Dictionary<string, int> m, string? col) =>
        col != null && m.TryGetValue(col, out int i) && !r.IsDBNull(i) && r[i]?.ToString() is { } v && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static double? LeerDbl(MySqlDataReader r, Dictionary<string, int> m, string? col) =>
        col != null && m.TryGetValue(col, out int i) && !r.IsDBNull(i) && double.TryParse(r[i].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : null;

    private static DateTime? LeerDate(MySqlDataReader r, Dictionary<string, int> m, string? col) =>
        col != null && m.TryGetValue(col, out int i) && !r.IsDBNull(i) && DateTime.TryParse(r[i].ToString(), out DateTime d) ? d : null;

    /// <summary>
    /// Método: EnriquecerCamposFaltantes
    /// - Operación: Autodetecta campos vacíos en el DataItem analizando dinámicamente las columnas de CamposExtra mediante heurísticas.
    /// </summary>
    private static void EnriquecerCamposFaltantes(
        List<DataItem> items, string? colCategoria, string? colValor, string? colNombre)
    {
        if (items.Count == 0) return;

        bool faltaCategoria = string.IsNullOrWhiteSpace(colCategoria);
        bool faltaValor = string.IsNullOrWhiteSpace(colValor);
        bool faltaNombre = string.IsNullOrWhiteSpace(colNombre);

        string? kCategoria = faltaCategoria ? BuscarMejorClaveCategoria(items) : null;
        string? kValor = faltaValor ? BuscarMejorClaveNumerica(items) : null;
        string? kNombre = faltaNombre ? BuscarMejorClaveTexto(items, kCategoria) : null;

        foreach (var item in items)
        {
            if (kCategoria != null &&
                (string.IsNullOrWhiteSpace(item.Categoria) || item.Categoria == "Sin categoría") &&
                item.CamposExtra.TryGetValue(kCategoria, out var cat) &&
                !string.IsNullOrWhiteSpace(cat))
                item.Categoria = cat.Trim();

            if (kValor != null && Math.Abs(item.Valor) < 0.0000001 &&
                item.CamposExtra.TryGetValue(kValor, out var raw) &&
                double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                item.Valor = v;

            if (kNombre != null &&
                item.Nombre.StartsWith("Registro-", StringComparison.OrdinalIgnoreCase) &&
                item.CamposExtra.TryGetValue(kNombre, out var nombre) &&
                !string.IsNullOrWhiteSpace(nombre))
                item.Nombre = nombre.Trim();
        }
    }

    /// <summary>
    /// Método: BuscarMejorClaveCategoria
    /// - Operación: Califica cada columna según el porcentaje de valores textuales no vacíos y únicos, estimando cuál funciona como Categoría.
    /// </summary>
    private static string? BuscarMejorClaveCategoria(List<DataItem> items)
    {
        return items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                var values = items.Select(item => item.CamposExtra.TryGetValue(key, out var raw) ? raw : null)
                                  .Where(v => !string.IsNullOrWhiteSpace(v))
                                  .Select(v => v!.Trim())
                                  .ToList();
                int noVacios = values.Count;
                int noNumericos = values.Count(v => !double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _));
                var unicos = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
                
                bool valido = noVacios > 0 && 
                              noNumericos >= Math.Max(2, noVacios / 2) && 
                              unicos.Count > 1 && 
                              unicos.Count <= Math.Max(2, items.Count - 1);
                              
                int puntaje = noVacios + Math.Min(unicos.Count * 2, 30);
                return new { Key = key, Puntaje = puntaje, Valido = valido };
            })
            .Where(x => x.Valido)
            .OrderByDescending(x => x.Puntaje)
            .Select(x => x.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// Método: BuscarMejorClaveNumerica
    /// - Operación: Encuentra la columna con el mayor número de elementos que puedan convertirse exitosamente a número decimal.
    /// </summary>
    private static string? BuscarMejorClaveNumerica(List<DataItem> items)
    {
        return items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => new
            {
                Key = key,
                Count = items.Count(item => item.CamposExtra.TryGetValue(key, out var raw) &&
                                            double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            })
            .Where(x => x.Count >= Math.Max(2, items.Count / 3))
            .OrderByDescending(x => x.Count)
            .Select(x => x.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// Método: BuscarMejorClaveTexto
    /// - Operación: Identifica la columna de texto descriptivo sobrante que mejor actúe como Nombre del registro.
    /// </summary>
    private static string? BuscarMejorClaveTexto(List<DataItem> items, string? evitar)
    {
        return items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !string.Equals(k, evitar, StringComparison.OrdinalIgnoreCase))
            .Select(key =>
            {
                var values = items.Select(item => item.CamposExtra.TryGetValue(key, out var raw) ? raw : null)
                                  .Where(v => !string.IsNullOrWhiteSpace(v))
                                  .ToList();
                int noVacios = values.Count;
                int noNumericos = values.Count(v => !double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _));
                return new { Key = key, NoVacios = noVacios, Valido = noVacios > 0 && noNumericos >= Math.Max(2, noVacios / 2) };
            })
            .Where(x => x.Valido)
            .OrderByDescending(x => x.NoVacios)
            .Select(x => x.Key)
            .FirstOrDefault();
    }
}