using System.Drawing;
using System.Drawing.Drawing2D;

public static class ImageHelper
{
    public static Image RedondearImagen(Image imagen, int radio)
    {
        var bitmap = new Bitmap(imagen.Width, imagen.Height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, radio, radio, 180, 90);
        path.AddArc(imagen.Width - radio, 0, radio, radio, 270, 90);
        path.AddArc(imagen.Width - radio, imagen.Height - radio, radio, radio, 0, 90);
        path.AddArc(0, imagen.Height - radio, radio, radio, 90, 90);
        path.CloseFigure();
        g.SetClip(path);
        g.DrawImage(imagen, 0, 0);
        return bitmap;
    }
}
