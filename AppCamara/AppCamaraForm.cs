using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using ExploradorArchivos.AppVideo;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppCamara;

public partial class AppCamaraForm : Form
{
    // ─── Captura de cámara ───────────────────────────────────────────────────
    private FilterInfoCollection? _dispositivosVideo;
    private VideoCaptureDevice?   _fuenteVideo;

    // ─── Grabación de video ──────────────────────────────────────────────────
    private AviGrabador?          _grabador;
    private bool                  _grabando          = false;
    private int                   _segundosGrabacion = 0;
    private System.Windows.Forms.Timer _timerGrabacion = default!;
    private int                   _frameWidth        = 640;
    private int                   _frameHeight       = 480;
    private string                _rutaAviTemporal   = string.Empty;  // archivo intermedio
    private string                _rutaMp4Final      = string.Empty;  // archivo final MP4

    // ─── Controles UI ────────────────────────────────────────────────────────
    private Panel      pnlTop      = default!;
    private PictureBox picPreview  = default!;
    private Panel      pnlBottom   = default!;
    private Button     btnCapturar = default!;
    private Button     btnGrabar   = default!;
    private Label      lblTimer    = default!;
    private ComboBox   cbCamaras   = default!;
    private string     _rutaDestino;

    // ─── Resultado ───────────────────────────────────────────────────────────
    public string? RutaFotoGuardada  { get; private set; }
    public string? RutaVideoGuardado { get; private set; }

    // ════════════════════════════════════════════════════════════════════════
    public AppCamaraForm(string rutaDestino)
    {
        _rutaDestino = Directory.Exists(rutaDestino)
            ? rutaDestino
            : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        InitializeCustomComponents();
        InicializarTimerGrabacion();
        CargarDispositivosCamara();
    }

    // ════════════════════════════════════════════════════════════════════════
    #region Inicialización UI

