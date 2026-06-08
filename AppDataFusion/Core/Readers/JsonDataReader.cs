using System.Text.Json; 
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Readers;

public static class JsonDataReader
{
    private static readonly string[] _nombreKeys = { "nombre", "name", "titulo", "title", "producto", "juego", "descripcion", "description", "player", "jugador" };
    private static readonly string[] _categoriaKeys = { "categoria", "category", "genero", "genre", "tipo", "type", "grupo", "group", "departamento", "department", "nivel", "level" };
    private static readonly string[] _valorKeys = { "valor", "value", "precio", "price", "monto", "amount", "score", "puntos", "points", "salario", "salary", "total", "suma" };
    private static readonly string[] _fechaKeys = { "fecha", "date", "releasedate", "fecha_lanzamiento", "fecha_registro", "created_at", "updated_at", "timestamp", "periodo" };
    private static readonly string[] _idKeys = { "id", "Id", "ID", "_id", "codigo", "code", "sku" };

    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Analiza un archivo JSON e intenta extraer la información detectando inteligentemente
    /// si es un array plano de objetos, un formato matricial (array de arrays) o una estructura "fields/records".
    /// </summary>
    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();
        UltimasColumnas = new List<string>();
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[JSON] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            using var stream = File.OpenRead(rutaArchivo);
            using var documento = JsonDocument.Parse(stream);
            var raiz = documento.RootElement;

