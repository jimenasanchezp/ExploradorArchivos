using System;
using System.IO;
using System.Collections.Generic;
using Xceed.Document.NET;
using Xceed.Words.NET;
using ClosedXML.Excel;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

namespace ExploradorArchivos.Services;

/// <summary>
/// Motor de exportación universal para archivos. 
/// Permite transformar archivos de texto o imágenes a formatos de Office (DOCX, XLSX, PPTX) y PDF.
/// </summary>
public static class FileConverterService
{
    /// <summary>
    /// Convierte un archivo físico a otro formato según la extensión de destino.
    /// Crea automáticamente nombres secuenciales si el archivo destino ya existe (ej. archivo (1).pdf).
    /// </summary>
    /// <param name="rutaOrigen">Ruta absoluta del archivo original.</param>
    /// <param name="formatoDestino">Extensión de destino (ej. ".pdf", ".docx").</param>
    public static void Convertir(string rutaOrigen, string formatoDestino)
    {
        string dir = Path.GetDirectoryName(rutaOrigen)!;
        string nombreSinExt = Path.GetFileNameWithoutExtension(rutaOrigen);
        string nuevaRuta = Path.Combine(dir, $"{nombreSinExt}{formatoDestino}");

        int contador = 1;
        while (File.Exists(nuevaRuta))
        {
            nuevaRuta = Path.Combine(dir, $"{nombreSinExt} ({contador}){formatoDestino}");
            contador++;
        }

        string extOrigen = Path.GetExtension(rutaOrigen).ToLower();
        bool esImagen = extOrigen == ".jpg" || extOrigen == ".jpeg" || extOrigen == ".png" || extOrigen == ".bmp";
        
        switch (formatoDestino.ToLower())
        {
            case ".docx":
                ConvertirADocx(rutaOrigen, nuevaRuta, esImagen);
                break;
            case ".xlsx":
                ConvertirAXlsx(rutaOrigen, nuevaRuta, esImagen);
                break;
            case ".pptx":
                ConvertirAPptx(rutaOrigen, nuevaRuta, esImagen);
                break;
            case ".pdf":
                ConvertirAPdf(rutaOrigen, nuevaRuta, esImagen);
                break;
            default:
                throw new NotSupportedException($"El formato {formatoDestino} no está soportado.");
        }
    }

    private static void ConvertirADocx(string rutaOrigen, string nuevaRuta, bool esImagen)
    {
        using (var doc = DocX.Create(nuevaRuta))
        {
            if (esImagen)
            {
                var img = doc.AddImage(rutaOrigen);
                var picture = img.CreatePicture();
                var p = doc.InsertParagraph();
                p.AppendPicture(picture);
            }
            else
            {
                var p = doc.InsertParagraph();
                int contador = 0;
                int maxLineasPermitidas = 50000; // Límite de seguridad para evitar que DocX reviente la memoria RAM
                
                foreach (var linea in ExtraerLineas(rutaOrigen))
                {
                    if (contador > maxLineasPermitidas)
                    {
                        p = doc.InsertParagraph("\n[... CONTENIDO TRUNCADO POR LÍMITE DE TAMAÑO ...]");
                        break;
                    }

                    p.Append(linea).AppendLine();
                    contador++;

                    // Crear un nuevo párrafo cada 100 líneas para no ahogar un solo nodo del DOM
                    if (contador % 100 == 0)
                    {
                        p = doc.InsertParagraph();
                    }
                }
            }
            doc.Save();
        }
    }

