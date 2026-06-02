using System;
using System.IO;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Servicio encargado del registro de eventos y errores de la aplicación en un archivo de log local.
    /// </summary>
    internal static class LoggerService
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ExploradorArchivos",
            "app_errors.log"
        );

        /// <summary>
        /// Registra una excepción y un mensaje explicativo en el archivo de logs de la aplicación de manera segura.
        /// </summary>
        public static void LogError(string mensaje, Exception ex)
        {
            try
            {
                string? directory = Path.GetDirectoryName(LogFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string entradaLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {mensaje}{Environment.NewLine}" +
                                    $"Excepción: {ex.Message}{Environment.NewLine}" +
                                    $"Detalles: {ex.StackTrace}{Environment.NewLine}" +
                                    $"------------------------------------------------------------------{Environment.NewLine}";

                File.AppendAllText(LogFilePath, entradaLog);
            }
            catch
            {
                // Evitamos que un fallo de escritura de log detenga el flujo de ejecución principal
            }
        }
    }
}
