using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExploradorArchivos.Models;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;

namespace ExploradorArchivos;

/// <summary>
/// Visualización: ListView, filtros, miniaturas, ordenamiento,
/// estadísticas y exportación CSV.
/// </summary>
public partial class Form1
{
    // En Form1.Visualizacion.cs
    private void PoblarListViewDesdeMemoria()
    {
        listViewPrincipal.BeginUpdate();

        // CRÍTICO: Desactivar el ordenador automático para que respete nuestro orden manual
        listViewPrincipal.ListViewItemSorter = null;

        listViewPrincipal.Items.Clear();
        listViewPrincipal.Groups.Clear();

        // Ordenar: Los más recientes arriba (OrderByDescending)
        var itemsOrdenados = _itemsActuales
            .Where(x => _filtroActivo == "Todos" || x.CategoriaVisual == _filtroActivo)
            .OrderByDescending(x => x.FechaModificacion)
            .ToList();

        foreach (var item in itemsOrdenados)
        {
            var lvi = new ListViewItem(item.Nombre) { Tag = item.RutaCompleta };
            lvi.SubItems.Add(item.Tipo);
            lvi.SubItems.Add(item.TamanoTexto);
            lvi.SubItems.Add(item.InfoAdicional);
            lvi.SubItems.Add(item.FechaModificacion.ToString("dd/MM/yyyy HH:mm"));

            // ... resto del código de grupos ...
            listViewPrincipal.Items.Add(lvi);
        }

        listViewPrincipal.EndUpdate();
    }

    // === FILTROS RÁPIDOS (CHIPS) ===

    private void ConfigurarFiltrosRapidos()
    {
        _pnlFiltros = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = ThemeRenderer.MainBg,
            Padding = new Padding(10, 10, 0, 0),
            WrapContents = false,
            AutoScroll = true
        };

        _pnlFiltros.Paint += (s, e) => {
            ThemeRenderer.DrawClassicBorder(e.Graphics, _pnlFiltros.ClientRectangle, true);
        };

        splitContainerMain.Panel2.Controls.Add(_pnlFiltros);
        _pnlFiltros.BringToFront();

        string[] filtros = { "Todos", "Carpetas", "Imágenes", "Documentos", "Audio", "Video" };

