using System.Drawing;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

public static class ThemeRenderer
{
    public static readonly Color MainBg = ColorTranslator.FromHtml("#FFF5F8");
    public static readonly Color SecondaryBg = ColorTranslator.FromHtml("#FCE4EC");
    public static readonly Color Accent = ColorTranslator.FromHtml("#F48FB1");
    public static readonly Color Hover = ColorTranslator.FromHtml("#F8BBD0");
    public static readonly Color MainText = ColorTranslator.FromHtml("#2D2D2D");
    public static readonly Color SecondaryText = ColorTranslator.FromHtml("#888888");

    public static void DrawListViewColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        using SolidBrush bgBrush = new SolidBrush(MainBg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        using SolidBrush textBrush = new SolidBrush(SecondaryText);
        StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(e.Header.Text, e.Font, textBrush, e.Bounds, format);

        using Pen borderPen = new Pen(SecondaryBg);
        e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    public static void DrawListViewItem(object sender, DrawListViewItemEventArgs e) { } // Delegado a SubItem

    public static void DrawListViewSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        // Color base dependiendo si es fila par o impar
        Color defaultBg = (e.Item.Index % 2 == 0) ? MainBg : ColorTranslator.FromHtml("#FFF0F5");

        Color backColor = e.Item.Selected ? Accent : defaultBg;
        Color foreColor = e.Item.Selected ? Color.White : MainText;

        using SolidBrush bgBrush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        using SolidBrush textBrush = new SolidBrush(foreColor);
        StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center };
        RectangleF rect = new RectangleF(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
        e.Graphics.DrawString(e.SubItem.Text, e.Item.ListView.Font, textBrush, rect, format);
    }

    public static void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null) return;
        bool isSelected = (e.State & TreeNodeStates.Selected) != 0;
        Color backColor = isSelected ? Accent : SecondaryBg;
        Color foreColor = isSelected ? Color.White : MainText;

        using SolidBrush bgBrush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        using SolidBrush textBrush = new SolidBrush(foreColor);
        e.Graphics.DrawString(e.Node.Text, e.Node.TreeView.Font, textBrush, e.Bounds.X + 2, e.Bounds.Y + 2);
    }
}