    private void InitializeCustomComponents()
    {
        ThemeRenderer.ApplyTheme(this);
        this.Text            = "Cámara";
        this.Size            = new Size(700, 620);
        this.MinimumSize     = new Size(600, 520);
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition   = FormStartPosition.CenterParent;

        // ── Barra Superior ──────────────────────────────────────────────────
        pnlTop = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 65,
            BackColor = ThemeRenderer.SecondaryBg,
            Padding   = new Padding(80, 10, 10, 10)
        };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTop.ClientRectangle, true);

        bool isDragging  = false;
        Point lastCursor = Point.Empty;
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTop.MouseMove += (s, e) => { if (isDragging) this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); };
        pnlTop.MouseUp   += (s, e) => isDragging = false;

        ConfigurarSemaforos();

        Label lblTitulo = new Label
        {
            Text      = "📷 CÁMARA — FOTO & VIDEO",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("MS Sans Serif", 10, FontStyle.Bold),
            ForeColor = ThemeRenderer.MainText
        };
        pnlTop.Controls.Add(lblTitulo);

        // ── Vista Previa ─────────────────────────────────────────────────────
        picPreview = new PictureBox
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 20),
            SizeMode  = PictureBoxSizeMode.Zoom
        };

        // ── Panel Inferior (110px) ────────────────────────────────────────────
        pnlBottom = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 110,
            BackColor = ThemeRenderer.MainBg,
            Padding   = new Padding(15)
        };
        pnlBottom.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlBottom.ClientRectangle, true);

        // ── Fila 1: Selector de cámara ───────────────────────────────────────
        Label lblSelect = new Label
        {
            Text      = "Dispositivo:",
            Location  = new Point(15, 8),
            AutoSize  = true,
            Font      = new Font("MS Sans Serif", 8, FontStyle.Bold),
            ForeColor = ThemeRenderer.SecondaryText
        };

        cbCamaras = new ComboBox
        {
            Width         = 210,
            Location      = new Point(15, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("MS Sans Serif", 9)
        };
        cbCamaras.SelectedIndexChanged += CbCamaras_SelectedIndexChanged;

        // ── Timer visible solo mientras se graba ─────────────────────────────
        lblTimer = new Label
        {
            Text      = "🔴 00:00",
            Location  = new Point(240, 28),
            AutoSize  = true,
            Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 50, 50),
            Visible   = false
        };

        // ── Fila 2: Botones ───────────────────────────────────────────────────
        btnCapturar = new Button
        {
            Text      = "📸 Tomar Foto",
            Size      = new Size(140, 42),
            Anchor    = AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btnCapturar.FlatAppearance.BorderSize = 0;
        btnCapturar.Click += BtnCapturar_Click;
        btnCapturar.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, btnCapturar.ClientRectangle, true);

        btnGrabar = new Button
        {
            Text      = "🎥 Grabar Video",
            Size      = new Size(145, 42),
            Anchor    = AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(34, 139, 34),
            ForeColor = Color.White,
            Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btnGrabar.FlatAppearance.BorderSize = 0;
        btnGrabar.Click += BtnGrabar_Click;
        btnGrabar.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, btnGrabar.ClientRectangle, true);

        Button btnCancelar = new Button
        {
            Text      = "Cancelar",
            Size      = new Size(95, 42),
            Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            ForeColor = ThemeRenderer.MainText,
            Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btnCancelar.FlatAppearance.BorderSize = 0;
        btnCancelar.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        btnCancelar.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, btnCancelar.ClientRectangle, true);

        pnlBottom.Controls.AddRange(new Control[]
        {
            lblSelect, cbCamaras, lblTimer,
            btnCapturar, btnGrabar, btnCancelar
        });

        this.Controls.Add(picPreview);
        this.Controls.Add(pnlTop);
        this.Controls.Add(pnlBottom);

        this.SizeChanged += (s, e) => ReposicionarBotones(btnCancelar);
        ReposicionarBotones(btnCancelar);
    }

    private void ReposicionarBotones(Button btnCancelar)
    {
        int mitad = this.Width / 2;
        btnCapturar.Location = new Point(mitad - 155, 58);
        btnGrabar.Location   = new Point(mitad + 5,   58);
        btnCancelar.Location = new Point(this.Width - 115, 58);
    }

    private void InicializarTimerGrabacion()
    {
        _timerGrabacion          = new System.Windows.Forms.Timer();
        _timerGrabacion.Interval = 1000;
        _timerGrabacion.Tick    += TimerGrabacion_Tick;
    }

    private void ConfigurarSemaforos()
    {
        Panel pnlSemaforos = new Panel
        {
            Location  = new Point(10, 22),
            Size      = new Size(60, 20),
            BackColor = Color.Transparent
        };

        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0);
        btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40);
        btnMax.Click += (s, e) =>
            this.WindowState = this.WindowState == FormWindowState.Normal
                ? FormWindowState.Maximized
                : FormWindowState.Normal;

        pnlSemaforos.Controls.AddRange(new Control[] { btnClose, btnMin, btnMax });
        pnlTop.Controls.Add(pnlSemaforos);
    }

    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button b = new Button
        {
            Location  = new Point(x, 2),
            Size      = new Size(14, 14),
            BackColor = color,
            FlatStyle = FlatStyle.Flat
        };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(pnlTop.BackColor);
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
        };
        return b;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Cámara — Captura de frames

    private void CargarDispositivosCamara()
    {
        try
        {
            _dispositivosVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (_dispositivosVideo.Count == 0)
            {
                MessageBox.Show("No se encontraron dispositivos de cámara.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
                return;
            }

            foreach (FilterInfo d in _dispositivosVideo)
                cbCamaras.Items.Add(d.Name);

            cbCamaras.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al inicializar cámaras: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            this.Close();
        }
    }

    private void CbCamaras_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_grabando)
        {
            MessageBox.Show("Detén la grabación antes de cambiar de cámara.", "Aviso",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        IniciarCaptura();
    }

    private void IniciarCaptura()
    {
        try
        {
            DetenerCaptura();

            if (_dispositivosVideo != null && cbCamaras.SelectedIndex >= 0)
            {
                string moniker = _dispositivosVideo[cbCamaras.SelectedIndex].MonikerString;
                _fuenteVideo           = new VideoCaptureDevice(moniker);
                _fuenteVideo.NewFrame += FuenteVideo_NewFrame;
                _fuenteVideo.Start();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo iniciar la cámara: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FuenteVideo_NewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();

            // ── Escribir frame al archivo AVI si está grabando ────────────────
            if (_grabando && _grabador != null)
            {
                lock (_grabador)
                {
                    _grabador.EscribirFrame(frame);
                }
            }

            // ── Actualizar la vista previa en el hilo UI ──────────────────────
            picPreview.BeginInvoke(new Action(() =>
            {
                picPreview.Image?.Dispose();
                picPreview.Image = frame;
            }));
        }
        catch { }
    }

    private void DetenerCaptura()
    {
        if (_fuenteVideo != null)
        {
            if (_fuenteVideo.IsRunning)
            {
                _fuenteVideo.SignalToStop();
                _fuenteVideo.WaitForStop();
            }
            _fuenteVideo.NewFrame -= FuenteVideo_NewFrame;
            _fuenteVideo = null;
        }

        if (picPreview.Image != null)
        {
            picPreview.Image.Dispose();
            picPreview.Image = null;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Grabación de Video

    private void BtnGrabar_Click(object? sender, EventArgs e)
    {
        if (!_grabando)
            IniciarGrabacion();
        else
            DetenerGrabacion();
    }

    private void IniciarGrabacion()
    {
        if (picPreview.Image == null)
        {
            MessageBox.Show("No hay señal de cámara disponible.", "Aviso",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Detectar dimensiones del frame actual
            _frameWidth  = picPreview.Image.Width;
            _frameHeight = picPreview.Image.Height;

            // Rutas: AVI temporal y MP4 final
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            _rutaAviTemporal = Path.Combine(_rutaDestino, $"Video_{timestamp}_tmp.avi");
            _rutaMp4Final    = Path.Combine(_rutaDestino, $"Video_{timestamp}.mp4");

            // Inicializar el grabador AVI nativo (30 fps)
            _grabador = new AviGrabador();
            _grabador.Abrir(_rutaAviTemporal, _frameWidth, _frameHeight, 30);

            // Actualizar estado
            _grabando          = true;
            _segundosGrabacion = 0;

            // Actualizar UI
            btnGrabar.Text      = "⏹ Detener";
            btnGrabar.BackColor = Color.FromArgb(180, 30, 30);
            btnCapturar.Enabled = false;
            cbCamaras.Enabled   = false;
            lblTimer.Visible    = true;
            lblTimer.Text       = "🔴 00:00";

            _timerGrabacion.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar la grabación:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            LimpiarGrabador();
        }
    }

    private async void DetenerGrabacion()
    {
        _timerGrabacion.Stop();
        _grabando = false;

        LimpiarGrabador();   // cierra y libera el AVI

        // Restaurar UI
        btnGrabar.Text      = "🎥 Grabar Video";
        btnGrabar.BackColor = Color.FromArgb(34, 139, 34);
        btnCapturar.Enabled = false;   // deshabilitado durante conversión
        cbCamaras.Enabled   = false;
        lblTimer.Visible    = true;
        lblTimer.Text       = "⏳ Convirtiendo a MP4...";
        lblTimer.ForeColor  = Color.FromArgb(200, 150, 0);
        btnGrabar.Enabled   = false;

        // ── Convertir AVI → MP4 usando ffmpeg.exe ────────────────────────────
        bool convertido = false;
        if (File.Exists(_rutaAviTemporal))
        {
            convertido = await AppVideoProcessor.ConvertirAviAMp4(_rutaAviTemporal, _rutaMp4Final);

            if (convertido)
            {
                // Borrar el AVI temporal
                try { File.Delete(_rutaAviTemporal); } catch { }
                RutaVideoGuardado = _rutaMp4Final;
            }
            else
            {
                // ffmpeg no disponible → conservar AVI
                RutaVideoGuardado = _rutaAviTemporal;
            }
        }

        // Restaurar UI tras conversión
        btnCapturar.Enabled = true;
        cbCamaras.Enabled   = true;
        lblTimer.Visible    = false;
        lblTimer.ForeColor  = Color.FromArgb(220, 50, 50);
        btnGrabar.Enabled   = true;

        if (!string.IsNullOrEmpty(RutaVideoGuardado) && File.Exists(RutaVideoGuardado))
        {
            string formato = convertido ? "MP4" : "AVI (ffmpeg no encontrado)";
            var res = MessageBox.Show(
                $"✅ Video guardado como {formato}:\n{RutaVideoGuardado}\n\n¿Deseas cerrar la cámara?",
                "Grabación Completa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (res == DialogResult.Yes)
            {
                DetenerCaptura();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }

    private void LimpiarGrabador()
    {
        if (_grabador != null)
        {
            lock (_grabador)
            {
                try   { _grabador.Cerrar(); }
                catch { }
                _grabador.Dispose();
                _grabador = null;
            }
        }
    }

    private void TimerGrabacion_Tick(object? sender, EventArgs e)
    {
        _segundosGrabacion++;
        int min = _segundosGrabacion / 60;
        int seg = _segundosGrabacion % 60;
        lblTimer.Text = $"🔴 {min:D2}:{seg:D2}";
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Captura de Foto

    private void BtnCapturar_Click(object? sender, EventArgs e)
    {
        if (picPreview.Image == null)
        {
            MessageBox.Show("No hay señal de vídeo disponible para capturar.", "Aviso",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string timestamp   = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string rutaArchivo = Path.Combine(_rutaDestino, $"Foto_{timestamp}.jpg");

            lock (picPreview)
            {
                using Bitmap captura = new Bitmap(picPreview.Image);
                captura.Save(rutaArchivo, ImageFormat.Jpeg);
            }

            RutaFotoGuardada = rutaArchivo;
            DetenerCaptura();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar la foto: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_grabando)
        {
            _timerGrabacion.Stop();
            _grabando = false;
            LimpiarGrabador();
            // Limpiar AVI temporal si quedó sin convertir
            try { if (File.Exists(_rutaAviTemporal)) File.Delete(_rutaAviTemporal); } catch { }
        }

        DetenerCaptura();
        _timerGrabacion.Dispose();
        base.OnFormClosing(e);
    }
}
