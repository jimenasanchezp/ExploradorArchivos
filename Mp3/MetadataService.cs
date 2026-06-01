using System;
using System.IO;
using System.Drawing;

namespace ExploradorArchivos.Mp3;

/// <summary>
/// Servicio para editar y persistir metadatos directamente en archivos de audio.
/// Usa TagLibSharp para escribir los cambios en el archivo físico.
/// </summary>
public static class MetadataService
{
    /// <summary>
    /// Guarda los cambios de metadatos en el archivo de audio.
    /// Persiste Título, Artista, Álbum, Año y Foto de Portada.
    /// </summary>
    public static bool GuardarCambios(Cancion cancion)
    {
        try
        {
            using var archivo = TagLib.File.Create(cancion.RutaArchivo);

            archivo.Tag.Title = cancion.Titulo;
            archivo.Tag.Performers = new[] { cancion.Artista };
            archivo.Tag.Album = cancion.Album;
            archivo.Tag.Year = cancion.Anio;

            // Guardar letra si existe
            if (!string.IsNullOrEmpty(cancion.Letra))
                archivo.Tag.Lyrics = cancion.Letra;

            // Guardar portada si existe
            if (cancion.Portada != null)
            {
                using (var ms = new MemoryStream())
                {
                    cancion.Portada.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    var pic = new TagLib.Picture(new TagLib.ByteVector(ms.ToArray()))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = "image/jpeg"
                    };
                    archivo.Tag.Pictures = new TagLib.IPicture[] { pic };
                }
            }
            else
            {
                archivo.Tag.Pictures = Array.Empty<TagLib.IPicture>();
            }

            archivo.Save();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

