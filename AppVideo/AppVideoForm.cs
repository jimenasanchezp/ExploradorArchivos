using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using ExploradorArchivos.UI;
using ExploradorArchivos.Mp3; // Para CustomTrackBar si es necesario

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
    private CustomTrackBar trackInicio = null!;
    private CustomTrackBar trackFin = null!;
    private Label lblTiempoInicio = null!;
    private Label lblTiempoFin = null!;
    private Button _btnPlayPause = null!;
    private System.Windows.Forms.Timer _timerUI = null!;

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
        AgregarBotonEditor("✂️ Recortar", startX, () => MostrarDialogoRecorte());
        AgregarBotonEditor("✨ Soft", startX + 100, () => AplicarFiltro("Soft"));
        AgregarBotonEditor("🎞️ Sepia", startX + 200, () => AplicarFiltro("Sepia"));
        AgregarBotonEditor("🌑 B&N", startX + 300, () => AplicarFiltro("BN"));
        AgregarBotonEditor("🎵 Audio", startX + 400, ExtraerAudio);

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
        _trackProgreso.ValueChangedByUser += (val) => { if(_mediaPlayer != null) _mediaPlayer.Position = (float)val; };
        
        _btnPlayPause = new Button { 
            Text = "▶", 
            Size = new Size(50, 40), 
            Location = new Point(20, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White
        };
        _btnPlayPause.Click += (s, e) => TogglePlayPause();

        pnlControls.Controls.Add(_btnPlayPause);
        pnlControls.Controls.Add(_trackProgreso);

        pnlPlayer.Controls.Add(pnlVideoBorder);
        pnlPlayer.Controls.Add(pnlControls);
        split.Panel1.Controls.Add(pnlPlayer);

        // Sidebar
        pnlSidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), BackColor = ThemeRenderer.SecondaryBg };
        Label lblMetaTitle = new Label { Text = "📊 Metadatos", Dock = DockStyle.Top, Font = new Font("MS Sans Serif", 10, FontStyle.Bold), Height = 30 };
        lblMetaInfo = new Label { Dock = DockStyle.Top, Height = 120, Font = new Font("Consolas", 9), ForeColor = ThemeRenderer.SecondaryText };
        
        // Controles de Recorte en Sidebar
        Label lblRecorteTitle = new Label { Text = "✂️ Selección de Recorte", Dock = DockStyle.Top, Font = new Font("MS Sans Serif", 10, FontStyle.Bold), Height = 30, Margin = new Padding(0, 20, 0, 0) };
        
        Label lblStart = new Label { Text = "Inicio:", Dock = DockStyle.Top, Height = 20 };
        lblTiempoInicio = new Label { Text = "00:00:00", Dock = DockStyle.Top, Height = 20, Font = new Font("Consolas", 9), ForeColor = ThemeRenderer.Accent };
        trackInicio = new CustomTrackBar { Dock = DockStyle.Top, Height = 25, Value = 0 };
        trackInicio.ValueChangedByUser += (val) => { 
            lblTiempoInicio.Text = FormatearTiempoDesdePorcentaje(val);
            if (val > trackFin.Value) trackFin.Value = val; 
        };

        Label lblEnd = new Label { Text = "Fin:", Dock = DockStyle.Top, Height = 20, Margin = new Padding(0, 10, 0, 0) };
        lblTiempoFin = new Label { Text = "00:00:00", Dock = DockStyle.Top, Height = 20, Font = new Font("Consolas", 9), ForeColor = ThemeRenderer.Accent };
        trackFin = new CustomTrackBar { Dock = DockStyle.Top, Height = 25, Value = 1.0 };
        trackFin.ValueChangedByUser += (val) => { 
            lblTiempoFin.Text = FormatearTiempoDesdePorcentaje(val);
            if (val < trackInicio.Value) trackInicio.Value = val;
        };

        pnlSidebar.Controls.Add(trackFin);
        pnlSidebar.Controls.Add(lblTiempoFin);
        pnlSidebar.Controls.Add(lblEnd);
        pnlSidebar.Controls.Add(trackInicio);
        pnlSidebar.Controls.Add(lblTiempoInicio);
        pnlSidebar.Controls.Add(lblStart);
        pnlSidebar.Controls.Add(lblRecorteTitle);
        pnlSidebar.Controls.Add(lblMetaInfo);
        pnlSidebar.Controls.Add(lblMetaTitle);
        split.Panel2.Controls.Add(pnlSidebar);

        this.Controls.Add(split);
        this.Controls.Add(pnlTop);

        _timerUI = new System.Windows.Forms.Timer { Interval = 500 };
        _timerUI.Tick += (s, e) => {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
                _trackProgreso.Value = _mediaPlayer.Position;
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
        
        using var media = new Media(_libVLC, _rutaVideo, FromType.FromPath);
        _mediaPlayer.Play(media);
        
        _mediaPlayer.LengthChanged += (s, e) => {
            this.BeginInvoke(new Action(() => {
                lblTiempoFin.Text = TimeSpan.FromMilliseconds(e.Length).ToString(@"hh\:mm\:ss");
            }));
        };

        _timerUI.Start();
    }

    private string FormatearTiempoDesdePorcentaje(double porcentaje)
    {
        if (_mediaPlayer == null || _mediaPlayer.Length <= 0) return "00:00:00";
        long ms = (long)(porcentaje * _mediaPlayer.Length);
        return TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss");
    }

    private void CargarMetadatos()
    {
        _metadata = AppVideoProcessor.ObtenerMetadataManual(_rutaVideo);
        lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" +
                           $"Ext: {_metadata.Extension}\n" +
                           $"Tamaño: {(_metadata.TamanoBytes / 1024.0 / 1024.0):F2} MB\n" +
                           $"Resolución: {_metadata.Resolucion}\n" +
                           $"Codec: {_metadata.Codec}";
    }

    private void TogglePlayPause()
    {
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

    private void MostrarDialogoRecorte()
    {
        if (_mediaPlayer == null || _mediaPlayer.Length <= 0) return;

        TimeSpan inicio = TimeSpan.FromMilliseconds(trackInicio.Value * _mediaPlayer.Length);
        TimeSpan fin = TimeSpan.FromMilliseconds(trackFin.Value * _mediaPlayer.Length);
        TimeSpan duracion = fin - inicio;

        if (duracion.TotalSeconds <= 0)
        {
            MessageBox.Show("El punto de inicio debe ser anterior al punto de fin.");
            return;
        }

        if (MessageBox.Show($"¿Recortar desde {inicio:hh\\:mm\\:ss} hasta {fin:hh\\:mm\\:ss}?\nDuración total: {duracion:hh\\:mm\\:ss}", "Confirmar Recorte", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            ProcesarRecorte(inicio, duracion);
        }
    }

    private async void ProcesarRecorte(TimeSpan inicio, TimeSpan duracion)
    {
        PrepararParaProcesar();
        string output = Path.Combine(Path.GetDirectoryName(_rutaVideo)!, "Recorte_" + Path.GetFileName(_rutaVideo));
        bool ok = await AppVideoProcessor.Recortar(_rutaVideo, output, inicio, duracion);
        
        if (ok) MessageBox.Show("Recorte guardado: " + output);
        else MessageBox.Show("Error al recortar. Asegúrate de que ffmpeg.exe esté en la carpeta de la app.");
        
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
