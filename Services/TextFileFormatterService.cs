using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Servicio encargado de la lectura y el formateo de archivos de texto (JSON, XML, CSV).
    /// </summary>
    public static class TextFileFormatterService
    {
        /// <summary>
        /// Lee el archivo de texto y devuelve su contenido formateado según su extensión, junto con una descripción visual.
        /// </summary>
        public static string LeerYFormatear(string filePath, out string infoVisual)
        {
            string content = File.ReadAllText(filePath);
            string extension = Path.GetExtension(filePath).ToLower();
            string nombreArchivo = Path.GetFileName(filePath);
            infoVisual = nombreArchivo;

            if (extension == ".json")
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    content = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    infoVisual = $"{nombreArchivo} [JSON Formateado]";
                }
                catch
                {
                    // Fallback a texto plano si falla
                }
            }
            else if (extension == ".xml")
            {
                try
                {
                    var doc = XDocument.Parse(content);
                    content = doc.ToString();
                    infoVisual = $"{nombreArchivo} [XML Formateado]";
                }
                catch
                {
                    // Fallback a texto plano si falla
                }
            }
            else if (extension == ".csv")
            {
                try
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var maxLen = lines.Select(l => l.Split(',').Length).Max();
                        var colsWidth = new int[maxLen];
                        var grid = lines.Select(l => l.Split(',')).ToList();

                        foreach (var row in grid)
                        {
                            for (int i = 0; i < row.Length; i++)
                            {
                                colsWidth[i] = Math.Max(colsWidth[i], row[i].Trim().Length);
                            }
                        }

                        var sb = new System.Text.StringBuilder();
                        foreach (var row in grid)
                        {
                            for (int i = 0; i < row.Length; i++)
                            {
                                sb.Append(row[i].Trim().PadRight(colsWidth[i] + 4));
                            }
                            sb.AppendLine();
                        }
                        content = sb.ToString();
                        infoVisual = $"{nombreArchivo} [CSV Formateado]";
                    }
                }
                catch
                {
                    // Fallback a texto plano si falla
                }
            }
            else if (extension == ".md")
            {
                infoVisual = $"{nombreArchivo} [Markdown]";
            }

            return content;
        }
    }
}
