using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// Servicio para obtener letras de canciones desde internet de forma gratuita.
/// Utiliza la API de LRCLIB (lrclib.net).
/// </summary>
public static class LyricsService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    static LyricsService()
    {
        // Configurar User-Agent (buena práctica para APIs)
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ExploradorArchivos-Y2K-Player/1.0");
    }

    /// <summary>
    /// Busca la letra de una canción en internet.
    /// Prioriza letras sincronizadas (LRC) y luego letras planas.
    /// </summary>
    public static async Task<string?> BuscarLetraAsync(string artista, string titulo)
    {
        if (string.IsNullOrWhiteSpace(artista) || string.IsNullOrWhiteSpace(titulo))
            return null;

        try
        {
            string url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artista)}&track_name={Uri.EscapeDataString(titulo)}";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            // 1. Intentar obtener letra sincronizada (más premium)
            if (doc.RootElement.TryGetProperty("syncedLyrics", out var synced) && !string.IsNullOrWhiteSpace(synced.GetString()))
                return synced.GetString();
            
            // 2. Intentar obtener letra plana
            if (doc.RootElement.TryGetProperty("plainLyrics", out var plain) && !string.IsNullOrWhiteSpace(plain.GetString()))
                return plain.GetString();

            return null;
        }
        catch
        {
            // Error de conexión o JSON inválido
            return null;
        }
    }
}
