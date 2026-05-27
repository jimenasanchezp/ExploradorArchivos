#nullable disable
namespace ExploradorArchivos.AppDataFusion;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // ── Menu ──────────────────────────────────────────────────────────
    private MenuStrip menuStrip1;
    private ToolStripMenuItem menuArchivo, menuBaseDatos, menuAyuda;
    private ToolStripMenuItem menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt;
    private ToolStripMenuItem menuCargarPersonalizado, menuLimpiarDatos, menuSalir;
    private ToolStripMenuItem menuPostgres, menuMariaDB;
    private ToolStripMenuItem menuAcercaDe;
    private ToolStripMenuItem menuExportar, menuExportCsv, menuExportJson, menuExportXml, menuExportTxt;
    private ToolStripSeparator menuSep1, menuSep2, menuSepExport;

    // ── Sidebar toolbar (left vertical panel) ──────────────────────────────────────────────────────────
    private Panel pnlSidebar;
    private Panel pnlLogo;
    private Label lblLogoTitle;
    private Label lblLogoSub;
    private Panel pnlSemaforo;


    // Sidebar file type quick-load buttons
    private Panel pnlSidebarFiles;
    private Button btnSbJson, btnSbCsv, btnSbXml, btnSbTxt;

    // Sidebar export quick buttons
    private Panel pnlSidebarExport;

    // Sidebar database buttons
    private Panel pnlSidebarDB;
    private Button btnSbPostgres, btnSbMariaDB;

    // Sidebar sources filter
    private Panel pnlSidebarSources;
    private Panel pnlSidebarApi;
    private Button btnSbApi;
    private CheckedListBox clbFuentes;

    // ── Main content area ──────────────────────────────────────────────────────────
    private Panel pnlMain;

    // Top stats bar
    private Panel pnlTopStats;
    private Label lblTotalRegistros, lblTotalCategorias, lblTotalFuentes;

    // Tab control
    private CustomTabControl tabControl1;
    private TabPage tabTodos, tabGraficas;

    // ── Tab 1 ──────────────────────────────────────────────────────────
    private Panel pnlToolsTodos;
    private Label lblBuscar, lblOrdenar, lblContadorTodos;
    private ComboBox cmbCampoBusqueda, cmbCampoOrden;
    private TextBox txtBusqueda;
    private Button btnFiltrar, btnLimpiarFiltro, btnOrdenar;
    private RadioButton rbAscendente, rbDescendente;
    private DataGridView dgvTodos;

    // ── Tab 2 ──────────────────────────────────────────────────────────
    private SplitContainer splitCategoria;
    private ListBox lstCategorias;
    private DataGridView dgvCategoria;
    private Label lblCatInfo;

    // ── Tab 3 ──────────────────────────────────────────────────────────
    private Panel pnlStatsTop;
    private DataGridView dgvEstadisticas;

    // ── Tab 4 ──────────────────────────────────────────────────────────
    private Panel pnlGraficasTop;
    private Label lblTipoGrafica, lblGrupoGrafica, lblMetricaGrafica, lblChartTitle;
    private ComboBox cmbTipoGrafica, cmbGrupoGrafica, cmbMetricaGrafica;
    private Button btnActualizarGrafica;
    private ChartPanel chartMain;

    // ── Tab 5 ──────────────────────────────────────────────────────────
    private Panel pnlProcHeader;
    private Button btnDetectarDuplicados, btnEliminarDuplicados;
    private Label lblProcInfo;
    private ComboBox cmbLinqCampo;
    private TextBox txtLinqFiltro;
    private Button btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy, btnLinqLimpiar;
    private DataGridView dgvProcesamiento;

    // Status bar
    private StatusStrip statusStrip1;
    private ToolStripStatusLabel lblStatus;
    private ToolStripStatusLabel lblStatusRight;
    private Label lblSubtext;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

    // ── Design tokens (Estilo Clásico / ThemeRenderer spec) ──────────────────────────────────────────────────────────
        var clrBg = ColorTranslator.FromHtml("#FFF5F9");      // MainBg
        var clrSurface = Color.White;                         // white surface
        var clrSurface2 = ColorTranslator.FromHtml("#FFE4F2"); // Hover (light pink)
        var clrSidebar = ColorTranslator.FromHtml("#FFD1EA"); // SecondaryBg (sidebar)
        var clrSidebarText = ColorTranslator.FromHtml("#8B5E75"); // SecondaryText
        var clrSidebarLabels = ColorTranslator.FromHtml("#FF80BF"); // Accent (labels)
        var clrAcento = ColorTranslator.FromHtml("#FF80BF"); // Accent
        var clrBorder = Color.Lavender;
        var clrText = Color.DimGray;
        var clrTextDim = ColorTranslator.FromHtml("#8B5E75");
        var clrTextMuted = Color.LightGray;

        var clrRose = clrAcento;
        var clrMint = ColorTranslator.FromHtml("#CFF5E7"); // VerdeMenta
        var clrAmber = ColorTranslator.FromHtml("#FFBDE3"); // Selection
        var clrSky = ColorTranslator.FromHtml("#E6D4F8"); // FolderMusicBg
        var clrPurple = ColorTranslator.FromHtml("#FFBDE3");

    // ── Mandatory initializations for logic ──────────────────────────────────────────────────────────
        clbFuentes = new CheckedListBox { Visible = false };
        lblTotalRegistros = new Label { Visible = false };
        lblTotalCategorias = new Label { Visible = false };
        lblTotalFuentes = new Label { Visible = false };
        pnlSidebarSources = new Panel { Visible = false };
        pnlSidebarApi = new Panel { Visible = false };

        SuspendLayout();
        ClientSize = new Size(1440, 900);
        MinimumSize = new Size(1100, 700);
        BackColor = clrBg;
        ForeColor = clrText;
        Font = new Font("Segoe UI", 9f);
        Text = "Data Fusion Arena";
        WindowState = FormWindowState.Normal;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;

    // ──  ──────────────────────────────────────────────────────────
        //  MENU STRIP (hidden from view but functional)
    // ──  ──────────────────────────────────────────────────────────
        menuStrip1 = new MenuStrip
        {
            BackColor = clrSidebar,
            ForeColor = clrText,
            Visible = false,   // hidden - access via sidebar
            Renderer = new MinimalMenuRenderer()
        };

        menuArchivo = MI("Archivo", clrText);
        menuCargarJson = MI("Cargar JSON", clrAcento, (s, e) => BtnCargarJson_Click(s, e));
        menuCargarCsv = MI("Cargar CSV", clrAcento, (s, e) => BtnCargarCsv_Click(s, e));
        menuCargarXml = MI("Cargar XML", clrAcento, (s, e) => BtnCargarXml_Click(s, e));
        menuCargarTxt = MI("Cargar TXT", clrAcento, (s, e) => BtnCargarTxt_Click(s, e));
        menuCargarPersonalizado = MI("Abrir archivo...", clrText, MenuCargarPersonalizado_Click!);
        menuSep1 = new ToolStripSeparator();
        menuExportar = MI("Exportar archivo", clrAcento);
        menuExportCsv = MI("CSV", clrText, (s, e) => BtnExportarCsv_Click(s, e));
        menuExportJson = MI("JSON", clrText, (s, e) => BtnExportarJson_Click(s, e));
        menuExportXml = MI("XML", clrText, (s, e) => BtnExportarXml_Click(s, e));
        menuExportTxt = MI("TXT", clrText, (s, e) => BtnExportarTxt_Click(s, e));
        menuExportar.DropDownItems.AddRange(new ToolStripItem[]
            { menuExportCsv, menuExportJson, menuExportXml, menuExportTxt });
        menuSepExport = new ToolStripSeparator();
        menuLimpiarDatos = MI("Limpiar datos", clrRose, MenuLimpiarDatos_Click!);
        menuSep2 = new ToolStripSeparator();
        menuSalir = MI("Salir", clrRose, MenuSalir_Click!);
        menuArchivo.DropDownItems.AddRange(new ToolStripItem[]
        {
            menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt,
            menuCargarPersonalizado, menuSep1,
            menuExportar,
            menuSepExport, menuLimpiarDatos, menuSep2, menuSalir
        });
        menuBaseDatos = MI("Base de Datos", clrText);
        menuPostgres = MI("PostgreSQL", clrAcento, BtnConectarPostgres_Click!);
        menuMariaDB = MI("MariaDB", clrAcento, BtnConectarMariaDB_Click!);
        menuBaseDatos.DropDownItems.AddRange(new ToolStripItem[] { menuPostgres, menuMariaDB });
        menuAyuda = MI("Ayuda", clrText);
        menuAcercaDe = MI("Acerca de...", clrText, MenuAcercaDe_Click!);
        menuAyuda.DropDownItems.Add(menuAcercaDe);
        menuStrip1.Items.AddRange(new ToolStripItem[] { menuArchivo, menuBaseDatos, menuAyuda });
        MainMenuStrip = menuStrip1;

    // ──  ──────────────────────────────────────────────────────────
        //  STATUS STRIP
    // ──  ──────────────────────────────────────────────────────────
        statusStrip1 = new StatusStrip
        {
            BackColor = clrSurface,
            SizingGrip = false,
            Padding = new Padding(8, 0, 8, 0),
            Font = new Font("Segoe UI", 8f)
        };
        statusStrip1.Renderer = new MinimalMenuRenderer();
        lblStatus = new ToolStripStatusLabel("Listo — carga datos para comenzar")
        {
            ForeColor = clrTextDim,
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        lblStatusRight = new ToolStripStatusLabel("Data Fusion Arena v1.0")
        {
            ForeColor = clrTextMuted,
            TextAlign = ContentAlignment.MiddleRight
        };
        statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatus, lblStatusRight });

    // ──  ──────────────────────────────────────────────────────────
        //  SIDEBAR
    // ──  ──────────────────────────────────────────────────────────
        pnlSidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 280,
            BackColor = clrSidebar
        };

    // ── Logo ──────────────────────────────────────────────────────────
        pnlLogo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = clrSidebar
        };
        pnlSemaforo = new Panel
        {
            Location = new Point(16, 12),
            Size = new Size(60, 20),
            BackColor = Color.Transparent
        };
        var logoAccent = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(3, 110),
            BackColor = clrAcento
        };
        lblLogoTitle = new Label
        {
            Text = "DATA FUSION",
            Location = new Point(16, 32),
            AutoSize = true,
            ForeColor = clrSidebarText,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold)
        };
        lblLogoSub = new Label
        {
            Text = "ARENA · DATA ENGINE",
            Location = new Point(17, 64),
            AutoSize = true,
            ForeColor = clrAcento,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        pnlLogo.Controls.AddRange(new Control[] { logoAccent, lblLogoTitle, lblLogoSub, pnlSemaforo });

    // ── Helpers ──────────────────────────────────────────────────────────
        Panel SbDivider() => new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(40, 0, 0, 0)
        };

        Label SbSection(string text) => new Label
        {
            Text = text.ToUpper(),
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(14, 18, 0, 0),
            ForeColor = clrSidebarLabels,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = clrSidebar
        };

        Button SbBtn(string text, Color accent, EventHandler? click = null)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = clrSidebarText,
                Font = new Font("Segoe UI", 11f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = clrSurface2;
            b.FlatAppearance.MouseDownBackColor = clrAcento;
            if (click != null) b.Click += click;
            return b;
        }

        // ── LOAD FILES SECTION ─────────────────────────────────────
        pnlSidebarFiles = new Panel { Dock = DockStyle.Top, AutoSize = true, BackColor = clrSidebar };

        var lblFilesTitle = SbSection("Cargar datos");
        btnSbCsv = SbBtn("📄  Subir CSV", clrAcento, BtnCargarCsv_Click!);
        btnSbJson = SbBtn("📁  Subir JSON", clrAcento, BtnCargarJson_Click!);
        btnSbXml = SbBtn("📋  Subir XML", clrAcento, BtnCargarXml_Click!);
        btnSbTxt = SbBtn("📝  Subir TXT", clrAcento, BtnCargarTxt_Click!);

        pnlSidebarFiles.Controls.AddRange(new Control[]
        {
            btnSbTxt, btnSbXml, btnSbJson, btnSbCsv,
            lblFilesTitle
        });

        // ── DB SECTION ──────────────────────────────────────────────
        pnlSidebarDB = new Panel { Dock = DockStyle.Top, AutoSize = true, BackColor = clrSidebar };
        var sbDivDB = SbDivider();
        var lblDBTitle = SbSection("Bases de datos");
        btnSbPostgres = SbBtn("🐘  Conectar PostgreSQL", clrAcento, BtnConectarPostgres_Click!);
        btnSbMariaDB = SbBtn("🐬  Conectar MariaDB", clrAcento, BtnConectarMariaDB_Click!);
        var btnSbRefresh = SbBtn("🔄  Actualizar BD", clrAcento, BtnRefresh_Click!);

        pnlSidebarDB.Controls.AddRange(new Control[]
            { btnSbRefresh, btnSbMariaDB, btnSbPostgres, lblDBTitle, sbDivDB });

    // ── INTEGRATION SECTION ──────────────────────────────────────────────────────────
        pnlSidebarApi = new Panel { Dock = DockStyle.Top, AutoSize = true, BackColor = clrSidebar, Visible = true };
        var sbDivApi = SbDivider();
        var lblApiTitle = SbSection("Integración");
        btnSbApi = SbBtn("📍  API Geocoding", clrAcento);
        pnlSidebarApi.Controls.AddRange(new Control[] { btnSbApi, lblApiTitle, sbDivApi });

    // ── TOOLS SECTION ──────────────────────────────────────────────────────────
        pnlSidebarExport = new Panel { Dock = DockStyle.Top, AutoSize = true, BackColor = clrSidebar };
        var sbDivTools = SbDivider();
        var lblToolsTitle = SbSection("Herramientas");
        var btnSbLimpiar = SbBtn("🧹  Limpiar datos", clrAcento, MenuLimpiarDatos_Click!);
        pnlSidebarExport.Controls.AddRange(new Control[] { btnSbLimpiar, lblToolsTitle, sbDivTools });

    // ──  ──────────────────────────────────────────────────────────
        pnlSidebar.SuspendLayout();
        pnlSidebar.Controls.Clear();

        pnlSidebar.Controls.Add(pnlSidebarExport);
        pnlSidebar.Controls.Add(pnlSidebarApi);
        pnlSidebar.Controls.Add(pnlSidebarDB);
        pnlSidebar.Controls.Add(pnlSidebarFiles);
        pnlSidebar.Controls.Add(pnlLogo);

        pnlSidebar.ResumeLayout();

    // ──  ──────────────────────────────────────────────────────────
        //  MAIN CONTENT PANEL
    // ──  ──────────────────────────────────────────────────────────
        pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = clrBg,
            Padding = new Padding(0)
        };

        pnlTopStats = new Panel
        {
            Dock = DockStyle.Top,
            Height = 170, // Mayor altura para evitar que se superponga a las pestañas
            BackColor = clrSurface,
            Padding = new Padding(32, 0, 32, 0)
        };

        var tblHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        tblHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var pnlTitles = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var lblDataset = new Label
        {
            Text = "Dataset",
            Location = new Point(0, 10),
            AutoSize = true,
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 28f, FontStyle.Bold)
        };
        var lblPrincipal = new Label
        {
            Text = "Principal",
            Location = new Point(0, 65),
            AutoSize = true,
            ForeColor = clrAcento,
            Font = new Font("Segoe UI", 32f, FontStyle.Bold)
        };
        lblSubtext = new Label
        {
            Text = "Datos fusionados · Motor activo",
            Location = new Point(4, 135),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 10.5f)
        };
        pnlTitles.Controls.AddRange(new Control[] { lblDataset, lblPrincipal, lblSubtext });

        var pnlHeaderButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 50, 0, 0), // Centrados verticalmente
            BackColor = Color.Transparent
        };



        tblHeader.Controls.Add(pnlTitles, 0, 0);
        tblHeader.Controls.Add(pnlHeaderButtons, 1, 0);
        pnlTopStats.Controls.Add(tblHeader);

    // ── Tab Control ──────────────────────────────────────────────────────────
        tabControl1 = new CustomTabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(160, 42),
            Padding = new Point(16, 8),
            SizeMode = TabSizeMode.Fixed
        };

        tabTodos = new TabPage("📊 DataSet") { BackColor = clrBg, UseVisualStyleBackColor = false };
        tabGraficas = new TabPage("📈 Gráfico") { BackColor = clrBg, UseVisualStyleBackColor = false };


        tabControl1.TabPages.AddRange(new[]
            { tabTodos, tabGraficas });

    // ──  ──────────────────────────────────────────────────────────
    // ── TAB 1 ──────────────────────────────────────────────────────────
    // ──  ──────────────────────────────────────────────────────────
        pnlToolsTodos = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70, // Espacio suficiente para los inputs
            BackColor = clrBg,
            Padding = new Padding(16, 16, 16, 0)
        };
        var toolsBorderBot = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder };

    // ── Reorganizaci ──────────────────────────────────────────────────────────
        lblBuscar = FLabel("Buscar en:", new Point(16, 24), clrTextDim);
        cmbCampoBusqueda = FCombo(new Point(100, 20), 130, clrSurface, clrText, new object[] { }, -1);

        txtBusqueda = new TextBox
        {
            Location = new Point(240, 20),
            Width = 250,
            Height = 32,
            BackColor = Color.White,
            ForeColor = clrText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 11f),
            PlaceholderText = "🔍 Buscar en la tabla..."
        };
        txtBusqueda.KeyPress += (s, e) => { if (e.KeyChar == (char)13) BtnFiltrar_Click(s, EventArgs.Empty); };

        btnFiltrar = FButton("Filtrar", new Point(500, 18), 80, clrSurface2, clrRose, BtnFiltrar_Click!);
        btnLimpiarFiltro = FButton("Limpiar", new Point(590, 18), 80, clrSurface, clrTextDim, BtnLimpiarFiltro_Click!);

        // Separador vertical
        var toolsDiv = new Panel { Location = new Point(680, 15), Size = new Size(1, 40), BackColor = clrBorder };

        lblOrdenar = FLabel("Ordenar por:", new Point(700, 24), clrTextDim);
        cmbCampoOrden = FCombo(new Point(790, 20), 130, clrSurface, clrText, new object[] { }, -1);

        rbAscendente = FRadio("↑ Asc", new Point(930, 10), true, clrBg);
        rbDescendente = FRadio("↓ Desc", new Point(930, 32), false, clrBg);

        btnOrdenar = FButton("Ordenar ▲", new Point(1010, 18), 100, Color.White, clrRose, BtnOrdenar_Click!);
        btnOrdenar.FlatAppearance.BorderSize = 1;
        btnOrdenar.FlatAppearance.BorderColor = clrRose;

        lblContadorTodos = new Label
        {
            Text = "6 registros",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlToolsTodos.Width - 120, 24),
            AutoSize = true,
            Font = new Font("Segoe UI", 10f),
            ForeColor = clrTextDim
        };

        pnlToolsTodos.Controls.AddRange(new Control[]
        {
            toolsBorderBot,
            lblBuscar, cmbCampoBusqueda, txtBusqueda, btnFiltrar, btnLimpiarFiltro,
            toolsDiv,
            lblOrdenar, cmbCampoOrden, rbAscendente, rbDescendente, btnOrdenar,
            lblContadorTodos
        });

        dgvTodos = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrText);
        dgvTodos.Dock = DockStyle.Fill;

        tabTodos.Controls.Add(dgvTodos);
        tabTodos.Controls.Add(pnlToolsTodos);

    // ──  ──────────────────────────────────────────────────────────
    // ── TAB 2 ──────────────────────────────────────────────────────────
    // ──  ──────────────────────────────────────────────────────────
        splitCategoria = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Panel1MinSize = 180,
            FixedPanel = FixedPanel.Panel1,
            BackColor = clrBg,
            SplitterWidth = 1
        };
        splitCategoria.SplitterDistance = 220;

        var pnlCatLeft = splitCategoria.Panel1;
        pnlCatLeft.BackColor = clrSurface;

        var lblCatTitle = new Label
        {
            Text = "Categorías",
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = clrText,
            BackColor = clrSurface,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };
        var catTitleBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };

        lstCategorias = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = clrSurface,
            ForeColor = clrText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9f),
            ItemHeight = 30,
            DrawMode = DrawMode.OwnerDrawFixed
        };
        lstCategorias.DrawItem += LstCat_DrawItem!;
        lstCategorias.SelectedIndexChanged += LstCategorias_SelectedIndexChanged!;

        pnlCatLeft.Controls.AddRange(new Control[] { lstCategorias, catTitleBorder, lblCatTitle });

        lblCatInfo = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = clrTextDim,
            BackColor = clrSurface,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };
        var catInfoBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };





    // ──  ──────────────────────────────────────────────────────────
    // ── TAB 4 ──────────────────────────────────────────────────────────
    // ──  ──────────────────────────────────────────────────────────
        pnlGraficasTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = clrBg
        };
        var grafBorderBot = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder };

        const int gy = 16;   // top Y for all controls
        const int gh = 28;   // fixed height for combos and button
        int gx = 20;         // running X cursor

        lblChartTitle = new Label
        {
            Text = "Configuración de Gráfico",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrRose,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        gx += lblChartTitle.PreferredWidth + 30;

        lblTipoGrafica = new Label
        {
            Text = "Tipo",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 9f)
        };
        gx += lblTipoGrafica.PreferredWidth + 10;

        cmbTipoGrafica = new ComboBox
        {
            Location = new Point(gx, gy),
            Width = 120,
            Height = gh,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = clrSurface,
            ForeColor = clrText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbTipoGrafica.Items.AddRange(new object[] { "Columnas", "Barras", "Pastel" });
        cmbTipoGrafica.SelectedIndex = 0;
        cmbTipoGrafica.SelectedIndexChanged += CmbTipoGrafica_SelectedIndexChanged!;
        gx += 120 + 15;

        btnActualizarGrafica = new Button
        {
            Text = "↺",
            Location = new Point(gx, gy),
            Width = 38,
            Height = gh,
            FlatStyle = FlatStyle.Flat,
            BackColor = clrSurface2,
            ForeColor = clrRose,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Padding = new Padding(0),
            Cursor = Cursors.Hand
        };
        btnActualizarGrafica.FlatAppearance.BorderSize = 0;
        btnActualizarGrafica.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 230, 235);
        btnActualizarGrafica.Click += BtnActualizarGrafica_Click!;
        gx += 38 + 25;

        lblGrupoGrafica = new Label
        {
            Text = "Agrupar por",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 9f)
        };
        gx += lblGrupoGrafica.PreferredWidth + 10;

        cmbGrupoGrafica = new ComboBox
        {
            Location = new Point(gx, gy),
            Width = 180,
            Height = gh,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = clrSurface,
            ForeColor = clrText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbGrupoGrafica.SelectedIndexChanged += CmbGrupoGrafica_SelectedIndexChanged!;
        gx += 180 + 25;

        lblMetricaGrafica = new Label
        {
            Text = "Métrica",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 9f)
        };
        gx += lblMetricaGrafica.PreferredWidth + 10;

        cmbMetricaGrafica = new ComboBox
        {
            Location = new Point(gx, gy),
            Width = 180,
            Height = gh,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = clrSurface,
            ForeColor = clrText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbMetricaGrafica.SelectedIndexChanged += CmbMetricaGrafica_SelectedIndexChanged!;

        pnlGraficasTop.Controls.AddRange(new Control[]
        {
            grafBorderBot,
            lblTipoGrafica, cmbTipoGrafica,
            btnActualizarGrafica,
            lblGrupoGrafica, cmbGrupoGrafica,
            lblMetricaGrafica, cmbMetricaGrafica
        });

        chartMain = new ChartPanel { Dock = DockStyle.Fill, BackColor = clrSurface };
        tabGraficas.Controls.Add(chartMain);
        tabGraficas.Controls.Add(pnlGraficasTop);

    // ──  ──────────────────────────────────────────────────────────
    // ── TAB 5 ──────────────────────────────────────────────────────────
    // ──  ──────────────────────────────────────────────────────────
        pnlProcHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = clrBg,
            Padding = new Padding(0)
        };
        var procBorderBot = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder };

    // ── Row 1 ──────────────────────────────────────────────────────────
        var rowDupes = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(4000, 52),
            BackColor = clrBg
        };
        btnDetectarDuplicados = FButton("Detectar duplicados", new Point(16, 12), 168,
            clrRose, Color.White, BtnDetectarDuplicados_Click!);
        btnEliminarDuplicados = FButton("Eliminar duplicados", new Point(194, 12), 168,
            clrSurface, clrTextDim, BtnEliminarDuplicados_Click!);
        btnEliminarDuplicados.Enabled = false;

        lblProcInfo = new Label
        {
            Text = "Selecciona una operación para comenzar.",
            AutoSize = false,
            Size = new Size(800, 26),
            Location = new Point(376, 16),
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.Transparent
        };
        rowDupes.Controls.AddRange(new Control[]
            { btnDetectarDuplicados, btnEliminarDuplicados, lblProcInfo });

        var rowSep = new Panel { Location = new Point(0, 52), Size = new Size(4000, 1), BackColor = clrBorder };

    // ── Row 2 ──────────────────────────────────────────────────────────
        var rowLinq = new Panel
        {
            Location = new Point(0, 53),
            Size = new Size(4000, 50),
            BackColor = clrBg
        };
        var lblLinqTag = new Label
        {
            Text = "LINQ",
            Location = new Point(16, 16),
            AutoSize = true,
            ForeColor = clrRose,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        var lblCampoL = FLabel("Campo", new Point(68, 16), clrTextDim);
        cmbLinqCampo = FCombo(new Point(130, 12), 120, clrSurface, clrText, new object[] { }, -1);

        var lblBuscarL = FLabel("Buscar", new Point(268, 16), clrTextDim);
        txtLinqFiltro = new TextBox
        {
            Location = new Point(322, 12),
            Width = 155,
            Height = 28,
            BackColor = clrSurface,
            ForeColor = clrText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f)
        };

        btnLinqWhere = FButton(".Where()", new Point(492, 12), 90, clrRose, Color.White, BtnLinqWhere_Click!);
        btnLinqGroupBy = FButton(".GroupBy()", new Point(590, 12), 90, clrRose, Color.White, BtnLinqGroupBy_Click!);
        btnLinqOrderBy = FButton(".OrderBy()", new Point(688, 12), 90, clrRose, Color.White, BtnLinqOrderBy_Click!);
        btnLinqLimpiar = FButton("Limpiar", new Point(786, 12), 72, clrSurface, clrTextDim, BtnLinqLimpiar_Click!);

        rowLinq.Controls.AddRange(new Control[]
        {
            lblLinqTag, lblCampoL, cmbLinqCampo, lblBuscarL,
            txtLinqFiltro, btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy, btnLinqLimpiar
        });

        pnlProcHeader.Controls.AddRange(new Control[] { procBorderBot, rowSep, rowLinq, rowDupes });

        dgvProcesamiento = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrRose);
        dgvProcesamiento.Dock = DockStyle.Fill;

        dgvCategoria = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrRose);
        dgvEstadisticas = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrRose);


    // ──  ──────────────────────────────────────────────────────────
        //  ASSEMBLE MAIN PANEL
    // ──  ──────────────────────────────────────────────────────────
        pnlMain.Controls.Add(tabControl1);
        pnlMain.Controls.Add(pnlTopStats);

    // ──  ──────────────────────────────────────────────────────────
        //  FORM LAYOUT
    // ──  ──────────────────────────────────────────────────────────
        Controls.Add(pnlMain);
        Controls.Add(pnlSidebar);
        Controls.Add(menuStrip1);
        Controls.Add(statusStrip1);
        ResumeLayout(false);
        PerformLayout();
    }

    // ──  ──────────────────────────────────────────────────────────
    //  FACTORY HELPERS
    // ──  ──────────────────────────────────────────────────────────

    private static DataGridView BuildGrid(Color bg, Color surface, Color border,
        Color text, Color textDim, Color accent)
    {
        var dgv = new DataGridView
        {
            AutoGenerateColumns = false,
            BackgroundColor = surface, // Changed to white background
            GridColor = border,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            RowTemplate = { Height = 45 },
            ScrollBars = ScrollBars.Both,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
        };
        dgv.DefaultCellStyle.BackColor = surface;
        dgv.DefaultCellStyle.ForeColor = text;
        dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
        dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#FFBDE3");
        dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

        dgv.AlternatingRowsDefaultCellStyle.BackColor = surface;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = surface;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = text;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        dgv.ColumnHeadersHeight = 45;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.EnableHeadersVisualStyles = false;

        typeof(DataGridView)
            .GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(dgv, true);

        return dgv;
    }

    private static Label FLabel(string text, Point loc, Color fore) =>
        new Label
        {
            Text = text,
            Location = loc,
            AutoSize = true,
            ForeColor = fore,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.Transparent
        };

    private static Button FButton(string text, Point loc, int width,
        Color bg, Color fg, EventHandler click)
    {
        var b = new Button
        {
            Text = text,
            Location = loc,
            Width = width,
            Height = 32,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.1f);
        b.Click += click;
        return b;
    }

    private static RadioButton FRadio(string text, Point loc, bool chk, Color bg) =>
        new RadioButton
        {
            Text = text,
            Location = loc,
            AutoSize = true,
            Checked = chk,
            ForeColor = Color.FromArgb(108, 117, 125),
            BackColor = bg,
            Font = new Font("Segoe UI", 9f)
        };

    private static ComboBox FCombo(Point loc, int width, Color bg, Color fg,
        object[] items, int sel)
    {
        var c = new ComboBox
        {
            Location = loc,
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f)
        };
        c.Items.AddRange(items);
        if (sel >= 0 && items.Length > sel) c.SelectedIndex = sel;
        return c;
    }

    private static ToolStripMenuItem MI(string text, Color fore, EventHandler? click = null)
    {
        var m = new ToolStripMenuItem(text) { ForeColor = fore };
        if (click != null) m.Click += click;
        return m;
    }

    // ──  ──────────────────────────────────────────────────────────
    //  CUSTOM LIST BOX DRAWING
    // ──  ──────────────────────────────────────────────────────────
    private void LstCat_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var clrSurface = Color.White;
        var clrText = Color.DimGray;
        var clrRose = ColorTranslator.FromHtml("#FF80BF");
        var clrBorder = Color.Lavender;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(selected ? ColorTranslator.FromHtml("#FFBDE3") : clrSurface);
        g.FillRectangle(bgBrush, e.Bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(clrRose);
            g.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        string text = lstCategorias.Items[e.Index]?.ToString() ?? "";
        using var textBrush = new SolidBrush(selected ? clrRose : clrText);
        using var font = new Font("Segoe UI", 9.5f, selected ? FontStyle.Bold : FontStyle.Regular);
        var textRect = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, textRect, sf);

        using var borderPen = new Pen(clrBorder, 1);
        g.DrawLine(borderPen, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }
}

    // ──  ──────────────────────────────────────────────────────────
    // ── CUSTOM TAB CONTROL ──────────────────────────────────────────────────────────
    // ──  ──────────────────────────────────────────────────────────
