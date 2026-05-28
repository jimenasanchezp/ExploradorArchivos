using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ExploradorArchivos.Models;

namespace ExploradorArchivos.Services;

/// <summary>
/// Provee servicios encapsulados para operaciones físicas del sistema de archivos, 
/// incluyendo acceso a funciones nativas del sistema operativo (P/Invoke) para el manejo de la Papelera.
/// </summary>
public static class FileService
{
    // === P/INVOKE PARA LA PAPELERA DE RECICLAJE ===
    /// <summary>
    /// Estructura requerida por la API nativa <c>shell32.dll</c> para ejecutar operaciones 
    /// seguras de archivos en el sistema operativo Windows.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] 
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040; // Envía a la papelera
    private const ushort FOF_NOCONFIRMATION = 0x0010; // No pregunta "Estás seguro?"

    /// <summary>
    /// Envía archivos o carpetas de forma segura a la Papelera de Reciclaje.
    /// Utiliza flags nativos para permitir la acción de deshacer (Undo) y omitir ventanas de confirmación del sistema.
    /// </summary>
    /// <param name="ruta">Ruta absoluta del elemento a eliminar.</param>
    /// <returns>Verdadero si la operación fue exitosa, falso en caso contrario.</returns>
    public static bool EnviarAPapelera(string ruta)
    {
        try
        {
            SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT // Configuración para eliminar sin confirmación y permitir deshacer
            {
                wFunc = FO_DELETE,
                pFrom = ruta + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };
            int result = SHFileOperation(ref shf); // Ejecuta la operación
            return result == 0;
        }
        catch { return false; }
    }

    // === LECTURA ASÍNCRONA DE DIRECTORIOS ===
    /// <summary>
    /// Mapea asíncronamente archivos y carpetas de un directorio físico a objetos <see cref="FileSystemItem"/>.
    /// Ejecuta la operación en un hilo secundario mediante <c>Task.Run</c> para evitar congelar la interfaz de usuario.
    /// </summary>
    /// <param name="rutaPath">Ruta absoluta del directorio a inspeccionar.</param>
    /// <returns>Lista de objetos que representan el contenido del directorio.</returns>
    public static async Task<List<FileSystemItem>> ObtenerContenidoAsync(string rutaPath)
    {
        return await Task.Run(() =>
        // task,run para ejecutar la operación de lectura en un hilo separado y evitar bloquear la UI
        // await para esperar el resultado sin bloquear el hilo principal, lo que mejora la responsividad de la aplicación
        {
            var items = new List<FileSystemItem>();
            try
            {
                DirectoryInfo dir = new DirectoryInfo(rutaPath);

                // 1. Cargar Carpetas
                foreach (var d in dir.GetDirectories())
                //cuenta cuantas subcarpetas tiene cada carpeta, para mostrar esa información adicional en la UI.
                //Si no se tienen permisos para acceder a una carpeta, se ignora y se continúa con la siguiente.
                {
                    int subFolders = 0;
                    try { subFolders = d.GetDirectories().Length; } catch { } // Ignorar sin permisos

                    items.Add(new FileSystemItem
                    // FileSystemItem transforma los datos de la carpeta de windows  a un modelo propio
                    {
                        Nombre = d.Name,
                        RutaCompleta = d.FullName,
                        EsCarpeta = true,
                        Tipo = "Carpeta",
                        TamanoTexto = "",
                        InfoAdicional = $"{subFolders} subcarpetas",
                        FechaModificacion = d.LastWriteTime
                    });
                }

                // 2. Cargar Archivos
                foreach (var f in dir.GetFiles()) // obtiene la lista de los archivos del directorio
                {
                    items.Add(new FileSystemItem
                    {
                        Nombre = f.Name,
                        RutaCompleta = f.FullName,
                        EsCarpeta = false,
                        Tipo = f.Extension.ToUpper().Replace(".", "") + " File",
                        TamanoTexto = FormatearTamano(f.Length),
                        InfoAdicional = f.Extension,
                        FechaModificacion = f.LastWriteTime
                    });
                }
            }
            catch (UnauthorizedAccessException) { /* Omitir carpetas sin permiso */ }
            return items;
        });
    }

    /// <summary>
    /// Convierte bytes crudos a formatos legibles por el usuario (B, KB, MB, GB, TB).
    /// </summary>
    /// <param name="bytes">Tamaño en bytes a convertir.</param>
    /// <returns>Cadena formateada con el peso exacto en la unidad más adecuada.</returns>
    private static string FormatearTamano(long bytes)
    {
        string[] sufijos = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblSByte = bytes;
        while (dblSByte >= 1024 && i < sufijos.Length - 1)
        {
            dblSByte /= 1024;
            i++;
        }
        return $"{dblSByte:0.##} {sufijos[i]}";
    }
}