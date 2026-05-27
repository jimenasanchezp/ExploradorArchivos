using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppCamara;

public partial class AppCamaraForm : Form
{
    private FilterInfoCollection? _dispositivosVideo;
    private VideoCaptureDevice? _fuenteVideo;
    
    private Panel pnlTop = default!;
    private PictureBox picPreview = default!;
    private Panel pnlBottom = default!;
    private Button btnCapturar = default!;
    private ComboBox cbCamaras = default!;
    private string _rutaDestino;

    public string? RutaFotoGuardada { get; private set; }

    public AppCamaraForm(string rutaDestino)
    {
        _rutaDestino = Directory.Exists(rutaDestino) ? rutaDestino : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        InitializeCustomComponents();
        CargarDispositivosCamara();
    }

    private void InitializeCustomComponents()
    {
        ThemeRenderer.ApplyTheme(this);
        this.Text = "Cámara";
        this.Size = new Size(700, 600);
        this.MinimumSize = new Size(600, 500);
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterParent;

        // Barra Superior (con 80px de padding izquierdo para los semáforos)
        pnlTop = new Panel 
        { 
            Dock = DockStyle.Top, 
            Height = 65, 
            BackColor = ThemeRenderer.SecondaryBg, 
            Padding = new Padding(80, 10, 10, 10) 
        };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTop.ClientRectangle, true);

        // Arrastrar Ventana
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTop.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTop.MouseUp += (s, e) => { isDragging = false; };

        ConfigurarSemaforos();

        Label lblTitulo = new Label
        {
            Text = "📷 CAPTURA DE FOTOS",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("MS Sans Serif", 10, FontStyle.Bold),
            ForeColor = ThemeRenderer.MainText
        };
        pnlTop.Controls.Add(lblTitulo);

        // Contenedor de Vista Previa
        picPreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            SizeMode = PictureBoxSizeMode.Zoom
        };

        // Panel de Control Inferior
        pnlBottom = new Panel 
        { 
            Dock = DockStyle.Bottom, 
            Height = 75, 
            BackColor = ThemeRenderer.MainBg, 
            Padding = new Padding(15) 
        };
        pnlBottom.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlBottom.ClientRectangle, true);

        // Selector de Cámara
        cbCamaras = new ComboBox
        {
            Width = 200,
            Location = new Point(15, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("MS Sans Serif", 9)
        };
        cbCamaras.SelectedIndexChanged += CbCamaras_SelectedIndexChanged;

        Label lblSelect = new Label
        {
            Text = "Dispositivo:",
            Location = new Point(15, 5),
            AutoSize = true,
            Font = new Font("MS Sans Serif", 8, FontStyle.Bold),
            ForeColor = ThemeRenderer.SecondaryText
        };
        pnlBottom.Controls.Add(lblSelect);
        pnlBottom.Controls.Add(cbCamaras);

        // Botón Capturar
        btnCapturar = new Button
        {
            Text = "📸 Tomar Foto",
            Size = new Size(140, 45),
            Location = new Point(this.Width / 2 - 70, 15),
            Anchor = AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Font = new Font("MS Sans Serif", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCapturar.FlatAppearance.BorderSize = 0;
        btnCapturar.Click += BtnCapturar_Click;
        btnCapturar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnCapturar.ClientRectangle, true);
        pnlBottom.Controls.Add(btnCapturar);

        // Botón Cancelar
        Button btnCancelar = new Button
        {
            Text = "Cancelar",
            Size = new Size(95, 45),
            Location = new Point(this.Width - 115, 15),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            Font = new Font("MS Sans Serif", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCancelar.FlatAppearance.BorderSize = 0;
        btnCancelar.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        btnCancelar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnCancelar.ClientRectangle, true);
        pnlBottom.Controls.Add(btnCancelar);

        this.Controls.Add(picPreview);
        this.Controls.Add(pnlTop);
        this.Controls.Add(pnlBottom);

        // Reposicionar controles dinámicamente al cambiar de tamaño
        this.SizeChanged += (s, e) => {
            btnCapturar.Left = this.Width / 2 - 70;
            btnCancelar.Left = this.Width - 115;
        };
    }

    private void ConfigurarSemaforos()
    {
        Panel pnlSemaforos = new Panel { Location = new Point(10, 22), Size = new Size(60, 20), BackColor = Color.Transparent };
        
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0);
        btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        
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

    private void CargarDispositivosCamara()
    {
        try
        {
            _dispositivosVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (_dispositivosVideo.Count == 0)
            {
                MessageBox.Show("No se encontraron dispositivos de cámara de vídeo.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
                return;
            }

            foreach (FilterInfo dispositivo in _dispositivosVideo)
            {
                cbCamaras.Items.Add(dispositivo.Name);
            }
            cbCamaras.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al inicializar las cámaras: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            this.Close();
        }
    }

    private void CbCamaras_SelectedIndexChanged(object? sender, EventArgs e)
    {
        IniciarCaptura();
    }

    private void IniciarCaptura()
    {
        try
        {
            DetenerCaptura();

            if (_dispositivosVideo != null && cbCamaras.SelectedIndex >= 0)
            {
                string monikerString = _dispositivosVideo[cbCamaras.SelectedIndex].MonikerString;
                _fuenteVideo = new VideoCaptureDevice(monikerString);
                _fuenteVideo.NewFrame += FuenteVideo_NewFrame;
                _fuenteVideo.Start();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo iniciar la cámara seleccionada: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FuenteVideo_NewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
            picPreview.BeginInvoke(new Action(() =>
            {
                picPreview.Image?.Dispose();
                picPreview.Image = frame;
            }));
        }
        catch { }
    }

    private void BtnCapturar_Click(object? sender, EventArgs e)
    {
        if (picPreview.Image == null)
        {
            MessageBox.Show("No hay señal de vídeo disponible para capturar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Generar ruta de guardado
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string rutaArchivo = Path.Combine(_rutaDestino, $"Foto_{timestamp}.jpg");

            // Clonar imagen actual y guardarla
            lock (picPreview)
            {
                using (Bitmap captura = new Bitmap(picPreview.Image))
                {
                    captura.Save(rutaArchivo, ImageFormat.Jpeg);
                }
            }

            RutaFotoGuardada = rutaArchivo;
            DetenerCaptura();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar la captura de foto: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        DetenerCaptura();
        base.OnFormClosing(e);
    }
}
