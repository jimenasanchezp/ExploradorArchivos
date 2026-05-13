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
        return new AppVideoMetadata
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
    }
}
