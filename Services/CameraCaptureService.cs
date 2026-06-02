using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using AForge.Video.DirectShow;
using ExploradorArchivos.AppCamara;
using ExploradorArchivos.AppVideo;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Servicio encargado de la gestión de dispositivos de cámara y operaciones de captura de fotos y grabación de video.
    /// </summary>
    internal static class CameraCaptureService
    {
        /// <summary>
        /// Obtiene la colección de dispositivos de video (cámaras) conectados al sistema.
        /// </summary>
        public static FilterInfoCollection ObtenerDispositivosCamara()
        {
            return new FilterInfoCollection(FilterCategory.VideoInputDevice);
        }

        /// <summary>
        /// Guarda un fotograma estático de la cámara como una fotografía en formato JPEG.
        /// </summary>
        public static string GuardarFoto(Image image, string rutaDestino)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string rutaArchivo = Path.Combine(rutaDestino, $"Foto_{timestamp}.jpg");

            lock (image)
            {
                using (var captura = new Bitmap(image))
                {
                    captura.Save(rutaArchivo, ImageFormat.Jpeg);
                }
            }

            return rutaArchivo;
        }

        /// <summary>
        /// Inicializa una nueva sesión de grabación de video creando y abriendo un AviGrabador.
        /// </summary>
        public static AviGrabador IniciarGrabacion(string rutaDestino, int frameWidth, int frameHeight, out string rutaAviTemporal, out string rutaMp4Final)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            rutaAviTemporal = Path.Combine(rutaDestino, $"Video_{timestamp}_tmp.avi");
            rutaMp4Final = Path.Combine(rutaDestino, $"Video_{timestamp}.mp4");

            var grabador = new AviGrabador();
            grabador.Abrir(rutaAviTemporal, frameWidth, frameHeight, 30);
            return grabador;
        }

        /// <summary>
        /// Escribe un frame de video en el grabador especificado.
        /// </summary>
        public static void EscribirFrame(AviGrabador grabador, Bitmap frame, TimeSpan elapsed)
        {
            if (grabador == null) return;

            lock (grabador)
            {
                grabador.EscribirFrame(frame, elapsed);
            }
        }

        /// <summary>
        /// Cierra y libera los recursos del grabador de video.
        /// </summary>
        public static void DetenerGrabacion(AviGrabador grabador)
        {
            if (grabador != null)
            {
                lock (grabador)
                {
                    try
                    {
                        grabador.Cerrar();
                    }
                    catch
                    {
                        // Ignorar errores al cerrar
                    }
                    grabador.Dispose();
                }
            }
        }

        /// <summary>
        /// Convierte de forma asíncrona un archivo de video AVI temporal a MP4 comprimido usando FFmpeg.
        /// Si la conversión es exitosa, elimina el archivo AVI temporal.
        /// </summary>
        public static async Task<bool> ConvertirAviAMp4Async(string rutaAviTemporal, string rutaMp4Final)
        {
            if (File.Exists(rutaAviTemporal))
            {
                bool convertido = await AppVideoProcessor.ConvertirAviAMp4(rutaAviTemporal, rutaMp4Final);
                if (convertido)
                {
                    try
                    {
                        File.Delete(rutaAviTemporal);
                    }
                    catch
                    {
                        // Ignorar errores al borrar
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
