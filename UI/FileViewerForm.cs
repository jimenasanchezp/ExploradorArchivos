using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

/// <summary>
/// Visor y editor ligero de texto integrado. Soporta auto-formateo para JSON, XML y CSV.
/// Incluye interfaz en modo lectura por defecto con opción a edición y guardado.
/// </summary>
public class FileViewerForm : Form
{
    private RichTextBox _richTextBox = default!;
    private string _filePath;
    private bool _isEditMode = false;
    private bool _hasUnsavedChanges = false;
    private bool _isLoading = false;

    private Panel pnlTitleBar = default!;
    private Label lblTitle = default!;
    private Panel pnlToolbar = default!;
    private Button btnEditar = default!;
    private Button btnGuardar = default!;
    private Button btnCancelar = default!;
    private Label lblInfo = default!;

    public FileViewerForm(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        ThemeRenderer.ApplyTheme(this);

        ConfigurarBotonClasico(btnEditar);
        ConfigurarBotonClasico(btnGuardar);
        ConfigurarBotonClasico(btnCancelar);

        LoadFile(filePath);
        ActualizarTitulo();
    }

    private void InitializeComponent()
    {
        this.Text = "Visualizador de Texto 📄";
        this.Size = new Size(900, 700);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.None; // Ventana sin bordes estilo clásico
        this.Padding = new Padding(2); // Espacio para pintar el borde clásico 3D
        this.KeyPreview = true;

        // Pintar el borde clásico en los límites del formulario
        this.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, this.ClientRectangle, true);