    private static void ConvertirAXlsx(string rutaOrigen, string nuevaRuta, bool esImagen)
    {
        using (var workbook = new XLWorkbook())
        {
            if (esImagen)
            {
                var worksheet = workbook.Worksheets.Add("Hoja1");
                worksheet.AddPicture(rutaOrigen).MoveTo(worksheet.Cell(1, 1));
            }
            else
            {
                int hojaActual = 1;
                int filaActual = 1;
                var worksheet = workbook.Worksheets.Add($"Hoja{hojaActual}");
                string extension = Path.GetExtension(rutaOrigen).ToLower();
                bool esCsvOTxt = extension == ".csv" || extension == ".txt";

                foreach (var linea in ExtraerLineas(rutaOrigen))
                {
                    if (filaActual > 1000000) // Límite de seguridad para Excel
                    {
                        hojaActual++;
                        worksheet = workbook.Worksheets.Add($"Hoja{hojaActual}");
                        filaActual = 1;
                    }

                    if (esCsvOTxt)
                    {
                        var delimitador = linea.Contains('\t') ? '\t' : ',';
                        var columnas = linea.Split(delimitador);
                        for (int col = 0; col < columnas.Length; col++)
                        {
                            worksheet.Cell(filaActual, col + 1).Value = columnas[col].Trim();
                        }
                    }
                    else
                    {
                        worksheet.Cell(filaActual, 1).Value = linea;
                    }
                    
                    filaActual++;
                }
            }
            workbook.SaveAs(nuevaRuta);
        }
    }

    private static void ConvertirAPdf(string rutaOrigen, string nuevaRuta, bool esImagen)
    {
        var pdf = new PdfDocument();
        
        if (esImagen)
        {
            var page = pdf.AddPage();
            using (var gfx = XGraphics.FromPdfPage(page))
            using (var image = XImage.FromFile(rutaOrigen))
            {
                double ratio = Math.Min(page.Width / image.PixelWidth, page.Height / image.PixelHeight);
                gfx.DrawImage(image, 0, 0, image.PixelWidth * ratio, image.PixelHeight * ratio);
            }
        }
        else
        {
            var font = new XFont("Arial", 11, XFontStyle.Regular);
            var page = pdf.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            
            double margen = 40;
            double yPos = margen;
            double xPos = margen;
            double lineHeight = font.GetHeight();
            double maxWidth = page.Width - (margen * 2);

            foreach (var linea in ExtraerLineas(rutaOrigen))
            {
                var lineasEnvueltas = DividirTextoEnLineasCortas(gfx, linea, font, maxWidth);
                
                foreach (var textToDraw in lineasEnvueltas)
                {
                    if (yPos + lineHeight > page.Height - margen)
                    {
                        gfx.Dispose();
                        page = pdf.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPos = margen;
                    }
                    
                    gfx.DrawString(textToDraw, font, XBrushes.Black, new XRect(xPos, yPos, maxWidth, lineHeight), XStringFormats.TopLeft);
                    yPos += lineHeight;
                }
            }
            gfx.Dispose();
        }
        pdf.Save(nuevaRuta);
    }

    private static IEnumerable<string> DividirTextoEnLineasCortas(XGraphics gfx, string texto, XFont font, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            yield return texto;
            yield break;
        }

        var palabras = texto.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (palabras.Length == 0)
        {
            yield return texto;
            yield break;
        }

        string lineaActual = "";
        foreach (var palabra in palabras)
        {
            string prueba = string.IsNullOrEmpty(lineaActual) ? palabra : lineaActual + " " + palabra;
            var size = gfx.MeasureString(prueba, font);

            if (size.Width > maxWidth && !string.IsNullOrEmpty(lineaActual))
            {
                yield return lineaActual;
                lineaActual = palabra;
            }
            else
            {
                lineaActual = prueba;
            }
        }

