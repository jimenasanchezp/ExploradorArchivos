using ExploradorArchivos.AppDataFusion.Models;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace ExploradorArchivos.AppDataFusion.Services;

/// <summary>
/// Servicio de geocodificación que interactúa con la API pública de Nominatim (OpenStreetMap)
/// para resolver asíncronamente nombres de ciudades/ubicaciones en coordenadas (latitud y longitud).
/// Implementa políticas de caché y throttle (1 req/sec) para no saturar la API.
/// </summary>
public static class GeocodingService
{
    // Cliente HTTP de uso único y compartido para optimizar la reutilización de conexiones de red
    private static readonly HttpClient _httpClient = new();

    // Constructor estático para inicializar cabeceras globales requeridas
    static GeocodingService()
    {
        // Agregar cabecera User-Agent identificativa para cumplir con las políticas de uso de Nominatim
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DataFusionArena", "1.0"));
    }

    // Caché en memoria para guardar las búsquedas previas de coordenadas y evitar llamadas duplicadas a la API
    private static readonly Dictionary<string, (double Lat, double Lon)?> _cache = new();
    // Límite máximo de peticiones de geolocalización por lote de procesamiento
    private const int MAX_GEOCODE_PER_BATCH = 20;

    // Palabras clave en español e inglés utilizadas para identificar campos que contengan información de la ubicación/ciudad
    private static readonly string[] _cityKeywords = { "ciudad", "ciudades", "city", "town", "location", "ubicacion" };

    /// <summary>
    /// Intenta resolver y asignar las coordenadas para una lista de <c>DataItem</c>,
    /// priorizando campos que contengan palabras clave como "ciudad" o "location".
    /// </summary>
    public static async Task<(int Processed, int Geocoded, string Message)> IdentificarCoordenadasAsync(
        IEnumerable<DataItem> items, 
        Action<string>? progressCallback = null)
    {
        int queryCount = 0;
        int geocodedCount = 0;

        // Primero seleccionamos los elementos que realmente necesitan procesamiento
        var itemsToProcess = new List<(DataItem Item, string Target)>();
        foreach (var item in items)
        {
            if ((item.Latitude != 0 && item.Latitude != null) || (item.Longitude != 0 && item.Longitude != null)) continue;

            // Buscar en CamposExtra el primer valor que parezca una ciudad utilizando operadores de LINQ
            string? target = item.CamposExtra
                .Where(kv => _cityKeywords.Any(k => kv.Key.ToLowerInvariant().Contains(k)))
                .Select(kv => kv.Value)
                .FirstOrDefault(val => !string.IsNullOrEmpty(val) && val.Length >= 3);

            if (string.IsNullOrEmpty(target)) target = item.Nombre ?? item.Categoria;
            if (string.IsNullOrEmpty(target) || target.Length < 3) continue;

            itemsToProcess.Add((item, target));
        }

        if (itemsToProcess.Count == 0)
        {
            return (0, 0, "No hay nuevos registros válidos que requieran geocodificación.");
        }

        progressCallback?.Invoke($"Iniciando geocodificación (lote máx. {MAX_GEOCODE_PER_BATCH} consultas)...");

        foreach (var (item, target) in itemsToProcess)
        {
            if (queryCount >= MAX_GEOCODE_PER_BATCH) break;

            // Verificar si el término de búsqueda ya se encuentra registrado en nuestra caché local
            if (_cache.TryGetValue(target, out var cached))
            {
                if (cached.HasValue) 
                { 
                    item.Latitude = cached.Value.Lat; 
                    item.Longitude = cached.Value.Lon; 
                    geocodedCount++;
                }
                continue;
            }

            progressCallback?.Invoke($"Consultando coordenadas para '{target}' ({queryCount + 1}/{MAX_GEOCODE_PER_BATCH})...");

            try
            {
                // Realizar la consulta física de geolocalización a la API remota
                var coords = await FetchCoordsAsync(target);
                
                // Almacenar el resultado (coordenadas o nulo) en la caché
                _cache[target] = coords;
                
                // Incrementar contador de llamadas en el lote por cada petición física de red realizada
                queryCount++;

                if (coords != null)
                {
                    // Asignar latitud y longitud encontradas
                    item.Latitude = coords.Value.Lat;
                    item.Longitude = coords.Value.Lon;
                    geocodedCount++;
                }

                // Aplicar retardo de 1 segundo para respetar estrictamente las directivas de Nominatim (1 req/sec)
                // Se hace por cada consulta de red para evitar ser penalizado/bloqueado.
                await Task.Delay(1000);
            }
            catch (GeocodingBlockedException ex)
            {
                progressCallback?.Invoke($"Límite excedido o bloqueo: {ex.Message}");
                return (queryCount, geocodedCount, $"La API de Nominatim ha bloqueado o limitado temporalmente las peticiones (HTTP 429/403). Se abortó el lote actual.");
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Error al consultar '{target}': {ex.Message}");
                return (queryCount, geocodedCount, $"Error inesperado al geocodificar '{target}': {ex.Message}. Se detuvo el lote.");
            }
        }

        return (queryCount, geocodedCount, $"Lote de geocodificación finalizado. Se realizaron {queryCount} consultas de red, obteniendo {geocodedCount} coordenadas exitosas.");
    }

    /// <summary>
    /// Consulta el servicio web de Nominatim para obtener la latitud y longitud asociadas a una cadena de ubicación.
    /// </summary>
    private static async Task<(double Lat, double Lon)?> FetchCoordsAsync(string input)
    {
        // Validar que la cadena de consulta sea apta
        if (string.IsNullOrWhiteSpace(input) || input.Length < 3) return null;

        try
        {
            // Construir URL de consulta escapando el texto de forma segura y limitando a 1 resultado en formato JSON
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(input)}&format=json&limit=1";
            
            // Usar GetAsync para poder verificar los códigos de respuesta del servidor (especialmente 429/403)
            using var response = await _httpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new GeocodingBlockedException("La API de Nominatim ha bloqueado las peticiones temporalmente (HTTP 429/403).");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            
            // Parsear la respuesta en un árbol JSON estructurado
            var json = JsonDocument.Parse(responseBody);
            // Obtener el nodo raíz del JSON
            var root = json.RootElement;

            // Validar que la respuesta sea un arreglo JSON con al menos un resultado
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                // Extraer el primer resultado coincidente
                var first = root[0];
                // Intentar parsear los atributos 'lat' y 'lon' de texto a valores de punto flotante de doble precisión (double)
                if (double.TryParse(first.GetProperty("lat").GetString(), out double lat) &&
                    double.TryParse(first.GetProperty("lon").GetString(), out double lon))
                {
                    // Devolver las coordenadas mapeadas en una tupla
                    return (lat, lon);
                }
            }
        }
        catch (GeocodingBlockedException)
        {
            throw; // Propagar para que el llamador aborte el bucle
        }
        catch 
        { 
            // Absorber cualquier otra excepción de red o parseo y retornar nulo
        }

        // Retornar nulo si no se encontraron coordenadas válidas
        return null;
    }
}

/// <summary>
/// Excepción personalizada para representar bloqueos o límites de velocidad (403/429) por parte de Nominatim.
/// </summary>
public class GeocodingBlockedException : Exception
{
    public GeocodingBlockedException(string message) : base(message) { }
}
