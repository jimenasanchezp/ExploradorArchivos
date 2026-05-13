using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ExploradorArchivos.Mp3
{
    public class MusicPlayerForm : Form
    {
        // === Kawaii Glass Minimalist Palette ===
        private static readonly Color RosaPastel = ColorTranslator.FromHtml("#FFD9E8");
        private static readonly Color Lila = ColorTranslator.FromHtml("#E6D4F8");
        private static readonly Color AzulCielo = ColorTranslator.FromHtml("#CDE7F0");
        private static readonly Color VerdeMenta = ColorTranslator.FromHtml("#CFF5E7");
        private static readonly Color BeigeClaro = ColorTranslator.FromHtml("#FFF5F9");
        private static readonly Color GrisSuave = ColorTranslator.FromHtml("#F9F9F9");

        private readonly GestorReproduccion _gestor;
        
        // UI Controls
        private PictureBox _picPortada = null!;
        private Label _lblTitulo = null!;
        private Label _lblArtista = null!;
        private Label _lblAlbum = null!;
        private Label _lblTiempoActual = null!;
        private Label _lblTiempoTotal = null!;
        private CustomTrackBar _trackProgreso = null!;
        private CustomTrackBar _trackVolumen = null!;
        
        private Button _btnAnterior = null!;
        private Button _btnPlayPause = null!;
        private Button _btnSiguiente = null!;
        private Button _btnShuffle = null!;
        private Button _btnRepeat = null!;
        
        private ListBox _lstCola = null!;
        private RichTextBox _rtbLetras = null!;
        private FlowLayoutPanel _flowCarpetas = null!;

        private Panel _pnlReproductor = null!;
        private Panel _pnlBiblioteca = null!;
        private Panel _pnlCarpetas = null!;
        private Panel _pnlLetras = null!;

        // Dragging
        private bool _dragging = false;
        private Point _startPoint = new Point(0, 0);

        public MusicPlayerForm(List<string> rutas, string? rutaInicial = null)
        {
            _gestor = new GestorReproduccion();
            InicializarComponentes();
            ConectarEventos();
            _gestor.CargarCola(rutas, rutaInicial);
            CargarCarpetasMusica();
        }

        private void InicializarComponentes()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(850, 550);
            this.BackColor = BeigeClaro;
            this.StartPosition = FormStartPosition.CenterScreen;
            try { this.Font = new Font("Inter", 9); } catch { this.Font = new Font("Segoe UI", 9); }

            this.Paint += (s, e) => DrawRetroBorder(e.Graphics, new Rectangle(0, 0, this.Width - 1, this.Height - 1), true);

            // === TITLE BAR ===
            Panel pnlTitleBar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Lila };
            pnlTitleBar.MouseDown += TitleBar_MouseDown;
            pnlTitleBar.MouseMove += TitleBar_MouseMove;
            pnlTitleBar.MouseUp += TitleBar_MouseUp;

            Label lblTitle = new Label { Text = "🌸 mp3 🎧 ✨", ForeColor = Color.FromArgb(45, 45, 45), Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold), AutoSize = true, Location = new Point(70, 6) };
            lblTitle.MouseDown += TitleBar_MouseDown;
            lblTitle.MouseMove += TitleBar_MouseMove;
            lblTitle.MouseUp += TitleBar_MouseUp;

            Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 10);
            btnClose.Click += (s, e) => this.Close();

            Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 30);
            btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            
            Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 50);
            btnMax.Click += (s, e) => {
                this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
            };

            pnlTitleBar.Controls.AddRange(new Control[] { lblTitle, btnMin, btnMax, btnClose });

            // === MENU BAR ===
            MenuStrip menu = new MenuStrip { BackColor = GrisSuave, Dock = DockStyle.Top, Padding = new Padding(2,2,0,2) };
            menu.Items.Add(new ToolStripMenuItem("Disc 💿"));
            menu.Items.Add(new ToolStripMenuItem("View 🪟"));
            menu.Items.Add(new ToolStripMenuItem("Options ⚙️"));
            menu.Items.Add(new ToolStripMenuItem("Help ❓"));

            // === SIDEBAR ===
            Panel pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 160, BackColor = GrisSuave, Padding = new Padding(10, 20, 10, 10) };
            pnlSidebar.Paint += (s, e) => {
                e.Graphics.DrawLine(new Pen(ColorTranslator.FromHtml("#DCDCDC")), pnlSidebar.Width - 1, 0, pnlSidebar.Width - 1, pnlSidebar.Height);
            };

            var btnMenuRep = CrearBotonSide("Reproductor 💖");
            var btnMenuBib = CrearBotonSide("Biblioteca 🧸");
            var btnMenuCarp = CrearBotonSide("Carpetas 🍧");
            var btnMenuLetras = CrearBotonSide("Letras 🎀");

            FlowLayoutPanel flowSide = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            flowSide.Controls.AddRange(new Control[] { btnMenuRep, btnMenuBib, btnMenuCarp, btnMenuLetras });
            
            Label lblStickerSide = new Label { Text = "🐰🌷", Font = new Font("Segoe UI", 24), AutoSize = true, Margin = new Padding(30, 100, 0, 0), BackColor = Color.Transparent };
            flowSide.Controls.Add(lblStickerSide);
            pnlSidebar.Controls.Add(flowSide);

            // === BOTTOM BAR ===
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 120, BackColor = GrisSuave };
            pnlBottom.Paint += (s, e) => {
                e.Graphics.DrawLine(new Pen(ColorTranslator.FromHtml("#DCDCDC")), 0, 0, pnlBottom.Width, 0);
            };

            _lblTiempoActual = new Label { Text = "0:00", Location = new Point(20, 15), Size = new Size(40, 20) };
            _trackProgreso = new CustomTrackBar { Size = new Size(720, 15), Location = new Point(65, 17), Value = 0 };
            _lblTiempoTotal = new Label { Text = "0:00", Location = new Point(790, 15), Size = new Size(40, 20) };

            _btnShuffle = CrearBotonControl("🔀", GrisSuave);
            _btnShuffle.Location = new Point(200, 50);
            _btnAnterior = CrearBotonControl("⏮️", AzulCielo);
            _btnAnterior.Location = new Point(270, 50);
            _btnPlayPause = CrearBotonControl("⏯️", RosaPastel);
            _btnPlayPause.Size = new Size(70, 45);
            _btnPlayPause.Location = new Point(340, 47);
            _btnSiguiente = CrearBotonControl("⏭️", AzulCielo);
            _btnSiguiente.Location = new Point(420, 50);
            _btnRepeat = CrearBotonControl("🔁", GrisSuave);
            _btnRepeat.Location = new Point(490, 50);

            Label lblVolSticker = new Label { Text = "🔊", Font = new Font("Segoe UI", 14), Location = new Point(650, 55), AutoSize = true };
            _trackVolumen = new CustomTrackBar { Size = new Size(120, 15), Location = new Point(685, 60), Value = 0.7 };

            pnlBottom.Controls.AddRange(new Control[] { 
                _lblTiempoActual, _trackProgreso, _lblTiempoTotal, 
                _btnShuffle, _btnAnterior, _btnPlayPause, _btnSiguiente, _btnRepeat,
                lblVolSticker, _trackVolumen 
            });

            // === MAIN CONTENT ===
            Panel pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            
            // 1. REPRODUCTOR PANEL
            _pnlReproductor = new Panel { Dock = DockStyle.Fill, Visible = true };
            
            Panel pnlCoverContainer = new Panel { Location = new Point(10, 10), Size = new Size(240, 240) };
            pnlCoverContainer.Paint += (s, e) => {
                e.Graphics.FillRectangle(new SolidBrush(Color.White), pnlCoverContainer.ClientRectangle);
                DrawRetroBorder(e.Graphics, new Rectangle(0,0, 239, 239), false);
            };
            _picPortada = new PictureBox { Location = new Point(4, 4), Size = new Size(232, 232), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
            pnlCoverContainer.Controls.Add(_picPortada);

            Panel pnlInfoContainer = new Panel { Location = new Point(280, 10), Size = new Size(350, 240) };
            Label lblDeco1 = new Label { Text = "🦋 Now Playing", Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold), Location = new Point(0, 0), AutoSize = true, ForeColor = Color.DimGray };
            Button btnEdit = CrearBotonTitle("✏️");
            btnEdit.Size = new Size(30, 25);
            btnEdit.Location = new Point(310, 0);
            btnEdit.Click += (s, e) => MessageBox.Show("Abrir edición de metadatos (WIP) 🌸"); // Placeholder

            Panel pnlTitle = CrearPanelInfo("Title:", 30, out _lblTitulo);
            Panel pnlArtist = CrearPanelInfo("Artist:", 90, out _lblArtista);
            Panel pnlAlbum = CrearPanelInfo("Album:", 150, out _lblAlbum);

            pnlInfoContainer.Controls.AddRange(new Control[] { lblDeco1, btnEdit, pnlTitle, pnlArtist, pnlAlbum });
            
            Label lblSticker1 = new Label { Text = "⭐", Font = new Font("Segoe UI", 18), Location = new Point(620, 220), AutoSize = true, BackColor = Color.Transparent };
            Label lblSticker2 = new Label { Text = "💿", Font = new Font("Segoe UI", 24), Location = new Point(15, 260), AutoSize = true, BackColor = Color.Transparent };
            Label lblSticker3 = new Label { Text = "🍓", Font = new Font("Segoe UI", 18), Location = new Point(350, 260), AutoSize = true, BackColor = Color.Transparent };
            
            _pnlReproductor.Controls.AddRange(new Control[] { pnlCoverContainer, pnlInfoContainer, lblSticker1, lblSticker2, lblSticker3 });

            // 2. BIBLIOTECA PANEL
            _pnlBiblioteca = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(10) };
            Label lblBib = new Label { Text = "🧸 Mi Biblioteca Musical", Dock = DockStyle.Top, Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold), Height = 30 };
            _lstCola = new ListBox { Dock = DockStyle.Fill, BackColor = Color.White, Font = new Font(this.Font.FontFamily, 10), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 35 };
            _lstCola.DrawItem += LstCola_DrawItem;
            Panel pnlListBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            pnlListBorder.Paint += (s, e) => DrawRetroBorder(e.Graphics, pnlListBorder.ClientRectangle, false);
            pnlListBorder.Controls.Add(_lstCola);
            _pnlBiblioteca.Controls.AddRange(new Control[] { pnlListBorder, lblBib });

            // 3. CARPETAS PANEL
            _pnlCarpetas = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(10) };
            Label lblCarp = new Label { Text = "🍧 Carpetas Locales", Dock = DockStyle.Top, Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold), Height = 30 };
            _flowCarpetas = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            Panel pnlFlowBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            pnlFlowBorder.Paint += (s, e) => DrawRetroBorder(e.Graphics, pnlFlowBorder.ClientRectangle, false);
            pnlFlowBorder.Controls.Add(_flowCarpetas);
            _pnlCarpetas.Controls.AddRange(new Control[] { pnlFlowBorder, lblCarp });

            // 4. LETRAS PANEL
            _pnlLetras = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(10) };
            Label lblLetras = new Label { Text = "🎀 Letras de la Canción", Dock = DockStyle.Top, Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold), Height = 30 };
            _rtbLetras = new RichTextBox { Dock = DockStyle.Fill, BackColor = VerdeMenta, BorderStyle = BorderStyle.None, ReadOnly = true, Font = new Font(this.Font.FontFamily, 11) };
            _rtbLetras.SelectionAlignment = HorizontalAlignment.Center;
            Panel pnlLetrasBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            pnlLetrasBorder.Paint += (s, e) => DrawRetroBorder(e.Graphics, pnlLetrasBorder.ClientRectangle, false);
            pnlLetrasBorder.Controls.Add(_rtbLetras);
            _pnlLetras.Controls.AddRange(new Control[] { pnlLetrasBorder, lblLetras });

            pnlMain.Controls.AddRange(new Control[] { _pnlReproductor, _pnlBiblioteca, _pnlCarpetas, _pnlLetras });

            this.Controls.Add(pnlMain);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(menu);
            this.Controls.Add(pnlTitleBar);

            // Side Events
            btnMenuRep.Click += (s, e) => MostrarPanel(_pnlReproductor);
            btnMenuBib.Click += (s, e) => MostrarPanel(_pnlBiblioteca);
            btnMenuCarp.Click += (s, e) => MostrarPanel(_pnlCarpetas);
            btnMenuLetras.Click += (s, e) => MostrarPanel(_pnlLetras);
        }

        private void MostrarPanel(Panel p)
        {
            _pnlReproductor.Visible = (p == _pnlReproductor);
            _pnlBiblioteca.Visible = (p == _pnlBiblioteca);
            _pnlCarpetas.Visible = (p == _pnlCarpetas);
            _pnlLetras.Visible = (p == _pnlLetras);
        }

        private void DrawRetroBorder(Graphics g, Rectangle bounds, bool raised)
        {
            using Pen borderPen = new Pen(ColorTranslator.FromHtml("#DCDCDC"), 1);
            g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        private Panel CrearPanelInfo(string title, int y, out Label lblValue)
        {
            Panel pnl = new Panel { Location = new Point(0, y), Size = new Size(340, 55) };
            Label lblDesc = new Label { Text = title, Location = new Point(0, 0), AutoSize = true, ForeColor = Color.DimGray, Font = new Font(this.Font.FontFamily, 8) };
            lblValue = new Label { Text = "...", Location = new Point(8, 22), Size = new Size(310, 20), Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold), AutoEllipsis = true };
            
            pnl.Paint += (s, e) => {
                var rect = new Rectangle(2, 18, 330, 30);
                e.Graphics.FillRectangle(new SolidBrush(Color.White), rect);
                DrawRetroBorder(e.Graphics, rect, false);
            };
            pnl.Controls.AddRange(new Control[] { lblDesc, lblValue });
            return pnl;
        }

        private Button CrearBotonTitle(string text)
        {
            Button btn = new Button {
                Text = text, Size = new Size(20, 20), FlatStyle = FlatStyle.Flat,
                BackColor = GrisSuave, Font = new Font(this.Font.FontFamily, 8, FontStyle.Bold),
                Padding = new Padding(0), Margin = new Padding(0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CrearBotonSemaforo(Color color, int x)
        {
            Button b = new Button { Location = new Point(x, 7), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
            b.FlatAppearance.BorderSize = 0;
            b.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.Clear(Lila);
                e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
                e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
            };
            return b;
        }

        private Button CrearBotonControl(string text, Color bgColor)
        {
            Button btn = new Button {
                Text = text, Size = new Size(50, 35), FlatStyle = FlatStyle.Flat,
                BackColor = bgColor, Font = new Font("Segoe UI Emoji", 14),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Paint += (s, e) => DrawRetroBorder(e.Graphics, new Rectangle(0,0, btn.Width - 1, btn.Height - 1), true);
            return btn;
        }

        private Button CrearBotonSide(string text)
        {
            Button btn = new Button {
                Text = text, Width = 140, Height = 45, FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft, BackColor = GrisSuave,
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(5, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // --- Drag Logic ---
        private void TitleBar_MouseDown(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _dragging = true; _startPoint = e.Location; } }
        private void TitleBar_MouseMove(object? sender, MouseEventArgs e) { if (_dragging) { this.Location = new Point(this.Location.X + e.X - _startPoint.X, this.Location.Y + e.Y - _startPoint.Y); } }
        private void TitleBar_MouseUp(object? sender, MouseEventArgs e) { _dragging = false; }


        // --- Player Logic ---
        private void ConectarEventos()
        {
            _btnPlayPause.Click += (s, e) => _gestor.TogglePlayPause();
            _btnSiguiente.Click += (s, e) => _gestor.Siguiente();
            _btnAnterior.Click += (s, e) => _gestor.Anterior();
            
            _btnShuffle.Click += (s, e) => {
                _gestor.ModoAleatorio = !_gestor.ModoAleatorio;
                _btnShuffle.BackColor = _gestor.ModoAleatorio ? VerdeMenta : GrisSuave;
            };

            _btnRepeat.Click += (s, e) => {
                _gestor.ModoRepetir = _gestor.ModoRepetir switch {
                    ModoRepetir.Desactivado => ModoRepetir.RepetirTodos,
                    ModoRepetir.RepetirTodos => ModoRepetir.RepetirUno,
                    _ => ModoRepetir.Desactivado
                };
                ActualizarIconoRepeat();
            };

            _trackProgreso.ValueChangedByUser += (val) => _gestor.Seek(val);
            _trackVolumen.ValueChangedByUser += (val) => _gestor.Volumen = (float)val;

            _lstCola.DoubleClick += (s, e) => {
                if (_lstCola.SelectedIndex >= 0) {
                    var cola = _gestor.ObtenerColaOrdenada();
                    _gestor.ReproducirPorIndice(cola[_lstCola.SelectedIndex].IndiceReal);
                }
            };

            _gestor.CancionCambiada += OnCancionCambiada;
            _gestor.PosicionActualizada += OnPosicionActualizada;
            _gestor.EstadoCambiado += OnEstadoCambiado;
        }

        private void ActualizarIconoRepeat()
        {
            _btnRepeat.Text = _gestor.ModoRepetir switch {
                ModoRepetir.RepetirUno => "🔂",
                _ => "🔁"
            };
            _btnRepeat.BackColor = _gestor.ModoRepetir != ModoRepetir.Desactivado ? VerdeMenta : GrisSuave;
        }

        private void OnCancionCambiada(Cancion cancion)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => OnCancionCambiada(cancion))); return; }
            _lblTitulo.Text = cancion.Titulo;
            _lblArtista.Text = cancion.Artista;
            _lblAlbum.Text = cancion.Album;
            _lblTiempoTotal.Text = cancion.DuracionTexto;

            _rtbLetras.Text = !string.IsNullOrEmpty(cancion.Letra) ? cancion.Letra : "💗 Buscando letra o no disponible 🌸";
            _rtbLetras.SelectAll();
            _rtbLetras.SelectionAlignment = HorizontalAlignment.Center;
            _rtbLetras.DeselectAll();

            _picPortada.Image?.Dispose();
            _picPortada.Image = cancion.Portada != null ? (Image)cancion.Portada.Clone() : null;
            ActualizarCola();
        }

        private void OnPosicionActualizada(TimeSpan actual, TimeSpan total)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => OnPosicionActualizada(actual, total))); return; }
            if (total.TotalSeconds > 0) _trackProgreso.Value = actual.TotalSeconds / total.TotalSeconds;
            _lblTiempoActual.Text = actual.ToString(@"m\:ss");
        }

        private void OnEstadoCambiado(bool repro) => _btnPlayPause.Text = repro ? "⏸️" : "⏯️";

        private void ActualizarCola()
        {
            _lstCola.BeginUpdate();
            _lstCola.Items.Clear();
            foreach (var item in _gestor.ObtenerColaOrdenada()) _lstCola.Items.Add(item.Cancion);
            if (_gestor.IndiceCola >= 0 && _gestor.IndiceCola < _lstCola.Items.Count)
                _lstCola.SelectedIndex = _gestor.IndiceCola;
            _lstCola.EndUpdate();
        }

        private void LstCola_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool isPlaying = e.Index == _gestor.IndiceCola;
            e.Graphics.FillRectangle(new SolidBrush(isPlaying ? RosaPastel : Color.White), e.Bounds);
            if (_lstCola.Items[e.Index] is Cancion cancion)
                e.Graphics.DrawString(cancion.Titulo, this.Font, Brushes.Black, e.Bounds.X + 5, e.Bounds.Y + 10);
        }

        private void CargarCarpetasMusica()
        {
            string musicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (!Directory.Exists(musicPath)) return;

            _flowCarpetas.Controls.Clear();
            var carpetas = Directory.GetDirectories(musicPath);

            foreach (var r in carpetas)
            {
                Button btn = new Button
                {
                    Text = "📁 " + Path.GetFileName(r),
                    Size = new Size(180, 80),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = GrisSuave,
                    Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold),
                    Margin = new Padding(10)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Paint += (s, e) => DrawRetroBorder(e.Graphics, new Rectangle(0,0, btn.Width - 1, btn.Height - 1), true);
                
                btn.Click += (s, e) => {
                    var mp3s = Directory.GetFiles(r, "*.mp3", SearchOption.TopDirectoryOnly).ToList();
                    if (mp3s.Count > 0) {
                        _gestor.CargarCola(mp3s);
                        MostrarPanel(_pnlReproductor);
                    } else {
                        MessageBox.Show("No se encontraron archivos MP3 en esta carpeta. 😢");
                    }
                };
                _flowCarpetas.Controls.Add(btn);
            }
        }
    }
}