class CustomTabControl : TabControl
{
    private static readonly Color BgColor = Color.FromArgb(247, 246, 252);
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(236, 236, 245);
    private static readonly Color AccentColor = Color.FromArgb(232, 48, 90); // Rose
    private static readonly Color TextActive = Color.FromArgb(232, 48, 90);  // Rose text for active
    private static readonly Color TextInactive = Color.FromArgb(108, 117, 125);

    public CustomTabControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        // 1. Fill entire control background light
        g.Clear(SurfaceColor);

        // 2. Fill the tab strip area with Background Color
    // ── Utilizamos el tama ──────────────────────────────────────────────────────────
        int tabH = TabCount > 0 ? GetTabRect(0).Bottom : ItemSize.Height + 2;
        var tabStrip = new Rectangle(0, 0, Width, tabH);
        using (var sb = new SolidBrush(BgColor))
            g.FillRectangle(sb, tabStrip);

        // 3. Draw a bottom border under the tab strip
        using (var bp = new Pen(AccentColor, 2)) // Thick red border matching the design
            g.DrawLine(bp, 0, tabH, Width, tabH);

        // 4. Draw each tab
        for (int i = 0; i < TabCount; i++)
        {
            var rect = GetTabRect(i);
            bool sel = SelectedIndex == i;
            string txt = TabPages[i].Text;

            // Tab background (Matches surface when active, bg when inactive)
            using (var tb = new SolidBrush(sel ? SurfaceColor : BgColor))
                g.FillRectangle(tb, rect);

            // Bottom accent line on selected tab
            if (sel)
            {
                using var mb = new SolidBrush(AccentColor);
                g.FillRectangle(mb, rect.X, rect.Bottom - 2, rect.Width, 2);
            }

            // Tab text
            using var tf = new SolidBrush(sel ? TextActive : TextInactive);
            using var font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(txt, font, tf, rect, sf);
        }

