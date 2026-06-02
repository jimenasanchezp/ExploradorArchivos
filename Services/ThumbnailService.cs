using System;
using System.Drawing;
using System.IO;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Servicio para la carga de imágenes en miniatura y generación de iconos con fallbacks basados en emojis.
    /// </summary>
    public static class ThumbnailService
    {
        /// <summary>
        /// Genera una miniatura a partir de la ruta de un archivo de imagen en disco sin bloquear el archivo original.
        /// </summary>
        public static Bitmap? GenerarMiniatura(string rutaCompleta, int ancho = 96, int alto = 96)
        {
            try
            {
                if (!File.Exists(rutaCompleta)) return null;

                // Leer en memoria para no bloquear el archivo físico original
                byte[] bytes = File.ReadAllBytes(rutaCompleta);
                using (var ms = new MemoryStream(bytes))
                {
                    using (var original = Image.FromStream(ms))
                    {
                        return new Bitmap(original, new Size(ancho, alto));
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Genera un icono de color de fondo específico con un emoji en el centro.
        /// </summary>
        public static Bitmap GenerarIconoBase(string emoji, Color backColor, int ancho = 96, int alto = 96)
        {
            Bitmap bmp = new Bitmap(ancho, alto);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(backColor);
                using (Font font = new Font("Segoe UI Emoji", 40))
                {
                    g.DrawString(emoji, font, Brushes.Black, new PointF(10, 10));
                }
            }
            return bmp;
        }
    }
}
