using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExploradorArchivos.Services;

/// <summary>
/// Provee funcionalidad para indizar recursivamente directorios y exportar la información a archivos CSV.
/// </summary>
public static class CsvIndexer
{
    /// <summary>
    /// Exporta la estructura de directorios a un archivo CSV de manera asíncrona.
    /// </summary>
    /// <param name="rootPath">Ruta raíz del directorio a escanear.</param>
    /// <param name="outputFile">Ruta completa del archivo CSV destino.</param>
    /// <param name="progress">Interfaz para notificar el progreso a la interfaz de usuario.</param>
    /// <param name="token">Token para solicitar la cancelación de la operación.</param>
    /// <returns>Una tarea que representa la operación asíncrona.</returns>
    public static async Task ExportarAsync(string rootPath, string outputFile, IProgress<string>? progress, CancellationToken token)
    {
        // Operación: Inicia y ejecuta la tarea de indización en un hilo de fondo
        await Task.Run(() =>
        {
            // Inicialización: Abre el flujo de escritura para el archivo CSV con codificación UTF-8
            using StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8);

            // Operación: Escribe la cabecera con los nombres de columna del archivo CSV
            writer.WriteLine("\"Ruta Completa\",\"Nombre Carpeta\",\"Carpetas\",\"Archivos Total\",\"Último Acceso\"");

            // Operación: Inicia el recorrido recursivo sobre la ruta raíz especificada
            ExportarRecursivo(rootPath, writer, progress, token);
        }, token);
    }

    /// <summary>
    /// Recorre recursivamente los directorios registrando estadísticas en el escritor de archivos.
    /// </summary>
    /// <param name="path">Ruta del directorio actual bajo análisis.</param>
    /// <param name="writer">Escritor de flujos para emitir los registros CSV.</param>
    /// <param name="progress">Canal de reporte de progreso.</param>
    /// <param name="token">Token para verificar solicitudes de cancelación.</param>
    private static void ExportarRecursivo(string path, StreamWriter writer, IProgress<string>? progress, CancellationToken token)
    {
        // Operación: Verifica si el usuario ha solicitado cancelar la tarea en este punto de la recursión
        if (token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            // Inicialización: Obtiene información del directorio actual
            DirectoryInfo dir = new DirectoryInfo(path);

            // Operación: Reporta el nombre de la carpeta actual que está siendo procesada
            progress?.Report($"Indexando: {dir.Name}");

            // Declaración y operación: Cuenta la cantidad de subdirectorios y archivos del directorio actual
            int dirCount = dir.GetDirectories().Length;
            int fileCount = dir.GetFiles().Length;

            // Operación: Escribe la fila correspondiente en el archivo CSV
            writer.WriteLine($"\"{dir.FullName}\",\"{dir.Name}\",{dirCount},{fileCount},\"{dir.LastAccessTime}\"");

            // Bucle: Recorre cada uno de los subdirectorios hijos para procesarlos recursivamente
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                ExportarRecursivo(subDir.FullName, writer, progress, token);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Operación: Ignora silenciosamente los directorios protegidos o del sistema sin permisos de lectura
        }
    }
}