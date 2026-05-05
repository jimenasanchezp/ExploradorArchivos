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

namespace ExploradorArchivos;

public partial class Form1 : Form
{
    private string _rutaActual = "Inicio";
    private Stack<string> _historial = new Stack<string>();
    private List<FileSystemItem> _itemsActuales = new List<FileSystemItem>();
    private ListViewSorter _sorter = default!;
    private QuickLookForm? _quickLookForm;
    private string _filtroActivo = "Todos";
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
        // Aplicar diseño flat a la UI
        listViewPrincipal.BorderStyle = BorderStyle.None;
        treeViewLateral.BorderStyle = BorderStyle.None;

        _sorter = new ListViewSorter();
        listViewPrincipal.ListViewItemSorter = _sorter;

        ConfigurarFiltrosRapidos(); // 
        ConfigurarVistaMiniaturas(); //

        // Opcional: Activar grupos visuales para la vista de Detalles
        listViewPrincipal.ShowGroups = true;

        // OwnerDraw
        listViewPrincipal.OwnerDraw = true;
        listViewPrincipal.DrawColumnHeader += ThemeRenderer.DrawListViewColumnHeader;
        listViewPrincipal.DrawItem += ThemeRenderer.DrawListViewItem;
        listViewPrincipal.DrawSubItem += ThemeRenderer.DrawListViewSubItem;

        //columna fecha
        listViewPrincipal.Columns.Add("Fecha de Modificación", 150);

        treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        treeViewLateral.DrawNode += ThemeRenderer.DrawTreeNode;

