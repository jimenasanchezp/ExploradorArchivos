using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

/// <summary>
/// Helper para aplicar estilos retro-Y2K pastel a controles.
/// Proporciona métodos para bordes, colores y efectos visuales.
/// </summary>
public static class RetroDesignHelper
{
    /// <summary>
    /// Aplica un borde retro 3D a un control.
    /// </summary>
    public static void AplicarBordeRetro(Control control, Color colorBorde)
    {
        control.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Borde exterior (más oscuro)
            using (Pen penDark = new Pen(Color.Gray, 2))
            {
                e.Graphics.DrawRectangle(penDark, 0, 0, control.Width - 2, control.Height - 2);
            }
            
            // Borde interior (más claro)
            using (Pen penLight = new Pen(Color.White, 1))
            {
                e.Graphics.DrawRectangle(penLight, 1, 1, control.Width - 3, control.Height - 3);
            }
        };
    }

    /// <summary>
    /// Aplica un borde redondeado a un botón.
    /// </summary>
    public static void AplicarBordeRedondeado(Button button, int radio = 6, Color? borderColor = null)
    {
        Color borde = borderColor ?? ThemeRenderer.Accent;
        
        button.Paint += (s, e) =>
        {
            e.Graphics.Clear(button.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = CrearRectanguloRedondeado(
                new Rectangle(0, 0, button.Width - 1, button.Height - 1), radio))
            {
                using (Pen pen = new Pen(borde, 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
                }

                // Dibujar texto
                TextFormatFlags flags = TextFormatFlags.HorizontalCenter | 
                                       TextFormatFlags.VerticalCenter;
                TextRenderer.DrawText(e.Graphics, button.Text, button.Font, 
                    button.ClientRectangle, button.ForeColor, flags);
            }
        };
    }

    /// <summary>
    /// Aplica un efecto hover pastel a un control.
    /// </summary>
    public static void AplicarHoverPastel(Control control, Color colorNormal, Color colorHover)
    {
        control.MouseEnter += (s, e) => control.BackColor = colorHover;
        control.MouseLeave += (s, e) => control.BackColor = colorNormal;
    }

    /// <summary>
    /// Crea un rectángulo con esquinas redondeadas.
    /// </summary>
    public static GraphicsPath CrearRectanguloRedondeado(Rectangle rect, int radio)
    {
        GraphicsPath path = new GraphicsPath();
        int diametro = radio * 2;

        // Esquina superior izquierda
        path.AddArc(rect.X, rect.Y, diametro, diametro, 180, 90);
        
        // Esquina superior derecha
        path.AddArc(rect.Right - diametro, rect.Y, diametro, diametro, 270, 90);
        
        // Esquina inferior derecha
        path.AddArc(rect.Right - diametro, rect.Bottom - diametro, diametro, diametro, 0, 90);
        
        // Esquina inferior izquierda
        path.AddArc(rect.X, rect.Bottom - diametro, diametro, diametro, 90, 90);
        
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Personaliza un ListViewItem con color de fondo pastel.
    /// </summary>
    public static void PersonalizarItemLista(ListViewItem item, Color colorFondo)
    {
        item.BackColor = colorFondo;
    }

    /// <summary>
    /// Aplica gradiente pastel a un panel.
    /// </summary>
    public static void AplicarGradientePastel(Panel panel, Color colorInicio, Color colorFin)
    {
        panel.Paint += (s, e) =>
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Point(0, 0), 
                new Point(0, panel.Height),
                colorInicio, 
                colorFin))
            {
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            }
        };
    }

    /// <summary>
    /// Personaliza una carpeta con color pastel específico.
    /// </summary>
    public static Color ObtenerColorCarpeta(string nombreCarpeta)
    {
        string nombre = nombreCarpeta.ToLower();

        return nombre switch
        {
            var x when x.Contains("música") || x.Contains("music") => 
                ThemeRenderer.FolderMusicBg,
            
            var x when x.Contains("documentos") || x.Contains("documents") => 
                ThemeRenderer.FolderDocsBg,
            
            var x when x.Contains("imágenes") || x.Contains("pictures") => 
                ThemeRenderer.FolderPicsBg,
            
            var x when x.Contains("descargas") || x.Contains("downloads") => 
                ThemeRenderer.FolderDownloadsBg,
            
            _ => ThemeRenderer.SecondaryBg // Color por defecto
        };
    }

    /// <summary>
    /// Agrega emoji automático según el tipo de carpeta.
    /// </summary>
    public static string AgregaEmojiCarpeta(string nombreCarpeta)
    {
        string nombre = nombreCarpeta.ToLower();

        // No agregar si ya tiene emoji
        if (nombreCarpeta.Contains("🎵") || nombreCarpeta.Contains("📄") || 
            nombreCarpeta.Contains("🖼️") || nombreCarpeta.Contains("📥"))
            return nombreCarpeta;

        return nombre switch
        {
            var x when x.Contains("música") || x.Contains("music") => "🎵 " + nombreCarpeta,
            var x when x.Contains("documentos") || x.Contains("documents") => "📄 " + nombreCarpeta,
            var x when x.Contains("imágenes") || x.Contains("pictures") => "🖼️ " + nombreCarpeta,
            var x when x.Contains("descargas") || x.Contains("downloads") => "📥 " + nombreCarpeta,
            var x when x.Contains("vídeos") || x.Contains("videos") => "🎬 " + nombreCarpeta,
            var x when x.Contains("escritorio") || x.Contains("desktop") => "🖥️ " + nombreCarpeta,
            _ => "📁 " + nombreCarpeta
        };
    }
}
