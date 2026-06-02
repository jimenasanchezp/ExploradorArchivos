using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ExploradorArchivos.Mp3
{
    /// <summary>
    /// Modelo enriquecido para representar una pista musical.
    /// Almacena metadatos estándar (ID3v2) y recursos multimedia como la portada y la letra de la canción.
    /// </summary>
    public class Cancion
    {
        // === PROPIEDADES ===
        public string RutaArchivo { get; set; } = string.Empty;
        public string Titulo { get; set; } = "Sin título";
        public string Artista { get; set; } = "Desconocido";
        public string Album { get; set; } = "Desconocido";
        public uint Anio { get; set; }
        public TimeSpan Duracion { get; set; }
        public string Letra { get; set; } = string.Empty;
        public Image? Portada { get; set; }

        // === CONSTRUCTORES ===

        /// <summary>
        /// Constructor vacío por defecto para creación manual o serialización.
        /// </summary>
        public Cancion() { }

        /// <summary>
        /// Constructor principal de la clase.
        /// Extrae las etiquetas ID3v2 (Título, Artista, Álbum, Año, Duración) utilizando TagLib y carga recursos asociados.
        /// </summary>
        /// <param name="ruta">Ruta física del archivo de audio.</param>
        public Cancion(string ruta)
        {
            RutaArchivo = ruta;

            try
            {
                using var archivo = TagLib.File.Create(ruta);

                // Si las etiquetas ID3v2 no tienen título, se usa el nombre del archivo de audio sin extensión
                Titulo = !string.IsNullOrWhiteSpace(archivo.Tag.Title)
                    ? archivo.Tag.Title
                    : Path.GetFileNameWithoutExtension(ruta);

                Artista = !string.IsNullOrWhiteSpace(archivo.Tag.FirstPerformer)
                    ? archivo.Tag.FirstPerformer
                    : "Desconocido";

                Album = !string.IsNullOrWhiteSpace(archivo.Tag.Album)
                    ? archivo.Tag.Album
                    : "Desconocido";

                Anio = archivo.Tag.Year;
                Duracion = archivo.Properties.Duration;

                Portada = CargarPortadaDesdeTag(archivo);
                if (Portada == null)
                {
                    Portada = BuscarPortadaLocal(ruta);
                }

                Letra = CargarLetra(archivo, ruta);
            }
            catch
            {
                // Fallback: si TagLib falla (archivo corrupto/sin tags), se usa el nombre del archivo
                Titulo = Path.GetFileNameWithoutExtension(ruta);
            }
        }

        // === MÉTODOS PRIVADOS ===

        /// <summary>
        /// Extrae la portada embebida en el tag del archivo de audio (por ejemplo, APIC en ID3v2).
        /// </summary>
        /// <param name="archivo">El objeto TagLib.File que representa el archivo de audio abierto.</param>
        /// <returns>La imagen cargada como System.Drawing.Image si existe; de lo contrario, null.</returns>
        private static Image? CargarPortadaDesdeTag(TagLib.File archivo)
        {
            try
            {
                if (archivo.Tag.Pictures != null && archivo.Tag.Pictures.Length > 0)
                {
                    var pictureData = archivo.Tag.Pictures[0].Data.Data;
                    using var ms = new MemoryStream(pictureData);
                    return Image.FromStream(ms);
                }
            }
            catch
            {
                // Ignora excepciones por datos corruptos o formatos de imagen no soportados
            }
            return null;
        }

        /// <summary>
        /// Busca un archivo de imagen de portada en el mismo directorio físico del archivo de audio.
        /// Prioriza archivos cuyos nombres coincidan con palabras clave relacionadas a portadas.
        /// </summary>
        /// <param name="rutaAudio">La ruta de archivo absoluta del audio.</param>
        /// <returns>La imagen cargada de la portada si se encuentra alguna; de lo contrario, null.</returns>
        private static Image? BuscarPortadaLocal(string rutaAudio)
        {
            try
            {
                string? directorio = Path.GetDirectoryName(rutaAudio);
                if (string.IsNullOrEmpty(directorio))
                {
                    return null;
                }

                string[] extensiones = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                string[] palabrasClave = { "cover", "folder", "front", "album", "artwork", "art" };

                // Buscar archivos de imagen válidos en la carpeta
                var archivosImagen = Directory.GetFiles(directorio)
                    .Where(f => extensiones.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                // Intentar buscar archivos de imagen cuyo nombre coincida con palabras clave comunes
                var imagenCoincidente = archivosImagen.FirstOrDefault(img =>
                {
                    string nombre = Path.GetFileNameWithoutExtension(img).ToLower();
                    return palabrasClave.Any(kw => nombre.Contains(kw));
                });

                if (imagenCoincidente != null)
                {
                    using var fs = new FileStream(imagenCoincidente, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return Image.FromStream(fs);
                }

                // Fallback: si no coincide con palabras clave, tomar la primera imagen del directorio
                if (archivosImagen.Count > 0)
                {
                    using var fs = new FileStream(archivosImagen[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return Image.FromStream(fs);
                }
            }
            catch
            {
                // Ignora excepciones por falta de permisos o imágenes corruptas locales
            }
            return null;
        }

        /// <summary>
        /// Carga la letra de la canción desde los tags internos de metadatos o busca archivos de texto externos (.lrc o .txt).
        /// </summary>
        /// <param name="archivo">El objeto TagLib.File que representa el archivo de audio abierto.</param>
        /// <param name="rutaAudio">La ruta de archivo absoluta del audio.</param>
        /// <returns>La letra de la canción en formato string.</returns>
        private static string CargarLetra(TagLib.File archivo, string rutaAudio)
        {
            // 1. Intento de lectura del tag USLT (Unsynchronized Lyrics) embebido
            if (!string.IsNullOrWhiteSpace(archivo.Tag.Lyrics))
            {
                return archivo.Tag.Lyrics;
            }

            // 2. Intento de búsqueda y lectura de un archivo de letra de tipo LRC (.lrc) con el mismo nombre
            string rutaLrc = Path.ChangeExtension(rutaAudio, ".lrc");
            if (File.Exists(rutaLrc))
            {
                return File.ReadAllText(rutaLrc);
            }

            // 3. Intento de búsqueda y lectura de un archivo de texto estándar (.txt) con el mismo nombre
            string rutaTxt = Path.ChangeExtension(rutaAudio, ".txt");
            if (File.Exists(rutaTxt))
            {
                return File.ReadAllText(rutaTxt);
            }

            return string.Empty;
        }

        // === PROPIEDADES DE AYUDA PARA LA UI ===

        /// <summary>
        /// Retorna una cadena de texto formateada con Artista, Álbum y Año para mostrarse en la interfaz.
        /// </summary>
        public string InfoFormateada =>
            Anio > 0
                ? $"{Artista}  ·  {Album}  ·  {Anio}"
                : $"{Artista}  ·  {Album}";

        /// <summary>
        /// Retorna la duración formateada legible en formato mm:ss o h:mm:ss según corresponda.
        /// </summary>
        public string DuracionTexto =>
            Duracion.TotalHours >= 1
                ? Duracion.ToString(@"h\:mm\:ss")
                : Duracion.ToString(@"m\:ss");

        /// <summary>
        /// Representación textual por defecto de la clase.
        /// </summary>
        /// <returns>Cadena formateada como 'Artista - Título'.</returns>
        public override string ToString() => $"{Artista} - {Titulo}";
    }
}