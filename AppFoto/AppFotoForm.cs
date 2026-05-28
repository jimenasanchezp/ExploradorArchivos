using ExploradorArchivos.Mp3;
using ExploradorArchivos.UI;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ExploradorArchivos.AppFoto;

/// <summary>
/// Interfaz gráfica principal del módulo "App Foto".
/// Soporta visualización con zoom dinámico, edición (recorte, dibujo, filtros), 
/// ajuste en tiempo real (brillo, contraste) y gestión de metadatos GPS a través de un mapa integrado.
/// </summary>
public partial class AppFotoForm : Form
{
    private enum ToolMode { None, Draw, Text, Crop }
    private ToolMode _currentMode = ToolMode.None;
    private Stack<Image> _undoStack = new Stack<Image>();

    private readonly string _rutaFoto;
    private AppFotoMetadata _metadata = default!;
    private Image _imagenOriginal = default!;
    private Image _imagenActual = default!;

    // UI Controls
    private Panel pnlTop = default!;
    private FlowLayoutPanel flowButtons = default!;
    private SplitContainer splitMain = default!;
    private PictureBox picPhoto = default!;
    private TabControl tabSidebar = default!;
    private TabPage tabAjustes = default!;
    private TabPage tabInfo = default!;
    private WebView2 webMap = default!;
    private Label lblMetaInfo = default!;
    private Button btnSetLocation = default!;

    // Ajustes Sliders
    private TrackBar trkBrillo = default!;
    private TrackBar trkContraste = default!;
    private TrackBar trkSaturacion = default!;
    private TrackBar trkLuces = default!;
    private TrackBar trkSombras = default!;

    // Editor State
    private Point _startPoint;
    private Rectangle _cropRect;
    private bool _isSelecting = false;
    private Color _drawColor = Color.HotPink;
    private float _penWidth = 5f;
    private bool _isAdjusting = false;

    public AppFotoForm(string ruta)
    {
        _rutaFoto = ruta;
        InitializeCustomComponents();
        CargarFoto();
    }

