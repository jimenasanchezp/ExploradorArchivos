using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExploradorArchivos.Services;

/// <summary>
/// Provee servicios para enviar correos electrónicos utilizando MAPI nativo o clientes de correo del sistema.
/// </summary>
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
    /// Si falla (ej. no hay cliente MAPI configurado), hace fallback a Outlook, Thunderbird o una URI mailto:.
    /// </summary>
    /// <param name="hwndOwner">Identificador de la ventana propietaria para mostrar los diálogos.</param>
    /// <param name="rutaArchivo">Ruta absoluta del archivo que se desea adjuntar.</param>
    public static void EnviarCorreoConAdjunto(IntPtr hwndOwner, string rutaArchivo)
    {
        // Operación: Validar la existencia del archivo
        if (string.IsNullOrEmpty(rutaArchivo) || !File.Exists(rutaArchivo))
        {
            MessageBox.Show("El archivo seleccionado no es válido o no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Declaración: Reservar memoria para el puntero de archivo
        IntPtr punteroArchivoMapi = IntPtr.Zero;
        try
        {
            // Inicialización: Estructura de descripción de archivo MAPI
            MapiFileDesc descripcionArchivo = new MapiFileDesc
            {
                reserved = 0,
                flags = 0,
                position = -1, // -1 coloca el adjunto en la posición por defecto
                path = rutaArchivo,
                name = Path.GetFileName(rutaArchivo)
            };

            // Operación: Reservar memoria no administrada y mapear la estructura a ella
            punteroArchivoMapi = Marshal.AllocHGlobal(Marshal.SizeOf(descripcionArchivo));
            Marshal.StructureToPtr(descripcionArchivo, punteroArchivoMapi, false);

            // Inicialización: Estructura del mensaje MAPI
            MapiMessage mensajeMapi = new MapiMessage
            {
                subject = $"Archivo compartido: {Path.GetFileName(rutaArchivo)}",
                noteText = "Hola, te comparto el archivo adjunto enviado desde el Explorador de Archivos.",
                fileCount = 1,
                files = punteroArchivoMapi
            };

            // Operación: Enviar correo utilizando la API nativa de Windows MAPI
            int codigoError = MAPISendMail(IntPtr.Zero, hwndOwner, mensajeMapi, MAPI_LOGON_UI | MAPI_DIALOG, 0);

            // Operación: Validar si la API reportó algún código de error (0: éxito, 1: cancelado por usuario)
            if (codigoError > 1)
            {
                throw new Exception($"Código de error MAPI: {codigoError}");
            }
        }
        catch (Exception)
        {
            // Fallback 1: Intentar iniciar Outlook directamente pasando la ruta del archivo adjunto
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
            catch { /* Ignorar si falla */ }

            // Fallback 2: Intentar lanzar Thunderbird directamente pasando la ruta del archivo adjunto
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
            catch { /* Ignorar si falla */ }

            // Fallback 3: Lanzar una URI mailto: como última alternativa
            try
            {
                // Inicialización y Declaración: Prepara asunto y cuerpo codificados para la URL
                string asuntoMail = Uri.EscapeDataString($"Compartir archivo: {Path.GetFileName(rutaArchivo)}");
                string cuerpoMail = Uri.EscapeDataString($"Te comparto la ruta del archivo: {rutaArchivo}\n\n(Adjunta el archivo manualmente si el cliente de correo no lo ha enlazado automáticamente).");
                string uriMailto = $"mailto:?subject={asuntoMail}&body={cuerpoMail}";
                
                // Operación: Lanzar el proceso de correo predeterminado del sistema operativo
                Process.Start(new ProcessStartInfo
                {
                    FileName = uriMailto,
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
            // Operación: Liberar explícitamente la memoria no administrada para evitar fugas de memoria
            if (punteroArchivoMapi != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(punteroArchivoMapi);
            }
        }
    }
}
