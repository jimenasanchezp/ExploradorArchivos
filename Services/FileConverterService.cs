using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xceed.Document.NET;
using Xceed.Words.NET;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

namespace ExploradorArchivos.Services;

/// <summary>
/// Motor de exportación universal para archivos. 
/// Coordina el flujo de conversión delegando a la estrategia correspondiente.
/// </summary>
public static class FileConverterService
{
    /// <summary>
    /// Convierte de forma asíncrona un archivo de origen físico a un formato de destino especificado
    /// utilizando la estrategia adecuada. El trabajo de conversión se ejecuta fuera del hilo de la UI.
    /// </summary>
    public static async Task ConvertirAsync(string rutaOrigen, string formatoDestino)
    {
        string directorioDestino = Path.GetDirectoryName(rutaOrigen)!;
        string nombreSinExtension = Path.GetFileNameWithoutExtension(rutaOrigen);
        
        string rutaArchivoDestino = Path.Combine(directorioDestino, $"{nombreSinExtension}{formatoDestino}");

        int numeroIntento = 1;
        while (File.Exists(rutaArchivoDestino))
        {
            rutaArchivoDestino = Path.Combine(directorioDestino, $"{nombreSinExtension} ({numeroIntento}){formatoDestino}");
            numeroIntento++;
        }

        string extensionOrigen = Path.GetExtension(rutaOrigen).ToLower();
        bool esArchivoImagen = extensionOrigen == ".jpg" ||
                               extensionOrigen == ".jpeg" ||
                               extensionOrigen == ".png"  ||
                               extensionOrigen == ".bmp";

        IFileConverter conversor = FileConverterFactory.GetConverter(rutaOrigen, formatoDestino, esArchivoImagen);
        await conversor.ConvertirAsync(rutaOrigen, rutaArchivoDestino, esArchivoImagen);
    }


    /// <summary>
    /// Elimina caracteres de control que son inválidos en XML (0x00–0x08, 0x0B, 0x0C, 0x0E–0x1F).
    /// </summary>
    public static string SanitizarTextoXml(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (char c in texto)
        {
            if (c == 0x09 || c == 0x0A || c == 0x0D || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD))
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Lee secuencialmente las líneas de texto del archivo físico, detectando el formato del archivo de entrada.
    /// </summary>
    public static IEnumerable<string> ExtraerLineas(string rutaArchivo)
    {
        string extension = Path.GetExtension(rutaArchivo).ToLower();
        string[] extensionesTextoValidas = { ".txt", ".csv", ".json", ".xml", ".cs", ".html", ".css", ".js", ".md", ".py" };
        
        if (extension == ".docx")
        {
            using (DocX documentoWord = DocX.Load(rutaArchivo))
            {
                string[] lineasWord = SanitizarTextoXml(documentoWord.Text).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string linea in lineasWord)
                    yield return linea;
            }
        }
        else if (extension == ".xlsx")
        {
            using (XLWorkbook libroExcel = new XLWorkbook(rutaArchivo))
            {
                IXLWorksheet hojaExcel = libroExcel.Worksheet(1);
                IXLCell ultimaCeldaUsada = hojaExcel.LastCellUsed();
                if (ultimaCeldaUsada != null)
                {
                    for (int fila = 1; fila <= ultimaCeldaUsada.Address.RowNumber; fila++)
                    {
                        string textoFila = "";
                        for (int columna = 1; columna <= ultimaCeldaUsada.Address.ColumnNumber; columna++)
                            textoFila += hojaExcel.Cell(fila, columna).GetString() + "\t";
                        
                        yield return SanitizarTextoXml(textoFila.TrimEnd('\t'));
                    }
                }
            }
        }
        else if (extension == ".pptx")
        {
            using (PresentationDocument presentacionPowerPoint = PresentationDocument.Open(rutaArchivo, false))
            {
                if (presentacionPowerPoint.PresentationPart?.Presentation?.SlideIdList != null)
                {
                    foreach (SlideId idDiapositiva in presentacionPowerPoint.PresentationPart.Presentation.SlideIdList.Elements<SlideId>())
                    {
                        SlidePart parteDiapositiva = (SlidePart)presentacionPowerPoint.PresentationPart.GetPartById(idDiapositiva.RelationshipId!);
                        if (parteDiapositiva.Slide == null) continue;
 
                        foreach (TextBody cuerpoTexto in parteDiapositiva.Slide.Descendants<TextBody>())
                        {
                            foreach (D.Paragraph parrafo in cuerpoTexto.Descendants<D.Paragraph>())
                            {
                                var sbLinea = new System.Text.StringBuilder();
                                foreach (D.Run run in parrafo.Descendants<D.Run>())
                                    sbLinea.Append(run.Text?.Text ?? "");
 
                                string lineaParrafo = SanitizarTextoXml(sbLinea.ToString());
                                if (!string.IsNullOrWhiteSpace(lineaParrafo))
                                    yield return lineaParrafo;
                            }
                        }
                    }
                }
            }
        }
        else if (extensionesTextoValidas.Contains(extension))
        {
            using (FileStream flujoArchivo = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader lectorFlujo = new StreamReader(flujoArchivo))
            {
                string? lineaTexto;
                while ((lineaTexto = lectorFlujo.ReadLine()) != null)
                    yield return SanitizarTextoXml(lineaTexto);
            }
        }
        else
        {
            throw new NotSupportedException($"No se admite la extracción de texto para archivos con extensión '{extension}' en este tipo de conversión.");
        }
    }
}
