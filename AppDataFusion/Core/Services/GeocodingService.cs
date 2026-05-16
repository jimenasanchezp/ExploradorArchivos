using ExploradorArchivos.AppDataFusion.Models;
using System.Text.Json;
using System.Net.Http.Headers;

namespace ExploradorArchivos.AppDataFusion.Services;

public static class GeocodingService
{
    private static readonly HttpClient _httpClient = new();

    static GeocodingService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DataFusionArena", "1.0"));
    }

    private static readonly Dictionary<string, (double Lat, double Lon)?> _cache = new();
    private const int MAX_GEOCODE_PER_BATCH = 20;

    private static readonly string[] _cityKeywords = { "ciudad", "ciudades", "city", "town", "location", "ubicacion" };

    public static async Task IdentificarCoordenadasAsync(IEnumerable<DataItem> items)
    {
        int count = 0;
        foreach (var item in items)
        {
            if ((item.Latitude != 0 && item.Latitude != null) || (item.Longitude != 0 && item.Longitude != null)) continue;
            if (count >= MAX_GEOCODE_PER_BATCH) break;

            // Priority: Extra fields that look like cities
            string target = null;
            foreach (var kv in item.CamposExtra)
            {
                if (_cityKeywords.Any(k => kv.Key.ToLower().Contains(k)))
                {
                    target = kv.Value;
                    if (!string.IsNullOrEmpty(target) && target.Length >= 3) break;
                    target = null;
                }
            }

            if (string.IsNullOrEmpty(target)) target = item.Nombre ?? item.Categoria;
            if (string.IsNullOrEmpty(target) || target.Length < 3) continue;

            if (_cache.TryGetValue(target, out var cached))
            {
                if (cached.HasValue) { item.Latitude = cached.Value.Lat; item.Longitude = cached.Value.Lon; }
                continue;
            }

            var coords = await FetchCoordsAsync(target);
            _cache[target] = coords;

            if (coords != null)
            {
                item.Latitude = coords.Value.Lat;
                item.Longitude = coords.Value.Lon;
                count++;
                await Task.Delay(1000); // Nominatim policy: 1 req/sec
            }
        }
    }

    private static async Task<(double Lat, double Lon)?> FetchCoordsAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length < 3) return null;

        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(input)}&format=json&limit=1";
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (double.TryParse(first.GetProperty("lat").GetString(), out double lat) &&
                    double.TryParse(first.GetProperty("lon").GetString(), out double lon))
                {
                    return (lat, lon);
                }
            }
        }
        catch { }

        return null;
    }
}

