using MySqlConnector;
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Database;

public class MariaDbConnector
{
    public string CadenaConexion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int LimiteFilas { get; set; } = 0;

    public List<string> UltimasColumnas { get; private set; } = new();
    public Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Nombre real de la columna PRIMARY KEY en la tabla de la BD.
    /// Se detecta automáticamente al leer columnas o datos.
    /// </summary>
    public string? ColPrimaryKey { get; private set; }

    // Flag: el usuario ya eligió el mapeo manualmente → no tocar en LeerDatos
    private bool _mapeoConfirmadoPorUsuario = false;

    public MariaDbConnector() { }
    public MariaDbConnector(string cadenaConexion, string tabla)
    { CadenaConexion = cadenaConexion; Tabla = tabla; }

    /// <summary>
    /// Consulta la tabla en MariaDB limitando a 0 filas para extraer rápidamente
    /// los metadatos y nombres reales de las columnas disponibles.
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
            // Detectar clave primaria real desde INFORMATION_SCHEMA
            ColPrimaryKey = ObtenerPrimaryKeyColumna(conn);
        }
        catch (Exception ex)
        { Console.WriteLine($"[MariaDB] ObtenerNombresColumnas: {ex.Message}"); }

        UltimasColumnas = new List<string>(cols);
        ActualizarMapeoAutomatico(cols);
        _mapeoConfirmadoPorUsuario = false;
        return cols;
    }

    /// <summary>Consulta INFORMATION_SCHEMA para obtener el nombre real de la PRIMARY KEY o un índice único.</summary>
    private string? ObtenerPrimaryKeyColumna(MySqlConnection conn)
    {
        try
        {
            // Intento 1: Clave PRIMARY KEY
            string sql = @"SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                WHERE TABLE_SCHEMA = @schema 
                  AND TABLE_NAME   = @tabla 
                  AND CONSTRAINT_NAME = 'PRIMARY' 
                ORDER BY ORDINAL_POSITION 
                LIMIT 1";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@schema", conn.Database);
            cmd.Parameters.AddWithValue("@tabla", Tabla);
            var result = cmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(result))
            {
                Console.WriteLine($"[MariaDB] PK detectada (PRIMARY KEY): '{result}'");
                return result;
            }

            // Intento 2: Índice UNIQUE (primer campo del primer índice único)
            string sqlUnique = @"SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.STATISTICS 
                WHERE TABLE_SCHEMA = @schema2 
                  AND TABLE_NAME   = @tabla2 
                  AND NON_UNIQUE   = 0 
                  AND INDEX_NAME  != 'PRIMARY' 
                ORDER BY SEQ_IN_INDEX 
                LIMIT 1";
            using var cmd2 = new MySqlCommand(sqlUnique, conn);
            cmd2.Parameters.AddWithValue("@schema2", conn.Database);
            cmd2.Parameters.AddWithValue("@tabla2", Tabla);
            var result2 = cmd2.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(result2))
            {
                Console.WriteLine($"[MariaDB] PK detectada (UNIQUE index): '{result2}'");
                return result2;
            }

            // Intento 3: Primera columna de la tabla (fallback más robusto)
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
            // Fallback de emergencia: primera columna conocida
            if (UltimasColumnas.Count > 0)
            {
                Console.WriteLine($"[MariaDB] Fallback de emergencia: primera columna '{UltimasColumnas[0]}'");
                return UltimasColumnas[0];
            }
            return null;
        }
    }

    /// <summary>
    /// Permite al usuario forzar un mapeo manual de las columnas (roles: id, categoría, valor, nombre, fecha),
    /// anulando la inferencia automática basada en expresiones regulares.
    /// </summary>
    public void SobreescribirMapeo(
        string colId,
        string colCategoria, string colValor,
        string colNombre, string colFecha)
    {
        // Si no se proporcionó un ID, intentar buscar uno automáticamente
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
    /// Abre la conexión a MariaDB, recupera todos los registros de la tabla configurada y
    /// los mapea uno a uno hacia objetos <see cref="DataItem"/> en memoria.
    /// </summary>
    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();
        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[MariaDB] ✓  Conectado a {conn.Database}");

            // Detectar PRIMARY KEY real antes de leer datos
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

            // Si la PK fue detectada y no está en MapeoColumnas como "id", agregarla
            if (!string.IsNullOrEmpty(ColPrimaryKey) &&
                !MapeoColumnas.ContainsKey(ColPrimaryKey))
            {
                // Solo agregamos si no hay ya una columna mapeada como "id"
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
                var item = new DataItem { Fuente = "mariadb" };

                bool parsedOk = false;
                int idV = 0;
                string? rawIdVal = null;
                if (colId != null && mapa.TryGetValue(colId, out int iId) && !reader.IsDBNull(iId))
                {
                    rawIdVal = reader[iId].ToString();
                    parsedOk = int.TryParse(rawIdVal, out idV);
                }

                item.Id = parsedOk ? idV : contador;

                // IMPORTANTE: Siempre guardar el valor del identificador en CamposExtra
                // (aunque la columna esté mapeada a otro rol), para poder usar en el WHERE del UPDATE
                if (colId != null)
                {
                    // Leer el valor directamente del reader si no se leyó antes
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
                        item.CamposExtra[kv.Key] = reader[kv.Value]?.ToString() ?? "";
                }

                lista.Add(item);
                contador++;
                if (contador % 10_000 == 0)
                    Console.WriteLine($"[MariaDB]    ... {contador} registros leídos");
            }
            Console.WriteLine($"[MariaDB] ✓  {lista.Count} registros leídos. " +
                $"Cat={colCat ?? "—"} Val={colVal ?? "—"} Nom={colNom ?? "—"}");
            EnriquecerCamposFaltantes(lista, colCat, colVal, colNom);
        }
        catch (Exception ex) { Console.WriteLine($"[MariaDB] ✗  Error: {ex.Message}"); }
        return lista;
    }

    /// <summary>
    /// Ejecuta una consulta rápida (COUNT) para verificar que la cadena de conexión
    /// sea válida y la tabla exista en MariaDB. Retorna el resultado en el mensaje de salida.
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
    /// Analiza los nombres de las columnas obtenidas de la BD y busca palabras clave comunes
    /// (ej. "price", "ventas", "name", "fecha") para asignar automáticamente cada columna al rol correspondiente.
    /// </summary>
    private void ActualizarMapeoAutomatico(IEnumerable<string>? cols = null)
    {
        var src = (cols ?? UltimasColumnas).ToList();
        MapeoColumnas.Clear();

        string? Find(params string[] alias)
        {
            foreach (var a in alias)
                foreach (var c in src)
                    if (string.Equals(c, a, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        string? c;
        c = Find("id", "_id", "codigo", "code", "num", "numero", "idx", "index", "rank", "no", "student_id", "car_id", "usedcarskuid", "sku_id", "sku"); if (c != null) MapeoColumnas[c] = "id";
        c = Find("nombre", "name", "titulo", "title", "pais", "country", "jugador", "player", "empleado", "employee", "producto", "item", "micrositio", "descripcion", "description", "persona", "person", "autor", "author", "atleta", "athlete", "brand", "marca", "model", "modelo"); if (c != null) MapeoColumnas[c] = "nombre";
        c = Find("categoria", "category", "genero", "genre", "gender", "sexo", "sex", "region", "tipo", "type", "departamento", "department", "level", "nivel", "segmento", "segment", "grupo", "group_name", "group", "clasificacion", "clase", "class", "division"); if (c != null) MapeoColumnas[c] = "categoria";
        c = Find("valor", "value", "precio", "price", "ventas_global", "ventas", "sales", "score", "puntos", "salario", "puntaje", "total", "monto", "amount", "importe", "cost", "costo", "revenue", "ingreso", "metrica", "Numero_de_notas_transmitidas", "salary", "points", "frec", "frecuencia", "frequency", "count", "cantidad", "edad_media", "edad", "age", "promedio", "avg"); if (c != null) MapeoColumnas[c] = "valor";
        c = Find("fecha", "date", "fecha_lanzamiento", "anio", "year", "created_at", "updated_at", "timestamp", "fecha_registro", "fecha_reporte", "periodo", "fechaContratacion", "hireDate", "period"); if (c != null) MapeoColumnas[c] = "fecha";
    }

    private static string? LeerStr(MySqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        var v = r[i]?.ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static double? LeerDbl(MySqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return double.TryParse(r[i].ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private static DateTime? LeerDate(MySqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return DateTime.TryParse(r[i].ToString(), out DateTime d) ? d : null;
    }

    /// <summary>
    /// Evalúa la colección de items recién cargados. Si alguna propiedad principal (Nombre, Categoria, Valor)
    /// quedó en blanco (o 0), ejecuta una heurística estadística sobre los CamposExtra para inferir el dato faltante.
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
    /// Heurística: Examina todos los campos extras dinámicos y califica cuál es más probable
    /// que sea una Categoría analizando cuántos valores únicos y de texto contiene frente al total.
    /// </summary>
    private static string? BuscarMejorClaveCategoria(List<DataItem> items)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        string? mejor = null;
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
    /// Heurística: Examina los campos extras y elige la columna que contenga la mayor cantidad
    /// de datos que puedan parsearse exitosamente a <see cref="double"/>.
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
    /// Heurística: Intenta identificar cuál campo dinámico restante sirve mejor como "Nombre" descriptivo,
    /// evitando columnas puramente numéricas o que ya hayan sido seleccionadas como Categoría.
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

