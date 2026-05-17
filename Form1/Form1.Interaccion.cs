using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using ExploradorArchivos.Mp3;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;
using ExploradorArchivos.AppFoto;
using ExploradorArchivos.AppVideo;
using ExploradorArchivos.AppDataFusion;

namespace ExploradorArchivos;

/// <summary>
/// Interacción del usuario: doble clic, apertura de archivos,
/// drag and drop a papelera, Quick Look y búsqueda.
/// </summary>
public partial class Form1
{
    // Doble clic en el ListView para abrir archivos o navegar a carpetas
    private void ListViewPrincipal_DoubleClick(object? sender, EventArgs e)
    {
        if (listViewPrincipal.SelectedItems.Count == 0) return;
        string? ruta = listViewPrincipal.SelectedItems[0].Tag?.ToString();
        if (string.IsNullOrEmpty(ruta)) return;
        if (ruta == "Inicio" || ruta == "Favoritos" || ruta == "EsteEquipo" || Directory.Exists(ruta)) CargarDirectorio(ruta);
        else AbrirArchivoConAppPredeterminada(ruta);
    }

    // Método para abrir archivos con la aplicación predeterminada del sistema
    private void AbrirArchivoConAppPredeterminada(string ruta)
    {
        try
        {
            // Registrar el archivo en nuestro historial persistente de archivos abiertos
            RecentFilesService.RegistrarArchivoAbierto(ruta);

            string ext = Path.GetExtension(ruta).ToLower();
            string[] imgExt = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            string[] mediaExt = { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma", ".aac" };
            string[] videoExt = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v" };
            string[] txtExt = { ".cs", ".html", ".css", ".js", ".md", ".py" };
            string[] dataExt = { ".csv", ".json", ".xml", ".txt" };

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

            if (dataExt.Contains(ext))
            { 
                var frm = new ExploradorArchivos.AppDataFusion.MainForm(ruta);
                frm.Show();
                return; 
            }

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

    // Mapea la tecla espacio para activar el Quick Look (vista previa rápida) de archivos, y atajos Ctrl+C, Ctrl+X, Ctrl+V
    private void ListViewPrincipal_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control)
        {
            if (e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                CopiarSeleccionados();
            }
            else if (e.KeyCode == Keys.X)
            {
                e.SuppressKeyPress = true;
                CortarSeleccionados();
            }
            else if (e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PegarArchivos();
            }
        }
        else if (e.KeyCode == Keys.Space)
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

    // Mapea la tecla Enter para activar la búsqueda en el ListView
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

    // Evento Click para crear una nueva carpeta
    private void BtnNuevaCarpeta_Click(object? sender, EventArgs e)
    {
        if (_rutaActual == "Inicio" || _rutaActual == "EsteEquipo" || !Directory.Exists(_rutaActual))
        {
            MessageBox.Show("No se pueden crear carpetas en esta ubicación especial.", "Operación no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? nombre = InputDialog.Show("Nueva Carpeta", "Ingresa el nombre de la nueva carpeta:", "Nueva Carpeta");
        if (nombre == null) return; // Cancelado

        nombre = nombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show("El nombre de la carpeta no puede estar vacío.", "Nombre inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Validar caracteres prohibidos
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (nombre.Any(c => invalidChars.Contains(c)))
        {
            MessageBox.Show("El nombre contiene caracteres no válidos.", "Nombre inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string nuevaRuta = Path.Combine(_rutaActual, nombre);
        if (Directory.Exists(nuevaRuta) || File.Exists(nuevaRuta))
        {
            MessageBox.Show("Ya existe un archivo o carpeta con el mismo nombre.", "Conflicto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(nuevaRuta);
            CargarDirectorio(_rutaActual, false);
            lblStatus.Text = $"Carpeta creada: {nombre}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo crear la carpeta: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // === Drag & Drop en el ListView ===
    private void ListViewPrincipal_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            e.Effect = DragDropEffects.Move;
        }
    }

    private void ListViewPrincipal_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            Point pt = listViewPrincipal.PointToClient(new Point(e.X, e.Y));
            ListViewItem hoverItem = listViewPrincipal.GetItemAt(pt.X, pt.Y);

            if (hoverItem != null && hoverItem.Tag != null)
            {
                string destDir = hoverItem.Tag.ToString()!;
                // Solo si es una carpeta y no está seleccionada (para no mover a sí misma)
                if (Directory.Exists(destDir) && !hoverItem.Selected)
                {
                    e.Effect = DragDropEffects.Move;
                    int idx = hoverItem.Index;
                    if (idx != ThemeRenderer.GetHoverIndex())
                    {
                        ThemeRenderer.SetHoverIndex(idx);
                        listViewPrincipal.Invalidate();
                    }
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
            if (ThemeRenderer.GetHoverIndex() != -1)
            {
                ThemeRenderer.SetHoverIndex(-1);
                listViewPrincipal.Invalidate();
            }
        }
    }

    private void ListViewPrincipal_DragLeave(object? sender, EventArgs e)
    {
        if (ThemeRenderer.GetHoverIndex() != -1)
        {
            ThemeRenderer.SetHoverIndex(-1);
            listViewPrincipal.Invalidate();
        }
    }

    private void ListViewPrincipal_DragDrop(object? sender, DragEventArgs e)
    {
        if (ThemeRenderer.GetHoverIndex() != -1)
        {
            ThemeRenderer.SetHoverIndex(-1);
            listViewPrincipal.Invalidate();
        }

        if (e.Data != null && e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) is ListView.SelectedListViewItemCollection items)
        {
            Point pt = listViewPrincipal.PointToClient(new Point(e.X, e.Y));
            ListViewItem hoverItem = listViewPrincipal.GetItemAt(pt.X, pt.Y);

            if (hoverItem != null && hoverItem.Tag != null)
            {
                string destDir = hoverItem.Tag.ToString()!;
                if (Directory.Exists(destDir) && !hoverItem.Selected)
                {
                    MoverItems(items, destDir);
                }
            }
        }
    }

    // === Drag & Drop en el TreeView ===
    private void TreeViewLateral_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            e.Effect = DragDropEffects.Move;
        }
    }

    private void TreeViewLateral_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(typeof(ListView.SelectedListViewItemCollection)))
        {
            Point pt = treeViewLateral.PointToClient(new Point(e.X, e.Y));
            TreeNode hoverNode = treeViewLateral.GetNodeAt(pt.X, pt.Y);

            if (hoverNode != null && hoverNode.Tag != null)
            {
                string destDir = hoverNode.Tag.ToString()!;
                if (Directory.Exists(destDir))
                {
                    e.Effect = DragDropEffects.Move;
                    treeViewLateral.SelectedNode = hoverNode; // Feedback visual al resaltar el nodo
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }
    }

    private void TreeViewLateral_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) is ListView.SelectedListViewItemCollection items)
        {
            Point pt = treeViewLateral.PointToClient(new Point(e.X, e.Y));
            TreeNode hoverNode = treeViewLateral.GetNodeAt(pt.X, pt.Y);

            if (hoverNode != null && hoverNode.Tag != null)
            {
                string destDir = hoverNode.Tag.ToString()!;
                if (Directory.Exists(destDir))
                {
                    MoverItems(items, destDir);
                }
            }
        }
    }

    // Helper robusto para mover archivos y carpetas
    private void MoverItems(ListView.SelectedListViewItemCollection items, string destDir)
    {
        bool algunaModificacion = false;
        try
        {
            foreach (ListViewItem item in items)
            {
                string? srcPath = item.Tag?.ToString();
                if (string.IsNullOrEmpty(srcPath)) continue;

                string name = Path.GetFileName(srcPath);
                string destPath = Path.Combine(destDir, name);

                // Evitar mover a sí mismo
                if (Path.GetDirectoryName(srcPath) == destDir) continue;

                // Evitar mover una carpeta dentro de sí misma
                if (srcPath == destDir || destDir.StartsWith(srcPath + Path.DirectorySeparatorChar))
                {
                    MessageBox.Show($"No se puede mover la carpeta '{name}' dentro de sí misma.", "Operación no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                bool esCarpeta = Directory.Exists(srcPath);

                if (esCarpeta)
                {
                    if (Directory.Exists(destPath))
                    {
                        var res = MessageBox.Show($"La carpeta '{name}' ya existe en el destino. ¿Deseas reemplazarla?", "Conflicto de Carpeta", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (res == DialogResult.Cancel) break;
                        if (res == DialogResult.No) continue;

                        Directory.Delete(destPath, true);
                    }
                    else if (File.Exists(destPath))
                    {
                        var res = MessageBox.Show($"Ya existe un archivo con el nombre '{name}' en el destino. ¿Deseas reemplazarlo?", "Conflicto", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (res == DialogResult.Cancel) break;
                        if (res == DialogResult.No) continue;

                        File.Delete(destPath);
                    }
                    Directory.Move(srcPath, destPath);
                    algunaModificacion = true;
                }
                else
                {
                    if (File.Exists(destPath))
                    {
                        var res = MessageBox.Show($"El archivo '{name}' ya existe en el destino. ¿Deseas reemplazarlo?", "Conflicto de Archivo", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (res == DialogResult.Cancel) break;
                        if (res == DialogResult.No) continue;

                        File.Delete(destPath);
                    }
                    else if (Directory.Exists(destPath))
                    {
                        var res = MessageBox.Show($"Ya existe una carpeta con el nombre '{name}' en el destino. ¿Deseas reemplazarla?", "Conflicto", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (res == DialogResult.Cancel) break;
                        if (res == DialogResult.No) continue;

                        Directory.Delete(destPath, true);
                    }
                    File.Move(srcPath, destPath);
                    algunaModificacion = true;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al mover elementos: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (algunaModificacion)
            {
                CargarDirectorio(_rutaActual, false);
            }
        }
    }
}