using System;
using System.IO;
using ClosedXML.Excel;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Estrategia de conversión que genera un libro de cálculo de Microsoft Excel (.xlsx).
    /// </summary>
    public class XlsxFileConverter : IFileConverter
    {
        public void Convertir(string rutaOrigen, string rutaDestino, bool esImagen)
        {
            using (XLWorkbook libroExcel = new XLWorkbook())
            {
                if (esImagen)
                {
                    IXLWorksheet hojaExcel = libroExcel.Worksheets.Add("Hoja de Imagen");
                    hojaExcel.AddPicture(rutaOrigen).MoveTo(hojaExcel.Cell(1, 1));
                }
                else
                {
                    int indiceHoja = 1;
                    int filaActualExcel = 1;
                    IXLWorksheet hojaExcel = libroExcel.Worksheets.Add($"Hoja {indiceHoja}");

                    string extensionOrigen = Path.GetExtension(rutaOrigen).ToLower();
                    bool esArchivoDelimitado = extensionOrigen == ".csv" || extensionOrigen == ".txt";

                    foreach (string lineaTexto in FileConverterService.ExtraerLineas(rutaOrigen))
                    {
                        if (filaActualExcel > 1000000)
                        {
                            indiceHoja++;
                            hojaExcel = libroExcel.Worksheets.Add($"Hoja {indiceHoja}");
                            filaActualExcel = 1;
                        }

                        if (esArchivoDelimitado)
                        {
                            char caracterDelimitador = lineaTexto.Contains('\t') ? '\t' : ',';
                            string[] columnasTexto = lineaTexto.Split(caracterDelimitador);

                            for (int indiceColumna = 0; indiceColumna < columnasTexto.Length; indiceColumna++)
                            {
                                hojaExcel.Cell(filaActualExcel, indiceColumna + 1).Value = columnasTexto[indiceColumna].Trim();
                            }
                        }
                        else
                        {
                            hojaExcel.Cell(filaActualExcel, 1).Value = lineaTexto;
                        }

                        filaActualExcel++;
                    }
                }
                libroExcel.SaveAs(rutaDestino);
            }
        }
    }
}
