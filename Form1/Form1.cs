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
        listViewPrincipal.BorderStyle = BorderStyle.None;
        treeViewLateral.BorderStyle = BorderStyle.None;

        _sorter = new ListViewSorter();
        listViewPrincipal.ListViewItemSorter = _sorter;

        ConfigurarFiltrosRapidos();
        ConfigurarVistaMiniaturas();

        listViewPrincipal.ShowGroups = true;

        // OwnerDraw para diseño flat
        listViewPrincipal.OwnerDraw = true;
        listViewPrincipal.DrawColumnHeader += ThemeRenderer.DrawListViewColumnHeader;
        listViewPrincipal.DrawItem += ThemeRenderer.DrawListViewItem;
        listViewPrincipal.DrawSubItem += ThemeRenderer.DrawListViewSubItem;

        // Columna de fecha
        listViewPrincipal.Columns.Add("Fecha de Modificación", 150);

        treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        treeViewLateral.DrawNode += ThemeRenderer.DrawTreeNode;

        // Breadcrumbs
        _flpBreadcrumbs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#FFF5F8"),
            WrapContents = false,
            AutoScroll = false,
            Cursor = Cursors.IBeam
        };
        _flpBreadcrumbs.Click += (s, e) => MostrarTextBoxDireccion();

        pnlAddressBorder.Controls.Add(_flpBreadcrumbs);
        _flpBreadcrumbs.BringToFront();

        txtDireccion.Visible = false;
        txtDireccion.Leave += (s, e) => OcultarTextBoxDireccion();

        // === Reordenar paneles: Árbol a la izquierda, ListView a la derecha ===
        splitContainerMain.Panel1.Controls.Clear();
        splitContainerMain.Panel2.Controls.Clear();

        splitContainerMain.Panel1.Controls.Add(treeViewLateral);
        splitContainerMain.Panel1.Controls.Add(pnlSearch);
        pnlSearch.BringToFront();

        splitContainerMain.Panel2.Controls.Add(listViewPrincipal);
        splitContainerMain.Panel2.Controls.Add(_pnlFiltros);
        _pnlFiltros.BringToFront();

        splitContainerMain.SplitterDistance = 250;
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