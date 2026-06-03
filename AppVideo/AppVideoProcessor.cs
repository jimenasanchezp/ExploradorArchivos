using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploradorArchivos.AppVideo;

/// <summary>
/// Proporciona envolturas (wrappers) asíncronas para ejecutar comandos de <c>FFmpeg</c>.
/// Permite recortar, aplicar filtros y extraer audio de manera eficiente usando procesos nativos.
/// </summary>
public static class AppVideoProcessor
{
    private static string FfmpegPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

    /// <summary>
    /// Recorta asíncronamente un segmento del video especificado sin recodificar (copia directa del codec).
    /// </summary>
    public static async Task<bool> Recortar(string input, string output, TimeSpan inicio, TimeSpan duracion)
    {
        string arguments = $"-nostdin -ss {inicio:hh\\:mm\\:ss} -t {duracion:hh\\:mm\\:ss} -i \"{input}\" -c copy \"{output}\" -y";
        return await EjecutarComando(arguments);
    }

    /// <summary>
    /// Recodifica el video aplicando el filtro de escala de grises (B&N) con FFmpeg.
    /// </summary>
    public static async Task<bool> AplicarFiltro(string input, string output, string filtro)
    {
        string videoFilter = filtro switch
        {
            "BN" => "colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3",
            _ => ""
        };

        if (string.IsNullOrEmpty(videoFilter)) return false;

        string arguments = $"-nostdin -i \"{input}\" -vf \"{videoFilter}\" -c:a copy \"{output}\" -y";
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
        string arguments = $"-nostdin -i \"{inputAvi}\" -vcodec libx264 -pix_fmt yuv420p -preset ultrafast -crf 23 -an \"{outputMp4}\" -y";
        return await EjecutarComando(arguments);
    }

