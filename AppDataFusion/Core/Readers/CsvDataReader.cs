using ExploradorArchivos.AppDataFusion.Models;
using System.Globalization;

namespace ExploradorArchivos.AppDataFusion.Readers;

public static class CsvDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Abre y lee un archivo CSV en modo streaming, mapeando dinámicamente sus columnas
    /// a las propiedades de <see cref="DataItem"/> según las coincidencias semánticas encontradas.
    /// </summary>
    public static List<DataItem> Leer(string rutaArchivo, char separador = ',')
    {
        var lista = new List<DataItem>();

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[CSV] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            using var reader = new StreamReader(rutaArchivo);
            string? lineaEncabezado = reader.ReadLine();
            if (lineaEncabezado == null) return lista;

            // Detectar separador desde la primera línea
            if (!lineaEncabezado.Contains(separador) && lineaEncabezado.Contains(';'))
                separador = ';';
            else if (!lineaEncabezado.Contains(separador) && lineaEncabezado.Contains('\t'))
                separador = '\t';

            var encabezadosRaw = SepararCsvRobust(lineaEncabezado, separador)
                                       .Select(h => h.Trim().Replace("\"", ""))
                                       .ToArray();
            
            // Filtrar encabezados vacíos al final para evitar columnas fantasma
            int totalCols = encabezadosRaw.Length;
            while (totalCols > 0 && string.IsNullOrWhiteSpace(encabezadosRaw[totalCols - 1]))
                totalCols--;

            var encabezados = encabezadosRaw.Take(totalCols)
                                       .Select(h => h.ToLowerInvariant())
                                       .ToArray();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < encabezados.Length; i++)
                mapa[encabezados[i]] = i;

            UltimasColumnas = encabezadosRaw.Take(totalCols).ToList();

            int idxId = BuscarColumna(mapa, "id", "car_id", "usedcarskuid", "sku_id", "codigo", "code", "sku", "#", "uuid", "guid", "_id", "num", "numero", "idx", "index", "rank", "no", "student_id");
            int idxNombre = BuscarColumna(mapa, "nombre", "name", "titulo", "title", "producto", "juego", "descripcion", "description", "brand", "marca", "model", "modelo", "micrositio", "persona", "person", "autor", "author", "atleta", "athlete");
            int idxCat = BuscarColumna(mapa, "categoria", "category", "genero", "gender", "sexo", "sex", "genre", "tipo", "type", "grupo", "group", "body", "car_type", "level", "nivel", "clasificacion", "clase", "class", "division");
            int idxValor = BuscarColumna(mapa, "valor", "value", "precio", "price", "monto", "amount", "ventas", "sales", "score", "puntos", "points", "salary", "total", "edad", "age", "promedio", "avg", "cantidad", "count");
            int idxFecha = BuscarColumnaExacta(mapa, "fecha", "date", "releasedate", "created_at", "anio", "model_year", "periodo", "period", "updated_at", "timestamp", "fecha_registro", "fecha_reporte");

            var mapeadas = new HashSet<int>(new[] { idxId, idxNombre, idxCat, idxValor, idxFecha }.Where(x => x >= 0));
            
            // Si no hay mapeo numérico, buscar la primera columna numérica en una muestra pequeña
            if (idxValor < 0)
            {
                // En streaming esto es más difícil, pero podemos intentar con la primera fila de datos
            }

            MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (idxId >= 0) MapeoColumnas[encabezados[idxId]] = "Id";
            if (idxNombre >= 0) MapeoColumnas[encabezados[idxNombre]] = "Nombre";
            if (idxCat >= 0) MapeoColumnas[encabezados[idxCat]] = "Categoria";
            if (idxValor >= 0) MapeoColumnas[encabezados[idxValor]] = "Valor";
            if (idxFecha >= 0) MapeoColumnas[encabezados[idxFecha]] = "Fecha";

            int fila = 1;
            string? linea;
            while ((linea = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(linea)) continue;
                
                // Limpieza agresiva: si al quitar separadores y espacios no queda nada, es una fila basura
                string lineaLimpia = linea.Replace(separador.ToString(), "").Trim();
                if (string.IsNullOrWhiteSpace(lineaLimpia)) continue;

                try
                {
                    var cols = SepararCsvRobust(linea, separador);

                    var item = new DataItem { Fuente = "csv" };

                    int parsedId = idxId >= 0 ? ParseInt(cols, idxId) : 0;
                    item.Id = parsedId != 0 ? parsedId : fila;

                    if (idxId >= 0 && parsedId == 0)
                    {
                        string rawId = Limpiar(cols, idxId);
                        if (!string.IsNullOrEmpty(rawId)) item.CamposExtra[encabezados[idxId]] = rawId;
                    }

                    item.Nombre = idxNombre >= 0 ? Limpiar(cols, idxNombre) : $"Fila-{fila}";
                    item.Categoria = idxCat >= 0 ? Limpiar(cols, idxCat) : "Sin categoría";
                    item.Valor = idxValor >= 0 ? ParseDouble(cols, idxValor) : 0;
                    item.Fecha = idxFecha >= 0 ? ParseFechaEspecial(cols, idxFecha, encabezados[idxFecha]) : DateTime.Now;

                    var usadas = new HashSet<int> { idxId, idxNombre, idxCat, idxValor, idxFecha };
                    for (int c = 0; c < encabezados.Length; c++)
                    {
                        if (usadas.Contains(c)) continue;
                        string val = c < cols.Count ? cols[c].Trim().Replace("\"", "") : "";
                        item.CamposExtra[encabezados[c]] = val;
                    }

                    lista.Add(item);
                }
                catch { }
                fila++;
            }

            Console.WriteLine($"[CSV] ✓  {lista.Count} registros leídos (Streaming) desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CSV] ✗  Error leyendo archivo: {ex.Message}");
        }

        return lista;
    }

    // ──────────────────────────────────────────────────────────────
    // Parser robusto: maneja comillas dobles, listas Python ["..."],
    // y campos con corchetes sin comillas envolventes.
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Parser robusto personalizado que divide una línea en columnas respetando
    /// el encapsulamiento de comillas dobles, llaves JSON o corchetes que puedan contener el caracter separador.
    /// </summary>
    private static List<string> SepararCsvRobust(string linea, char sep)
    {
        var campos = new List<string>();
        var actual = new System.Text.StringBuilder();
        bool enComillas = false;
        int profCorchete = 0;   // cuenta [ sin cerrar
        int profLlave = 0;   // cuenta { sin cerrar

        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];

            // Cambio de estado de comillas dobles (no afecta si estamos en []/{})
            if (c == '"' && profCorchete == 0 && profLlave == 0)
            {
                enComillas = !enComillas;
                // Incluir la comilla en el valor para que Limpiar() la quite después
                actual.Append(c);
                continue;
            }

            // Dentro de comillas dobles: copiar literalmente
            if (enComillas)
            {
                actual.Append(c);
                continue;
            }

            // Rastrear profundidad de corchetes/llaves fuera de comillas dobles
            if (c == '[') { profCorchete++; actual.Append(c); continue; }
            if (c == ']') { profCorchete = Math.Max(0, profCorchete - 1); actual.Append(c); continue; }
            if (c == '{') { profLlave++; actual.Append(c); continue; }
            if (c == '}') { profLlave = Math.Max(0, profLlave - 1); actual.Append(c); continue; }

            // Separador real sólo cuando no estamos dentro de ninguna estructura
            if (c == sep && profCorchete == 0 && profLlave == 0)
            {
                campos.Add(actual.ToString());
                actual.Clear();
                continue;
            }

            actual.Append(c);
        }
        campos.Add(actual.ToString());
        return campos;
    }

    private static int BuscarColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.TryGetValue(a, out int idx)) return idx;
        return -1;
    }

    private static int BuscarColumnaExacta(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.TryGetValue(a, out int idx)) return idx;
        return -1;
    }

    private static int SiguienteLibre(HashSet<int> mapeadas, int total, int desde)
    {
        for (int i = desde; i < total; i++)
            if (mapeadas.Add(i)) return i;
        return -1;
    }

    private static string Limpiar(List<string> cols, int idx)
        => idx >= 0 && idx < cols.Count ? cols[idx].Trim().Replace("\"", "") : "";

    private static int ParseInt(List<string> cols, int idx)
        => idx >= 0 && idx < cols.Count && int.TryParse(cols[idx].Trim(), out int v) ? v : 0;

    private static double ParseDouble(List<string> cols, int idx)
    {
        if (idx < 0 || idx >= cols.Count) return 0;
        string s = cols[idx].Trim().Replace("\"", "");
        // Quitar ".0" flotante si viene como "370000.0"
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            return v;
        return 0;
    }

    private static DateTime ParseFechaEspecial(List<string> cols, int idx, string nombreColumna)
    {
        if (idx < 0 || idx >= cols.Count) return DateTime.Now;

        string valor = cols[idx].Trim();
        string colLow = nombreColumna.ToLower();

        if (colLow is "anio" or "año" or "year" or "model_year" or "myear")
        {
            if (int.TryParse(valor, out int anio))
                return new DateTime(Math.Clamp(anio, 1900, 2100), 1, 1);
        }

        if (DateTime.TryParse(valor, out DateTime d)) return d;
        return DateTime.Now;
    }

    /// <summary>
    /// Busca la primera columna no mapeada cuyos valores sean mayoritariamente numéricos.
    /// Usa SepararCsvRobust para no romper con campos complejos.
    /// </summary>
    private static int BuscarPrimeraNumerica(string[] lineas, string[] encabezados,
        char sep, HashSet<int> mapeadas)
    {
        int filasMuestra = Math.Min(10, lineas.Length - 1);
        if (filasMuestra <= 0) return -1;

        for (int col = 0; col < encabezados.Length; col++)
        {
            if (mapeadas.Contains(col)) continue;

            int numericos = 0;
            for (int fila = 1; fila <= filasMuestra; fila++)
            {
                var cols = SepararCsvRobust(lineas[fila], sep);
                if (col >= cols.Count) continue;
                string val = cols[col].Trim().Replace("\"", "");
                if (double.TryParse(val, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out _))
                    numericos++;
            }

            if (numericos >= filasMuestra * 0.7)
            {
                mapeadas.Add(col);
                return col;
            }
        }
        return -1;
    }
}

