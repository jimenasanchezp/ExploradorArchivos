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
    private const ushort FOF_NOCONFIRMATION = 0x0010; // Omitir cuadro de diálogo de confirmación

    /// <summary>
    /// Envía un archivo o directorio a la Papelera de Reciclaje de Windows.
    /// </summary>
    /// <param name="ruta">Ruta absoluta del elemento que se desea eliminar.</param>
    /// <returns>Verdadero si la operación finalizó con éxito; falso en caso contrario.</returns>
    public static bool EnviarAPapelera(string ruta)
    {
        try
        {
            // Inicialización: Configura la estructura para la operación de eliminación
            SHFILEOPSTRUCT operacionArchivo = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = ruta + '\0' + '\0', // Requiere doble nulo al final del string
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };
            
            // Operación: Invoca a la API nativa de Windows
            int resultadoOperacion = SHFileOperation(ref operacionArchivo);
            
            // Operación: Retorna verdadero si el código de salida de la API es 0 (éxito)
            return resultadoOperacion == 0;
        }
        catch 
        { 
            return false; 
        }
    }

    // === LECTURA ASÍNCRONA DE DIRECTORIOS ===
    /// <summary>
    /// Obtiene de forma asíncrona la lista de carpetas y archivos dentro del directorio especificado.
    /// </summary>
    /// <param name="rutaPath">Ruta absoluta del directorio que se desea inspeccionar.</param>
    /// <returns>Una lista de objetos FileSystemItem que representan los archivos y carpetas encontrados.</returns>
    public static async Task<List<FileSystemItem>> ObtenerContenidoAsync(string rutaPath)
    {
        // Operación: Ejecuta la lectura en un hilo secundario de Task para no congelar la UI
        return await Task.Run(() =>
        {
            // Declaración e inicialización: Lista destino de items para la UI
            List<FileSystemItem> listaItems = new List<FileSystemItem>();
            try
            {
                // Inicialización: Obtiene información del directorio raíz físico
                DirectoryInfo directorioInfo = new DirectoryInfo(rutaPath);

                // Bucle: Recorre cada una de las subcarpetas del directorio actual
                foreach (DirectoryInfo subcarpeta in directorioInfo.GetDirectories())
                {
                    // Declaración e inicialización: Cantidad de subcarpetas contenidas
                    int totalSubcarpetas = 0;
                    try 
                    { 
                        totalSubcarpetas = subcarpeta.GetDirectories().Length; 
                    } 
                    catch 
                    { 
                        // Ignora de forma silenciosa si no se tienen permisos de lectura para esa carpeta específica
                    }

                    // Operación: Construye y añade el modelo visual de la carpeta a la lista
                    listaItems.Add(new FileSystemItem
                    {
                        Nombre = subcarpeta.Name,
                        RutaCompleta = subcarpeta.FullName,
                        EsCarpeta = true,
                        Tipo = "Carpeta",
                        TamanoTexto = "",
                        InfoAdicional = $"{totalSubcarpetas} subcarpetas",
                        FechaModificacion = subcarpeta.LastWriteTime
                    });
                }

                // Bucle: Recorre cada uno de los archivos del directorio actual
                foreach (FileInfo archivo in directorioInfo.GetFiles())
                {
                    // Evitar mostrar archivos ocultos de sistema como desktop.ini o thumbs.db
                    if (string.Equals(archivo.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(archivo.Name, "thumbs.db", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Operación: Construye y añade el modelo visual del archivo a la lista
                    listaItems.Add(new FileSystemItem
                    {
                        Nombre = archivo.Name,
                        RutaCompleta = archivo.FullName,
                        EsCarpeta = false,
                        Tipo = archivo.Extension.ToUpper().Replace(".", "") + " File",
                        TamanoTexto = FormatearTamano(archivo.Length),
                        InfoAdicional = archivo.Extension,
                        FechaModificacion = archivo.LastWriteTime
                    });
                }
            }
            catch (UnauthorizedAccessException) 
            { 
                // Ignorar el acceso denegado a nivel de directorio general
            }
            return listaItems;
        });
    }

    /// <summary>
    /// Convierte un valor de bytes en un formato legible para humanos (B, KB, MB, GB, TB).
    /// </summary>
    /// <param name="cantidadBytes">Número total de bytes a formatear.</param>
    /// <returns>Cadena de caracteres formateada con la unidad de medida más apropiada.</returns>
    private static string FormatearTamano(long cantidadBytes)
    {
        // Declaración e inicialización: Sufijos de unidades y variables de cálculo
        string[] sufijosUnidades = { "B", "KB", "MB", "GB", "TB" };
        int indiceUnidad = 0;
        double tamañoConvertido = cantidadBytes;

        // Bucle: Divide sucesivamente entre 1024 para escalar a la unidad de medida correcta
        while (tamañoConvertido >= 1024 && indiceUnidad < sufijosUnidades.Length - 1)
        {
            tamañoConvertido /= 1024;
            indiceUnidad++;
        }

        // Operación: Retorna el tamaño formateado con hasta dos posiciones decimales y la unidad
        return $"{tamañoConvertido:0.##} {sufijosUnidades[indiceUnidad]}";
    }

    /// <summary>
    /// Copia un directorio y todo su contenido de forma recursiva a un nuevo destino.
    /// </summary>
    public static void CopiarDirectorio(string origenDir, string destinoDir)
    {
        Directory.CreateDirectory(destinoDir);

        foreach (string file in Directory.GetFiles(origenDir))
        {
            string destFile = Path.Combine(destinoDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string folder in Directory.GetDirectories(origenDir))
        {
            string destFolder = Path.Combine(destinoDir, Path.GetFileName(folder));
            CopiarDirectorio(folder, destFolder);
        }
    }

    /// <summary>
    /// Genera una ruta de destino única para evitar colisiones agregando un sufijo numerado.
    /// </summary>
    public static string GenerarRutaUnica(string destinoDir, string nombreSinExt, string ext, string sufijoBase = "Copia")
    {
        string sufijoPart = string.IsNullOrEmpty(sufijoBase) ? "" : $" - {sufijoBase}";
        string destino = Path.Combine(destinoDir, $"{nombreSinExt}{sufijoPart}{ext}");
        int contador = 1;
        while (File.Exists(destino) || Directory.Exists(destino))
        {
            string contadorPart = string.IsNullOrEmpty(sufijoBase) ? $" ({contador})" : $" - {sufijoBase} ({contador})";
            destino = Path.Combine(destinoDir, $"{nombreSinExt}{contadorPart}{ext}");
            contador++;
        }
        return destino;
    }

    // === METODOS ENCAPSULADOS PARA EVITAR ACOPLAMIENTO DIRECTO CON SYSTEM.IO ===

    public static bool ExisteDirectorio(string ruta) => Directory.Exists(ruta);

    public static bool ExisteArchivo(string ruta) => File.Exists(ruta);

    public static void CrearDirectorio(string ruta) => Directory.CreateDirectory(ruta);

    public static void EliminarDirectorio(string ruta, bool recursivo) => Directory.Delete(ruta, recursivo);

    public static void EliminarArchivo(string ruta) => File.Delete(ruta);

    public static void MoverDirectorio(string origen, string destino) => Directory.Move(origen, destino);

    public static void MoverArchivo(string origen, string destino) => File.Move(origen, destino);

    public static char[] ObtenerCaracteresInvalidosDeNombre() => Path.GetInvalidFileNameChars();

    public static string CombinarRutas(string ruta1, string ruta2) => Path.Combine(ruta1, ruta2);
}