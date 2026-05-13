using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using ExploradorArchivos.Mp3;

namespace ExploradorArchivos.Video;

public class VideoPlayerForm : Form
{
    // === Paleta Dark Mode (misma que MusicPlayerForm) ===
    private static readonly Color BgPrimary = ColorTranslator.FromHtml("#1E1E2E");
    private static readonly Color BgSurface = ColorTranslator.FromHtml("#2A2A3E");
    private static readonly Color BgCard = ColorTranslator.FromHtml("#313145");
    private static readonly Color TextPrimary = ColorTranslator.FromHtml("#CDD6F4");
    private static readonly Color TextSecondary = ColorTranslator.FromHtml("#A6ADC8");
    private static readonly Color AccentRose = ColorTranslator.FromHtml("#F48FB1");
    private static readonly Color AccentViolet = ColorTranslator.FromHtml("#CE93D8");

    // === LibVLC ===
    private LibVLC _libVLC = null!;
    private LibVLCSharp.Shared.MediaPlayer _mediaPlayer = null!;
    private VideoView _videoView = null!;

    // === Controles UI ===
    private Panel _pnlControles = null!;
    private Panel _pnlInfo = null!;
    private Label _lblTitulo = null!;
    private Label _lblInfo = null!;
    private CustomTrackBar _trackProgreso = null!;
    private CustomTrackBar _trackVolumen = null!;
    private Label _lblTiempoActual = null!;
    private Label _lblTiempoTotal = null!;
    private Button _btnPlayPause = null!;
    private Button _btnStop = null!;
    private Button _btnAnterior = null!;
    private Button _btnSiguiente = null!;
    private Button _btnFullscreen = null!;
    private Label _lblVolIcon = null!;

    // === Estado ===
    private readonly string _filePath;
    private bool _actualizandoProgreso;
    private bool _isFullscreen;
    private FormWindowState _prevWindowState;
    private FormBorderStyle _prevBorderStyle;
    private readonly System.Windows.Forms.Timer _timerUI;

    // Arrastre
    private bool _dragging = false;
    private Point _startPoint = new Point(0, 0);

    public VideoPlayerForm(string filePath)
    {
        _filePath = filePath;

        _timerUI = new System.Windows.Forms.Timer { Interval = 300 };
        _timerUI.Tick += TimerUI_Tick;

        InicializarComponentes();
        InicializarVLC();
    }

    private void InicializarComponentes()
    {
        // === Form ===
        this.Text = $"🎬 {Path.GetFileName(_filePath)}";
        this.Size = new Size(960, 640);
        this.MinimumSize = new Size(640, 480);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = BgPrimary;
        this.ForeColor = TextPrimary;
        try { this.Font = new Font("Inter", 10); } catch { this.Font = new Font("Segoe UI", 10); }
        this.DoubleBuffered = true;
        this.KeyPreview = true;
        this.FormBorderStyle = FormBorderStyle.None;
        this.KeyDown += VideoPlayerForm_KeyDown;

        // Borde plano opcional
        this.Paint += (s, e) => {
            e.Graphics.DrawRectangle(new Pen(ColorTranslator.FromHtml("#313145"), 1), 0, 0, this.Width - 1, this.Height - 1);
        };

        // ==========================================
        // PANEL DE CONTROLES (Bottom)
        // ==========================================
        _pnlControles = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 120,
            BackColor = BgSurface,
            Padding = new Padding(15, 8, 15, 8)
        };

