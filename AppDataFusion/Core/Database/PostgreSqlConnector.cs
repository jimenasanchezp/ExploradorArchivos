using Npgsql;
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Conector para base de datos PostgreSQL.
/// Se encarga de abrir conexiones, recuperar la metadata de columnas,
/// buscar claves primarias y mapear filas a objetos DataItem.
/// </summary>
public class PostgreSqlConnector
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

    private bool _mapeoConfirmadoPorUsuario = false;

    public PostgreSqlConnector() { }
    public PostgreSqlConnector(string cadenaConexion, string tabla)
    { 
        CadenaConexion = cadenaConexion; 
        Tabla = tabla; 
    }

    /// <summary>
    /// Método: ObtenerNombresColumnas
    /// - Inicializa: NpgsqlConnection y NpgsqlCommand.
    /// - Objetos: NpgsqlDataReader (lector de esquema).
    /// - Operación: Ejecuta SELECT con LIMIT 0 para recuperar rápidamente las columnas de la tabla y detecta la clave primaria.
    /// </summary>
    public List<string> ObtenerNombresColumnas()
    {
        var cols = new List<string>();
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            
            using var cmd = new NpgsqlCommand($"SELECT * FROM {Tabla} LIMIT 0", conn);
            using var r = cmd.ExecuteReader();
            
            for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
            
            ColPrimaryKey = ObtenerPrimaryKeyColumna(conn);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ObtenerNombresColumnas: {ex.Message}");
        }

        UltimasColumnas = [..cols];
        ActualizarMapeoAutomatico(cols);
        _mapeoConfirmadoPorUsuario = false;
        return cols;
    }

    /// <summary>
    /// Método: ObtenerPrimaryKeyColumna
    /// - Inicializa: NpgsqlCommand.
    /// - Operación: Consulta information_schema buscando constraints PRIMARY KEY o UNIQUE; retorna la primera columna de la tabla como fallback.
    /// </summary>
    private string? ObtenerPrimaryKeyColumna(NpgsqlConnection conn)
    {
        try
        {
            string? GetColumn(string constraintType)
            {
                string sql = $@"SELECT kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_name = kcu.constraint_name
                        AND tc.table_schema   = kcu.table_schema
                    WHERE tc.constraint_type = '{constraintType}'
                      AND tc.table_name = @tabla
                    ORDER BY kcu.ordinal_position
                    LIMIT 1";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@tabla", Tabla.ToLowerInvariant());
                return cmd.ExecuteScalar()?.ToString();
            }

            if (GetColumn("PRIMARY KEY") is { } pk && !string.IsNullOrEmpty(pk))
            {
                Console.WriteLine($"[PostgreSQL] PK detectada (PRIMARY KEY): '{pk}'");
                return pk;
            }

            if (GetColumn("UNIQUE") is { } uq && !string.IsNullOrEmpty(uq))
            {
                Console.WriteLine($"[PostgreSQL] PK detectada (UNIQUE): '{uq}'");
                return uq;
            }

            if (UltimasColumnas.Count > 0)
            {
                string firstCol = UltimasColumnas[0];
                Console.WriteLine($"[PostgreSQL] ⚠ Sin PK/UNIQUE — usando primera columna: '{firstCol}'");
                return firstCol;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ObtenerPrimaryKey error: {ex.Message}");
            if (UltimasColumnas.Count > 0)
            {
                Console.WriteLine($"[PostgreSQL] Fallback de emergencia: primera columna '{UltimasColumnas[0]}'");
                return UltimasColumnas[0];
            }
            return null;
        }
    }

    /// <summary>
    /// Método: SobreescribirMapeo
    /// - Operación: Mapea manualmente las columnas proporcionadas por el usuario a los campos estándar de DataItem.
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
    /// - Inicializa: NpgsqlConnection y NpgsqlCommand.
    /// - Objetos: NpgsqlDataReader (lector de registros).
    /// - Operación: Obtiene los registros de PostgreSQL, valida que la tabla exista, mapea dinámicamente cada columna al DataItem y enriquece valores faltantes.
    /// </summary>
    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[PostgreSQL] ✓  Conectado a {conn.Database}");

            var colsInfo = ObtenerColumnasInfo(conn, Tabla);
            if (colsInfo.Count == 0)
            {
                Console.WriteLine($"[PostgreSQL] ⚠  Tabla '{Tabla}' no encontrada.");
                return lista;
            }

            ColPrimaryKey = ObtenerPrimaryKeyColumna(conn);

            string sql = LimiteFilas > 0
                ? $"SELECT * FROM {Tabla} LIMIT {LimiteFilas}"
                : $"SELECT * FROM {Tabla}";

            using var cmd = new NpgsqlCommand(sql, conn);
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

            var mapeadasSet = new HashSet<string>(MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "postgresql" };

                bool parsedOk = false;
                int idV = 0;
                string? rawIdVal = null;
                if (colId != null && mapa.TryGetValue(colId, out int iId) && !reader.IsDBNull(iId))
                {
                    rawIdVal = reader[iId].ToString();
                    parsedOk = int.TryParse(rawIdVal, out idV);
                }

                item.Id = parsedOk ? idV : contador;

                if (colId != null)
                {
                    if (string.IsNullOrEmpty(rawIdVal) && mapa.TryGetValue(colId, out int iId2) && !reader.IsDBNull(iId2))
                        rawIdVal = reader[iId2]?.ToString();

                    if (!string.IsNullOrEmpty(rawIdVal))
                        item.CamposExtra[colId] = rawIdVal;
                }

                item.Nombre = LeerStr(reader, mapa, colNom) ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, colCat) ?? "Sin categoría";
                item.Valor = LeerDbl(reader, mapa, colVal) ?? 0;
                item.Fecha = LeerDate(reader, mapa, colFec) ?? DateTime.Now;

                foreach (var kv in mapa)
                {
                    if (mapeadasSet.Contains(kv.Key)) continue;
                    if (!reader.IsDBNull(kv.Value))
                        item.CamposExtra[kv.Key] = reader[kv.Value].ToString() ?? "";
                }

                lista.Add(item);
                contador++;
                if (contador % 10_000 == 0)
                    Console.WriteLine($"[PostgreSQL]    ... {contador} registros leídos");
            }
            Console.WriteLine($"[PostgreSQL] ✓  {lista.Count} registros leídos. " +
                $"Cat={colCat ?? "—"} Val={colVal ?? "—"} Nom={colNom ?? "—"}");
            
            EnriquecerCamposFaltantes(lista, colCat, colVal, colNom);
        }
        catch (Exception ex) { Console.WriteLine($"[PostgreSQL] ✗  Error: {ex.Message}"); }
        return lista;
    }

    /// <summary>
    /// Método: ProbarConexion
    /// - Inicializa: NpgsqlConnection y NpgsqlCommand.
    /// - Operación: Ejecuta una consulta COUNT para validar la conexión y reportar el total de registros en la tabla.
    /// </summary>
    public bool ProbarConexion(out string mensaje)
    {
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {Tabla}", conn);
            long total = (long)(cmd.ExecuteScalar() ?? 0L);
            mensaje = $"Conexión exitosa · DB: {conn.Database} · Servidor: {conn.Host} · Filas en '{Tabla}': {total:N0}";
            return true;
        }
        catch (Exception ex) { mensaje = $"Error de conexión: {ex.Message}"; return false; }
    }

    /// <summary>
    /// Método: ObtenerColumnasInfo
    /// - Inicializa: NpgsqlCommand.
    /// - Operación: Consulta information_schema.columns para validar que las columnas existan en la tabla física.
    /// </summary>
    private List<string> ObtenerColumnasInfo(NpgsqlConnection conn, string tabla)
    {
        var cols = new List<string>();
        try
        {
            using var cmd = new NpgsqlCommand(
                $"SELECT column_name FROM information_schema.columns WHERE table_name='{tabla.ToLower()}'",
                conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(0));
        }
        catch { }
        return cols;
    }

    /// <summary>
    /// Método: ActualizarMapeoAutomatico
    /// - Operación: Mapea semánticamente columnas de base de datos a los roles estándar comparándolas con arreglos de alias.
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

    private static string? LeerStr(NpgsqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        var v = r[i]?.ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static double? LeerDbl(NpgsqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return double.TryParse(r[i].ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private static DateTime? LeerDate(NpgsqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return DateTime.TryParse(r[i].ToString(), out DateTime d) ? d : null;
    }

    /// <summary>
    /// Método: EnriquecerCamposFaltantes
    /// - Operación: Autodetecta campos vacíos en el DataItem analizando las columnas de CamposExtra mediante heurísticas.
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
                double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
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
    /// - Operación: Califica dinámicamente cada columna de CamposExtra buscando textos repetitivos idóneos para Categoría.
    /// </summary>
    private static string? BuscarMejorClaveCategoria(List<DataItem> items)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        string? mejor = null; // variable para almacenar la mejor clave encontrada
        int mejorPuntaje = 0;

        foreach (var key in candidatos)
        {
            int noVacios = 0, noNumericos = 0;
            var unicos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (!item.CamposExtra.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) continue;
                noVacios++;
                var v = raw.Trim();
                unicos.Add(v);
                bool esNumero = double.TryParse(v, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
                if (!esNumero) noNumericos++;
            }

            if (noVacios == 0 || noNumericos < Math.Max(2, noVacios / 2)) continue;
            if (unicos.Count <= 1 || unicos.Count > Math.Max(2, items.Count - 1)) continue;

            int puntaje = noVacios + Math.Min(unicos.Count * 2, 30);
            if (puntaje > mejorPuntaje) { mejorPuntaje = puntaje; mejor = key; }
        }
        return mejor;
    }

    /// <summary>
    /// Método: BuscarMejorClaveNumerica
    /// - Operación: Encuentra la columna adicional que albergue la mayor cantidad de valores que se puedan convertir a números double.
    /// </summary>
    private static string? BuscarMejorClaveNumerica(List<DataItem> items)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        string? mejor = null;
        int mejorPuntaje = 0;

        foreach (var key in candidatos)
        {
            int numericos = 0;
            foreach (var item in items)
                if (item.CamposExtra.TryGetValue(key, out var raw) &&
                    double.TryParse(raw, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    numericos++;

            if (numericos < Math.Max(2, items.Count / 3)) continue;
            if (numericos > mejorPuntaje) { mejorPuntaje = numericos; mejor = key; }
        }
        return mejor;
    }

    /// <summary>
    /// Método: BuscarMejorClaveTexto
    /// - Operación: Selecciona una columna de texto razonable para fungir como el Nombre del registro, evitando la de Categoría.
    /// </summary>
    private static string? BuscarMejorClaveTexto(List<DataItem> items, string? evitar)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !string.Equals(k, evitar, StringComparison.OrdinalIgnoreCase));

        string? mejor = null;
        int mejorPuntaje = 0;

        foreach (var key in candidatos)
        {
            int noVacios = 0, noNumericos = 0;
            foreach (var item in items)
            {
                if (!item.CamposExtra.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) continue;
                noVacios++;
                bool esNumero = double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
                if (!esNumero) noNumericos++;
            }
            if (noVacios == 0 || noNumericos < Math.Max(2, noVacios / 2)) continue;
            if (noVacios > mejorPuntaje) { mejorPuntaje = noVacios; mejor = key; }
        }
        return mejor;
    }
}
