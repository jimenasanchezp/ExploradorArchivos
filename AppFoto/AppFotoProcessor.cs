using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Serialization;
using System.Text;

namespace ExploradorArchivos.AppFoto;

/// <summary>
/// Motor de procesamiento de imágenes GDI+.
/// Proporciona utilidades para aplicar filtros con <c>ColorMatrix</c>, ajustes de brillo/contraste,
/// recortes, dibujo de primitivas y modificación de tags EXIF (incluyendo GPS).
/// </summary>
public static class AppFotoProcessor
{
    /// <summary>
    /// Lee el tag EXIF de orientación (0x0112) y rota la imagen automáticamente
    /// a su orientación visual correcta (RotateFlip), eliminando el tag para no aplicar la rotación doble.
    /// </summary>
    /// <param name="img">Objeto imagen cargado en memoria.</param>
    public static void CorrectOrientation(Image img)
    {
        if (Array.IndexOf(img.PropertyIdList, 0x0112) == -1) 
        {
            return;
        }

        PropertyItem prop = img.GetPropertyItem(0x0112);
        int val = BitConverter.ToUInt16(prop.Value, 0);
        RotateFlipType rot = RotateFlipType.RotateNoneFlipNone;

        // Mapeo del estándar EXIF de orientación a RotateFlipType
        switch (val)
        {
            case 2: rot = RotateFlipType.RotateNoneFlipX; break;
            case 3: rot = RotateFlipType.Rotate180FlipNone; break;
            case 4: rot = RotateFlipType.Rotate180FlipX; break;
            case 5: rot = RotateFlipType.Rotate90FlipX; break;
            case 6: rot = RotateFlipType.Rotate90FlipNone; break;
            case 7: rot = RotateFlipType.Rotate270FlipX; break;
            case 8: rot = RotateFlipType.Rotate270FlipNone; break;
        }

        if (rot != RotateFlipType.RotateNoneFlipNone)
        {
            img.RotateFlip(rot);
            img.RemovePropertyItem(0x0112); // Remover para evitar rotaciones duplicadas al guardar
        }
    }

    /// <summary>
    /// Aplica un filtro de color predefinido sobre la imagen original utilizando transformaciones de matrices de color.
    /// </summary>
    /// <param name="original">Imagen base a transformar.</param>
    /// <param name="filtro">Nombre del filtro (BN, Sepia, Soft).</param>
    /// <returns>Una nueva instancia <see cref="Bitmap"/> con el filtro aplicado.</returns>
    public static Bitmap AplicarFiltro(Image original, string filtro)
    {
        Bitmap bmp = new Bitmap(original.Width, original.Height);

        using (Graphics g = Graphics.FromImage(bmp))
        {
            ColorMatrix matrix = filtro switch
            {
                "BN" => GetGrayscaleMatrix(),
                "Sepia" => GetSepiaMatrix(),
                "Soft" => GetSoftMatrix(),
                _ => new ColorMatrix() // Matriz Identidad
            };

            using ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);

            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
        }

