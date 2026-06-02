using System;
using System.IO;
using System.Collections.Generic;
using System.Linq; // Importa LINQ para consultas sobre colecciones.
using System.Globalization; // Importa facilidades de cultura para parsear fechas y números decimales.
using ExploradorArchivos.AppDataFusion.Models; // Importa el modelo DataItem de la aplicación.

namespace ExploradorArchivos.AppDataFusion.Readers;

/// <summary>
/// Clase estática para leer y procesar archivos CSV mapeándolos dinámicamente a DataItem.
/// </summary>
public static class CsvDataReader
{
    // Almacena los nombres de columnas detectados en la fila de encabezados del archivo CSV.
    public static List<string> UltimasColumnas { get; private set; } = new List<string>();

    // Guarda la relación mapeada entre columnas del CSV y los roles de la aplicación (ej. "precio" -> "Valor").
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Abre y lee un archivo CSV en modo streaming, mapeando dinámicamente sus columnas
    /// a las propiedades de DataItem según las coincidencias semánticas encontradas.
    /// </summary>
    public static List<DataItem> Leer(string rutaArchivo, char separador = ',')
    {
        // Instancia la lista que guardará los registros finales.
        var lista = new List<DataItem>();

        // Si el archivo no existe físicamente, escribe una advertencia y retorna la lista vacía.
        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[CSV] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            // Abre un stream de lectura para procesar línea por línea sin cargar todo en memoria.
            using var reader = new StreamReader(rutaArchivo);
            
            // Lee la primera línea que se asume contiene las cabeceras/columnas.
            string? lineaEncabezado = reader.ReadLine();
            // Si el archivo está vacío, retorna la lista vacía.
            if (lineaEncabezado == null) return lista;

            // Autodetecta el separador si el provisto no se encuentra y hay punto y coma o tabuladores.
            if (!lineaEncabezado.Contains(separador) && lineaEncabezado.Contains(';'))
                separador = ';';
            else if (!lineaEncabezado.Contains(separador) && lineaEncabezado.Contains('\t'))
                separador = '\t';

            // Separa los encabezados usando el método de parseo robusto y los limpia de comillas.
            var encabezadosRaw = SepararCsvRobust(lineaEncabezado, separador)
                                       .Select(h => h.Trim().Replace("\"", ""))
                                       .ToArray();
            
            // Filtra y descarta las columnas fantasma vacías al final de los encabezados.
            int totalCols = encabezadosRaw.Length;
            while (totalCols > 0 && string.IsNullOrWhiteSpace(encabezadosRaw[totalCols - 1]))
                totalCols--;

            // Convierte todos los encabezados válidos a minúsculas para un emparejamiento semántico homogéneo.
            var encabezados = encabezadosRaw.Take(totalCols)
                                       .Select(h => h.ToLowerInvariant())
                                       .ToArray();

            // Crea un diccionario de búsqueda rápida para relacionar el nombre de columna con su índice numérico.
            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < encabezados.Length; i++)
                mapa[encabezados[i]] = i;

            // Almacena las columnas procesadas finales en la variable global.
            UltimasColumnas = encabezadosRaw.Take(totalCols).ToList();

            // Identifica la posición de cada campo en base a los listados de alias.
            int idxId = BuscarColumna(mapa, "id", "car_id", "usedcarskuid", "sku_id", "codigo", "code", "sku", "#", "uuid", "guid", "_id", "num", "numero", "idx", "index", "rank", "no", "student_id");
            int idxNombre = BuscarColumna(mapa, "nombre", "name", "titulo", "title", "producto", "juego", "descripcion", "description", "brand", "marca", "model", "modelo", "micrositio", "persona", "person", "autor", "author", "atleta", "athlete");
            int idxCat = BuscarColumna(mapa, "categoria", "category", "genero", "gender", "sexo", "sex", "genre", "tipo", "type", "grupo", "group", "body", "car_type", "level", "nivel", "clasificacion", "clase", "class", "division");
            int idxValor = BuscarColumna(mapa, "valor", "value", "precio", "price", "monto", "amount", "ventas", "sales", "score", "puntos", "points", "salary", "total", "edad", "age", "promedio", "avg", "cantidad", "count");
            int idxFecha = BuscarColumnaExacta(mapa, "fecha", "date", "releasedate", "created_at", "anio", "model_year", "periodo", "period", "updated_at", "timestamp", "fecha_registro", "fecha_reporte");

