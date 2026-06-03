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

/// <summary>
/// Interfaz principal del módulo "App Video".
/// Utiliza <c>LibVLCSharp</c> para la reproducción de video y se integra con <c>FFmpeg</c>
/// para operaciones de edición asíncronas (filtros y extracción de audio).
/// </summary>
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
        CargarMetadatos();
        InicializarVLC();
    }

    /// <summary>
    /// Construye la interfaz mediante código para un control total sobre el layout y la apariencia.
    /// Incorpora el control <c>VideoView</c> de LibVLC, sliders personalizados y una barra lateral de metadatos.
    /// </summary>
    private void InitializeCustomComponents()
    {
        ThemeRenderer.ApplyTheme(this);
        this.Text = "App Video - Studio";
        this.Size = new Size(1100, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None;

        // Barra Superior (80px de padding izquierdo para los semáforos)
        pnlTop = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = ThemeRenderer.SecondaryBg, Padding = new Padding(80, 0, 0, 0) };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTop.ClientRectangle, true);
        
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
        AgregarBotonEditor("🌑 B&N", startX, () => AplicarFiltro("BN"));
        AgregarBotonEditor("🎵 Audio", startX + 100, ExtraerAudio);
        AgregarBotonEditor("↩️ Revertir", startX + 200, RevertirCambios);
        AgregarBotonEditor("✂️ Recortar", startX + 300, RecortarVideo);

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
        btnSetLocation.Click += (s, e) => {
            if (btnSetLocation.Text == "📍 Registrar Ubicación")
            {
                ActivarModoMapaPicker();
            }
            else
            {
                ConfirmarUbicacionManual(s, e);
            }
        };

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

    /// <summary>
    /// Renderiza y añade un botón estandarizado a la barra superior, asociándole
    /// una acción específica (por ejemplo, aplicar un filtro o extraer audio).
    /// </summary>
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
        btn.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, true);
        pnlTop.Controls.Add(btn);
    }
    // Semáforos al estilo macOS
    /// <summary>
    /// Configura los botones de la barra de título con estilo semáforo (estilo macOS)
    /// permitiendo cerrar, minimizar o maximizar la ventana.
    /// </summary>
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

    /// <summary>
    /// Crea un botón circular con estilo plano para el panel de semáforos.
    /// </summary>
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
    /// Configura el motor nativo de LibVLC y el <c>MediaPlayer</c>.
    /// Maneja los eventos de cambio de longitud para sincronizar la interfaz de usuario.
    /// </summary>
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

    /// <summary>
    /// Extrae metadatos del archivo de video (resolución, codec, peso, coordenadas GPS) de manera asíncrona
    /// y carga dinámicamente un mapa de WebView2 dependiendo de si existen o no las coordenadas.
    /// </summary>
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

    /// <summary>
    /// Formatea y muestra los metadatos extraídos del video en la barra lateral (Sidebar).
    /// </summary>
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

    /// <summary>
    /// Carga el selector visual de mapa permitiéndole al usuario hacer clic en un punto
    /// para asignar manualmente coordenadas GPS a un video que carece de ellas.
    /// </summary>
    private void ActivarModoMapaPicker()
    {
        webMap.NavigateToString(AppVideoMapService.GenerarMapaPickerHtml());
        btnSetLocation.Text = "✅ Guardar Ubicación";
        btnSetLocation.BackColor = Color.LightGreen;
        btnSetLocation.ForeColor = Color.Black;
    }

    private void ConfirmarUbicacionManual(object? sender, EventArgs e)
    {
        btnSetLocation.Text = "📍 Registrar Ubicación";
        btnSetLocation.BackColor = ThemeRenderer.Accent;
        btnSetLocation.ForeColor = Color.White;
        
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

    /// <summary>
    /// Alterna el estado del reproductor de video entre pausa y reproducción,
    /// cambiando el icono del botón dinámicamente.
    /// </summary>
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

    /// <summary>
    /// Solicita al usuario el tiempo de inicio y la duración, y procede a recortar el video.
    /// </summary>
    private async void RecortarVideo()
    {
        TimeSpan posicionActual = _mediaPlayer != null ? TimeSpan.FromMilliseconds(_mediaPlayer.Time) : TimeSpan.Zero;
        if (posicionActual < TimeSpan.Zero) posicionActual = TimeSpan.Zero;

        TimeSpan duracionTotal = _mediaPlayer != null ? TimeSpan.FromMilliseconds(_mediaPlayer.Length) : TimeSpan.Zero;
        if (duracionTotal < TimeSpan.Zero) duracionTotal = TimeSpan.Zero;

        string sugerenciaInicio = posicionActual.ToString(@"hh\:mm\:ss");
        string sugerenciaFin = duracionTotal > TimeSpan.Zero ? duracionTotal.ToString(@"hh\:mm\:ss") : (posicionActual + TimeSpan.FromSeconds(10)).ToString(@"hh\:mm\:ss");

        string inicioStr = Microsoft.VisualBasic.Interaction.InputBox(
            "Introduce el tiempo de inicio (hh:mm:ss o segundos):", 
            "Recortar Video - Inicio", 
            sugerenciaInicio);
        if (string.IsNullOrWhiteSpace(inicioStr)) return;

        string finStr = Microsoft.VisualBasic.Interaction.InputBox(
            "Introduce el tiempo de finalización (hh:mm:ss o segundos):", 
            "Recortar Video - Final", 
            sugerenciaFin);
        if (string.IsNullOrWhiteSpace(finStr)) return;

        TimeSpan inicio;
        if (!TimeSpan.TryParse(inicioStr, out inicio))
        {
            if (double.TryParse(inicioStr, out double secs))
                inicio = TimeSpan.FromSeconds(secs);
            else
            {
                MessageBox.Show("Tiempo de inicio no válido. Formato esperado: hh:mm:ss o segundos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        TimeSpan fin;
        if (!TimeSpan.TryParse(finStr, out fin))
        {
            if (double.TryParse(finStr, out double secs))
                fin = TimeSpan.FromSeconds(secs);
            else
            {
                MessageBox.Show("Tiempo de finalización no válido. Formato esperado: hh:mm:ss o segundos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        if (fin <= inicio)
        {
            MessageBox.Show("El tiempo de finalización debe ser mayor que el tiempo de inicio.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        TimeSpan duracion = fin - inicio;

        lblMetaInfo.Text = "\nRECORTANDO VIDEO...\nPor favor espera.";
        lblMetaInfo.Update(); // Asegurar actualización visual de la UI

        PrepararParaProcesar();

        bool ok = false;
        string errorMsg = "";
        string tempOutput = Path.Combine(Path.GetDirectoryName(_rutaVideo)!, "temp_" + Guid.NewGuid().ToString("N") + Path.GetExtension(_rutaVideo));

        // Ejecutar procesamiento pesado en segundo plano
        await Task.Run(async () =>
        {
            try
            {
                // Crear copia de seguridad original (.bak) si no existe
                string backupPath = _rutaVideo + ".bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(_rutaVideo, backupPath, overwrite: false);
                }

                // Preservar metadatos
                if (_metadata != null)
                {
                    AppVideoProcessor.GuardarMetadata(_metadata);
                }

                // Esperar a que VLC libere completamente el handle del archivo
                await Task.Delay(1500).ConfigureAwait(false);

                ok = await AppVideoProcessor.Recortar(_rutaVideo, tempOutput, inicio, duracion).ConfigureAwait(false);

                if (ok)
                {
                    // Esperar hasta que el archivo original esté libre
                    bool archivoLibre = false;
                    for (int intento = 0; intento < 15; intento++)
                    {
                        try
                        {
                            using var fs = new FileStream(_rutaVideo, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                            archivoLibre = true;
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(1000).ConfigureAwait(false);
                        }
                    }

                    if (!archivoLibre)
                        throw new IOException("El archivo sigue bloqueado por otro proceso.");

                    File.Delete(_rutaVideo);
                    File.Move(tempOutput, _rutaVideo);
                }
            }
            catch (Exception ex)
            {
                ok = false;
                errorMsg = ex.Message;
            }
        });

        if (ok)
        {
            MessageBox.Show("Video recortado con éxito.", "Recorte de Video", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            string msj = "Error al recortar video.";
            if (!string.IsNullOrEmpty(errorMsg))
            {
                msj += $"\nDetalle: {errorMsg}";
            }
            else
            {
                msj += "\nAsegúrate de que ffmpeg.exe esté en la carpeta de la app.";
            }
            MessageBox.Show(msj, "Error de FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (File.Exists(tempOutput))
            {
                try { File.Delete(tempOutput); } catch { }
            }
        }
        
        CargarMetadatos();
        ReiniciarReproductor();
    }

    /// <summary>
    /// Detiene la reproducción para liberar el archivo, aplica el filtro de color a un archivo temporal
    /// y luego reemplaza el video original con la versión filtrada para realizar los cambios sobre el mismo video.
    /// Preserva los metadatos en un archivo companion .meta.json y crea una copia de seguridad original (.bak) si no existe.
    /// </summary>
    /// <param name="nombre">Nombre del filtro (Soft, Sepia, BN).</param>
    private async void AplicarFiltro(string nombre)
    {
        // Crear copia de seguridad original (.bak) si no existe ya
        string backupPath = _rutaVideo + ".bak";
        if (!File.Exists(backupPath))
        {
            try
            {
                File.Copy(_rutaVideo, backupPath, overwrite: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo crear una copia de seguridad: {ex.Message}. El proceso continuará pero no se podrá revertir.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Preservar metadatos en el archivo companion .meta.json antes de sobreescribir el video
        if (_metadata != null)
        {
            AppVideoProcessor.GuardarMetadata(_metadata);
        }

        string tempOutput = Path.Combine(Path.GetDirectoryName(_rutaVideo)!, "temp_" + Guid.NewGuid().ToString("N") + Path.GetExtension(_rutaVideo));
        
        lblMetaInfo.Text = "\nPROCESANDO FILTRO...\nPor favor espera.";
        
        // Detener la reproducción y destruir el MediaPlayer para que VLC libere el handle del archivo
        PrepararParaProcesar();
        await Task.Delay(1500); // Pausa ampliada para garantizar la liberación del handle por parte de VLC
        
        bool ok = await AppVideoProcessor.AplicarFiltro(_rutaVideo, tempOutput, nombre);
        
        if (ok)
        {
            try
            {
                // Esperar hasta que el archivo original esté libre (máximo 10 intentos de 1 segundo)
                bool archivoLibre = false;
                for (int intento = 0; intento < 10; intento++)
                {
                    try
                    {
                        using var fs = new FileStream(_rutaVideo, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        archivoLibre = true;
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(1000);
                    }
                }

                if (!archivoLibre)
                    throw new IOException("El archivo sigue siendo utilizado por otro proceso después de 10 segundos de espera.");

                // Reemplazar el archivo de video original con el filtrado
                File.Delete(_rutaVideo);
                File.Move(tempOutput, _rutaVideo);
                MessageBox.Show("Filtro aplicado con éxito sobre el mismo video.", "Filtro Aplicado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Filtro aplicado, pero no se pudo reemplazar el archivo original: {ex.Message}\nEl archivo filtrado se guardó en: {tempOutput}", "Error de reemplazo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else
        {
            MessageBox.Show("Error al aplicar filtro. Asegúrate de que ffmpeg.exe esté en la carpeta de la app.", "Error de FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (File.Exists(tempOutput))
            {
                try { File.Delete(tempOutput); } catch { }
            }
        }
        
        CargarMetadatos();
        ReiniciarReproductor();
    }

    /// <summary>
    /// Llama asíncronamente a FFmpeg para extraer únicamente la pista de audio
    /// y guardarla en formato MP3 en la misma ruta que el video original.
    /// Se realiza en segundo plano sin interrumpir la reproducción actual del video.
    /// </summary>
    private async void ExtraerAudio()
    {
        string output = Path.Combine(Path.GetDirectoryName(_rutaVideo)!, Path.GetFileNameWithoutExtension(_rutaVideo) + ".mp3");
        
        string originalMetaText = lblMetaInfo.Text;
        lblMetaInfo.Text = "\nEXTRAYENDO AUDIO...\nPor favor espera.";
        
        bool ok = await AppVideoProcessor.ExtraerAudio(_rutaVideo, output);
        
        if (ok)
        {
            MessageBox.Show("Audio extraído con éxito en: " + output, "Extracción de Audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Error al extraer audio. Asegúrate de que ffmpeg.exe esté en la carpeta de la app.", "Error de FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        
        CargarMetadatos();
    }

    /// <summary>
    /// Restaura el video original a partir de la copia de seguridad (.bak) creada antes del primer filtro.
    /// </summary>
    private async void RevertirCambios()
    {
        string backupPath = _rutaVideo + ".bak";
        if (!File.Exists(backupPath))
        {
            MessageBox.Show("No hay cambios que revertir (el video ya está en su versión original).", "Revertir Cambios", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var res = MessageBox.Show("¿Deseas revertir todos los filtros aplicados y regresar al video original?", "Confirmar Reversión", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (res != DialogResult.Yes) return;

        PrepararParaProcesar();
        await Task.Delay(500); // Esperar liberación por parte de VLC

        try
        {
            if (File.Exists(_rutaVideo))
            {
                File.Delete(_rutaVideo);
            }
            File.Move(backupPath, _rutaVideo);
            MessageBox.Show("Cambios revertidos. Video original restaurado.", "Restaurado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al restaurar el archivo original: {ex.Message}", "Error de restauración", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        CargarMetadatos();
        ReiniciarReproductor();
    }

    /// <summary>
    /// Frena la reproducción y destruye completamente el MediaPlayer y su Media para que VLC
    /// suelte el handle del archivo de disco, permitiendo que FFmpeg pueda sobrescribir o leer el archivo.
    /// Se recrea el MediaPlayer vacío listo para ser reiniciado después.
    /// </summary>
    private void PrepararParaProcesar()
    {
        _timerUI.Stop();
        _mediaPlayer.Stop();
        _mediaPlayer.Media = null;
        // Desasociar el VideoView para que VLC libere completamente el handle del archivo
        _videoView.MediaPlayer = null;
        _mediaPlayer.Dispose();
        // Recrear el MediaPlayer vacío (sin cargar ningún archivo aún)
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        _videoView.MediaPlayer = _mediaPlayer;
    }

    /// <summary>
    /// Vuelve a crear y montar la instancia de Media en el reproductor para reanudar 
    /// la vista del video original una vez terminado un proceso.
    /// </summary>
    private void ReiniciarReproductor()
    {
        _timerUI.Start();
        using var media = new Media(_libVLC, _rutaVideo, FromType.FromPath);
        _mediaPlayer.Play(media);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timerUI.Stop();
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();

        // Limpiar archivos temporales generados durante la sesión de edición
        LimpiarArchivosTemporales();

        base.OnFormClosing(e);
    }

    /// <summary>
    /// Elimina silenciosamente el archivo de copia de seguridad (.bak) y el companion
    /// de metadatos (.meta.json) que se crean al aplicar filtros, para no dejar
    /// archivos residuales en la carpeta del video original.
    /// </summary>
    private void LimpiarArchivosTemporales()
    {
        try { File.Delete(_rutaVideo + ".bak"); } catch { }
        try { File.Delete(_rutaVideo + ".meta.json"); } catch { }
    }
}