            // ── Formato fields/records ────────────────────────────────────
            // { "fields": [{"id":"col1","type":"text"}, ...], "records": [[v1,v2,...], ...] }
            if (raiz.ValueKind == JsonValueKind.Object &&
                raiz.TryGetProperty("fields", out var fieldsEl) &&
                raiz.TryGetProperty("records", out var recordsEl) &&
                fieldsEl.ValueKind == JsonValueKind.Array &&
                recordsEl.ValueKind == JsonValueKind.Array)
            {
                lista.AddRange(LeerFieldsRecords(fieldsEl, recordsEl));
            }
            // ── Objeto envuelto: { "data": [...] } o { "results": [...] } ─
            else if (raiz.ValueKind == JsonValueKind.Object)
            {
                // Buscar la primera propiedad que sea un array de objetos
                JsonElement? arrayEncontrado = null;
                foreach (var prop in raiz.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        // Verificar que sea fields/records anidado
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            // Intentar fields/records anidado
                            if (prop.Name.Equals("data", StringComparison.OrdinalIgnoreCase) ||
                                prop.Name.Equals("results", StringComparison.OrdinalIgnoreCase) ||
                                prop.Name.Equals("items", StringComparison.OrdinalIgnoreCase))
                            {
                                var inner = prop.Value;
                                if (inner.ValueKind == JsonValueKind.Object &&
                                    inner.TryGetProperty("fields", out var f2) &&
                                    inner.TryGetProperty("records", out var r2))
                                {
                                    lista.AddRange(LeerFieldsRecords(f2, r2));
                                    break;
                                }
                            }
                        }
                        arrayEncontrado ??= prop.Value;
                    }
                }

                if (lista.Count == 0 && arrayEncontrado.HasValue)
                {
                    lista.AddRange(LeerArrayObjetos(arrayEncontrado.Value.EnumerateArray().ToList()));
                }
                else if (lista.Count == 0)
                {
                    // El objeto raíz mismo es un registro
                    lista.AddRange(LeerArrayObjetos(new List<JsonElement> { raiz }));
                }
            }
            // ── Array plano de objetos ────────────────────────────────────
            else if (raiz.ValueKind == JsonValueKind.Array)
            {
                var elementos = raiz.EnumerateArray().ToList();
                if (elementos.Count > 0)
                {
                    // Verificar si es array de arrays (otro formato tabular)
                    if (elementos[0].ValueKind == JsonValueKind.Array)
                    {
                        lista.AddRange(LeerArrayDeArrays(elementos));
                    }
                    else
                    {
                        lista.AddRange(LeerArrayObjetos(elementos));
                    }
                }
            }

            Console.WriteLine($"[JSON] ✓  {lista.Count} registros leídos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[JSON] ✗  JSON mal formado en {Path.GetFileName(rutaArchivo)}: {ex.Message}");
        }

        return lista;
    }

    // ══════════════════════════════════════════════════════════════
    //  FORMATO fields/records
    //  { "fields": [{"id":"col","type":"text"}, ...],
    //    "records": [[v1,v2,...], [v1,v2,...]] }
    // ══════════════════════════════════════════════════════════════
    /// <summary>
    /// Extrae datos tabulares desde un formato en el que las columnas están definidas en una propiedad 'fields' 
    /// y los valores en una matriz 'records'.
    /// </summary>
    private static List<DataItem> LeerFieldsRecords(JsonElement fieldsEl, JsonElement recordsEl)
    {
        var lista = new List<DataItem>();

        // Extraer nombres de columna desde fields
        var columnas = new List<string>();
        foreach (var f in fieldsEl.EnumerateArray())
        {
            string colName = "";
            if (f.ValueKind == JsonValueKind.Object)
            {
                // Puede ser {"id":"col","type":"text"} o {"name":"col"} o simplemente "col"
                if (f.TryGetProperty("id", out var idProp))
                    colName = idProp.GetString() ?? "";
                else if (f.TryGetProperty("name", out var nameProp))
                    colName = nameProp.GetString() ?? "";
                else if (f.TryGetProperty("label", out var labelProp))
                    colName = labelProp.GetString() ?? "";
            }
            else if (f.ValueKind == JsonValueKind.String)
            {
                colName = f.GetString() ?? "";
            }
            columnas.Add(colName);
        }

        if (columnas.Count == 0) return lista;

        // Publicar metadatos
        UltimasColumnas = new List<string>(columnas);
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DetectarMapeoDesdeColumnas(columnas);

        // Índices de campos estándar
        int idxId = EncontrarIndice(columnas, _idKeys);
        int idxNombre = EncontrarIndice(columnas, _nombreKeys);
        int idxCat = EncontrarIndice(columnas, _categoriaKeys);
        int idxValor = EncontrarIndice(columnas, _valorKeys);
        int idxFecha = EncontrarIndice(columnas, _fechaKeys);

        int contador = 1;
        foreach (var row in recordsEl.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) continue;

            var vals = row.EnumerateArray().ToList();
            var item = new DataItem { Fuente = "json" };

            item.Id = ObtenerInt(vals, idxId) ?? contador;
            item.Nombre = ObtenerStr(vals, idxNombre) ?? $"Registro-{contador}";
            item.Categoria = ObtenerStr(vals, idxCat) ?? "";
            item.Valor = ObtenerDouble(vals, idxValor) ?? 0;
            item.Fecha = ObtenerFecha(vals, idxFecha) ?? DateTime.Now;

            // Resto de columnas → CamposExtra
            var usados = new HashSet<int>(
                new[] { idxId, idxNombre, idxCat, idxValor, idxFecha }.Where(x => x >= 0));

            for (int c = 0; c < columnas.Count && c < vals.Count; c++)
            {
                if (usados.Contains(c)) continue;
                item.CamposExtra[columnas[c]] = vals[c].ToString();
            }

            lista.Add(item);
            contador++;
        }

        return lista;
    }

    // ══════════════════════════════════════════════════════════════
    //  FORMATO array de arrays (primera fila = encabezados opcional)
    //  [[col1,col2,...],[v1,v2,...],[v1,v2,...]]
    // ══════════════════════════════════════════════════════════════
    /// <summary>
    /// Procesa el JSON como una estructura tabular simple de matriz (array bidimensional).
    /// Si la primera fila contiene solo textos, la toma como nombres de columnas.
    /// </summary>
    private static List<DataItem> LeerArrayDeArrays(List<JsonElement> elementos)
    {
        var lista = new List<DataItem>();
        if (elementos.Count == 0) return lista;

        // Determinar si la primera fila son encabezados (todos strings)
        var primeraFila = elementos[0].EnumerateArray().ToList();
        bool esEncabezado = primeraFila.All(e => e.ValueKind == JsonValueKind.String);

        List<string> columnas;
        int inicioFila;

        if (esEncabezado)
        {
            columnas = primeraFila.Select(e => e.GetString() ?? "").ToList();
            inicioFila = 1;
        }
        else
        {
            columnas = Enumerable.Range(0, primeraFila.Count).Select(i => $"col{i + 1}").ToList();
            inicioFila = 0;
        }

        UltimasColumnas = new List<string>(columnas);
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DetectarMapeoDesdeColumnas(columnas);

        int idxId = EncontrarIndice(columnas, _idKeys);
        int idxNombre = EncontrarIndice(columnas, _nombreKeys);
        int idxCat = EncontrarIndice(columnas, _categoriaKeys);
        int idxValor = EncontrarIndice(columnas, _valorKeys);
        int idxFecha = EncontrarIndice(columnas, _fechaKeys);

        int contador = 1;
        for (int r = inicioFila; r < elementos.Count; r++)
        {
            var vals = elementos[r].EnumerateArray().ToList();
            var item = new DataItem { Fuente = "json" };

            item.Id = ObtenerInt(vals, idxId) ?? contador;
            item.Nombre = ObtenerStr(vals, idxNombre) ?? $"Registro-{contador}";
            item.Categoria = ObtenerStr(vals, idxCat) ?? "";
            item.Valor = ObtenerDouble(vals, idxValor) ?? 0;
            item.Fecha = ObtenerFecha(vals, idxFecha) ?? DateTime.Now;

            var usados = new HashSet<int>(
                new[] { idxId, idxNombre, idxCat, idxValor, idxFecha }.Where(x => x >= 0));

            for (int c = 0; c < columnas.Count && c < vals.Count; c++)
            {
                if (usados.Contains(c)) continue;
                item.CamposExtra[columnas[c]] = vals[c].ToString();
            }

            lista.Add(item);
            contador++;
        }

        return lista;
    }

    // ══════════════════════════════════════════════════════════════
    //  FORMATO array de objetos (comportamiento original)
    // ══════════════════════════════════════════════════════════════
    /// <summary>
    /// Analiza el formato JSON estándar de un array que contiene un objeto por fila,
    /// inspeccionando el primer objeto para mapear dinámicamente las propiedades a los campos base.
    /// </summary>
    private static List<DataItem> LeerArrayObjetos(List<JsonElement> elementos)
    {
        var lista = new List<DataItem>();
        if (elementos.Count == 0) return lista;

        if (elementos[0].ValueKind == JsonValueKind.Object)
            DetectarMetadatosObjeto(elementos[0]);

        int contadorId = 1;
        foreach (var el in elementos)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            try
            {
                var item = new DataItem { Fuente = "json" };

                item.Id = LeerEntero(el, _idKeys) ?? contadorId;
                item.Nombre = LeerCadena(el, _nombreKeys) ?? FallbackPrimeraString(el, _idKeys) ?? $"Item-{contadorId}";
                item.Categoria = LeerCadena(el, _categoriaKeys) ?? FallbackPrimeraString(el, _idKeys, _nombreKeys) ?? "";
                item.Valor = LeerDouble(el, _valorKeys) ?? FallbackPrimerNumero(el, _idKeys) ?? 0.0;
                item.Fecha = LeerFecha(el, _fechaKeys) ?? DateTime.Now;

                // Solo excluir campos realmente mapeados, no todos los aliases posibles
                var usadas = new HashSet<string>(MapeoColumnas.Keys,
                                            StringComparer.OrdinalIgnoreCase);
                foreach (var prop in el.EnumerateObject())
                {
                    if (usadas.Contains(prop.Name)) continue;

                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var arr = prop.Value.EnumerateArray()
                            .Select(e => e.GetString() ?? e.ToString())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                        item.CamposExtra[prop.Name] = string.Join(", ", arr);
                    }
                    else
                    {
                        item.CamposExtra[prop.Name] = prop.Value.ToString();
                    }
                }

                lista.Add(item);
                contadorId++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JSON] ⚠  Error en elemento #{contadorId}: {ex.Message}");
                contadorId++;
            }
        }

        return lista;
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS – índices y valores para arrays posicionales
    // ══════════════════════════════════════════════════════════════

    private static int EncontrarIndice(List<string> columnas, string[] aliases)
    {
        foreach (var alias in aliases)
            for (int i = 0; i < columnas.Count; i++)
                if (string.Equals(columnas[i], alias, StringComparison.OrdinalIgnoreCase))
                    return i;
        return -1;
    }

    private static string? ObtenerStr(List<JsonElement> vals, int idx)
    {
        if (idx < 0 || idx >= vals.Count) return null;
        var v = vals[idx];
        if (v.ValueKind == JsonValueKind.String) return v.GetString()?.Trim();
        if (v.ValueKind == JsonValueKind.Null) return null;
        return v.ToString();
    }

    private static int? ObtenerInt(List<JsonElement> vals, int idx)
    {
        if (idx < 0 || idx >= vals.Count) return null;
        var v = vals[idx];
        if (v.TryGetInt32(out int i)) return i;
        if (int.TryParse(v.ToString(), out int i2)) return i2;
        return null;
    }

    private static double? ObtenerDouble(List<JsonElement> vals, int idx)
    {
        if (idx < 0 || idx >= vals.Count) return null;
        var v = vals[idx];
        if (v.TryGetDouble(out double d)) return d;
        if (double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double d2)) return d2;
        return null;
    }

    private static DateTime? ObtenerFecha(List<JsonElement> vals, int idx)
    {
        if (idx < 0 || idx >= vals.Count) return null;
        string s = vals[idx].ValueKind == JsonValueKind.String
            ? vals[idx].GetString() ?? ""
            : vals[idx].ToString();
        return DateTime.TryParse(s, out DateTime d) ? d : null;
    }

    private static void DetectarMapeoDesdeColumnas(List<string> columnas)
    {
        MapeoColumnas.Clear();

        string? Find(string[] aliases)
        {
            foreach (var a in aliases)
                foreach (var c in columnas)
                    if (string.Equals(c, a, StringComparison.OrdinalIgnoreCase))
                        return c;
            return null;
        }

        string? c;
        c = Find(_idKeys); if (c != null) MapeoColumnas[c] = "id";
        c = Find(_nombreKeys); if (c != null) MapeoColumnas[c] = "nombre";
        c = Find(_categoriaKeys); if (c != null) MapeoColumnas[c] = "categoria";
        c = Find(_valorKeys); if (c != null) MapeoColumnas[c] = "valor";
        c = Find(_fechaKeys); if (c != null) MapeoColumnas[c] = "fecha";
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS – objetos (formato original)
    // ══════════════════════════════════════════════════════════════

    private static void DetectarMetadatosObjeto(JsonElement primerElemento)
    {
        var todasProps = new List<string>();
        foreach (var prop in primerElemento.EnumerateObject())
            todasProps.Add(prop.Name);

        string? idCol = BuscarProp(todasProps, _idKeys);
        string? nombreCol = BuscarProp(todasProps, _nombreKeys);
        string? catCol = BuscarProp(todasProps, _categoriaKeys);
        string? valorCol = BuscarProp(todasProps, _valorKeys);
        string? fechaCol = BuscarProp(todasProps, _fechaKeys);

        MapeoColumnas.Clear();
        if (idCol != null) MapeoColumnas[idCol] = "id";
        if (nombreCol != null) MapeoColumnas[nombreCol] = "nombre";
        if (catCol != null) MapeoColumnas[catCol] = "categoria";
        if (valorCol != null) MapeoColumnas[valorCol] = "valor";
        if (fechaCol != null) MapeoColumnas[fechaCol] = "fecha";

        UltimasColumnas.Clear();
        foreach (var prop in todasProps)
            UltimasColumnas.Add(prop);
    }

    private static string? BuscarProp(List<string> props, string[] aliases)
    {
        foreach (var alias in aliases)
            foreach (var p in props)
                if (string.Equals(p, alias, StringComparison.OrdinalIgnoreCase))
                    return p;
        return null;
    }

    /// <summary>
    /// Recuperador de emergencia (parser robusto custom). Escanea la cadena cruda del JSON
    /// en búsqueda de delimitadores '{' y '}' para extraer objetos parciales ignorando corrupciones sintácticas externas.
    /// </summary>
    private static List<DataItem> RecuperarJsonParcial(string contenido)
    {
        var lista = new List<DataItem>();
        int inicio = contenido.IndexOf('{');
        int id = 1;
        while (inicio >= 0)
        {
            int fin = EncontrarCierreLlave(contenido, inicio);
            if (fin < 0) break;

            string fragmento = contenido[inicio..(fin + 1)];
            try
            {
                using var doc = JsonDocument.Parse(fragmento);
                var el = doc.RootElement;
                lista.Add(new DataItem
                {
                    Id = LeerEntero(el, _idKeys) ?? id,
                    Nombre = LeerCadena(el, _nombreKeys) ?? FallbackPrimeraString(el, _idKeys) ?? $"Recuperado-{id}",
                    Categoria = LeerCadena(el, _categoriaKeys) ?? "Recuperado",
                    Valor = LeerDouble(el, _valorKeys) ?? 0,
                    Fuente = "json(recuperado)",
                    Fecha = DateTime.Now
                });
                id++;
            }
            catch { }

            inicio = contenido.IndexOf('{', fin + 1);
        }
        Console.WriteLine($"[JSON]    Recuperados {lista.Count} objetos parciales.");
        return lista;
    }

    private static int EncontrarCierreLlave(string s, int inicio)
    {
        int depth = 0;
        bool inString = false;
        for (int i = inicio; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' && (i == 0 || s[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    private static string? LeerCadena(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString()?.Trim();
        return null;
    }

    private static int? LeerEntero(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p))
                if (p.TryGetInt32(out int v)) return v;
                else if (int.TryParse(p.ToString(), out int v2)) return v2;
        return null;
    }

    private static double? LeerDouble(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p))
                if (p.TryGetDouble(out double v)) return v;
                else if (double.TryParse(p.ToString(), System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out double v2)) return v2;
        return null;
    }

    private static DateTime? LeerFecha(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p) && p.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(p.GetString(), out DateTime d)) return d;
        return null;
    }

    private static string? FallbackPrimeraString(JsonElement el, params string[][] excluidos)
    {
        var exc = new HashSet<string>(excluidos.SelectMany(a => a), StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            if (exc.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        return null;
    }

    private static double? FallbackPrimerNumero(JsonElement el, params string[][] excluidos)
    {
        var exc = new HashSet<string>(excluidos.SelectMany(a => a), StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            if (exc.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out double v))
                return v;
        }
        return null;
    }
}

