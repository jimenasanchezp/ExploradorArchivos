using System;
using System.Drawing;
using System.IO;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// Servicio para editar y persistir metadatos directamente en archivos de audio.
/// Utiliza la biblioteca TagLib para realizar la escritura física de las etiquetas de audio.
/// </summary>
public static class MetadataService
{
    /// <summary>
    /// Guarda los cambios de metadatos en el archivo de audio.
    /// Persiste Título, Artista, Álbum, Año, Letra e Imagen de Portada.
    /// </summary>
    /// <param name="cancion">El objeto de tipo Cancion cuyos metadatos se van a persistir.</param>
    /// <returns>True si los metadatos se guardaron correctamente; de lo contrario, False.</returns>
    public static bool GuardarCambios(Cancion cancion)
    {
        try
        {
            using var archivo = TagLib.File.Create(cancion.RutaArchivo);

            archivo.Tag.Title = cancion.Titulo;
            archivo.Tag.Performers = new[] { cancion.Artista };
            archivo.Tag.Album = cancion.Album;
            archivo.Tag.Year = cancion.Anio;

            if (!string.IsNullOrEmpty(cancion.Letra))
            {
                archivo.Tag.Lyrics = cancion.Letra;
            }

            if (cancion.Portada != null)
            {
                using var ms = new MemoryStream();
                // Convierte la imagen al formato estándar JPEG para incrustarla en el archivo de audio
                cancion.Portada.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                
                var pic = new TagLib.Picture(new TagLib.ByteVector(ms.ToArray()))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = "image/jpeg"
                };
                
                archivo.Tag.Pictures = new TagLib.IPicture[] { pic };
            }
            else
            {
                archivo.Tag.Pictures = Array.Empty<TagLib.IPicture>();
            }

            archivo.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

