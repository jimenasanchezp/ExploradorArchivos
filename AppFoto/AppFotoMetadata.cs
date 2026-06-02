using System;

namespace ExploradorArchivos.AppFoto;

/// <summary>
/// Modelo de datos para representar la información EXIF y técnica de una imagen fotográfica.
/// </summary>
public class AppFotoMetadata
{
    /// <summary>
    /// Ruta absoluta del archivo de imagen.
    /// </summary>
    public string RutaArchivo { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de pila del archivo de imagen.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora original de la captura fotográfica según el tag EXIF.
    /// </summary>
    public DateTime? FechaCaptura { get; set; }

    /// <summary>
    /// Modelo del dispositivo de cámara utilizado para tomar la foto.
    /// </summary>
    public string ModeloCamara { get; set; } = "Desconocido";
    
    /// <summary>
    /// Latitud en formato decimal.
    /// </summary>
    public double? Latitud { get; set; }

    /// <summary>
    /// Longitud en formato decimal.
    /// </summary>
    public double? Longitud { get; set; }
    
    /// <summary>
    /// Indica si la imagen contiene coordenadas geográficas completas (tanto latitud como longitud).
    /// </summary>
    public bool TieneUbicacion => Latitud.HasValue && Longitud.HasValue;
    
    /// <summary>
    /// Dimensiones físicas de la imagen en formato Ancho x Alto.
    /// </summary>
    public string Dimensiones { get; set; } = "0x0";

    /// <summary>
    /// Resolución de la imagen (por ejemplo: "72 dpi").
    /// </summary>
    public string Resolucion { get; set; } = "72 dpi";
}
