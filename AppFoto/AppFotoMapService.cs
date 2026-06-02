using System;
using System.Globalization;

namespace ExploradorArchivos.AppFoto;

/// <summary>
/// Genera plantillas HTML dinámicas para integrar <c>Leaflet.js</c> y mapas de OpenStreetMap.
/// Permite visualizar coordenadas EXIF o interactuar gráficamente para seleccionar una ubicación.
/// </summary>
public static class AppFotoMapService
{
    /// <summary>
    /// Genera código HTML/JS de solo lectura que coloca un marcador en las coordenadas dadas.
    /// Utiliza un estilo incrustado que se ajusta automáticamente al contenedor del <c>WebView2</c>.
    /// </summary>
    /// <param name="lat">Latitud en formato decimal.</param>
    /// <param name="lon">Longitud en formato decimal.</param>
    /// <returns>Una cadena de texto con el documento HTML listo para cargarse en un control WebView2.</returns>
    public static string GenerarMapaHtml(double lat, double lon)
    {
        // Inicialización y Declaración: Representación de coordenadas formateadas en formato invariant (con punto decimal en lugar de coma)
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lonStr = lon.ToString(CultureInfo.InvariantCulture);

        // Operación: Generación del código HTML y JavaScript que inicializa el mapa de Leaflet.js
        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
            <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
            <style>
                body {{ margin: 0; padding: 0; }}
                #map {{ height: 100vh; width: 100%; border-radius: 8px; }}
            </style>
        </head>
        <body>
            <div id='map'></div>
            <script>
                // Inicialización del mapa Leaflet apuntando a las coordenadas y con un nivel de zoom de 13
                var map = L.map('map').setView([{latStr}, {lonStr}], 13);
                
                // Configuración de la capa base de mapa de OpenStreetMap
                L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                    attribution: '© OpenStreetMap'
                }}).addTo(map);
                
                // Creación e inyección del marcador en la posición geográfica de la foto
                L.marker([{latStr}, {lonStr}]).addTo(map)
                    .bindPopup('Ubicación de la foto')
                    .openPopup();
            </script>
        </body>
        </html>";
    }

    /// <summary>
    /// Genera código HTML/JS interactivo para seleccionar una ubicación en el mapa global.
    /// Utiliza <c>window.chrome.webview.postMessage</c> para enviar los datos de vuelta a la aplicación C#.
    /// </summary>
    /// <returns>Una cadena de texto con el mapa interactivo listo para capturar clicks de coordenadas.</returns>
    public static string GenerarMapaPickerHtml()
    {
        // Operación: Retorna el código HTML/JS que habilita la selección de coordenadas mediante eventos de Leaflet
        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
            <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
            <style>
                body {{ margin: 0; padding: 0; }}
                #map {{ height: 100vh; width: 100%; border-radius: 8px; }}
            </style>
        </head>
        <body>
            <div id='map'></div>
            <script>
                // Inicialización global del mapa en el centro del mundo (0, 0) y zoom inicial alejado
                var map = L.map('map').setView([0, 0], 2);
                
                // Configuración de la capa base de OpenStreetMap
                L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                    attribution: '© OpenStreetMap'
                }}).addTo(map);

                // Inicialización y Declaración: Variable de marcador para registrar el punto seleccionado
                var marker;

                // Intentar geolocalizar al usuario como referencia inicial si los permisos lo permiten
                map.locate({{setView: true, maxZoom: 16}});

                // Operación: Evento al encontrar la localización del usuario
                function onLocationFound(e) {{
                    if (marker) map.removeLayer(marker);
                    marker = L.marker(e.latlng).addTo(map).bindPopup('Tu ubicación aproximada').openPopup();
                    enviarCoordenadas(e.latlng.lat, e.latlng.lng);
                }}
                
                // Operación: Evento al fallar la geolocalización
                function onLocationError(e) {{
                    console.log('Error de geolocalización: ' + e.message);
                }}

                // Enlace de los eventos de geolocalización en Leaflet
                map.on('locationfound', onLocationFound);
                map.on('locationerror', onLocationError);

                // Operación: Manejador del evento clic en el mapa para ubicar manualmente el marcador
                map.on('click', function(e) {{
                    if (marker) map.removeLayer(marker);
                    marker = L.marker(e.latlng).addTo(map).bindPopup('Nueva ubicación seleccionada').openPopup();
                    enviarCoordenadas(e.latlng.lat, e.latlng.lng);
                }});

                // Operación: Envía los datos capturados (latitud y longitud) de vuelta al contenedor de C# (WebView2)
                function enviarCoordenadas(lat, lng) {{
                    if (window.chrome && window.chrome.webview) {{
                        window.chrome.webview.postMessage({{ lat: lat, lng: lng }});
                    }}
                }}
            </script>
        </body>
        </html>";
    }
}
