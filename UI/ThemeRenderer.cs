using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

public static class ThemeRenderer
{
    // === Kawaii 95 Palette ===
    public static readonly Color MainBg = ColorTranslator.FromHtml("#FFF0FA");      // Blanco Crema
    public static readonly Color SecondaryBg = ColorTranslator.FromHtml("#D2BEF0"); // Púrpura Ventana
    public static readonly Color Accent = ColorTranslator.FromHtml("#FFB4DC");      // Rosa Suave
    public static readonly Color Hover = ColorTranslator.FromHtml("#FFDBF0");       // Rosa Muy Claro
    public static readonly Color MainText = ColorTranslator.FromHtml("#2D2D2D");
    public static readonly Color SecondaryText = ColorTranslator.FromHtml("#5D5D5D");
    
    public static readonly Color AddressYellow = ColorTranslator.FromHtml("#FFFCE6");
    public static readonly Color Lila = ColorTranslator.FromHtml("#D6C1F0");
    public static readonly Color AzulCielo = ColorTranslator.FromHtml("#CDE7F0");
    public static readonly Color VerdeMenta = ColorTranslator.FromHtml("#CFF5E7");

    // Compatibilidad con RetroDesignHelper
    public static readonly Color FolderMusicBg = AzulCielo;
    public static readonly Color FolderDocsBg = VerdeMenta;
    public static readonly Color FolderPicsBg = Accent;
    public static readonly Color FolderDownloadsBg = Hover;

    /// <summary>
    /// Dibuja un borde retro 3D (estilo Win95).
    /// </summary>
    public static void DrawRetroBorder(Graphics g, Rectangle bounds, bool raised)
    {
        Color light = Color.White;
        Color dark = Color.Gray;
        Color shadow = Color.Black;

        using Pen penLight = new Pen(raised ? light : dark, 1);
        using Pen penDark = new Pen(raised ? dark : light, 1);

        // Borde superior e izquierdo
        g.DrawLine(penLight, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        g.DrawLine(penLight, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);

        // Borde inferior y derecho
        g.DrawLine(penDark, bounds.Left + 1, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(penDark, bounds.Right - 1, bounds.Top + 1, bounds.Right - 1, bounds.Bottom - 1);
    }

    public static void DrawListViewColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        // Fondo morado pastel
        using SolidBrush bgBrush = new SolidBrush(Lila);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        // Borde 3D levantado
        DrawRetroBorder(e.Graphics, e.Bounds, true);

        // Texto centrado con margen
        using SolidBrush textBrush = new SolidBrush(MainText);
        StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
        Rectangle textRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
        
        using Font headerFont = new Font("Segoe UI", 9, FontStyle.Bold);
        e.Graphics.DrawString(e.Header?.Text ?? string.Empty, headerFont, textBrush, textRect, format);
    }

    public static void DrawListViewItem(object sender, DrawListViewItemEventArgs e) { } // Delegado a SubItem

    public static void DrawListViewSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        bool isSelected = e.Item?.Selected ?? false;
        Color backColor = isSelected ? Accent : (e.Item?.Index % 2 == 0 ? MainBg : Color.White);
        Color foreColor = isSelected ? Color.Black : MainText;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        if (isSelected && e.ColumnIndex == 0)
        {
            using Pen focusPen = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawRectangle(focusPen, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 3, e.Bounds.Height - 3);
        }

        StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        if (e.ColumnIndex == 0)
        {
            string icono = "📄"; 
            string tipo = e.Item?.SubItems[1].Text ?? string.Empty;
            string extension = Path.GetExtension(e.Item?.Tag?.ToString() ?? "").ToLower();

            bool esComprimido = extension == ".zip" || extension == ".7z" || extension == ".rar" || extension == ".tar";

            if (tipo == "Carpeta") icono = "📁";
            else if (tipo.Contains("png") || tipo.Contains("jpg")) icono = "🖼️";
            else if (tipo.Contains("mp3")) icono = "🎵";
            else if (tipo.Contains("mp4")) icono = "🎬";
            else if (tipo.Contains("txt") || tipo.Contains("cs") || tipo.Contains("md")) icono = "📝";
            else if (esComprimido) icono = "📦";
            else if (tipo.Contains("pdf")) icono = "📕";
            else if (tipo.Contains("xlsx") || tipo.Contains("csv")) icono = "📊";

            // Tinte de fondo especial para comprimidos (Verde Menta Suave)
            if (esComprimido && !isSelected)
            {
                using SolidBrush packBrush = new SolidBrush(Color.FromArgb(200, VerdeMenta));
                e.Graphics.FillRectangle(packBrush, e.Bounds);
            }

            // Dibujar el icono con fuente correcta
            using Font iconFont = new Font("Segoe UI Emoji", 11);
            e.Graphics.DrawString(icono, iconFont, new SolidBrush(foreColor), e.Bounds.X + 6, e.Bounds.Y + 4);

            // IMPORTANTE: Offset aumentado a 45 para evitar solapamiento
            RectangleF textRect = new RectangleF(e.Bounds.X + 45, e.Bounds.Y, e.Bounds.Width - 45, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? string.Empty, e.Item?.ListView?.Font ?? Control.DefaultFont, new SolidBrush(foreColor), textRect, format);
        }
        else
        {
            RectangleF textRect = new RectangleF(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? string.Empty, e.Item?.ListView?.Font ?? Control.DefaultFont, new SolidBrush(foreColor), textRect, format);
        }
    }

    public static void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null) return;
        bool isSelected = (e.State & TreeNodeStates.Selected) != 0;
        
        Color backColor = isSelected ? Accent : SecondaryBg;
        Color foreColor = isSelected ? Color.Black : MainText;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        if (isSelected)
        {
            using Pen focusPen = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawRectangle(focusPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }

        string emoji = e.Node.Tag?.ToString() == "Inicio" ? "🏠" : 
                       (e.Node.Tag?.ToString() == "EsteEquipo" ? "💻" : "📂");
        
        if (e.Node.Text.Contains("Música")) emoji = "🎵";
        else if (e.Node.Text.Contains("Imágenes")) emoji = "🖼️";
        else if (e.Node.Text.Contains("Papelera") || e.Node.Text.Contains("Trash")) emoji = "🗑️";

        // Usar Segoe UI Emoji para evitar los cuadritos vacíos
        using Font iconFont = new Font("Segoe UI Emoji", 10);
        using Font textFont = new Font("Segoe UI", 9);
        
        e.Graphics.DrawString(emoji, iconFont, new SolidBrush(foreColor), e.Bounds.X + 2, e.Bounds.Y + 4);
        e.Graphics.DrawString(e.Node.Text, textFont, new SolidBrush(foreColor), e.Bounds.X + 25, e.Bounds.Y + 5);
    }
}
