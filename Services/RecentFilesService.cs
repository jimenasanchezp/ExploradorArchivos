using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExploradorArchivos.Services;

public static class RecentFilesService
{
    private static readonly string HistorialFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ExploradorArchivos",
        "recientes.txt"
    );

    /// <summary>
    /// Registra un archivo abierto agregándolo al historial persistente.
    /// Si el archivo ya existe, lo mueve al inicio. Limita el historial a 15 elementos.
    /// </summary>
    public static void RegistrarArchivoAbierto(string ruta)
    {
        try
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return;

            // Asegurarse de que el directorio de datos existe
            string? dir = Path.GetDirectoryName(HistorialFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            List<string> lineas = new List<string>();
            if (File.Exists(HistorialFilePath))
            {
                lineas = File.ReadAllLines(HistorialFilePath).ToList();
            }

            // Eliminar duplicados previos (insensible a mayúsculas/minúsculas)
            lineas.RemoveAll(x => x.Equals(ruta, StringComparison.OrdinalIgnoreCase));

            // Insertar al principio (el más reciente)
            lineas.Insert(0, ruta);

            // Mantener solo los últimos 15
            if (lineas.Count > 15)
            {
                lineas = lineas.Take(15).ToList();
            }

            File.WriteAllLines(HistorialFilePath, lineas);
        }
        catch
        {
            // Silenciar errores de escritura de historial
        }
    }

    /// <summary>
    /// Retorna la lista de archivos que realmente existen a partir de la lista del historial.
    /// </summary>
    public static List<FileInfo> ObtenerArchivosRecientes()
    {
        List<FileInfo> lista = new List<FileInfo>();
        try
        {
            if (!File.Exists(HistorialFilePath)) return lista;

            var lineas = File.ReadAllLines(HistorialFilePath);
            foreach (var ruta in lineas)
            {
                if (File.Exists(ruta))
                {
                    lista.Add(new FileInfo(ruta));
                }
            }
        }
        catch
        {
            // Silenciar errores
        }
        return lista;
    }
}
