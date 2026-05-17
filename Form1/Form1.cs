using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ExploradorArchivos.Models;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;
using ExploradorArchivos.Mp3;
using ExploradorArchivos.AppVideo;
using ExploradorArchivos.AppFoto;
using ExploradorArchivos.AppDataFusion;

namespace ExploradorArchivos;

public partial class Form1 : Form
{
    // === Estado de navegación ===
    private string _rutaActual = "Inicio"; // Ruta actual mostrada en el ListView
    private Stack<string> _historial = new Stack<string>(); // Historial tipo pila LIFO para navegación hacia atrás
    private List<FileSystemItem> _itemsActuales = new List<FileSystemItem>(); // Lista de items actualmente mostrados (para filtros y búsqueda)
    private string _filtroActivo = "Todos"; // filtro de visualización activo (Todos, Imágenes, Audio, Video, Texto/Código, Otros)

    // === Componentes de UI ===
    private ListViewSorter _sorter = default!; // Ordenador de ListView para ordenamiento por columnas
    private QuickLookForm? _quickLookForm;
    private FlowLayoutPanel _pnlFiltros = default!;
    private ImageList _imageListMiniaturas = default!;
    private Button _btnToggleVista = default!;
    private Button _btnAppData = default!;
    private FlowLayoutPanel _flpBreadcrumbs = default!;

    public Form1()
    {
        InitializeComponent();
        ConfigurarUI();
        ConectarEventos();
        ConfigurarContextoMenu();
        CargarDirectorio(_rutaActual);
    }

    /* Construye dinamicamente el menu contextual de clic derecho (Abrir, 
     * QuickLook, Cortar, Copiar, Pegar, Eliminar, Propiedades)
     utilizando un renderizador personalizado de colores */
    private void ConfigurarContextoMenu()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new CustomMenuRenderer(); // Estilo retro/limpio
        
        ToolStripMenuItem itemAbrir = new ToolStripMenuItem("📄  Abrir");
        ToolStripMenuItem itemAbrirCon = new ToolStripMenuItem("📁  Abrir con...");
        
        ToolStripMenuItem itemCortar = new ToolStripMenuItem("✂️  Cortar");
        ToolStripMenuItem itemCopiar = new ToolStripMenuItem("📋  Copiar");
        ToolStripMenuItem itemRenombrar = new ToolStripMenuItem("✏️  Cambiar nombre");
        ToolStripMenuItem itemEliminar = new ToolStripMenuItem("🗑️  Eliminar");
        ToolStripMenuItem itemPropiedades = new ToolStripMenuItem("🛠️  Propiedades");

        // Submenú Abrir con...
        ToolStripMenuItem itemAppVideo = new ToolStripMenuItem("🎬  App Video");
        ToolStripMenuItem itemAppFoto = new ToolStripMenuItem("🖼️  App Foto");
        ToolStripMenuItem itemAppData = new ToolStripMenuItem("📊  App Data");
        ToolStripMenuItem itemMusic = new ToolStripMenuItem("🎵  App Music");
        ToolStripMenuItem itemTexto = new ToolStripMenuItem("📝  Visor de Texto");
        ToolStripMenuItem itemPredeterminada = new ToolStripMenuItem("💻  Sistema (App Predeterminada)");

        // Eventos de apertura
        itemAbrir.Click += (s, e) => ListViewPrincipal_DoubleClick(s, e); 
        
        itemAppVideo.Click += (s, e) => AbrirCon(new AppVideoForm(GetSelectedPath()));
        itemAppFoto.Click += (s, e) => AbrirCon(new AppFotoForm(GetSelectedPath()));
        itemAppData.Click += (s, e) => AbrirCon(new ExploradorArchivos.AppDataFusion.MainForm(GetSelectedPath()));
        itemMusic.Click += (s, e) => AbrirCon(new MusicPlayerForm(new List<string> { GetSelectedPath() }, GetSelectedPath()));
        itemTexto.Click += (s, e) => AbrirCon(new FileViewerForm(GetSelectedPath()));
        itemPredeterminada.Click += (s, e) => AbrirConSistema(GetSelectedPath());

