using System;
using System.Collections.Generic;
using System.IO;
using PdfVisual = PdfSharpCore.Pdf;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Estrategia de conversión que genera un documento de formato portátil (.pdf).
    /// </summary>
    public class PdfFileConverter : IFileConverter
    {
        public void Convertir(string rutaOrigen, string rutaDestino, bool esImagen)
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new PdfSharpCore.Utils.FontResolver();

            PdfDocument documentoPdf = new PdfDocument();

            if (esImagen)
            {
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
                XFont fuenteArial = new XFont("Arial", 11, XFontStyle.Regular);
                PdfPage paginaPdf = documentoPdf.AddPage();
                XGraphics graficosPdf = XGraphics.FromPdfPage(paginaPdf);

                double margenPagina = 40;
                double posicionY = margenPagina;
                double posicionX = margenPagina;
                double altoLinea = fuenteArial.GetHeight();
                double anchoDisponible = paginaPdf.Width - (margenPagina * 2);

                foreach (string lineaTexto in FileConverterService.ExtraerLineas(rutaOrigen))
                {
                    IEnumerable<string> lineasFragmentadas = DividirTextoEnLineasCortas(graficosPdf, lineaTexto, fuenteArial, anchoDisponible);

                    foreach (string lineaAjustada in lineasFragmentadas)
                    {
                        if (posicionY + altoLinea > paginaPdf.Height - margenPagina)
                        {
                            graficosPdf.Dispose();
                            paginaPdf = documentoPdf.AddPage();
                            graficosPdf = XGraphics.FromPdfPage(paginaPdf);
                            posicionY = margenPagina;
                        }

                        graficosPdf.DrawString(lineaAjustada, fuenteArial, XBrushes.Black, new XRect(posicionX, posicionY, anchoDisponible, altoLinea), XStringFormats.TopLeft);
                        posicionY += altoLinea;
                    }
                }
                graficosPdf.Dispose();
            }

            documentoPdf.Save(rutaDestino);
        }

        private static IEnumerable<string> DividirTextoEnLineasCortas(XGraphics graficosPdf, string textoCompleto, XFont fuenteArial, double anchoMaximo)
        {
            if (string.IsNullOrWhiteSpace(textoCompleto))
            {
                yield return textoCompleto;
                yield break;
            }

            string[] palabras = textoCompleto.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (palabras.Length == 0)
            {
                yield return textoCompleto;
                yield break;
            }

            string lineaActualAcumulada = "";

            foreach (string palabra in palabras)
            {
                string lineaPrueba = string.IsNullOrEmpty(lineaActualAcumulada) ? palabra : lineaActualAcumulada + " " + palabra;
                XSize tamañoTexto = graficosPdf.MeasureString(lineaPrueba, fuenteArial);

                if (tamañoTexto.Width > anchoMaximo && !string.IsNullOrEmpty(lineaActualAcumulada))
                {
                    yield return lineaActualAcumulada;
                    lineaActualAcumulada = palabra;
                }
                else
                {
                    lineaActualAcumulada = lineaPrueba;
                }
            }

            if (!string.IsNullOrEmpty(lineaActualAcumulada))
            {
                yield return lineaActualAcumulada;
            }
        }
    }
}
