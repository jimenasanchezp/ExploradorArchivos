using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppFoto;

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
    private SplitContainer splitMain = default!;
    private PictureBox picPhoto = default!;
    private Panel pnlSidebar = default!;
    private WebView2 webMap = default!;
    private Label lblMetaInfo = default!;
    private Button btnSetLocation = default!;

    // Editor State
    private Point _startPoint;
    private Rectangle _cropRect;
    private bool _isSelecting = false;
    private Color _drawColor = Color.HotPink;
    private float _penWidth = 5f;

    public AppFotoForm(string ruta)
    {
        _rutaFoto = ruta;
        InitializeCustomComponents();
        CargarFoto();
    }

    private void InitializeCustomComponents()
    {
        this.Text = "App Foto - Kawaii Studio";
        this.Size = new Size(1100, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = ThemeRenderer.MainBg;
        this.Icon = SystemIcons.Shield; // Placeholder icon

        // Barra Superior
        pnlTop = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = ThemeRenderer.SecondaryBg };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTop.ClientRectangle, true);
        
        Label lblTitle = new Label { 
            Text = "📸 App Foto Studio", 
            Location = new Point(20, 15), 
            AutoSize = true, 
            Font = new Font("MS Sans Serif", 12, FontStyle.Bold),
            ForeColor = ThemeRenderer.MainText 
        };
        pnlTop.Controls.Add(lblTitle);

        // Botones de edición
        int startX = 250;
        AgregarBotonEditor("↩️ Girar", startX, () => Rotar(RotateFlipType.Rotate270FlipNone));
        AgregarBotonEditor("↪️ Girar", startX + 90, () => Rotar(RotateFlipType.Rotate90FlipNone));
        AgregarBotonEditor("✨ Kawaii", startX + 180, () => AplicarFiltro("Kawaii"));
        AgregarBotonEditor("🎞️ Sepia", startX + 280, () => AplicarFiltro("Sepia"));
        AgregarBotonEditor("🌑 B&N", startX + 380, () => AplicarFiltro("BN"));
        
        // Nuevas herramientas
        AgregarBotonEditor("✂️ Recortar", startX + 480, () => _currentMode = ToolMode.Crop);
        AgregarBotonEditor("✏️ Dibujar", startX + 580, () => _currentMode = ToolMode.Draw);
        AgregarBotonEditor("ABC Texto", startX + 680, () => _currentMode = ToolMode.Text);
        AgregarBotonEditor("🔙 Deshacer", startX + 780, Deshacer);
        AgregarBotonEditor("💾 Guardar", startX + 880, GuardarImagen);

        // Split Container
        splitMain = new SplitContainer { 
            Dock = DockStyle.Fill, 
            SplitterDistance = 750,
            BorderStyle = BorderStyle.Fixed3D 
        };

        // Visor de fotos
        picPhoto = new PictureBox { 
            SizeMode = PictureBoxSizeMode.Zoom, 
            Dock = DockStyle.Fill, 
            BackColor = Color.Transparent,
            Cursor = Cursors.Cross
        };

        // Eventos de ratón para edición
        picPhoto.MouseDown += PicPhoto_MouseDown;
        picPhoto.MouseMove += PicPhoto_MouseMove;
        picPhoto.MouseUp += PicPhoto_MouseUp;
        picPhoto.Paint += PicPhoto_Paint;

        splitMain.Panel1.Controls.Add(picPhoto);

        // Sidebar
        pnlSidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        
        Label lblSidebarTitle = new Label { 
            Text = "📍 Geolocalización", 
            Dock = DockStyle.Top, 
            Font = new Font("MS Sans Serif", 10, FontStyle.Bold),
            Height = 30
        };
        
        lblMetaInfo = new Label { 
            Text = "Cargando metadatos...", 
            Dock = DockStyle.Top, 
            Height = 150,
            Font = new Font("Consolas", 8),
            ForeColor = ThemeRenderer.SecondaryText
        };

        webMap = new WebView2 { 
            Dock = DockStyle.Fill, 
            MinimumSize = new Size(200, 200) 
        };
        webMap.WebMessageReceived += WebMap_WebMessageReceived;

        btnSetLocation = new Button {
            Text = "📍 Registrar Ubicación",
            Dock = DockStyle.Bottom,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Font = new Font("MS Sans Serif", 9, FontStyle.Bold),
            Visible = false
        };
        btnSetLocation.Click += (s, e) => ActivarModoMapaPicker();

        pnlSidebar.Controls.Add(webMap);
        pnlSidebar.Controls.Add(btnSetLocation);
        pnlSidebar.Controls.Add(lblMetaInfo);
        pnlSidebar.Controls.Add(lblSidebarTitle);
        splitMain.Panel2.Controls.Add(pnlSidebar);

        this.Controls.Add(splitMain);
        this.Controls.Add(pnlTop);
    }

    private void AgregarBotonEditor(string texto, int x, Action accion)
    {
        Button btn = new Button {
            Text = texto,
            Location = new Point(x, 15),
            Size = new Size(85, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            Cursor = Cursors.Hand,
            Font = new Font("MS Sans Serif", 8, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => accion();
        btn.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, btn.ClientRectangle, true);
        pnlTop.Controls.Add(btn);
    }

    private async void CargarFoto()
    {
        try
        {
            if (!File.Exists(_rutaFoto)) return;

            _imagenOriginal = Image.FromFile(_rutaFoto);
            AppFotoProcessor.CorrectOrientation(_imagenOriginal); // Corregir orientación aquí
            _imagenActual = (Image)_imagenOriginal.Clone();
            
            ActualizarImagenEnPantalla();

            _metadata = AppFotoExifService.LeerMetadatos(_rutaFoto);
            
            // Actualizar Info Sidebar
            lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" +
                               $"Dimensiones: {_metadata.Dimensiones}\n" +
                               $"Fecha: {(_metadata.FechaCaptura?.ToString() ?? "N/A")}\n" +
                               $"Cámara: {_metadata.ModeloCamara}\n" +
                               $"Latitud: {(_metadata.Latitud?.ToString("F5") ?? "N/A")}\n" +
                               $"Longitud: {(_metadata.Longitud?.ToString("F5") ?? "N/A")}";

            // Inicializar Mapa
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
        var filtrada = AppFotoProcessor.AplicarFiltro(_imagenActual, nombre); // Aplicar sobre el estado actual
        _imagenActual?.Dispose();
        _imagenActual = filtrada;
        ActualizarImagenEnPantalla();
    }

    private void Rotar(RotateFlipType tipo)
    {
        GuardarEstadoParaDeshacer();
        AppFotoProcessor.Rotar(_imagenActual, tipo);
        ActualizarImagenEnPantalla();
    }

    private void GuardarEstadoParaDeshacer()
    {
        // Guardamos un clon de la imagen actual antes de modificarla
        _undoStack.Push((Image)_imagenActual.Clone());
        
        // Limitar la pila a 10 niveles para no saturar la memoria
        if (_undoStack.Count > 10)
        {
            var vieja = _undoStack.ToArray()[_undoStack.Count - 1];
            // En una pila real no es tan fácil quitar el fondo, pero para simplificar:
            // if (_undoStack.Count > 10) _undoStack = new Stack<Image>(_undoStack.Take(10).Reverse());
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
        
        // Cambiar comportamiento del botón para confirmar
        btnSetLocation.Click -= (s, e) => ActivarModoMapaPicker();
        btnSetLocation.Click += ConfirmarUbicacionManual;
    }

    private void ConfirmarUbicacionManual(object? sender, EventArgs e)
    {
        btnSetLocation.Text = "📍 Registrar Ubicación";
        btnSetLocation.BackColor = ThemeRenderer.Accent;
        btnSetLocation.ForeColor = Color.White;
        btnSetLocation.Click -= ConfirmarUbicacionManual;
        btnSetLocation.Click += (s, e) => ActivarModoMapaPicker();
        
        ActualizarInfoMetadata();
        
        // Mostrar la nueva ubicación en el mapa inmediatamente
        if (_metadata.TieneUbicacion)
        {
            string html = AppFotoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value);
            webMap.NavigateToString(html);
            btnSetLocation.Visible = false; // Ocultar pues ya se registró
        }

        MessageBox.Show("Ubicación registrada en la sesión. Haz clic en el botón 'Guardar' (arriba a la derecha) para incrustar estos datos permanentemente en el archivo de la foto.");
    }

    private void WebMap_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            // Recibimos JSON del mapa: {"lat": 12.3, "lng": 45.6}
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

    private Point GetImagePoint(Point mousePoint)
    {
        if (picPhoto.Image == null) return mousePoint;

        float imgAspect = (float)picPhoto.Image.Width / picPhoto.Image.Height;
        float pbAspect = (float)picPhoto.Width / picPhoto.Height;

        float scale;
        float dx = 0, dy = 0;

        if (pbAspect > imgAspect) // PB is wider than image
        {
            scale = (float)picPhoto.Height / picPhoto.Image.Height;
            dx = (picPhoto.Width - picPhoto.Image.Width * scale) / 2;
        }
        else // PB is taller than image
        {
            scale = (float)picPhoto.Width / picPhoto.Image.Width;
            dy = (picPhoto.Height - picPhoto.Image.Height * scale) / 2;
        }

        return new Point(
            (int)((mousePoint.X - dx) / scale),
            (int)((mousePoint.Y - dy) / scale)
        );
    }

    private Point GetMousePoint(Point imagePoint)
    {
        // Operación inversa para dibujar el rectángulo de selección correctamente
        if (picPhoto.Image == null) return imagePoint;

        float imgAspect = (float)picPhoto.Image.Width / picPhoto.Image.Height;
        float pbAspect = (float)picPhoto.Width / picPhoto.Height;

        float scale;
        float dx = 0, dy = 0;

        if (pbAspect > imgAspect)
        {
            scale = (float)picPhoto.Height / picPhoto.Image.Height;
            dx = (picPhoto.Width - picPhoto.Image.Width * scale) / 2;
        }
        else
        {
            scale = (float)picPhoto.Width / picPhoto.Image.Width;
            dy = (picPhoto.Height - picPhoto.Image.Height * scale) / 2;
        }

        return new Point(
            (int)(imagePoint.X * scale + dx),
            (int)(imagePoint.Y * scale + dy)
        );
    }

    // --- INTERACCIONES DE EDICIÓN ---

    private void PicPhoto_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_currentMode == ToolMode.None) return;
        _startPoint = GetImagePoint(e.Location); // Coordenadas en la imagen real
        _isSelecting = true;

        if (_currentMode == ToolMode.Text || _currentMode == ToolMode.Draw || _currentMode == ToolMode.Crop)
        {
            GuardarEstadoParaDeshacer(); // Guardar antes de empezar a dibujar/recortar
        }

        if (_currentMode == ToolMode.Text)
        {
            string txt = Microsoft.VisualBasic.Interaction.InputBox("Escribe el texto:", "Añadir Texto", "¡Kawaii!");
            if (!string.IsNullOrWhiteSpace(txt))
            {
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

        if (_currentMode == ToolMode.Draw)
        {
            AppFotoProcessor.DibujarLinea(_imagenActual, _startPoint, currentImagePoint, _drawColor, _penWidth);
            _startPoint = currentImagePoint;
            picPhoto.Invalidate();
        }
        else if (_currentMode == ToolMode.Crop)
        {
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

        if (_currentMode == ToolMode.Crop && _cropRect.Width > 10)
        {
            if (MessageBox.Show("¿Recortar imagen a esta selección?", "Recortar", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var recortada = AppFotoProcessor.Recortar(_imagenActual, _cropRect);
                _imagenActual.Dispose();
                _imagenActual = recortada;
                ActualizarImagenEnPantalla();
            }
            else
            {
                // Si cancela el recorte, restauramos el estado previo que guardamos en MouseDown
                Deshacer();
            }
            _cropRect = Rectangle.Empty;
            _currentMode = ToolMode.None;
            picPhoto.Invalidate();
        }
    }

    private void PicPhoto_Paint(object? sender, PaintEventArgs e)
    {
        if (_currentMode == ToolMode.Crop && _isSelecting)
        {
            // Dibujar el rectángulo de selección mapeado a la pantalla
            Point p1 = GetMousePoint(_cropRect.Location);
            Point p2 = GetMousePoint(new Point(_cropRect.Right, _cropRect.Bottom));
            Rectangle screenRect = new Rectangle(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y);

            using Pen p = new Pen(Color.White, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(p, screenRect);
            e.Graphics.DrawRectangle(Pens.Black, screenRect);
        }
    }

    private void GuardarImagen()
    {
        using SaveFileDialog sfd = new SaveFileDialog { 
            Filter = "Imagen JPEG|*.jpg|Imagen PNG|*.png", 
            FileName = "Editada_" + _metadata.Nombre 
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            string ext = Path.GetExtension(sfd.FileName).ToLower();
            bool esJpeg = ext == ".jpg" || ext == ".jpeg";

            if (_metadata.TieneUbicacion && esJpeg)
            {
                // Guardar con metadatos GPS (solo soportado en JPEG en este módulo)
                AppFotoProcessor.GuardarConGps(_imagenActual, sfd.FileName, _metadata.Latitud!.Value, _metadata.Longitud!.Value);
                MessageBox.Show($"Imagen guardada con éxito ✨\nIncluyendo coordenadas: {_metadata.Latitud:F5}, {_metadata.Longitud:F5}");
            }
            else
            {
                _imagenActual.Save(sfd.FileName);
                MessageBox.Show("Imagen guardada con éxito ✨" + (_metadata.TieneUbicacion ? "\n(Los datos GPS solo se guardan en formato JPG)" : ""));
            }
        }
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _imagenOriginal?.Dispose();
        _imagenActual?.Dispose();
        base.OnFormClosing(e);
    }
}
