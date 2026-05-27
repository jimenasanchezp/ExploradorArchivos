using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using ExploradorArchivos.UI;
using ExploradorArchivos.Mp3; // Para CustomTrackBar si es necesario
using Microsoft.Web.WebView2.WinForms;

namespace ExploradorArchivos.AppVideo;

public partial class AppVideoForm : Form
{
    private readonly string _rutaVideo;
    private AppVideoMetadata _metadata = default!;
    
    // VLC
    private LibVLC _libVLC = null!;
    private LibVLCSharp.Shared.MediaPlayer _mediaPlayer = null!;
    private VideoView _videoView = null!;
    
    // UI
    private Panel pnlTop = default!;
    private Panel pnlSidebar = default!;
    private Label lblMetaInfo = default!;
    private CustomTrackBar _trackProgreso = null!;
    private Label lblTiempoFin = null!;
    private Button _btnPlayPause = null!;
    private System.Windows.Forms.Timer _timerUI = null!;
    private float? _pendingPosition = null;
    
    // Geolocalización
    private WebView2 webMap = default!;
    private Button btnSetLocation = default!;

    public AppVideoForm(string ruta)
    {
        _rutaVideo = ruta;
        InitializeCustomComponents();
        InicializarVLC();
        CargarMetadatos();
    }

    private void InitializeCustomComponents()
    {
        ThemeRenderer.ApplyTheme(this);
        this.Text = "App Video - Studio";
        this.Size = new Size(1100, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None;

        // Barra Superior (80px de padding izquierdo para los semáforos)
        pnlTop = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = ThemeRenderer.SecondaryBg, Padding = new Padding(80, 0, 0, 0) };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTop.ClientRectangle, true);
        
        // Arrastrar Ventana
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTop.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTop.MouseUp += (s, e) => { isDragging = false; };

        ConfigurarSemaforos();
        
        Label lblTitle = new Label { 
            Text = "🎬 App Video Studio", 
            Location = new Point(10, 22), // Ahora es relativo al padding de pnlTop
            AutoSize = true, 
            Font = new Font("MS Sans Serif", 10, FontStyle.Bold),
            ForeColor = ThemeRenderer.MainText 
        };
        pnlTop.Controls.Add(lblTitle);

        // Botones de edición (startX relativo al padding si fuera un FlowPanel, pero aquí es absoluto)
        // Ajustamos startX para que no pise el título
        int startX = 280; 
        AgregarBotonEditor("✨ Soft", startX, () => AplicarFiltro("Soft"));
        AgregarBotonEditor("🎞️ Sepia", startX + 100, () => AplicarFiltro("Sepia"));
        AgregarBotonEditor("🌑 B&N", startX + 200, () => AplicarFiltro("BN"));
        AgregarBotonEditor("🎵 Audio", startX + 300, ExtraerAudio);

