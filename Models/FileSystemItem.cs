using System;
using System.IO;

namespace ExploradorArchivos.Models;

public class FileSystemItem
{
    public string Nombre { get; set; } = string.Empty;
    public string RutaCompleta { get; set; } = string.Empty;
    public bool EsCarpeta { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string TamanoTexto { get; set; } = string.Empty;
    public string InfoAdicional { get; set; } = string.Empty;
    public DateTime FechaModificacion { get; set; }

    // Propiedad calculada para saber a qué grupo pertenece en el TreeView
    public string CategoriaVisual
    {
        get
        {
            if (EsCarpeta) return "Carpetas";
            string ext = Path.GetExtension(Nombre).ToLower();
            if (ext is ".jpg" or ".png" or ".jpeg" or ".gif" or ".bmp" or ".webp") return "Imágenes";
            if (ext is ".mp3" or ".wav" or ".flac") return "Audio";
            if (ext is ".mp4" or ".mkv" or ".avi") return "Video";
            if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".csv" or ".log") return "Texto/Código";
            return "Otros";
        }
    }
}