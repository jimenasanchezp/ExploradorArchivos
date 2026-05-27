using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploradorArchivos.AppVideo;

public static class AppVideoProcessor
{
    private static string FfmpegPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

    public static async Task<bool> Recortar(string input, string output, TimeSpan inicio, TimeSpan duracion)
    {
        string arguments = $"-ss {inicio:hh\\:mm\\:ss} -t {duracion:hh\\:mm\\:ss} -i \"{input}\" -c copy \"{output}\" -y";
        return await EjecutarComando(arguments);
    }

    public static async Task<bool> AplicarFiltro(string input, string output, string filtro)
    {
        string videoFilter = filtro switch
        {
            "BN" => "colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3",
            "Sepia" => "colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131",
            "Soft" => "hue=s=0.8, curves=m='0/0.1 0.5/0.5 1/0.9'", // Algo similar a Soft/Kawaii
            _ => ""
        };

        if (string.IsNullOrEmpty(videoFilter)) return false;

        string arguments = $"-i \"{input}\" -vf \"{videoFilter}\" -c:a copy \"{output}\" -y";
        return await EjecutarComando(arguments);
    }

    /// <summary>
    /// Convierte un archivo AVI (grabado por la cámara) a MP4/H.264.
    /// Requiere ffmpeg.exe en la carpeta de la aplicación.
    /// </summary>
    public static async Task<bool> ConvertirAviAMp4(string inputAvi, string outputMp4)
    {
        // -vcodec libx264   → codec H.264
        // -pix_fmt yuv420p  → compatibilidad máxima con reproductores
        // -preset ultrafast → velocidad máxima (prioridad sobre tamaño)
        // -crf 23           → calidad razonable
        // -an               → sin audio (la cámara no captura audio en este flujo)
        string arguments = $"-i \"{inputAvi}\" -vcodec libx264 -pix_fmt yuv420p -preset ultrafast -crf 23 -an \"{outputMp4}\" -y";
        return await EjecutarComando(arguments);
    }

    public static async Task<bool> ExtraerAudio(string input, string output)
    {
        string arguments = $"-i \"{input}\" -vn -ab 192k -ar 44100 -y \"{output}\"";
        return await EjecutarComando(arguments);
    }

    private static async Task<bool> EjecutarComando(string arguments)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static AppVideoMetadata ObtenerMetadataManual(string ruta)
    {
        var info = new FileInfo(ruta);
        var metadata = new AppVideoMetadata
        {
            RutaArchivo = ruta,
            Nombre = info.Name,
            Extension = info.Extension,
            TamanoBytes = info.Length,
            // La duración y otros campos técnicos requerirían ffprobe o LibVLC
            Duracion = TimeSpan.Zero,
            Resolucion = "Desconocida",
            Codec = "Desconocido"
        };

        // Cargar desde archivo companion JSON si existe
        string metaPath = ruta + ".meta.json";
        if (File.Exists(metaPath))
        {
            try
            {
                string jsonString = File.ReadAllText(metaPath);
                var savedData = JsonSerializer.Deserialize<AppVideoMetadata>(jsonString);
                if (savedData != null)
                {
                    metadata.Latitud = savedData.Latitud;
                    metadata.Longitud = savedData.Longitud;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar metadatos companion: {ex.Message}");
            }
        }

        return metadata;
    }

    public static void GuardarMetadata(AppVideoMetadata metadata)
    {
        try
        {
            string metaPath = metadata.RutaArchivo + ".meta.json";
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(metadata, options);
            File.WriteAllText(metaPath, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar metadatos companion: {ex.Message}");
        }
    }
}
