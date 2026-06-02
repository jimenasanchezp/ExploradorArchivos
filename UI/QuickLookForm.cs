using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms; // ¡Requisito indispensable para PDFs!

namespace ExploradorArchivos.UI;

/// <summary>
/// Ventana de vista previa ultra rápida ("Quick Look" estilo macOS).
/// Permite previsualizar imágenes, texto y PDFs sin abrir un editor completo.
/// </summary>
public class QuickLookForm : Form
{
    private PictureBox? _pictureBox; 
    private RichTextBox? _richTextBox;
    private WebView2? _webView;

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

        // 2. Barra de título y Semáforos
        Panel pnlTitleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = ColorTranslator.FromHtml("#1E1E1E")
        };

        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTitleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTitleBar.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTitleBar.MouseUp += (s, e) => { isDragging = false; };
        pnlTitleBar.DoubleClick += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        Panel pnlSemaforos = new Panel { Name = "pnlSemaforos", Location = new Point(15, 13), Size = new Size(60, 20), BackColor = Color.Transparent };
        
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0, pnlTitleBar.BackColor);
        btnClose.Click += (s, e) => this.Close();
        
        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20, pnlTitleBar.BackColor);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        
        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40, pnlTitleBar.BackColor);
        btnMax.Click += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        pnlSemaforos.Controls.AddRange([btnClose, btnMin, btnMax]);
        pnlTitleBar.Controls.Add(pnlSemaforos);

        Label lblTitle = new Label
        {
            Text = Path.GetFileName(filePath),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable", 12, FontStyle.Bold)
        };
        
        lblTitle.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        lblTitle.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        lblTitle.MouseUp += (s, e) => { isDragging = false; };
        lblTitle.DoubleClick += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        pnlTitleBar.Controls.Add(lblTitle);
        lblTitle.SendToBack();
        this.Controls.Add(pnlTitleBar);

        // 3. Cargar el archivo
        CargarContenido(filePath);

        // 4. Cerrar con Espacio o Escape
        this.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Escape)
                this.Close();
        };
    }

    private void CargarContenido(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();

        switch (ext)
        {
            // === IMÁGENES ===
            case ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp":
                _pictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
                byte[] bytes = File.ReadAllBytes(filePath);
                using (var ms = new MemoryStream(bytes))
                {
                    _pictureBox.Image = Image.FromStream(ms);
                }
                this.Controls.Add(_pictureBox);
                _pictureBox.BringToFront();
                break;

            // === TEXTO / CÓDIGO ===
            case ".txt" or ".json" or ".xml" or ".cs" or ".csv" or ".log" or ".py" or ".bat" or ".cmd" or ".dat" or ".md" or ".html" or ".css" or ".js":
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
                _richTextBox.Text = fi.Length > 1024 * 1024
                    ? "El archivo es demasiado grande para la vista previa rápida."
                    : File.ReadAllText(filePath);

                this.Controls.Add(_richTextBox);
                _richTextBox.BringToFront();
                break;

            // === PDFs Y WEB ===
            case ".pdf":
                CargarPDFAsync(filePath);
                break;

            // === ARCHIVO NO SOPORTADO ===
            default:
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
                break;
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
            _webView?.CoreWebView2.Navigate(filePath);
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

    private Button CrearBotonSemaforo(Color color, int x, Color backColor)
    {
        Button b = new Button { Name = "btnSemaforo", Location = new Point(x, 0), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(backColor);
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
        };
        return b;
    }
}