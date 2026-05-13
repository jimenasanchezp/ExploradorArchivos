using System;

namespace ExploradorArchivos.AppFoto;

public static class AppFotoMapService
{
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
}
