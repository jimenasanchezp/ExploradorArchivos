using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

public static class ThemeRenderer
{
    // === Pink Retro 90s Palette ===
    public static readonly Color MainBg = ColorTranslator.FromHtml("#C0C0C0");      // Classic Win95 Beige/Gray
    public static readonly Color SecondaryBg = ColorTranslator.FromHtml("#FFB6C1"); // Pink Retro
    public static readonly Color Selection = ColorTranslator.FromHtml("#000080");   // Classic Navy Blue
    public static readonly Color SelectionText = Color.White;
    public static readonly Color SidebarSelection = ColorTranslator.FromHtml("#000080"); 
    public static readonly Color Accent = ColorTranslator.FromHtml("#FF69B4");      // Hot Pink
    public static readonly Color Hover = ColorTranslator.FromHtml("#C0C0C0");       // No hover in 90s
    public static readonly Color MainText = Color.Black;
    public static readonly Color SecondaryText = Color.Black;
    
    public static readonly Color AddressYellow = Color.White; 
    public static readonly Color Lila = ColorTranslator.FromHtml("#FFB6C1");        // Pink Retro
    public static readonly Color RosaIntenso = ColorTranslator.FromHtml("#FF69B4"); 

    // Compatibilidad con RetroDesignHelper
    public static readonly Color FolderMusicBg = MainBg;
    public static readonly Color FolderDocsBg = MainBg;
    public static readonly Color FolderPicsBg = MainBg;
    public static readonly Color FolderDownloadsBg = MainBg;
    public static readonly Color SelectionBorder = Color.White;

    private static int _hoverIndex = -1;
    public static void SetHoverIndex(int index) => _hoverIndex = index;
    public static int GetHoverIndex() => _hoverIndex;

