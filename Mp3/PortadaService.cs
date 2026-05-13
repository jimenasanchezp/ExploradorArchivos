using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// Servicio para buscar portadas de álbum con fallback en cascada:
/// 1. Tag embebido (ya manejado en Cancion.cs)
/// 2. Imagen local en la carpeta
/// 3. API pública de iTunes (sin autenticación)
/// </summary>
public static class PortadaService
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Busca una portada en la API de iTunes cuando no hay portada local.
    /// Retorna null si no encuentra nada.
    /// </summary>
    public static async Task<Image?> BuscarPortadaEnWeb(string artista, string album)
    {
        try
        {
            // Construir término de búsqueda
            string termino = $"{artista} {album}".Trim();
            if (string.IsNullOrWhiteSpace(termino) || termino == "Desconocido Desconocido")
                return null;

            string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(termino)}&entity=album&limit=1";

            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0) return null;

            // Obtener la URL del artwork y escalar a 600x600
            // PortadaService.cs: Escalado de imagen para alta calidad
            string? artworkUrl = results[0].GetProperty("artworkUrl100").GetString();
            if (artworkUrl == null) return null;
            artworkUrl = artworkUrl.Replace("100x100", "600x600"); // Mejora la estética visual

            // Descargar la imagen
            var imageBytes = await _httpClient.GetByteArrayAsync(artworkUrl);
            using var ms = new MemoryStream(imageBytes);
            return Image.FromStream(ms);
        }
        catch
        {
            // Sin conexión o API no disponible
            return null;
        }
    }
}
