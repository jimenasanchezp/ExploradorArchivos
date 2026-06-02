using System;
using System.Collections.Generic;
using System.IO;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Servicio encargado de persistir y cargar colecciones de rutas o texto en disco.
    /// </summary>
    public static class PersistenceService
    {
        /// <summary>
        /// Carga líneas de texto desde un archivo, verificando que correspondan a archivos o directorios existentes.
        /// </summary>
        public static List<string> CargarRutasExistentes(string filePath)
        {
            var rutasValidas = new List<string>();
            try
            {
                if (File.Exists(filePath))
                {
                    var lineas = File.ReadAllLines(filePath);
                    foreach (var linea in lineas)
                    {
                        if (!string.IsNullOrWhiteSpace(linea) && (Directory.Exists(linea) || File.Exists(linea)))
                        {
                            rutasValidas.Add(linea);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignorar de forma silenciosa para mantener compatibilidad con el diseño original
            }
            return rutasValidas;
        }

        /// <summary>
        /// Guarda una colección de rutas en el archivo especificado, creando la carpeta contenedora si no existe.
        /// </summary>
        public static void GuardarRutas(string filePath, IEnumerable<string> rutas)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllLines(filePath, rutas);
            }
            catch (Exception)
            {
                // Ignorar de forma silenciosa para mantener compatibilidad con el diseño original
            }
        }
    }
}
