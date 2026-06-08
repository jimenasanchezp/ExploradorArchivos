using System;
using System.IO;
using System.Threading.Tasks;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Estrategia de conversión que genera un documento de Microsoft Word (.docx).
    /// Xceed no tiene API async nativa, por lo que el trabajo de CPU se encapsula
    /// en <c>Task.Run</c> para liberar el hilo de la UI durante la conversión.
    /// </summary>
    public class DocxFileConverter : IFileConverter
    {
        public Task ConvertirAsync(string rutaOrigen, string rutaDestino, bool esImagen)
            => Task.Run(() => ConvertirInterno(rutaOrigen, rutaDestino, esImagen));

        private static void ConvertirInterno(string rutaOrigen, string rutaDestino, bool esImagen)
        {
            using (DocX documentoWord = DocX.Create(rutaDestino))
            {
                if (esImagen)
                {
                    Xceed.Document.NET.Image imagenDocx = documentoWord.AddImage(rutaOrigen);
                    Xceed.Document.NET.Picture contenedorImagen = imagenDocx.CreatePicture();
                    Paragraph parrafoWord = documentoWord.InsertParagraph();
                    parrafoWord.AppendPicture(contenedorImagen);
                }
                else
                {
                    Paragraph parrafoWord = documentoWord.InsertParagraph();
                    int lineasProcesadas = 0;
                    int limiteMaximoLineas = 50000;

                    foreach (string lineaTexto in FileConverterService.ExtraerLineas(rutaOrigen))
                    {
                        if (lineasProcesadas > limiteMaximoLineas)
                        {
                            documentoWord.InsertParagraph("\n[... CONTENIDO TRUNCADO DEBIDO A SU GRAN TAMAÑO ...]");
                            break;
                        }

                        parrafoWord.Append(lineaTexto).AppendLine();
                        lineasProcesadas++;

                        if (lineasProcesadas % 100 == 0)
                        {
                            parrafoWord = documentoWord.InsertParagraph();
                        }
                    }
                }
                documentoWord.Save();
            }
        }
    }
}
