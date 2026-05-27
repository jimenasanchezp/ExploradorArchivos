using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;

namespace ExploradorArchivos.UI;

public static class ThemeRenderer
{
    // === Total Pink Classic Palette ===
    public static readonly Color MainBg = ColorTranslator.FromHtml("#FFF5F9");      // Rosa Crema muy claro
    public static readonly Color SecondaryBg = ColorTranslator.FromHtml("#FFD1EA"); // Rosa Pastel (Sidebar)
    public static readonly Color Accent = ColorTranslator.FromHtml("#FF80BF");      // Rosa Intenso (Botones/Tabs)
    public static readonly Color Hover = ColorTranslator.FromHtml("#FFE4F2");       // Rosa Pálido
    public static readonly Color Selection = ColorTranslator.FromHtml("#FFBDE3");   // Rosa Medio
    public static readonly Color SelectionText = Color.Black;
    public static readonly Color MainText = Color.DimGray;
    public static readonly Color SecondaryText = ColorTranslator.FromHtml("#8B5E75");

    public static readonly Color AddressYellow = Color.White;
    public static readonly Color Lila = ColorTranslator.FromHtml("#FFBDE3");        // Cambiado de Púrpura a Rosa
    public static readonly Color VerdeMenta = ColorTranslator.FromHtml("#CFF5E7");  // Se mantiene para contraste de archivos

    // Compatibilidad con carpetas
    public static readonly Color FolderMusicBg = ColorTranslator.FromHtml("#E6D4F8"); // Púrpura corregido
    public static readonly Color FolderDocsBg = VerdeMenta;
    public static readonly Color FolderPicsBg = Accent;
    public static readonly Color FolderDownloadsBg = Hover;

    private static int _hoverIndex = -1;
    public static void SetHoverIndex(int index) => _hoverIndex = index;
    public static int GetHoverIndex() => _hoverIndex;

    /// <summary>
    /// Dibuja un borde clásico 3D suave.
    /// </summary>
    public static void DrawClassicBorder(Graphics g, Rectangle bounds, bool raised)
    {
        Color light = Color.White;
        Color dark = Color.Gray;

        using Pen penLight = new Pen(raised ? light : dark, 1);
        using Pen penDark = new Pen(raised ? dark : light, 1);

        g.DrawLine(penLight, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        g.DrawLine(penLight, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
        g.DrawLine(penDark, bounds.Left + 1, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(penDark, bounds.Right - 1, bounds.Top + 1, bounds.Right - 1, bounds.Bottom - 1);
    }

    public static void DrawListViewColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        // Forzamos el dibujo por defecto para evitar errores con los grupos
        e.DrawDefault = true;
    }

    public static void DrawListViewItem(object sender, DrawListViewItemEventArgs e)
    {
        if (e.Item == null || e.Item.ListView == null) return;
        if (e.Item.ListView.View != View.LargeIcon) return;

        bool isSelected = e.Item.Selected;
        Rectangle bounds = e.Bounds;

        if (isSelected)
        {
            using SolidBrush selBrush = new SolidBrush(Selection);
            e.Graphics.FillRectangle(selBrush, bounds);
        }

        string icono = GetIconForType(e.Item.SubItems[1].Text, e.Item.Tag?.ToString() ?? "");
        using Font iconFont = new Font("Segoe UI Emoji", 32);
        e.Graphics.DrawString(icono, iconFont, Brushes.Black, bounds.X + (bounds.Width / 2) - 24, bounds.Y + 10);

        Rectangle textRect = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height - 35, bounds.Width - 4, 30);
        StringFormat format = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        using Font nameFont = new Font("Segoe UI", 9);
        e.Graphics.DrawString(e.Item.Text, nameFont, new SolidBrush(isSelected ? SelectionText : MainText), textRect, format);
    }

    public static void DrawListViewSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item?.ListView?.View != View.Details) return;

        bool isSelected = e.Item?.Selected ?? false;
        Color itemColor = e.Item?.BackColor ?? Color.White; // Respeta el color dinámico de la carpeta

        Color backColor = isSelected ? Selection : itemColor;
        Color foreColor = isSelected ? SelectionText : MainText;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        // Gridlines clásicos suaves
        e.Graphics.DrawLine(Pens.Lavender, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        if (e.ColumnIndex == 0) // Columna Nombre
        {
            string icono = GetIconForType(e.Item?.SubItems[1].Text ?? "", e.Item?.Tag?.ToString() ?? "");
            using Font iconFont = new Font("Segoe UI Emoji", 11);
            e.Graphics.DrawString(icono, iconFont, new SolidBrush(foreColor), e.Bounds.X + 6, e.Bounds.Y + 4);

            // Espaciado corregido para no amontonar el texto
            RectangleF textRect = new RectangleF(e.Bounds.X + 45, e.Bounds.Y, e.Bounds.Width - 50, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? string.Empty, e.Item?.ListView?.Font ?? Control.DefaultFont, new SolidBrush(foreColor), textRect, format);
        }
        else // Otras columnas (Tipo, Tamaño, Fecha)
        {
            // Margen de 15px para evitar que el texto choque entre columnas
            RectangleF textRect = new RectangleF(e.Bounds.X + 15, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? string.Empty, e.Item?.ListView?.Font ?? Control.DefaultFont, new SolidBrush(foreColor), textRect, format);
        }
    }

