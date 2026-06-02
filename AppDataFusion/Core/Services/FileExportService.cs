using ExploradorArchivos.AppDataFusion.Models;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ExploradorArchivos.AppDataFusion.Services;

/// <summary>
/// Proporciona métodos estáticos para exportar colecciones de datos estructurados (<c>DataItem</c>) 
/// a múltiples formatos de archivo (CSV, JSON, XML, TXT).
/// </summary>
public static class FileExportService
{
    /// <summary>
    /// Exporta los datos a un archivo de texto delimitado por comas (CSV).
    /// </summary>
    public static void ExportarCsv(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Obtener columnas por defecto si la lista de columnas de entrada es nula
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        // Inicializar el diccionario de mapeo si es nulo ignorando mayúsculas y minúsculas
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Lista auxiliar de líneas de texto a exportar
        var lineas = new List<string>();
        
        // Agregar la línea de cabeceras escapando de forma correcta los caracteres CSV
        lineas.Add(string.Join(",", cols.Select(EscapeCsv)));
        
        // Iterar en los datos transformando cada elemento a una fila CSV separada por comas usando LINQ
        lineas.AddRange(datos.Select(item => 
            string.Join(",", cols.Select(c => EscapeCsv(GetValorExport(item, c, map))))
        ));
        
        // Guardar todas las líneas en el archivo codificado con UTF-8
        File.WriteAllLines(ruta, lineas, Encoding.UTF8);
    }

    /// <summary>
    /// Exporta los datos a formato JSON, construyendo la estructura limpia y concisa con LINQ.
    /// </summary>
    public static void ExportarJson(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Resolver columnas predeterminadas
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        // Inicializar diccionario de mapeo de metadatos
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Mapear cada elemento a su representación en formato de objeto JSON utilizando LINQ
        var registrosJson = datos.Select(item =>
        {
            // Mapear cada columna a un par "clave": valor en JSON
            var camposJson = cols.Select(col =>
            {
                // Extraer el valor correspondiente
                string val = GetValorExport(item, col, map);
                // Validar si el valor puede ser numérico para evitar ponerlo entre comillas dobles
                bool esNum = double.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _) && val.Length > 0;
                // Escapar la cadena si no es un tipo numérico válido
                string jsonVal = esNum ? val : JsonStr(val);
                // Retornar propiedad formateada
                return $"    {JsonStr(col)}: {jsonVal}";
            });
            // Unir propiedades por comas y envolverlas entre llaves
            return "  {\n" + string.Join(",\n", camposJson) + "\n  }";
        });

