using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Estrategia de conversión de alta fidelidad que utiliza LibreOffice Headless en segundo plano.
    /// Este es el único convertidor verdaderamente asíncrono a nivel de I/O, ya que espera la
    /// finalización del proceso externo con <c>WaitForExitAsync</c> sin bloquear ningún hilo.
    /// </summary>
    public class LibreOfficeFileConverter : IFileConverter
    {
        private readonly string _rutaSOffice;

        public LibreOfficeFileConverter(string rutaSOffice)
        {
            _rutaSOffice = rutaSOffice;
        }

        public async Task ConvertirAsync(string rutaOrigen, string rutaDestino, bool esImagen)
        {
            string directorioDestino = Path.GetDirectoryName(rutaDestino)!;
            string formatoDestino = Path.GetExtension(rutaDestino);

            string dirTemporal = Path.Combine(Path.GetTempPath(), $"lo_conv_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dirTemporal);

            try
            {
                string filtro = formatoDestino.ToLower() switch
                {
                    ".pdf"  => "pdf",
                    ".docx" => "docx:MS Word 2007 XML",
                    ".xlsx" => "xlsx:Calc MS Excel 2007 XML",
                    ".pptx" => "pptx:impress_pptx_Export",
                    ".txt"  => "txt:Text",
                    _       => throw new NotSupportedException($"LibreOffice no puede convertir a '{formatoDestino}'.")
                };

                var psi = new ProcessStartInfo
                {
                    FileName               = _rutaSOffice,
                    Arguments              = $"--headless --convert-to {filtro} \"{rutaOrigen}\" --outdir \"{dirTemporal}\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };

                using var proceso = Process.Start(psi)
                    ?? throw new InvalidOperationException("No se pudo iniciar el proceso de LibreOffice.");

                // Espera verdaderamente asíncrona: no bloquea ningún hilo del ThreadPool.
                // Se cancela automáticamente si LibreOffice tarda más de 3 minutos.
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try
                {
                    await proceso.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    proceso.Kill();
                    throw new TimeoutException("LibreOffice tardó demasiado en convertir el archivo.");
                }

                if (proceso.ExitCode != 0)
                {
                    string stderr = await proceso.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"LibreOffice falló (código {proceso.ExitCode}): {stderr}");
                }

                string archivoGenerado = Path.Combine(dirTemporal,
                    Path.GetFileNameWithoutExtension(rutaOrigen) + formatoDestino);

                if (!File.Exists(archivoGenerado))
                    throw new FileNotFoundException("LibreOffice no generó el archivo de salida esperado.", archivoGenerado);

                File.Move(archivoGenerado, rutaDestino);
            }
            finally
            {
                if (Directory.Exists(dirTemporal))
                    Directory.Delete(dirTemporal, recursive: true);
            }
        }
    }
}
