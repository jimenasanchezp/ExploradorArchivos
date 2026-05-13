using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ExploradorArchivos.Models;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;

namespace ExploradorArchivos;

/// <summary>
/// Lógica de navegación: carga de directorios, vistas especiales,
/// TreeView lateral y breadcrumbs.
/// </summary>
public partial class Form1
{
    private async void CargarDirectorio(string ruta, bool guardarHistorial = true)
    {
        if (guardarHistorial && !string.IsNullOrEmpty(_rutaActual) && _rutaActual != ruta)
            _historial.Push(_rutaActual);

        _rutaActual = ruta;

        // Vista de Inicio (Dashboard)
        if (ruta == "Inicio")
        {
            txtDireccion.Text = "🏠 Inicio";
            ActualizarBreadcrumbs();
            OcultarTextBoxDireccion();
            GenerarVistaInicio();
            PoblarTreeViewNormal();
            return;
        }

        // Vista de Este Equipo (Discos duros)
        if (ruta == "EsteEquipo")
        {
            txtDireccion.Text = "💻 Este Equipo";
            ActualizarBreadcrumbs();
            OcultarTextBoxDireccion();
            GenerarVistaEsteEquipo();
            PoblarTreeViewNormal();
            return;
        }

        // Navegación normal (Carpetas reales)
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

        // Grupo: Carpetas Principales
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
                lvi.SubItems.Add("");
                listViewPrincipal.Items.Add(lvi);
            }
        }

        // Grupo: Archivos Recientes
        ListViewGroup grpRecientes = new ListViewGroup("Archivos Recientes", "🕒 Archivos Recientes");
        listViewPrincipal.Groups.Add(grpRecientes);

        try
        {
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
        _itemsActuales.Clear();
    }

    private void GenerarVistaEsteEquipo()
    {
        listViewPrincipal.BeginUpdate();
        listViewPrincipal.Items.Clear();
        listViewPrincipal.Groups.Clear();

        ListViewGroup grpDiscos = new ListViewGroup("Unidades de Disco", "💻 Unidades de Disco");
        listViewPrincipal.Groups.Add(grpDiscos);

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                var lvi = new ListViewItem($"{drive.Name} ({drive.VolumeLabel})") { Tag = drive.Name, Group = grpDiscos };
                lvi.SubItems.Add("Unidad Local");
                lvi.SubItems.Add((drive.TotalSize / 1073741824).ToString() + " GB");
                lvi.SubItems.Add($"Libre: {(drive.AvailableFreeSpace / 1073741824)} GB");
                lvi.SubItems.Add("");
                listViewPrincipal.Items.Add(lvi);
            }
        }

        listViewPrincipal.EndUpdate();
        lblStatus.Text = "💻 Vista de Este Equipo cargada.";
        _itemsActuales.Clear();
    }

    // === TREEVIEW (PANEL LATERAL) ===

    private void PoblarTreeViewNormal()
    {
        treeViewLateral.BeginUpdate();
        treeViewLateral.Nodes.Clear();

        // Accesos Rápidos
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

        // Este Equipo
        TreeNode nodoEquipo = new TreeNode("💻 Este Equipo") { Tag = "EsteEquipo" };
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
                nodoEquipo.Nodes.Add(new TreeNode($"💽 {drive.Name} ({drive.VolumeLabel})") { Tag = drive.Name });
        }
        treeViewLateral.Nodes.Add(nodoEquipo);
        nodoEquipo.Expand();

        // Carpeta actual (solo si estamos navegando en una ruta real)
        if (_rutaActual != "Inicio" && _rutaActual != "EsteEquipo" && Directory.Exists(_rutaActual))
        {
            TreeNode nodoActual = new TreeNode($"📂 Abierto: {new DirectoryInfo(_rutaActual).Name}");
            var grupos = _itemsActuales.GroupBy(x => x.CategoriaVisual);

            foreach (var grupo in grupos.OrderBy(g => g.Key))
            {
                TreeNode nodoPadre = new TreeNode($"{grupo.Key} ({grupo.Count()})");
                foreach (var item in grupo)
                {
                    if (item.EsCarpeta)
                        nodoPadre.Nodes.Add(new TreeNode("📁 " + item.Nombre) { Tag = item.RutaCompleta });
                }
                if (nodoPadre.Nodes.Count > 0)
                    nodoActual.Nodes.Add(nodoPadre);
            }
            treeViewLateral.Nodes.Add(nodoActual);
            nodoActual.ExpandAll();
        }

        treeViewLateral.EndUpdate();
    }

    private void TreeViewLateral_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node.Tag != null)
        {
            string? ruta = e.Node.Tag.ToString();
            if (string.IsNullOrEmpty(ruta)) return;

            if (Directory.Exists(ruta))
                CargarDirectorio(ruta);
            else
                AbrirArchivoConAppPredeterminada(ruta);
        }
    }

    // === BREADCRUMBS ===

    private void ActualizarBreadcrumbs()
    {
        _flpBreadcrumbs.Controls.Clear();
        if (string.IsNullOrEmpty(_rutaActual)) return;

        string[] partes = _rutaActual.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        string rutaAcumulada = "";

        for (int i = 0; i < partes.Length; i++)
        {
            string parte = partes[i];
            rutaAcumulada += parte + Path.DirectorySeparatorChar;

            string rutaCapturada = rutaAcumulada;

            Button btnCrumb = new Button
            {
                Text = parte + (i < partes.Length - 1 ? " ➔" : ""),
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
}