        // 5. Fill the rest of the tab strip to the right of the last tab
        if (TabCount > 0)
        {
            var lastTab = GetTabRect(TabCount - 1);
            int fillX = lastTab.Right;
            int fillW = Width - fillX;
            if (fillW > 0)
            {
                using var fb = new SolidBrush(BgColor);
                g.FillRectangle(fb, fillX, 0, fillW, tabH);
            }
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Suppress default background paint
    }
}

    // ──  ──────────────────────────────────────────────────────────
//  MINIMAL MENU RENDERER
    // ──  ──────────────────────────────────────────────────────────
class MinimalMenuRenderer : ToolStripProfessionalRenderer
{
    public MinimalMenuRenderer() : base(new MinimalColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.ForeColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        var g = e.Graphics;
        var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        using var b = new SolidBrush(ColorTranslator.FromHtml("#FFBDE3"));
        g.FillRectangle(b, r);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        int y = e.Item.Height / 2;
        using var p = new Pen(Color.FromArgb(236, 236, 245));
        g.DrawLine(p, 8, y, e.Item.Width - 8, y);
    }
}

class MinimalColorTable : ProfessionalColorTable
{
    private static readonly Color Bg = ColorTranslator.FromHtml("#FFF5F9");
    private static readonly Color Surface = Color.White;
    private static readonly Color Border = Color.Lavender;
    private static readonly Color Hover = ColorTranslator.FromHtml("#FFE4F2");

    public override Color MenuItemSelected => Hover;
    public override Color MenuItemBorder => Border;
    public override Color MenuBorder => Border;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Surface;
    public override Color MenuItemPressedGradientEnd => Surface;
    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color MenuStripGradientBegin => Bg;
    public override Color MenuStripGradientEnd => Bg;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;
    public override Color StatusStripGradientBegin => Surface;
    public override Color StatusStripGradientEnd => Surface;
    public override Color ToolStripBorder => Border;
    public override Color ToolStripGradientBegin => Surface;
    public override Color ToolStripGradientMiddle => Surface;
    public override Color ToolStripGradientEnd => Surface;
}