    public static void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null) return;
        bool isSelected = (e.State & TreeNodeStates.Selected) != 0;

        Color backColor = isSelected ? Accent : SecondaryBg;
        Color foreColor = isSelected ? SelectionText : MainText;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        // Icono simple basado en el tipo de nodo
        string emoji = "📁";
        string? tagStr = e.Node.Tag?.ToString();

        if (tagStr == "Inicio" || e.Node.Text == "Inicio") emoji = "🏠";
        else if (tagStr == "EsteEquipo" || e.Node.Text == "Este Equipo") emoji = "💻";
        else if (e.Node.Text == "Favoritos") emoji = "⭐";
        else if (e.Node.Text.Contains("Música")) emoji = "🎵";
        else if (e.Node.Text.Contains("Imágenes")) emoji = "🖼️";
        else if (e.Node.Text.Contains("Vídeos") || e.Node.Text.Contains("Videos")) emoji = "🎬";
        else if (e.Node.Text.Contains("Descargas")) emoji = "📥";
        else if (e.Node.Text.Contains("Documentos")) emoji = "📄";
        else if (e.Node.Text.Contains("Escritorio")) emoji = "🖥️";
        else if (tagStr != null && File.Exists(tagStr))
        {
            // Es un archivo fijado
            string ext = Path.GetExtension(tagStr).ToLower();
            if (new[] { ".mp3", ".wav", ".flac", ".ogg", ".wma", ".aac" }.Contains(ext)) emoji = "🎵";
            else if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }.Contains(ext)) emoji = "🖼️";
            else if (new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v" }.Contains(ext)) emoji = "🎬";
            else if (new[] { ".txt", ".cs", ".json", ".xml", ".md", ".html", ".css", ".js", ".py" }.Contains(ext)) emoji = "📄";
            else emoji = "📄";
        }

        using Font iconFont = new Font("Segoe UI Emoji", 10);
        using Font textFont = new Font("Segoe UI", 9.5f);

        // Dibujar el emoji y luego el texto dejando dos espacios (desplazamiento de 28px)
        e.Graphics.DrawString(emoji, iconFont, new SolidBrush(foreColor), e.Bounds.X + 4, e.Bounds.Y + 4);
        e.Graphics.DrawString(e.Node.Text, textFont, new SolidBrush(foreColor), e.Bounds.X + 28, e.Bounds.Y + 5);
    }

    public static void ApplyTheme(Control parent)
    {
        if (parent is Form) parent.BackColor = MainBg;
        
        Font standardFont;
        // Aumentamos de 9 a 10.5 para que el diseño se vea más lleno y legible
        try { standardFont = new Font("MS Sans Serif", 10.5f); } catch { standardFont = new Font("Microsoft Sans Serif", 10.5f); }
        parent.Font = standardFont;

        foreach (Control c in parent.Controls)
        {
            // Respetamos los botones semáforo (no les cambiamos el BackColor)
            bool isSemaforo = c is Button && (c.Width < 20 || c.Name.ToLower().Contains("semaforo") || c.Parent?.Name.ToLower().Contains("semaforo") == true);

            if (c is Button btn && !isSemaforo)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = MainBg;
                btn.ForeColor = MainText;
                btn.Font = new Font(standardFont, FontStyle.Bold);
            }
            else if (c is Label lbl)
            {
                lbl.ForeColor = MainText;
                // Si es un título de grupo (en el sidebar por ejemplo), lo hacemos un poco más grande
                if (lbl.Text.ToUpper() == lbl.Text && lbl.Text.Length > 3)
                    lbl.Font = new Font(standardFont.FontFamily, 11.5f, FontStyle.Bold);
            }
            else if (c is Panel pnl)
            {
                if (pnl.Name.ToLower().Contains("sidebar") || pnl.Name.ToLower().Contains("pnlleft") || pnl.Name.ToLower().Contains("pnlsearch"))
                    pnl.BackColor = SecondaryBg;
            }
            else if (c is TabPage tp)
            {
                tp.BackColor = SecondaryBg;
            }
            else if (c is MenuStrip ms)
            {
                ms.BackColor = SecondaryBg;
                ms.ForeColor = MainText;
            }
            else if (c is DataGridView dgv)
            {
                dgv.BackgroundColor = MainBg;
                dgv.DefaultCellStyle.BackColor = MainBg;
                dgv.DefaultCellStyle.Font = standardFont;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = SecondaryBg;
                dgv.EnableHeadersVisualStyles = false;
            }

            // Aplicamos recursivamente pero pasamos el standardFont para mantener consistencia
            if (c.HasChildren) ApplyTheme(c);
            
            // Aseguramos que la fuente se aplique a todo después de la recursión si no fue capturado antes
            if (!(c is Button && isSemaforo))
                c.Font = standardFont;
        }
    }

    private static string GetIconForType(string tipo, string ruta)
    {
        string extension = Path.GetExtension(ruta).ToLower();
        if (tipo == "Carpeta") return "📁";
        if (extension is ".jpg" or ".png" or ".jpeg") return "🖼️";
        if (extension is ".mp3" or ".wav") return "🎵";
        if (extension is ".mp4" or ".mkv") return "🎬";
        if (extension is ".txt" or ".cs" or ".md") return "📝";
        if (extension == ".pdf") return "📕";
        return "📄";
    }
}