        // Main Layout
        SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 800 };
        
        // Video Player Area
        Panel pnlPlayer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        
        Panel pnlVideoBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
        pnlVideoBorder.Paint += (s, e) => {
            using Pen p = new Pen(ThemeRenderer.Accent, 2);
            e.Graphics.DrawRectangle(p, 0, 0, pnlVideoBorder.Width - 1, pnlVideoBorder.Height - 1);
        };

        _videoView = new VideoView { Dock = DockStyle.Fill, BackColor = Color.Black };
        pnlVideoBorder.Controls.Add(_videoView);
        
        // Controles Inferiores
        Panel pnlControls = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = Color.Transparent };
        _trackProgreso = new CustomTrackBar { Dock = DockStyle.Top, Height = 20 };
        _trackProgreso.ValueChangedByUser += (val) => { 
            if(_mediaPlayer != null) 
            {
                if (_mediaPlayer.State == VLCState.Ended || _mediaPlayer.State == VLCState.Stopped)
                {
                    _pendingPosition = (float)val;
                    ReiniciarReproductor();
                }
                else
                {
                    _mediaPlayer.Position = (float)val; 
                }
            }
        };
        
        _btnPlayPause = new Button { 
            Text = "▶", 
            Size = new Size(50, 40), 
            Location = new Point(20, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White
        };
        _btnPlayPause.Click += (s, e) => TogglePlayPause();

        lblTiempoFin = new Label { 
            Text = "00:00:00", 
            Location = new Point(80, 40),
            AutoSize = true,
            Font = new Font("Consolas", 10),
            ForeColor = ThemeRenderer.Accent
        };

        pnlControls.Controls.Add(_btnPlayPause);
        pnlControls.Controls.Add(lblTiempoFin);
        pnlControls.Controls.Add(_trackProgreso);

        pnlPlayer.Controls.Add(pnlVideoBorder);
        pnlPlayer.Controls.Add(pnlControls);
        split.Panel1.Controls.Add(pnlPlayer);

        // Sidebar
        pnlSidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), BackColor = ThemeRenderer.SecondaryBg };
        Label lblMetaTitle = new Label { Text = "📊 Metadatos", Dock = DockStyle.Top, Font = new Font("MS Sans Serif", 10, FontStyle.Bold), Height = 30 };
        lblMetaInfo = new Label { Dock = DockStyle.Top, Height = 140, Font = new Font("Consolas", 9), ForeColor = ThemeRenderer.SecondaryText };
        
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

        pnlSidebar.Controls.Add(webMap);
        pnlSidebar.Controls.Add(btnSetLocation);
        pnlSidebar.Controls.Add(lblMetaInfo);
        pnlSidebar.Controls.Add(lblMetaTitle);
        split.Panel2.Controls.Add(pnlSidebar);

        this.Controls.Add(split);
        this.Controls.Add(pnlTop);

        _timerUI = new System.Windows.Forms.Timer { Interval = 500 };
        _timerUI.Tick += (s, e) => {
            if (_mediaPlayer != null)
            {
                if (_mediaPlayer.IsPlaying)
                    _trackProgreso.Value = _mediaPlayer.Position;
                
                _btnPlayPause.Text = _mediaPlayer.IsPlaying ? "⏸" : "▶";
            }
        };
    }

    private void AgregarBotonEditor(string texto, int x, Action accion)
    {
        Button btn = new Button {
            Text = texto,
            Location = new Point(x, 15),
            Size = new Size(90, 35),
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

    private void InicializarVLC()
    {
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        _videoView.MediaPlayer = _mediaPlayer;
        
        _mediaPlayer.Playing += (s, e) => {
            if (_pendingPosition.HasValue)
            {
                // We use a small delay or set it directly. Direct set usually works in Playing event.
                _mediaPlayer.Position = _pendingPosition.Value;
                _pendingPosition = null;
            }
        };
        
        using var media = new Media(_libVLC, _rutaVideo, FromType.FromPath);
        _mediaPlayer.Play(media);
        
        _mediaPlayer.LengthChanged += (s, e) => {
            if (this.IsHandleCreated)
            {
                this.BeginInvoke(new Action(() => {
                    lblTiempoFin.Text = TimeSpan.FromMilliseconds(e.Length).ToString(@"hh\:mm\:ss");
                }));
            }
        };

        _timerUI.Start();
    }

    private void CargarMetadatos()
    {
        CargarMetadatosAsync();
    }

    private async void CargarMetadatosAsync()
    {
        _metadata = AppVideoProcessor.ObtenerMetadataManual(_rutaVideo);
        ActualizarInfoMetadata();

        try
        {
            await webMap.EnsureCoreWebView2Async();
            if (_metadata.TieneUbicacion)
            {
                string html = AppVideoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value);
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
            Console.WriteLine($"Error al cargar mapa: {ex.Message}");
        }
    }

    private void ActualizarInfoMetadata()
    {
        lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" +
                           $"Ext: {_metadata.Extension}\n" +
                           $"Tamaño: {(_metadata.TamanoBytes / 1024.0 / 1024.0):F2} MB\n" +
                           $"Resolución: {_metadata.Resolucion}\n" +
                           $"Codec: {_metadata.Codec}\n" +
                           $"Latitud: {(_metadata.Latitud?.ToString("F5") ?? "N/A")}\n" +
                           $"Longitud: {(_metadata.Longitud?.ToString("F5") ?? "N/A")}";
    }

    private void ActivarModoMapaPicker()
    {
        webMap.NavigateToString(AppVideoMapService.GenerarMapaPickerHtml());
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
            AppVideoProcessor.GuardarMetadata(_metadata);
            string html = AppVideoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value);
            webMap.NavigateToString(html);
            btnSetLocation.Visible = false;
        }
        MessageBox.Show("Ubicación registrada y guardada.");
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

    private void TogglePlayPause()
    {
        if (_mediaPlayer.State == VLCState.Ended || _mediaPlayer.State == VLCState.Stopped)
        {
            ReiniciarReproductor();
            return;
        }

        if (_mediaPlayer.IsPlaying) _mediaPlayer.Pause();
        else _mediaPlayer.Play();
        _btnPlayPause.Text = _mediaPlayer.IsPlaying ? "⏸" : "▶";
    }

    private async void AplicarFiltro(string nombre)
    {
        PrepararParaProcesar();
        string output = Path.Combine(Path.GetDirectoryName(_rutaVideo)!, "Filtro_" + Path.GetFileName(_rutaVideo));
        
        lblMetaInfo.Text = "\nPROCESANDO FILTRO...\nPor favor espera.";
        bool ok = await AppVideoProcessor.AplicarFiltro(_rutaVideo, output, nombre);
        
        if (ok) MessageBox.Show("Filtro aplicado con éxito: " + output);
        else MessageBox.Show("Error al aplicar filtro. Asegúrate de que ffmpeg.exe esté en la carpeta de la app.");
        
        CargarMetadatos();
        ReiniciarReproductor();
    }

    private async void ExtraerAudio()
    {
        PrepararParaProcesar();
        string output = Path.Combine(Path.GetDirectoryName(_rutaVideo)!, Path.GetFileNameWithoutExtension(_rutaVideo) + ".mp3");
        bool ok = await AppVideoProcessor.ExtraerAudio(_rutaVideo, output);
        
        if (ok) MessageBox.Show("Audio extraído: " + output);
        else MessageBox.Show("Error al extraer audio. Asegúrate de que ffmpeg.exe esté en la carpeta de la app.");
        
        ReiniciarReproductor();
    }

    private void PrepararParaProcesar()
    {
        _mediaPlayer.Stop();
        // Forzamos a que VLC suelte el archivo asignando un medio nulo o vacío
        _mediaPlayer.Media = null;
    }

    private void ReiniciarReproductor()
    {
        using var media = new Media(_libVLC, _rutaVideo, FromType.FromPath);
        _mediaPlayer.Play(media);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timerUI.Stop();
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.OnFormClosing(e);
    }
}
