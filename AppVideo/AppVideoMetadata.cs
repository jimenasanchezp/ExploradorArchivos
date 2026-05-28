using System;

namespace ExploradorArchivos.AppVideo;

/// <summary>
/// Representa los metadatos técnicos y geográficos de un archivo de video.
/// </summary>
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
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public bool TieneUbicacion => Latitud.HasValue && Longitud.HasValue;
}
