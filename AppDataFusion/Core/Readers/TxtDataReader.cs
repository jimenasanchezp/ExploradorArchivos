using ExploradorArchivos.AppDataFusion.Models;
using System.Globalization;
using System.Text;

namespace ExploradorArchivos.AppDataFusion.Readers;

public static class TxtDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _idAlias = {
        "id", "#", "num", "numero", "número", "index", "idx",
        "codigo", "código", "code", "no", "nro", "rank"
    };
    private static readonly string[] _nombreAlias = {
        "nombre", "name", "titulo", "título", "title",
        "jugador", "player", "athlete", "atleta",
        "producto", "item", "descripcion", "descripción", "description",
        "empleado", "employee", "persona", "person", "autor", "author"
    };
    private static readonly string[] _catAlias = {
        "categoria", "categoría", "category", "genero", "género", "genre",
        "tipo", "type", "nivel", "level", "sport", "deporte",
        "grupo", "group", "departamento", "department",
        "clasificacion", "clasificación", "clase", "class",
        "division", "región", "region", "pais", "country", "país"
    };
    private static readonly string[] _valorAlias = {
        "valor", "value", "puntos", "score", "mark", "record",
        "tiempo", "time", "precio", "price", "monto", "amount",
        "ventas", "sales", "total", "distancia", "distance",
        "resultado", "result", "medida", "measure", "metros", "segundos",
        "salario", "salary", "sueldo"
    };
    private static readonly string[] _fechaAlias = {
        "fecha", "date", "año", "anio", "year", "periodo", "period",
        "timestamp", "created_at", "updated_at", "fecha_registro",
        "release", "publicacion", "publicación", "lanzamiento"
    };

    /// <summary>
    /// Abre un archivo de texto plano de manera transaccional y analiza líneas para inferir
    /// dinámicamente si posee delimitadores consistentes, mapeando los resultados al formato interno.
    /// </summary>
    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();
        UltimasColumnas = new List<string>();
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[TXT] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            var encoding = DetectarEncoding(rutaArchivo);
            using var reader = new StreamReader(rutaArchivo, encoding);
            
            // Leer muestra para detectar separador
            var muestra = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                string? l = reader.ReadLine();
                if (l != null) muestra.Add(l);
                else break;
            }
            if (muestra.Count == 0) return lista;

            char sep = DetectarSeparador(muestra.ToArray());
            string primeraLinea = QuitarBom(muestra[0]);
            string[] primeraFila = Separar(primeraLinea, sep);
            bool tieneEncabezado = EsEncabezado(primeraFila);
            
            string[] headersRaw = tieneEncabezado
                ? primeraFila.Select(h => h.Trim().Trim('"').Trim()).ToArray()
                : Enumerable.Range(1, primeraFila.Length).Select(i => $"col{i}").ToArray();

            // Filtrar columnas vacías al final
            int totalCols = headersRaw.Length;
            while (totalCols > 0 && string.IsNullOrWhiteSpace(headersRaw[totalCols - 1]))
                totalCols--;
            
            string[] headers = headersRaw.Take(totalCols).ToArray();

            int[] mapa = MapearColumnas(headers);

            UltimasColumnas = headers.ToList();
            MapeoColumnas.Clear();
            if (mapa[0] >= 0) MapeoColumnas[headers[mapa[0]]] = "id";
            if (mapa[1] >= 0) MapeoColumnas[headers[mapa[1]]] = "nombre";
            if (mapa[2] >= 0) MapeoColumnas[headers[mapa[2]]] = "categoria";
            if (mapa[3] >= 0) MapeoColumnas[headers[mapa[3]]] = "valor";
            if (mapa[4] >= 0) MapeoColumnas[headers[mapa[4]]] = "fecha";

            // Procesar la muestra primero
            int rowNum = 0;
            int inicioMuestra = tieneEncabezado ? 1 : 0;
            for (int i = inicioMuestra; i < muestra.Count; i++)
            {
                string l = muestra[i];
                if (string.IsNullOrWhiteSpace(l)) continue;
                string lLimpia = l.Replace(sep.ToString(), "").Trim();
                if (string.IsNullOrWhiteSpace(lLimpia)) continue;

                ProcesarLineaTxt(l, sep, headers, mapa, ++rowNum, lista);
            }

            // Continuar con el resto del archivo
            string? linea;
            while ((linea = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(linea) || linea.TrimStart().StartsWith('#')) continue;
                
                string lineaLimpia = linea.Replace(sep.ToString(), "").Trim();
                if (string.IsNullOrWhiteSpace(lineaLimpia)) continue;

                ProcesarLineaTxt(linea, sep, headers, mapa, ++rowNum, lista);
            }

            Console.WriteLine($"[TXT] ✓  {lista.Count} registros leídos (Streaming) desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TXT] ✗  Error leyendo TXT: {ex.Message}");
        }

        return lista;
    }

    /// <summary>
    /// Divide y limpia una única línea de texto delimitada, llenando un objeto DataItem de 
    /// acuerdo con el mapa pre-calculado de columnas.
    /// </summary>
    private static void ProcesarLineaTxt(string linea, char sep, string[] headers, int[] mapa, int rowNum, List<DataItem> lista)
    {
        try
        {
            string[] cols = Separar(linea, sep);
            if (cols.Length < 1 || (cols.Length == 1 && string.IsNullOrWhiteSpace(cols[0]))) return;
            
            // Validar si la fila tiene datos reales
            if (cols.All(c => string.IsNullOrWhiteSpace(c.Trim().Trim('"')))) return;

            var item = new DataItem { Fuente = "txt" };
            item.Id = ValorInt(cols, mapa[0]) ?? rowNum;
            item.Nombre = ValorStr(cols, mapa[1]) ?? $"Fila-{rowNum}";
            item.Categoria = ValorStr(cols, mapa[2]) ?? "";
            item.Valor = ValorDouble(cols, mapa[3]) ?? 0;
            item.Fecha = ValorFecha(cols, mapa[4]) ?? DateTime.Now;

            for (int c = 0; c < headers.Length; c++)
            {
                string key = headers[c].ToLowerInvariant();
                string raw = c < cols.Length ? cols[c].Trim().Trim('"') : "";
                item.CamposExtra[key] = raw;
            }
            lista.Add(item);
        }
        catch { }
    }

    private static Encoding DetectarEncoding(string ruta)
    {
        try
        {
            byte[] bom = new byte[4];
            using var fs = File.OpenRead(ruta);
            int read = fs.Read(bom, 0, 4);
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        catch { }
        return Encoding.UTF8;
    }

    private static string QuitarBom(string linea)
    {
        if (linea.Length > 0 && linea[0] == '\uFEFF')
            return linea[1..];
        return linea;
    }

    /// <summary>
    /// Analiza una muestra estadística de las primeras líneas del archivo contra posibles 
    /// caracteres candidatos (| ; , \t) para determinar cuál es el separador real del documento.
    /// </summary>
    private static char DetectarSeparador(string[] lineas)
    {
        char[] candidatos = { '|', '\t', ';', ',' };
        string[] muestra = lineas.Take(Math.Min(6, lineas.Length)).ToArray();

        char mejor = '|';
        int mejorScore = -1;

        foreach (char sep in candidatos)
        {
            int[] conteos = muestra.Select(l => ContarSeparadores(l, sep)).ToArray();
            if (conteos[0] == 0) continue;

            bool consistente = conteos.All(c => c == conteos[0]);
            int score = (consistente ? 10000 : 0) + conteos[0];

            if (score > mejorScore) { mejorScore = score; mejor = sep; }
        }
        return mejor;
    }

    private static int ContarSeparadores(string linea, char sep)
    {
        int count = 0;
        bool enComillas = false;
        foreach (char c in linea)
        {
            if (c == '"') { enComillas = !enComillas; continue; }
            if (!enComillas && c == sep) count++;
        }
        return count;
    }

    /// <summary>
    /// Heurística sencilla que examina la primera fila tokenizada intentando averiguar si actúa
    /// como una fila de encabezados o si directamente contiene información de un registro de datos.
    /// </summary>
    private static bool EsEncabezado(string[] tokens)
    {
        if (tokens.Length == 0) return false;

        string primero = tokens[0].Trim().TrimStart('\uFEFF').ToLower();

        if (primero is "id" or "#" or "num" or "index" or "idx") return true;

        if (double.TryParse(tokens[0].Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out _)) return false;

        int parecenEtiqueta = tokens.Count(t =>
        {
            string s = t.Trim().Trim('"');
            if (string.IsNullOrEmpty(s)) return false;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return false;
            if (s.Length >= 8 && s.Length <= 10 && s.Contains('-') && DateTime.TryParse(s, out _)) return false;
            if (s.Length == 4 && int.TryParse(s, out int y) && y >= 1900 && y <= 2200) return false;
            return true;
        });

        return parecenEtiqueta >= Math.Max(1, tokens.Length * 0.55);
    }

    private static int[] MapearColumnas(string[] headers)
    {
        string[] lower = headers.Select(h => h.ToLowerInvariant().Trim().Trim('"')).ToArray();
        var usados = new HashSet<int>();

        int Buscar(string[] alias)
        {
            foreach (string a in alias)
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i] == a)
                    { usados.Add(i); return i; }

            foreach (string a in alias)
            {
                if (a.Length < 3) continue;
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i].StartsWith(a + "_", StringComparison.Ordinal))
                    { usados.Add(i); return i; }
            }

            return -1;
        }

        return new[]
        {
            Buscar(_idAlias),
            Buscar(_nombreAlias),
            Buscar(_catAlias),
            Buscar(_valorAlias),
            Buscar(_fechaAlias)
        };
    }

    private static string[] Separar(string linea, char sep)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        bool enComillas = false;

        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];

            if (c == '"')
            {
                if (enComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    enComillas = !enComillas;
                }
                continue;
            }

            if (c == sep && !enComillas)
            {
                campos.Add(actual.ToString().Trim());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }
        campos.Add(actual.ToString().Trim());
        return campos.ToArray();
    }

    private static int? ValorInt(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return int.TryParse(s, out int v) ? v : null;
    }

    private static string? ValorStr(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static double? ValorDouble(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return double.TryParse(s, NumberStyles.Any,
            CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private static DateTime? ValorFecha(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        if (string.IsNullOrEmpty(s)) return null;

        if (s.Length == 4 && int.TryParse(s, out int anio) && anio >= 1900 && anio <= 2200)
            return new DateTime(anio, 1, 1);

        string[] fmts = {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy",
            "MM-dd-yyyy", "MM/dd/yyyy", "yyyy-MM-ddTHH:mm:ss",
            "dd-MMM-yyyy", "MMM dd yyyy", "yyyy.MM.dd"
        };
        if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime dt)) return dt;

        return DateTime.TryParse(s, out DateTime dt2) ? dt2 : null;
    }
}

