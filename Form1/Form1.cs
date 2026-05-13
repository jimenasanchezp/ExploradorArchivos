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
using ExploradorArchivos.Video;

namespace ExploradorArchivos;

public partial class Form1 : Form
{
    // === Estado de navegación ===
    private string _rutaActual = "Inicio";
    private Stack<string> _historial = new Stack<string>();
    private List<FileSystemItem> _itemsActuales = new List<FileSystemItem>();
    private string _filtroActivo = "Todos";

    // === Componentes de UI ===
    private ListViewSorter _sorter = default!;
    private QuickLookForm? _quickLookForm;
    private FlowLayoutPanel _pnlFiltros = default!;
    private ImageList _imageListMiniaturas = default!;
    private Button _btnToggleVista = default!;
    private FlowLayoutPanel _flpBreadcrumbs = default!;

    public Form1()
    {
        InitializeComponent();
        ConfigurarUI();
        ConectarEventos();
        CargarDirectorio(_rutaActual);
    }

    private void ConfigurarUI()
    {
        this.BackColor = ThemeRenderer.MainBg;
        listViewPrincipal.BackColor = Color.White;
        listViewPrincipal.BorderStyle = BorderStyle.None;
        treeViewLateral.BackColor = ThemeRenderer.SecondaryBg;
        treeViewLateral.BorderStyle = BorderStyle.None;

        _sorter = new ListViewSorter();
        listViewPrincipal.ListViewItemSorter = _sorter;

        ConfigurarFiltrosRapidos();
        ConfigurarVistaMiniaturas();

        listViewPrincipal.ShowGroups = true;

        // OwnerDraw para diseño Kawaii 95
        listViewPrincipal.OwnerDraw = true;
        listViewPrincipal.DrawColumnHeader += ThemeRenderer.DrawListViewColumnHeader;
        listViewPrincipal.DrawItem += ThemeRenderer.DrawListViewItem;
        listViewPrincipal.DrawSubItem += ThemeRenderer.DrawListViewSubItem;

        treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        treeViewLateral.DrawNode += ThemeRenderer.DrawTreeNode;

        // --- 1. REORGANIZACIÓN DE BARRA SUPERIOR (pnlTop) ---
        pnlTop.BackColor = ThemeRenderer.Lila;
        pnlTop.Height = 70; // Aumentar altura para los grupos
        pnlTop.Controls.Clear();
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTop.ClientRectangle, true);

        // Grupo: Navegación
        Panel pnlNav = CrearGrupoHerramientas("Navegación 🎀", 10, 10, 150);
        pnlNav.Controls.Add(btnAtras); btnAtras.Location = new Point(10, 22); btnAtras.Size = new Size(35, 30);
        pnlNav.Controls.Add(btnSubir); btnSubir.Location = new Point(50, 22); btnSubir.Size = new Size(35, 30);
        pnlNav.Controls.Add(btnActualizar); btnActualizar.Location = new Point(90, 22); btnActualizar.Size = new Size(35, 30);
        pnlTop.Controls.Add(pnlNav);

        // Grupo: Dirección
        Panel pnlAddr = CrearGrupoHerramientas("Dirección ✨", 170, 10, pnlTop.Width - 550);
        pnlAddr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlAddr.Controls.Add(pnlAddressBorder);
        pnlAddressBorder.Location = new Point(10, 22);
        pnlAddressBorder.Size = new Size(pnlAddr.Width - 20, 30);
        pnlAddressBorder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlTop.Controls.Add(pnlAddr);

        // Grupo: Acciones
        Panel pnlActions = CrearGrupoHerramientas("Acciones 🌸", pnlTop.Width - 370, 10, 360);
        pnlActions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlActions.Controls.Add(btnNuevaCarpeta); btnNuevaCarpeta.Location = new Point(10, 22); btnNuevaCarpeta.Size = new Size(110, 30);
        pnlActions.Controls.Add(btnExportarCSV); btnExportarCSV.Location = new Point(125, 22); btnExportarCSV.Size = new Size(110, 30);
        pnlActions.Controls.Add(_btnToggleVista); _btnToggleVista.Location = new Point(240, 22); _btnToggleVista.Size = new Size(110, 30);
        pnlTop.Controls.Add(pnlActions);

