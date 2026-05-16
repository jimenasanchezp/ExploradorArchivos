using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ExploradorArchivos.AppDataFusion;

public enum TipoGrafica { Columnas, Barras, Pastel }

/// <summary>
/// Control GDI+ propio para gráficas. Reemplaza System.Windows.Forms.DataVisualization
/// que tiene dependencia rota en System.Data.SqlClient en .NET 10.
/// </summary>
public class ChartPanel : Panel
{
    private List<(string Label, double Value)> _data = new();
    private TipoGrafica _tipo = TipoGrafica.Columnas;
    private string _titulo = "Sin datos — carga archivos primero";

    // ── Paleta Kawaii 95 ────────────────────────────
    private static readonly Color[] Palette =
    {
        Color.FromArgb(255, 128, 191), Color.FromArgb(255, 189, 227),
        Color.FromArgb(230, 150, 255), Color.FromArgb(180, 130, 255),
        Color.FromArgb(255, 160, 160), Color.FromArgb(255, 200, 150),
        Color.FromArgb(160, 220, 255), Color.FromArgb(200, 180, 255),
        Color.FromArgb(255, 100, 150), Color.FromArgb(220, 140, 240)
    };

    private static readonly Color BgPanel = Color.FromArgb(255, 245, 249);
    private static readonly Color Grid = Color.FromArgb(255, 209, 234);
    private static readonly Color AxisClr = Color.FromArgb(150, 80, 110);
    private static readonly Color LabelClr = Color.FromArgb(80, 40, 60);

