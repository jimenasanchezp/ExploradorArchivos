using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ExploradorArchivos.Mp3;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;
using ExploradorArchivos.AppFoto;
using ExploradorArchivos.AppVideo;

namespace ExploradorArchivos;

/// <summary>
/// Interacción del usuario: doble clic, apertura de archivos,
/// drag and drop a papelera, Quick Look y búsqueda.
/// </summary>
public partial class Form1
{
    private void ListViewPrincipal_DoubleClick(object? sender, EventArgs e)
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
            { new AppFotoForm(ruta).Show(); return; }

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
            { new AppVideoForm(ruta).Show(); return; }

            if (txtExt.Contains(ext))
            { new FileViewerForm(ruta).Show(); return; }

            Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show("No se pudo abrir: " + ex.Message); }
    }

    private void PnlTrash_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            e.Effect = DragDropEffects.Move;
            pnlTrash.BackColor = ThemeRenderer.Accent;
            lblTrash.ForeColor = Color.Black;
        }
    }

    private void PnlTrash_DragLeave(object? sender, EventArgs e)
    {
        pnlTrash.BackColor = ThemeRenderer.MainBg;
        lblTrash.ForeColor = ThemeRenderer.SecondaryText;
    }

    private void PnlTrash_DragDrop(object? sender, DragEventArgs e)
    {
        PnlTrash_DragLeave(sender, e);
        if (e.Data != null && e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) is ListView.SelectedListViewItemCollection items)
        {
            foreach (ListViewItem item in items)
            {
                string? ruta = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(ruta)) FileService.EnviarAPapelera(ruta);
            }
            CargarDirectorio(_rutaActual, false);
        }
    }

    private void ListViewPrincipal_KeyDown(object? sender, KeyEventArgs e)
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

    private void TxtBuscar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            string filtro = txtBuscar.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(filtro)) 
            { 
                PoblarListViewDesdeMemoria(); 
                return; 
            }

            // Filtrado en Memoria y afectando solo al ListView
            listViewPrincipal.BeginUpdate();
            listViewPrincipal.ListViewItemSorter = null; // Ordenamiento por Fecha (desactivar auto)
            listViewPrincipal.Items.Clear();
            listViewPrincipal.Groups.Clear();

            var resultados = _itemsActuales
                .Where(x => x.Nombre.ToLower().Contains(filtro))
                .OrderByDescending(x => x.FechaModificacion)
                .ToList();

            foreach (var item in resultados)
            {
                var lvi = new ListViewItem(item.Nombre) { Tag = item.RutaCompleta };
                lvi.SubItems.Add(item.Tipo);
                lvi.SubItems.Add(item.TamanoTexto);
                lvi.SubItems.Add(item.InfoAdicional);
                lvi.SubItems.Add(item.FechaModificacion.ToString("dd/MM/yyyy HH:mm"));
                listViewPrincipal.Items.Add(lvi);
            }

            listViewPrincipal.EndUpdate();
            
            // Preservación del Estilo Y2K (forzar redibujado)
            listViewPrincipal.Invalidate();
            
            lblStatus.Text = $"Búsqueda: '{filtro}' ({resultados.Count} resultados)";
        }
    }
}