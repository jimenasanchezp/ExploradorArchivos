namespace ExploradorArchivos.AppDataFusion.Database;

/// <summary>
/// Fábrica estática que crea el conector de base de datos correcto
/// según el tipo solicitado. Desacopla al resto de la aplicación de
/// las implementaciones concretas (PostgreSQL, MariaDB).
/// </summary>
public static class DbConnectorFactory
{
    /// <summary>
    /// Crea e inicializa un conector de base de datos para el motor indicado.
    /// </summary>
    /// <param name="tipo">Tipo de motor: "postgresql" o "mariadb".</param>
    /// <param name="cadenaConexion">Cadena de conexión completa.</param>
    /// <param name="tabla">Nombre de la tabla de trabajo.</param>
    /// <returns>Un IDbConnector listo para usar.</returns>
    /// <exception cref="ArgumentException">Si el tipo no es reconocido.</exception>
    public static IDbConnector Crear(string tipo, string cadenaConexion, string tabla)
        => tipo.Trim().ToLowerInvariant() switch
        {
            "postgresql" => new PostgreSqlConnector(cadenaConexion, tabla),
            "mariadb"    => new MariaDbConnector(cadenaConexion, tabla),
            _            => throw new ArgumentException($"Tipo de base de datos no soportado: '{tipo}'. Use 'postgresql' o 'mariadb'.")
        };
}
