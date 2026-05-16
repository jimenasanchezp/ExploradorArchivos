using ExploradorArchivos.AppDataFusion.Models;
using System.Text;

namespace ExploradorArchivos.AppDataFusion.Services;

public static class FileExportService
{
    public static void ExportarCsv(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lineas = new List<string>();
        lineas.Add(string.Join(",", cols.Select(EscapeCsv)));
        foreach (var item in datos)
            lineas.Add(string.Join(",", cols.Select(c => EscapeCsv(GetValorExport(item, c, map)))));
        File.WriteAllLines(ruta, lineas, Encoding.UTF8);
    }

    public static void ExportarJson(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < datos.Count; i++)
        {
            var item = datos[i];
            sb.AppendLine("  {");
            for (int c = 0; c < cols.Count; c++)
            {
                string col = cols[c];
                string val = GetValorExport(item, col, map);
                bool isLast = c == cols.Count - 1;
                bool esNum = double.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _) && val.Length > 0;
                string jsonVal = esNum ? val : JsonStr(val);
                sb.AppendLine($"    {JsonStr(col)}: {jsonVal}{(isLast ? "" : ",")}");
            }
            sb.Append("  }");
            if (i < datos.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }
        sb.AppendLine("]");
        File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportarXml(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<dataset>");
        foreach (var item in datos)
        {
            sb.AppendLine("  <registro>");
            foreach (var col in cols)
            {
                string tag = XmlTag(col);
                string val = GetValorExport(item, col, map);
                sb.AppendLine($"    <{tag}>{XmlEscape(val)}</{tag}>");
            }
            sb.AppendLine("  </registro>");
        }
        sb.AppendLine("</dataset>");
        File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportarTxt(string ruta, List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var cols = columnas ?? ObtenerColumnasPorDefecto(datos);
        var map = mapeo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lineas = new List<string>();
        lineas.Add(string.Join("|", cols));
        foreach (var item in datos)
            lineas.Add(string.Join("|", cols.Select(c =>
                GetValorExport(item, c, map).Replace("|", " "))));
        File.WriteAllLines(ruta, lineas, Encoding.UTF8);
    }

    public static byte[] ExportarCsvBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_csv_{Guid.NewGuid()}.csv");
        try { ExportarCsv(tmp, datos, columnas, mapeo); return File.ReadAllBytes(tmp); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public static byte[] ExportarJsonBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_json_{Guid.NewGuid()}.json");
        try { ExportarJson(tmp, datos, columnas, mapeo); return File.ReadAllBytes(tmp); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public static byte[] ExportarXmlBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_xml_{Guid.NewGuid()}.xml");
        try { ExportarXml(tmp, datos, columnas, mapeo); return File.ReadAllBytes(tmp); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public static byte[] ExportarTxtBytes(List<DataItem> datos,
        List<string>? columnas = null, Dictionary<string, string>? mapeo = null)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_txt_{Guid.NewGuid()}.txt");
        try { ExportarTxt(tmp, datos, columnas, mapeo); return File.ReadAllBytes(tmp); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    private static List<string> ObtenerColumnasPorDefecto(List<DataItem> datos)
    {
        var cols = new List<string> { "id", "nombre", "categoria", "valor", "fecha", "fuente" };
        var extras = datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .Where(k => !cols.Contains(k, StringComparer.OrdinalIgnoreCase));
        cols.AddRange(extras);
        return cols;
    }

    public static string GetValorExport(DataItem item, string col, Dictionary<string, string> mapeo)
    {
        if (item.CamposExtra.TryGetValue(col, out var v)) return v ?? "";
        if (item.CamposExtra.TryGetValue(col.ToLowerInvariant(), out v)) return v ?? "";

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

    private static string EscapeCsv(string v) =>
        $"\"{v.Replace("\"", "\"\"")}\"";

    private static string JsonStr(string v) =>
        $"\"{v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")}\"";

    private static string XmlEscape(string v) =>
        v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("\"", "&quot;").Replace("'", "&apos;");

    private static string XmlTag(string k)
    {
        string t = System.Text.RegularExpressions.Regex.Replace(k, @"[^a-zA-Z0-9_\-]", "_");
        if (t.Length > 0 && char.IsDigit(t[0])) t = "_" + t;
        return string.IsNullOrEmpty(t) ? "campo" : t;
    }
}

