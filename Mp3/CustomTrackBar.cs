using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// TrackBar 100% OwnerDraw con diseño premium.
/// Barra delgada con gradiente Rose→Violet y thumb circular con glow.
/// </summary>
public class CustomTrackBar : Control
{
    // === Diseño ===
    // Apple‑style colors
    private readonly Color _trackBackground = Color.FromArgb(44, 44, 46); // dark rail
    private readonly Color _progressColor = ColorTranslator.FromHtml("#FA2D48"); // Apple red
    // Thumb will be white when hovering

    private const int TRACK_HEIGHT = 2; // Ultra‑thin rail
    private const int THUMB_RADIUS = 7;

    // === Estado ===
    private double _value = 0.0;
    private bool _isDragging;
    private bool _isHovering;

    // === Eventos ===
    public event Action<double>? ValueChanged;
    public event Action<double>? ValueChangedByUser;

    /// <summary>
    /// Valor actual del trackbar (0.0 a 1.0).
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            double newVal = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(newVal - _value) > 0.0001)
            {
                _value = newVal;
                ValueChanged?.Invoke(_value);
                Invalidate();
            }
        }
    }

    public CustomTrackBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        this.Height = 24;
        this.Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int margin = THUMB_RADIUS + 2;
        int trackWidth = this.Width - (margin * 2);
        int barTop = (Height - TRACK_HEIGHT) / 2;
        int progressWidth = (int)(_value * trackWidth);

        // Rail (dark)
        using var railBrush = new SolidBrush(_trackBackground);
        g.FillRectangle(railBrush, margin, barTop, trackWidth, TRACK_HEIGHT);

        // Progress (Apple red)
        if (progressWidth > 0)
        {
            using var progBrush = new SolidBrush(_progressColor);
            g.FillRectangle(progBrush, margin, barTop, progressWidth, TRACK_HEIGHT);
        }

        // Thumb (white) only on hover
        if (_isHovering)
        {
            int thumbSize = 10;
            Rectangle thumbRect = new Rectangle(margin + progressWidth - thumbSize/2, (Height - thumbSize)/2, thumbSize, thumbSize);
            g.FillEllipse(Brushes.White, thumbRect);
        }
    }

    // === Interacción ===

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            UpdateValueFromMouse(e.X);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging)
            UpdateValueFromMouse(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            UpdateValueFromMouse(e.X);
            ValueChangedByUser?.Invoke(_value);
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovering = false;
        if (!_isDragging) Invalidate();
        base.OnMouseLeave(e);
    }

    private void UpdateValueFromMouse(int mouseX)
    {
        int margin = THUMB_RADIUS + 2;
        int trackWidth = this.Width - (margin * 2);
        double newValue = (double)(mouseX - margin) / trackWidth;
        Value = newValue;
    }

    // === Helper: Rectángulo redondeado ===
    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;

        if (rect.Width < diameter) rect.Width = diameter;
        if (rect.Height < diameter) rect.Height = diameter;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
