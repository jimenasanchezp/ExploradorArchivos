using Npgsql;
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Database;

public class PostgreSqlConnector
{
    public string CadenaConexion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int LimiteFilas { get; set; } = 0;

    public List<string> UltimasColumnas { get; private set; } = new();
    public Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _mapeoConfirmadoPorUsuario = false;

    public PostgreSqlConnector() { }
    public PostgreSqlConnector(string cadenaConexion, string tabla)
    { CadenaConexion = cadenaConexion; Tabla = tabla; }

    // ── Paso 1: obtener nombres de columna (sin leer filas) ──────
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
        }
        catch (Exception ex)
        { Console.WriteLine($"[PostgreSQL] ObtenerNombresColumnas: {ex.Message}"); }

        UltimasColumnas = new List<string>(cols);
        ActualizarMapeoAutomatico(cols);
        _mapeoConfirmadoPorUsuario = false;
        return cols;
    }

    // ── Paso 2: el usuario confirma el mapeo ─────────────────────
    public void SobreescribirMapeo(
        string colCategoria, string colValor,
        string colNombre, string colFecha)
    {
        MapeoColumnas.Clear();
        var colId = UltimasColumnas.FirstOrDefault(c =>
            string.Equals(c, "id", StringComparison.OrdinalIgnoreCase));
        if (colId != null) MapeoColumnas[colId] = "id";

        // Evitar sobrescritura accidental cuando el usuario selecciona
        // la misma columna para más de un rol (ej. categoría y nombre).
        // El primer rol que llegue conserva prioridad.
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

    // ── Paso 3: leer datos usando el mapeo vigente ───────────────
    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[PostgreSQL] ✓  Conectado a {conn.Database}");

            // Verificar que la tabla existe
            var colsInfo = ObtenerColumnasInfo(conn, Tabla);
            if (colsInfo.Count == 0)
            {
                Console.WriteLine($"[PostgreSQL] ⚠  Tabla '{Tabla}' no encontrada.");
                return lista;
            }

            string sql = LimiteFilas > 0
                ? $"SELECT * FROM {Tabla} LIMIT {LimiteFilas}"
                : $"SELECT * FROM {Tabla}";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            using var reader = cmd.ExecuteReader();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++) mapa[reader.GetName(i)] = i;
            UltimasColumnas = mapa.Keys.ToList();

            // SOLO aplicar auto-mapeo si el usuario NO confirmó manualmente
            if (!_mapeoConfirmadoPorUsuario)
                ActualizarMapeoAutomatico(mapa.Keys);

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

                item.Id = (colId != null && mapa.TryGetValue(colId, out int iId)
                           && !reader.IsDBNull(iId)
                           && int.TryParse(reader[iId].ToString(), out int idV))
                           ? idV : contador;

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
        c = Find("id"); if (c != null) MapeoColumnas[c] = "id";
        c = Find("nombre", "name", "titulo", "title", "pais", "country", "jugador", "player", "empleado", "employee", "producto", "item"); if (c != null) MapeoColumnas[c] = "nombre";
        c = Find("categoria", "category", "genero", "genre", "region", "tipo", "type", "departamento", "department", "level", "segmento", "segment", "grupo", "group_name", "clasificacion"); if (c != null) MapeoColumnas[c] = "categoria";
        c = Find("valor", "value", "precio", "price", "ventas_global", "ventas", "sales", "score", "puntos", "salario", "puntaje", "total", "monto", "amount", "importe", "cost", "costo", "revenue", "ingreso", "metrica"); if (c != null) MapeoColumnas[c] = "valor";
        c = Find("fecha", "date", "fecha_lanzamiento", "anio", "year", "created_at", "updated_at", "timestamp", "fecha_registro", "fecha_reporte", "periodo"); if (c != null) MapeoColumnas[c] = "fecha";
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

    private static void EnriquecerCamposFaltantes(
        List<DataItem> items, string? colCategoria, string? colValor, string? colNombre)
    {
        if (items.Count == 0) return;

        // IMPORTANTE:
        // Si el usuario seleccionó una columna manualmente en el diálogo,
        // respetamos esa decisión y NO sobreescribimos con heurísticas.
        // Solo inferimos cuando el mapeo quedó en "(ninguna)".
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

