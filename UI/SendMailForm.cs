using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

public class SendMailForm : Form
{
    private string _filePath;
    private SmtpConfig _config = new();

    private Panel pnlTitleBar = default!;
    private Label lblTitle = default!;
    
    // Controles de Envío
    private TextBox txtTo = default!;
    private TextBox txtSubject = default!;
    private TextBox txtBody = default!;
    private Label lblAttachment = default!;
    
    // Controles SMTP
    private TextBox txtSmtpServer = default!;
    private TextBox txtSmtpPort = default!;
    private CheckBox chkSsl = default!;
    private TextBox txtSenderEmail = default!;
    private TextBox txtSenderPassword = default!;

    private Button btnEnviar = default!;
    private Button btnCancelar = default!;

    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExploradorArchivos",
        "smtp_config.json"
    );

    public class SmtpConfig
    {
        public string Server { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";
    }

    public SendMailForm(string filePath)
    {
        _filePath = filePath;
        CargarConfiguracion();
        InitializeComponent();
        ThemeRenderer.ApplyTheme(this);

        ConfigurarBotonClasico(btnEnviar);
        ConfigurarBotonClasico(btnCancelar);

        // Prellenar campos
        lblAttachment.Text = $"📎  Archivo: {Path.GetFileName(_filePath)} ({ObtenerTamanoArchivo()})";
        txtSubject.Text = $"Envío de archivo: {Path.GetFileName(_filePath)}";
        
        // Prellenar campos SMTP
        txtSmtpServer.Text = _config.Server;
        txtSmtpPort.Text = _config.Port.ToString();
        chkSsl.Checked = _config.EnableSsl;
        txtSenderEmail.Text = _config.SenderEmail;
        txtSenderPassword.Text = _config.SenderPassword;
    }

    private void InitializeComponent()
    {
        this.Size = new Size(620, 680);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.None;
        this.Padding = new Padding(2);

        // Borde clásico 3D
        this.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, this.ClientRectangle, true);

        // Title Bar
        pnlTitleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 35,
            BackColor = ThemeRenderer.SecondaryBg
        };
        pnlTitleBar.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTitleBar.ClientRectangle, true);

        // Arrastrar Ventana
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTitleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTitleBar.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTitleBar.MouseUp += (s, e) => { isDragging = false; };

        // Botón Cerrar (Semáforo)
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 15);
        btnClose.Click += (s, e) => this.Close();
        pnlTitleBar.Controls.Add(btnClose);

        lblTitle = new Label
        {
            Text = "✉️ Enviar Correo Electrónico",
            Location = new Point(40, 8),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ThemeRenderer.SecondaryText
        };
        pnlTitleBar.Controls.Add(lblTitle);

        // Panel de Contenido principal
        Panel pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            Location = new Point(2, 37),
            Size = new Size(616, 641),
            BackColor = ThemeRenderer.MainBg
        };

        // --- CAMPOS DE CORREO ---
        Label lblTo = new Label { Text = "Para (Email Destinatario):", Location = new Point(20, 20), AutoSize = true };
        txtTo = new TextBox { Location = new Point(20, 42), Width = 576 };

        Label lblSubject = new Label { Text = "Asunto:", Location = new Point(20, 80), AutoSize = true };
        txtSubject = new TextBox { Location = new Point(20, 102), Width = 576 };

        Label lblBody = new Label { Text = "Mensaje / Cuerpo:", Location = new Point(20, 140), AutoSize = true };
        txtBody = new TextBox { Location = new Point(20, 162), Width = 576, Height = 120, Multiline = true, ScrollBars = ScrollBars.Vertical };

        lblAttachment = new Label
        {
            Text = "📎  Archivo Adjunto",
            Location = new Point(20, 295),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ThemeRenderer.SecondaryText
        };

        // --- CONFIGURACIÓN SMTP (Recuadro clásico) ---
        Panel pnlSmtp = new Panel
        {
            Location = new Point(20, 335),
            Size = new Size(576, 205),
            BackColor = Color.White
        };
        pnlSmtp.Paint += (s, e) => {
            ThemeRenderer.DrawClassicBorder(e.Graphics, pnlSmtp.ClientRectangle, false); // Sunken
        };

        Label lblSmtpTitle = new Label
        {
            Text = "⚙️ CONFIGURACIÓN DEL REMITENTE (SMTP)",
            Location = new Point(15, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = ThemeRenderer.SecondaryText
        };

        Label lblSmtpServer = new Label { Text = "Servidor:", Location = new Point(15, 45), AutoSize = true };
        txtSmtpServer = new TextBox { Location = new Point(15, 65), Width = 230 };

        Label lblSmtpPort = new Label { Text = "Puerto:", Location = new Point(265, 45), AutoSize = true };
        txtSmtpPort = new TextBox { Location = new Point(265, 65), Width = 80 };

        chkSsl = new CheckBox { Text = "SSL/TLS", Location = new Point(365, 65), AutoSize = true, Checked = true };

        Label lblSenderEmail = new Label { Text = "Tu Correo Electrónico:", Location = new Point(15, 110), AutoSize = true };
        txtSenderEmail = new TextBox { Location = new Point(15, 130), Width = 260 };

        Label lblSenderPassword = new Label { Text = "Contraseña o Token de App:", Location = new Point(295, 110), AutoSize = true };
        txtSenderPassword = new TextBox { Location = new Point(295, 130), Width = 260, PasswordChar = '*' };

        Label lblInfoHelp = new Label
        {
            Text = "* Nota: Si usas Gmail/Outlook, debes activar la 'Contraseña de Aplicación' en la seguridad de tu cuenta.",
            Location = new Point(15, 172),
            Size = new Size(540, 25),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic),
            ForeColor = Color.Gray
        };

        pnlSmtp.Controls.AddRange(new Control[] {
            lblSmtpTitle, lblSmtpServer, txtSmtpServer, lblSmtpPort, txtSmtpPort, chkSsl,
            lblSenderEmail, txtSenderEmail, lblSenderPassword, txtSenderPassword, lblInfoHelp
        });

        // --- BOTONES DE ACCIÓN ---
        btnEnviar = new Button { Text = "✉️  Enviar", Location = new Point(360, 560), Size = new Size(110, 32) };
        btnEnviar.Click += async (s, e) => await EnviarCorreoAsync();

        btnCancelar = new Button { Text = "❌  Cancelar", Location = new Point(486, 560), Size = new Size(110, 32) };
        btnCancelar.Click += (s, e) => this.Close();

        pnlContent.Controls.AddRange(new Control[] {
            lblTo, txtTo, lblSubject, txtSubject, lblBody, txtBody, lblAttachment,
            pnlSmtp, btnEnviar, btnCancelar
        });

        this.Controls.Add(pnlContent);
        this.Controls.Add(pnlTitleBar);
    }

    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button b = new Button { Name = "btnSemaforo", Location = new Point(x, 10), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(pnlTitleBar.BackColor);
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
        };
        return b;
    }

    private void ConfigurarBotonClasico(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = ThemeRenderer.MainBg;
        btn.Font = new Font("MS Sans Serif", 9, FontStyle.Bold);
        btn.ForeColor = ThemeRenderer.MainText;

        bool isPressed = false;
        btn.MouseDown += (s, e) => { isPressed = true; btn.Invalidate(); };
        btn.MouseUp += (s, e) => { isPressed = false; btn.Invalidate(); };

        btn.Paint += (s, e) =>
        {
            e.Graphics.Clear(btn.BackColor);
            ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, !isPressed);

            int offset = isPressed ? 1 : 0;
            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font,
                new Rectangle(btn.ClientRectangle.X + offset, btn.ClientRectangle.Y + offset, btn.ClientRectangle.Width, btn.ClientRectangle.Height),
                btn.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }

    private string ObtenerTamanoArchivo()
    {
        try
        {
            var info = new FileInfo(_filePath);
            double kb = info.Length / 1024.0;
            return kb >= 1024 ? $"{kb / 1024.0:F2} MB" : $"{kb:F2} KB";
        }
        catch { return "0 KB"; }
    }

    private async Task EnviarCorreoAsync()
    {
        // Validaciones básicas
        string recipient = txtTo.Text.Trim();
        string subject = txtSubject.Text.Trim();
        string body = txtBody.Text;

        if (string.IsNullOrEmpty(recipient))
        {
            MessageBox.Show("Por favor ingresa el correo del destinatario.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Validar tamaño del archivo adjunto (Límite estándar de 25 MB para Gmail y la mayoría de servidores SMTP)
        try
        {
            var fileInfo = new FileInfo(_filePath);
            if (fileInfo.Length > 25 * 1024 * 1024) // 25 MB
            {
                MessageBox.Show(
                    "El archivo seleccionado supera el límite de 25 MB permitido por la mayoría de los servidores de correo (como Gmail y Outlook). No es posible enviarlo por este medio.",
                    "Archivo demasiado grande",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al validar el archivo adjunto: {ex.Message}", "Error de Validación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Obtener configuración SMTP ingresada
        if (!int.TryParse(txtSmtpPort.Text, out int port))
        {
            MessageBox.Show("Puerto SMTP inválido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Server = txtSmtpServer.Text.Trim();
        _config.Port = port;
        _config.EnableSsl = chkSsl.Checked;
        _config.SenderEmail = txtSenderEmail.Text.Trim();
        _config.SenderPassword = txtSenderPassword.Text;

        if (string.IsNullOrEmpty(_config.SenderEmail) || string.IsNullOrEmpty(_config.SenderPassword))
        {
            MessageBox.Show("Por favor completa los datos de tu cuenta de correo emisora (SMTP).", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Guardar configuración para la próxima vez
        GuardarConfiguracion();

        // Cambiar estado visual a "Enviando"
        btnEnviar.Enabled = false;
        btnCancelar.Enabled = false;
        btnEnviar.Text = "Enviando...";
        this.Cursor = Cursors.WaitCursor;

        try
        {
            await Task.Run(() =>
            {
                using var mail = new MailMessage();
                mail.From = new MailAddress(_config.SenderEmail);
                mail.To.Add(recipient);
                mail.Subject = subject;
                mail.Body = body;

                // Adjuntar el archivo seleccionado
                var attachment = new Attachment(_filePath);
                mail.Attachments.Add(attachment);

                using var smtp = new SmtpClient(_config.Server, _config.Port);
                smtp.Credentials = new NetworkCredential(_config.SenderEmail, _config.SenderPassword);
                smtp.EnableSsl = _config.EnableSsl;

                smtp.Send(mail);
            });

            MessageBox.Show("¡El correo electrónico y el archivo adjunto se enviaron con éxito!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ocurrió un error al enviar el correo:\n{ex.InnerException?.Message ?? ex.Message}", "Error al Enviar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnEnviar.Enabled = true;
            btnCancelar.Enabled = true;
            btnEnviar.Text = "✉️  Enviar";
            this.Cursor = Cursors.Default;
        }
    }

    private void CargarConfiguracion()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonSerializer.Deserialize<SmtpConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                }
            }
        }
        catch { /* Ignorar errores al cargar */ }
    }

    private void GuardarConfiguracion()
    {
        try
        {
            string? directory = Path.GetDirectoryName(ConfigFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch { /* Ignorar errores al guardar */ }
    }
}
