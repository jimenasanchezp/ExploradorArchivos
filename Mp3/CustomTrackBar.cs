using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// Control de barra de progreso 100% dibujado a mano (OwnerDraw).
/// Dibuja un riel delgado y un pulgar circular que reacciona a los eventos del ratón.
/// </summary>
public class CustomTrackBar : Control
{
    // === Diseño ===
    private readonly Color _trackBackground = Color.FromArgb(44, 44, 46); // Gris oscuro estilo Apple
    private readonly Color _progressColor = ColorTranslator.FromHtml("#FA2D48"); // Rojo estilo Apple Music
    private const int TRACK_HEIGHT = 2;
    private const int THUMB_RADIUS = 7;

    // === Estado ===
    private double _value = 0.0;
    private bool _isDragging;
    private bool _isHovering;

    // === Eventos ===
    public event Action<double>? ValueChanged;
    public event Action<double>? ValueChangedByUser; // Disparado únicamente al interactuar con el mouse

    /// <summary>
    /// Propiedad pública que encapsula el valor actual del progreso (rango: 0.0 a 1.0).
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            double newVal = Math.Clamp(value, 0.0, 1.0);

            // Evitar redibujado redundante si la diferencia de valor es insignificante
            if (Math.Abs(newVal - _value) > 0.0001)
            {
                _value = newVal;
                ValueChanged?.Invoke(_value);
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Constructor de la barra de progreso personalizada.
    /// Configura los estilos de dibujo óptimos y establece dimensiones iniciales.
    /// </summary>
    public CustomTrackBar()
    {
        // Optimización del dibujado para evitar parpadeos
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        this.Height = 24;
        this.Cursor = Cursors.Hand;
    }

    /// <summary>
    /// Sobrescribe el renderizado por defecto para dibujar la barra de progreso utilizando GDI+.
    /// Incluye suavizado (AntiAlias) y dibuja el riel, el progreso y el pulgar dinámico.
    /// </summary>
    /// <param name="e">Argumentos del evento Paint con el objeto Graphics asociado.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Margen para evitar que el pulgar circular se recorte al llegar a los extremos
        int margin = THUMB_RADIUS + 2;
        int trackWidth = this.Width - (margin * 2);
        int barTop = (Height - TRACK_HEIGHT) / 2;
        int progressWidth = (int)(_value * trackWidth);

        // 1. Dibujado del riel de fondo
        using var railBrush = new SolidBrush(_trackBackground);
        g.FillRectangle(railBrush, margin, barTop, trackWidth, TRACK_HEIGHT);

        // 2. Dibujado del riel de progreso activo
        if (progressWidth > 0)
        {
            using var progBrush = new SolidBrush(_progressColor);
            g.FillRectangle(progBrush, margin, barTop, progressWidth, TRACK_HEIGHT);
        }

        // 3. Dibujado del pulgar circular (sólo se visualiza con el puntero encima)
        if (_isHovering)
        {
            int thumbSize = 10;
            Rectangle thumbRect = new Rectangle(
                margin + progressWidth - thumbSize / 2, 
                (Height - thumbSize) / 2, 
                thumbSize, 
                thumbSize
            );

            g.FillEllipse(Brushes.White, thumbRect);
        }
    }

    // === Interacción de Ratón ===

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
        {
            UpdateValueFromMouse(e.X);
        }
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
        if (!_isDragging)
        {
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    /// <summary>
    /// Calcula e inicializa el nuevo valor de progreso proporcional de 0.0 a 1.0 basándose en la coordenada X del ratón.
    /// </summary>
    /// <param name="mouseX">Posición X actual del puntero del ratón respecto al control.</param>
    private void UpdateValueFromMouse(int mouseX)
    {
        int margin = THUMB_RADIUS + 2;
        int trackWidth = this.Width - (margin * 2);

        double newValue = (double)(mouseX - margin) / trackWidth;
        Value = newValue;
    }
}