        if (!string.IsNullOrEmpty(lineaActual))
        {
            yield return lineaActual;
        }
    }

    private static void ConvertirAPptx(string rutaOrigen, string nuevaRuta, bool esImagen)
    {
        using (PresentationDocument presentationDoc = PresentationDocument.Create(nuevaRuta, PresentationDocumentType.Presentation))
        {
            PresentationPart presentationPart = presentationDoc.AddPresentationPart();
            presentationPart.Presentation = new Presentation();

            SlideMasterIdList slideMasterIdList = new SlideMasterIdList(new SlideMasterId() { Id = (UInt32Value)2147483648U, RelationshipId = "rId1" });
            SlideIdList slideIdList = new SlideIdList();
            
            presentationPart.Presentation.Append(slideMasterIdList, slideIdList);

            SlideLayoutPart slideLayoutPart = presentationPart.AddNewPart<SlideLayoutPart>("rId1");
            slideLayoutPart.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));
            
            SlideMasterPart slideMasterPart = slideLayoutPart.AddNewPart<SlideMasterPart>("rId3");
            slideMasterPart.SlideMaster = new SlideMaster(new CommonSlideData(new ShapeTree()));
            
            ThemePart themePart = slideMasterPart.AddNewPart<ThemePart>("rId4");
            themePart.Theme = new D.Theme() { Name = "Office Theme" };

            if (!esImagen)
            {
                int lineCount = 0;
                string currentSlideText = "";
                uint slideIdIndex = 256;

                foreach (var linea in ExtraerLineas(rutaOrigen))
                {
                    currentSlideText += linea + "\n";
                    lineCount++;

                    if (lineCount >= 22) // Aproximadamente 22 líneas por diapositiva
                    {
                        AddSlideToPresentation(presentationPart, slideIdList, slideIdIndex, currentSlideText, slideLayoutPart);
                        slideIdIndex++;
                        lineCount = 0;
                        currentSlideText = "";
                    }
                }

                if (!string.IsNullOrEmpty(currentSlideText))
                {
                    AddSlideToPresentation(presentationPart, slideIdList, slideIdIndex, currentSlideText, slideLayoutPart);
                }
            }
            else
            {
                // Just create one empty slide for now for images
                AddSlideToPresentation(presentationPart, slideIdList, 256, "Imagen (soporte de imagen en PPTX no implementado completamente)", slideLayoutPart);
            }
            
            presentationPart.Presentation.Save();
        }
    }

    private static void AddSlideToPresentation(PresentationPart presentationPart, SlideIdList slideIdList, uint slideIdIndex, string text, SlideLayoutPart slideLayoutPart)
    {
        SlidePart slidePart = presentationPart.AddNewPart<SlidePart>($"rId{slideIdIndex + 100}");
        slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));

        ShapeTree shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;
        var shape = new Shape();
        shape.NonVisualShapeProperties = new NonVisualShapeProperties(
            new NonVisualDrawingProperties() { Id = 2, Name = "TextBox" },
            new NonVisualShapeDrawingProperties(),
            new ApplicationNonVisualDrawingProperties());
        
        shape.ShapeProperties = new ShapeProperties(
            new D.Transform2D(
                new D.Offset() { X = 500000L, Y = 500000L },
                new D.Extents() { Cx = 8000000L, Cy = 6000000L }),
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle });
        
        shape.TextBody = new TextBody(
            new D.BodyProperties(),
            new D.ListStyle(),
            new D.Paragraph(new D.Run(new D.Text(text))));

        shapeTree.AppendChild(shape);
        slidePart.AddPart(slideLayoutPart);

        slideIdList.Append(new SlideId() { Id = slideIdIndex, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
    }

    private static IEnumerable<string> ExtraerLineas(string ruta)
    {
        string ext = Path.GetExtension(ruta).ToLower();
        
        if (ext == ".docx")
        {
            using (var doc = DocX.Load(ruta))
            {
                var lines = doc.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var l in lines) yield return l;
            }
        }
        else if (ext == ".xlsx")
        {
            using (var wb = new XLWorkbook(ruta))
            {
                var ws = wb.Worksheet(1);
                var lastCell = ws.LastCellUsed();
                if (lastCell != null)
                {
                    for (int r = 1; r <= lastCell.Address.RowNumber; r++)
                    {
                        var rowText = "";
                        for (int c = 1; c <= lastCell.Address.ColumnNumber; c++)
                        {
                            rowText += ws.Cell(r, c).GetString() + "\t";
                        }
                        yield return rowText.TrimEnd('\t');
                    }
                }
            }
        }
        else if (ext == ".pptx")
        {
            using (PresentationDocument ppt = PresentationDocument.Open(ruta, false))
            {
                if (ppt.PresentationPart?.Presentation?.SlideIdList != null)
                {
                    foreach (var slideId in ppt.PresentationPart.Presentation.SlideIdList.Elements<SlideId>())
                    {
                        var slidePart = (SlidePart)ppt.PresentationPart.GetPartById(slideId.RelationshipId!);
                        if (slidePart.Slide != null)
                        {
                            foreach (var textBody in slidePart.Slide.Descendants<D.TextBody>())
                            {
                                var lines = textBody.InnerText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var l in lines) yield return l;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // Fallback para texto plano usando streaming directo
            using (var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }
}