        // Title Bar (Personalizada para arrastre y botones de semáforo)
        pnlTitleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 35,
            BackColor = ThemeRenderer.SecondaryBg
        };
        pnlTitleBar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTitleBar.ClientRectangle, true);

        // Arrastrar Ventana desde la barra de título
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTitleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTitleBar.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTitleBar.MouseUp += (s, e) => { isDragging = false; };
        pnlTitleBar.DoubleClick += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        // Panel de Semáforos (Botones estilo clásico macOS en rosa)
        Panel pnlSemaforos = new Panel { Name = "pnlSemaforos", Location = new Point(15, 10), Size = new Size(60, 20), BackColor = Color.Transparent };
        
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0);
        btnClose.Click += (s, e) => this.Close();
        
        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        
        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40);
        btnMax.Click += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        pnlSemaforos.Controls.AddRange(new Control[] { btnClose, btnMin, btnMax });
        pnlTitleBar.Controls.Add(pnlSemaforos);

        // Título del archivo en el Title Bar
        lblTitle = new Label
        {
            Text = "Editor de Texto",
            Location = new Point(90, 8),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ThemeRenderer.SecondaryText
        };
        pnlTitleBar.Controls.Add(lblTitle);

        // Toolbar (Herramientas del editor)
        pnlToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = ThemeRenderer.MainBg
        };
        pnlToolbar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlToolbar.ClientRectangle, true);

        btnEditar = new Button { Text = "✏️  Editar", Location = new Point(15, 12), Size = new Size(110, 30) };
        btnEditar.Click += (s, e) => SetMode(true);

        btnGuardar = new Button { Text = "💾  Guardar", Location = new Point(15, 12), Size = new Size(110, 30), Visible = false };
        btnGuardar.Click += (s, e) => GuardarArchivo();

        btnCancelar = new Button { Text = "❌  Cancelar", Location = new Point(135, 12), Size = new Size(110, 30), Visible = false };
        btnCancelar.Click += (s, e) => {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "¿Deseas descartar los cambios realizados?",
                    "Descartar cambios",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );
                if (result == DialogResult.No) return;
            }
            SetMode(false);
        };

        lblInfo = new Label
        {
            Text = Path.GetFileName(_filePath),
            Location = new Point(260, 18),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
            ForeColor = ThemeRenderer.SecondaryText
        };

        pnlToolbar.Controls.Add(btnEditar);
        pnlToolbar.Controls.Add(btnGuardar);
        pnlToolbar.Controls.Add(btnCancelar);
        pnlToolbar.Controls.Add(lblInfo);

        // Editor
        _richTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 11f),
            ReadOnly = true,
            BackColor = ThemeRenderer.MainBg,
            ForeColor = ThemeRenderer.MainText,
            BorderStyle = BorderStyle.None,
            WordWrap = true
        };

        _richTextBox.TextChanged += (s, e) =>
        {
            if (!_isLoading && _isEditMode)
            {
                _hasUnsavedChanges = true;
                ActualizarTitulo();
            }
        };

        // Captura Ctrl+S para guardar rápidamente
        this.KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true; // Evitar pitido del sistema
                if (_isEditMode)
                {
                    GuardarArchivo();
                }
            }
        };

        // Confirmar salir si hay cambios sin guardar
        this.FormClosing += (s, e) =>
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Tienes cambios sin guardar. ¿Deseas guardarlos antes de salir?",
                    "Cambios sin guardar",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    var guardado = GuardarArchivo();
                    if (!guardado) e.Cancel = true;
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        };

        this.Controls.Add(_richTextBox);
        this.Controls.Add(pnlToolbar);
        this.Controls.Add(pnlTitleBar); // Carga al final para ordenar arriba
    }

    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button b = new Button { Name = "btnSemaforo", Location = new Point(x, 0), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(pnlTitleBar.BackColor);
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
        };
        return b;
    }

    private void ConfigurarBotonClasico(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = ThemeRenderer.MainBg;
        btn.Font = new Font("MS Sans Serif", 9, FontStyle.Bold);
        btn.ForeColor = ThemeRenderer.MainText;

        bool isPressed = false;
        btn.MouseDown += (s, e) => { isPressed = true; btn.Invalidate(); };
        btn.MouseUp += (s, e) => { isPressed = false; btn.Invalidate(); };

        btn.Paint += (s, e) =>
        {
            e.Graphics.Clear(btn.BackColor);
            ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, !isPressed);

            int offset = isPressed ? 1 : 0;
            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font,
                new Rectangle(btn.ClientRectangle.X + offset, btn.ClientRectangle.Y + offset, btn.ClientRectangle.Width, btn.ClientRectangle.Height),
                btn.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }

    private void SetMode(bool editMode)
    {
        _isEditMode = editMode;
        _richTextBox.ReadOnly = !editMode;

        // Visual feedback del modo
        _richTextBox.BackColor = editMode ? Color.White : ThemeRenderer.MainBg;
        _richTextBox.ForeColor = editMode ? Color.Black : ThemeRenderer.MainText;

        btnEditar.Visible = !editMode;
        btnGuardar.Visible = editMode;
        btnCancelar.Visible = editMode;

        if (editMode)
        {
            CargarTextoOriginal();
        }
        else
        {
            LoadFile(_filePath);
            _hasUnsavedChanges = false;
        }
        ActualizarTitulo();
    }

    private void ActualizarTitulo()
    {
        string nombreArchivo = Path.GetFileName(_filePath);
        string asterisco = _hasUnsavedChanges ? "*" : "";
        string modo = _isEditMode ? " [Modo Edición]" : " [Modo Lectura]";
        string fullTitle = $"Editor de Texto - {nombreArchivo}{asterisco}{modo}";
        
        this.Text = fullTitle;
        if (lblTitle != null)
        {
            lblTitle.Text = fullTitle;
        }
    }

    private void CargarTextoOriginal()
    {
        try
        {
            _isLoading = true;
            string content = File.ReadAllText(_filePath);
            _richTextBox.Text = content;
            _isLoading = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar el texto original: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _isLoading = false;
        }
    }

    private bool GuardarArchivo()
    {
        try
        {
            File.WriteAllText(_filePath, _richTextBox.Text);
            _hasUnsavedChanges = false;
            MessageBox.Show("Archivo guardado correctamente.", "Guardar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetMode(false); // Volver al modo lectura (recarga y formatea)
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar el archivo: {ex.Message}", "Error al Guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void LoadFile(string path)
    {
        try
        {
            _isLoading = true;
            string content = File.ReadAllText(path);
            string extension = Path.GetExtension(path).ToLower();

            if (extension == ".json")
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    content = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    lblInfo.Text = $"{Path.GetFileName(_filePath)} [JSON Formateado]";
                }
                catch { /* fallback to plain text */ }
            }
            else if (extension == ".xml")
            {
                try
                {
                    var doc = XDocument.Parse(content);
                    content = doc.ToString();
                    lblInfo.Text = $"{Path.GetFileName(_filePath)} [XML Formateado]";
                }
                catch { /* fallback to plain text */ }
            }
            else if (extension == ".csv")
            {
                try
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var maxLen = lines.Select(l => l.Split(',').Length).Max();
                        var colsWidth = new int[maxLen];
                        var grid = lines.Select(l => l.Split(',')).ToList();

                        foreach (var row in grid)
                            for (int i = 0; i < row.Length; i++)
                                colsWidth[i] = Math.Max(colsWidth[i], row[i].Trim().Length);

                        var sb = new System.Text.StringBuilder();
                        foreach (var row in grid)
                        {
                            for (int i = 0; i < row.Length; i++)
                                sb.Append(row[i].Trim().PadRight(colsWidth[i] + 4));
                            sb.AppendLine();
                        }
                        content = sb.ToString();
                        lblInfo.Text = $"{Path.GetFileName(_filePath)} [CSV Formateado]";
                    }
                }
                catch { /* fallback to plain text */ }
            }
            else if (extension == ".md")
            {
                lblInfo.Text = $"{Path.GetFileName(_filePath)} [Markdown]";
            }
            else
            {
                lblInfo.Text = Path.GetFileName(_filePath);
            }

            _richTextBox.Text = content;
            _isLoading = false;
        }
        catch (Exception ex)
        {
            _richTextBox.Text = $"Error al leer el archivo: {ex.Message}";
            _richTextBox.ForeColor = Color.Salmon;
            _isLoading = false;
        }
    }
}