    /// <summary>
    /// Configura dinámicamente todos los controles de la interfaz (botones, sliders, lienzo)
    /// omitiendo el uso del diseñador visual de Visual Studio en favor de código procedimental escalable.
    /// </summary>
    private void InitializeCustomComponents()
    {
        ThemeRenderer.ApplyTheme(this);
        this.Text = "App Foto - Premium Studio";
        this.Size = new Size(1200, 800);
        this.MinimumSize = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None;

        // Barra Superior Responsiva (80px de padding izquierdo para los semáforos)
        pnlTop = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = ThemeRenderer.SecondaryBg, Padding = new Padding(80, 10, 10, 10) };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTop.ClientRectangle, true);
        
        // Arrastrar Ventana
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTop.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTop.MouseUp += (s, e) => { isDragging = false; };

        ConfigurarSemaforos();

        flowButtons = new FlowLayoutPanel { 
            Dock = DockStyle.Fill, 
            FlowDirection = FlowDirection.LeftToRight, 
            WrapContents = true,
            AutoScroll = true
        };

        AgregarBotonEditor("✂️ Recorte", () => _currentMode = ToolMode.Crop);
        AgregarBotonEditor("↩️ Giro L", () => Rotar(RotateFlipType.Rotate270FlipNone));
        AgregarBotonEditor("↪️ Giro R", () => Rotar(RotateFlipType.Rotate90FlipNone));
        AgregarBotonEditor("🌑 B&N", () => AplicarFiltro("BN"));
        AgregarBotonEditor("✏️ Dibujar", () => _currentMode = ToolMode.Draw);
        AgregarBotonEditor("ABC Texto", () => _currentMode = ToolMode.Text);
        AgregarBotonEditor("🔙 Deshacer", Deshacer);
        AgregarBotonEditor("💾 Guardar", GuardarImagen);

        pnlTop.Controls.Add(flowButtons);

        // Split Container
        splitMain = new SplitContainer { 
            Dock = DockStyle.Fill, 
            SplitterDistance = 850,
            BorderStyle = BorderStyle.None 
        };

        // Visor de fotos
        picPhoto = new PictureBox { 
            SizeMode = PictureBoxSizeMode.Zoom, 
            Dock = DockStyle.Fill, 
            BackColor = Color.FromArgb(40, 40, 40),
            Cursor = Cursors.Cross
        };
        picPhoto.MouseDown += PicPhoto_MouseDown;
        picPhoto.MouseMove += PicPhoto_MouseMove;
        picPhoto.MouseUp += PicPhoto_MouseUp;
        picPhoto.Paint += PicPhoto_Paint;

        splitMain.Panel1.Controls.Add(picPhoto);

        // Sidebar con Tabs
        tabSidebar = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
        tabAjustes = new TabPage("🎨 Ajustes");
        tabInfo = new TabPage("📍 Info/Mapa");
        
        tabAjustes.BackColor = ThemeRenderer.SecondaryBg;
        tabInfo.BackColor = ThemeRenderer.SecondaryBg;
        
        ConfigurarPanelAjustes();
        ConfigurarPanelInfo();

        tabSidebar.TabPages.Add(tabAjustes);
        tabSidebar.TabPages.Add(tabInfo);
        splitMain.Panel2.Controls.Add(tabSidebar);

        this.Controls.Add(splitMain);
        this.Controls.Add(pnlTop);
    }

    /// <summary>
    /// Construye dinámicamente el panel de ajustes de imagen (brillo, contraste, saturación)
    /// utilizando un diseño basado en <c>TableLayoutPanel</c> para alineación automática.
    /// </summary>
    private void ConfigurarPanelAjustes()
    {
        TableLayoutPanel tlp = new TableLayoutPanel { 
            Dock = DockStyle.Top, 
            ColumnCount = 1, 
            RowCount = 12, 
            AutoSize = true, 
            Padding = new Padding(10) 
        };

        trkBrillo = CrearSliderAjuste(tlp, "☀️ Brillo", -100, 100, 0);
        trkContraste = CrearSliderAjuste(tlp, "🌗 Contraste", 0, 200, 100);
        trkSaturacion = CrearSliderAjuste(tlp, "🌈 Saturación", 0, 200, 100);
        trkLuces = CrearSliderAjuste(tlp, "💡 Luces", -100, 100, 0);
        trkSombras = CrearSliderAjuste(tlp, "🌑 Sombras", -100, 100, 0);

        Button btnReset = new Button { 
            Text = "🔄 Resetear Ajustes", 
            Dock = DockStyle.Top, 
            Height = 40, 
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Margin = new Padding(0, 20, 0, 0)
        };
        btnReset.Click += (s, e) => ResetearAjustes();
        tlp.Controls.Add(btnReset);

        tabAjustes.Controls.Add(tlp);
    }

    private TrackBar CrearSliderAjuste(TableLayoutPanel parent, string nombre, int min, int max, int val)
    {
        Label lbl = new Label { Text = nombre, AutoSize = true, Margin = new Padding(0, 10, 0, 0), Font = new Font("MS Sans Serif", 8, FontStyle.Bold) };
        TrackBar trk = new TrackBar { Minimum = min, Maximum = max, Value = val, TickStyle = TickStyle.None, Dock = DockStyle.Top, Height = 30 };
        
        trk.ValueChanged += (s, e) => AplicarAjustesTiempoReal();
        trk.MouseDown += (s, e) => { if (!_isAdjusting) { GuardarEstadoParaDeshacer(); _isAdjusting = true; } };
        trk.MouseUp += (s, e) => { _isAdjusting = false; };

        parent.Controls.Add(lbl);
        parent.Controls.Add(trk);
        return trk;
    }

    /// <summary>
    /// Configura la pestaña de información, incrustando el control <c>WebView2</c> para mostrar el mapa
    /// interactivo de la ubicación y el panel de etiquetas con metadatos de la cámara.
    /// </summary>
    private void ConfigurarPanelInfo()
    {
        Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        
        lblMetaInfo = new Label { Dock = DockStyle.Top, Height = 180, Font = new Font("Consolas", 8), ForeColor = ThemeRenderer.SecondaryText };
        webMap = new WebView2 { Dock = DockStyle.Fill, MinimumSize = new Size(150, 150) };
        webMap.WebMessageReceived += WebMap_WebMessageReceived;

        btnSetLocation = new Button {
            Text = "📍 Registrar Ubicación",
            Dock = DockStyle.Bottom,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Visible = false
        };
        btnSetLocation.Click += (s, e) => ActivarModoMapaPicker();

        pnl.Controls.Add(webMap);
        pnl.Controls.Add(btnSetLocation);
        pnl.Controls.Add(lblMetaInfo);
        tabInfo.Controls.Add(pnl);
    }

    private void AgregarBotonEditor(string texto, Action accion)
    {
        Button btn = new Button {
            Text = texto,
            Size = new Size(100, 45),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            Cursor = Cursors.Hand,
            Font = new Font("MS Sans Serif", 8, FontStyle.Bold),
            Margin = new Padding(5)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => accion();
        btn.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, true);
        flowButtons.Controls.Add(btn);
    }

    private void ConfigurarSemaforos()
    {
        Panel pnlSemaforos = new Panel { Location = new Point(10, 25), Size = new Size(60, 20), BackColor = Color.Transparent };
        
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0);
        btnClose.Click += (s, e) => this.Close();
        
        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        
        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40);
        btnMax.Click += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        pnlSemaforos.Controls.AddRange(new Control[] { btnClose, btnMin, btnMax });
        pnlTop.Controls.Add(pnlSemaforos);
    }

    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button b = new Button { Location = new Point(x, 2), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(pnlTop.BackColor);
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
        };
        return b;
    }

    /// <summary>
    /// Lee la imagen desde el disco de manera segura, aplica correcciones de rotación EXIF
    /// e inicializa el mapa interactivo (<c>WebView2</c>) basándose en los metadatos geográficos.
    /// </summary>
    private async void CargarFoto()
    {
        try
        {
            if (!File.Exists(_rutaFoto)) return;

            _imagenOriginal = Image.FromFile(_rutaFoto);
            AppFotoProcessor.CorrectOrientation(_imagenOriginal);
            _imagenActual = (Image)_imagenOriginal.Clone();
            _undoStack.Push((Image)_imagenActual.Clone());
            
            ActualizarImagenEnPantalla();

            _metadata = AppFotoExifService.LeerMetadatos(_rutaFoto);
            
            lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" +
                               $"Dimensiones: {_metadata.Dimensiones}\n" +
                               $"Fecha: {(_metadata.FechaCaptura?.ToString() ?? "N/A")}\n" +
                               $"Cámara: {_metadata.ModeloCamara}\n" +
                               $"Latitud: {(_metadata.Latitud?.ToString("F5") ?? "N/A")}\n" +
                               $"Longitud: {(_metadata.Longitud?.ToString("F5") ?? "N/A")}";

            await webMap.EnsureCoreWebView2Async();
            if (_metadata.TieneUbicacion)
            {
                string html = AppFotoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value);
                webMap.NavigateToString(html);
                btnSetLocation.Visible = false;
            }
            else
            {
                webMap.NavigateToString("<html><body style='background:#f9f9f9; display:flex; justify-content:center; align-items:center; height:100vh; font-family:sans-serif; text-align:center;'><div><h3>No hay datos de GPS 📍</h3><p style='font-size:12px; color:#666;'>Usa el botón de abajo para registrar la ubicación manualmente.</p></div></body></html>");
                btnSetLocation.Visible = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando la foto: {ex.Message}");
        }
    }

    private void AplicarFiltro(string nombre)
    {
        GuardarEstadoParaDeshacer();
        var filtrada = AppFotoProcessor.AplicarFiltro(_imagenActual, nombre);
        _imagenActual?.Dispose();
        _imagenActual = filtrada;
        ActualizarImagenEnPantalla();
    }

    /// <summary>
    /// Aplica instantáneamente los valores combinados de los sliders (brillo, contraste, luces)
    /// utilizando la pila de deshacer para no degradar la imagen en múltiples pasadas.
    /// </summary>
    private void AplicarAjustesTiempoReal()
    {
        if (_undoStack.Count == 0) return;
        var filtrada = AppFotoProcessor.AjustarImagen(_undoStack.Peek(), 
            trkBrillo.Value, trkContraste.Value, trkSaturacion.Value, trkLuces.Value, trkSombras.Value);
        
        _imagenActual?.Dispose();
        _imagenActual = filtrada;
        picPhoto.Image = _imagenActual;
    }

    private void ResetearAjustes()
    {
        trkBrillo.Value = 0;
        trkContraste.Value = 100;
        trkSaturacion.Value = 100;
        trkLuces.Value = 0;
        trkSombras.Value = 0;
        AplicarAjustesTiempoReal();
    }

    private void Rotar(RotateFlipType tipo)
    {
        GuardarEstadoParaDeshacer();
        AppFotoProcessor.Rotar(_imagenActual, tipo);
        ActualizarImagenEnPantalla();
    }

    private void GuardarEstadoParaDeshacer()
    {
        _undoStack.Push((Image)_imagenActual.Clone());
        if (_undoStack.Count > 15)
        {
             // Limpieza básica si es necesario
        }
    }

    private void Deshacer()
    {
        if (_undoStack.Count > 0)
        {
            _imagenActual?.Dispose();
            _imagenActual = _undoStack.Pop();
            ActualizarImagenEnPantalla();
        }
        else
        {
            MessageBox.Show("No hay más cambios para deshacer.");
        }
    }

    private void ActualizarImagenEnPantalla()
    {
        picPhoto.Image = _imagenActual;
    }

    private void ActivarModoMapaPicker()
    {
        webMap.NavigateToString(AppFotoMapService.GenerarMapaPickerHtml());
        btnSetLocation.Text = "✅ Guardar Ubicación";
        btnSetLocation.BackColor = Color.LightGreen;
        btnSetLocation.ForeColor = Color.Black;
        btnSetLocation.Click -= ConfirmarUbicacionManual;
        btnSetLocation.Click += ConfirmarUbicacionManual;
    }

    private void ConfirmarUbicacionManual(object? sender, EventArgs e)
    {
        btnSetLocation.Text = "📍 Registrar Ubicación";
        btnSetLocation.BackColor = ThemeRenderer.Accent;
        btnSetLocation.ForeColor = Color.White;
        btnSetLocation.Click -= ConfirmarUbicacionManual;
        
        ActualizarInfoMetadata();
        if (_metadata.TieneUbicacion)
        {
            string html = AppFotoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value);
            webMap.NavigateToString(html);
            btnSetLocation.Visible = false;
        }
        MessageBox.Show("Ubicación registrada. Haz clic en 'Guardar' para aplicar permanentemente.");
    }

    private void WebMap_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.WebMessageAsJson;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            _metadata.Latitud = doc.RootElement.GetProperty("lat").GetDouble();
            _metadata.Longitud = doc.RootElement.GetProperty("lng").GetDouble();
        }
        catch { }
    }

    private void ActualizarInfoMetadata()
    {
        lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" +
                           $"Dimensiones: {_metadata.Dimensiones}\n" +
                           $"Fecha: {(_metadata.FechaCaptura?.ToString() ?? "N/A")}\n" +
                           $"Cámara: {_metadata.ModeloCamara}\n" +
                           $"Latitud: {(_metadata.Latitud?.ToString("F5") ?? "N/A")}\n" +
                           $"Longitud: {(_metadata.Longitud?.ToString("F5") ?? "N/A")}";
    }

    /// <summary>
    /// Convierte coordenadas de la pantalla (mouse) a las coordenadas reales del píxel 
    /// en la imagen original, tomando en cuenta el nivel de zoom y el letterboxing del <c>PictureBox</c>.
    /// </summary>
    private Point GetImagePoint(Point mousePoint)
    {
        if (picPhoto.Image == null) return mousePoint;
        float imgAspect = (float)picPhoto.Image.Width / picPhoto.Image.Height;
        float pbAspect = (float)picPhoto.Width / picPhoto.Height;
        float scale;
        float dx = 0, dy = 0;
        if (pbAspect > imgAspect) {
            scale = (float)picPhoto.Height / picPhoto.Image.Height;
            dx = (picPhoto.Width - picPhoto.Image.Width * scale) / 2;
        } else {
            scale = (float)picPhoto.Width / picPhoto.Image.Width;
            dy = (picPhoto.Height - picPhoto.Image.Height * scale) / 2;
        }
        return new Point((int)((mousePoint.X - dx) / scale), (int)((mousePoint.Y - dy) / scale));
    }

    /// <summary>
    /// Operación inversa a <see cref="GetImagePoint"/>. Convierte coordenadas del píxel real de la imagen
    /// a las coordenadas visuales en pantalla para dibujar el rectángulo de selección de recorte interactivo.
    /// </summary>
    private Point GetMousePoint(Point imagePoint)
    {
        if (picPhoto.Image == null) return imagePoint;
        float imgAspect = (float)picPhoto.Image.Width / picPhoto.Image.Height;
        float pbAspect = (float)picPhoto.Width / picPhoto.Height;
        float scale;
        float dx = 0, dy = 0;
        if (pbAspect > imgAspect) {
            scale = (float)picPhoto.Height / picPhoto.Image.Height;
            dx = (picPhoto.Width - picPhoto.Image.Width * scale) / 2;
        } else {
            scale = (float)picPhoto.Width / picPhoto.Image.Width;
            dy = (picPhoto.Height - picPhoto.Image.Height * scale) / 2;
        }
        return new Point((int)(imagePoint.X * scale + dx), (int)(imagePoint.Y * scale + dy));
    }

    private void PicPhoto_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_currentMode == ToolMode.None) return;
        _startPoint = GetImagePoint(e.Location);
        _isSelecting = true;
        if (_currentMode == ToolMode.Text || _currentMode == ToolMode.Draw || _currentMode == ToolMode.Crop)
            GuardarEstadoParaDeshacer();
        if (_currentMode == ToolMode.Text) {
            string txt = Microsoft.VisualBasic.Interaction.InputBox("Escribe el texto:", "Añadir Texto", "¡Hola!");
            if (!string.IsNullOrWhiteSpace(txt)) {
                AppFotoProcessor.DibujarTexto(_imagenActual, txt, _startPoint, new Font("Segoe UI", 24, FontStyle.Bold), _drawColor);
                picPhoto.Invalidate();
            }
            _isSelecting = false;
        }
    }

    private void PicPhoto_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;
        Point currentImagePoint = GetImagePoint(e.Location);
        if (_currentMode == ToolMode.Draw) {
            AppFotoProcessor.DibujarLinea(_imagenActual, _startPoint, currentImagePoint, _drawColor, _penWidth);
            _startPoint = currentImagePoint;
            picPhoto.Invalidate();
        } else if (_currentMode == ToolMode.Crop) {
            int x = Math.Min(_startPoint.X, currentImagePoint.X);
            int y = Math.Min(_startPoint.Y, currentImagePoint.Y);
            int w = Math.Abs(_startPoint.X - currentImagePoint.X);
            int h = Math.Abs(_startPoint.Y - currentImagePoint.Y);
            _cropRect = new Rectangle(x, y, w, h);
            picPhoto.Invalidate();
        }
    }

    private void PicPhoto_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;
        if (_currentMode == ToolMode.Crop && _cropRect.Width > 10) {
            if (MessageBox.Show("¿Recortar?", "Recortar", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                var recortada = AppFotoProcessor.Recortar(_imagenActual, _cropRect);
                _imagenActual.Dispose();
                _imagenActual = recortada;
                ActualizarImagenEnPantalla();
            } else { Deshacer(); }
            _cropRect = Rectangle.Empty;
            _currentMode = ToolMode.None;
            picPhoto.Invalidate();
        }
    }

    private void PicPhoto_Paint(object? sender, PaintEventArgs e)
    {
        if (_currentMode == ToolMode.Crop && _isSelecting) {
            Point p1 = GetMousePoint(_cropRect.Location);
            Point p2 = GetMousePoint(new Point(_cropRect.Right, _cropRect.Bottom));
            Rectangle screenRect = new Rectangle(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y);
            using Pen p = new Pen(Color.White, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(p, screenRect);
            e.Graphics.DrawRectangle(Pens.Black, screenRect);
        }
    }

    /// <summary>
    /// Exporta la imagen actual al disco. Si la imagen contiene datos GPS y se guarda en JPEG,
    /// inyecta asíncronamente las etiquetas de geolocalización dentro del nuevo archivo.
    /// </summary>
    private void GuardarImagen()
    {
        using SaveFileDialog sfd = new SaveFileDialog { Filter = "Imagen JPEG|*.jpg|Imagen PNG|*.png", FileName =  _metadata.Nombre };
        if (sfd.ShowDialog() == DialogResult.OK) {
            string ext = Path.GetExtension(sfd.FileName).ToLower();
            if (_metadata.TieneUbicacion && (ext == ".jpg" || ext == ".jpeg")) {
                AppFotoProcessor.GuardarConGps(_imagenActual, sfd.FileName, _metadata.Latitud!.Value, _metadata.Longitud!.Value);
            } else { _imagenActual.Save(sfd.FileName); }
            MessageBox.Show("Imagen guardada ✨");
        }
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _imagenOriginal?.Dispose();
        _imagenActual?.Dispose();
        base.OnFormClosing(e);
    }
}