        // Construir arreglo JSON completo estructurando el resultado final
        string jsonCompleto = "[\n" + string.Join(",\n", registrosJson) + "\n]";
        // Escribir el contenido estructurado en la ruta destino
        File.WriteAllText(ruta, jsonCompleto, Encoding.UTF8);
    }

    /// <summary>
    /// Exporta la lista de elementos en un formato XML limpio y jerárquico mediante LINQ.
    /// </summary>
    public static void ExportarXml(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Obtener nombres de columnas
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        // Obtener el diccionario de mapeo
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Mapear recursivamente el listado de datos a estructuras XML de registro usando LINQ
        var registrosXml = datos.Select(item =>
        {
            // Generar las etiquetas y valores XML para cada columna
            var camposXml = cols.Select(col =>
            {
                // Sanitizar el nombre del tag XML
                string tag = XmlTag(col);
                // Recuperar y escapar de forma segura el valor interno del nodo
                string val = GetValorExport(item, col, map);
                return $"    <{tag}>{XmlEscape(val)}</{tag}>";
            });
            // Envolver la lista de nodos internos dentro de etiquetas de registro
            return "  <registro>\n" + string.Join("\n", camposXml) + "\n  </registro>";
        });

        // Instanciar StringBuilder para estructurar el archivo XML completo
        var sb = new StringBuilder();
        // Definir la cabecera estándar de XML
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        // Nodo raíz del conjunto de datos
        sb.AppendLine("<dataset>");
        // Adjuntar todos los registros generados
        sb.AppendLine(string.Join("\n", registrosXml));
        // Cerrar nodo raíz
        sb.AppendLine("</dataset>");
        
        // Escribir el documento en el archivo destino
        File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Exporta los datos estructurados a un archivo TXT delimitado por barras verticales.
    /// </summary>
    public static void ExportarTxt(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Establecer columnas
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        // Inicializar mapeos
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Lista de líneas del archivo de salida
        var lineas = new List<string>();
        
        // Agregar cabecera uniendo las columnas por el delimitador
        lineas.Add(string.Join("|", cols));
        
        // Proyectar cada objeto a una línea unida por pipes utilizando LINQ
        lineas.AddRange(datos.Select(item =>
            string.Join("|", cols.Select(c => GetValorExport(item, c, map).Replace("|", " ")))
        ));
        
        // Escribir de forma masiva en el disco en codificación UTF-8
        File.WriteAllLines(ruta, lineas, Encoding.UTF8);
    }

    /// <summary>
    /// Exporta el listado CSV y devuelve un array de bytes en memoria.
    /// </summary>
    public static byte[] ExportarCsvBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Crear una ruta de archivo temporal única en el sistema
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_csv_{Guid.NewGuid()}.csv");
        try 
        { 
            // Exportar físicamente al archivo temporal
            ExportarCsv(tmp, datos, columnas, mapeo); 
            // Retornar los bytes leídos de dicho archivo temporal
            return File.ReadAllBytes(tmp); 
        }
        finally 
        { 
            // Asegurar la remoción y limpieza del archivo temporal creado
            if (File.Exists(tmp)) File.Delete(tmp); 
        }
    }

    /// <summary>
    /// Exporta el listado JSON y devuelve un array de bytes en memoria.
    /// </summary>
    public static byte[] ExportarJsonBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Ruta temporal única
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_json_{Guid.NewGuid()}.json");
        try 
        { 
            // Escribir archivo JSON temporal
            ExportarJson(tmp, datos, columnas, mapeo); 
            // Retornar bytes
            return File.ReadAllBytes(tmp); 
        }
        finally 
        { 
            // Eliminar archivo temporal
            if (File.Exists(tmp)) File.Delete(tmp); 
        }
    }

    /// <summary>
    /// Exporta el listado XML y devuelve un array de bytes en memoria.
    /// </summary>
    public static byte[] ExportarXmlBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Ruta temporal única
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_xml_{Guid.NewGuid()}.xml");
        try 
        { 
            // Escribir archivo XML temporal
            ExportarXml(tmp, datos, columnas, mapeo); 
            // Retornar bytes
            return File.ReadAllBytes(tmp); 
        }
        finally 
        { 
            // Eliminar archivo temporal
            if (File.Exists(tmp)) File.Delete(tmp); 
        }
    }

    /// <summary>
    /// Exporta el listado TXT delimitado y devuelve un array de bytes en memoria.
    /// </summary>
    public static byte[] ExportarTxtBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        // Ruta temporal única
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_txt_{Guid.NewGuid()}.txt");
        try 
        { 
            // Escribir archivo TXT temporal
            ExportarTxt(tmp, datos, columnas, mapeo); 
            // Retornar bytes
            return File.ReadAllBytes(tmp); 
        }
        finally 
        { 
            // Eliminar archivo temporal
            if (File.Exists(tmp)) File.Delete(tmp); 
        }
    }

    /// <summary>
    /// Recupera las columnas por defecto e integra campos adicionales mapeados de forma dinámica desde los registros locales.
    /// </summary>
    private static List<string> ObtenerColumnasPorDefecto(List<DataItem> datos)
    {
        // Columnas fijas estándar del modelo base
        var cols = new List<string> { "id", "nombre", "categoria", "valor", "fecha", "fuente" };
        
        // Obtener mediante LINQ todas las claves adicionales que existan en CamposExtra
        var extras = datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            // Excluir claves que ya pertenezcan a la lista por defecto
            .Where(k => !cols.Contains(k, StringComparer.OrdinalIgnoreCase));
        
        // Agregar las columnas extra al listado final
        cols.AddRange(extras);
        return cols;
    }

    /// <summary>
    /// Mapea y extrae el valor correspondiente a una propiedad de un DataItem en base a su nombre o metadatos de mapeo.
    /// </summary>
    public static string GetValorExport(DataItem item, string col, Dictionary<string, string> mapeo)
    {
        // 1. Validar si el campo existe textualmente en la lista de CamposExtra
        if (item.CamposExtra.TryGetValue(col, out var v)) return v ?? "";
        // 2. Validar si existe en minúsculas en CamposExtra
        if (item.CamposExtra.TryGetValue(col.ToLowerInvariant(), out v)) return v ?? "";

        // 3. Mapear de acuerdo al diccionario de mapeo o usar el valor de columna estándar
        string campo = mapeo.TryGetValue(col, out var m) ? m.ToLower() : col.ToLower();
        return campo switch
        {
            "id" => item.Id.ToString(),
            "nombre" => item.Nombre ?? "",
            "categoria" => item.Categoria ?? "",
            "valor" => item.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "fecha" => item.Fecha.ToString("yyyy-MM-dd"),
            "fuente" => item.Fuente ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Escapa las comillas dobles y envuelve el campo en formato CSV estándar.
    /// </summary>
    private static string EscapeCsv(string v) =>
        $"\"{v.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// Escapa y estructura caracteres inválidos para representarse en un string de JSON válido.
    /// </summary>
    private static string JsonStr(string v) =>
        $"\"{v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")}\"";

    /// <summary>
    /// Reemplaza caracteres HTML y XML reservados por sus entidades válidas.
    /// </summary>
    private static string XmlEscape(string v) =>
        v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("\"", "&quot;").Replace("'", "&apos;");

    /// <summary>
    /// Convierte el nombre de una cabecera de datos a un tag de XML válido evitando caracteres especiales.
    /// </summary>
    private static string XmlTag(string k)
    {
        // Reemplazar caracteres no alfanuméricos ni guiones por guión bajo usando expresiones regulares
        string t = System.Text.RegularExpressions.Regex.Replace(k, @"[^a-zA-Z0-9_\-]", "_");
        // Si el nombre del nodo inicia con un número, anteponer un guión bajo
        if (t.Length > 0 && char.IsDigit(t[0])) t = "_" + t;
        // Retornar 'campo' por defecto si el tag resultante es completamente vacío
        return string.IsNullOrEmpty(t) ? "campo" : t;
    }
}