        // --- 2. REFINAMIENTO DEL PANEL LATERAL (Sidebar) ---
        pnlSearch.BackColor = ThemeRenderer.SecondaryBg;
        pnlSearch.Height = 65;
        pnlSearch.Controls.Clear();
        pnlSearch.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlSearch.ClientRectangle, false);

        Label lblSearchTitle = new Label { Text = "✨ Buscar Cositas", Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(10, 5), AutoSize = true };
        pnlSearch.Controls.Add(lblSearchTitle);
        pnlSearch.Controls.Add(pnlSearchBorder);
        pnlSearchBorder.Location = new Point(10, 25);
        pnlSearchBorder.Width = pnlSearch.Width - 95;
        pnlSearch.Controls.Add(btnBuscar);
        btnBuscar.Location = new Point(pnlSearch.Width - 80, 25);
        btnBuscar.Size = new Size(70, 30);

        // --- 3. BARRA DE FILTROS INTEGRADA ---
        _pnlFiltros.BackColor = ThemeRenderer.MainBg;
        _pnlFiltros.Height = 45;
        _pnlFiltros.Padding = new Padding(10, 10, 0, 0);
        _pnlFiltros.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, _pnlFiltros.ClientRectangle, false);

        // --- Configurar Botones (Estilo Retro) ---
        ConfigurarBotonRetro(btnAtras, "⬅️");
        ConfigurarBotonRetro(btnSubir, "⬆️");
        ConfigurarBotonRetro(btnActualizar, "🔄");
        ConfigurarBotonRetro(btnNuevaCarpeta, "📁+");
        ConfigurarBotonRetro(btnExportarCSV, "📊 CSV");
        ConfigurarBotonRetro(btnBuscar, "🔍");
        ConfigurarBotonRetro(_btnToggleVista, "🖼️ Vista");

        // --- Barra de Direcciones ---
        pnlAddressBorder.BackColor = Color.White;
        pnlAddressBorder.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlAddressBorder.ClientRectangle, false);
        
        _flpBreadcrumbs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeRenderer.AddressYellow,
            WrapContents = false,
            AutoScroll = false,
            Cursor = Cursors.IBeam
        };
        _flpBreadcrumbs.Click += (s, e) => MostrarTextBoxDireccion();
        pnlAddressBorder.Controls.Add(_flpBreadcrumbs);
        _flpBreadcrumbs.BringToFront();

        txtDireccion.BackColor = ThemeRenderer.AddressYellow;
        txtDireccion.Visible = false;
        txtDireccion.Leave += (s, e) => OcultarTextBoxDireccion();

        // --- Elementos Decorativos ---
        Label lblKawaii3 = new Label { Text = "🌸", Font = new Font("Segoe UI", 16), AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Location = new Point(5, 5), BackColor = Color.Transparent };
        pnlBottom.BackColor = ThemeRenderer.Lila;
        pnlBottom.Controls.Add(lblKawaii3);
        pnlBottom.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlBottom.ClientRectangle, true);

        // --- Barra de Estado y Papelera ---
        lblStatus.ForeColor = ThemeRenderer.MainText;
        lblStatus.Font = new Font("Segoe UI", 9, FontStyle.Italic);
        pnlTrash.BackColor = ThemeRenderer.MainBg;
        pnlTrash.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTrash.ClientRectangle, false);
        lblTrash.ForeColor = ThemeRenderer.SecondaryText;
        lblTrash.Text = "🌸 Papelera 🌸";

        // --- Reordenar paneles ---
        splitContainerMain.Panel1.Controls.Clear();
        splitContainerMain.Panel2.Controls.Clear();

        splitContainerMain.Panel1.Controls.Add(treeViewLateral);
        splitContainerMain.Panel1.Controls.Add(pnlSearch);
        
        // Sticker Kawaii en la esquina inferior del Sidebar
        Label lblSticker = new Label 
        { 
            Text = "🐰✨", 
            Font = new Font("Segoe UI", 24), 
            AutoSize = true, 
            BackColor = Color.Transparent, 
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(180, splitContainerMain.Panel1.Height - 150)
        };
        splitContainerMain.Panel1.Controls.Add(lblSticker);
        lblSticker.BringToFront();

        pnlSearch.BringToFront();

        splitContainerMain.Panel2.Controls.Add(listViewPrincipal);
        splitContainerMain.Panel2.Controls.Add(_pnlFiltros);
        _pnlFiltros.BringToFront();

        splitContainerMain.SplitterDistance = 250;
    }

    private Panel CrearGrupoHerramientas(string titulo, int x, int y, int ancho)
    {
        Panel pnl = new Panel { Location = new Point(x, y), Size = new Size(ancho, 55), BackColor = Color.Transparent };
        Label lbl = new Label { Text = titulo, Location = new Point(5, 2), AutoSize = true, Font = new Font("Segoe UI", 7, FontStyle.Bold), ForeColor = ThemeRenderer.SecondaryText };
        pnl.Controls.Add(lbl);
        pnl.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, new Rectangle(0, 15, pnl.Width - 1, pnl.Height - 16), false);
        return pnl;
    }

    private void ConfigurarBotonRetro(Button btn, string emoji)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = ThemeRenderer.Lila;
        btn.Text = emoji + " " + btn.Text.Replace("📊 ", "").Replace("📁 ", "").Replace("⟳", "").Replace("▲", "").Replace("◄", "").Replace("🖼️ ", "");
        btn.Font = new Font("Segoe UI Emoji", 9, FontStyle.Bold);
        
        bool isPressed = false;

        btn.MouseDown += (s, e) => { isPressed = true; btn.Invalidate(); };
        btn.MouseUp += (s, e) => { isPressed = false; btn.Invalidate(); };

        btn.Paint += (s, e) => {
            ThemeRenderer.DrawRetroBorder(e.Graphics, btn.ClientRectangle, !isPressed);
            if (isPressed)
            {
                // Mover el texto 1px abajo y derecha cuando se presiona
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