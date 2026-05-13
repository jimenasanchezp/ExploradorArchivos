using System;

namespace ExploradorArchivos.AppFoto;

public class AppFotoMetadata
{
    public string RutaArchivo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime? FechaCaptura { get; set; }
    public string ModeloCamara { get; set; } = "Desconocido";
    
    // Coordenadas en formato decimal para el mapa
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    
    public bool TieneUbicacion => Latitud.HasValue && Longitud.HasValue;
    
    public string Dimensiones { get; set; } = "0x0";
    public string Resolucion { get; set; } = "72 dpi";
}
