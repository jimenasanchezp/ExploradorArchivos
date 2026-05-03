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
    private string _rutaActual = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private Stack<string> _historial = new Stack<string>();
    private List<FileSystemItem> _itemsActuales = new List<FileSystemItem>();
    private ListViewSorter _sorter;
    private QuickLookForm _quickLookForm;
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

        // OwnerDraw
        listViewPrincipal.OwnerDraw = true;
        listViewPrincipal.DrawColumnHeader += ThemeRenderer.DrawListViewColumnHeader;
        listViewPrincipal.DrawItem += ThemeRenderer.DrawListViewItem;
        listViewPrincipal.DrawSubItem += ThemeRenderer.DrawListViewSubItem;

        treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        treeViewLateral.DrawNode += ThemeRenderer.DrawTreeNode;
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
    }

    // ==========================================
    // CARGA ASÍNCRONA DE DIRECTORIO
    // ==========================================
    private async void CargarDirectorio(string ruta, bool guardarHistorial = true)
    {
        if (!Directory.Exists(ruta)) return;

        if (guardarHistorial && !string.IsNullOrEmpty(_rutaActual) && _rutaActual != ruta)
            _historial.Push(_rutaActual);

        _rutaActual = ruta;
        txtDireccion.Text = _rutaActual;
        lblStatus.Text = "Cargando directorio...";
        listViewPrincipal.Items.Clear();

        // 1. Obtener archivos asíncronamente
        _itemsActuales = await FileService.ObtenerContenidoAsync(_rutaActual);

        // 2. Poblar ListView
        listViewPrincipal.BeginUpdate();
        foreach (var item in _itemsActuales)
        {
            var lvi = new ListViewItem(item.Nombre) { Tag = item.RutaCompleta };
            lvi.SubItems.Add(item.Tipo);
            lvi.SubItems.Add(item.TamanoTexto);
            lvi.SubItems.Add(item.InfoAdicional);
            listViewPrincipal.Items.Add(lvi);
        }
        listViewPrincipal.EndUpdate();

        ActualizarEstadisticas();
        PoblarTreeViewNormal();

        // Asegúrate de poner esto en ConfigurarUI():
        // listViewPrincipal.ShowGroups = true;

        // Dentro de CargarDirectorio(), al momento de poblar el ListView:
        listViewPrincipal.BeginUpdate();
        listViewPrincipal.Groups.Clear(); // Limpiamos grupos anteriores

        foreach (var item in _itemsActuales)
        {
            var lvi = new ListViewItem(item.Nombre) { Tag = item.RutaCompleta };
            lvi.SubItems.Add(item.Tipo);
            lvi.SubItems.Add(item.TamanoTexto);
            lvi.SubItems.Add(item.InfoAdicional);

            // LÓGICA DE GRUPOS
            string nombreGrupo = item.CategoriaVisual;
            var grupo = listViewPrincipal.Groups[nombreGrupo];
            if (grupo == null)
            {
                grupo = new ListViewGroup(nombreGrupo, nombreGrupo);
                grupo.Name = nombreGrupo;
                listViewPrincipal.Groups.Add(grupo);
            }
            lvi.Group = grupo; // Asignamos el item a su grupo

            listViewPrincipal.Items.Add(lvi);
        }
        listViewPrincipal.EndUpdate();
    }

    // ==========================================
    // TREEVIEW (PANEL LATERAL)
    // ==========================================
    private void PoblarTreeViewNormal()
    {
        treeViewLateral.Nodes.Clear();
        var grupos = _itemsActuales.GroupBy(x => x.CategoriaVisual);

        foreach (var grupo in grupos.OrderBy(g => g.Key))
        {
            TreeNode nodoPadre = new TreeNode($"{grupo.Key} ({grupo.Count()})");
            foreach (var item in grupo)
            {
                nodoPadre.Nodes.Add(new TreeNode(item.Nombre) { Tag = item.RutaCompleta });
            }
            treeViewLateral.Nodes.Add(nodoPadre);
        }
        treeViewLateral.ExpandAll();
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
        string ruta = listViewPrincipal.SelectedItems[0].Tag.ToString();

        if (Directory.Exists(ruta)) CargarDirectorio(ruta);
        else AbrirArchivoConAppPredeterminada(ruta); // Para abrir el archivo
    }

    private void AbrirArchivoConAppPredeterminada(string ruta)
    {
        try { Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("No se pudo abrir: " + ex.Message); }
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
                FileService.EnviarAPapelera(item.Tag.ToString());
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

            string ruta = listViewPrincipal.SelectedItems[0].Tag.ToString();

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