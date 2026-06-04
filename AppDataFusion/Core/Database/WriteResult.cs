namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Resultado de una operación de escritura masiva en la base de datos.
/// Encapsula el estado de éxito, conteo de registros y mensajes de error.
/// </summary>
public class WriteResult
{
    /// <summary>Indica si la operación se completó sin errores fatales.</summary>
    public bool Exito { get; set; }

    /// <summary>Mensaje descriptivo del resultado (éxito o error).</summary>
    public string Mensaje { get; set; } = "";

    /// <summary>Número total de registros insertados correctamente.</summary>
    public int Insertados { get; set; }

    /// <summary>Número de registros que fallaron durante la inserción.</summary>
    public int Errores { get; set; }
}
