using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;

namespace ExploradorArchivos.AppFoto;

/// <summary>
/// Servicio encargado de extraer metadatos EXIF de imágenes utilizando <c>System.Drawing</c>.
/// </summary>
public static class AppFotoExifService
{
    /// <summary>
    /// Lee el archivo de imagen especificado y mapea sus propiedades EXIF a un objeto <see cref="AppFotoMetadata"/>.
    /// </summary>
    /// <param name="ruta">Ruta física de la imagen a procesar.</param>
    /// <returns>Un objeto <see cref="AppFotoMetadata"/> con la información técnica y de ubicación extraída.</returns>
    public static AppFotoMetadata LeerMetadatos(string ruta)
    {
        // Inicialización y Declaración: Objeto clave para almacenar los metadatos de salida
        var meta = new AppFotoMetadata
        {
            RutaArchivo = ruta,
            Nombre = Path.GetFileName(ruta)
        };

        try
        {
            // Inicialización y Declaración: Carga del objeto Image desde el archivo de forma segura con using
            using (var img = Image.FromFile(ruta))
            {
                // Operación: Asignación de propiedades físicas de la imagen
                meta.Dimensiones = $"{img.Width} x {img.Height}";
                meta.Resolucion = $"{img.HorizontalResolution} dpi";

                // Inicialización y Operación LINQ: Indexación de propiedades EXIF en un diccionario por su ID
                // Esto optimiza las búsquedas posteriores y evita excepciones costosas de GetPropertyItem
                var properties = img.PropertyItems.ToDictionary(p => p.Id);

                // Operación: Extracción del Modelo de Cámara (Tag EXIF 0x0110)
                if (properties.TryGetValue(0x0110, out var propModel) && propModel.Value != null)
                {
                    meta.ModeloCamara = Encoding.UTF8.GetString(propModel.Value).Trim('\0', ' ');
                }

                // Operación: Extracción de Fecha de Captura (Tag EXIF 0x9003 = DateTimeOriginal, 0x0132 = DateTime)
                if (properties.TryGetValue(0x9003, out var propDate) || properties.TryGetValue(0x0132, out propDate))
                {
                    if (propDate.Value != null)
                    {
                        string dateStr = Encoding.UTF8.GetString(propDate.Value).Trim('\0', ' ');
                        if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            meta.FechaCaptura = dt;
                        }
                    }
                }

                // Operación: Extracción y decodificación de la Latitud GPS (Tag EXIF 0x0002 y su referencia cardinal 0x0001)
                if (properties.TryGetValue(0x0002, out var propLat) && properties.TryGetValue(0x0001, out var propLatRef))
                {
                    meta.Latitud = ParseGpsCoordinate(propLat, propLatRef);
                }

                // Operación: Extracción y decodificación de la Longitud GPS (Tag EXIF 0x0004 y su referencia cardinal 0x0003)
                if (properties.TryGetValue(0x0004, out var propLon) && properties.TryGetValue(0x0003, out var propLonRef))
                {
                    meta.Longitud = ParseGpsCoordinate(propLon, propLonRef);
                }
            }
        }
        catch (Exception ex)
        {
            // Operación: Registro del error en consola en caso de fallo durante la lectura
            Console.WriteLine($"Error leyendo EXIF: {ex.Message}");
        }

        return meta;
    }

    /// <summary>
    /// Decodifica las coordenadas GPS crudas (grados, minutos y segundos) desde el tag EXIF a un formato decimal estándar.
    /// </summary>
    /// <param name="prop">Propiedad EXIF que contiene el valor racional de la coordenada.</param>
    /// <param name="refProp">Propiedad EXIF que contiene el indicador de dirección de referencia cardinal.</param>
    /// <returns>El valor decimal de la coordenada, o <c>null</c> si no se puede procesar o el formato no es válido.</returns>
    private static double? ParseGpsCoordinate(PropertyItem prop, PropertyItem refProp)
    {
        // Validación: Asegurar que los datos de entrada sean válidos y tengan el tamaño mínimo requerido
        if (prop.Value == null || prop.Value.Length < 24 || refProp.Value == null || refProp.Value.Length == 0)
        {
            return null;
        }

        try
        {
            // Inicialización y Declaración: Arreglos/Racionales EXIF GPS estructurados en 3 partes (Grados, Minutos, Segundos)
            // Cada parte se compone de 2 enteros de 32 bits sin signo: [0-3] Numerador, [4-7] Denominador.
            uint dNum = BitConverter.ToUInt32(prop.Value, 0);
            uint dDen = BitConverter.ToUInt32(prop.Value, 4);
            uint mNum = BitConverter.ToUInt32(prop.Value, 8);
            uint mDen = BitConverter.ToUInt32(prop.Value, 12);
            uint sNum = BitConverter.ToUInt32(prop.Value, 16);
            uint sDen = BitConverter.ToUInt32(prop.Value, 20);

            // Validación: Evitar divisiones por cero
            if (dDen == 0 || mDen == 0 || sDen == 0)
            {
                return null;
            }

            // Operación: Conversión matemática de racionales a valores de punto flotante de precisión doble
            double degrees = (double)dNum / dDen;
            double minutes = (double)mNum / mDen;
            double seconds = (double)sNum / sDen;

            // Operación: Cálculo final para consolidar la coordenada decimal (Grados + Minutos/60 + Segundos/3600)
            double coordinate = degrees + (minutes / 60.0) + (seconds / 3600.0);

            // Inicialización y Operación: Ajustar el signo de acuerdo a la dirección cardinal (Sur/Oeste implican valores negativos)
            string direction = Encoding.UTF8.GetString(refProp.Value).Trim('\0', ' ');
            if (direction == "S" || direction == "W")
            {
                coordinate *= -1;
            }

            return coordinate;
        }
        catch
        {
            return null;
        }
    }
}
