using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppFoto;

public partial class AppFotoForm : Form
{
    private readonly string _rutaFoto;
    private AppFotoMetadata _metadata = default!;
    private Image _imagenOriginal = default!;
    private Image _imagenActual = default!;

    // UI Controls
    private Panel pnlTop = default!;
    private SplitContainer splitMain = default!;
    private PictureBox picPhoto = default!;
    private Panel pnlSidebar = default!;
    private WebView2 webMap = default!;
    private Label lblMetaInfo = default!;

    public AppFotoForm(string ruta)
    {
        _rutaFoto = ruta;
        InitializeCustomComponents();
        CargarFoto();
    }

    private void InitializeCustomComponents()
    {
        this.Text = "App Foto - Kawaii Studio";
        this.Size = new Size(1100, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = ThemeRenderer.MainBg;
        this.Icon = SystemIcons.Shield; // Placeholder icon

        // Barra Superior
        pnlTop = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = ThemeRenderer.SecondaryBg };
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, pnlTop.ClientRectangle, true);
        
        Label lblTitle = new Label { 
            Text = "📸 App Foto Studio", 
            Location = new Point(20, 15), 
            AutoSize = true, 
            Font = new Font("MS Sans Serif", 12, FontStyle.Bold),
            ForeColor = ThemeRenderer.MainText 
        };
        pnlTop.Controls.Add(lblTitle);

        // Botones de edición
        int startX = 250;
        AgregarBotonEditor("↩️ Girar", startX, () => Rotar(RotateFlipType.Rotate270FlipNone));
        AgregarBotonEditor("↪️ Girar", startX + 90, () => Rotar(RotateFlipType.Rotate90FlipNone));
        AgregarBotonEditor("✨ Kawaii", startX + 180, () => AplicarFiltro("Kawaii"));
        AgregarBotonEditor("🎞️ Sepia", startX + 280, () => AplicarFiltro("Sepia"));
        AgregarBotonEditor("🌑 B&N", startX + 380, () => AplicarFiltro("BN"));
        AgregarBotonEditor("💾 Guardar", startX + 480, GuardarImagen);

        // Split Container
        splitMain = new SplitContainer { 
            Dock = DockStyle.Fill, 
            SplitterDistance = 800,
            BorderStyle = BorderStyle.Fixed3D 
        };

        // Visor de fotos
        Panel pnlVisor = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(240, 240, 240) };
        picPhoto = new PictureBox { 
            SizeMode = PictureBoxSizeMode.Zoom, 
            Dock = DockStyle.Fill, 
            BackColor = Color.Transparent 
        };
        pnlVisor.Controls.Add(picPhoto);
        splitMain.Panel1.Controls.Add(pnlVisor);

        // Sidebar
        pnlSidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        
        Label lblSidebarTitle = new Label { 
            Text = "📍 Geolocalización", 
            Dock = DockStyle.Top, 
            Font = new Font("MS Sans Serif", 10, FontStyle.Bold),
            Height = 30
        };
        
        lblMetaInfo = new Label { 
            Text = "Cargando metadatos...", 
            Dock = DockStyle.Top, 
            Height = 150,
            Font = new Font("Consolas", 8),
            ForeColor = ThemeRenderer.SecondaryText
        };

        webMap = new WebView2 { 
            Dock = DockStyle.Fill, 
            MinimumSize = new Size(200, 200) 
        };

        pnlSidebar.Controls.Add(webMap);
        pnlSidebar.Controls.Add(lblMetaInfo);
        pnlSidebar.Controls.Add(lblSidebarTitle);
        splitMain.Panel2.Controls.Add(pnlSidebar);

        this.Controls.Add(splitMain);
        this.Controls.Add(pnlTop);
    }

    private void AgregarBotonEditor(string texto, int x, Action accion)
    {
        Button btn = new Button {
            Text = texto,
            Location = new Point(x, 15),
            Size = new Size(85, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeRenderer.MainBg,
            Cursor = Cursors.Hand,
            Font = new Font("MS Sans Serif", 8, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => accion();
        btn.Paint += (s, e) => ThemeRenderer.DrawRetroBorder(e.Graphics, btn.ClientRectangle, true);
        pnlTop.Controls.Add(btn);
    }

    private async void CargarFoto()
    {
        try
        {
            if (!File.Exists(_rutaFoto)) return;

            _imagenOriginal = Image.FromFile(_rutaFoto);
            _imagenActual = (Image)_imagenOriginal.Clone();
            picPhoto.Image = _imagenActual;

            _metadata = AppFotoExifService.LeerMetadatos(_rutaFoto);
            
            // Actualizar Info Sidebar
            lblMetaInfo.Text = $"Archivo: {_metadata.Nombre}\n" +
                               $"Dimensiones: {_metadata.Dimensiones}\n" +
                               $"Fecha: {(_metadata.FechaCaptura?.ToString() ?? "N/A")}\n" +
                               $"Cámara: {_metadata.ModeloCamara}\n" +
                               $"Latitud: {(_metadata.Latitud?.ToString("F5") ?? "N/A")}\n" +
                               $"Longitud: {(_metadata.Longitud?.ToString("F5") ?? "N/A")}";

            // Inicializar Mapa
            await webMap.EnsureCoreWebView2Async();
            if (_metadata.TieneUbicacion)
            {
                string html = AppFotoMapService.GenerarMapaHtml(_metadata.Latitud!.Value, _metadata.Longitud!.Value);
                webMap.NavigateToString(html);
            }
            else
            {
                webMap.NavigateToString("<html><body style='background:#f9f9f9; display:flex; justify-content:center; align-items:center; height:100vh; font-family:sans-serif;'><h3>No hay datos de GPS 📍</h3></body></html>");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando la foto: {ex.Message}");
        }
    }

    private void AplicarFiltro(string nombre)
    {
        var filtrada = AppFotoProcessor.AplicarFiltro(_imagenOriginal, nombre);
        _imagenActual?.Dispose();
        _imagenActual = filtrada;
        picPhoto.Image = _imagenActual;
    }

    private void Rotar(RotateFlipType tipo)
    {
        AppFotoProcessor.Rotar(_imagenActual, tipo);
        picPhoto.Invalidate();
    }

    private void GuardarImagen()
    {
        using SaveFileDialog sfd = new SaveFileDialog { Filter = "JPEG|*.jpg|PNG|*.png", FileName = "Editada_" + _metadata.Nombre };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            _imagenActual.Save(sfd.FileName);
            MessageBox.Show("Imagen guardada con éxito ✨");
        }
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _imagenOriginal?.Dispose();
        _imagenActual?.Dispose();
        base.OnFormClosing(e);
    }
}