        // --- Info del archivo ---
        _pnlInfo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 25,
            BackColor = Color.Transparent
        };
        _pnlInfo.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _startPoint = e.Location; } };
        _pnlInfo.MouseMove += (s, e) => { if (_dragging) { this.Location = new Point(this.Location.X + e.X - _startPoint.X, this.Location.Y + e.Y - _startPoint.Y); } };
        _pnlInfo.MouseUp += (s, e) => { _dragging = false; };

        _lblTitulo = new Label
        {
            Text = Path.GetFileNameWithoutExtension(_filePath),
            Font = new Font("Segoe UI Variable Display", 10, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _pnlInfo.Controls.Add(_lblTitulo);

        _lblInfo = new Label
        {
            Text = Path.GetExtension(_filePath).ToUpper().Replace(".", "") + " Video",
            Font = new Font("Segoe UI", 9),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _pnlInfo.Controls.Add(_lblInfo);
        _pnlControles.Controls.Add(_pnlInfo);

        // --- Barra de progreso ---
        var pnlProgreso = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.Transparent,
            Padding = new Padding(50, 0, 60, 0)
        };

        _lblTiempoActual = new Label
        {
            Text = "0:00",
            Font = new Font("Segoe UI", 9),
            ForeColor = TextSecondary,
            Dock = DockStyle.Left,
            Width = 50,
            TextAlign = ContentAlignment.MiddleRight
        };
        pnlProgreso.Controls.Add(_lblTiempoActual);

        _trackProgreso = new CustomTrackBar
        {
            Dock = DockStyle.Fill
        };
        _trackProgreso.ValueChangedByUser += TrackProgreso_UserChanged;
        pnlProgreso.Controls.Add(_trackProgreso);

        _lblTiempoTotal = new Label
        {
            Text = "0:00",
            Font = new Font("Segoe UI", 9),
            ForeColor = TextSecondary,
            Dock = DockStyle.Right,
            Width = 55,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlProgreso.Controls.Add(_lblTiempoTotal);

        _pnlControles.Controls.Add(pnlProgreso);

        // --- Botones de control ---
        var pnlBotones = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        int centerX = 420;
        int btnY = 8;

        _btnAnterior = CrearBotonControl("⏮", new Point(centerX - 110, btnY), 40);
        _btnAnterior.Click += (s, e) => RetrocederVideo();

        _btnPlayPause = CrearBotonControl("▶", new Point(centerX - 55, btnY), 50, true);
        _btnPlayPause.Click += (s, e) => TogglePlayPause();

        _btnStop = CrearBotonControl("⏹", new Point(centerX + 5, btnY), 40);
        _btnStop.Click += (s, e) => DetenerVideo();

        _btnSiguiente = CrearBotonControl("⏭", new Point(centerX + 55, btnY), 40);
        _btnSiguiente.Click += (s, e) => AvanzarVideo();

        _btnFullscreen = CrearBotonControl("⛶", new Point(centerX + 130, btnY), 40);
        _btnFullscreen.Click += (s, e) => ToggleFullscreen();

        // Volumen
        _lblVolIcon = new Label
        {
            Text = "🔊",
            Font = new Font("Segoe UI Emoji", 11),
            ForeColor = TextSecondary,
            Location = new Point(centerX + 195, btnY + 8),
            Size = new Size(28, 24),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _trackVolumen = new CustomTrackBar
        {
            Location = new Point(centerX + 225, btnY + 10),
            Size = new Size(120, 20),
            Value = 0.8,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _trackVolumen.ValueChanged += (val) =>
        {
            if (_mediaPlayer != null) _mediaPlayer.Volume = (int)(val * 100);
        };

        pnlBotones.Controls.AddRange(new Control[] {
            _btnAnterior, _btnPlayPause, _btnStop, _btnSiguiente,
            _btnFullscreen, _lblVolIcon, _trackVolumen
        });

        _pnlControles.Controls.Add(pnlBotones);
        this.Controls.Add(_pnlControles);

        // ==========================================
        // VIDEO VIEW (área principal)
        // ==========================================
        _videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };
        _videoView.DoubleClick += (s, e) => ToggleFullscreen();
        this.Controls.Add(_videoView);
    }

    private void InicializarVLC()
    {
        Core.Initialize();

        _libVLC = new LibVLC("--no-xlib");
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

        _videoView.MediaPlayer = _mediaPlayer;

        // Eventos del media player
        _mediaPlayer.Playing += (s, e) => this.BeginInvoke(() => OnEstadoCambiado(true));
        _mediaPlayer.Paused += (s, e) => this.BeginInvoke(() => OnEstadoCambiado(false));
        _mediaPlayer.Stopped += (s, e) => this.BeginInvoke(() => OnEstadoCambiado(false));
        _mediaPlayer.EndReached += (s, e) => this.BeginInvoke(() =>
        {
            OnEstadoCambiado(false);
            _trackProgreso.Value = 1.0;
        });

        _mediaPlayer.LengthChanged += (s, e) => this.BeginInvoke(() =>
        {
            var total = TimeSpan.FromMilliseconds(e.Length);
            _lblTiempoTotal.Text = FormatTime(total);
        });

        // Iniciar timer para actualizar progreso
        _timerUI.Start();

        // Reproducir el archivo
        using var media = new Media(_libVLC, _filePath, FromType.FromPath);
        _mediaPlayer.Play(media);
    }

    // === TIMER UI (actualiza progreso) ===

    private void TimerUI_Tick(object? sender, EventArgs e)
    {
        if (_mediaPlayer == null || _actualizandoProgreso) return;

        if (_mediaPlayer.IsPlaying)
        {
            float pos = _mediaPlayer.Position;
            _trackProgreso.Value = pos;

            long currentMs = (long)(pos * _mediaPlayer.Length);
            _lblTiempoActual.Text = FormatTime(TimeSpan.FromMilliseconds(currentMs));
        }
    }

    // === CONTROLES ===

    private void TogglePlayPause()
    {
        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
            _mediaPlayer.Pause();
        else
            _mediaPlayer.Play();
    }

    private void DetenerVideo()
    {
        if (_mediaPlayer == null) return;
        System.Threading.ThreadPool.QueueUserWorkItem(_ => _mediaPlayer.Stop());
        _trackProgreso.Value = 0;
        _lblTiempoActual.Text = "0:00";
    }

    private void AvanzarVideo()
    {
        if (_mediaPlayer == null) return;
        float newPos = Math.Min(_mediaPlayer.Position + 0.05f, 1.0f);
        _mediaPlayer.Position = newPos;
    }

    private void RetrocederVideo()
    {
        if (_mediaPlayer == null) return;
        float newPos = Math.Max(_mediaPlayer.Position - 0.05f, 0.0f);
        _mediaPlayer.Position = newPos;
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            this.FormBorderStyle = _prevBorderStyle;
            this.WindowState = _prevWindowState;
            _pnlControles.Visible = true;
            _isFullscreen = false;
        }
        else
        {
            _prevWindowState = this.WindowState;
            _prevBorderStyle = this.FormBorderStyle;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            _pnlControles.Visible = false;
            _isFullscreen = true;
        }
    }

    private void TrackProgreso_UserChanged(double val)
    {
        if (_mediaPlayer == null) return;
        _actualizandoProgreso = true;
        _mediaPlayer.Position = (float)val;
        _actualizandoProgreso = false;
    }

    private void OnEstadoCambiado(bool reproduciendo)
    {
        _btnPlayPause.Text = reproduciendo ? "⏸" : "▶";
    }

    // === KEYBOARD SHORTCUTS ===

    private void VideoPlayerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space:
                e.SuppressKeyPress = true;
                TogglePlayPause();
                break;
            case Keys.Escape:
                if (_isFullscreen) ToggleFullscreen();
                else this.Close();
                break;
            case Keys.F:
            case Keys.F11:
                ToggleFullscreen();
                break;
            case Keys.Right:
                AvanzarVideo();
                break;
            case Keys.Left:
                RetrocederVideo();
                break;
            case Keys.Up:
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = Math.Min(_mediaPlayer.Volume + 5, 100);
                    _trackVolumen.Value = _mediaPlayer.Volume / 100.0;
                }
                break;
            case Keys.Down:
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = Math.Max(_mediaPlayer.Volume - 5, 0);
                    _trackVolumen.Value = _mediaPlayer.Volume / 100.0;
                }
                break;
        }
    }

    // === HELPERS ===

    private Button CrearBotonControl(string text, Point location, int size, bool isPrimary = false)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(size, size),
            Location = location,
            Font = new Font("Segoe UI Emoji", isPrimary ? 18 : 13),
            ForeColor = isPrimary ? Color.White : TextSecondary,
            BackColor = isPrimary ? AccentRose : Color.Transparent,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = isPrimary
            ? AccentViolet
            : ColorTranslator.FromHtml("#3A3A52");
        return btn;
    }

    private static string FormatTime(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    // === CLEANUP ===

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timerUI.Stop();
        _timerUI.Dispose();

        if (_mediaPlayer != null)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Dispose();
            });
        }

        _libVLC?.Dispose();
        base.OnFormClosing(e);
    }
}