    /// <summary>
    /// Dibuja un borde retro 3D (estilo Win95).
    /// </summary>
    public static void DrawRetroBorder(Graphics g, Rectangle bounds, bool raised)
    {
        Color light = Color.White;
        Color dark = ColorTranslator.FromHtml("#808080"); // Gris oscuro
        Color shadow = Color.Black;

        using Pen penLight = new Pen(raised ? light : dark, 1);
        using Pen penDark = new Pen(raised ? dark : light, 1);
        using Pen penShadow = new Pen(raised ? shadow : light, 1);
        using Pen penHighlight = new Pen(raised ? light : shadow, 1);

        // Borde exterior
        g.DrawLine(penLight, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        g.DrawLine(penLight, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
        g.DrawLine(penShadow, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(penShadow, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);

        // Borde interior grueso
        g.DrawLine(raised ? Pens.LightGray : penDark, bounds.Left + 1, bounds.Top + 1, bounds.Right - 2, bounds.Top + 1);
        g.DrawLine(raised ? Pens.LightGray : penDark, bounds.Left + 1, bounds.Top + 1, bounds.Left + 1, bounds.Bottom - 2);
        g.DrawLine(penDark, bounds.Left + 1, bounds.Bottom - 2, bounds.Right - 2, bounds.Bottom - 2);
        g.DrawLine(penDark, bounds.Right - 2, bounds.Top + 1, bounds.Right - 2, bounds.Bottom - 2);
    }

    public static void DrawListViewColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        // BUG DE WINFORMS: Al usar OwnerDraw = true + ShowGroups = true, el custom draw de 
        // los headers falla o no se muestra. Usamos DrawDefault para forzar que el OS los pinte.
        e.DrawDefault = true;
    }

    public static void DrawListViewItem(object sender, DrawListViewItemEventArgs e) 
    {
        if (e.Item == null || e.Item.ListView == null) return;
        if (e.Item.ListView.View != View.LargeIcon) return; // En Details se encarga SubItem

        bool isSelected = e.Item.Selected;
        bool isHover = (e.Item.Index == _hoverIndex);
        
        Rectangle bounds = e.Bounds;
        
        // 1. Fondo de la "Tarjeta"
        if (isSelected)
        {
            using SolidBrush selBrush = new SolidBrush(Selection);
            e.Graphics.FillRectangle(selBrush, bounds);
        }

        // 2. Dibujar el Icono (Emoji Grande)
        string icono = GetIconForType(e.Item.SubItems[1].Text, e.Item.Tag?.ToString() ?? "");
        using Font iconFont = new Font("Segoe UI Emoji", 32);
        
        // Sombra suave para el emoji
        e.Graphics.DrawString(icono, iconFont, Brushes.Gray, bounds.X + (bounds.Width / 2) - 22, bounds.Y + 12);
        e.Graphics.DrawString(icono, iconFont, Brushes.Black, bounds.X + (bounds.Width / 2) - 24, bounds.Y + 10);

        // 3. Texto del Nombre (Centrado y con elipsis)
        Rectangle textRect = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height - 35, bounds.Width - 4, 30);
        StringFormat format = new StringFormat 
        { 
            Alignment = StringAlignment.Center, 
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter
        };

        // Resaltado de texto retro
        if (isSelected)
        {
            e.Graphics.FillRectangle(new SolidBrush(Selection), textRect);
        }

        using Font nameFont = new Font("MS Sans Serif", 8);
        e.Graphics.DrawString(e.Item.Text, nameFont, new SolidBrush(isSelected ? SelectionText : MainText), textRect, format);
        
        // Foco
        if (isSelected)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, textRect);
        }
    }

    public static void DrawListViewSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item?.ListView?.View != View.Details) return;

        bool isSelected = e.Item?.Selected ?? false;
        bool isHover = (e.Item?.Index == _hoverIndex);
        
        Color itemColor = Color.White;

        Color backColor = isSelected ? Selection : itemColor;
        Color foreColor = isSelected ? SelectionText : MainText;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        // Gridlines retro
        e.Graphics.DrawLine(Pens.LightGray, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        e.Graphics.DrawLine(Pens.LightGray, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);

        if (isSelected && e.ColumnIndex == 0)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);
        }

        StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        if (e.ColumnIndex == 0)
        {
            string icono = GetIconForType(e.Item?.SubItems[1].Text ?? "", e.Item?.Tag?.ToString() ?? "");
            
            using Font iconFont = new Font("Segoe UI Emoji", 11);
            e.Graphics.DrawString(icono, iconFont, new SolidBrush(foreColor), e.Bounds.X + 6, e.Bounds.Y + 4);

            RectangleF textRect = new RectangleF(e.Bounds.X + 45, e.Bounds.Y, e.Bounds.Width - 45, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? string.Empty, e.Item?.ListView?.Font ?? Control.DefaultFont, new SolidBrush(foreColor), textRect, format);
        }
        else
        {
            RectangleF textRect = new RectangleF(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? string.Empty, e.Item?.ListView?.Font ?? Control.DefaultFont, new SolidBrush(foreColor), textRect, format);
        }
    }

    private static string GetIconForType(string tipo, string ruta)
    {
        string extension = Path.GetExtension(ruta).ToLower();
        bool esComprimido = extension == ".zip" || extension == ".7z" || extension == ".rar" || extension == ".tar";

        if (tipo == "Carpeta") return "📁";
        if (extension is ".jpg" or ".png" or ".jpeg" or ".gif" or ".bmp" or ".webp") return "🖼️";
        if (extension is ".mp3" or ".wav" or ".flac" or ".m4a") return "🎵";
        if (extension is ".mp4" or ".mkv" or ".avi" or ".mov") return "🎬";
        if (extension is ".txt" or ".cs" or ".md" or ".json" or ".py" or ".html") return "📝";
        if (esComprimido) return "📦";
        if (extension == ".pdf") return "📕";
        if (extension is ".xlsx" or ".csv" or ".xls") return "📊";
        return "📄";
    }

    public static void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null) return;
        bool isSelected = (e.State & TreeNodeStates.Selected) != 0;
        
        Color backColor = isSelected ? SidebarSelection : SecondaryBg;
        Color foreColor = isSelected ? SelectionText : MainText;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        if (isSelected)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);
        }

        // Signos de expansión estilo Win95 (Cajas con + y -)
        if (e.Node.Nodes.Count > 0)
        {
            Rectangle expandRect = new Rectangle(e.Bounds.X - 16, e.Bounds.Y + 4, 9, 9);
            e.Graphics.FillRectangle(Brushes.White, expandRect);
            e.Graphics.DrawRectangle(Pens.Gray, expandRect);
            e.Graphics.DrawLine(Pens.Black, expandRect.X + 2, expandRect.Y + 4, expandRect.X + 6, expandRect.Y + 4);
            
            if (!e.Node.IsExpanded)
            {
                e.Graphics.DrawLine(Pens.Black, expandRect.X + 4, expandRect.Y + 2, expandRect.X + 4, expandRect.Y + 6);
            }
        }

        string emoji = e.Node.Tag?.ToString() == "Inicio" ? "🏠" : 
                       (e.Node.Tag?.ToString() == "EsteEquipo" ? "💻" : "📁");
        
        if (e.Node.Text.Contains("Música")) emoji = "🎵";
        else if (e.Node.Text.Contains("Imágenes")) emoji = "🖼️";
        else if (e.Node.Text.Contains("Papelera") || e.Node.Text.Contains("Trash")) emoji = "🗑️";

        using Font iconFont = new Font("Segoe UI Emoji", 10);
        using Font textFont = new Font("MS Sans Serif", 8);
        
        e.Graphics.DrawString(emoji, iconFont, new SolidBrush(foreColor), e.Bounds.X + 2, e.Bounds.Y + 2);
        e.Graphics.DrawString(e.Node.Text, textFont, new SolidBrush(foreColor), e.Bounds.X + 22, e.Bounds.Y + 3);
    }

    // Extensiones para facilitar el dibujo redondeado
    public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
    {
        using GraphicsPath path = RoundedRect(new Rectangle(x, y, width, height), radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, int x, int y, int width, int height, int radius)
    {
        using GraphicsPath path = RoundedRect(new Rectangle(x, y, width, height), radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        Size size = new Size(diameter, diameter);
        Rectangle arc = new Rectangle(bounds.Location, size);
        GraphicsPath path = new GraphicsPath();

        if (radius == 0) { path.AddRectangle(bounds); return path; }

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

