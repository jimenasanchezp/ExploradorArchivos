using System.Threading.Tasks;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Interfaz común para todas las estrategias de conversión de archivos.
    /// </summary>
    public interface IFileConverter
    {
        /// <summary>
        /// Convierte de forma asíncrona un archivo de origen físico a un formato de destino específico.
        /// </summary>
        /// <param name="rutaOrigen">Ruta absoluta del archivo de entrada.</param>
        /// <param name="rutaDestino">Ruta absoluta del archivo de salida.</param>
        /// <param name="esImagen">Especifica si el archivo de entrada original es una imagen.</param>
        Task ConvertirAsync(string rutaOrigen, string rutaDestino, bool esImagen);
    }
}
