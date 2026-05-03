using System.Drawing;
using System.Windows.Forms;

namespace ExploradorArchivos;

public class ImageViewerForm : Form
{
    private PictureBox _pictureBox;
    private Panel _mainPanel;
    private float _zoomFactor = 1.0f;
    private Image _originalImage;

    public ImageViewerForm(string imagePath)
    {
        InitializeComponent();
        LoadImage(imagePath);
    }

    private void InitializeComponent()
    {
        this.Text = "Visualizador de Imágenes 🖼️";
        this.Size = new Size(800, 600);
        this.KeyPreview = true;
        this.StartPosition = FormStartPosition.CenterParent;

        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ColorTranslator.FromHtml("#4A0E0E")
        };

        _pictureBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(0, 0)
        };

        _mainPanel.Controls.Add(_pictureBox);
        this.Controls.Add(_mainPanel);

        this.KeyDown += ImageViewerForm_KeyDown;
        //Form1.ApplyTheme(this);
    }

    private void LoadImage(string path)
    {
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                _originalImage = Image.FromStream(stream);
                _pictureBox.Image = (Image)_originalImage.Clone();
                UpdateZoom();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar imagen: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            this.Close();
        }
    }

    private void ImageViewerForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.Close();
            return;
        }

        if (e.KeyCode == Keys.Add || (e.KeyCode == Keys.Oemplus && e.Shift) || e.KeyCode == Keys.Oemplus)
        {
            _zoomFactor += 0.1f;
            UpdateZoom();
        }
        else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)
        {
            _zoomFactor = Math.Max(0.1f, _zoomFactor - 0.1f);
            UpdateZoom();
        }
        else if (e.Control && e.KeyCode == Keys.D0)
        {
            _zoomFactor = 1.0f;
            UpdateZoom();
        }
    }

    private void UpdateZoom()
    {
        if (_originalImage == null) return;

        int newWidth = (int)(_originalImage.Width * _zoomFactor);
        int newHeight = (int)(_originalImage.Height * _zoomFactor);

        _pictureBox.Size = new Size(newWidth, newHeight);
        
        // Center image if smaller than panel
        if (newWidth < _mainPanel.Width)
            _pictureBox.Left = (_mainPanel.Width - newWidth) / 2;
        else
            _pictureBox.Left = 0;

        if (newHeight < _mainPanel.Height)
            _pictureBox.Top = (_mainPanel.Height - newHeight) / 2;
        else
            _pictureBox.Top = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _originalImage?.Dispose();
            _pictureBox.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
