using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExploradorArchivos.Services;

public static class CsvIndexer
{
    public static async Task ExportarAsync(string rootPath, string outputFile, IProgress<string>? progress, CancellationToken token)
    {
        await Task.Run(() =>
        {
            using StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8);
            writer.WriteLine("\"Ruta Completa\",\"Nombre Carpeta\",\"Carpetas\",\"Archivos Total\",\"Último Acceso\"");

            ExportarRecursivo(rootPath, writer, progress, token);
        }, token);
    }

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