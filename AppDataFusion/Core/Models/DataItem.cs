namespace ExploradorArchivos.AppDataFusion.Models;

/// <summary>
/// Modelo universal que representa cualquier registro de cualquier fuente de datos.
/// Todas las fuentes (JSON, CSV, XML, TXT, PostgreSQL, MariaDB) se convierten a este modelo.
/// </summary>
public class DataItem
{
    public int    Id        { get; set; }
    public string Nombre    { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public double Valor     { get; set; }
    /// <summary>Origen del dato: "json" | "csv" | "xml" | "txt" | "postgresql" | "mariadb"</summary>
    public string Fuente    { get; set; } = string.Empty;
    public DateTime Fecha   { get; set; } = DateTime.Now;
    public double?  Latitude  { get; set; }
    public double?  Longitude { get; set; }

    /// <summary>
    /// Campos extra que no encajan en las propiedades base.
    /// Útil para columnas adicionales de CSV o atributos XML extras.
    /// </summary>
    public Dictionary<string, string> CamposExtra { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>Representación de una fila para mostrar en consola.</summary>
    public override string ToString()
        => $"{Id,5} | {Nombre,-28} | {Categoria,-18} | {Valor,10:F2} | {Fecha:yyyy-MM-dd} | {Fuente}";

    /// <summary>
    /// Dos DataItems son iguales si vienen de la misma fuente y tienen el mismo Id y Nombre.
    /// Usado para detección de duplicados entre fuentes distintas.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is DataItem other)
            return Id == other.Id
                && string.Equals(Nombre,    other.Nombre,    StringComparison.OrdinalIgnoreCase)
                && string.Equals(Categoria, other.Categoria, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public override int GetHashCode()
        => HashCode.Combine(Id, Nombre.ToLowerInvariant(), Categoria.ToLowerInvariant());

    /// <summary>Devuelve una copia superficial del item.</summary>
    public DataItem Clonar() => (DataItem)MemberwiseClone();
}