    public ChartPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = BgPanel;
    }

    public void SetData(List<(string Label, double Value)> data,
                        TipoGrafica tipo, string titulo)
    {
        _data = data ?? new();
        _tipo = tipo;
        _titulo = titulo;
        Invalidate();
    }

    public void Limpiar()
    {
        _data.Clear();
        _titulo = "Sin datos — carga archivos primero";
        Invalidate();
    }

    // ════════════════════════════════════════════════════════
    //  PAINT
    // ════════════════════════════════════════════════════════
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(BgPanel);

        if (_data.Count == 0 || _data.Sum(d => d.Value) == 0)
        {
            DrawCentered(g, "Sin datos — carga archivos primero", LabelClr);
            return;
        }

        switch (_tipo)
        {
            case TipoGrafica.Columnas: DrawColumnas(g); break;
            case TipoGrafica.Barras: DrawBarras(g); break;
            case TipoGrafica.Pastel: DrawPastel(g); break;
        }
    }

    // ════════════════════════════════════════════════════════
    //  COLUMNAS (vertical)
    // ════════════════════════════════════════════════════════
    private void DrawColumnas(Graphics g)
    {
        var (area, _) = PrepararArea(g, TipoGrafica.Columnas);
        double max = _data.Max(d => d.Value);
        if (max <= 0) return;

        DrawGridLinesH(g, area, max);
        DrawEjes(g, area);

        float bw = area.Width / _data.Count;
        float gap = Math.Max(3f, bw * 0.18f);

        for (int i = 0; i < _data.Count; i++)
        {
            var (lbl, val) = _data[i];
            float h = (float)(val / max * area.Height);
            float x = area.Left + i * bw + gap / 2f;
            float y = area.Bottom - h;
            float w = bw - gap;
            var c = Palette[i % Palette.Length];

            if (h > 0 && w > 0)
            {
                var rect = new RectangleF(x, y, w, h);
                using var br = new LinearGradientBrush(
                    new PointF(x, y), new PointF(x, area.Bottom),
                    Color.FromArgb(230, c), Color.FromArgb(130, c));
                g.FillRectangle(br, rect);
                using var pen = new Pen(Color.FromArgb(180, c), 1f);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            string vs = FormatVal(val);
            using var vf = new Font("MS Sans Serif", 8f, FontStyle.Bold);
            var vs2 = g.MeasureString(vs, vf);
            if (vs2.Width < w + 4)
                g.DrawString(vs, vf, new SolidBrush(Color.FromArgb(200, c)),
                    x + (w - vs2.Width) / 2f,
                    Math.Max(y - vs2.Height - 2f, area.Top + 2));

            DrawXLabel(g, lbl, x + w / 2f, area.Bottom + 5f, c);
        }
    }

    // ════════════════════════════════════════════════════════
    //  BARRAS (horizontal) — etiquetas dinámicas, nunca cortadas
    // ════════════════════════════════════════════════════════
    private void DrawBarras(Graphics g)
    {
        float titleH = DrawTitulo(g, TipoGrafica.Barras);

        // ── 1. Medir el label más largo para calcular el margen izquierdo
        using var lblFont = new Font("MS Sans Serif", 8.5f);
        float maxLabelW = 0f;
        foreach (var (lbl, _) in _data)
        {
            // Texto completo para medir — no truncamos todavía
            float w = g.MeasureString(lbl, lblFont).Width;
            if (w > maxLabelW) maxLabelW = w;
        }

        // Margen izquierdo = ancho del label más largo + 10px de respiración
        // Mínimo 90px, máximo 40% del panel para no aplastar las barras
        float marginLeft = Math.Clamp(maxLabelW + 10f, 90f, Width * 0.40f);
        float marginRight = 64f;  // espacio para el valor a la derecha
        float marginTop = titleH + 8f;
        float marginBot = 16f;

        var area = new RectangleF(
            marginLeft,
            marginTop,
            Width - marginLeft - marginRight,
            Height - marginTop - marginBot);

        if (area.Height < 20 || area.Width < 20) return;

        double max = _data.Max(d => d.Value);
        if (max <= 0) return;

        DrawGridLinesV(g, area, max);
        DrawEjes(g, area);

        float bh = area.Height / _data.Count;
        float gap = Math.Max(2f, bh * 0.20f);

        for (int i = 0; i < _data.Count; i++)
        {
            var (lbl, val) = _data[i];
            float bw = (float)(val / max * area.Width);
            float y = area.Top + i * bh + gap / 2f;
            float h = bh - gap;
            var c = Palette[i % Palette.Length];

            // Barra con gradiente
            if (bw > 0 && h > 0)
            {
                var rect = new RectangleF(area.Left, y, bw, h);
                using var br = new LinearGradientBrush(
                    new PointF(area.Left, y), new PointF(area.Left + bw, y),
                    Color.FromArgb(130, c), Color.FromArgb(220, c));
                g.FillRectangle(br, rect);
                using var pen = new Pen(Color.FromArgb(180, c), 1f);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // ── Valor a la derecha de la barra
            string vs = FormatVal(val);
            using var vf = new Font("MS Sans Serif", 8f);
            var vsz = g.MeasureString(vs, vf);
            g.DrawString(vs, vf,
                new SolidBrush(Color.FromArgb(190, c)),
                area.Left + bw + 5f,
                y + (h - vsz.Height) / 2f);

            // ── Etiqueta a la izquierda, ajustada al margen disponible
            // El espacio real disponible para el texto es marginLeft - 8px
            float availableW = marginLeft - 8f;
            string displayLbl = lbl;

            // Si el texto completo no cabe, truncar con "…"
            if (g.MeasureString(lbl, lblFont).Width > availableW)
            {
                // Truncar carácter a carácter hasta que quepa
                displayLbl = "";
                foreach (char ch in lbl)
                {
                    string candidate = displayLbl + ch;
                    if (g.MeasureString(candidate + "…", lblFont).Width > availableW)
                        break;
                    displayLbl = candidate;
                }
                displayLbl += "…";
            }

            var lblSz = g.MeasureString(displayLbl, lblFont);
            g.DrawString(
                displayLbl,
                lblFont,
                new SolidBrush(Color.FromArgb(200, c)),
                area.Left - lblSz.Width - 6f,       // alineado a la derecha del margen
                y + (h - lblSz.Height) / 2f);
        }
    }

    // ════════════════════════════════════════════════════════
    //  PASTEL
    // ════════════════════════════════════════════════════════
    private void DrawPastel(Graphics g)
    {
        float titleH = DrawTitulo(g, TipoGrafica.Pastel);
        double total = _data.Sum(d => d.Value);
        if (total <= 0) return;

        float legendW = Math.Min(220f, Width * 0.30f);
        float drawW = Width - legendW - 20f;
        float drawH = Height - titleH - 20f;
        float radius = Math.Min(drawW / 2f - 30f, drawH / 2f - 20f);
        if (radius < 15) return;

        float cx = 20f + drawW / 2f;
        float cy = titleH + 20f + drawH / 2f;

        var pieRect = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);
        float startAngle = -90f;

        for (int i = 0; i < _data.Count; i++)
        {
            var (lbl, val) = _data[i];
            float sweep = (float)(val / total * 360.0);
            var c = Palette[i % Palette.Length];

            using var br = new SolidBrush(c);
            using var sep = new Pen(BgPanel, 2f);
            g.FillPie(br, pieRect, startAngle, sweep);
            g.DrawPie(sep, pieRect, startAngle, sweep);

            if (sweep > 12f)
            {
                double midRad = (startAngle + sweep / 2f) * Math.PI / 180.0;
                float lx = cx + (float)(Math.Cos(midRad) * radius * 0.62f);
                float ly = cy + (float)(Math.Sin(midRad) * radius * 0.62f);
                string pct = $"{val / total:P0}";
                using var pf = new Font("MS Sans Serif", 8f, FontStyle.Bold);
                var ps = g.MeasureString(pct, pf);
                g.DrawString(pct, pf, Brushes.White,
                    lx - ps.Width / 2f, ly - ps.Height / 2f);
            }
            startAngle += sweep;
        }

        // Leyenda derecha
        float lx2 = Width - legendW + 10f;
        float ly2 = titleH + 30f;
        using var legF = new Font("MS Sans Serif", 8.5f);

        for (int i = 0; i < _data.Count && ly2 + 16 < Height - 10; i++)
        {
            var (lbl, val) = _data[i];
            var c = Palette[i % Palette.Length];
            string display = lbl.Length > 22 ? lbl[..20] + "…" : lbl;
            string pct = $"{val / total:P1}";

            g.FillRectangle(new SolidBrush(c), lx2, ly2 + 1, 11, 11);
            g.DrawString($"{display}  {pct}", legF,
                new SolidBrush(LabelClr), lx2 + 15f, ly2 - 1f);
            ly2 += 16f;
        }
    }

    // ════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════

    private float DrawTitulo(Graphics g, TipoGrafica t)
    {
        var titleColor = t switch
        {
            TipoGrafica.Columnas => Color.FromArgb(232, 48, 90), // Rose
            TipoGrafica.Barras => Color.FromArgb(150, 80, 110),
            TipoGrafica.Pastel => Color.FromArgb(120, 60, 150),
            _ => Color.Black
        };
        using var tf = new Font("MS Sans Serif", 10.5f, FontStyle.Bold);
        var ts = g.MeasureString(_titulo, tf);
        g.DrawString(_titulo, tf, new SolidBrush(titleColor),
            (Width - ts.Width) / 2f, 8f);
        return ts.Height + 14f;
    }

    private (RectangleF area, Color tc) PrepararArea(Graphics g, TipoGrafica t)
    {
        float titleH = DrawTitulo(g, t);
        const int marginLeft = 68;
        const int marginBottom = 80;  // enough room for 45° rotated labels
        var area = new RectangleF(
            marginLeft,
            titleH + 5,
            Width - marginLeft - 20,
            Height - titleH - marginBottom);
        return (area, Color.White);
    }

    private void DrawGridLinesH(Graphics g, RectangleF area, double max)
    {
        using var gp = new Pen(Grid, 1f) { DashStyle = DashStyle.Dash };
        using var lf = new Font("MS Sans Serif", 8f);
        const int n = 5;
        for (int i = 0; i <= n; i++)
        {
            double val = max * i / n;
            float y = area.Bottom - (float)(i * area.Height / n);
            g.DrawLine(gp, area.Left, y, area.Right, y);
            string vs = FormatVal(val);
            var sz = g.MeasureString(vs, lf);
            g.DrawString(vs, lf, new SolidBrush(LabelClr),
                area.Left - sz.Width - 3f, y - sz.Height / 2f);
        }
    }

    private void DrawGridLinesV(Graphics g, RectangleF area, double max)
    {
        using var gp = new Pen(Grid, 1f) { DashStyle = DashStyle.Dash };
        using var lf = new Font("MS Sans Serif", 8f);
        const int n = 5;
        for (int i = 0; i <= n; i++)
        {
            double val = max * i / n;
            float x = area.Left + (float)(i * area.Width / n);
            g.DrawLine(gp, x, area.Top, x, area.Bottom);
            string vs = FormatVal(val);
            var sz = g.MeasureString(vs, lf);
            g.DrawString(vs, lf, new SolidBrush(LabelClr),
                x - sz.Width / 2f, area.Bottom + 3f);
        }
    }

    private void DrawEjes(Graphics g, RectangleF area)
    {
        using var p = new Pen(AxisClr, 1.5f);
        g.DrawLine(p, area.Left, area.Top, area.Left, area.Bottom);
        g.DrawLine(p, area.Left, area.Bottom, area.Right, area.Bottom);
    }

    private void DrawXLabel(Graphics g, string label, float cx, float baseY, Color c)
    {
        // Calculate max chars that fit given the available diagonal space
        // With 45° rotation and marginBottom=80, diagonal length ≈ 80/sin(45°) ≈ 113px
        using var f = new Font("MS Sans Serif", 8f);
        string txt = label;

        // Measure and truncate if needed to fit within ~110px of diagonal space
        while (txt.Length > 1 && g.MeasureString(txt, f).Width > 110)
            txt = txt[..^1];
        if (txt.Length < label.Length) txt = txt.TrimEnd() + "…";

        var sz = g.MeasureString(txt, f);
        var state = g.Save();
        g.TranslateTransform(cx, baseY + 4f);
        g.RotateTransform(-45f);
        g.DrawString(txt, f, new SolidBrush(Color.FromArgb(190, c)),
            -sz.Width / 2f, 0f);
        g.Restore(state);
    }

    private void DrawCentered(Graphics g, string text, Color c)
    {
        using var f = new Font("MS Sans Serif", 11.5f, FontStyle.Bold);
        var sz = g.MeasureString(text, f);
        g.DrawString(text, f, new SolidBrush(c),
            (Width - sz.Width) / 2f,
            (Height - sz.Height) / 2f);
    }

    private static string FormatVal(double v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000:F1}M";
        if (v >= 1_000) return $"{v / 1_000:F0}K";
        // Show as integer if the value has no meaningful decimal part
        if (v == Math.Floor(v)) return $"{(long)v}";
        return $"{v:F2}";
    }
}

