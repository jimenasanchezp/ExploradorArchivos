using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Configuración para la conexión con el servidor de correo emisor SMTP.
    /// </summary>
    public class SmtpConfig
    {
        public string Server { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";
    }

    /// <summary>
    /// Servicio encargado de la configuración SMTP y el envío asíncrono de correos electrónicos con archivos adjuntos.
    /// </summary>
    public static class SmtpMailService
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ExploradorArchivos",
            "smtp_config.json"
        );

        /// <summary>
        /// Carga la configuración SMTP guardada en el directorio de datos de la aplicación.
        /// </summary>
        public static SmtpConfig CargarConfiguracion()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<SmtpConfig>(json) ?? new SmtpConfig();
                }
            }
            catch
            {
                // Retornar por defecto si ocurre algún error
            }
            return new SmtpConfig();
        }

        /// <summary>
        /// Guarda la configuración SMTP en el directorio de datos de la aplicación.
        /// </summary>
        public static void GuardarConfiguracion(SmtpConfig config)
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch
            {
                // Ignorar de forma silenciosa para mantener consistencia con el comportamiento original
            }
        }

        /// <summary>
        /// Realiza el envío del correo electrónico con el archivo adjunto usando los parámetros SMTP especificados de forma asíncrona.
        /// </summary>
        public static async Task EnviarCorreoAsync(SmtpConfig config, string recipient, string subject, string body, string filePath)
        {
            await Task.Run(() =>
            {
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(config.SenderEmail);
                    mail.To.Add(recipient);
                    mail.Subject = subject;
                    mail.Body = body;

                    var attachment = new Attachment(filePath);
                    mail.Attachments.Add(attachment);

                    using (var smtp = new SmtpClient(config.Server, config.Port))
                    {
                        smtp.Credentials = new NetworkCredential(config.SenderEmail, config.SenderPassword);
                        smtp.EnableSsl = config.EnableSsl;
                        smtp.Send(mail);
                    }
                }
            });
        }
    }
}
