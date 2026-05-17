using System;
using System.IO;

namespace ExploradorArchivos.Models;

public class FileSystemItem
{
    public string Nombre { get; set; } = string.Empty; // El nombre del archivo o carpeta
    public string RutaCompleta { get; set; } = string.Empty; // La ruta completa del archivo o carpeta
    public bool EsCarpeta { get; set; } // Indica si es una carpeta o un archivo
    public string Tipo { get; set; } = string.Empty; // El tipo de archivo (extensión) o "Carpeta" si es una carpeta
    public string TamanoTexto { get; set; } = string.Empty; // El tamaño del archivo en formato legible o vacío si es una carpeta
    public string InfoAdicional { get; set; } = string.Empty; // Información adicional como fecha de creación o modificación
    public DateTime FechaModificacion { get; set; } // La fecha de modificación del archivo o carpeta

    // Propiedad calculada para saber a qué grupo pertenece en el TreeView
    public string CategoriaVisual
    {
        // Si es una carpeta, la categoría es "Carpetas". Si es un archivo, se categoriza según su extensión.
        get
        {
            if (EsCarpeta) return "Carpetas";
            string ext = Path.GetExtension(Nombre).ToLower(); // Obtener la extensión del archivo en minúsculas
            if (ext is ".jpg" or ".png" or ".jpeg" or ".gif" or ".bmp" or ".webp") return "Imágenes";
            if (ext is ".mp3" or ".wav" or ".flac") return "Audio";
            if (ext is ".mp4" or ".mkv" or ".avi") return "Video";
            if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".csv" or ".log") return "Texto/Código";
            return "Otros";
        }
    }
}