        // Configuración de Breadcrumbs (Migas de pan)
        _flpBreadcrumbs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#FFF5F8"),
            WrapContents = false,
            AutoScroll = false,
            Cursor = Cursors.IBeam // Para que el usuario sepa que puede dar clic para escribir
        };
        _flpBreadcrumbs.Click += (s, e) => MostrarTextBoxDireccion();

        // Lo añadimos encima del TextBox
        pnlAddressBorder.Controls.Add(_flpBreadcrumbs);
        _flpBreadcrumbs.BringToFront();

        // Ocultamos el TextBox por defecto y lo mostramos solo al editar
        txtDireccion.Visible = false;
        txtDireccion.Leave += (s, e) => OcultarTextBoxDireccion();

        // ==========================================
        // MOVER PANEL LATERAL A LA IZQUIERDA
        // ==========================================
        splitContainerMain.Panel1.Controls.Clear();
        splitContainerMain.Panel2.Controls.Clear();

        // Panel Izquierdo: Buscador y Árbol de navegación
        splitContainerMain.Panel1.Controls.Add(treeViewLateral);
        splitContainerMain.Panel1.Controls.Add(pnlSearch);
        pnlSearch.BringToFront(); // Asegurar que el buscador quede arriba

        // Panel Derecho: Filtros rápidos y Lista de Archivos principal
        splitContainerMain.Panel2.Controls.Add(listViewPrincipal);
        splitContainerMain.Panel2.Controls.Add(_pnlFiltros);
        _pnlFiltros.BringToFront(); // Asegurar que los filtros queden arriba

        // Ajustar el tamaño del panel izquierdo
        splitContainerMain.SplitterDistance = 250;
    }

    private void ConectarEventos()
    {
        // Navegación
        btnAtras.Click += (s, e) => { if (_historial.Count > 0) CargarDirectorio(_historial.Pop(), false); };
        btnSubir.Click += (s, e) => {
            var parent = Directory.GetParent(_rutaActual);
            if (parent != null) CargarDirectorio(parent.FullName);
        };
        btnActualizar.Click += (s, e) => CargarDirectorio(_rutaActual, false);
        txtDireccion.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CargarDirectorio(txtDireccion.Text); } };

        // ListView Acciones
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

        // Quick Look (Vista Previa)
        listViewPrincipal.KeyDown += ListViewPrincipal_KeyDown;

        // TreeView
        treeViewLateral.NodeMouseDoubleClick += TreeViewLateral_NodeMouseDoubleClick;
    }

    private void PoblarListViewDesdeMemoria()
    {
        listViewPrincipal.BeginUpdate();
        listViewPrincipal.Items.Clear();
        listViewPrincipal.Groups.Clear();

        // 1. Filtrar usando el Chip activo (memoria RAM pura, muy rápido)
        var itemsFiltrados = _itemsActuales.Where(x =>
            _filtroActivo == "Todos" ||
            (_filtroActivo == "Carpetas" && x.EsCarpeta) ||
            (!x.EsCarpeta && x.CategoriaVisual == _filtroActivo)
        ).ToList();

        foreach (var item in itemsFiltrados)
        {
            var lvi = new ListViewItem(item.Nombre) { Tag = item.RutaCompleta };
            lvi.SubItems.Add(item.Tipo);
            lvi.SubItems.Add(item.TamanoTexto);
            lvi.SubItems.Add(item.InfoAdicional);
            lvi.SubItems.Add(item.FechaModificacion.ToString("dd/MM/yyyy HH:mm"));

            // Agrupación visual (Lo que te propuse antes)
            var grupo = listViewPrincipal.Groups[item.CategoriaVisual];
            if (grupo == null)
            {
                grupo = new ListViewGroup(item.CategoriaVisual, item.CategoriaVisual);
                listViewPrincipal.Groups.Add(grupo);
            }
            lvi.Group = grupo;

            // Asignar icono base para la vista de cuadrícula
            lvi.ImageKey = item.EsCarpeta ? "folder" : "file";

            listViewPrincipal.Items.Add(lvi);
        }
        listViewPrincipal.EndUpdate();
    }

    private void TreeViewLateral_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        // Si el nodo tiene una ruta en su Tag, navegamos hacia allá
        if (e.Node.Tag != null)
        {
            string? ruta = e.Node.Tag.ToString();
            if (string.IsNullOrEmpty(ruta)) return;

            if (Directory.Exists(ruta))
                CargarDirectorio(ruta);
            else
                AbrirArchivoConAppPredeterminada(ruta); // Por si dan doble clic a un archivo
        }
    }

    private async void CargarDirectorio(string ruta, bool guardarHistorial = true)
    {
        if (guardarHistorial && !string.IsNullOrEmpty(_rutaActual) && _rutaActual != ruta)
            _historial.Push(_rutaActual);

        _rutaActual = ruta;

        // 1. VISTA DE INICIO (Dashboard)
        if (ruta == "Inicio")
        {
            txtDireccion.Text = "🏠 Inicio";
            ActualizarBreadcrumbs();
            OcultarTextBoxDireccion();
            GenerarVistaInicio();
            PoblarTreeViewNormal();
            return;
        }

        // 2. VISTA DE ESTE EQUIPO (Discos duros)
        if (ruta == "EsteEquipo")
        {
            txtDireccion.Text = "💻 Este Equipo";
            ActualizarBreadcrumbs();
            OcultarTextBoxDireccion();
            GenerarVistaEsteEquipo();
            PoblarTreeViewNormal();
            return;
        }

        // 3. NAVEGACIÓN NORMAL (Carpetas reales)
        if (!Directory.Exists(ruta)) return;

        txtDireccion.Text = _rutaActual;
        ActualizarBreadcrumbs();
        OcultarTextBoxDireccion();
        lblStatus.Text = "Cargando directorio...";

        _imageListMiniaturas.Images.Clear();
        _itemsActuales = await FileService.ObtenerContenidoAsync(_rutaActual);

        PoblarListViewDesdeMemoria();
        ActualizarEstadisticas();
        PoblarTreeViewNormal();

        _ = GenerarMiniaturasAsync();
    }
    private void GenerarVistaInicio()
    {
        listViewPrincipal.BeginUpdate();
        listViewPrincipal.Items.Clear();
        listViewPrincipal.Groups.Clear();

        // Grupo 1: Carpetas Principales
        ListViewGroup grpAccesos = new ListViewGroup("Carpetas Principales", "📌 Carpetas Principales");
        listViewPrincipal.Groups.Add(grpAccesos);

        var carpetas = new Dictionary<string, string>
    {
        { "Escritorio", Environment.GetFolderPath(Environment.SpecialFolder.Desktop) },
        { "Descargas", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") },
        { "Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
        { "Imágenes", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
        { "Música", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) },
        { "Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) }
    };

        foreach (var kvp in carpetas)
        {
            if (Directory.Exists(kvp.Value))
            {
                var lvi = new ListViewItem(kvp.Key) { Tag = kvp.Value, Group = grpAccesos };
                lvi.SubItems.Add("Carpeta");
                lvi.SubItems.Add("");
                lvi.SubItems.Add("Directorio del usuario");
                lvi.SubItems.Add(""); // Sin fecha para que quede limpio
                listViewPrincipal.Items.Add(lvi);
            }
        }

        // Grupo 2: Archivos Recientes
        ListViewGroup grpRecientes = new ListViewGroup("Archivos Recientes", "🕒 Archivos Recientes");
        listViewPrincipal.Groups.Add(grpRecientes);

        try
        {
            // Traemos los últimos 15 archivos modificados en Documentos
            var dirDocs = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            var recientes = dirDocs.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                                   .OrderByDescending(f => f.LastWriteTime)
                                   .Take(15);

            foreach (var f in recientes)
            {
                var lvi = new ListViewItem(f.Name) { Tag = f.FullName, Group = grpRecientes };
                lvi.SubItems.Add(f.Extension.ToUpper() + " File");
                lvi.SubItems.Add((f.Length / 1024).ToString() + " KB");
                lvi.SubItems.Add("Modificado recientemente");
                lvi.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                listViewPrincipal.Items.Add(lvi);
            }
        }
        catch { /* Ignorar errores de permisos */ }

        listViewPrincipal.EndUpdate();
        lblStatus.Text = "🏠 Vista de Inicio cargada.";
        _itemsActuales.Clear(); // Limpiamos para que los filtros superiores no hagan cosas raras aquí
    }

    private void GenerarVistaEsteEquipo()
    {
        listViewPrincipal.BeginUpdate();
        listViewPrincipal.Items.Clear();
        listViewPrincipal.Groups.Clear();

        ListViewGroup grpDiscos = new ListViewGroup("Unidades de Disco", "💻 Unidades de Disco");
        listViewPrincipal.Groups.Add(grpDiscos);

        // Iteramos por todos los discos duros conectados (C:, D:, USBs)
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                var lvi = new ListViewItem($"{drive.Name} ({drive.VolumeLabel})") { Tag = drive.Name, Group = grpDiscos };
                lvi.SubItems.Add("Unidad Local");
                lvi.SubItems.Add((drive.TotalSize / 1073741824).ToString() + " GB"); // Tamaño total
                lvi.SubItems.Add($"Libre: {(drive.AvailableFreeSpace / 1073741824)} GB"); // Espacio libre
                lvi.SubItems.Add("");
                listViewPrincipal.Items.Add(lvi);
            }
        }

        listViewPrincipal.EndUpdate();
        lblStatus.Text = "💻 Vista de Este Equipo cargada.";
        _itemsActuales.Clear();
    }

    // ==========================================
    // TREEVIEW (PANEL LATERAL)
    // ==========================================
    private void PoblarTreeViewNormal()
    {
        treeViewLateral.BeginUpdate();
        treeViewLateral.Nodes.Clear();

        // 1. PINNED / ACCESOS RÁPIDOS (Siempre visibles)
        TreeNode nodoFavoritos = new TreeNode("📌 Accesos Rápidos");
        nodoFavoritos.Nodes.Add(new TreeNode("🏠 Inicio") { Tag = "Inicio" });
        nodoFavoritos.Nodes.Add(new TreeNode("🖥️ Escritorio") { Tag = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) });
        nodoFavoritos.Nodes.Add(new TreeNode("📥 Descargas") { Tag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") });
        nodoFavoritos.Nodes.Add(new TreeNode("📄 Documentos") { Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) });
        nodoFavoritos.Nodes.Add(new TreeNode("🖼️ Imágenes") { Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) });
        nodoFavoritos.Nodes.Add(new TreeNode("🎵 Música") { Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) });
        nodoFavoritos.Nodes.Add(new TreeNode("🎬 Videos") { Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) });
        treeViewLateral.Nodes.Add(nodoFavoritos);
        nodoFavoritos.Expand();

        // 2. ESTE EQUIPO (Discos Duros)
        TreeNode nodoEquipo = new TreeNode("💻 Este Equipo") { Tag = "EsteEquipo" };
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                nodoEquipo.Nodes.Add(new TreeNode($"💽 {drive.Name} ({drive.VolumeLabel})") { Tag = drive.Name });
            }
        }
        treeViewLateral.Nodes.Add(nodoEquipo);
        nodoEquipo.Expand();

        // 3. CARPETA ACTUAL (Solo se dibuja si estamos dentro de una ruta real navegando)
        if (_rutaActual != "Inicio" && _rutaActual != "EsteEquipo" && Directory.Exists(_rutaActual))
        {
            TreeNode nodoActual = new TreeNode($"📂 Abierto: {new DirectoryInfo(_rutaActual).Name}");
            var grupos = _itemsActuales.GroupBy(x => x.CategoriaVisual);

            foreach (var grupo in grupos.OrderBy(g => g.Key))
            {
                TreeNode nodoPadre = new TreeNode($"{grupo.Key} ({grupo.Count()})");
                foreach (var item in grupo)
                {
                    // En el árbol izquierdo solo mostramos las subcarpetas, no los archivos
                    if (item.EsCarpeta)
                        nodoPadre.Nodes.Add(new TreeNode("📁 " + item.Nombre) { Tag = item.RutaCompleta });
                }
                // Solo agregamos la categoría si tiene carpetas adentro
                if (nodoPadre.Nodes.Count > 0)
                    nodoActual.Nodes.Add(nodoPadre);
            }
            treeViewLateral.Nodes.Add(nodoActual);
            nodoActual.ExpandAll();
        }

        treeViewLateral.EndUpdate();
    }

    private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            string filtro = txtBuscar.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filtro))
            {
                PoblarTreeViewNormal();
                return;
            }

            treeViewLateral.Nodes.Clear();
            var resultados = _itemsActuales.Where(x => x.Nombre.ToLower().Contains(filtro)).ToList();
            TreeNode nodoSearch = new TreeNode($"Búsqueda: '{filtro}' ({resultados.Count})");

            foreach (var r in resultados)
                nodoSearch.Nodes.Add(new TreeNode(r.Nombre) { Tag = r.RutaCompleta });

            treeViewLateral.Nodes.Add(nodoSearch);
            nodoSearch.Expand();
        }
    }

    // ==========================================
    // INTERACCIÓN: DOBLE CLIC Y PAPELERA
    // ==========================================
    private void ListViewPrincipal_DoubleClick(object sender, EventArgs e)
    {
        if (listViewPrincipal.SelectedItems.Count == 0) return;
        string? ruta = listViewPrincipal.SelectedItems[0].Tag?.ToString();
        if (string.IsNullOrEmpty(ruta)) return;

        if (Directory.Exists(ruta)) CargarDirectorio(ruta);
        else AbrirArchivoConAppPredeterminada(ruta); // Para abrir el archivo
    }

    private void AbrirArchivoConAppPredeterminada(string ruta)
    {
        try 
        {
            string ext = Path.GetExtension(ruta).ToLower();
            
            // 1. Imágenes
            string[] imgExt = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            if (imgExt.Contains(ext))
            {
                var view = new ImageViewerForm(ruta);
                view.Show();
                return;
            }

            // 2. Multimedia (Audio/Video)
            string[] mediaExt = { ".mp3", ".wav", ".mp4", ".mov", ".webm", ".m4a", ".ogg" };
            if (mediaExt.Contains(ext))
            {
                var player = new MediaPlayerForm(ruta);
                player.Show();
                return;
            }

            // 3. Texto y Código
            string[] txtExt = { ".txt", ".json", ".xml", ".csv", ".cs", ".html", ".css", ".js", ".md", ".py" };
            if (txtExt.Contains(ext))
            {
                var view = new FileViewerForm(ruta);
                view.Show();
                return;
            }

            // 4. Otros (Abrir con Windows)
            Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true }); 
        }
        catch (Exception ex) 
        { 
            MessageBox.Show("No se pudo abrir: " + ex.Message); 
        }
    }

    private void PnlTrash_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            e.Effect = DragDropEffects.Move;
            pnlTrash.BackColor = ColorTranslator.FromHtml("#FF8DA1"); // Feedback visual oscuro
            lblTrash.ForeColor = Color.White;
        }
    }

    private void PnlTrash_DragLeave(object sender, EventArgs e)
    {
        pnlTrash.BackColor = ThemeRenderer.MainBg; // Vuelve al color normal
        lblTrash.ForeColor = ThemeRenderer.SecondaryText;
    }

    private void PnlTrash_DragDrop(object sender, DragEventArgs e)
    {
        PnlTrash_DragLeave(null, null); // Restaurar colores

        if (e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) is ListView.SelectedListViewItemCollection items)
        {
            foreach (ListViewItem item in items)
            {
                string? ruta = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(ruta))
                {
                    FileService.EnviarAPapelera(ruta);
                }
            }
            CargarDirectorio(_rutaActual, false); // Refrescar
        }
    }

    // ==========================================
    // EXPORTACIÓN Y ESTADÍSTICAS
    // ==========================================
    private async void BtnExportarCSV_Click(object sender, EventArgs e)
    {
        SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "Indice.csv" };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            btnExportarCSV.Enabled = false;
            var progress = new Progress<string>(msg => lblStatus.Text = msg);
            var cts = new CancellationTokenSource();

            await CsvIndexer.ExportarAsync(_rutaActual, sfd.FileName, progress, cts.Token);

            btnExportarCSV.Enabled = true;
            lblStatus.Text = "Exportación completada.";
            if (MessageBox.Show("Exportación exitosa. ¿Abrir archivo?", "Exportar", MessageBoxButtons.YesNo) == DialogResult.Yes)
                AbrirArchivoConAppPredeterminada(sfd.FileName);
        }
    }

    // =========================================
    // ORDENAMIENTO POR COLUMNAS
    // =========================================
    private void ListViewPrincipal_ColumnClick(object sender, ColumnClickEventArgs e)
    {
        // 1. Determinar si es ascendente o descendente
        if (e.Column == _sorter.ColumnToSort)
        {
            // Si hizo clic en la misma columna, invierte el orden
            _sorter.OrderOfSort = _sorter.OrderOfSort == SortOrder.Ascending
                ? SortOrder.Descending
                : SortOrder.Ascending;
        }
        else
        {
            // Si es una columna nueva, por defecto ascendente
            _sorter.ColumnToSort = e.Column;
            _sorter.OrderOfSort = SortOrder.Ascending;
        }

        // 2. Limpiar las flechitas visuales de todas las columnas
        foreach (ColumnHeader header in listViewPrincipal.Columns)
        {
            header.Text = header.Text.Replace(" ▲", "").Replace(" ▼", "");
        }

        // 3. Agregar la flechita a la columna actual
        string flecha = _sorter.OrderOfSort == SortOrder.Ascending ? " ▲" : " ▼";
        listViewPrincipal.Columns[e.Column].Text += flecha;

        // 4. Forzar el re-ordenamiento
        listViewPrincipal.Sort();
    }

    // =========================================
    // QUICK LOOK (VISTA PREVIA)
    // =========================================
    private void ListViewPrincipal_KeyDown(object sender, KeyEventArgs e)
    {
        // Si presionan la barra espaciadora
        if (e.KeyCode == Keys.Space)
        {
            e.SuppressKeyPress = true; // Evita que el ListView haga scroll hacia abajo

            // 1. Si el Quick Look ya está abierto, lo cerramos
            if (_quickLookForm != null && !_quickLookForm.IsDisposed)
            {
                _quickLookForm.Close();
                _quickLookForm = null;
                return;
            }

            // 2. Verificamos que haya un archivo seleccionado
            if (listViewPrincipal.SelectedItems.Count == 0) return;

            string? ruta = listViewPrincipal.SelectedItems[0].Tag?.ToString();
            if (string.IsNullOrEmpty(ruta)) return;

            // No mostramos vista previa de carpetas
            if (Directory.Exists(ruta)) return;

            // 3. Instanciamos la ventana flotante
            _quickLookForm = new QuickLookForm(ruta);

            // 4. Centramos la ventana flotante justo sobre nuestro Formulario principal
            _quickLookForm.StartPosition = FormStartPosition.Manual;
            _quickLookForm.Location = new Point(
                this.Location.X + (this.Width - _quickLookForm.Width) / 2,
                this.Location.Y + (this.Height - _quickLookForm.Height) / 2
            );

            // Show(this) indica que pertenece a Form1. Si minimizas Form1, QuickLook se minimiza con él.
            _quickLookForm.Show(this);
        }
    }

    // 
    //
    private void ConfigurarFiltrosRapidos()
    {
        // 1. Crear el contenedor de los chips
        _pnlFiltros = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = ThemeRenderer.SecondaryBg,
            Padding = new Padding(10, 5, 0, 0),
            WrapContents = false
        };

        // 2. Insertarlo en el Panel 1 del SplitContainer (Justo arriba del ListView)
        splitContainerMain.Panel1.Controls.Add(_pnlFiltros);
        _pnlFiltros.BringToFront();

        // 3. Crear los botones (Chips)
        string[] filtros = { "Todos", "Carpetas", "Imágenes", "Texto/Código", "Audio", "Video" };

        foreach (var f in filtros)
        {
            Button btnChip = new Button
            {
                Text = f,
                FlatStyle = FlatStyle.Flat,
                Height = 28,
                AutoSize = true,
                Cursor = Cursors.Hand,
                BackColor = (f == "Todos") ? ThemeRenderer.Accent : ThemeRenderer.MainBg,
                ForeColor = (f == "Todos") ? Color.White : ThemeRenderer.MainText,
                Tag = f // Guardamos el nombre del filtro aquí
            };
            btnChip.FlatAppearance.BorderSize = 0;

            // Evento click del chip
            btnChip.Click += (s, e) =>
            {
                _filtroActivo = btnChip.Tag?.ToString() ?? "Todos";
                ActualizarEstiloChips(btnChip);
                PoblarListViewDesdeMemoria(); // Filtra sin leer el disco
            };

            _pnlFiltros.Controls.Add(btnChip);
        }
    }

    //
    //
    private void ConfigurarVistaMiniaturas()
    {
        // 1. Configurar la lista de imágenes para las miniaturas
        _imageListMiniaturas = new ImageList
        {
            ImageSize = new Size(96, 96), // Tamaño de las miniaturas
            ColorDepth = ColorDepth.Depth32Bit
        };
        listViewPrincipal.LargeImageList = _imageListMiniaturas;

        // 2. Crear un botón Toggle y ponerlo en la barra superior derecha
        _btnToggleVista = new Button
        {
            Text = "🖼️ Vista",
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            ForeColor = ThemeRenderer.MainText,
            Size = new Size(90, 30),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlTop.Width - 360, 10) // Ajusta la posición según tus botones actuales
        };
        _btnToggleVista.FlatAppearance.BorderSize = 0;

        _btnToggleVista.Click += (s, e) =>
        {
            if (listViewPrincipal.View == View.Details)
            {
                listViewPrincipal.View = View.LargeIcon;
                listViewPrincipal.OwnerDraw = false; // Windows dibuja la grilla
            }
            else
            {
                listViewPrincipal.View = View.Details;
                listViewPrincipal.OwnerDraw = true; // Nuestro ThemeRenderer dibuja la lista
            }
        };

        pnlTop.Controls.Add(_btnToggleVista);
    }

    // Generador de miniaturas asíncrono
    // Generador de miniaturas asíncrono
    private async Task GenerarMiniaturasAsync()
    {
        // 1. Crear iconos por defecto
        if (!_imageListMiniaturas.Images.ContainsKey("folder"))
            _imageListMiniaturas.Images.Add("folder", GenerarIconoBase("📁"));

        if (!_imageListMiniaturas.Images.ContainsKey("file"))
            _imageListMiniaturas.Images.Add("file", GenerarIconoBase("📄"));

        // 2. Filtrar solo las imágenes
        var imagenes = _itemsActuales.Where(x => x.CategoriaVisual == "Imágenes").ToList();

        foreach (var img in imagenes)
        {
            try
            {
                // Leemos la imagen en un hilo secundario
                Bitmap miniatura = await Task.Run(() =>
                {
                    try
                    {
                        // Agregamos FileShare.ReadWrite por si otro programa está usando la imagen
                        using var fs = new FileStream(img.RutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var original = Image.FromStream(fs);
                        // Retornamos un clon redimensionado
                        return new Bitmap(original, new Size(96, 96));
                    }
                    catch
                    {
                        // Si GDI+ no soporta la imagen (ej. WebP) o está corrupta, regresamos null
                        return null;
                    }
                });

                // Si la miniatura se generó con éxito, la agregamos a la UI
                if (miniatura != null)
                {
                    _imageListMiniaturas.Images.Add(img.RutaCompleta, miniatura);

                    // Actualizamos el ListViewItem en tiempo real
                    var lvi = listViewPrincipal.Items.Cast<ListViewItem>().FirstOrDefault(x => x.Tag?.ToString() == img.RutaCompleta);
                    if (lvi != null)
                    {
                        lvi.ImageKey = img.RutaCompleta;
                    }
                }
            }
            catch { /* Ignorar si la imagen se elimina justo mientras cargaba */ }
        }
    }

    // Helper para dibujar un emoji grande como icono por defecto para carpetas/documentos
    private Bitmap GenerarIconoBase(string emoji)
    {
        Bitmap bmp = new Bitmap(96, 96);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(ThemeRenderer.MainBg);
            using Font font = new Font("Segoe UI Emoji", 40);
            g.DrawString(emoji, font, Brushes.Black, new PointF(10, 10));
        }
        return bmp;
    }

    // Actualiza los breadcrumbs (migas de pan) cada vez que cambiamos de directorio
    private void ActualizarBreadcrumbs()
    {
        _flpBreadcrumbs.Controls.Clear();
        if (string.IsNullOrEmpty(_rutaActual)) return;

        // Dividimos la ruta (Ej: C: \ Users \ Juan)
        string[] partes = _rutaActual.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        string rutaAcumulada = "";

        for (int i = 0; i < partes.Length; i++)
        {
            string parte = partes[i];
            rutaAcumulada += parte + Path.DirectorySeparatorChar;

            string rutaCapturada = rutaAcumulada; // Closure para el evento clic

            Button btnCrumb = new Button
            {
                Text = parte + (i < partes.Length - 1 ? " ➔" : ""), // Flechita estilo Windows
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = ThemeRenderer.MainText,
                Font = new Font("Segoe UI Semibold", 10),
                Margin = new Padding(0, 3, 0, 0),
                Cursor = Cursors.Hand
            };
            btnCrumb.FlatAppearance.BorderSize = 0;
            btnCrumb.FlatAppearance.MouseOverBackColor = ThemeRenderer.Hover;

            // Al hacer clic en un breadcrumb, navega a esa parte exacta
            btnCrumb.Click += (s, e) => CargarDirectorio(rutaCapturada);

            _flpBreadcrumbs.Controls.Add(btnCrumb);
        }
    }

    private void MostrarTextBoxDireccion()
    {
        _flpBreadcrumbs.Visible = false;
        txtDireccion.Visible = true;
        txtDireccion.Text = _rutaActual;
        txtDireccion.Focus();
        txtDireccion.SelectAll();
    }

    private void OcultarTextBoxDireccion()
    {
        txtDireccion.Visible = false;
        _flpBreadcrumbs.Visible = true;
    }

    private void ActualizarEstiloChips(Button chipActivo)
    {
        foreach (Control ctrl in _pnlFiltros.Controls)
        {
            if (ctrl is Button btn)
            {
                bool activo = (btn == chipActivo);
                btn.BackColor = activo ? ThemeRenderer.Accent : ThemeRenderer.MainBg;
                btn.ForeColor = activo ? Color.White : ThemeRenderer.MainText;
            }
        }
    }


    private void ActualizarEstadisticas()
    {
        int carpetas = _itemsActuales.Count(x => x.EsCarpeta);
        int archivos = _itemsActuales.Count(x => !x.EsCarpeta);
        int img = _itemsActuales.Count(x => x.CategoriaVisual == "Imágenes");
        int audio = _itemsActuales.Count(x => x.CategoriaVisual == "Audio");
        int video = _itemsActuales.Count(x => x.CategoriaVisual == "Video");
        int txt = _itemsActuales.Count(x => x.CategoriaVisual == "Texto/Código");

        lblStatus.Text = $"📁 {carpetas} carpetas  ·  📄 {archivos} archivos  ·  🖼️ {img} img  ·  🎵 {audio} audio  ·  🎬 {video} video  ·  📝 {txt} doc";
    }
}