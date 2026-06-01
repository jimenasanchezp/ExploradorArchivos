using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppDataFusion
{
    /// <summary>
    /// Formulario interactivo que permite configurar los parámetros de un servidor SMTP
    /// y realizar el envío de correo de forma directa y asíncrona con el archivo CSV adjunto.
    /// </summary>
    public class FormEnviarCorreoSmtp : Form
    {
        private readonly string _attachmentPath;
        private readonly int _recordCount;

        // Controles de Configuración SMTP
        private readonly TextBox txtHost;
        private readonly TextBox txtPuerto;
        private readonly TextBox txtUsuario;
        private readonly TextBox txtContrasena;
        private readonly CheckBox chkSsl;
        private readonly CheckBox chkRecordarPass;
        
        // Controles de Contenido de Mensaje
        private readonly TextBox txtDestinatario;
        private readonly TextBox txtAsunto;
        private readonly TextBox txtCuerpo;
        
        // Indicadores y Botones
        private readonly Label lblEstado;
        private readonly Button btnEnviar;
        private readonly Button btnCancelar;

        private readonly string _configPath;

        public FormEnviarCorreoSmtp(string attachmentPath, int recordCount)
        {
            _attachmentPath = attachmentPath;
            _recordCount = recordCount;
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExploradorArchivos",
                "smtp_settings.json"
            );

            Text = "Enviar datos por Correo (SMTP)";
            Size = new Size(515, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            int y = 18;
            const int lx = 15, cx = 160, cw = 310;

            Label Lbl(string t) => new() 
            { 
                Text = t, 
                AutoSize = true, 
                ForeColor = ThemeRenderer.SecondaryText 
            };

            TextBox Txt(string d, bool p = false, bool multiline = false, int height = 24) => new()
            {
                Width = cw,
                Height = height,
                Text = d,
                BackColor = Color.White,
                ForeColor = ThemeRenderer.MainText,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = p,
                Multiline = multiline
            };

            // Sección: Configuración SMTP
            var lblSmtpTitle = new Label
            {
                Text = "Configuración del Servidor SMTP",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(lx, y),
                AutoSize = true,
                ForeColor = ThemeRenderer.Accent
            };
            y += 28;

            var l1 = Lbl("Servidor SMTP:"); l1.Location = new Point(lx, y + 3);
            txtHost = Txt("smtp.gmail.com"); txtHost.Location = new Point(cx, y); y += 32;

            var l2 = Lbl("Puerto SMTP:"); l2.Location = new Point(lx, y + 3);
            txtPuerto = Txt("587"); txtPuerto.Location = new Point(cx, y); y += 32;

            var l3 = Lbl("Correo Emisor:"); l3.Location = new Point(lx, y + 3);
            txtUsuario = Txt(""); txtUsuario.Location = new Point(cx, y); y += 32;

            var l4 = Lbl("Contraseña:"); l4.Location = new Point(lx, y + 3);
            txtContrasena = Txt("", true); txtContrasena.Location = new Point(cx, y); y += 32;

            chkSsl = new CheckBox 
            { 
                Text = "Usar SSL / TLS", 
                Checked = true, 
                Location = new Point(cx, y), 
                AutoSize = true,
                ForeColor = ThemeRenderer.SecondaryText
            };
            y += 24;

            chkRecordarPass = new CheckBox 
            { 
                Text = "Recordar contraseña (localmente)", 
                Checked = false, 
                Location = new Point(cx, y), 
                AutoSize = true,
                ForeColor = ThemeRenderer.SecondaryText
            };
            y += 32;

            var sep = new Label 
            { 
                Location = new Point(lx, y), 
                Size = new Size(470, 1), 
                BackColor = Color.Lavender 
            };
            y += 15;

            // Sección: Datos del Mensaje
            var lblMsgTitle = new Label
            {
                Text = "Información del Correo",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(lx, y),
                AutoSize = true,
                ForeColor = ThemeRenderer.Accent
            };
            y += 28;

            var l5 = Lbl("Destinatario (Para):"); l5.Location = new Point(lx, y + 3);
            txtDestinatario = Txt(""); txtDestinatario.Location = new Point(cx, y); y += 32;

            var l6 = Lbl("Asunto:"); l6.Location = new Point(lx, y + 3);
            txtAsunto = Txt("Datos exportados de Data Fusion Arena"); txtAsunto.Location = new Point(cx, y); y += 32;

            var l7 = Lbl("Mensaje:"); l7.Location = new Point(lx, y + 3);
            txtCuerpo = Txt($"Hola,\n\nSe han exportado {_recordCount} registros.\nEl archivo CSV con los datos se adjunta a este correo.", false, true, 80);
            txtCuerpo.Location = new Point(cx, y); y += 88;

            var l8 = Lbl("Archivo Adjunto:"); l8.Location = new Point(lx, y + 3);
            var lblAdjunto = new Label
            {
                Text = Path.GetFileName(attachmentPath),
                Location = new Point(cx, y + 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = ThemeRenderer.SecondaryText
            };
            y += 32;

            lblEstado = new Label
            {
                Text = "Estado: Listo.",
                Location = new Point(lx, y + 3),
                Size = new Size(200, 24),
                ForeColor = ThemeRenderer.SecondaryText
            };

            btnEnviar = new Button
            {
                Text = "Enviar",
                Location = new Point(290, y),
                Width = 95,
                Height = 28,
                FlatStyle = FlatStyle.Flat
            };
            btnEnviar.FlatAppearance.BorderSize = 0;
            btnEnviar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnEnviar.ClientRectangle, true);
            btnEnviar.Click += BtnEnviar_Click!;

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(395, y),
                Width = 90,
                Height = 28,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnCancelar.ClientRectangle, true);

            Controls.AddRange(new Control[]
            {
                lblSmtpTitle, l1, txtHost, l2, txtPuerto, l3, txtUsuario, l4, txtContrasena, chkSsl, chkRecordarPass,
                sep,
                lblMsgTitle, l5, txtDestinatario, l6, txtAsunto, l7, txtCuerpo, l8, lblAdjunto,
                lblEstado, btnEnviar, btnCancelar
            });

            AcceptButton = btnEnviar;
            CancelButton = btnCancelar;

            ThemeRenderer.ApplyTheme(this);
            CargarConfiguracion();
        }

        private void CargarConfiguracion()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var settings = JsonSerializer.Deserialize<SmtpSettings>(json);
                    if (settings != null)
                    {
                        txtHost.Text = settings.Server;
                        txtPuerto.Text = settings.Port.ToString();
                        txtUsuario.Text = settings.SenderEmail;
                        txtDestinatario.Text = settings.RecipientEmail;
                        chkSsl.Checked = settings.EnableSsl;
                        
                        if (!string.IsNullOrEmpty(settings.Password))
                        {
                            txtContrasena.Text = settings.Password;
                            chkRecordarPass.Checked = true;
                        }
                    }
                }
            }
            catch { /* Ignorar errores de carga silenciosamente */ }
        }

        private void GuardarConfiguracion()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var settings = new SmtpSettings
                {
                    Server = txtHost.Text.Trim(),
                    Port = int.TryParse(txtPuerto.Text, out int port) ? port : 587,
                    SenderEmail = txtUsuario.Text.Trim(),
                    RecipientEmail = txtDestinatario.Text.Trim(),
                    EnableSsl = chkSsl.Checked,
                    Password = chkRecordarPass.Checked ? txtContrasena.Text : ""
                };

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { /* Ignorar errores de guardado silenciosamente */ }
        }

        private async void BtnEnviar_Click(object sender, EventArgs e)
        {
            // Validaciones básicas de campos vacíos
            if (string.IsNullOrWhiteSpace(txtHost.Text) ||
                string.IsNullOrWhiteSpace(txtPuerto.Text) ||
                string.IsNullOrWhiteSpace(txtUsuario.Text) ||
                string.IsNullOrWhiteSpace(txtContrasena.Text) ||
                string.IsNullOrWhiteSpace(txtDestinatario.Text))
            {
                MessageBox.Show("Por favor complete todos los campos obligatorios.", "Campos vacíos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(txtPuerto.Text, out int port))
            {
                MessageBox.Show("El puerto debe ser un número válido.", "Puerto inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Deshabilitar interfaz mientras se envía
            btnEnviar.Enabled = false;
            btnCancelar.Enabled = false;
            lblEstado.Text = "Enviando correo...";
            lblEstado.ForeColor = Color.FromArgb(180, 140, 30);

            try
            {
                await Task.Run(() =>
                {
                    using var mail = new MailMessage();
                    mail.From = new MailAddress(txtUsuario.Text.Trim());
                    mail.To.Add(txtDestinatario.Text.Trim());
                    mail.Subject = txtAsunto.Text;
                    mail.Body = txtCuerpo.Text;
                    mail.Attachments.Add(new Attachment(_attachmentPath));

                    using var smtp = new SmtpClient(txtHost.Text.Trim(), port);
                    smtp.Credentials = new NetworkCredential(txtUsuario.Text.Trim(), txtContrasena.Text);
                    smtp.EnableSsl = chkSsl.Checked;
                    smtp.Send(mail);
                });

                GuardarConfiguracion();
                lblEstado.Text = "Enviado con éxito.";
                lblEstado.ForeColor = Color.FromArgb(52, 180, 120);
                
                MessageBox.Show("El correo se envió correctamente con el archivo adjunto.", "Envío Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblEstado.Text = "Error al enviar.";
                lblEstado.ForeColor = Color.FromArgb(200, 80, 80);
                MessageBox.Show($"Ocurrió un error al enviar el correo:\n\n{ex.Message}", "Error al enviar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnEnviar.Enabled = true;
                btnCancelar.Enabled = true;
            }
        }
    }

    /// <summary>
    /// Clase para almacenar localmente las configuraciones de SMTP del usuario.
    /// </summary>
    public class SmtpSettings
    {
        public string Server { get; set; } = "";
        public int Port { get; set; } = 587;
        public string SenderEmail { get; set; } = "";
        public string RecipientEmail { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
        public string Password { get; set; } = "";
    }
}
