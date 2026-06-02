using ExploradorArchivos.UI; // Importa utilidades de diseño y temas personalizados del explorador.
using Microsoft.Web.WebView2.WinForms; // Importa el control de navegador WebView2 para WinForms.
using System; // Importa tipos fundamentales y excepciones del sistema de .NET.
using System.Drawing; // Importa tipos de GDI+ para manipulación de gráficos, colores y fuentes.
using System.IO; // Importa clases para entrada y salida de archivos y directorios.
using System.Windows.Forms; // Importa clases para interfaces de usuario basadas en Windows Forms.
using System.Collections.Generic; // Importa colecciones genéricas como listas y pilas.
using System.Linq; // Importa LINQ para realizar consultas y operaciones funcionales concisas.

namespace ExploradorArchivos.AppFoto; // Define el espacio de nombres para el módulo de visualización y edición de fotos.

/// <summary>
/// Interfaz gráfica principal del módulo "App Foto".
/// Soporta visualización con zoom dinámico, edición (recorte, dibujo, filtros), 
/// ajuste en tiempo real (brillo, contraste) y gestión de metadatos GPS a través de un mapa integrado.
/// </summary>
public partial class AppFotoForm : Form // Declara la clase del formulario heredando de Form.
{
    private enum ToolMode { None, Draw, Text, Crop } // Define los modos de herramienta disponibles en el editor.
    private ToolMode _currentMode = ToolMode.None; // Modo de herramienta activo actualmente.
    private readonly List<Image> _undoStack = new List<Image>(); // Lista para gestionar el historial de deshacer (limitada para conservar memoria).

    private readonly string _rutaFoto; // Ruta del archivo de foto cargado en el disco.
    private AppFotoMetadata _metadata = default!; // Objeto para almacenar y leer metadatos EXIF de la foto.
    private Image _imagenOriginal = default!; // Referencia a la imagen original cargada inicialmente.
    private Image _imagenActual = default!; // Referencia a la imagen con modificaciones en tiempo real.

    // Controles de interfaz gráfica
    private Panel pnlTop = default!; // Panel superior para controles de la barra de título y botones.
    private FlowLayoutPanel flowButtons = default!; // Contenedor dinámico de botones de edición.
    private SplitContainer splitMain = default!; // Divisor principal que separa el visor del sidebar de ajustes.
    private PictureBox picPhoto = default!; // Lienzo/visor interactivo de la foto.
    private TabControl tabSidebar = default!; // Control de pestañas lateral.
    private TabPage tabAjustes = default!; // Pestaña de ajustes de color y filtros.
    private TabPage tabInfo = default!; // Pestaña de información GPS y metadatos con mapa.
    private WebView2 webMap = default!; // Control web para renderizar el mapa interactivo (OpenStreetMap/Leaflet).
    private Label lblMetaInfo = default!; // Label para mostrar información textual de metadatos.
    private Button btnSetLocation = default!; // Botón para registrar o editar la ubicación en el mapa.

    // Sliders de ajuste de color y luz
    private TrackBar trkBrillo = default!; // Control deslizante para el brillo.
    private TrackBar trkContraste = default!; // Control deslizante para el contraste.
    private TrackBar trkSaturacion = default!; // Control deslizante para la saturación de colores.
    private TrackBar trkLuces = default!; // Control deslizante para la exposición de luces altas.
    private TrackBar trkSombras = default!; // Control deslizante para la exposición de las sombras.

    // Estado del editor interactivo
    private Point _startPoint; // Almacena el punto inicial de inicio del arrastre del mouse.
    private Rectangle _cropRect; // Rectángulo del área seleccionada para el recorte.
    private bool _isSelecting = false; // Bandera que indica si el usuario está arrastrando para seleccionar.
    private Color _drawColor = Color.HotPink; // Color por defecto para las pinceladas y texto.
    private float _penWidth = 5f; // Ancho del pincel utilizado en el modo dibujo.
    private bool _isAdjusting = false; // Evita registrar múltiples pasos de deshacer intermedios al arrastrar un trackbar.

    // Constructor que inicializa el formulario con la ruta de la foto a cargar.
    public AppFotoForm(string ruta)
    {
        _rutaFoto = ruta; // Asigna la ruta de la foto recibida.
        InitializeCustomComponents(); // Configura todos los controles dinámicos de la UI.
        CargarFoto(); // Lee la imagen del disco y procesa metadatos.
    }

