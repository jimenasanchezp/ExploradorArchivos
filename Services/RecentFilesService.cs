using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExploradorArchivos.Services;

/// <summary>
/// Provee servicios para el registro y obtención de los archivos abiertos recientemente.
/// Mantiene los datos persistidos en el almacenamiento local de la aplicación.
/// </summary>
public static class RecentFilesService
{
    // Declaración: Ruta del archivo donde se almacena de forma persistente la lista de archivos recientes
    private static readonly string HistorialFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ExploradorArchivos",
        "recientes.txt"
    );

    /// <summary>
    /// Registra un archivo abierto agregándolo al historial persistente.
    /// Si el archivo ya existía previamente, lo mueve al inicio de la lista. Limita el historial a un máximo de 15 elementos.
    /// </summary>
    /// <param name="ruta">Ruta absoluta del archivo abierto.</param>
    public static void RegistrarArchivoAbierto(string ruta)
    {
        try
        {
            // Operación: Validar que la ruta del archivo no sea vacía y que el archivo físicamente exista
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta))
            {
                return;
            }

            // Declaración e inicialización: Obtiene el directorio padre de la ruta de historial
            string? dir = Path.GetDirectoryName(HistorialFilePath);
            
            // Operación: Crea el directorio para el archivo de historial en caso de que no exista
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Declaración e inicialización: Lista para almacenar las líneas de historial leídas de disco
            List<string> lineas = new List<string>();
            if (File.Exists(HistorialFilePath))
            {
                // Operación: Lee todas las líneas actuales del historial
                lineas = File.ReadAllLines(HistorialFilePath).ToList();
            }

            // Operación: Eliminar el duplicado previo en caso de existir, comparando sin diferenciar mayúsculas y minúsculas
            lineas.RemoveAll(x => x.Equals(ruta, StringComparison.OrdinalIgnoreCase));

            // Operación: Inserta la ruta del archivo en la primera posición (indicando el más reciente)
            lineas.Insert(0, ruta);

            // Operación: Trunca la lista para almacenar únicamente los últimos 15 archivos abiertos
            if (lineas.Count > 15)
            {
                lineas = lineas.Take(15).ToList();
            }

            // Operación: Escribe los cambios actualizados en el archivo de texto del historial
            File.WriteAllLines(HistorialFilePath, lineas);
        }
        catch
        {
            // Operación: Silenciar errores de lectura/escritura físicos para evitar detener el flujo de la aplicación
        }
    }

    /// <summary>
    /// Retorna una lista con la información de los archivos recientes que aún existen en el disco duro.
    /// </summary>
    /// <returns>Una lista de objetos FileInfo representando los archivos recientemente abiertos.</returns>
    public static List<FileInfo> ObtenerArchivosRecientes()
    {
        // Declaración e inicialización: Lista destino de archivos existentes
        List<FileInfo> listaArchivosValidos = new List<FileInfo>();
        try
        {
            // Operación: Validar si el archivo de historial existe en el disco
            if (!File.Exists(HistorialFilePath))
            {
                return listaArchivosValidos;
            }

            // Declaración e inicialización: Carga las rutas del archivo de historial
            string[] lineasHistorial = File.ReadAllLines(HistorialFilePath);
            
            // Bucle: Recorre cada una de las rutas encontradas en el historial
            foreach (string ruta in lineasHistorial)
            {
                // Operación: Añade a la lista destino únicamente si el archivo físico aún existe
                if (File.Exists(ruta))
                {
                    listaArchivosValidos.Add(new FileInfo(ruta));
                }
            }
        }
        catch
        {
            // Operación: Silenciar errores para evitar fallos de interfaz
        }
        
        return listaArchivosValidos;
    }
}
