using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace ExploradorArchivos.AppFoto;

/// <summary>
/// Servicio encargado de extraer metadatos EXIF de imágenes utilizando <c>System.Drawing</c>.
/// Parsea dimensiones, modelo de cámara, fecha de captura y geolocalización (GPS).
/// </summary>
public static class AppFotoExifService
{
    /// <summary>
    /// Lee el archivo de imagen especificado y mapea sus propiedades EXIF a un objeto <see cref="AppFotoMetadata"/>.
    /// </summary>
    /// <param name="ruta">Ruta física de la imagen.</param>
    /// <returns>Modelo poblado con la información extraída de la imagen.</returns>
    public static AppFotoMetadata LeerMetadatos(string ruta)
    {
        var meta = new AppFotoMetadata
        {
            RutaArchivo = ruta,
            Nombre = Path.GetFileName(ruta)
        };

        try
        {
            using (var img = Image.FromFile(ruta))
            {
                meta.Dimensiones = $"{img.Width} x {img.Height}";
                meta.Resolucion = $"{img.HorizontalResolution} dpi";

                // EXIF Property Items
                foreach (var prop in img.PropertyItems)
                {
                    switch (prop.Id)
                    {
                        case 0x0110: // Model
                            meta.ModeloCamara = Encoding.UTF8.GetString(prop.Value).Trim('\0');
                            break;
                        case 0x9003: // DateTimeOriginal
                        case 0x0132: // DateTime
                            string dateStr = Encoding.UTF8.GetString(prop.Value).Trim('\0');
                            if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dt))
                                meta.FechaCaptura = dt;
                            break;
                        case 0x0002: // GPS Latitude
                            meta.Latitud = ParseGpsCoordinate(prop, img.GetPropertyItem(0x0001));
                            break;
                        case 0x0004: // GPS Longitude
                            meta.Longitud = ParseGpsCoordinate(prop, img.GetPropertyItem(0x0003));
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leyendo EXIF: {ex.Message}");
        }

        return meta;
    }

    /// <summary>
    /// Decodifica las coordenadas GPS crudas (grados, minutos y segundos) desde el tag EXIF a un formato decimal estándar.
    /// </summary>
    /// <param name="prop">Tag EXIF correspondiente al valor de longitud/latitud.</param>
    /// <param name="refProp">Tag EXIF correspondiente a la orientación cardinal (N/S o E/W).</param>
    /// <returns>Valor double de la coordenada, o nulo en caso de formato incorrecto.</returns>
    private static double? ParseGpsCoordinate(PropertyItem prop, PropertyItem refProp)
    {
        try
        {
            // EXIF GPS format: 3 rationals (degrees, minutes, seconds)
            // Each rational is 2 uint32 (numerator, denominator)
            uint dNum = BitConverter.ToUInt32(prop.Value, 0);
            uint dDen = BitConverter.ToUInt32(prop.Value, 4);
            uint mNum = BitConverter.ToUInt32(prop.Value, 8);
            uint mDen = BitConverter.ToUInt32(prop.Value, 12);
            uint sNum = BitConverter.ToUInt32(prop.Value, 16);
            uint sDen = BitConverter.ToUInt32(prop.Value, 20);

            double degrees = (double)dNum / dDen;
            double minutes = (double)mNum / mDen;
            double seconds = (double)sNum / sDen;

            double coordinate = degrees + (minutes / 60.0) + (seconds / 3600.0);

            string direction = Encoding.UTF8.GetString(refProp.Value).Trim('\0');
            if (direction == "S" || direction == "W")
                coordinate *= -1;

            return coordinate;
        }
        catch { return null; }
    }
}
