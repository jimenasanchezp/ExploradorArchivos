using System;
using System.IO;
using System.Linq;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Fábrica encargada de resolver y retornar la estrategia de conversión de archivos correspondiente.
    /// </summary>
    public static class FileConverterFactory
    {
        /// <summary>
        /// Retorna la mejor estrategia de conversión para el formato y tipo de archivo dados.
        /// Prioriza LibreOffice si está instalado y se convierte un documento de Office a PDF.
        /// </summary>
        public static IFileConverter GetConverter(string sourcePath, string targetExtension, bool esImagen)
        {
            string? rutaSOffice = BuscarSOffice();
            string sourceExtension = Path.GetExtension(sourcePath).ToLower();
            string[] extensionesDocumento = { ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt" };

            // Usamos LibreOffice únicamente si se exporta un documento de Office existente a formato PDF
            if (rutaSOffice != null && !esImagen && targetExtension.ToLower() == ".pdf" && extensionesDocumento.Contains(sourceExtension))
            {
                return new LibreOfficeFileConverter(rutaSOffice);
            }

            switch (targetExtension.ToLower())
            {
                case ".docx":
                    return new DocxFileConverter();
                case ".xlsx":
                    return new XlsxFileConverter();
                case ".pptx":
                    return new PptxFileConverter();
                case ".pdf":
                    return new PdfFileConverter();
                default:
                    throw new NotSupportedException($"El formato de destino '{targetExtension}' no está soportado.");
            }
        }

        /// <summary>
        /// Busca el ejecutable de LibreOffice en las ubicaciones estándar del sistema.
        /// </summary>
        private static string? BuscarSOffice()
        {
            string[] rutasCandidatas =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),      @"LibreOffice\program\soffice.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),   @"LibreOffice\program\soffice.exe"),
                @"C:\LibreOffice\program\soffice.exe",
                @"D:\LibreOffice\program\soffice.exe",
            };
            return rutasCandidatas.FirstOrDefault(File.Exists);
        }
    }
}
