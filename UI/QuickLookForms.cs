using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms; // ¡Requisito indispensable para PDFs!

namespace ExploradorArchivos.UI
{
    public class QuickLookForm : Form
    {
        private PictureBox _pictureBox;
        private RichTextBox _richTextBox;
        private WebView2 _webView;

        public QuickLookForm(string filePath)
        {
            // 1. Configuración Ultra-Flat
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = ColorTranslator.FromHtml("#1E1E1E");
            this.Size = new Size(800, 600);
            this.ShowInTaskbar = false;
            this.KeyPreview = true;

            this.Padding = new Padding(2);
            this.Paint += (s, e) => {
                using Pen pen = new Pen(ThemeRenderer.Accent, 2);
                e.Graphics.DrawRectangle(pen, 1, 1, this.Width - 2, this.Height - 2);
            };

            // 2. Título superior
            Label lblTitle = new Label
            {
                Text = Path.GetFileName(filePath),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Variable", 12, FontStyle.Bold)
            };
            this.Controls.Add(lblTitle);

            // 3. Cargar el archivo
            CargarContenido(filePath);

            // 4. Cerrar con Espacio o Escape
            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Escape)
                    this.Close();
            };

            // Cerrar si haces clic fuera de la ventana
            this.Deactivate += (s, e) => this.Close();
        }

        private void CargarContenido(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();

            // === IMÁGENES ===
            if (ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp")
            {
                _pictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    _pictureBox.Image = Image.FromStream(fs);
                }
                this.Controls.Add(_pictureBox);
                _pictureBox.BringToFront();
            }
            // === TEXTO / CÓDIGO ===
            else if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".csv" or ".log")
            {
                _richTextBox = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = ThemeRenderer.MainBg,
                    ForeColor = ThemeRenderer.MainText,
                    Font = new Font("Consolas", 11),
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None
                };

                FileInfo fi = new FileInfo(filePath);
                if (fi.Length > 1024 * 1024)
                    _richTextBox.Text = "El archivo es demasiado grande para la vista previa rápida.";
                else
                    _richTextBox.Text = File.ReadAllText(filePath);

                this.Controls.Add(_richTextBox);
                _richTextBox.BringToFront();
            }
            // === PDFs Y WEB ===
            else if (ext == ".pdf")
            {
                CargarPDFAsync(filePath);
            }
            // === ARCHIVO NO SOPORTADO ===
            else
            {
                Label lblNoPreview = new Label
                {
                    Text = "Vista previa no disponible para este formato.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 12)
                };
                this.Controls.Add(lblNoPreview);
                lblNoPreview.BringToFront();
            }
        }

        private async void CargarPDFAsync(string filePath)
        {
            try
            {
                _webView = new WebView2
                {
                    Dock = DockStyle.Fill,
                    BackColor = ColorTranslator.FromHtml("#1E1E1E")
                };

                this.Controls.Add(_webView);
                _webView.BringToFront();

                // Inicializar motor Chromium
                await _webView.EnsureCoreWebView2Async(null);

                // Navegar al PDF
                _webView.CoreWebView2.Navigate(filePath);
            }
            catch (Exception)
            {
                Label lblError = new Label
                {
                    Text = "Error al cargar el visor PDF.\nVerifica que WebView2 esté instalado correctamente.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = ThemeRenderer.Accent,
                    Font = new Font("Segoe UI", 11)
                };
                this.Controls.Add(lblError);
                lblError.BringToFront();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pictureBox?.Image?.Dispose();
            _webView?.Dispose(); // Liberamos memoria de Chromium
            base.OnFormClosed(e);
        }
    }
}