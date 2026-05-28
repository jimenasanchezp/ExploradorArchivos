using System;

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
    public static string GenerarMapaHtml(double lat, double lon)
    {
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
                var map = L.map('map').setView([{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}], 13);
                L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                    attribution: '© OpenStreetMap'
                }}).addTo(map);
                L.marker([{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}]).addTo(map)
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
    public static string GenerarMapaPickerHtml()
    {
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
                var map = L.map('map').setView([0, 0], 2);
                L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                    attribution: '© OpenStreetMap'
                }}).addTo(map);

                var marker;

                // Intentar geolocalizar al usuario como referencia inicial
                map.locate({{setView: true, maxZoom: 16}});

                function onLocationFound(e) {{
                    if (marker) map.removeLayer(marker);
                    marker = L.marker(e.latlng).addTo(map).bindPopup('Tu ubicación aproximada').openPopup();
                    enviarCoordenadas(e.latlng.lat, e.latlng.lng);
                }}
                
                function onLocationError(e) {{
                    console.log('Error de geolocalización: ' + e.message);
                }}

                map.on('locationfound', onLocationFound);
                map.on('locationerror', onLocationError);

                // Permitir clic para cambiar ubicación
                map.on('click', function(e) {{
                    if (marker) map.removeLayer(marker);
                    marker = L.marker(e.latlng).addTo(map).bindPopup('Nueva ubicación seleccionada').openPopup();
                    enviarCoordenadas(e.latlng.lat, e.latlng.lng);
                }});

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
