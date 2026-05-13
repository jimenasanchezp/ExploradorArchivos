using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ExploradorArchivos.Models;

namespace ExploradorArchivos.Services;

public static class FileService
{
    // === P/INVOKE PARA LA PAPELERA DE RECICLAJE ===
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

    public static bool EnviarAPapelera(string ruta)
    {
        try
        {
            SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = ruta + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };
            int result = SHFileOperation(ref shf);
            return result == 0;
        }
        catch { return false; }
    }

    // === LECTURA ASÍNCRONA DE DIRECTORIOS ===
    public static async Task<List<FileSystemItem>> ObtenerContenidoAsync(string rutaPath)
    {
        return await Task.Run(() =>
        {
            var items = new List<FileSystemItem>();
            try
            {
                DirectoryInfo dir = new DirectoryInfo(rutaPath);

                // 1. Cargar Carpetas
                foreach (var d in dir.GetDirectories())
                {
                    int subFolders = 0;
                    try { subFolders = d.GetDirectories().Length; } catch { } // Ignorar sin permisos

                    items.Add(new FileSystemItem
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
                foreach (var f in dir.GetFiles())
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