            // Limpia y crea el mapeo de metadatos global de columnas.
            MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (idxId >= 0) MapeoColumnas[encabezados[idxId]] = "Id";
            if (idxNombre >= 0) MapeoColumnas[encabezados[idxNombre]] = "Nombre";
            if (idxCat >= 0) MapeoColumnas[encabezados[idxCat]] = "Categoria";
            if (idxValor >= 0) MapeoColumnas[encabezados[idxValor]] = "Valor";
            if (idxFecha >= 0) MapeoColumnas[encabezados[idxFecha]] = "Fecha";

            // Conjunto de índices que han sido mapeados para excluirlos de CamposExtra.
            var mapeadas = new HashSet<int>(new[] { idxId, idxNombre, idxCat, idxValor, idxFecha }.Where(x => x >= 0));

            int fila = 1;
            string? linea;
            // Lee línea por línea el archivo CSV (modo streaming) hasta llegar al final.
            while ((linea = reader.ReadLine()) != null)
            {
                // Ignora líneas que estén totalmente en blanco.
                if (string.IsNullOrWhiteSpace(linea)) continue;
                
                // Remueve separadores y espacios; si no queda nada de texto, descarta la fila como fila basura.
                string lineaLimpia = linea.Replace(separador.ToString(), "").Trim();
                if (string.IsNullOrWhiteSpace(lineaLimpia)) continue;

                try
                {
                    // Obtiene los valores de las columnas usando el separador robusto.
                    var cols = SepararCsvRobust(linea, separador);

                    // Crea la instancia de DataItem asignando origen "csv".
                    var item = new DataItem { Fuente = "csv" };

                    // Procesa el ID; si no existe, asigna el número de fila actual.
                    int parsedId = idxId >= 0 ? ParseInt(cols, idxId) : 0;
                    item.Id = parsedId != 0 ? parsedId : fila;

                    // Si el ID original en el CSV era texto o inválido, lo conserva en CamposExtra.
                    if (idxId >= 0 && parsedId == 0)
                    {
                        string rawId = Limpiar(cols, idxId);
                        if (!string.IsNullOrEmpty(rawId)) item.CamposExtra[encabezados[idxId]] = rawId;
                    }

                    // Mapea las propiedades estándar Nombre, Categoría, Valor y Fecha del DataItem.
                    item.Nombre = idxNombre >= 0 ? Limpiar(cols, idxNombre) : $"Fila-{fila}";
                    item.Categoria = idxCat >= 0 ? Limpiar(cols, idxCat) : "Sin categoría";
                    item.Valor = idxValor >= 0 ? ParseDouble(cols, idxValor) : 0;
                    item.Fecha = idxFecha >= 0 ? ParseFechaEspecial(cols, idxFecha, encabezados[idxFecha]) : DateTime.Now;

                    // Agrega el resto de columnas no mapeadas directamente al diccionario CamposExtra del DataItem.
                    for (int c = 0; c < encabezados.Length; c++)
                    {
                        if (mapeadas.Contains(c)) continue; // Omite las mapeadas.
                        string val = c < cols.Count ? cols[c].Trim().Replace("\"", "") : "";
                        item.CamposExtra[encabezados[c]] = val;
                    }

                    // Agrega el objeto mapeado a la lista de salida.
                    lista.Add(item);
                }
                catch 
                {
                    // Control de errores silencioso por registro corrupto en el CSV.
                }
                fila++;
            }

            Console.WriteLine($"[CSV] ✓  {lista.Count} registros leídos (Streaming) desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            // Registra errores de entrada/salida o parsing crítico.
            Console.WriteLine($"[CSV] ✗  Error leyendo archivo: {ex.Message}");
        }

