using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Contrato común para cualquier conector de base de datos.
/// Permite que el resto de la aplicación trabaje con PostgreSQL o MariaDB
/// de forma intercambiable sin conocer la implementación concreta.
/// </summary>
public interface IDbConnector
{
    /// <summary>Cadena de conexión completa a la base de datos.</summary>
    string CadenaConexion { get; set; }

    /// <summary>Nombre de la tabla con la que opera el conector.</summary>
    string Tabla { get; set; }

    /// <summary>Máximo de filas a leer. 0 = sin límite.</summary>
    int LimiteFilas { get; set; }

    /// <summary>Lista de nombres de columna tal como existen en la BD.</summary>
    List<string> UltimasColumnas { get; }

    /// <summary>Mapeo de columnas BD → roles semánticos (id, nombre, valor, etc.).</summary>
    Dictionary<string, string> MapeoColumnas { get; }

    /// <summary>Nombre real de la columna PRIMARY KEY detectada automáticamente.</summary>
    string? ColPrimaryKey { get; }

    /// <summary>Lee y devuelve los nombres de las columnas de la tabla activa.</summary>
    List<string> ObtenerNombresColumnas();

    /// <summary>Sobreescribe el mapeo automático con la selección manual del usuario.</summary>
    void SobreescribirMapeo(
        string colId,
        string colCategoria,
        string colValor,
        string colNombre,
        string colFecha);

    /// <summary>Lee todos los registros de la tabla y los convierte en DataItems.</summary>
    List<DataItem> LeerDatos();

    /// <summary>Verifica si la conexión está disponible. Devuelve false y un mensaje de error si falla.</summary>
    bool ProbarConexion(out string mensaje);
}