        // Eventos de archivo
        itemCortar.Click += (s, e) => CortarArchivo(GetSelectedPath());
        itemCopiar.Click += (s, e) => CopiarArchivo(GetSelectedPath());
        itemRenombrar.Click += (s, e) => listViewPrincipal.SelectedItems[0].BeginEdit();
        itemEliminar.Click += (s, e) => EliminarArchivo(GetSelectedPath());
        itemPropiedades.Click += (s, e) => MostrarPropiedades(GetSelectedPath());

        itemAbrirCon.DropDownItems.AddRange(new ToolStripItem[] {
            itemAppVideo, itemAppFoto, itemAppData, itemMusic, itemTexto, new ToolStripSeparator(), itemPredeterminada
        });

        menu.Items.AddRange(new ToolStripItem[] {
            itemAbrir, itemAbrirCon, new ToolStripSeparator(),
            itemCortar, itemCopiar, itemRenombrar, itemEliminar,
            new ToolStripSeparator(), itemPropiedades
        });

        listViewPrincipal.ContextMenuStrip = menu;
        listViewPrincipal.LabelEdit = true;
        listViewPrincipal.AfterLabelEdit += (s, e) => {
            if (string.IsNullOrEmpty(e.Label)) { e.CancelEdit = true; return; }
            string? rutaOld = listViewPrincipal.SelectedItems[0].Tag?.ToString();
            if (rutaOld != null) RenombrarArchivo(rutaOld, e.Label);
        };

        menu.Opening += (s, e) => {
            if (listViewPrincipal.SelectedItems.Count == 0) { e.Cancel = true; return; }
            string ruta = GetSelectedPath();
            string ext = Path.GetExtension(ruta).ToLower();
            
            string[] imgExt = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            string[] mediaExt = { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma", ".aac" };
            string[] videoExt = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v" };
            string[] dataExt = { ".csv", ".json", ".xml", ".txt" };
            
            itemAppVideo.Visible = videoExt.Contains(ext);
            itemAppFoto.Visible = imgExt.Contains(ext);
            itemAppData.Visible = dataExt.Contains(ext);
            itemMusic.Visible = mediaExt.Contains(ext);
            itemTexto.Visible = dataExt.Contains(ext) || new[] { ".cs", ".html", ".css", ".js", ".md", ".py" }.Contains(ext);
        };
    }

    /* Obtiene la ruta completa del item seleccionado en el ListView,
     * para usar en acciones como abrir, cortar, copiar, eliminar, etc. 
     * Si no hay item seleccionado, devuelve cadena vacía.*/
    private string GetSelectedPath() => listViewPrincipal.SelectedItems[0].Tag?.ToString() ?? "";


    /* Método helper generico que muestra subformularios del explorador
     * (Visor de imágenes, Reproductor de música, Visor de texto, etc.)*/
    private void AbrirCon(Form frm) => frm.Show();