        return lista;
    }

    /// <summary>
    /// Parser robusto personalizado que divide una línea en columnas respetando
    /// el encapsulamiento de comillas dobles, llaves JSON o corchetes que puedan contener el caracter separador.
    /// </summary>
    private static List<string> SepararCsvRobust(string linea, char sep)
    {
        var campos = new List<string>();
        var actual = new System.Text.StringBuilder();
        bool enComillas = false;
        int profCorchete = 0;   // cuenta corchetes '[' sin cerrar.
        int profLlave = 0;      // cuenta llaves '{' sin cerrar.

        // Recorre carácter por carácter de la línea del CSV.
        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];

            // Rellena y conmuta el estado de lectura en comillas dobles si no está dentro de subestructuras.
            if (c == '"' && profCorchete == 0 && profLlave == 0)
            {
                enComillas = !enComillas;
                actual.Append(c); // Agrega la comilla para que Limpiar() se encargue de ella.
                continue;
            }

            // Dentro de las comillas dobles los separadores no tienen efecto; se copia directamente.
            if (enComillas)
            {
                actual.Append(c);
                continue;
            }

            // Rastrear la profundidad de corchetes y llaves para no partir en separadores de sub-objetos o listas (JSON).
            if (c == '[') { profCorchete++; actual.Append(c); continue; }
            if (c == ']') { profCorchete = Math.Max(0, profCorchete - 1); actual.Append(c); continue; }
            if (c == '{') { profLlave++; actual.Append(c); continue; }
            if (c == '}') { profLlave = Math.Max(0, profLlave - 1); actual.Append(c); continue; }

            // Divide la columna únicamente cuando encuentra el separador y no está dentro de ninguna estructura.
            if (c == sep && profCorchete == 0 && profLlave == 0)
            {
                campos.Add(actual.ToString());
                actual.Clear();
                continue;
            }

            actual.Append(c);
        }
        // Agrega el residuo final de la línea.
        campos.Add(actual.ToString());
        return campos;
    }

    // Busca un alias dentro del diccionario de cabeceras mapeadas y retorna su posición.
    private static int BuscarColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        // Retorna el índice si localiza alguno de los aliases con LINQ.
        return alias.Where(mapa.ContainsKey).Select(a => mapa[a]).FirstOrDefault(-1);
    }

    // Busca coincidencia exacta del alias y devuelve el índice.
    private static int BuscarColumnaExacta(Dictionary<string, int> mapa, params string[] alias)
    {
        return alias.Where(mapa.ContainsKey).Select(a => mapa[a]).FirstOrDefault(-1);
    }

    // Retorna la columna limpia sin espacios al inicio/final ni comillas dobles.
    private static string Limpiar(List<string> cols, int idx)
    {
        return idx >= 0 && idx < cols.Count ? cols[idx].Trim().Replace("\"", "") : "";
    }

    // Parseador de seguridad para enteros de columnas.
    private static int ParseInt(List<string> cols, int idx)
    {
        return idx >= 0 && idx < cols.Count && int.TryParse(cols[idx].Trim(), out int v) ? v : 0;
    }

    // Parseador de seguridad para doubles de columnas con punto decimal.
    private static double ParseDouble(List<string> cols, int idx)
    {
        if (idx < 0 || idx >= cols.Count) return 0;
        string s = cols[idx].Trim().Replace("\"", "");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            return v;
        return 0;
    }

    // Parseador especial para columnas que representan un año en número ("2023") o fechas completas.
    private static DateTime ParseFechaEspecial(List<string> cols, int idx, string nombreColumna)
    {
        if (idx < 0 || idx >= cols.Count) return DateTime.Now;

        string valor = cols[idx].Trim();
        string colLow = nombreColumna.ToLower();

        // Si la columna es del año, construye la fecha al primer día del año detectado.
        if (colLow is "anio" or "año" or "year" or "model_year" or "myear")
        {
            if (int.TryParse(valor, out int anio))
                return new DateTime(Math.Clamp(anio, 1900, 2100), 1, 1);
        }

        if (DateTime.TryParse(valor, out DateTime d)) return d;
        return DateTime.Now;
    }
}
