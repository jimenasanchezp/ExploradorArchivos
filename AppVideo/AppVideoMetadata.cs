using System;

namespace ExploradorArchivos.AppVideo;

public class AppVideoMetadata
{
    public string RutaArchivo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public TimeSpan Duracion { get; set; }
    public string Resolucion { get; set; } = "Desconocida";
    public string Formato { get; set; } = "Desconocido";
    public string Codec { get; set; } = "Desconocido";
    public long TamanoBytes { get; set; }
    public float FrameRate { get; set; }
}