        return bmp;
    }

    /// <summary>
    /// Aplica dinámicamente un ajuste combinado de Brillo, Contraste, Saturación y balances tonales
    /// calculando y aplicando una única <see cref="ColorMatrix"/> para mejor rendimiento.
    /// </summary>
    /// <param name="original">Imagen original.</param>
    /// <param name="brillo">Valor de brillo (-100 a 100).</param>
    /// <param name="contraste">Valor de contraste (0 a 200).</param>
    /// <param name="saturacion">Valor de saturación (0 a 200).</param>
    /// <param name="luces">Ajuste de luces (-100 a 100).</param>
    /// <param name="sombras">Ajuste de sombras (-100 a 100).</param>
    /// <returns>Una nueva instancia <see cref="Bitmap"/> con los ajustes aplicados.</returns>
    public static Bitmap AjustarImagen(Image original, float brillo, float contraste, float saturacion, float luces, float sombras)
    {
        // Conversión de escalas de control a multiplicadores decimales relativos
        float b = brillo / 100f;
        float c = contraste / 100f;
        float s = saturacion / 100f;
        float l = luces / 100f;
        float sh = sombras / 100f;

        float t = (1.0f - c) / 2.0f;
        
        // Coeficientes de luminancia estándar NTSC
        float lumR = 0.3086f;
        float lumG = 0.6094f;
        float lumB = 0.0820f;

        float sr = (1 - s) * lumR;
        float sg = (1 - s) * lumG;
        float sb = (1 - s) * lumB;

        // Construcción de la ColorMatrix unificada
        ColorMatrix matrix = new ColorMatrix(new float[][]
        {
            new float[] {c * (sr + s),  c * sr,          c * sr,          0, 0},
            new float[] {c * sg,          c * (sg + s),  c * sg,          0, 0},
            new float[] {c * sb,          c * sb,          c * (sb + s),  0, 0},
            new float[] {0,               0,               0,               1, 0},
            new float[] {t + b + l + sh,  t + b + l + sh,  t + b + l + sh,  0, 1}
        });

        Bitmap bmp = new Bitmap(original.Width, original.Height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            using ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
        }

        return bmp;
    }

    /// <summary>
    /// Gira o voltea la imagen según el tipo de rotación especificado.
    /// </summary>
    /// <param name="img">Imagen a rotar/voltear en memoria.</param>
    /// <param name="tipo">Especificación de rotación y/o volteo a aplicar.</param>
    public static void Rotar(Image img, RotateFlipType tipo)
    {
        img.RotateFlip(tipo);
    }

    /// <summary>
    /// Crea una nueva imagen extrayendo un área rectangular específica de la imagen original.
    /// </summary>
    /// <param name="original">Imagen original.</param>
    /// <param name="area">Región de recorte deseada.</param>
    /// <returns>Un nuevo <see cref="Bitmap"/> que representa la sección recortada.</returns>
    public static Bitmap Recortar(Image original, Rectangle area)
    {
        Bitmap bmp = new Bitmap(area.Width, area.Height);

        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.DrawImage(original, new Rectangle(0, 0, area.Width, area.Height),
                area, GraphicsUnit.Pixel);
        }

        return bmp;
    }

    /// <summary>
    /// Dibuja una línea suavizada (AntiAlias) entre dos puntos directamente sobre la imagen dada.
    /// </summary>
    /// <param name="img">Imagen sobre la cual dibujar.</param>
    /// <param name="p1">Punto inicial de la línea.</param>
    /// <param name="p2">Punto final de la línea.</param>
    /// <param name="color">Color de la línea.</param>
    /// <param name="grosor">Ancho de la pluma.</param>
    public static void DibujarLinea(Image img, Point p1, Point p2, Color color, float grosor)
    {
        using (Graphics g = Graphics.FromImage(img))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen pen = new Pen(color, grosor) 
            { 
                StartCap = System.Drawing.Drawing2D.LineCap.Round, 
                EndCap = System.Drawing.Drawing2D.LineCap.Round 
            };

            g.DrawLine(pen, p1, p2);
        }
    }

    /// <summary>
    /// Incrusta texto renderizado con alta calidad sobre la imagen en las coordenadas indicadas.
    /// </summary>
    /// <param name="img">Imagen sobre la cual renderizar el texto.</param>
    /// <param name="texto">Texto a incrustar.</param>
    /// <param name="p">Posición de origen (esquina superior izquierda) del texto.</param>
    /// <param name="fuente">Fuente tipográfica a utilizar.</param>
    /// <param name="color">Color del pincel del texto.</param>
    public static void DibujarTexto(Image img, string texto, Point p, Font fuente, Color color)
    {
        using (Graphics g = Graphics.FromImage(img))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using SolidBrush brush = new SolidBrush(color);
            g.DrawString(texto, fuente, brush, p);
        }
    }

    /// <summary>
    /// Sobrescribe el archivo JPEG inyectando los tags EXIF estándar para coordenadas geográficas (GPS).
    /// </summary>
    /// <param name="img">Imagen a guardar.</param>
    /// <param name="ruta">Ruta física de destino (debe ser formato JPEG).</param>
    /// <param name="lat">Latitud en formato decimal.</param>
    /// <param name="lon">Longitud en formato decimal.</param>
    public static void GuardarConGps(Image img, string ruta, double lat, double lon)
    {
        using (var tempImg = (Image)img.Clone())
        {
            // Escribir los tags GPS individuales necesarios
            SetGpsVersionTag(tempImg);
            SetGpsTag(tempImg, 0x0001, lat >= 0 ? "N" : "S");
            SetGpsTag(tempImg, 0x0002, Math.Abs(lat));
            SetGpsTag(tempImg, 0x0003, lon >= 0 ? "E" : "W");
            SetGpsTag(tempImg, 0x0004, Math.Abs(lon));

            // Obtener codificador JPEG específico para no perder los metadatos EXIF
            ImageCodecInfo? jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            if (jpegEncoder != null)
            {
                EncoderParameters ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                tempImg.Save(ruta, jpegEncoder, ep);
            }
            else
            {
                tempImg.Save(ruta, ImageFormat.Jpeg);
            }
        }
    }

    /// <summary>
    /// Obtiene el codificador de imágenes correspondiente al formato dado.
    /// </summary>
    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FormatID == format.Guid) 
            {
                return codec;
            }
        }

        return null;
    }

    /// <summary>
    /// Inyecta un tag GPS de tipo ASCII (Referencia cardinal).
    /// </summary>
    private static void SetGpsTag(Image img, ushort id, string value)
    {
        PropertyItem pi = CreatePropertyItem(img);
        pi.Id = id;
        pi.Type = 2; // ASCII
        pi.Value = Encoding.ASCII.GetBytes(value + "\0");
        pi.Len = pi.Value.Length;

        img.SetPropertyItem(pi);
    }

    /// <summary>
    /// Inyecta un tag GPS de tipo Racional (Valores decimales convertidos a racionales).
    /// </summary>
    private static void SetGpsTag(Image img, ushort id, double value)
    {
        PropertyItem pi = CreatePropertyItem(img);
        pi.Id = id;
        pi.Type = 5; // Rational
        
        // Conversión a representación EXIF GPS: Grados, Minutos y Segundos
        uint degrees = (uint)Math.Floor(value);
        uint minutes = (uint)Math.Floor((value - degrees) * 60);
        double secondsDouble = ((value - degrees) * 60 - minutes) * 60;
        uint secondsNumerator = (uint)Math.Round(secondsDouble * 100);
        uint secondsDenominator = 100;

        // Estructura de 24 bytes para 3 racionales (cada racional = 2 uint32: numerador, denominador)
        byte[] data = new byte[24];
        BitConverter.GetBytes(degrees).CopyTo(data, 0);
        BitConverter.GetBytes(1U).CopyTo(data, 4);
        BitConverter.GetBytes(minutes).CopyTo(data, 8);
        BitConverter.GetBytes(1U).CopyTo(data, 12);
        BitConverter.GetBytes(secondsNumerator).CopyTo(data, 16);
        BitConverter.GetBytes(secondsDenominator).CopyTo(data, 20);

        pi.Value = data;
        pi.Len = data.Length;

        img.SetPropertyItem(pi);
    }

    /// <summary>
    /// Crea un objeto <see cref="PropertyItem"/> utilizando un hack de reflexión o clonado de moldes existentes.
    /// </summary>
    private static PropertyItem CreatePropertyItem(Image img)
    {
        if (img.PropertyIdList.Length > 0)
        {
            return img.GetPropertyItem(img.PropertyIdList[0]);
        }
            
        // PropertyItem no posee un constructor público, se instancia sin inicializar mediante FormatterServices
        return (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
    }

    /// <summary>
    /// Inyecta la versión del tag GPS requerido para la especificación EXIF.
    /// </summary>
    private static void SetGpsVersionTag(Image img)
    {
        PropertyItem pi = CreatePropertyItem(img);
        pi.Id = 0x0000;
        pi.Type = 1; // Byte
        pi.Value = new byte[] { 2, 2, 0, 0 };
        pi.Len = 4;

        img.SetPropertyItem(pi);
    }

    /// <summary>
    /// Retorna la matriz de color para conversión a escala de grises.
    /// </summary>
    private static ColorMatrix GetGrayscaleMatrix()
    {
        return new ColorMatrix(new float[][]
        {
            new float[] {.3f, .3f, .3f, 0, 0},
            new float[] {.59f, .59f, .59f, 0, 0},
            new float[] {.11f, .11f, .11f, 0, 0},
            new float[] {0, 0, 0, 1, 0},
            new float[] {0, 0, 0, 0, 1}
        });
    }

    /// <summary>
    /// Retorna la matriz de color para conversión a Sepia.
    /// </summary>
    private static ColorMatrix GetSepiaMatrix()
    {
        return new ColorMatrix(new float[][]
        {
            new float[] {.393f, .349f, .272f, 0, 0},
            new float[] {.769f, .686f, .534f, 0, 0},
            new float[] {.189f, .168f, .131f, 0, 0},
            new float[] {0, 0, 0, 1, 0},
            new float[] {0, 0, 0, 0, 1}
        });
    }

    /// <summary>
    /// Retorna la matriz de color para el filtro Soft (Pastel).
    /// </summary>
    private static ColorMatrix GetSoftMatrix()
    {
        return new ColorMatrix(new float[][]
        {
            new float[] {1.2f, 0, 0, 0, 0},
            new float[] {0, 1.0f, 0, 0, 0},
            new float[] {0, 0, 1.1f, 0, 0},
            new float[] {0, 0, 0, 1, 0},
            new float[] {0.1f, 0.05f, 0.1f, 0, 1}
        });
    }
}
