using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExploradorArchivos.AppCamara;
using ExploradorArchivos.AppVideo;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Servicio encargado de capturar la pantalla del escritorio (screenshot y grabación).
    /// Utiliza <c>Graphics.CopyFromScreen</c> (nativo de .NET, sin librerías externas)
    /// y el <see cref="AviGrabador"/> ya existente en el proyecto para la grabación.
    /// </summary>
    internal static class ScreenCaptureService
    {
        // ─── Carpetas por defecto ────────────────────────────────────────────────
        private static string CarpetaImagenes =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        private static string CarpetaVideos =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        // ════════════════════════════════════════════════════════════════════════
        #region Captura de Pantalla (Screenshot)

        /// <summary>
        /// Captura toda la pantalla principal y la guarda como PNG en Mis Imágenes.
        /// </summary>
        /// <returns>Ruta completa del archivo PNG guardado.</returns>
        public static string CapturarPantallaCompleta()
        {
            Rectangle pantalla = Screen.PrimaryScreen?.Bounds
                ?? new Rectangle(0, 0, 1920, 1080);
            return CapturarRegion(pantalla);
        }

        /// <summary>
        /// Captura únicamente la región rectangular indicada (en coordenadas de pantalla)
        /// y la guarda como PNG en Mis Imágenes.
        /// </summary>
        /// <param name="region">Rectángulo en coordenadas absolutas de pantalla.</param>
        /// <returns>Ruta completa del archivo PNG guardado.</returns>
        public static string CapturarRegion(Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentException("La región seleccionada no tiene dimensiones válidas.");

            using var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using var g   = Graphics.FromImage(bmp);
            g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);

            string timestamp   = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string rutaArchivo = Path.Combine(CarpetaImagenes, $"Captura_{timestamp}.png");
            bmp.Save(rutaArchivo, ImageFormat.Png);
            return rutaArchivo;
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        #region Grabación de Pantalla (Screen Recording)

        /// <summary>
        /// Inicializa una nueva sesión de grabación de pantalla para la región indicada.
        /// Devuelve el <see cref="AviGrabador"/> abierto y las rutas de salida.
        /// </summary>
        /// <param name="region">Área de pantalla a grabar. Si es <c>Rectangle.Empty</c> se usa la pantalla completa.</param>
        /// <param name="rutaAviTemporal">Ruta del archivo AVI temporal generado.</param>
        /// <param name="rutaMp4Final">Ruta del archivo MP4 final tras la conversión.</param>
        /// <returns>Instancia de <see cref="AviGrabador"/> lista para recibir frames.</returns>
        public static AviGrabador IniciarGrabacion(Rectangle region,
            out string rutaAviTemporal,
            out string rutaMp4Final)
        {
            if (region.IsEmpty || region.Width <= 0 || region.Height <= 0)
                region = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            rutaAviTemporal  = Path.Combine(CarpetaVideos, $"Grabacion_{timestamp}_tmp.avi");
            rutaMp4Final     = Path.Combine(CarpetaVideos, $"Grabacion_{timestamp}.mp4");

            var grabador = new AviGrabador();
            grabador.Abrir(rutaAviTemporal, region.Width, region.Height, 15);
            return grabador;
        }

        /// <summary>
        /// Captura un frame de la región indicada de la pantalla y lo retorna como <see cref="Bitmap"/>.
        /// Este método debe llamarse desde un Timer para construir el video frame a frame.
        /// </summary>
        /// <param name="region">Región de pantalla a capturar.</param>
        /// <returns>Bitmap con el contenido actual de la región.</returns>
        public static Bitmap CapturarFrame(Rectangle region)
        {
            var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        /// <summary>
        /// Escribe un frame en el grabador de forma segura (thread-safe).
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
        /// Cierra y libera el grabador de video AVI.
        /// </summary>
        public static void DetenerGrabacion(AviGrabador grabador)
        {
            if (grabador == null) return;
            lock (grabador)
            {
                try   { grabador.Cerrar(); }
                catch { /* ignorar errores al cerrar */ }
                grabador.Dispose();
            }
        }

        /// <summary>
        /// Convierte de forma asíncrona el AVI temporal a MP4 usando FFmpeg.
        /// Si tiene éxito, elimina el AVI temporal.
        /// </summary>
        /// <returns><c>true</c> si la conversión fue exitosa; <c>false</c> si FFmpeg no está disponible.</returns>
        public static async Task<bool> ConvertirAviAMp4Async(string rutaAviTemporal, string rutaMp4Final)
        {
            if (!File.Exists(rutaAviTemporal)) return false;

            bool convertido = await AppVideoProcessor.ConvertirAviAMp4(rutaAviTemporal, rutaMp4Final);
            if (convertido)
            {
                try   { File.Delete(rutaAviTemporal); }
                catch { /* ignorar */ }
                return true;
            }
            return false;
        }

        #endregion
    }
}
