using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExploradorArchivos.AppCamara;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppCapturaPantalla
{
    /// <summary>
    /// Interfaz del módulo de Captura de Pantalla y Grabación de Pantalla.
    /// Permite capturar la pantalla completa o una región seleccionada como imagen PNG,
    /// y grabar la pantalla (completa o región) como video MP4 mediante <c>AviGrabador</c> + FFmpeg.
    /// Sigue el mismo estilo visual que <see cref="ExploradorArchivos.AppCamara.AppCamaraForm"/>.
    /// </summary>
    public partial class AppCapturaPantallaForm : Form
    {
        // ─── Grabación de video ──────────────────────────────────────────────────
        private AviGrabador?                   _grabador          = null;
        private bool                           _grabando          = false;
        private int                            _segundosGrabacion = 0;
        private System.Windows.Forms.Timer     _timerGrabacion    = default!;
        private System.Diagnostics.Stopwatch?  _stopwatch;
        private string                         _rutaAviTemporal   = string.Empty;
        private string                         _rutaMp4Final      = string.Empty;
        private CancellationTokenSource?       _ctsGrabacion      = null;

        // ─── Región seleccionada ─────────────────────────────────────────────────
        /// <summary>Región activa a capturar / grabar. Si es Empty se usa la pantalla completa.</summary>
        private Rectangle _regionActiva = Rectangle.Empty;

        // ─── Timer de captura de frames durante grabación ─────────────────────
        // Reemplazado por BucleCapturaAsync

        // ─── Controles UI ────────────────────────────────────────────────────────
        private Panel      _pnlTop         = default!;
        private Panel      _pnlBottom      = default!;
        private PictureBox _picPreview     = default!;
        private Button     _btnCapturar    = default!;
        private Button     _btnGrabar      = default!;
        private Label      _lblTimer       = default!;
        private Label      _lblModo        = default!;
        private RadioButton _rbCompleta    = default!;
        private RadioButton _rbRegion      = default!;
        private Label      _lblRegionInfo  = default!;

        // ─── Resultado ───────────────────────────────────────────────────────────
        /// <summary>Ruta del PNG guardado al capturar. <c>null</c> si no se ha capturado aún.</summary>
        public string? RutaCapturaGuardada { get; private set; }
        /// <summary>Ruta del MP4/AVI guardado al terminar la grabación. <c>null</c> si no se ha grabado aún.</summary>
        public string? RutaVideoGuardado   { get; private set; }

        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Inicializa el formulario: construye los controles visuales, crea los timers
        /// y muestra una vista previa del escritorio en el <see cref="PictureBox"/>.
        /// </summary>
        public AppCapturaPantallaForm()
        {
            InicializarComponentes();
            InicializarTimers();
            ActualizarPreview();
        }

        // ════════════════════════════════════════════════════════════════════════
        #region Inicialización UI

        /// <summary>
        /// Construye dinámicamente todos los controles del formulario: barra superior
        /// con semáforos, vista previa del escritorio (<see cref="PictureBox"/>), panel
        /// inferior con selector de modo, etiqueta de timer y botones de acción.
        /// </summary>
        private void InicializarComponentes()
        {
            ThemeRenderer.ApplyTheme(this);
            this.Text            = "Captura de Pantalla";
            this.Size            = new Size(720, 560);
            this.MinimumSize     = new Size(620, 480);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition   = FormStartPosition.CenterParent;

            // ── Barra Superior ──────────────────────────────────────────────────
            _pnlTop = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 65,
                BackColor = ThemeRenderer.SecondaryBg,
                Padding   = new Padding(80, 10, 10, 10)
            };
            _pnlTop.Paint += (s, e) =>
                ThemeRenderer.DrawClassicBorder(e.Graphics, _pnlTop.ClientRectangle, true);

            // Arrastrar ventana
            bool isDragging = false; Point lastCursor = Point.Empty;
            _pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
            _pnlTop.MouseMove += (s, e) => { if (isDragging) this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); };
            _pnlTop.MouseUp   += (s, e) => isDragging = false;

            ConfigurarSemaforos();

            var lblTitulo = new Label
            {
                Text      = "🖥️ CAPTURA & GRABACIÓN DE PANTALLA",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("MS Sans Serif", 10, FontStyle.Bold),
                ForeColor = ThemeRenderer.MainText
            };
            _pnlTop.Controls.Add(lblTitulo);

            // ── Vista Previa ─────────────────────────────────────────────────────
            _picPreview = new PictureBox
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                SizeMode  = PictureBoxSizeMode.Zoom
            };

            // ── Panel Inferior ────────────────────────────────────────────────────
            _pnlBottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 130,
                BackColor = ThemeRenderer.MainBg,
                Padding   = new Padding(15)
            };
            _pnlBottom.Paint += (s, e) =>
                ThemeRenderer.DrawClassicBorder(e.Graphics, _pnlBottom.ClientRectangle, true);

            // ── Fila 1: Modo de captura ──────────────────────────────────────────
            _lblModo = new Label
            {
                Text      = "Modo de captura:",
                Location  = new Point(15, 8),
                AutoSize  = true,
                Font      = new Font("MS Sans Serif", 8, FontStyle.Bold),
                ForeColor = ThemeRenderer.SecondaryText
            };

            _rbCompleta = new RadioButton
            {
                Text      = "🖥️ Pantalla completa",
                Location  = new Point(15, 26),
                AutoSize  = true,
                Checked   = true,
                Font      = new Font("MS Sans Serif", 9),
                ForeColor = ThemeRenderer.MainText
            };
            _rbCompleta.CheckedChanged += (s, e) =>
            {
                if (_rbCompleta.Checked)
                {
                    _regionActiva = Rectangle.Empty;
                    ActualizarEtiquetaRegion();
                    ActualizarPreview();
                }
            };

            _rbRegion = new RadioButton
            {
                Text      = "✂️ Seleccionar región...",
                Location  = new Point(175, 26),
                AutoSize  = true,
                Font      = new Font("MS Sans Serif", 9),
                ForeColor = ThemeRenderer.MainText
            };
            _rbRegion.CheckedChanged += async (s, e) =>
            {
                if (_rbRegion.Checked) await SeleccionarRegion();
            };

            _lblRegionInfo = new Label
            {
                Text      = "",
                Location  = new Point(340, 29),
                AutoSize  = true,
                Font      = new Font("MS Sans Serif", 8),
                ForeColor = Color.FromArgb(80, 140, 200)
            };

            // ── Timer (visible solo al grabar) ────────────────────────────────────
            _lblTimer = new Label
            {
                Text      = "🔴 00:00",
                Location  = new Point(580, 29),
                AutoSize  = true,
                Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 50, 50),
                Visible   = false
            };

            // ── Fila 2: Botones ───────────────────────────────────────────────────
            _btnCapturar = new Button
            {
                Text      = "📸 Capturar",
                Size      = new Size(140, 42),
                Anchor    = AnchorStyles.Top,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeRenderer.Accent,
                ForeColor = Color.White,
                Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnCapturar.FlatAppearance.BorderSize = 0;
            _btnCapturar.Click += BtnCapturar_Click;
            _btnCapturar.Paint += (s, e) =>
                ThemeRenderer.DrawClassicBorder(e.Graphics, _btnCapturar.ClientRectangle, true);

            _btnGrabar = new Button
            {
                Text      = "🔴 Grabar",
                Size      = new Size(140, 42),
                Anchor    = AnchorStyles.Top,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 139, 34),
                ForeColor = Color.White,
                Font      = new Font("MS Sans Serif", 9, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnGrabar.FlatAppearance.BorderSize = 0;
            _btnGrabar.Click += BtnGrabar_Click;
            _btnGrabar.Paint += (s, e) =>
                ThemeRenderer.DrawClassicBorder(e.Graphics, _btnGrabar.ClientRectangle, true);

            _pnlBottom.Controls.AddRange(new Control[]
            {
                _lblModo, _rbCompleta, _rbRegion, _lblRegionInfo, _lblTimer,
                _btnCapturar, _btnGrabar
            });

            this.Controls.Add(_picPreview);
            this.Controls.Add(_pnlTop);
            this.Controls.Add(_pnlBottom);

            this.SizeChanged += (s, e) => ReposicionarBotones();
            ReposicionarBotones();
        }

        /// <summary>
        /// Centra horizontalmente los botones "Capturar" y "Grabar" cada vez que
        /// la ventana cambia de tamaño para mantener el layout simétrico.
        /// </summary>
        private void ReposicionarBotones()
        {
            int mitad = this.Width / 2;
            _btnCapturar.Location = new Point(mitad - 148, 80);
            _btnGrabar.Location   = new Point(mitad + 8,   80);
        }

        /// <summary>
        /// Crea el timer del módulo:<br/>
        /// • <b>_timerGrabacion</b> (1 000 ms): actualiza el contador MM:SS en la UI.<br/>
        /// </summary>
        private void InicializarTimers()
        {
            // Timer de contador de tiempo (1 segundo)
            _timerGrabacion          = new System.Windows.Forms.Timer();
            _timerGrabacion.Interval = 1000;
            _timerGrabacion.Tick    += TimerGrabacion_Tick;
        }

        /// <summary>
        /// Crea los tres botones de semáforo estilo macOS (🔴 cerrar, 🟡 minimizar, 🟢 maximizar)
        /// y los añade al panel superior izquierdo del formulario.
        /// </summary>
        private void ConfigurarSemaforos()
        {
            var pnlSemaforos = new Panel
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
            _pnlTop.Controls.Add(pnlSemaforos);
        }

        /// <summary>
        /// Crea un botón circular de semáforo con renderizado personalizado en el evento <c>Paint</c>.
        /// </summary>
        /// <param name="color">Color del círculo (rojo, amarillo o verde).</param>
        /// <param name="x">Posición X dentro del panel de semáforos.</param>
        /// <returns>El <see cref="Button"/> configurado listo para añadir al panel.</returns>
        private Button CrearBotonSemaforo(Color color, int x)
        {
            var b = new Button
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
                e.Graphics.Clear(_pnlTop.BackColor);
                e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
                e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
            };
            return b;
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        #region Vista Previa

        /// <summary>
        /// Actualiza el PictureBox con una miniatura de la pantalla o región activa.
        /// </summary>
        private void ActualizarPreview()
        {
            try
            {
                Rectangle region = _regionActiva.IsEmpty
                    ? (Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080))
                    : _regionActiva;

                var bmp = ScreenCaptureService.CapturarFrame(region);

                _picPreview.Image?.Dispose();
                _picPreview.Image = bmp;
            }
            catch { /* ignorar errores de preview */ }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        #region Selección de Región

        /// <summary>
        /// Abre el overlay de selección de región (<see cref="RegionSelectorForm"/>).
        /// Minimiza este formulario antes de mostrar el overlay para que no interfiera
        /// con la captura. Si el usuario cancela, restablece el modo a "Pantalla completa".
        /// </summary>
        private async Task SeleccionarRegion()
        {
            if (_grabando)
            {
                MessageBox.Show("Detén la grabación antes de cambiar la región.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rbCompleta.Checked = true;
                return;
            }

            // Minimizar de forma no bloqueante para que la ventana desaparezca antes del overlay
            this.WindowState = FormWindowState.Minimized;
            await Task.Delay(300);

            using var selector = new RegionSelectorForm();
            var resultado = selector.ShowDialog();

            this.WindowState = FormWindowState.Normal;
            this.BringToFront();

            if (resultado == DialogResult.OK && !selector.RegionSeleccionada.IsEmpty)
            {
                _regionActiva = selector.RegionSeleccionada;
                ActualizarEtiquetaRegion();
                ActualizarPreview();
            }
            else
            {
                // El usuario canceló → volver a pantalla completa
                _regionActiva = Rectangle.Empty;
                _rbCompleta.Checked = true;
                ActualizarEtiquetaRegion();
            }
        }

        /// <summary>
        /// Actualiza <c>_lblRegionInfo</c> con las dimensiones y posición de la región
        /// seleccionada, o la deja vacía si se usa el modo de pantalla completa.
        /// </summary>
        private void ActualizarEtiquetaRegion()
        {
            if (_regionActiva.IsEmpty)
                _lblRegionInfo.Text = "";
            else
                _lblRegionInfo.Text = $"[{_regionActiva.Width}×{_regionActiva.Height} en ({_regionActiva.X},{_regionActiva.Y})]";
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        #region Captura de Pantalla

        /// <summary>
        /// Manejador del botón "📸 Capturar". Minimiza el form con <c>await Task.Delay</c>
        /// para que no aparezca en la imagen, toma la captura mediante <see cref="ScreenCaptureService"/>
        /// y guarda el PNG en la carpeta Mis Imágenes. Al terminar restaura la ventana y la activa.
        /// </summary>
        private async void BtnCapturar_Click(object? sender, EventArgs e)
        {
            if (_grabando)
            {
                MessageBox.Show("Detén la grabación antes de tomar una captura.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Si eligió región pero no la ha seleccionado aún, forzar selección
                if (_rbRegion.Checked && _regionActiva.IsEmpty)
                {
                    await SeleccionarRegion();
                    if (_regionActiva.IsEmpty) return;
                }

                // Minimizar de forma no bloqueante para no aparecer en la captura
                this.WindowState = FormWindowState.Minimized;
                await Task.Delay(250);

                string ruta = _regionActiva.IsEmpty
                    ? ScreenCaptureService.CapturarPantallaCompleta()
                    : ScreenCaptureService.CapturarRegion(_regionActiva);

                this.WindowState    = FormWindowState.Normal;
                this.BringToFront();
                this.Activate();
                RutaCapturaGuardada = ruta;

                ActualizarPreview();

                var res = MessageBox.Show(
                    $"✅ Captura guardada en:\n{ruta}\n\n¿Deseas cerrar el módulo?",
                    "Captura Exitosa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (res == DialogResult.Yes)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                this.WindowState = FormWindowState.Normal;
                MessageBox.Show($"Error al capturar pantalla:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        #region Grabación de Pantalla

        /// <summary>
        /// Manejador del botón "🔴 Grabar / ⏹ Detener". Alterna entre iniciar
        /// y detener la sesión de grabación según el estado actual de <c>_grabando</c>.
        /// </summary>
        private async void BtnGrabar_Click(object? sender, EventArgs e)
        {
            if (!_grabando)
                await IniciarGrabacion();
            else
                DetenerGrabacion();
        }

        /// <summary>
        /// Inicia la grabación de pantalla: crea el <see cref="AviGrabador"/>,
        /// arranca el <c>Stopwatch</c> de referencia temporal, activa ambos timers
        /// y bloquea los controles de modo para evitar cambios durante la grabación.
        /// </summary>
        private async Task IniciarGrabacion()
        {
            // Si eligió región pero no la ha seleccionado aún, forzar selección
            if (_rbRegion.Checked && _regionActiva.IsEmpty)
            {
                await SeleccionarRegion();
                if (_regionActiva.IsEmpty) return;
            }

            try
            {
                Rectangle region = _regionActiva.IsEmpty
                    ? (Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080))
                    : _regionActiva;

                _grabador = ScreenCaptureService.IniciarGrabacion(
                    region, out _rutaAviTemporal, out _rutaMp4Final);

                _grabando          = true;
                _segundosGrabacion = 0;
                _stopwatch         = System.Diagnostics.Stopwatch.StartNew();

                // Actualizar UI
                _btnGrabar.Text      = "⏹ Detener";
                _btnGrabar.BackColor = Color.FromArgb(180, 30, 30);
                _btnCapturar.Enabled = false;
                _rbCompleta.Enabled  = false;
                _rbRegion.Enabled    = false;
                _lblTimer.Visible    = true;
                _lblTimer.Text       = "🔴 00:00";
                _lblTimer.ForeColor  = Color.FromArgb(220, 50, 50);

                _timerGrabacion.Start();
                
                _ctsGrabacion = new CancellationTokenSource();
                _ = BucleCapturaAsync(_ctsGrabacion.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar grabación:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LimpiarGrabador();
            }
        }

        /// <summary>
        /// Detiene la grabación: para los timers, cierra el <see cref="AviGrabador"/> y lanza
        /// la conversión asíncrona AVI → MP4 mediante FFmpeg. Muestra "Convirtiendo..." en la UI
        /// durante el proceso. Si FFmpeg no está disponible, el AVI se conserva como fallback.
        /// </summary>
        private async void DetenerGrabacion()
        {
            _ctsGrabacion?.Cancel();
            _ctsGrabacion?.Dispose();
            _ctsGrabacion = null;
            
            _timerGrabacion.Stop();
            _stopwatch?.Stop();
            _stopwatch  = null;
            _grabando   = false;

            LimpiarGrabador();

            // Actualizar UI durante conversión
            _btnGrabar.Text      = "🔴 Grabar";
            _btnGrabar.BackColor = Color.FromArgb(34, 139, 34);
            _btnGrabar.Enabled   = false;
            _btnCapturar.Enabled = false;
            _lblTimer.Text       = "⏳ Convirtiendo a MP4...";
            _lblTimer.ForeColor  = Color.FromArgb(200, 150, 0);
            _lblTimer.Visible    = true;

            bool convertido = await ScreenCaptureService.ConvertirAviAMp4Async(
                _rutaAviTemporal, _rutaMp4Final);

            RutaVideoGuardado = convertido ? _rutaMp4Final : _rutaAviTemporal;

            // Restaurar UI
            _btnGrabar.Enabled   = true;
            _btnCapturar.Enabled = true;
            _rbCompleta.Enabled  = true;
            _rbRegion.Enabled    = true;
            _lblTimer.Visible    = false;
            _lblTimer.ForeColor  = Color.FromArgb(220, 50, 50);

            if (!string.IsNullOrEmpty(RutaVideoGuardado) && File.Exists(RutaVideoGuardado))
            {
                string formato = convertido ? "MP4" : "AVI (FFmpeg no encontrado)";
                var res = MessageBox.Show(
                    $"✅ Video guardado como {formato}:\n{RutaVideoGuardado}\n\n¿Deseas cerrar el módulo?",
                    "Grabación Completa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (res == DialogResult.Yes)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
        }

        /// <summary>
        /// Libera el <see cref="AviGrabador"/> activo de forma segura delegando
        /// el cierre al <see cref="ScreenCaptureService.DetenerGrabacion"/>.
        /// </summary>
        private void LimpiarGrabador()
        {
            if (_grabador != null)
            {
                ScreenCaptureService.DetenerGrabacion(_grabador);
                _grabador = null;
            }
        }

        /// <summary>
        /// Callback del timer de 1 segundo. Incrementa <c>_segundosGrabacion</c>
        /// y actualiza la etiqueta "🔴 MM:SS" visible mientras la grabación está activa.
        /// </summary>
        private void TimerGrabacion_Tick(object? sender, EventArgs e)
        {
            _segundosGrabacion++;
            int min = _segundosGrabacion / 60;
            int seg = _segundosGrabacion % 60;
            _lblTimer.Text = $"🔴 {min:D2}:{seg:D2}";
        }

        /// <summary>
        /// Bucle asíncrono para la captura de frames (~15 fps). Captura un frame del área activa
        /// y lo escribe en el <see cref="AviGrabador"/> dentro de un Task para no bloquear la UI.
        /// </summary>
        private async Task BucleCapturaAsync(CancellationToken token)
        {
            var regionLocal = _regionActiva.IsEmpty
                ? (Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080))
                : _regionActiva;

            await Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && _grabador != null)
                {
                    var swFrame = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var elapsedLocal = _stopwatch?.Elapsed ?? TimeSpan.Zero;
                        using Bitmap frame = ScreenCaptureService.CapturarFrame(regionLocal);
                        ScreenCaptureService.EscribirFrame(_grabador, frame, elapsedLocal);
                    }
                    catch { /* ignorar frames perdidos */ }

                    swFrame.Stop();
                    int msRestantes = 66 - (int)swFrame.ElapsedMilliseconds;
                    
                    if (msRestantes > 0)
                    {
                        try
                        {
                            await Task.Delay(msRestantes, token);
                        }
                        catch (TaskCanceledException) { break; }
                    }
                }
            }, token);
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Limpia todos los recursos al cerrar el formulario: detiene los timers,
        /// libera el <see cref="AviGrabador"/> si hay grabación en curso, elimina
        /// el archivo AVI temporal y libera la imagen del <see cref="PictureBox"/>.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_grabando)
            {
                _ctsGrabacion?.Cancel();
                _timerGrabacion.Stop();
                _grabando = false;
                LimpiarGrabador();
                try { if (File.Exists(_rutaAviTemporal)) File.Delete(_rutaAviTemporal); } catch { }
            }

            _ctsGrabacion?.Dispose();
            _timerGrabacion.Dispose();
            _picPreview.Image?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