        foreach (var f in filtros)
        {
            Button btnTab = new Button
            {
                Text = f,
                FlatStyle = FlatStyle.Flat,
                Height = 36, // Más altas para legibilidad
                Width = 135, // Más anchas
                Cursor = Cursors.Hand,
                BackColor = (f == "Todos") ? ThemeRenderer.MainBg : ThemeRenderer.SecondaryBg,
                ForeColor = ThemeRenderer.MainText,
                Tag = f,
                Margin = new Padding(8, 0, 8, 0), // Mayor separación para que respiren
                Font = new Font("MS Sans Serif", 9, FontStyle.Bold)
            };
            btnTab.FlatAppearance.BorderSize = 0;

            btnTab.Paint += (s, e) =>
            {
                bool activo = (_filtroActivo == btnTab.Tag?.ToString());
                Rectangle rect = btnTab.ClientRectangle;
                if (!activo) { rect.Y += 2; rect.Height -= 2; }

                ThemeRenderer.DrawClassicBorder(e.Graphics, rect, true);
                
                TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                TextRenderer.DrawText(e.Graphics, btnTab.Text, btnTab.Font, rect, btnTab.ForeColor, flags);
            };

            btnTab.Click += (s, e) =>
            {
                _filtroActivo = btnTab.Tag?.ToString() ?? "Todos";
                // Actualizar colores
                foreach (Control c in _pnlFiltros.Controls)
                {
                    if (c is Button b)
                    {
                        b.BackColor = (b.Tag?.ToString() == _filtroActivo) ? ThemeRenderer.MainBg : ThemeRenderer.SecondaryBg;
                        b.Invalidate();
                    }
                }
                PoblarListViewDesdeMemoria();
            };

            _pnlFiltros.Controls.Add(btnTab);
        }
    }

    // === VISTA DE MINIATURAS ===

    private void ConfigurarVistaMiniaturas()
    {
        _imageListMiniaturas = new ImageList
        {
            ImageSize = new Size(96, 96),
            ColorDepth = ColorDepth.Depth32Bit
        };
        listViewPrincipal.LargeImageList = _imageListMiniaturas;

        _btnToggleVista = new Button
        {
            Text = "🖼️ Vista",
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            ForeColor = ThemeRenderer.MainText,
            Size = new Size(90, 30),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlTop.Width - 360, 10)
        };
        _btnToggleVista.FlatAppearance.BorderSize = 0;

        _btnToggleVista.Click += (s, e) =>
        {
            if (listViewPrincipal.View == View.Details)
            {
                listViewPrincipal.View = View.LargeIcon;
                listViewPrincipal.TileSize = new Size(120, 120); // Ajustar tamaño de tarjeta
            }
            else
            {
                listViewPrincipal.View = View.Details;
            }
            listViewPrincipal.OwnerDraw = true; // Siempre activo para diseño clásico
            listViewPrincipal.Invalidate();
        };

        pnlTop.Controls.Add(_btnToggleVista);
    }

    private async Task GenerarMiniaturasAsync()
    {
        if (!_imageListMiniaturas.Images.ContainsKey("folder"))
            _imageListMiniaturas.Images.Add("folder", GenerarIconoBase("📁"));

        if (!_imageListMiniaturas.Images.ContainsKey("file"))
            _imageListMiniaturas.Images.Add("file", GenerarIconoBase("📄"));

        var imagenes = _itemsActuales.Where(x => x.CategoriaVisual == "Imágenes").ToList();

        foreach (var img in imagenes)
        {
            try
            {
                Bitmap? miniatura = await Task.Run(() =>
                {
                    try
                    {
                        if (!File.Exists(img.RutaCompleta)) return null;

                        // Leer todos los bytes en memoria para evitar bloquear el archivo físico
                        byte[] bytes = File.ReadAllBytes(img.RutaCompleta);
                        using var ms = new MemoryStream(bytes);
                        using var original = Image.FromStream(ms);
                        return new Bitmap(original, new Size(96, 96));
                    }
                    catch { return null; }
                });

                if (miniatura != null)
                {
                    _imageListMiniaturas.Images.Add(img.RutaCompleta, miniatura);
                    var lvi = listViewPrincipal.Items.Cast<ListViewItem>()
                        .FirstOrDefault(x => x.Tag?.ToString() == img.RutaCompleta);
                    if (lvi != null) lvi.ImageKey = img.RutaCompleta;
                }
            }
            catch { /* Ignorar si la imagen se elimina mientras cargaba */ }
        }
    }

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

    // === ORDENAMIENTO POR COLUMNAS ===

    private void ListViewPrincipal_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (e.Column == _sorter.ColumnToSort)
        {
            _sorter.OrderOfSort = _sorter.OrderOfSort == SortOrder.Ascending
                ? SortOrder.Descending : SortOrder.Ascending;
        }
        else
        {
            _sorter.ColumnToSort = e.Column;
            _sorter.OrderOfSort = SortOrder.Ascending;
        }

        foreach (ColumnHeader header in listViewPrincipal.Columns)
            header.Text = header.Text.Replace(" ▲", "").Replace(" ▼", "");

        string flecha = _sorter.OrderOfSort == SortOrder.Ascending ? " ▲" : " ▼";
        listViewPrincipal.Columns[e.Column].Text += flecha;
        listViewPrincipal.Sort();
    }

    // === EXPORTACIÓN CSV ===

    private async void BtnExportarCSV_Click(object? sender, EventArgs e)
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

    // === ESTADÍSTICAS ===

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
