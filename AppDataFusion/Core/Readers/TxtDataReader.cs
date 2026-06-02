using System;
using System.IO;
using System.Collections.Generic;
using System.Linq; // Importa LINQ para operaciones de colecciones y filtrados.
using System.Text; // Importa soporte para encoding y StringBuilder.
using System.Globalization; // Importa facilidades de formatos culturales.
using ExploradorArchivos.AppDataFusion.Models; // Importa el modelo DataItem de la aplicación.

namespace ExploradorArchivos.AppDataFusion.Readers;

/// <summary>
/// Clase encargada de leer y procesar archivos de texto plano (.txt) tabulares, infiriendo su separador y mapeo.
/// </summary>
public static class TxtDataReader
{
    // Almacena las cabeceras o columnas detectadas en el archivo de texto.
    public static List<string> UltimasColumnas { get; private set; } = new List<string>();

    // Guarda el mapeo dinámico de cabeceras de columnas a las propiedades estándar.
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Listas de alias semánticos para asociar columnas a propiedades básicas de la entidad DataItem.
    private static readonly string[] _idAlias = { "id", "#", "num", "numero", "número", "index", "idx", "codigo", "código", "code", "no", "nro", "rank" };
    private static readonly string[] _nombreAlias = { "nombre", "name", "titulo", "título", "title", "jugador", "player", "athlete", "atleta", "producto", "item", "descripcion", "descripción", "description", "empleado", "employee", "persona", "person", "autor", "author" };
    private static readonly string[] _catAlias = { "categoria", "categoría", "category", "genero", "género", "genre", "tipo", "type", "nivel", "level", "sport", "deporte", "grupo", "group", "departamento", "department", "clasificacion", "clasificación", "clase", "class", "division", "región", "region", "pais", "country", "país" };
    private static readonly string[] _valorAlias = { "valor", "value", "puntos", "score", "mark", "record", "tiempo", "time", "precio", "price", "monto", "amount", "ventas", "sales", "total", "distancia", "distance", "resultado", "result", "medida", "measure", "metros", "segundos", "salario", "salary", "sueldo" };
    private static readonly string[] _fechaAlias = { "fecha", "date", "año", "anio", "year", "periodo", "period", "timestamp", "created_at", "updated_at", "fecha_registro", "release", "publicacion", "publicación", "lanzamiento" };

    /// <summary>
    /// Abre un archivo de texto plano de manera transaccional y analiza líneas para inferir
    /// dinámicamente si posee delimitadores consistentes, mapeando los resultados al formato interno.
    /// </summary>
    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>(); // Instancia la lista que retornará con los datos leídos.
        UltimasColumnas.Clear(); // Limpia los registros de la ejecución anterior.
        MapeoColumnas.Clear(); // Limpia los mapeos previos.

        // Si el archivo físico no existe en disco, finaliza retornando la lista vacía.
        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[TXT] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            // Detecta la codificación del archivo (UTF8 con o sin BOM) para evitar caracteres dañados.
            var encoding = DetectarEncoding(rutaArchivo);
            
            // Abre el archivo usando la codificación detectada.
            using var reader = new StreamReader(rutaArchivo, encoding);
            
