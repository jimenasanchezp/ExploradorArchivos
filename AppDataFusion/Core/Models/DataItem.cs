using System;
using System.Collections.Generic;

namespace ExploradorArchivos.AppDataFusion.Models;

/// <summary>
/// Modelo universal que representa cualquier registro de cualquier fuente de datos.
/// Todas las fuentes (JSON, CSV, XML, TXT, PostgreSQL, MariaDB) se convierten a este modelo.
/// </summary>
public class DataItem
{
    // === Propiedades Estándar del Modelo ===

    /// <summary>Identificador único del registro.</summary>
    public int Id { get; set; }

    /// <summary>Nombre principal o título del elemento descriptivo.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Clasificación o grupo al que pertenece el registro.</summary>
    public string Categoria { get; set; } = string.Empty;

    /// <summary>Métrica o valor numérico principal asociado al registro.</summary>
    public double Valor { get; set; }

    /// <summary>Origen de extracción del dato (ej. "json", "csv", "xml", "txt", "postgresql", "mariadb").</summary>
    public string Fuente { get; set; } = string.Empty;

    /// <summary>Fecha y hora de registro o captura de la información.</summary>
    public DateTime Fecha { get; set; } = DateTime.Now;

    /// <summary>Ubicación geográfica: Latitud del registro (opcional).</summary>
    public double? Latitude { get; set; }

    /// <summary>Ubicación geográfica: Longitud del registro (opcional).</summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Campos adicionales que no encajan en las propiedades estándar definidas.
    /// Útil para almacenar dinámicamente columnas adicionales de archivos CSV o atributos XML extras.
    /// </summary>
    public Dictionary<string, string> CamposExtra { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // === Métodos Auxiliares y Sobreescrituras ===

    /// <summary>
    /// Devuelve una representación legible de los atributos del registro formateada para consola o logs.
    /// </summary>
    public override string ToString()
        => $"{Id,5} | {Nombre,-28} | {Categoria,-18} | {Valor,10:F2} | {Fecha:yyyy-MM-dd} | {Fuente}";

    /// <summary>
    /// Determina si dos instancias de DataItem son equivalentes comparando su Id, Nombre y Categoría 
    /// (realizando una comparación insensible a mayúsculas y minúsculas).
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is DataItem other &&
        Id == other.Id &&
        string.Equals(Nombre, other.Nombre, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Categoria, other.Categoria, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Calcula el código hash basado en las propiedades que definen la igualdad (Id, Nombre y Categoría).
    /// </summary>
    public override int GetHashCode()
        => HashCode.Combine(Id, Nombre.ToLowerInvariant(), Categoria.ToLowerInvariant());

    /// <summary>
    /// Realiza y devuelve una copia superficial (shallow copy) del objeto DataItem actual.
    /// </summary>
    public DataItem Clonar() => (DataItem)MemberwiseClone();
}
