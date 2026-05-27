using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExploradorArchivos.Services;

public static class EmailService
{
    // === IMPORTACIÓN DE MAPI32.DLL PARA ENVÍO DE CORREO CON ADJUNTOS ===
    [DllImport("Mapi32.dll", CharSet = CharSet.Ansi)]
    private static extern int MAPISendMail(IntPtr session, IntPtr hwnd, MapiMessage message, int flags, int reserved);

    private const int MAPI_LOGON_UI = 0x00000001;
    private const int MAPI_DIALOG = 0x00000008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiMessage
    {
        public int reserved;
        public string? subject;
        public string? noteText;
        public string? messageType;
        public string? dateReceived;
        public string? conversationID;
        public int flags;
        public IntPtr originator;
        public int recipCount;
        public IntPtr recips;
        public int fileCount;
        public IntPtr files;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiFileDesc
    {
        public int reserved;
        public int flags;
        public int position;
        public string? path;
        public string? name;
        public IntPtr type;
    }

    /// <summary>
    /// Envía un archivo por correo utilizando el cliente predeterminado del sistema (MAPI).
    /// Si falla (ej. no hay cliente MAPI configurado), hace fallback a una URI mailto:.
    /// </summary>
    public static void EnviarCorreoConAdjunto(IntPtr hwndOwner, string rutaArchivo)
    {
        if (string.IsNullOrEmpty(rutaArchivo) || !File.Exists(rutaArchivo))
        {
            MessageBox.Show("El archivo seleccionado no es válido o no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        IntPtr filePtr = IntPtr.Zero;
        try
        {
            // Configurar la descripción del archivo adjunto
            var fileDesc = new MapiFileDesc
            {
                reserved = 0,
                flags = 0,
                position = -1, // -1 coloca el adjunto en la posición por defecto
                path = rutaArchivo,
                name = Path.GetFileName(rutaArchivo)
            };

            // Reservar memoria no administrada para la estructura del archivo
            filePtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileDesc));
            Marshal.StructureToPtr(fileDesc, filePtr, false);

            // Configurar el mensaje
            var message = new MapiMessage
            {
                subject = $"Archivo compartido: {Path.GetFileName(rutaArchivo)}",
                noteText = "Hola, te comparto el archivo adjunto enviado desde el Explorador de Archivos.",
                fileCount = 1,
                files = filePtr
            };

            // Enviar mensaje abriendo la interfaz de diálogo del cliente de correo
            int error = MAPISendMail(IntPtr.Zero, hwndOwner, message, MAPI_LOGON_UI | MAPI_DIALOG, 0);

            // Códigos de error de MAPI (0 = Éxito, 1 = Cancelado por usuario)
            if (error > 1)
            {
                throw new Exception($"Código de error MAPI: {error}");
            }
        }
        catch (Exception)
        {
            // Fallback 1: Intentar lanzar Outlook directamente (soporta adjuntos vía línea de comandos)
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "outlook.exe",
                    Arguments = $"/a \"{rutaArchivo}\"",
                    UseShellExecute = true
                });
                return;
            }
            catch { /* Ignorar si Outlook no está instalado o falla */ }

            // Fallback 2: Intentar lanzar Thunderbird directamente (soporta adjuntos vía línea de comandos)
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "thunderbird.exe",
                    Arguments = $"-compose \"attachment='{rutaArchivo}'\"",
                    UseShellExecute = true
                });
                return;
            }
            catch { /* Ignorar si Thunderbird no está instalado o falla */ }

            // Fallback 3: Esquema mailto: normal (abre cualquier cliente, pero el usuario debe adjuntar el archivo manualmente)
            try
            {
                string subject = Uri.EscapeDataString($"Compartir archivo: {Path.GetFileName(rutaArchivo)}");
                string body = Uri.EscapeDataString($"Te comparto la ruta del archivo: {rutaArchivo}\n\n(Adjunta el archivo manualmente si el cliente de correo no lo ha enlazado automáticamente).");
                string mailtoUri = $"mailto:?subject={subject}&body={body}";
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = mailtoUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo iniciar el cliente de correo: {ex.Message}", "Error al enviar correo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            // Liberar memoria no administrada
            if (filePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(filePtr);
            }
        }
    }
}
