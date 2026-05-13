using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ExploradorArchivos.Mp3;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;
using ExploradorArchivos.Video;

namespace ExploradorArchivos;

/// <summary>
/// Interacción del usuario: doble clic, apertura de archivos,
/// drag and drop a papelera, Quick Look y búsqueda.
/// </summary>
public partial class Form1
{
    private void ListViewPrincipal_DoubleClick(object sender, EventArgs e)
    {
        if (listViewPrincipal.SelectedItems.Count == 0) return;
        string? ruta = listViewPrincipal.SelectedItems[0].Tag?.ToString();
        if (string.IsNullOrEmpty(ruta)) return;
        if (Directory.Exists(ruta)) CargarDirectorio(ruta);
        else AbrirArchivoConAppPredeterminada(ruta);
    }

    private void AbrirArchivoConAppPredeterminada(string ruta)
    {
        try
        {
            string ext = Path.GetExtension(ruta).ToLower();
            string[] imgExt = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            string[] mediaExt = { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma", ".aac" };
            string[] videoExt = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v" };
            string[] txtExt = { ".txt", ".json", ".xml", ".csv", ".cs", ".html", ".css", ".js", ".md", ".py" };

            if (imgExt.Contains(ext))
            { new ImageViewerForm(ruta).Show(); return; }

            if (mediaExt.Contains(ext))
            {
                var audioFiles = _itemsActuales
                    .Where(x => !x.EsCarpeta && mediaExt.Contains(Path.GetExtension(x.RutaCompleta).ToLower()))
                    .Select(x => x.RutaCompleta).ToList();
                if (audioFiles.Count == 0) audioFiles.Add(ruta);
                new MusicPlayerForm(audioFiles, ruta).Show();
                return;
            }

            if (videoExt.Contains(ext))
            { new VideoPlayerForm(ruta).Show(); return; }

            if (txtExt.Contains(ext))
            { new FileViewerForm(ruta).Show(); return; }

            Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show("No se pudo abrir: " + ex.Message); }
    }

    private void PnlTrash_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            e.Effect = DragDropEffects.Move;
            pnlTrash.BackColor = ColorTranslator.FromHtml("#FF8DA1");
            lblTrash.ForeColor = Color.White;
        }
    }

    private void PnlTrash_DragLeave(object sender, EventArgs e)
    {
        pnlTrash.BackColor = ThemeRenderer.MainBg;
        lblTrash.ForeColor = ThemeRenderer.SecondaryText;
    }

    private void PnlTrash_DragDrop(object sender, DragEventArgs e)
    {
        PnlTrash_DragLeave(null, null);
        if (e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) is ListView.SelectedListViewItemCollection items)
        {
            foreach (ListViewItem item in items)
            {
                string? ruta = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(ruta)) FileService.EnviarAPapelera(ruta);
            }
            CargarDirectorio(_rutaActual, false);
        }
    }

    private void ListViewPrincipal_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            e.SuppressKeyPress = true;
            if (_quickLookForm != null && !_quickLookForm.IsDisposed)
            { _quickLookForm.Close(); _quickLookForm = null; return; }

            if (listViewPrincipal.SelectedItems.Count == 0) return;
            string? ruta = listViewPrincipal.SelectedItems[0].Tag?.ToString();
            if (string.IsNullOrEmpty(ruta) || Directory.Exists(ruta)) return;

            _quickLookForm = new QuickLookForm(ruta);
            _quickLookForm.StartPosition = FormStartPosition.Manual;
            _quickLookForm.Location = new Point(
                this.Location.X + (this.Width - _quickLookForm.Width) / 2,
                this.Location.Y + (this.Height - _quickLookForm.Height) / 2);
            _quickLookForm.Show(this);
        }
    }

    private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            string filtro = txtBuscar.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filtro)) { PoblarTreeViewNormal(); return; }

            treeViewLateral.Nodes.Clear();
            var resultados = _itemsActuales.Where(x => x.Nombre.ToLower().Contains(filtro)).ToList();
            TreeNode nodoSearch = new TreeNode($"Búsqueda: '{filtro}' ({resultados.Count})");
            foreach (var r in resultados)
                nodoSearch.Nodes.Add(new TreeNode(r.Nombre) { Tag = r.RutaCompleta });
            treeViewLateral.Nodes.Add(nodoSearch);
            nodoSearch.Expand();
        }
    }
}
