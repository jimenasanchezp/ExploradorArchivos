using System;
using System.Drawing;
using System.Windows.Forms;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.Mp3
{
    public class MetadataEditorForm : Form
    {
        private readonly Cancion _cancion;
        private readonly GestorReproduccion _gestor;
        private Image? _nuevaPortada;

        private PictureBox _picCover = null!;
        private TextBox _txtTitle = null!;
        private TextBox _txtArtist = null!;
        private Button _btnChangeCover = null!;
        private Button _btnRemoveCover = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public MetadataEditorForm(Cancion cancion, GestorReproduccion gestor)
        {
            _cancion = cancion;
            _gestor = gestor;
            _nuevaPortada = cancion.Portada != null ? (Image)cancion.Portada.Clone() : null;

            InicializarComponentes();
        }

        private void InicializarComponentes()
        {
            this.Text = "Editar Metadatos 🌸";
            this.Size = new Size(480, 270);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // === PORTADA (PICTUREBOX) ===
            _picCover = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(150, 150),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            if (_nuevaPortada != null)
            {
                _picCover.Image = _nuevaPortada;
            }

            // === BOTONES PORTADA ===
            _btnChangeCover = new Button
            {
                Text = "Cambiar 🖼️",
                Location = new Point(20, 180),
                Size = new Size(70, 28),
                Cursor = Cursors.Hand
            };
            _btnChangeCover.Click += BtnChangeCover_Click;

            _btnRemoveCover = new Button
            {
                Text = "Quitar ❌",
                Location = new Point(100, 180),
                Size = new Size(70, 28),
                Cursor = Cursors.Hand
            };
            _btnRemoveCover.Click += BtnRemoveCover_Click;

            // === TÍTULO ===
            Label lblTitle = new Label
            {
                Text = "Título:",
                Location = new Point(190, 20),
                Size = new Size(260, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };

            _txtTitle = new TextBox
            {
                Text = _cancion.Titulo,
                Location = new Point(190, 45),
                Size = new Size(250, 26)
            };

            // === ARTISTA ===
            Label lblArtist = new Label
            {
                Text = "Artista:",
                Location = new Point(190, 85),
                Size = new Size(260, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };

            _txtArtist = new TextBox
            {
                Text = _cancion.Artista,
                Location = new Point(190, 110),
                Size = new Size(250, 26)
            };

            // === BOTONES ACCIÓN ===
            _btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(190, 180),
                Size = new Size(115, 32),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };

            _btnSave = new Button
            {
                Text = "Guardar ✨",
                Location = new Point(325, 180),
                Size = new Size(115, 32),
                Cursor = Cursors.Hand
            };
            _btnSave.Click += BtnSave_Click;

            // Agregar controles
            this.Controls.AddRange(new Control[]
            {
                _picCover, _btnChangeCover, _btnRemoveCover,
                lblTitle, _txtTitle,
                lblArtist, _txtArtist,
                _btnCancel, _btnSave
            });

            // Aplicar el tema estético rosa pastel del reproductor
            ThemeRenderer.ApplyTheme(this);

            // CancelButton para cerrar con Escape
            this.CancelButton = _btnCancel;
        }

        private void BtnChangeCover_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Seleccionar Foto de Portada";
                ofd.Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        using (var temp = Image.FromFile(ofd.FileName))
                        {
                            _nuevaPortada?.Dispose();
                            // Clonar en memoria para no bloquear el archivo de imagen
                            _nuevaPortada = new Bitmap(temp);
                        }
                        _picCover.Image = _nuevaPortada;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al cargar la imagen: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnRemoveCover_Click(object? sender, EventArgs e)
        {
            _nuevaPortada?.Dispose();
            _nuevaPortada = null;
            _picCover.Image = null;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            string titulo = _txtTitle.Text.Trim();
            string artista = _txtArtist.Text.Trim();

            if (string.IsNullOrEmpty(titulo))
            {
                MessageBox.Show("El título no puede estar vacío.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Llamar al gestor para que guarde físicamente liberando el stream si es necesario
            bool guardado = _gestor.GuardarMetadatos(_cancion, titulo, artista, _nuevaPortada);

            if (guardado)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("No se pudieron guardar los cambios en el archivo físico.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.DialogResult != DialogResult.OK)
            {
                // Si el usuario canceló, liberar la copia local de la nueva portada
                _nuevaPortada?.Dispose();
            }
            base.OnFormClosing(e);
        }
    }
}
