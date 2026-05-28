using System;
using System.IO;

namespace ExploradorArchivos.Models;

/// <summary>
/// Representa la abstracción de un archivo o carpeta física en el disco.
/// Contiene propiedades que describen su estado, ubicación y categoría para ser renderizadas en la interfaz.
/// </summary>
public class FileSystemItem
{
    /// <summary>
    /// Obtiene o establece el nombre local del elemento (archivo o carpeta).
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Obtiene o establece la ruta física absoluta en el disco.
    /// </summary>
    public string RutaCompleta { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el elemento actual es un nodo de directorio (true) o un archivo (false).
    /// </summary>
    public bool EsCarpeta { get; set; }

    /// <summary>
    /// Obtiene o establece una clasificación amigable del archivo (ej. "Documento de texto").
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Obtiene o establece el peso pre-formateado del archivo (ej: "1.2 MB"). Para carpetas suele estar vacío.
    /// </summary>
    public string TamanoTexto { get; set; } = string.Empty;

    /// <summary>
    /// Campo flexible para metadatos complementarios en pantalla (como dimensiones de una foto o duración de un video).
    /// </summary>
    public string InfoAdicional { get; set; } = string.Empty;

    /// <summary>
    /// Obtiene o establece la fecha y hora del último cambio registrado en el archivo o carpeta.
    /// </summary>
    public DateTime FechaModificacion { get; set; }

    /// <summary>
    /// Propiedad calculada que determina a qué grupo lógico pertenece el elemento para su visualización.
    /// Usa <see cref="Path.GetExtension(string)"/> para clasificar los archivos.
    /// </summary>
    /// <value>Retorna "Carpetas", "Imágenes", "Audio", "Video", "Texto/Código" u "Otros".</value>
    public string CategoriaVisual
    {
        get
        {
            // Si es un directorio físico, su categoría siempre es "Carpetas".
            if (EsCarpeta) return "Carpetas";
            
            // Se extrae la extensión en minúsculas para evaluar los distintos formatos soportados.
            string ext = Path.GetExtension(Nombre).ToLower(); 
            
            // Clasificación por extensiones de imagen comunes.
            if (ext is ".jpg" or ".png" or ".jpeg" or ".gif" or ".bmp" or ".webp") return "Imágenes";
            
            // Clasificación por extensiones de audio compatibles.
            if (ext is ".mp3" or ".wav" or ".flac") return "Audio";
            
            // Clasificación por extensiones de video que el sistema puede reproducir.
            if (ext is ".mp4" or ".mkv" or ".avi") return "Video";
            
            // Clasificación de archivos de texto, código fuente y datos estructurados.
            if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".csv" or ".log") return "Texto/Código";
            
            // Archivos genéricos que no coinciden con las categorías anteriores.
            return "Otros";
        }
    }
}