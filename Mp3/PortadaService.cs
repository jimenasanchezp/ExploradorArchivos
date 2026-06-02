using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// Servicio para buscar portadas de álbum mediante llamadas HTTP externas.
/// </summary>
public static class PortadaService
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Busca la portada de un álbum en la API pública de iTunes cuando no está disponible de forma local.
    /// </summary>
    /// <param name="artista">Nombre del artista intérprete.</param>
    /// <param name="album">Nombre del álbum musical.</param>
    /// <returns>La portada descargada como System.Drawing.Image si se encuentra; de lo contrario, null.</returns>
    public static async Task<Image?> BuscarPortadaEnWeb(string artista, string album)
    {
        try
        {
            string termino = $"{artista} {album}".Trim();
            if (string.IsNullOrWhiteSpace(termino) || termino == "Desconocido Desconocido")
            {
                return null;
            }

            string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(termino)}&entity=album&limit=1";

            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0)
            {
                return null;
            }

            string? artworkUrl = results[0].GetProperty("artworkUrl100").GetString();
            if (artworkUrl == null)
            {
                return null;
            }
            
            // iTunes retorna miniaturas de 100x100 por defecto. Cambiamos a 600x600 para obtener mayor calidad visual.
            artworkUrl = artworkUrl.Replace("100x100", "600x600");

            var imageBytes = await _httpClient.GetByteArrayAsync(artworkUrl);
            using var ms = new MemoryStream(imageBytes);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }
}
