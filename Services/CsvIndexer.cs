using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExploradorArchivos.Services;

/// <summary>
/// Motor asíncrono recursivo para exportar árboles completos de directorios.
/// Permite generar índices en formato CSV de grandes volúmenes de archivos.
/// </summary>
public static class CsvIndexer
{
    /// <summary>
    /// Lanza un hilo secundario para indizar un directorio completo en un archivo CSV estructurado.
    /// </summary>
    /// <param name="rootPath">Ruta raíz desde donde iniciar la exportación.</param>
    /// <param name="outputFile">Ruta del archivo CSV resultante.</param>
    /// <param name="progress">Notificador de progreso para reportar carpetas analizadas a la UI.</param>
    /// <param name="token">Token de cancelación para detener el proceso a petición del usuario.</param>
    public static async Task ExportarAsync(string rootPath, string outputFile, IProgress<string>? progress, CancellationToken token)
    {
        await Task.Run(() =>
        {
            using StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8);
            writer.WriteLine("\"Ruta Completa\",\"Nombre Carpeta\",\"Carpetas\",\"Archivos Total\",\"Último Acceso\"");

            ExportarRecursivo(rootPath, writer, progress, token);
        }, token);
    }

    /// <summary>
    /// Realiza un recorrido en profundidad (DFS) recursivo sobre el árbol de directorios.
    /// Valida en cada iteración la cancelación vía <paramref name="token"/> y reporta estadísticas.
    /// </summary>
    /// <param name="path">Ruta actual de la iteración.</param>
    /// <param name="writer">Flujo de escritura hacia el archivo destino CSV.</param>
    /// <param name="progress">Objeto de notificación de progreso de la UI.</param>
    /// <param name="token">Token de cancelación.</param>
    private static void ExportarRecursivo(string path, StreamWriter writer, IProgress<string>? progress, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        try
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            progress?.Report($"Indexando: {dir.Name}");

            int dirCount = dir.GetDirectories().Length;
            int fileCount = dir.GetFiles().Length;

            writer.WriteLine($"\"{dir.FullName}\",\"{dir.Name}\",{dirCount},{fileCount},\"{dir.LastAccessTime}\"");

            foreach (var subDir in dir.GetDirectories())
            {
                ExportarRecursivo(subDir.FullName, writer, progress, token);
            }
        }
        catch (UnauthorizedAccessException) { /* Ignorar system folders */ }
    }
}