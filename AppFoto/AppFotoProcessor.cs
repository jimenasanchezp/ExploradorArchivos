using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ExploradorArchivos.AppFoto;

public static class AppFotoProcessor
{
    public static Bitmap AplicarFiltro(Image original, string filtro)
    {
        Bitmap bmp = new Bitmap(original.Width, original.Height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            ColorMatrix matrix = filtro switch
            {
                "BN" => GetGrayscaleMatrix(),
                "Sepia" => GetSepiaMatrix(),
                "Kawaii" => GetKawaiiMatrix(),
                _ => new ColorMatrix() // Identidad
            };

            using ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);

            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
        }
        return bmp;
    }

    public static void Rotar(Image img, RotateFlipType tipo)
    {
        img.RotateFlip(tipo);
    }

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

    private static ColorMatrix GetKawaiiMatrix()
    {
        // Tinte rosado pastel + Brillo
        return new ColorMatrix(new float[][]
        {
            new float[] {1.2f, 0, 0, 0, 0},    // R (Aumentado)
            new float[] {0, 1.0f, 0, 0, 0},    // G
            new float[] {0, 0, 1.1f, 0, 0},    // B
            new float[] {0, 0, 0, 1, 0},       // A
            new float[] {0.1f, 0.05f, 0.1f, 0, 1} // Offset para brillo rosado
        });
    }
}
