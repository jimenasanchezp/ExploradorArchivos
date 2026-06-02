using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Xceed.Document.NET;
using Xceed.Words.NET;
using ClosedXML.Excel;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
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
    /// Convierte un archivo físico de origen a otro formato de destino especificado (ej. de .txt a .pdf).
    /// Si el archivo de destino ya existe, genera un nombre único secuencial agregando un número (ej. archivo (1).pdf).
    /// </summary>
    /// <param name="rutaOrigen">Ruta absoluta en el disco del archivo original que se quiere convertir.</param>
    /// <param name="formatoDestino">Extensión final del archivo de salida (ej. ".pdf", ".docx", ".xlsx", ".pptx").</param>
    public static void Convertir(string rutaOrigen, string formatoDestino)
    {
        // 1. Obtener la información básica del archivo original
        string directorioDestino = Path.GetDirectoryName(rutaOrigen)!;
        string nombreSinExtension = Path.GetFileNameWithoutExtension(rutaOrigen);
        
        // 2. Establecer la ruta destino inicial tentativa
        string rutaArchivoDestino = Path.Combine(directorioDestino, $"{nombreSinExtension}{formatoDestino}");

        // 3. Si ya existe un archivo con ese nombre, buscamos un nombre secuencial libre
        int numeroIntento = 1;
        while (File.Exists(rutaArchivoDestino))
        {
            rutaArchivoDestino = Path.Combine(directorioDestino, $"{nombreSinExtension} ({numeroIntento}){formatoDestino}");
            numeroIntento++;
        }

        // 4. Determinar si el archivo original es una imagen por su extensión
        string extensionOrigen = Path.GetExtension(rutaOrigen).ToLower();
        bool esArchivoImagen = extensionOrigen == ".jpg" ||
                               extensionOrigen == ".jpeg" ||
                               extensionOrigen == ".png"  ||
                               extensionOrigen == ".bmp";

        // 5. Si LibreOffice está instalado, usarlo para la conversión (máxima fidelidad).
        //    LibreOffice headless soporta DOCX/XLSX/PPTX/TXT/imágenes → PDF y otros formatos.
        //    Si no está disponible, se usa el motor interno de C# como fallback.
        string? rutaSOffice = BuscarSOffice();
        if (rutaSOffice != null && !esArchivoImagen)
        {
            ConvertirConLibreOffice(rutaSOffice, rutaOrigen, rutaArchivoDestino, formatoDestino, directorioDestino, nombreSinExtension);
            return;
        }

        // 6. Fallback: motor interno C# (PdfSharpCore / DocX / ClosedXML / OpenXML)
        switch (formatoDestino.ToLower())
        {
            case ".docx":
                ConvertirADocx(rutaOrigen, rutaArchivoDestino, esArchivoImagen);
                break;
            case ".xlsx":
                ConvertirAXlsx(rutaOrigen, rutaArchivoDestino, esArchivoImagen);
                break;
            case ".pptx":
                ConvertirAPptx(rutaOrigen, rutaArchivoDestino, esArchivoImagen);
                break;
            case ".pdf":
                ConvertirAPdf(rutaOrigen, rutaArchivoDestino, esArchivoImagen);
                break;
            default:
                throw new NotSupportedException($"El formato de destino '{formatoDestino}' no está soportado.");
        }
    }

    // =====================================================================
    // LIBREOFFFICE HEADLESS
    // =====================================================================

    /// <summary>
    /// Busca el ejecutable soffice.exe de LibreOffice en las rutas estándar de instalación de Windows.
    /// Retorna la ruta completa si se encuentra, o null si LibreOffice no está instalado.
    /// </summary>
    private static string? BuscarSOffice()
    {
        // Rutas estándar donde LibreOffice se instala en Windows (x64 y x86)
        string[] rutasCandidatas =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),      @"LibreOffice\program\soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),   @"LibreOffice\program\soffice.exe"),
            // Instalaciones portables o alternativas
            @"C:\LibreOffice\program\soffice.exe",
            @"D:\LibreOffice\program\soffice.exe",
        };
        return rutasCandidatas.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Usa LibreOffice headless para convertir el archivo origen al formato destino con fidelidad total.
    /// LibreOffice convierte --convert-to <ext> y guarda el archivo en el directorio especificado,
    /// luego lo renombramos a la ruta destino definitiva (con numeración si ya existía).
    /// </summary>
    private static void ConvertirConLibreOffice(string rutaSOffice, string rutaOrigen,
        string rutaArchivoDestino, string formatoDestino, string directorioDestino, string nombreSinExtension)
    {
        // LibreOffice siempre guarda en el mismo directorio que el archivo de origen con el
        // nombre original + la extensión de destino (no permite especificar nombre de salida).
        // Usamos un directorio temporal para evitar colisiones con archivos existentes.
        string dirTemporal = Path.Combine(Path.GetTempPath(), $"lo_conv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirTemporal);

        try
        {
            // Determinar el filtro de conversión según el formato de destino
            string filtro = formatoDestino.ToLower() switch
            {
                ".pdf"  => "pdf",
                ".docx" => "docx",
                ".xlsx" => "xlsx",
                ".pptx" => "pptx",
                ".txt"  => "txt",
                _       => throw new NotSupportedException($"LibreOffice no puede convertir a '{formatoDestino}'.")
            };

            // Ejecutar LibreOffice en modo headless (sin interfaz gráfica)
            var psi = new ProcessStartInfo
            {
                FileName               = rutaSOffice,
                Arguments              = $"--headless --convert-to {filtro} \"{rutaOrigen}\" --outdir \"{dirTemporal}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var proceso = Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar el proceso de LibreOffice.");

            // Tiempo máximo de espera: 3 minutos (documentos grandes pueden tardar)
            bool termino = proceso.WaitForExit(180_000);
            if (!termino)
            {
                proceso.Kill();
                throw new TimeoutException("LibreOffice tardó demasiado en convertir el archivo.");
            }

            if (proceso.ExitCode != 0)
            {
                string stderr = proceso.StandardError.ReadToEnd();
                throw new InvalidOperationException($"LibreOffice falló (código {proceso.ExitCode}): {stderr}");
            }

            // LibreOffice genera el archivo con el nombre original + extensión destino
            string archivoGenerado = Path.Combine(dirTemporal,
                Path.GetFileNameWithoutExtension(rutaOrigen) + formatoDestino);

            if (!File.Exists(archivoGenerado))
                throw new FileNotFoundException("LibreOffice no generó el archivo de salida esperado.", archivoGenerado);

            // Mover el archivo generado a la ruta destino definitiva
            File.Move(archivoGenerado, rutaArchivoDestino);
        }
        finally
        {
            // Limpiar el directorio temporal siempre, aunque haya error
            if (Directory.Exists(dirTemporal))
                Directory.Delete(dirTemporal, recursive: true);
        }
    }

    /// <summary>
    /// Convierte el archivo origen a un documento de Microsoft Word (.docx).
    /// </summary>
    private static void ConvertirADocx(string rutaOrigen, string rutaArchivoDestino, bool esArchivoImagen)
    {
        // Creamos e inicializamos el archivo Word usando la librería DocX
        using (DocX documentoWord = DocX.Create(rutaArchivoDestino))
        {
            if (esArchivoImagen)
            {
                // Si es imagen, la agregamos directamente dentro del documento Word
                Xceed.Document.NET.Image imagenDocx = documentoWord.AddImage(rutaOrigen);
                Xceed.Document.NET.Picture contenedorImagen = imagenDocx.CreatePicture();
                Paragraph parrafoWord = documentoWord.InsertParagraph();
                
                parrafoWord.AppendPicture(contenedorImagen);
            }
            else
            {
                // Si es texto, leemos el archivo línea por línea y las insertamos en párrafos
                Paragraph parrafoWord = documentoWord.InsertParagraph();
                int lineasProcesadas = 0;
                int limiteMaximoLineas = 50000; // Límite de seguridad para evitar consumo excesivo de memoria RAM
                
                foreach (string lineaTexto in ExtraerLineas(rutaOrigen))
                {
                    // Si excede el límite seguro, truncamos la conversión y avisamos en el archivo
                    if (lineasProcesadas > limiteMaximoLineas)
                    {
                        parrafoWord = documentoWord.InsertParagraph("\n[... CONTENIDO TRUNCADO DEBIDO A SU GRAN TAMAÑO ...]");
                        break;
                    }

                    parrafoWord.Append(lineaTexto).AppendLine();
                    lineasProcesadas++;

                    // Para mantener el rendimiento óptimo del DOM del archivo Word, creamos un párrafo nuevo cada 100 líneas
                    if (lineasProcesadas % 100 == 0)
                    {
                        parrafoWord = documentoWord.InsertParagraph();
                    }
                }
            }

            // Guardamos físicamente los cambios realizados en el archivo Word
            documentoWord.Save();
        }
    }

    /// <summary>
    /// Convierte el archivo origen a un libro de hojas de cálculo de Microsoft Excel (.xlsx).
    /// </summary>
    private static void ConvertirAXlsx(string rutaOrigen, string rutaArchivoDestino, bool esArchivoImagen)
    {
        // Creamos un nuevo libro de trabajo de Excel utilizando ClosedXML
        using (XLWorkbook libroExcel = new XLWorkbook())
        {
            if (esArchivoImagen)
            {
                // Si es imagen, creamos una hoja de cálculo e insertamos la imagen a partir de la celda A1 (1,1)
                IXLWorksheet hojaExcel = libroExcel.Worksheets.Add("Hoja de Imagen");
                hojaExcel.AddPicture(rutaOrigen).MoveTo(hojaExcel.Cell(1, 1));
            }
            else
            {
                // Si es texto, determinamos si tiene formato separado por comas o tabulador (.csv / .txt)
                int indiceHoja = 1;
                int filaActualExcel = 1;
                IXLWorksheet hojaExcel = libroExcel.Worksheets.Add($"Hoja {indiceHoja}");
                
                string extensionOrigen = Path.GetExtension(rutaOrigen).ToLower();
                bool esArchivoDelimitado = extensionOrigen == ".csv" || extensionOrigen == ".txt";

                // Leemos línea a línea los datos e insertamos filas en la hoja de cálculo
                foreach (string lineaTexto in ExtraerLineas(rutaOrigen))
                {
                    // Excel soporta un máximo de 1,048,576 filas por hoja. Si nos acercamos al millón, creamos una nueva hoja
                    if (filaActualExcel > 1000000)
                    {
                        indiceHoja++;
                        hojaExcel = libroExcel.Worksheets.Add($"Hoja {indiceHoja}");
                        filaActualExcel = 1;
                    }

                    if (esArchivoDelimitado)
                    {
                        // Si es archivo delimitado, determinamos si usa tabulador o coma para separar las columnas
                        char caracterDelimitador = lineaTexto.Contains('\t') ? '\t' : ',';
                        string[] columnasTexto = lineaTexto.Split(caracterDelimitador);

                        // Escribimos cada columna en celdas consecutivas de la fila actual
                        for (int indiceColumna = 0; indiceColumna < columnasTexto.Length; indiceColumna++)
                        {
                            hojaExcel.Cell(filaActualExcel, indiceColumna + 1).Value = columnasTexto[indiceColumna].Trim();
                        }
                    }
                    else
                    {
                        // Si es texto plano sin separadores, volcamos toda la línea directamente en la celda de la columna A
                        hojaExcel.Cell(filaActualExcel, 1).Value = lineaTexto;
                    }
                    
                    filaActualExcel++;
                }
            }

            // Almacenamos el libro completo en el archivo XLSX
            libroExcel.SaveAs(rutaArchivoDestino);
        }
    }

    /// <summary>
    /// Convierte el archivo origen a un documento portátil PDF (.pdf) dibujando texto o ajustando imágenes.
    /// </summary>
    private static void ConvertirAPdf(string rutaOrigen, string rutaArchivoDestino, bool esArchivoImagen)
    {
        // Registrar el font resolver de sistema de Windows antes de crear cualquier documento PDF.
        // Sin esto, PdfSharpCore en .NET 8 no puede encontrar fuentes como "Arial" y DrawString
        // no dibuja nada, dejando el PDF completamente en blanco.
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new PdfSharpCore.Utils.FontResolver();

        // Instanciamos el documento PDF destino usando PdfSharpCore
        PdfDocument documentoPdf = new PdfDocument();
        
        if (esArchivoImagen)
        {
            // Creamos una nueva página del PDF y dibujamos la imagen ajustando su escala proporcionalmente
            PdfPage paginaPdf = documentoPdf.AddPage();
            using (XGraphics graficosPdf = XGraphics.FromPdfPage(paginaPdf))
            using (XImage imagenPdf = XImage.FromFile(rutaOrigen))
            {
                double escalaProporcional = Math.Min(paginaPdf.Width / imagenPdf.PixelWidth, paginaPdf.Height / imagenPdf.PixelHeight);
                graficosPdf.DrawImage(imagenPdf, 0, 0, imagenPdf.PixelWidth * escalaProporcional, imagenPdf.PixelHeight * escalaProporcional);
            }
        }
        else
        {
            // Para archivos de texto, configuramos márgenes, tipo de letra Arial 11 y área disponible
            XFont fuenteArial = new XFont("Arial", 11, XFontStyle.Regular);
            PdfPage paginaPdf = documentoPdf.AddPage();
            XGraphics graficosPdf = XGraphics.FromPdfPage(paginaPdf);
            
            double margenPagina = 40;
            double posicionY = margenPagina;
            double posicionX = margenPagina;
            double altoLinea = fuenteArial.GetHeight();
            double anchoDisponible = paginaPdf.Width - (margenPagina * 2);

            // Leemos y dibujamos línea por línea en el lienzo del PDF
            foreach (string lineaTexto in ExtraerLineas(rutaOrigen))
            {
                // Si una línea es más ancha que el papel, la dividimos en líneas más cortas que quepan
                IEnumerable<string> lineasFragmentadas = DividirTextoEnLineasCortas(graficosPdf, lineaTexto, fuenteArial, anchoDisponible);
                
                foreach (string lineaAjustada in lineasFragmentadas)
                {
                    // Si el texto llega al final de la página (margen inferior), creamos una página nueva
                    if (posicionY + altoLinea > paginaPdf.Height - margenPagina)
                    {
                        graficosPdf.Dispose();
                        paginaPdf = documentoPdf.AddPage();
                        graficosPdf = XGraphics.FromPdfPage(paginaPdf);
                        posicionY = margenPagina;
                    }
                    
                    // Dibujamos el texto actual en las coordenadas especificadas
                    graficosPdf.DrawString(lineaAjustada, fuenteArial, XBrushes.Black, new XRect(posicionX, posicionY, anchoDisponible, altoLinea), XStringFormats.TopLeft);
                    posicionY += altoLinea;
                }
            }
            graficosPdf.Dispose();
        }

        // Guardamos y finalizamos la escritura del archivo PDF
        documentoPdf.Save(rutaArchivoDestino);
    }

    /// <summary>
    /// Divide un texto largo en palabras y genera sub-líneas que no excedan el ancho máximo especificado.
    /// </summary>
    private static IEnumerable<string> DividirTextoEnLineasCortas(XGraphics graficosPdf, string textoCompleto, XFont fuenteArial, double anchoMaximo)
    {
        if (string.IsNullOrWhiteSpace(textoCompleto))
        {
            yield return textoCompleto;
            yield break;
        }

        // Separa el texto en palabras individuales
        string[] palabras = textoCompleto.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (palabras.Length == 0)
        {
            yield return textoCompleto;
            yield break;
        }

        string lineaActualAcumulada = "";

        // Evaluamos palabra por palabra si sobrepasa el ancho máximo permitido
        foreach (string palabra in palabras)
        {
            string lineaPrueba = string.IsNullOrEmpty(lineaActualAcumulada) ? palabra : lineaActualAcumulada + " " + palabra;
            XSize tamañoTexto = graficosPdf.MeasureString(lineaPrueba, fuenteArial);

            if (tamañoTexto.Width > anchoMaximo && !string.IsNullOrEmpty(lineaActualAcumulada))
            {
                // Si la línea de prueba se pasa del límite, enviamos la línea acumulada anterior y empezamos una nueva
                yield return lineaActualAcumulada;
                lineaActualAcumulada = palabra;
            }
            else
            {
                // Si cabe, seguimos agregando palabras a la línea actual
                lineaActualAcumulada = lineaPrueba;
            }
        }

        if (!string.IsNullOrEmpty(lineaActualAcumulada))
        {
            yield return lineaActualAcumulada;
        }
    }

    /// <summary>
    /// Convierte el archivo original de texto a una presentación de diapositivas de Microsoft PowerPoint (.pptx).
    /// </summary>
    private static void ConvertirAPptx(string rutaOrigen, string rutaArchivoDestino, bool esArchivoImagen)
    {
        // Inicializamos la estructura de la presentación PowerPoint usando DocumentFormat.OpenXml
        using (PresentationDocument presentacionPowerPoint = PresentationDocument.Create(rutaArchivoDestino, PresentationDocumentType.Presentation))
        {
            PresentationPart partePresentacion = presentacionPowerPoint.AddPresentationPart();
            partePresentacion.Presentation = new Presentation();

            SlideMasterPart parteMaestraDiapositiva = partePresentacion.AddNewPart<SlideMasterPart>("rId1");
            parteMaestraDiapositiva.SlideMaster = new SlideMaster(new CommonSlideData(new ShapeTree()));

            SlideLayoutPart parteDiseñoDiapositiva = parteMaestraDiapositiva.AddNewPart<SlideLayoutPart>("rId2");
            parteDiseñoDiapositiva.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));
            
            ThemePart parteTemaPresentacion = parteMaestraDiapositiva.AddNewPart<ThemePart>("rId3");
            parteTemaPresentacion.Theme = new D.Theme() { Name = "Office Theme" };

            SlideMasterIdList listaDiapositivasMaestras = new SlideMasterIdList(new SlideMasterId() { Id = (UInt32Value)2147483648U, RelationshipId = "rId1" });
            SlideIdList listaIdDiapositivas = new SlideIdList();
            
            partePresentacion.Presentation.Append(listaDiapositivasMaestras, listaIdDiapositivas);

            if (!esArchivoImagen)
            {
                // Leemos las líneas y creamos diapositivas. Cada una albergará unas 22 líneas de texto aprox.
                int contadorLineasDiapositiva = 0;
                string textoAcumuladoDiapositiva = "";
                uint indiceIdDiapositiva = 256;

                foreach (string lineaTexto in ExtraerLineas(rutaOrigen))
                {
                    textoAcumuladoDiapositiva += lineaTexto + "\n";
                    contadorLineasDiapositiva++;

                    if (contadorLineasDiapositiva >= 22)
                    {
                        AgregarDiapositivaAPresentacion(partePresentacion, listaIdDiapositivas, indiceIdDiapositiva, textoAcumuladoDiapositiva, parteDiseñoDiapositiva);
                        indiceIdDiapositiva++;
                        contadorLineasDiapositiva = 0;
                        textoAcumuladoDiapositiva = "";
                    }
                }

                // Generar una última diapositiva si quedó texto remanente
                if (!string.IsNullOrEmpty(textoAcumuladoDiapositiva))
                {
                    AgregarDiapositivaAPresentacion(partePresentacion, listaIdDiapositivas, indiceIdDiapositiva, textoAcumuladoDiapositiva, parteDiseñoDiapositiva);
                }
            }
            else
            {
                // Mensaje informativo por defecto si intentan convertir una imagen a PowerPoint (no implementado en OpenXML)
                AgregarDiapositivaAPresentacion(partePresentacion, listaIdDiapositivas, 256, "Imagen (soporte de imagen en PPTX no implementado completamente)", parteDiseñoDiapositiva);
            }
            
            partePresentacion.Presentation.Save();
        }
    }

    /// <summary>
    /// Agrega una diapositiva física a la estructura y le introduce un contenedor de caja de texto.
    /// </summary>
    private static void AgregarDiapositivaAPresentacion(PresentationPart partePresentacion, SlideIdList listaIdDiapositivas, uint indiceIdDiapositiva, string textoDiapositiva, SlideLayoutPart parteDiseñoDiapositiva)
    {
        SlidePart parteDiapositiva = partePresentacion.AddNewPart<SlidePart>($"rId{indiceIdDiapositiva + 100}");
        parteDiapositiva.Slide = new Slide(new CommonSlideData(new ShapeTree()));

        ShapeTree arbolFormas = parteDiapositiva.Slide.CommonSlideData!.ShapeTree!;
        
        // Creamos la forma de la caja de texto
        Shape cajaDeTexto = new Shape();
        cajaDeTexto.NonVisualShapeProperties = new NonVisualShapeProperties(
            new NonVisualDrawingProperties() { Id = 2, Name = "TextBox" },
            new NonVisualShapeDrawingProperties(),
            new ApplicationNonVisualDrawingProperties());
        
        // Configuramos la posición física y dimensiones dentro de la diapositiva
        cajaDeTexto.ShapeProperties = new ShapeProperties(
            new D.Transform2D(
                new D.Offset() { X = 500000L, Y = 500000L },
                new D.Extents() { Cx = 8000000L, Cy = 6000000L }),
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle });
        
        // Asignamos el cuerpo del texto a la forma geométrica.
        // Sanitizamos el texto para eliminar caracteres de control inválidos en XML (ej. 0x17)
        // que pueden provenir de archivos PPTX creados con software externo.
        cajaDeTexto.TextBody = new TextBody(
            new D.BodyProperties(),
            new D.ListStyle(),
            new D.Paragraph(new D.Run(new D.Text(SanitizarTextoXml(textoDiapositiva)))));

        arbolFormas.AppendChild(cajaDeTexto);
        parteDiapositiva.AddPart(parteDiseñoDiapositiva);

        // Añadimos el ID a la lista general de diapositivas
        listaIdDiapositivas.Append(new SlideId() { Id = indiceIdDiapositiva, RelationshipId = partePresentacion.GetIdOfPart(parteDiapositiva) });
    }

    /// <summary>
    /// Elimina caracteres de control que son inválidos en XML (0x00–0x08, 0x0B, 0x0C, 0x0E–0x1F).
    /// Los caracteres de tabulación (0x09), salto de línea (0x0A) y retorno de carro (0x0D) son válidos en XML y se conservan.
    /// Esto previene System.ArgumentException al escribir texto de documentos externos a formatos basados en XML.
    /// </summary>
    private static string SanitizarTextoXml(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (char c in texto)
        {
            // Caracteres XML válidos según la especificación: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
            if (c == 0x09 || c == 0x0A || c == 0x0D || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD))
                sb.Append(c);
            // Los demás (0x00-0x08, 0x0B, 0x0C, 0x0E-0x1F y surrogate pairs) se descartan silenciosamente
        }
        return sb.ToString();
    }

    /// <summary>
    /// Lee secuencialmente las líneas de texto del archivo físico, detectando el formato del archivo de entrada (.docx, .xlsx, .pptx o texto plano).
    /// </summary>
    /// <param name="rutaArchivo">Ruta completa del archivo a leer.</param>
    /// <returns>Flujo IEnumerable que retorna las líneas una a una.</returns>
    private static IEnumerable<string> ExtraerLineas(string rutaArchivo)
    {
        string extension = Path.GetExtension(rutaArchivo).ToLower();
        
        if (extension == ".docx")
        {
            // Extraer líneas desde un archivo de Word
            using (DocX documentoWord = DocX.Load(rutaArchivo))
            {
                // Sanitizamos el texto del documento Word para eliminar caracteres de control inválidos en XML
                // que pueden haberse incrustado por otros editores o macros de Office.
                string[] lineasWord = SanitizarTextoXml(documentoWord.Text).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string linea in lineasWord)
                    yield return linea;
            }
        }
        else if (extension == ".xlsx")
        {
            // Extraer líneas desde una hoja de cálculo Excel
            using (XLWorkbook libroExcel = new XLWorkbook(rutaArchivo))
            {
                IXLWorksheet hojaExcel = libroExcel.Worksheet(1);
                IXLCell ultimaCeldaUsada = hojaExcel.LastCellUsed();
                if (ultimaCeldaUsada != null)
                {
                    // Recorremos las celdas fila por fila para unirlas con tabuladores
                    for (int fila = 1; fila <= ultimaCeldaUsada.Address.RowNumber; fila++)
                    {
                        string textoFila = "";
                        for (int columna = 1; columna <= ultimaCeldaUsada.Address.ColumnNumber; columna++)
                            textoFila += hojaExcel.Cell(fila, columna).GetString() + "\t";
                        
                        // Las celdas pueden contener caracteres de control si fueron importadas de fuentes externas
                        yield return SanitizarTextoXml(textoFila.TrimEnd('\t'));
                    }
                }
            }
        }
        else if (extension == ".pptx")
        {
            // Extraer texto desde una presentación PowerPoint diapositiva por diapositiva.
            // IMPORTANTE: el texto de las formas de una diapositiva vive en <p:txBody>
            // (Presentation.TextBody), NO en <a:txBody> (Drawing.TextBody). Buscar el tipo
            // incorrecto retorna 0 resultados → el PDF resultante queda en blanco.
            using (PresentationDocument presentacionPowerPoint = PresentationDocument.Open(rutaArchivo, false))
            {
                if (presentacionPowerPoint.PresentationPart?.Presentation?.SlideIdList != null)
                {
                    foreach (SlideId idDiapositiva in presentacionPowerPoint.PresentationPart.Presentation.SlideIdList.Elements<SlideId>())
                    {
                        SlidePart parteDiapositiva = (SlidePart)presentacionPowerPoint.PresentationPart.GetPartById(idDiapositiva.RelationshipId!);
                        if (parteDiapositiva.Slide == null) continue;

                        // Usamos Presentation.TextBody (p:txBody) que es donde PowerPoint almacena
                        // el texto de cada forma. Iteramos párrafo a párrafo (a:p → Drawing.Paragraph)
                        // concatenando los runs (a:r → Drawing.Run) para preservar los saltos de línea.
                        foreach (TextBody cuerpoTexto in parteDiapositiva.Slide.Descendants<TextBody>())
                        {
                            foreach (D.Paragraph parrafo in cuerpoTexto.Descendants<D.Paragraph>())
                            {
                                // Concatenar todos los runs del párrafo en una sola línea
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
        else
        {
            // Lectura de flujo de texto plano (UTF-8, Csv, Txt, binarios, etc.).
            // IMPORTANTE: SanitizarTextoXml es indispensable aquí: archivos binarios o con codificación
            // inusual pueden producir null bytes (0x00) y otros caracteres de control que son inválidos
            // en XML. Sin sanitización, DocX.Save() y los writers de OpenXML crashean con ArgumentException.
            using (FileStream flujoArchivo = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader lectorFlujo = new StreamReader(flujoArchivo))
            {
                string? lineaTexto;
                while ((lineaTexto = lectorFlujo.ReadLine()) != null)
                    yield return SanitizarTextoXml(lineaTexto);
            }
        }
    }
}