    /// <summary>
    /// Configura dinámicamente todos los controles de la interfaz (botones, sliders, lienzo)
    /// omitiendo el uso del diseñador visual de Visual Studio en favor de código procedimental escalable.
    /// </summary>
    private void InitializeCustomComponents()
    {
        ThemeRenderer.ApplyTheme(this); // Aplica el tema personalizado (oscuro/premium) al formulario.
        this.Text = "App Foto - Premium Studio"; // Establece el título de la ventana.
        this.Size = new Size(1200, 800); // Establece el tamaño inicial de la ventana.
        this.MinimumSize = new Size(800, 600); // Define los límites mínimos de reescalado.
        this.StartPosition = FormStartPosition.CenterScreen; // Centra la ventana en la pantalla del usuario.
        this.FormBorderStyle = FormBorderStyle.None; // Elimina el borde estándar de Windows para usar un diseño minimalista.

        // Barra Superior Responsiva (80px de padding izquierdo para los semáforos)
        pnlTop = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = ThemeRenderer.SecondaryBg, Padding = new Padding(80, 10, 10, 10) }; // Inicializa el panel superior.
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTop.ClientRectangle, true); // Dibuja el borde clásico del panel superior.
        
        // Estado para permitir arrastrar la ventana sin bordes
        bool isDragging = false; // Flag para rastrear si se está arrastrando.
        Point lastCursor = Point.Empty; // Posición previa del cursor de mouse.
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } }; // Activa el arrastre al presionar click izquierdo.
        pnlTop.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } }; // Desplaza el formulario en base al delta de movimiento.
        pnlTop.MouseUp += (s, e) => { isDragging = false; }; // Finaliza el estado de arrastre.

        ConfigurarSemaforos(); // Inicializa los botones de control de la ventana (cerrar, min, max).

        flowButtons = new FlowLayoutPanel { // Configura el panel horizontal de herramientas.
            Dock = DockStyle.Fill, 
            FlowDirection = FlowDirection.LeftToRight, 
            WrapContents = true,
            AutoScroll = true
        };

        // Registra los botones del editor de forma secuencial
        AgregarBotonEditor("✂️ Recorte", () => _currentMode = ToolMode.Crop); // Botón para habilitar recorte.
        AgregarBotonEditor("↩️ Giro L", () => Rotar(RotateFlipType.Rotate270FlipNone)); // Botón para rotar a la izquierda.
        AgregarBotonEditor("↪️ Giro R", () => Rotar(RotateFlipType.Rotate90FlipNone)); // Botón para rotar a la derecha.
        AgregarBotonEditor("🌑 B&N", () => AplicarFiltro("BN")); // Botón para aplicar filtro a escala de grises.
        AgregarBotonEditor("✏️ Dibujar", () => _currentMode = ToolMode.Draw); // Botón para pintar a mano alzada.
        AgregarBotonEditor("ABC Texto", () => _currentMode = ToolMode.Text); // Botón para insertar texto.
        AgregarBotonEditor("🔙 Deshacer", Deshacer); // Botón para revertir cambios.
        AgregarBotonEditor("💾 Guardar", GuardarImagen); // Botón para exportar la imagen final.

        pnlTop.Controls.Add(flowButtons); // Agrega el panel de botones a la barra superior.

        // Configura el contenedor divisor principal de la ventana
        splitMain = new SplitContainer { 
            Dock = DockStyle.Fill, 
            SplitterDistance = 850,
            BorderStyle = BorderStyle.None 
        };

        // Inicializa el PictureBox visor de fotos con fondo oscuro y cursor de cruz
        picPhoto = new PictureBox { 
            SizeMode = PictureBoxSizeMode.Zoom, 
            Dock = DockStyle.Fill, 
            BackColor = Color.FromArgb(40, 40, 40),
            Cursor = Cursors.Cross
        };
        picPhoto.MouseDown += PicPhoto_MouseDown; // Evento click de mouse para iniciar dibujo/recorte.
        picPhoto.MouseMove += PicPhoto_MouseMove; // Evento movimiento para pintar o actualizar el rectángulo de selección.
        picPhoto.MouseUp += PicPhoto_MouseUp; // Evento liberar botón del mouse para finalizar acciones.
        picPhoto.Paint += PicPhoto_Paint; // Evento de renderizado personalizado para dibujar el rectángulo guía en pantalla.

        splitMain.Panel1.Controls.Add(picPhoto); // Agrega el lienzo al panel izquierdo.

        // Sidebar con pestañas para Ajustes e Información
        tabSidebar = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal }; // Inicializa el contenedor de pestañas.
        tabAjustes = new TabPage("🎨 Ajustes"); // Inicializa la pestaña de ajustes visuales.
        tabInfo = new TabPage("📍 Info/Mapa"); // Inicializa la pestaña de metadatos y GPS.
        
        tabAjustes.BackColor = ThemeRenderer.SecondaryBg; // Define el color de fondo para ajustes.
        tabInfo.BackColor = ThemeRenderer.SecondaryBg; // Define el color de fondo para la info.
        
        ConfigurarPanelAjustes(); // Construye los controles de brillo, contraste, etc.
        ConfigurarPanelInfo(); // Construye el visor HTML de mapas y metadatos.

        tabSidebar.TabPages.Add(tabAjustes); // Agrega la pestaña al contenedor.
        tabSidebar.TabPages.Add(tabInfo); // Agrega la pestaña al contenedor.
        splitMain.Panel2.Controls.Add(tabSidebar); // Agrega la barra lateral al panel derecho.

        this.Controls.Add(splitMain); // Agrega el divisor principal al formulario.
        this.Controls.Add(pnlTop); // Agrega el panel superior al formulario.
    }

    /// <summary>
    /// Construye dinámicamente el panel de ajustes de imagen (brillo, contraste, saturación)
    /// utilizando un diseño basado en <c>TableLayoutPanel</c> para alineación automática.
    /// </summary>
    private void ConfigurarPanelAjustes()
    {
        TableLayoutPanel tlp = new TableLayoutPanel { // Contenedor en cuadrícula vertical.
            Dock = DockStyle.Top, 
            ColumnCount = 1, 
            RowCount = 12, 
            AutoSize = true, 
            Padding = new Padding(10) 
        };

        // Genera los deslizadores de ajuste de imagen asignándolos a sus variables respectivas
        trkBrillo = CrearSliderAjuste(tlp, "☀️ Brillo", -100, 100, 0); // Slider de brillo.
        trkContraste = CrearSliderAjuste(tlp, "🌗 Contraste", 0, 200, 100); // Slider de contraste.
        trkSaturacion = CrearSliderAjuste(tlp, "🌈 Saturación", 0, 200, 100); // Slider de saturación.
        trkLuces = CrearSliderAjuste(tlp, "💡 Luces", -100, 100, 0); // Slider de luces.
        trkSombras = CrearSliderAjuste(tlp, "🌑 Sombras", -100, 100, 0); // Slider de sombras.

        Button btnReset = new Button { // Botón para resetear todos los ajustes aplicados.
            Text = "🔄 Resetear Ajustes", 
            Dock = DockStyle.Top, 
            Height = 40, 
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Margin = new Padding(0, 20, 0, 0)
        };
        btnReset.Click += (s, e) => ResetearAjustes(); // Enlaza el evento click al reset de sliders.
        tlp.Controls.Add(btnReset); // Agrega el botón de reset a la cuadrícula.

        tabAjustes.Controls.Add(tlp); // Agrega la cuadrícula a la pestaña de ajustes.
    }

    // Crea un TrackBar slider estilizado para un ajuste en particular y lo añade al layout parent.
    private TrackBar CrearSliderAjuste(TableLayoutPanel parent, string nombre, int min, int max, int val)
    {
        Label lbl = new Label { Text = nombre, AutoSize = true, Margin = new Padding(0, 10, 0, 0), Font = new Font("MS Sans Serif", 8, FontStyle.Bold) }; // Etiqueta del slider.
        TrackBar trk = new TrackBar { Minimum = min, Maximum = max, Value = val, TickStyle = TickStyle.None, Dock = DockStyle.Top, Height = 30 }; // TrackBar.
        
        trk.ValueChanged += (s, e) => AplicarAjustesTiempoReal(); // Ejecuta correcciones de imagen al cambiar valor.
        trk.MouseDown += (s, e) => { if (!_isAdjusting) { GuardarEstadoParaDeshacer(); _isAdjusting = true; } }; // Al presionar click, guarda el estado previo en historial.
        trk.MouseUp += (s, e) => { _isAdjusting = false; }; // Finaliza el ajuste.

        parent.Controls.Add(lbl); // Agrega etiqueta a la UI.
        parent.Controls.Add(trk); // Agrega slider a la UI.
        return trk; // Retorna el TrackBar creado.
    }

    /// <summary>
    /// Configura la pestaña de información, incrustando el control <c>WebView2</c> para mostrar el mapa
    /// interactivo de la ubicación y el panel de etiquetas con metadatos de la cámara.
    /// </summary>
    private void ConfigurarPanelInfo()
    {
        Panel pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) }; // Panel contenedor de información.
        
        lblMetaInfo = new Label { Dock = DockStyle.Top, Height = 180, Font = new Font("Consolas", 8), ForeColor = ThemeRenderer.SecondaryText }; // Etiqueta de metadatos EXIF.
        webMap = new WebView2 { Dock = DockStyle.Fill, MinimumSize = new Size(150, 150) }; // Instancia del visor del mapa.
        webMap.WebMessageReceived += WebMap_WebMessageReceived; // Enlaza el evento de comunicación de coordenadas del JS al código de C#.

        btnSetLocation = new Button { // Inicializa el botón para registrar ubicación.
            Text = "📍 Registrar Ubicación",
            Dock = DockStyle.Bottom,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.Accent,
            ForeColor = Color.White,
            Visible = false
        };
        btnSetLocation.Click += (s, e) => { // Lógica al hacer click en el botón del mapa.
            if (btnSetLocation.Text == "📍 Registrar Ubicación") // Si está en modo consulta:
            {
                ActivarModoMapaPicker(); // Habilita el mapa interactivo para seleccionar punto.
            }
            else // Si ya está guardando la ubicación seleccionada:
            {
                ConfirmarUbicacionManual(s, e); // Aplica los cambios de latitud y longitud.
            }
        };

        pnl.Controls.Add(webMap); // Agrega el WebView del mapa al panel.
        pnl.Controls.Add(btnSetLocation); // Agrega el botón de ubicación al panel.
        pnl.Controls.Add(lblMetaInfo); // Agrega el texto de metadatos al panel.
        tabInfo.Controls.Add(pnl); // Agrega el panel a la pestaña de información.
    }

    // Método auxiliar para construir dinámicamente un botón de edición y enlazar su comportamiento.
    private void AgregarBotonEditor(string texto, Action accion)
    {
        Button btn = new Button { // Instancia el botón con estilo personalizado flat.
            Text = texto,
            Size = new Size(100, 45),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            Cursor = Cursors.Hand,
            Font = new Font("MS Sans Serif", 8, FontStyle.Bold),
            Margin = new Padding(5)
        };
        btn.FlatAppearance.BorderSize = 0; // Elimina bordes predeterminados.
        btn.Click += (s, e) => accion(); // Enlaza la acción que se ejecuta al clickear.
        btn.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, true); // Aplica borde dinámico al pintar el botón.
        flowButtons.Controls.Add(btn); // Agrega el botón al flujo superior.
    }

    // Configura y posiciona los botones de semáforo de Apple (Cerrar, Minimizar, Maximizar/Restaurar).
    private void ConfigurarSemaforos()
    {
        Panel pnlSemaforos = new Panel { Location = new Point(10, 25), Size = new Size(60, 20), BackColor = Color.Transparent }; // Panel de semáforos.
        
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0); // Botón cerrar (rojo).
        btnClose.Click += (s, e) => this.Close(); // Cierra el formulario.
        
        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20); // Botón minimizar (amarillo).
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized; // Minimiza la ventana.
        
        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40); // Botón maximizar (verde).
        btnMax.Click += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal; // Alterna estado de ventana.
        };

        pnlSemaforos.Controls.AddRange(new Control[] { btnClose, btnMin, btnMax }); // Agrega los botones al panel de semáforos.
        pnlTop.Controls.Add(pnlSemaforos); // Agrega el panel de semáforos a la cabecera.
    }

    // Crea un botón con forma circular y color específico para la interfaz de semáforos.
    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button b = new Button { Location = new Point(x, 2), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat }; // Instancia del botón.
        b.FlatAppearance.BorderSize = 0; // Sin bordes de foco.
        b.Paint += (s, e) => { // Renderizado personalizado para círculos perfectos antialias.
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; // Habilita suavizado de curvas.
            e.Graphics.Clear(pnlTop.BackColor); // Limpia con el fondo del contenedor.
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1); // Rellena el círculo.
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1); // Dibuja un borde sutil oscuro.
        };
        return b; // Retorna el botón configurado.
    }

    /// <summary>
    /// Lee la imagen desde el disco de manera segura, aplica correcciones de rotación EXIF
    /// e inicializa el mapa interactivo (<c>WebView2</c>) basándose en los metadatos geográficos.
    /// </summary>
    private async void CargarFoto()
    {
        try // Manejo de errores para evitar cierres inesperados de aplicación al cargar ficheros corruptos o inexistentes.
        {
            if (!File.Exists(_rutaFoto)) return; // Cancela la operación si la ruta provista no corresponde a un archivo en disco.

            _imagenOriginal = Image.FromFile(_rutaFoto); // Carga la imagen de forma sincrónica.
            AppFotoProcessor.CorrectOrientation(_imagenOriginal); // Corrige la orientación rotacional utilizando tags EXIF.
            _imagenActual = (Image)_imagenOriginal.Clone(); // Clona la imagen cargada para el flujo de edición.
            _undoStack.Add((Image)_imagenActual.Clone()); // Inserta el estado original en la lista de deshacer.
            
            ActualizarImagenEnPantalla(); // Coloca la imagen cargada en el PictureBox de visualización.

            _metadata = AppFotoExifService.LeerMetadatos(_rutaFoto); // Extrae la información EXIF de la foto utilizando el servicio.
            
            ActualizarInfoMetadata(); // Coloca la información extraída en la etiqueta de metadatos.

            await webMap.EnsureCoreWebView2Async(); // Garantiza la inicialización en segundo plano de WebView2.
            if (_metadata.TieneUbicacion) // Si la foto posee coordenadas de geolocalización válidas:
            {
                string html = AppFotoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value); // Genera el mapa Leaflet centrado en el punto.
                webMap.NavigateToString(html); // Navega directamente al mapa en HTML.
                btnSetLocation.Visible = false; // Oculta el botón manual de localización.
            }
            else // Si no tiene coordenadas:
            {
                webMap.NavigateToString("<html><body style='background:#f9f9f9; display:flex; justify-content:center; align-items:center; height:100vh; font-family:sans-serif; text-align:center;'><div><h3>No hay datos de GPS 📍</h3><p style='font-size:12px; color:#666;'>Usa el botón de abajo para registrar la ubicación manualmente.</p></div></body></html>"); // Carga un HTML de aviso.
                btnSetLocation.Visible = true; // Habilita el botón manual para ingresar ubicación.
            }
        }
        catch (Exception ex) // Captura fallos de memoria, acceso a archivos o inicio de WebView2.
        {
            MessageBox.Show($"Error cargando la foto: {ex.Message}"); // Presenta un diálogo descriptivo al usuario.
        }
    }

    // Aplica filtros predefinidos de imagen guardando previamente el estado para deshacer.
    private void AplicarFiltro(string nombre)
    {
        GuardarEstadoParaDeshacer(); // Registra el estado actual antes de aplicar el filtro.
        var filtrada = AppFotoProcessor.AplicarFiltro(_imagenActual, nombre); // Procesa la imagen aplicando el filtro seleccionado.
        _imagenActual?.Dispose(); // Desecha la imagen actual anterior para limpiar recursos de memoria.
        _imagenActual = filtrada; // Reemplaza por la versión filtrada.
        ActualizarImagenEnPantalla(); // Refresca el picture box.
    }

    /// <summary>
    /// Aplica instantáneamente los valores combinados de los sliders (brillo, contraste, luces)
    /// utilizando la pila de deshacer para no degradar la imagen en múltiples pasadas.
    /// </summary>
    private void AplicarAjustesTiempoReal()
    {
        if (_undoStack.Count == 0) return; // Si no hay historial disponible para obtener la imagen de partida, aborta.
        var filtrada = AppFotoProcessor.AjustarImagen(_undoStack.Last(), // Toma el último estado original guardado antes de mover los sliders.
            trkBrillo.Value, trkContraste.Value, trkSaturacion.Value, trkLuces.Value, trkSombras.Value); // Ejecuta el ajuste con los valores actuales.
        
        _imagenActual?.Dispose(); // Libera la versión anterior ajustada.
        _imagenActual = filtrada; // Asigna el nuevo resultado de la imagen.
        picPhoto.Image = _imagenActual; // Actualiza visualmente la foto expuesta sin reconstruir el PictureBox.
    }

    // Restablece todos los trackbars a sus valores iniciales sin filtros y recalcula la imagen.
    private void ResetearAjustes()
    {
        trkBrillo.Value = 0; // Resetea brillo a neutro.
        trkContraste.Value = 100; // Resetea contraste al valor estándar de 100.
        trkSaturacion.Value = 100; // Resetea saturación al valor estándar de 100.
        trkLuces.Value = 0; // Resetea luces altas a neutro.
        trkSombras.Value = 0; // Resetea sombras a neutro.
        AplicarAjustesTiempoReal(); // Ejecuta la corrección de imagen en base a los valores restablecidos.
    }

    // Rota la imagen en pantalla y almacena el estado previo en el historial.
    private void Rotar(RotateFlipType tipo)
    {
        GuardarEstadoParaDeshacer(); // Registra la imagen en historial.
        AppFotoProcessor.Rotar(_imagenActual, tipo); // Invoca el procesador rotacional de forma inline.
        ActualizarImagenEnPantalla(); // Pinta la rotación en el lienzo.
    }

    // Guarda una copia de la imagen actual en el historial de cambios del usuario de forma segura.
    private void GuardarEstadoParaDeshacer()
    {
        _undoStack.Add((Image)_imagenActual.Clone()); // Clona la imagen en memoria y la añade a la lista historial.
        if (_undoStack.Count > 15) // Si el historial excede el límite asignado para optimizar RAM:
        {
            _undoStack[0]?.Dispose(); // Libera los recursos GDI del elemento más antiguo de la lista.
            _undoStack.RemoveAt(0); // Remueve la primera posición de la lista para desplazar el historial.
        }
    }

    // Recupera la imagen del paso anterior de edición.
    private void Deshacer()
    {
        if (_undoStack.Count > 0) // Si la lista de historial contiene al menos un paso guardado:
        {
            _imagenActual?.Dispose(); // Libera de inmediato los recursos GDI de la imagen con cambios que se va a descartar.
            _imagenActual = _undoStack.Last(); // Restaura la imagen al último estado guardado.
            _undoStack.RemoveAt(_undoStack.Count - 1); // Remueve el elemento recuperado del historial.
            ActualizarImagenEnPantalla(); // Renderiza la imagen recuperada en el PictureBox.
        }
        else // Si el historial se encuentra vacío:
        {
            MessageBox.Show("No hay más cambios para deshacer."); // Envía alerta al usuario.
        }
    }

    // Coloca la imagen modificada o cargada directamente en la pantalla.
    private void ActualizarImagenEnPantalla()
    {
        picPhoto.Image = _imagenActual; // Asigna el bitmap al control contenedor para el refresco del layout.
    }

    // Habilita el buscador interactivo de localización geográfica en el control web.
    private void ActivarModoMapaPicker()
    {
        webMap.NavigateToString(AppFotoMapService.GenerarMapaPickerHtml()); // Navega al mapa seleccionable por click.
        btnSetLocation.Text = "✅ Guardar Ubicación"; // Cambia el texto del botón de acción.
        btnSetLocation.BackColor = Color.LightGreen; // Aplica color verde al botón para denotar confirmación.
        btnSetLocation.ForeColor = Color.Black; // Cambia el color del texto a negro para su contraste.
    }

    // Registra formalmente los valores GPS seleccionados del mapa y restablece el botón.
    private void ConfirmarUbicacionManual(object? sender, EventArgs e)
    {
        btnSetLocation.Text = "📍 Registrar Ubicación"; // Restablece el label del botón a su estado normal.
        btnSetLocation.BackColor = ThemeRenderer.Accent; // Restablece color de acento del tema.
        btnSetLocation.ForeColor = Color.White; // Restablece color de fuente.
        
        ActualizarInfoMetadata(); // Actualiza la visualización de los datos textuales.
        if (_metadata.TieneUbicacion) // Si ya se registraron correctamente las coordenadas:
        {
            string html = AppFotoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value); // Muestra el mapa centrado con marcador fijo.
            webMap.NavigateToString(html); // Renderiza el mapa en WebView.
            btnSetLocation.Visible = false; // Oculta el botón para evitar doble ingreso.
        }
        MessageBox.Show("Ubicación registrada. Haz clic en 'Guardar' para aplicar permanentemente."); // Informa al usuario.
    }

    // Recibe las coordenadas latitud/longitud transmitidas mediante JS desde WebView2.
    private void WebMap_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try // El parseo JSON puede arrojar excepciones ante retornos con datos inválidos.
        {
            string json = e.WebMessageAsJson; // Obtiene el cuerpo en JSON del mensaje web recibido.
            using var doc = System.Text.Json.JsonDocument.Parse(json); // Parsea la estructura JSON.
            _metadata.Latitud = doc.RootElement.GetProperty("lat").GetDouble(); // Extrae y asigna el valor de la latitud.
            _metadata.Longitud = doc.RootElement.GetProperty("lng").GetDouble(); // Extrae y asigna el valor de la longitud.
        }
        catch { } // Ignora fallos silenciosamente.
    }

    // Construye de nuevo la cadena formateada de metadatos de la imagen.
    private void ActualizarInfoMetadata()
    {
        lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" + // Nombre de archivo de foto.
                           $"Dimensiones: {_metadata.Dimensiones}\n" + // Ancho x Alto de la foto.
                           $"Fecha: {(_metadata.FechaCaptura?.ToString() ?? "N/A")}\n" + // Fecha y hora de captura original.
                           $"Cámara: {_metadata.ModeloCamara}\n" + // Marca y modelo de cámara de foto.
                           $"Latitud: {(_metadata.Latitud?.ToString("F5") ?? "N/A")}\n" + // Coordenada de latitud.
                           $"Longitud: {(_metadata.Longitud?.ToString("F5") ?? "N/A")}"; // Coordenada de longitud.
    }

    /// <summary>
    /// Convierte coordenadas de la pantalla (mouse) a las coordenadas reales del píxel 
    /// en la imagen original, tomando en cuenta el nivel de zoom y el letterboxing del <c>PictureBox</c>.
    /// </summary>
    private Point GetImagePoint(Point mousePoint)
    {
        if (picPhoto.Image == null) return mousePoint; // Retorna sin conversión si no existe una imagen cargada.
        float imgAspect = (float)picPhoto.Image.Width / picPhoto.Image.Height; // Obtiene la relación de aspecto de la imagen física.
        float pbAspect = (float)picPhoto.Width / picPhoto.Height; // Obtiene la relación de aspecto del PictureBox en pantalla.
        float scale; // Escala de conversión de coordenadas.
        float dx = 0, dy = 0; // Variación o desfase en píxeles.
        if (pbAspect > imgAspect) { // Si hay bandas vacías a los lados:
            scale = (float)picPhoto.Height / picPhoto.Image.Height; // Calcula factor de escala en función del alto.
            dx = (picPhoto.Width - picPhoto.Image.Width * scale) / 2; // Desfase horizontal.
        } else { // Si hay bandas vacías arriba y abajo:
            scale = (float)picPhoto.Width / picPhoto.Image.Width; // Calcula factor de escala en función del ancho.
            dy = (picPhoto.Height - picPhoto.Image.Height * scale) / 2; // Desfase vertical.
        }
        return new Point((int)((mousePoint.X - dx) / scale), (int)((mousePoint.Y - dy) / scale)); // Calcula el punto de píxel real en la imagen.
    }

    /// <summary>
    /// Operación inversa a <see cref="GetImagePoint"/>. Convierte coordenadas del píxel real de la imagen
    /// a las coordenadas visuales en pantalla para dibujar el rectángulo de selección de recorte interactivo.
    /// </summary>
    private Point GetMousePoint(Point imagePoint)
    {
        if (picPhoto.Image == null) return imagePoint; // Si no hay imagen cargada, devuelve el punto recibido.
        float imgAspect = (float)picPhoto.Image.Width / picPhoto.Image.Height; // Relación de aspecto del bitmap.
        float pbAspect = (float)picPhoto.Width / picPhoto.Height; // Relación de aspecto del control picture box.
        float scale; // Escala de conversión.
        float dx = 0, dy = 0; // Desfases.
        if (pbAspect > imgAspect) { // Bandas a los costados:
            scale = (float)picPhoto.Height / picPhoto.Image.Height; // Escala.
            dx = (picPhoto.Width - picPhoto.Image.Width * scale) / 2; // Desfase X.
        } else { // Bandas arriba y abajo:
            scale = (float)picPhoto.Width / picPhoto.Image.Width; // Escala.
            dy = (picPhoto.Height - picPhoto.Image.Height * scale) / 2; // Desfase Y.
        }
        return new Point((int)(imagePoint.X * scale + dx), (int)(imagePoint.Y * scale + dy)); // Retorna punto mapeado a la pantalla de la PC.
    }

    // Gestiona la pulsación de un botón de mouse en el canvas de foto para iniciar la operación activa.
    private void PicPhoto_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_currentMode == ToolMode.None) return; // Si no hay herramienta activa, aborta.
        _startPoint = GetImagePoint(e.Location); // Obtiene e inicializa el punto de inicio convertido a coordenadas de píxel de la imagen.
        _isSelecting = true; // Activa bandera de selección de mouse.
        if (_currentMode == ToolMode.Text || _currentMode == ToolMode.Draw || _currentMode == ToolMode.Crop) // Si modifica la imagen:
            GuardarEstadoParaDeshacer(); // Salva el estado en la pila de historial de edición.
        if (_currentMode == ToolMode.Text) { // Si la herramienta actual es texto:
            string txt = Microsoft.VisualBasic.Interaction.InputBox("Escribe el texto:", "Añadir Texto", "¡Hola!"); // Abre ventana emergente estándar para capturar cadena.
            if (!string.IsNullOrWhiteSpace(txt)) { // Valida que el texto no esté vacío.
                AppFotoProcessor.DibujarTexto(_imagenActual, txt, _startPoint, new Font("Segoe UI", 24, FontStyle.Bold), _drawColor); // Plasma el texto en el bitmap actual.
                picPhoto.Invalidate(); // Fuerza al PictureBox a repintarse para reflejar el texto.
            }
            _isSelecting = false; // Finaliza de inmediato la selección ya que no requiere arrastre.
        }
    }

    // Dibuja trazos o calcula el área del rectángulo de corte mientras se arrastra el mouse.
    private void PicPhoto_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isSelecting) return; // Aborta si el mouse no está pulsado en modo acción.
        Point currentImagePoint = GetImagePoint(e.Location); // Coordenadas del mouse convertidas al píxel de la imagen.
        if (_currentMode == ToolMode.Draw) { // Si dibuja a mano alzada:
            AppFotoProcessor.DibujarLinea(_imagenActual, _startPoint, currentImagePoint, _drawColor, _penWidth); // Dibuja la línea en la imagen intermedia.
            _startPoint = currentImagePoint; // Desplaza el punto inicial para continuar el trazo fluido.
            picPhoto.Invalidate(); // Refresca lienzo.
        } else if (_currentMode == ToolMode.Crop) { // Si se está seleccionando área para recorte:
            int x = Math.Min(_startPoint.X, currentImagePoint.X); // Determina la coordenada X menor (esquina superior izquierda).
            int y = Math.Min(_startPoint.Y, currentImagePoint.Y); // Determina la coordenada Y menor.
            int w = Math.Abs(_startPoint.X - currentImagePoint.X); // Calcula el ancho absoluto del área.
            int h = Math.Abs(_startPoint.Y - currentImagePoint.Y); // Calcula el alto absoluto del área.
            _cropRect = new Rectangle(x, y, w, h); // Genera el objeto rectángulo de corte en el bitmap.
            picPhoto.Invalidate(); // Redibuja el recuadro punteado en el PictureBox.
        }
    }

    // Finaliza las operaciones de dibujo o recorte tras soltar el botón de click del mouse.
    private void PicPhoto_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isSelecting) return; // Si no se encuentra en estado de selección, cancela.
        _isSelecting = false; // Desactiva bandera.
        if (_currentMode == ToolMode.Crop && _cropRect.Width > 10) { // Si se recortará y el ancho mínimo de selección es válido:
            if (MessageBox.Show("¿Recortar?", "Recortar", MessageBoxButtons.YesNo) == DialogResult.Yes) { // Solicita confirmación en cuadro de diálogo.
                var recortada = AppFotoProcessor.Recortar(_imagenActual, _cropRect); // Ejecuta el recorte en la imagen.
                _imagenActual.Dispose(); // Libera la imagen previa para optimizar memoria RAM.
                _imagenActual = recortada; // Reemplaza por la versión recortada.
                ActualizarImagenEnPantalla(); // Refresca visor.
            } else { Deshacer(); } // Si cancela, deshace el estado guardado.
            _cropRect = Rectangle.Empty; // Resetea el rectángulo de selección de recorte.
            _currentMode = ToolMode.None; // Vuelve la herramienta activa a modo nulo.
            picPhoto.Invalidate(); // Refresca el lienzo para ocultar líneas guía de corte.
        }
    }

    // Evento de pintado para plasmar sobre el PictureBox el rectángulo guía de recorte de forma visual.
    private void PicPhoto_Paint(object? sender, PaintEventArgs e)
    {
        if (_currentMode == ToolMode.Crop && _isSelecting) { // Si recorta y está seleccionando activamente:
            Point p1 = GetMousePoint(_cropRect.Location); // Convierte esquina superior izquierda de la imagen a pantalla.
            Point p2 = GetMousePoint(new Point(_cropRect.Right, _cropRect.Bottom)); // Convierte esquina inferior derecha de la imagen a pantalla.
            Rectangle screenRect = new Rectangle(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y); // Crea el rectángulo escalado para dibujar.
            using Pen p = new Pen(Color.White, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }; // Crea pincel con estilo punteado blanco.
            e.Graphics.DrawRectangle(p, screenRect); // Dibuja el borde blanco discontinuo.
            e.Graphics.DrawRectangle(Pens.Black, screenRect); // Dibuja borde negro base para contraste en zonas claras.
        }
    }

    /// <summary>
    /// Exporta la imagen actual al disco. Si la imagen contiene datos GPS y se guarda en JPEG,
    /// inyecta asíncronamente las etiquetas de geolocalización dentro del nuevo archivo.
    /// </summary>
    private void GuardarImagen()
    {
        using SaveFileDialog sfd = new SaveFileDialog { Filter = "Imagen JPEG|*.jpg|Imagen PNG|*.png", FileName = _metadata.Nombre }; // Configura el diálogo para guardar.
        if (sfd.ShowDialog() == DialogResult.OK) { // Si confirma el archivo y ruta de guardado:
            string ext = Path.GetExtension(sfd.FileName).ToLower(); // Obtiene la extensión del archivo a exportar.
            if (_metadata.TieneUbicacion && (ext == ".jpg" || ext == ".jpeg")) { // Si tiene ubicación y es formato JPEG:
                AppFotoProcessor.GuardarConGps(_imagenActual, sfd.FileName, _metadata.Latitud!.Value, _metadata.Longitud!.Value); // Inyecta coordenadas EXIF al guardar.
            } else { _imagenActual.Save(sfd.FileName); } // Guarda la imagen de forma directa sin tags adicionales.
            MessageBox.Show("Imagen guardada ✨"); // Mensaje de éxito.
        }
    }
    
    // Método que intercepta el cierre del formulario para liberar toda la memoria gráfica asignada.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _imagenOriginal?.Dispose(); // Libera recursos del objeto de imagen original cargado.
        _imagenActual?.Dispose(); // Libera recursos del objeto de la imagen modificada en visor.
        _undoStack.ForEach(img => img?.Dispose()); // Utiliza LINQ/ForEach para liberar de memoria cada una de las imágenes de respaldo del historial.
        base.OnFormClosing(e); // Llama al comportamiento de cierre de formulario base.
    }
}