            // Colecciona una pequeña muestra de las primeras 10 líneas para inferir separadores.
            var muestra = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                string? l = reader.ReadLine();
                if (l != null) muestra.Add(l);
                else break;
            }
            // Si el archivo está vacío, retorna la lista vacía de inmediato.
            if (muestra.Count == 0) return lista;

            // Detecta estadísticamente cuál es el mejor separador del archivo (, ; | o tabulación).
            char sep = DetectarSeparador(muestra.ToArray());
            
            // Quita la marca BOM de la primera línea de la muestra si está presente.
            string primeraLinea = QuitarBom(muestra[0]);
            
            // Separa la primera línea en base al delimitador detectado.
            string[] primeraFila = Separar(primeraLinea, sep);
            
            // Infiere si la primera línea contiene nombres de columnas o datos de registro.
            bool tieneEncabezado = EsEncabezado(primeraFila);
            
            // Si tiene cabeceras las limpia, de lo contrario genera nombres sintéticos (col1, col2, etc).
            string[] headersRaw = tieneEncabezado
                ? primeraFila.Select(h => h.Trim().Trim('"').Trim()).ToArray()
                : Enumerable.Range(1, primeraFila.Length).Select(i => $"col{i}").ToArray();

            // Descarta las columnas fantasma vacías al final de las cabeceras.
            int totalCols = headersRaw.Length;
            while (totalCols > 0 && string.IsNullOrWhiteSpace(headersRaw[totalCols - 1]))
                totalCols--;
            
            // Se queda con el número real de columnas identificadas.
            string[] headers = headersRaw.Take(totalCols).ToArray();

            // Mapea las columnas a sus roles (IDs de índices: [id, nombre, cat, valor, fecha]).
            int[] mapa = MapearColumnasIndices(headers);

            // Almacena las columnas en la variable global.
            UltimasColumnas = headers.ToList();
            
            // Registra el mapeo en el diccionario global para consumo del sistema.
            if (mapa[0] >= 0) MapeoColumnas[headers[mapa[0]]] = "id";
            if (mapa[1] >= 0) MapeoColumnas[headers[mapa[1]]] = "nombre";
            if (mapa[2] >= 0) MapeoColumnas[headers[mapa[2]]] = "categoria";
            if (mapa[3] >= 0) MapeoColumnas[headers[mapa[3]]] = "valor";
            if (mapa[4] >= 0) MapeoColumnas[headers[mapa[4]]] = "fecha";

            int rowNum = 0;
            // Si la primera línea era de datos (no encabezado), inicia en 0; de lo contrario en la línea 1.
            int inicioMuestra = tieneEncabezado ? 1 : 0;
            
            // Procesa primero las líneas cargadas en la muestra de detección.
            for (int i = inicioMuestra; i < muestra.Count; i++)
            {
                string l = muestra[i];
                if (string.IsNullOrWhiteSpace(l)) continue;
                string lLimpia = l.Replace(sep.ToString(), "").Trim();
                if (string.IsNullOrWhiteSpace(lLimpia)) continue;

                // Convierte la línea de texto a un DataItem y la agrega a la lista.
                ProcesarLineaTxt(l, sep, headers, mapa, ++rowNum, lista);
            }

            // Continúa leyendo el flujo del archivo línea por línea hasta su fin (streaming).
            string? linea;
            while ((linea = reader.ReadLine()) != null)
            {
                // Descarta comentarios (líneas que empiezan con #) o líneas en blanco.
                if (string.IsNullOrWhiteSpace(linea) || linea.TrimStart().StartsWith('#')) continue;
                
                string lineaLimpia = linea.Replace(sep.ToString(), "").Trim();
                if (string.IsNullOrWhiteSpace(lineaLimpia)) continue;

                // Procesa la línea y la agrega a la lista.
                ProcesarLineaTxt(linea, sep, headers, mapa, ++rowNum, lista);
            }

            Console.WriteLine($"[TXT] ✓  {lista.Count} registros leídos (Streaming) desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            // Escribe en consola cualquier excepción ocurrida en el proceso principal.
            Console.WriteLine($"[TXT] ✗  Error leyendo TXT: {ex.Message}");
        }

        return lista; // Devuelve la lista de registros.
    }

    /// <summary>
    /// Divide y limpia una única línea de texto delimitada, llenando un objeto DataItem de 
    /// acuerdo con el mapa pre-calculado de columnas.
    /// </summary>
    private static void ProcesarLineaTxt(string linea, char sep, string[] headers, int[] mapa, int rowNum, List<DataItem> lista)
    {
        try
        {
            // Separa la línea por el delimitador.
            string[] cols = Separar(linea, sep);
            if (cols.Length < 1 || (cols.Length == 1 && string.IsNullOrWhiteSpace(cols[0]))) return;
            
            // Ignora la fila si todas las celdas contienen únicamente espacios.
            if (cols.All(c => string.IsNullOrWhiteSpace(c.Trim().Trim('"')))) return;

            // Instancia el DataItem.
            var item = new DataItem { Fuente = "txt" };
            
            // Asigna los valores procesando de forma segura según el índice de mapeo.
            item.Id = ValorInt(cols, mapa[0]) ?? rowNum;
            item.Nombre = ValorStr(cols, mapa[1]) ?? $"Fila-{rowNum}";
            item.Categoria = ValorStr(cols, mapa[2]) ?? "";
            item.Valor = ValorDouble(cols, mapa[3]) ?? 0;
            item.Fecha = ValorFecha(cols, mapa[4]) ?? DateTime.Now;

            // Agrega todos los campos del archivo al diccionario CamposExtra.
            for (int c = 0; c < headers.Length; c++)
            {
                string key = headers[c].ToLowerInvariant();
                string raw = c < cols.Length ? cols[c].Trim().Trim('"') : "";
                item.CamposExtra[key] = raw;
            }
            lista.Add(item);
        }
        catch 
        {
            // Control silencioso de errores por línea dañada.
        }
    }

    // Detecta la presencia de marca UTF-8 BOM leyendo los primeros bytes del archivo.
    private static Encoding DetectarEncoding(string ruta)
    {
        try
        {
            byte[] bom = new byte[4];
            using var fs = File.OpenRead(ruta);
            int read = fs.Read(bom, 0, 4);
            // Compara los primeros 3 bytes con el estándar UTF-8 BOM (0xEF, 0xBB, 0xBF).
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        catch { }
        return Encoding.UTF8; // Retorna codificación UTF-8 estándar de respaldo.
    }

    // Remueve el prefijo de carácter BOM del inicio de una línea.
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

        // Evalúa la consistencia de cada carácter delimitador en la muestra.
        foreach (char sep in candidatos)
        {
            int[] conteos = muestra.Select(l => ContarSeparadores(l, sep)).ToArray();
            if (conteos[0] == 0) continue; // Si la primera línea no tiene ese delimitador, lo descarta.

            // Comprueba si todas las líneas tienen la misma cantidad del carácter evaluado.
            bool consistente = conteos.All(c => c == conteos[0]);
            // Otorga un gran puntaje por consistencia uniforme, sumándole el número de apariciones.
            int score = (consistente ? 10000 : 0) + conteos[0];

            if (score > mejorScore) { mejorScore = score; mejor = sep; }
        }
        return mejor;
    }

    // Cuenta cuántas veces aparece un separador en la línea ignorando las apariciones que estén dentro de comillas.
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

        // Si la primera celda contiene palabras clave típicas de cabecera, asume que es encabezado.
        if (primero is "id" or "#" or "num" or "index" or "idx") return true;

        // Si la primera celda es un número puro, asume que es una línea de datos directos.
        if (double.TryParse(tokens[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return false;

        // Cuenta cuántos elementos parecen ser etiquetas de texto en lugar de valores numéricos o fechas.
        int parecenEtiqueta = tokens.Count(t =>
        {
            string s = t.Trim().Trim('"');
            if (string.IsNullOrEmpty(s)) return false;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return false;
            if (s.Length >= 8 && s.Length <= 10 && s.Contains('-') && DateTime.TryParse(s, out _)) return false;
            if (s.Length == 4 && int.TryParse(s, out int y) && y >= 1900 && y <= 2200) return false;
            return true;
        });

        // Si el 55% de los tokens parecen etiquetas de texto, se define como fila de encabezado.
        return parecenEtiqueta >= Math.Max(1, tokens.Length * 0.55);
    }

    // Mapea las cabeceras buscando aliases semánticos y devuelve una matriz de índices para [Id, Nombre, Categoría, Valor, Fecha].
    private static int[] MapearColumnasIndices(string[] headers)
    {
        // Pasa las cabeceras a minúsculas y elimina comillas de contenedor.
        string[] lower = headers.Select(h => h.ToLowerInvariant().Trim().Trim('"')).ToArray();
        var usados = new HashSet<int>();

        // Función interna con LINQ que localiza la mejor posición del alias en el array.
        int Buscar(string[] alias)
        {
            // Intenta encontrar coincidencia exacta.
            foreach (string a in alias)
            {
                int index = Array.FindIndex(lower, h => h == a);
                if (index >= 0 && usados.Add(index)) return index;
            }

            // Intenta encontrar coincidencia parcial (empieza con alias + "_").
            foreach (string a in alias)
            {
                if (a.Length < 3) continue;
                int index = Array.FindIndex(lower, h => h.StartsWith(a + "_", StringComparison.Ordinal));
                if (index >= 0 && usados.Add(index)) return index;
            }

            return -1;
        }

        // Devuelve el vector posicional de índices mapeados.
        return new[]
        {
            Buscar(_idAlias),
            Buscar(_nombreAlias),
            Buscar(_catAlias),
            Buscar(_valorAlias),
            Buscar(_fechaAlias)
        };
    }

    // Divide la línea respetando las comillas dobles que contienen separadores.
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
                // Maneja comillas dobles escapadas ("") incluyéndolas como comilla simple en el valor.
                if (enComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    enComillas = !enComillas; // Conmuta el estado de lectura de comillas.
                }
                continue;
            }

            // Si encuentra el separador y no está dentro de comillas, almacena la columna.
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
        campos.Add(actual.ToString().Trim()); // Agrega el último fragmento.
        return campos.ToArray();
    }

    // Parsea de forma segura a int.
    private static int? ValorInt(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return int.TryParse(s, out int v) ? v : null;
    }

    // Retorna el string limpio de comillas.
    private static string? ValorStr(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return string.IsNullOrEmpty(s) ? null : s;
    }

    // Parsea de forma segura a double usando el formato invariante de cultura.
    private static double? ValorDouble(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    // Parsea de forma segura a DateTime usando formatos exactos comunes de fechas.
    private static DateTime? ValorFecha(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        if (string.IsNullOrEmpty(s)) return null;

        // Si es un año puro de 4 dígitos, lo convierte a la fecha del 1 de enero de ese año.
        if (s.Length == 4 && int.TryParse(s, out int anio) && anio >= 1900 && anio <= 2200)
            return new DateTime(anio, 1, 1);

        string[] fmts = {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy",
            "MM-dd-yyyy", "MM/dd/yyyy", "yyyy-MM-ddTHH:mm:ss",
            "dd-MMM-yyyy", "MMM dd yyyy", "yyyy.MM.dd"
        };
        // Intenta parsear con formatos conocidos exactos.
        if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)) 
            return dt;

        // Si no coincide con ninguno, intenta el parseo general estándar.
        return DateTime.TryParse(s, out DateTime dt2) ? dt2 : null;
    }
}