    /// <summary>
    /// Extrae únicamente la pista de audio de un video y la exporta (generalmente como MP3/AAC) a 192k.
    /// </summary>
    public static async Task<bool> ExtraerAudio(string input, string output)
    {
        string arguments = $"-nostdin -i \"{input}\" -vn -ab 192k -ar 44100 -y \"{output}\"";
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

            // Leer stderr de forma asíncrona para evitar que el búfer se llene y bloquee el proceso
            string errors = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"FFmpeg falló con código {process.ExitCode}. Detalle: {errors}");
            }
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al ejecutar FFmpeg: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Obtiene información básica del archivo y carga las coordenadas GPS desde el propio archivo o de un archivo JSON "companion" (.meta.json).
    /// </summary>
    public static AppVideoMetadata ObtenerMetadataManual(string ruta)
    {
        var info = new FileInfo(ruta);
        var metadata = new AppVideoMetadata
        {
            RutaArchivo = ruta,
            Nombre = info.Name,
            Extension = info.Extension,
            TamanoBytes = info.Length,
            Duracion = TimeSpan.Zero,
            Resolucion = "Desconocida",
            Codec = "Desconocido"
        };

        // 1. Extraer metadatos técnicos con TagLibSharp
        try
        {
            using var file = TagLib.File.Create(ruta);
            if (file.Properties != null)
            {
                metadata.Duracion = file.Properties.Duration;
                if (file.Properties.VideoWidth > 0 && file.Properties.VideoHeight > 0)
                {
                    metadata.Resolucion = $"{file.Properties.VideoWidth}x{file.Properties.VideoHeight}";
                }

                if (file.Properties.Codecs != null)
                {
                    var codec = System.Linq.Enumerable.FirstOrDefault(file.Properties.Codecs, c => c != null && (c.MediaTypes & TagLib.MediaTypes.Video) != 0);
                    if (codec != null)
                    {
                        metadata.Codec = codec.Description ?? "Desconocido";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al extraer metadata técnica con TagLib: {ex.Message}");
        }

        // 2. Intentar extraer geolocalización (GPS) directamente desde el archivo de video (box ©xyz)
        var (gpsLat, gpsLon) = ExtractGpsFromMp4(ruta);
        if (gpsLat.HasValue && gpsLon.HasValue)
        {
            metadata.Latitud = gpsLat;
            metadata.Longitud = gpsLon;
        }
        else
        {
            // 3. Fallback: Cargar desde archivo companion JSON si existe
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
        }

        return metadata;
    }

    private static int ReadInt32BE(Stream stream)
    {
        byte[] buf = new byte[4];
        int read = stream.Read(buf, 0, 4);
        if (read < 4) return 0;
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(buf);
        }
        return BitConverter.ToInt32(buf, 0);
    }

    private static (double? lat, double? lon) ExtractGpsFromMp4(string filePath)
    {
        try
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // 1. Intentamos extraer via ©xyz (típico de Android y algunos iOS)
                var (lat, lon) = ExtractGpsFromLegacyXyz(stream);
                if (lat.HasValue && lon.HasValue)
                {
                    return (lat, lon);
                }

                // 2. Si falla, intentamos extraer via Apple QuickTime metadata (keys/ilst)
                return ExtractGpsFromAppleMetadata(stream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al extraer GPS del MP4: {ex.Message}");
        }
        return (null, null);
    }

    private static (double? lat, double? lon) ExtractGpsFromLegacyXyz(FileStream stream)
    {
        try
        {
            // Buscamos el patrón binario: 0xA9, 0x78, 0x79, 0x7A (©xyz)
            byte[] pattern = new byte[] { 0xA9, 0x78, 0x79, 0x7A };
            long pos = FindPattern(stream, pattern);
            if (pos != -1)
            {
                // Encontrado ©xyz, leemos los siguientes 64 bytes para extraer la cadena en formato ISO 6709
                stream.Position = pos + 4;
                byte[] buffer = new byte[64];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                string text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                // Expresión regular para detectar coordenadas ISO 6709 (ejemplo: +37.7749-122.4194/ o +41.3758+002.1492/)
                var match = System.Text.RegularExpressions.Regex.Match(text, @"([+-]\d+(?:\.\d+)?)([+-]\d+(?:\.\d+)?)");
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    {
                        return (lat, lon);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al extraer GPS legacy ©xyz: {ex.Message}");
        }
        return (null, null);
    }

    private static (double? lat, double? lon) ExtractGpsFromAppleMetadata(FileStream stream)
    {
        try
        {
            // 1. Find the key string "com.apple.quicktime.location.ISO6709"
            byte[] keyPattern = System.Text.Encoding.UTF8.GetBytes("com.apple.quicktime.location.ISO6709");
            long keyStringPos = FindPattern(stream, keyPattern);
            if (keyStringPos == -1)
            {
                return (null, null);
            }

            // 2. Find the "keys" atom that contains this key string by searching backwards (64KB window).
            byte[] keysSignature = new byte[] { 0x6B, 0x65, 0x79, 0x73 }; // "keys"
            long keysPos = FindPatternBackwards(stream, keysSignature, keyStringPos);
            if (keysPos == -1)
            {
                return (null, null);
            }

            // Read the keys atom size (4 bytes before "keys")
            stream.Position = keysPos - 4;
            int keysSize = ReadInt32BE(stream);
            long keysStart = keysPos - 4;
            long keysEnd = keysStart + keysSize;

            // Read entry count
            stream.Position = keysPos + 8; // skip "keys" (4), version/flags (4)
            int entryCount = ReadInt32BE(stream);

            int targetIndex = -1;
            // Iterate over key entries to find com.apple.quicktime.location.ISO6709
            for (int i = 1; i <= entryCount; i++)
            {
                if (stream.Position >= keysEnd) break;
                int entrySize = ReadInt32BE(stream);
                int entryNamespace = ReadInt32BE(stream);
                if (entrySize < 8) break; // invalid entry
                
                byte[] keyBytes = new byte[entrySize - 8];
                int read = stream.Read(keyBytes, 0, keyBytes.Length);
                string keyName = System.Text.Encoding.UTF8.GetString(keyBytes, 0, read);
                if (keyName == "com.apple.quicktime.location.ISO6709")
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                return (null, null);
            }

            // 3. Find the "ilst" atom (should be close after keysPos)
            byte[] ilstSignature = new byte[] { 0x69, 0x6C, 0x73, 0x74 }; // "ilst"
            long ilstPos = FindPatternInRange(stream, ilstSignature, keysPos, Math.Min(stream.Length, keysPos + 65536));
            if (ilstPos == -1)
            {
                return (null, null);
            }

            // Read the ilst atom size
            stream.Position = ilstPos - 4;
            int ilstSize = ReadInt32BE(stream);
            long ilstStart = ilstPos - 4;
            long ilstEnd = ilstStart + ilstSize;

            // Iterate over child atoms in ilst to find the one matching targetIndex
            stream.Position = ilstPos + 4; // skip "ilst" (4)
            while (stream.Position < ilstEnd - 8)
            {
                long childStart = stream.Position;
                int childSize = ReadInt32BE(stream);
                int childType = ReadInt32BE(stream); // this is the key index (1-based)
                if (childSize <= 0) break; // prevent infinite loop on malformed files

                if (childType == targetIndex)
                {
                    // Found the child atom! Let's parse the "data" atom inside it.
                    byte[] dataSignature = new byte[] { 0x64, 0x61, 0x74, 0x61 }; // "data"
                    long dataPos = FindPatternInRange(stream, dataSignature, stream.Position, childStart + childSize);
                    if (dataPos != -1)
                    {
                        stream.Position = dataPos - 4;
                        int dataSize = ReadInt32BE(stream);
                        // Skip "data" (4) + flags/type indicator (4) + locale (4) = 12 bytes
                        stream.Position = dataPos + 12;
                        int valueLen = dataSize - 16;
                        if (valueLen > 0 && valueLen < 1000)
                        {
                            byte[] valBytes = new byte[valueLen];
                            int read = stream.Read(valBytes, 0, valBytes.Length);
                            string valStr = System.Text.Encoding.UTF8.GetString(valBytes, 0, read);
                            
                            // Use regex to parse ISO 6709 coordinates
                            var match = System.Text.RegularExpressions.Regex.Match(valStr, @"([+-]\d+(?:\.\d+)?)([+-]\d+(?:\.\d+)?)");
                            if (match.Success)
                            {
                                if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                                    double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                                {
                                    return (lat, lon);
                                }
                            }
                        }
                    }
                    break;
                }
                
                // Skip to the next child atom
                stream.Position = childStart + childSize;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing Apple QuickTime metadata: {ex.Message}");
        }
        return (null, null);
    }

    private static long FindPatternBackwards(FileStream stream, byte[] pattern, long startFrom)
    {
        int patternLen = pattern.Length;
        long searchStart = Math.Max(0, startFrom - 65536);
        int bytesToRead = (int)(startFrom - searchStart);
        if (bytesToRead < patternLen) return -1;

        byte[] buffer = new byte[bytesToRead];
        stream.Position = searchStart;
        int bytesRead = stream.Read(buffer, 0, bytesToRead);

        for (int i = bytesRead - patternLen; i >= 0; i--)
        {
            bool match = true;
            for (int j = 0; j < patternLen; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return searchStart + i;
            }
        }
        return -1;
    }

    private static long FindPattern(FileStream stream, byte[] pattern)
    {
        // 1. Buscamos en el inicio (primeros 5 MB)
        long pos = FindPatternInRange(stream, pattern, 0, Math.Min(stream.Length, 5 * 1024 * 1024));
        if (pos != -1) return pos;

        // 2. Buscamos en el final (últimos 5 MB) si el archivo es mayor a 5 MB
        if (stream.Length > 5 * 1024 * 1024)
        {
            long startPos = Math.Max(0, stream.Length - 5 * 1024 * 1024);
            pos = FindPatternInRange(stream, pattern, startPos, stream.Length);
            if (pos != -1) return pos;
        }

        return -1;
    }

    private static long FindPatternInRange(FileStream stream, byte[] pattern, long start, long end)
    {
        int patternLen = pattern.Length;
        byte[] buffer = new byte[4096];
        long streamPos = start;
        
        while (streamPos < end)
        {
            stream.Position = streamPos;
            int bytesToRead = (int)Math.Min(buffer.Length, end - streamPos);
            if (bytesToRead < patternLen) break;

            int bytesRead = stream.Read(buffer, 0, bytesToRead);
            if (bytesRead < patternLen) break;
            
            for (int i = 0; i <= bytesRead - patternLen; i++)
            {
                bool match = true;
                for (int j = 0; j < patternLen; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return streamPos + i;
                }
            }
            streamPos += bytesRead - patternLen + 1;
        }
        return -1;
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
