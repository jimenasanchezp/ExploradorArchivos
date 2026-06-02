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
    public static async Task IdentificarCoordenadasAsync(IEnumerable<DataItem> items)
    {
        // Contador de peticiones realizadas en el lote actual
        int count = 0;
        
        // Recorrer los elementos recibidos
        foreach (var item in items)
        {
            // Omitir si el registro ya contiene información de latitud o longitud asignada
            if ((item.Latitude != 0 && item.Latitude != null) || (item.Longitude != 0 && item.Longitude != null)) continue;
            // Detener el proceso si alcanzamos el límite de consultas permitidas por lote
            if (count >= MAX_GEOCODE_PER_BATCH) break;

            // Buscar en CamposExtra el primer valor que parezca una ciudad utilizando operadores de LINQ
            string? target = item.CamposExtra
                .Where(kv => _cityKeywords.Any(k => kv.Key.ToLowerInvariant().Contains(k))) // Filtrar por claves con coincidencia de palabra clave
                .Select(kv => kv.Value) // Seleccionar los valores asociados
                .FirstOrDefault(val => !string.IsNullOrEmpty(val) && val.Length >= 3); // Obtener la primera cadena válida con longitud mínima de 3

            // Si no se encuentra un valor específico en CamposExtra, tomar el nombre o la categoría como fallback
            if (string.IsNullOrEmpty(target)) target = item.Nombre ?? item.Categoria;
            // Validar que el término de búsqueda resultante sea útil (mínimo 3 caracteres)
            if (string.IsNullOrEmpty(target) || target.Length < 3) continue;

            // Verificar si el término de búsqueda ya se encuentra registrado en nuestra caché local
            if (_cache.TryGetValue(target, out var cached))
            {
                // Si la caché tiene datos de coordenadas válidos, asignarlos directamente al elemento
                if (cached.HasValue) 
                { 
                    item.Latitude = cached.Value.Lat; 
                    item.Longitude = cached.Value.Lon; 
                }
                // Continuar con el siguiente registro
                continue;
            }

            // Realizar la consulta física de geolocalización a la API remota
            var coords = await FetchCoordsAsync(target);
            // Almacenar el resultado (coordenadas o nulo) en la caché
            _cache[target] = coords;

            // Si se resolvieron coordenadas de forma exitosa
            if (coords != null)
            {
                // Asignar latitud y longitud encontradas
                item.Latitude = coords.Value.Lat;
                item.Longitude = coords.Value.Lon;
                // Incrementar contador de llamadas exitosas en el lote
                count++;
                // Aplicar retardo de 1 segundo para respetar estrictamente las directivas de Nominatim (1 req/sec)
                await Task.Delay(1000);
            }
        }
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
            // Ejecutar la petición HTTP de forma asíncrona
            var response = await _httpClient.GetStringAsync(url);
            // Parsear la respuesta en un árbol JSON estructurado
            var json = JsonDocument.Parse(response);
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
        catch 
        { 
            // Absorber cualquier excepción de red o parseo y retornar nulo
        }

        // Retornar nulo si no se encontraron coordenadas válidas
        return null;
    }
}
