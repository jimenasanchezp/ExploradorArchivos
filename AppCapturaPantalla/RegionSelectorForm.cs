using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExploradorArchivos.AppCapturaPantalla
{
    /// <summary>
    /// Formulario overlay transparente que permite al usuario dibujar un rectángulo
    /// de selección sobre la pantalla con el mouse (similar a la herramienta de recorte de Windows).
    /// Se muestra a pantalla completa con fondo semitransparente y retorna el <see cref="Rectangle"/>
    /// seleccionado en coordenadas absolutas de pantalla.
    /// </summary>
    public class RegionSelectorForm : Form
    {
        // ─── Estado de selección ─────────────────────────────────────────────────
        private Point  _inicio    = Point.Empty;
        private Point  _fin       = Point.Empty;
        private bool   _dibujando = false;

        /// <summary>El rectángulo seleccionado en coordenadas de pantalla. Vacío si se canceló.</summary>
        public Rectangle RegionSeleccionada { get; private set; } = Rectangle.Empty;

        // ════════════════════════════════════════════════════════════════════════
        public RegionSelectorForm()
        {
            // Configuración del overlay
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState     = FormWindowState.Maximized;
            this.TopMost         = true;
            this.Cursor          = Cursors.Cross;
            this.DoubleBuffered  = true;
            this.BackColor       = Color.Black;
            this.Opacity         = 0.45;

            // Texto de instrucciones (se dibuja en OnPaint)
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    RegionSeleccionada = Rectangle.Empty;
                    this.DialogResult  = DialogResult.Cancel;
                    this.Close();
                }
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _inicio    = e.Location;
                _fin       = e.Location;
                _dibujando = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dibujando)
            {
                _fin = e.Location;
                this.Invalidate(); // Forzar redibujado del rectángulo de selección
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dibujando && e.Button == MouseButtons.Left)
            {
                _dibujando = false;
                _fin       = e.Location;

                // Calcular rectángulo normalizado (inicio puede estar en cualquier esquina)
                Rectangle seleccion = NormalizarRectangulo(_inicio, _fin);

                if (seleccion.Width > 5 && seleccion.Height > 5)
                {
                    // Convertir de coordenadas del form a coordenadas absolutas de pantalla
                    Point origenPantalla = this.PointToScreen(seleccion.Location);
                    RegionSeleccionada   = new Rectangle(origenPantalla, seleccion.Size);
                    this.DialogResult    = DialogResult.OK;
                }
                else
                {
                    RegionSeleccionada = Rectangle.Empty;
                    this.DialogResult  = DialogResult.Cancel;
                }
                this.Close();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            // Instrucciones en la parte superior
            string instruccion = "Arrastra para seleccionar la región   •   ESC para cancelar";
            using var fuenteTexto  = new Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold);
            using var pincelFondo  = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            using var pincelTexto  = new SolidBrush(Color.White);

            SizeF tamTexto = g.MeasureString(instruccion, fuenteTexto);
            RectangleF fondoTexto = new RectangleF(
                (this.Width - tamTexto.Width) / 2f - 15,
                20,
                tamTexto.Width + 30,
                tamTexto.Height + 10);
            g.FillRectangle(pincelFondo, fondoTexto);
            g.DrawString(instruccion, fuenteTexto, pincelTexto,
                fondoTexto.X + 15, fondoTexto.Y + 5);

            // Rectángulo de selección en tiempo real
            if (_dibujando && _inicio != _fin)
            {
                Rectangle rect = NormalizarRectangulo(_inicio, _fin);

                // Área seleccionada (más clara)
                using var pincelSeleccion = new SolidBrush(Color.FromArgb(60, 100, 180, 255));
                g.FillRectangle(pincelSeleccion, rect);

                // Borde del rectángulo
                using var lapizBorde = new Pen(Color.FromArgb(255, 80, 160, 255), 2);
                g.DrawRectangle(lapizBorde, rect);

                // Mostrar dimensiones
                string dims = $"{rect.Width} × {rect.Height} px";
                using var fuenteDims  = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
                using var pincelDimsBg = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
                SizeF sizeDims = g.MeasureString(dims, fuenteDims);
                float xDims = Math.Min(_fin.X + 8, this.Width - sizeDims.Width - 5);
                float yDims = Math.Min(_fin.Y + 8, this.Height - sizeDims.Height - 5);
                g.FillRectangle(pincelDimsBg, xDims - 4, yDims - 2, sizeDims.Width + 8, sizeDims.Height + 4);
                g.DrawString(dims, fuenteDims, pincelTexto, xDims, yDims);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Convierte dos puntos (inicio y fin del arrastre, en cualquier dirección)
        /// en un <see cref="Rectangle"/> siempre con X,Y positivos y Width/Height positivos.
        /// </summary>
        private static Rectangle NormalizarRectangulo(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            int w = Math.Abs(a.X - b.X);
            int h = Math.Abs(a.Y - b.Y);
            return new Rectangle(x, y, w, h);
        }
    }
}