    /* AbrirConSistema utiliza Process.Star UseShellExecute = True 
     * para delegar la ejecucion al visor predeterminado
     * del sistema operativo, lo que permite abrir cualquier tipo de archivo*/
    private void AbrirConSistema(string ruta) => Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });

    //Almacenan la referencia del archivo en el portapapeles del sistema operativo para transacciones I/O futuras
    private void CortarArchivo(string ruta)
    {
        var paths = new System.Collections.Specialized.StringCollection { ruta };
        DataObject data = new DataObject();
        data.SetFileDropList(paths);
        data.SetData("Preferred DropEffect", DragDropEffects.Move);
        Clipboard.SetDataObject(data);
    }

    private void CopiarArchivo(string ruta)
    {
        var paths = new System.Collections.Specialized.StringCollection { ruta };
        Clipboard.SetFileDropList(paths);
    }

    // En lugar de eliminar permanentemente, se envía el archivo a la papelera
    // de reciclaje utilizando la función SHFileOperation de Windows, lo que permite
    // recuperar archivos eliminados accidentalmente.
    private void EliminarArchivo(string ruta)
    {
        if (MessageBox.Show("¿Seguro que deseas eliminar este archivo?", "Eliminar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            FileService.EnviarAPapelera(ruta);
            CargarDirectorio(_rutaActual, false);
        }
    }
    // Ejecuta renombrado de archivos manejando errores comunes mediante File.Move o Directory.Move.
    private void RenombrarArchivo(string rutaOld, string nuevoNombre)
    {
        try
        {
            string dir = Path.GetDirectoryName(rutaOld)!; // Obtener el directorio padre para construir la nueva ruta
            string ext = Path.GetExtension(rutaOld); // Obtener la extensión del archivo original
            string rutaNew = Path.Combine(dir, nuevoNombre + (nuevoNombre.EndsWith(ext) ? "" : ext)); // Asegura que la extensión se mantenga si el usuario no la incluye al renombrar
            if (rutaOld != rutaNew)
            {
                if (Directory.Exists(rutaOld)) Directory.Move(rutaOld, rutaNew); 
                else File.Move(rutaOld, rutaNew); 
                CargarDirectorio(_rutaActual, false); 
            }
        }
        catch (Exception ex) { MessageBox.Show("Error al renombrar: " + ex.Message); }
    }

    // Construye un cuadro de diálogo descriptivo sobre el archivo mostrando su peso exacto, fecha de creación y atributos.
    private void MostrarPropiedades(string ruta)
    {
        var info = new FileInfo(ruta);
        string msg = $"Nombre: {info.Name}\n" +
                     $"Tamaño: {info.Length / 1024.0:F2} KB\n" +
                     $"Creado: {info.CreationTime}\n" +
                     $"Modificado: {info.LastWriteTime}";
        MessageBox.Show(msg, "Propiedades de Archivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // Clase de pintura GDI+ encargada de forzar el dibujo de bordes y rellenos en menús contextuales para ajustarlos a la estética.
    class CustomMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Rectangle rect = new Rectangle(Point.Empty, e.Item.Size);
                using (SolidBrush brush = new SolidBrush(ThemeRenderer.Selection))
                    e.Graphics.FillRectangle(brush, rect);
            }
        }
    }

    // ===== CONFIGURACIÓN DE INTERFAZ Y ESTILO VISUAL ===

    private void ConfigurarUI()
    {
        ThemeRenderer.ApplyTheme(this);
        ConfigurarSemaforos();
        this.BackColor = ThemeRenderer.MainBg;
        listViewPrincipal.BackColor = Color.White;
        listViewPrincipal.BorderStyle = BorderStyle.Fixed3D;
        treeViewLateral.BackColor = ThemeRenderer.SecondaryBg;
        treeViewLateral.BorderStyle = BorderStyle.Fixed3D;
        treeViewLateral.ItemHeight = 28; // Mayor espaciado en sidebar
        
        // Configurar Vista de Detalles Clásica (Más ancha y limpia)
        listViewPrincipal.View = View.Details;
        listViewPrincipal.HeaderStyle = ColumnHeaderStyle.Clickable;
        
        // Aumentar la altura de las filas para que no se vea amontonado
        var spacerList = new ImageList { ImageSize = new Size(1, 36) }; // 36px de alto
        listViewPrincipal.SmallImageList = spacerList;
        
        listViewPrincipal.Columns.Clear();
        listViewPrincipal.Columns.Add("Nombre", 450);
        listViewPrincipal.Columns.Add("Tipo", 120);
        listViewPrincipal.Columns.Add("Tamaño", 100);
        listViewPrincipal.Columns.Add("Info / Contenido", 150);
        listViewPrincipal.Columns.Add("Fecha de modificación", 200);
        listViewPrincipal.GridLines = true;

        _sorter = new ListViewSorter();
        listViewPrincipal.ListViewItemSorter = _sorter;

        ConfigurarFiltrosRapidos();
        ConfigurarVistaMiniaturas();

        listViewPrincipal.ShowGroups = true;

        // OwnerDraw para diseño Retro 95
        listViewPrincipal.OwnerDraw = true;
        listViewPrincipal.DrawColumnHeader += ThemeRenderer.DrawListViewColumnHeader;
        listViewPrincipal.DrawItem += ThemeRenderer.DrawListViewItem;
        listViewPrincipal.DrawSubItem += ThemeRenderer.DrawListViewSubItem;

        treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        treeViewLateral.DrawNode += ThemeRenderer.DrawTreeNode;

        // --- 1. REORGANIZACIÓN DE BARRA SUPERIOR (pnlTop) ---
        pnlTop.BackColor = ThemeRenderer.SecondaryBg;
        pnlTop.Height = 80;
        pnlTop.Controls.Clear();
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTop.ClientRectangle, true);

        // Arrastrar Ventana
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTop.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTop.MouseUp += (s, e) => { isDragging = false; };

        ConfigurarSemaforos();

        // Grupo: Navegación
        Panel pnlNav = CrearGrupoHerramientas("", 85, 12, 100);
        pnlNav.Controls.Add(btnAtras); btnAtras.Location = new Point(10, 18); btnAtras.Size = new Size(35, 30);
        pnlNav.Controls.Add(btnSubir); btnSubir.Location = new Point(50, 18); btnSubir.Size = new Size(35, 30);
        pnlTop.Controls.Add(pnlNav);

        // Grupo: Dirección
        Panel pnlAddr = CrearGrupoHerramientas("", 195, 12, pnlTop.Width - 725);
        pnlAddr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlAddr.Controls.Add(pnlAddressBorder);
        pnlAddressBorder.Location = new Point(10, 16);
        pnlAddressBorder.Size = new Size(pnlAddr.Width - 20, 36);
        pnlAddressBorder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlTop.Controls.Add(pnlAddr);

        // Grupo: Acciones
        Panel pnlActions = CrearGrupoHerramientas("", pnlTop.Width - 520, 12, 510);
        pnlActions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlActions.Controls.Add(btnActualizar); btnActualizar.Location = new Point(10, 18); btnActualizar.Size = new Size(35, 30);
        pnlActions.Controls.Add(btnNuevaCarpeta); btnNuevaCarpeta.Location = new Point(60, 18); btnNuevaCarpeta.Size = new Size(100, 30);
        pnlActions.Controls.Add(btnExportarCSV); btnExportarCSV.Location = new Point(175, 18); btnExportarCSV.Size = new Size(100, 30);
        pnlActions.Controls.Add(_btnToggleVista); _btnToggleVista.Location = new Point(290, 18); _btnToggleVista.Size = new Size(100, 30);
        
        _btnAppData = new Button();
        pnlActions.Controls.Add(_btnAppData); _btnAppData.Location = new Point(400, 18); _btnAppData.Size = new Size(100, 30);

        pnlTop.Controls.Add(pnlActions);

        // --- 2. REFINAMIENTO DEL PANEL LATERAL (Sidebar) ---
        pnlSearch.BackColor = ThemeRenderer.SecondaryBg;
        pnlSearch.Height = 65;
        pnlSearch.Controls.Clear();
        pnlSearch.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlSearch.ClientRectangle, true);

        pnlSearch.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlSearch.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlSearch.MouseUp += (s, e) => { isDragging = false; };

        pnlSearch.Controls.Add(pnlSearchBorder);
        pnlSearchBorder.Location = new Point(15, 15);
        pnlSearchBorder.Width = pnlSearch.Width - 40;
        pnlSearchBorder.Height = 30;
        pnlSearchBorder.BackColor = Color.White;
        
        pnlSearchBorder.Paint += (s, e) => {
            ThemeRenderer.DrawRetroBorder(e.Graphics, pnlSearchBorder.ClientRectangle, false); // Sunken
        };
        
        txtBuscar.BackColor = Color.White;
        txtBuscar.Location = new Point(5, 5);
        txtBuscar.Width = pnlSearchBorder.Width - 40;
        txtBuscar.Font = new Font(this.Font.FontFamily, 8);
        
        btnBuscar.Text = "🔍";
        btnBuscar.BackColor = Color.Transparent;
        btnBuscar.ForeColor = ThemeRenderer.SecondaryText;
        btnBuscar.Size = new Size(25, 25);
        btnBuscar.Location = new Point(pnlSearchBorder.Width - 30, 2);
        btnBuscar.FlatStyle = FlatStyle.Flat;
        btnBuscar.FlatAppearance.BorderSize = 0;

        pnlSearchBorder.Controls.Add(txtBuscar);
        pnlSearchBorder.Controls.Add(btnBuscar);

        // --- 3. BARRA DE FILTROS INTEGRADA ---
        _pnlFiltros.BackColor = ThemeRenderer.MainBg;
        _pnlFiltros.Height = 55;
        _pnlFiltros.Padding = new Padding(15, 12, 0, 0);
        _pnlFiltros.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, _pnlFiltros.ClientRectangle, true);

        // --- Configurar Botones (Estilo Clásico 3D) ---
        ConfigurarBotonRetro(btnAtras, "◀️");
        ConfigurarBotonRetro(btnSubir, "🔼");
        ConfigurarBotonRetro(btnActualizar, "🔄");
        ConfigurarBotonRetro(btnNuevaCarpeta, "📁 Nueva");
        ConfigurarBotonRetro(btnExportarCSV, "📊 Exportar");
        ConfigurarBotonRetro(_btnToggleVista, "🖼️ Vistas");
        ConfigurarBotonRetro(_btnAppData, "💎 App Data");

        // --- Barra de Direcciones ---
        pnlAddressBorder.BackColor = Color.White;
        pnlAddressBorder.Paint += (s, e) => {
            ThemeRenderer.DrawRetroBorder(e.Graphics, pnlAddressBorder.ClientRectangle, false); // Sunken
        };
        
        _flpBreadcrumbs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            WrapContents = false,
            AutoScroll = false,
            Cursor = Cursors.IBeam,
            Padding = new Padding(10, 4, 0, 0)
        };
        _flpBreadcrumbs.Click += (s, e) => MostrarTextBoxDireccion();
        pnlAddressBorder.Controls.Add(_flpBreadcrumbs);
        _flpBreadcrumbs.BringToFront();

        txtDireccion.BackColor = Color.White;
        txtDireccion.Visible = false;
        txtDireccion.Leave += (s, e) => OcultarTextBoxDireccion();

        // --- Elementos Decorativos y Barra de Estado ---
        pnlBottom.BackColor = ThemeRenderer.MainBg;
        pnlBottom.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlBottom.ClientRectangle, true);

        // --- Barra de Estado y Papelera ---
        lblStatus.ForeColor = ThemeRenderer.MainText;
        lblStatus.Font = new Font(this.Font.FontFamily, 8);
        lblStatus.AutoSize = false;
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.Text = "Vista de Inicio cargada";
        
        pnlTrash.BackColor = ThemeRenderer.MainBg;
        pnlTrash.Paint += (s, e) => {
            ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTrash.ClientRectangle, false); // Sunken
        };
        lblTrash.ForeColor = ThemeRenderer.MainText;
        lblTrash.Text = "🗑️ Papelera";

        // --- Reordenar paneles ---
        splitContainerMain.Panel1.Controls.Clear();
        splitContainerMain.Panel2.Controls.Clear();

        splitContainerMain.Panel1.Controls.Add(treeViewLateral);
        splitContainerMain.Panel1.Controls.Add(pnlSearch);
        
        pnlSearch.BringToFront();

        splitContainerMain.Panel2.Controls.Add(listViewPrincipal);
        splitContainerMain.Panel2.Controls.Add(_pnlFiltros);
        listViewPrincipal.BringToFront(); // <-- CLAVE: listViewPrincipal debe estar al frente para que dockee al último y no quede bajo _pnlFiltros

        splitContainerMain.SplitterDistance = 280; // Más espacio para el sidebar
    }

    private Panel CrearGrupoHerramientas(string titulo, int x, int y, int ancho)
    {
        Panel pnl = new Panel { Location = new Point(x, y), Size = new Size(ancho, 55), BackColor = Color.Transparent };
        Label lbl = new Label { Text = titulo, Location = new Point(5, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ThemeRenderer.SecondaryText };
        pnl.Controls.Add(lbl);
        pnl.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, new Rectangle(0, 15, pnl.Width - 1, pnl.Height - 16), false);
        return pnl;
    }

    private void ConfigurarSemaforos()
    {
        Panel pnlSemaforos = new Panel { Location = new Point(10, 30), Size = new Size(60, 20), BackColor = Color.Transparent };
        
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

    private void ConfigurarBotonRetro(Button btn, string text)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = ThemeRenderer.MainBg;
        btn.Text = text;
        btn.Font = new Font("MS Sans Serif", 9, FontStyle.Bold); // Botones más legibles
        btn.ForeColor = ThemeRenderer.MainText;
        
        bool isPressed = false;

        btn.MouseDown += (s, e) => { isPressed = true; btn.Invalidate(); };
        btn.MouseUp += (s, e) => { isPressed = false; btn.Invalidate(); };

        btn.Paint += (s, e) => {
            ThemeRenderer.DrawRetroBorder(e.Graphics, btn.ClientRectangle, !isPressed);
            if (isPressed)
            {
                TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, 
                    new Rectangle(btn.ClientRectangle.X + 1, btn.ClientRectangle.Y + 1, btn.ClientRectangle.Width, btn.ClientRectangle.Height), 
                    btn.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
    }

    private void ConectarEventos()
    {
        // Navegación
        btnAtras.Click += (s, e) => { if (_historial.Count > 0) CargarDirectorio(_historial.Pop(), false); };
        btnSubir.Click += (s, e) =>
        {
            var parent = Directory.GetParent(_rutaActual);
            if (parent != null) CargarDirectorio(parent.FullName);
        };
        btnActualizar.Click += (s, e) => CargarDirectorio(_rutaActual, false);
        txtDireccion.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CargarDirectorio(txtDireccion.Text); }
        };

        // ListView
        listViewPrincipal.DoubleClick += ListViewPrincipal_DoubleClick;
        listViewPrincipal.MouseMove += (s, e) =>
        {
            var item = listViewPrincipal.GetItemAt(e.X, e.Y);
            int newIndex = item?.Index ?? -1;
            if (newIndex != ThemeRenderer.GetHoverIndex())
            {
                ThemeRenderer.SetHoverIndex(newIndex);
                listViewPrincipal.Invalidate();
            }
        };
        listViewPrincipal.MouseLeave += (s, e) =>
        {
            ThemeRenderer.SetHoverIndex(-1);
            listViewPrincipal.Invalidate();
        };

        // Drag & Drop a Papelera
        listViewPrincipal.ItemDrag += (s, e) => listViewPrincipal.DoDragDrop(listViewPrincipal.SelectedItems, DragDropEffects.Move);
        pnlTrash.DragEnter += PnlTrash_DragEnter;
        pnlTrash.DragLeave += PnlTrash_DragLeave;
        pnlTrash.DragDrop += PnlTrash_DragDrop;

        // Búsqueda y Exportación
        txtBuscar.KeyDown += TxtBuscar_KeyDown;
        btnExportarCSV.Click += BtnExportarCSV_Click;

        // Ordenamiento por columnas
        listViewPrincipal.ColumnClick += ListViewPrincipal_ColumnClick;

        // Quick Look
        listViewPrincipal.KeyDown += ListViewPrincipal_KeyDown;

        // TreeView
        treeViewLateral.NodeMouseDoubleClick += TreeViewLateral_NodeMouseDoubleClick;
    }
}