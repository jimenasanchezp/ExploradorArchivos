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

        // Constructor vacío para creación manual
        public Cancion() { }

        /// <summary>
        /// Constructor principal: extrae etiquetas ID3v2 (Título, Artista, Álbum, Año, Duración) 
        /// con <c>TagLib.File</c> y carga recursos asociados de manera automática.
        /// </summary>
        /// <param name="ruta">Ruta física del archivo de audio.</param>
        public Cancion(string ruta)
        {
            RutaArchivo = ruta;

            try
            {
                using var archivo = TagLib.File.Create(ruta);

                // Metadatos básicos
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

                // === PORTADA ===
                Portada = CargarPortadaDesdeTag(archivo);
                if (Portada == null)
                    Portada = BuscarPortadaLocal(ruta);

                // === LETRA ===
                Letra = CargarLetra(archivo, ruta);
            }
            catch
            {
                // Si TagLib falla, usamos el nombre del archivo
                Titulo = Path.GetFileNameWithoutExtension(ruta);
            }
        }

        // === MÉTODOS PRIVADOS ===

        /// <summary>
        /// Extrae la portada embebida en el tag del archivo de audio.
        /// </summary>
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
            catch { /* Imagen corrupta o formato no soportado */ }
            return null;
        }

        /// <summary>
        /// Busca una imagen de portada en la misma carpeta del archivo de audio.
        /// Busca archivos que contengan 'cover', 'folder', 'front' o 'album' en el nombre.
        /// </summary>
        private static Image? BuscarPortadaLocal(string rutaAudio)
        {
            try
            {
                string? directorio = Path.GetDirectoryName(rutaAudio);
                if (string.IsNullOrEmpty(directorio)) return null;

                string[] extensiones = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                string[] palabrasClave = { "cover", "folder", "front", "album", "artwork", "art" };

                // Buscar archivos de imagen en la carpeta
                var archivosImagen = Directory.GetFiles(directorio)
                    .Where(f => extensiones.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                // Primero buscar por palabras clave en el nombre
                foreach (var img in archivosImagen)
                {
                    string nombre = Path.GetFileNameWithoutExtension(img).ToLower();
                    if (palabrasClave.Any(kw => nombre.Contains(kw)))
                    {
                        using var fs = new FileStream(img, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        return Image.FromStream(fs);
                    }
                }

                // Si no encuentra por nombre, tomar la primera imagen disponible
                if (archivosImagen.Count > 0)
                {
                    using var fs = new FileStream(archivosImagen[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return Image.FromStream(fs);
                }
            }
            catch { /* Sin permisos o imagen corrupta */ }
            return null;
        }

        /// <summary>
        /// Carga la letra de la canción desde el tag USLT o desde un archivo externo .lrc/.txt.
        /// </summary>
        private static string CargarLetra(TagLib.File archivo, string rutaAudio)
        {
            // 1. Intentar tag USLT (Unsynchronized Lyrics)
            if (!string.IsNullOrWhiteSpace(archivo.Tag.Lyrics))
                return archivo.Tag.Lyrics;

            // 2. Buscar archivo .lrc externo
            string rutaLrc = Path.ChangeExtension(rutaAudio, ".lrc");
            if (File.Exists(rutaLrc))
                return File.ReadAllText(rutaLrc);

            // 3. Buscar archivo .txt con el mismo nombre
            string rutaTxt = Path.ChangeExtension(rutaAudio, ".txt");
            if (File.Exists(rutaTxt))
                return File.ReadAllText(rutaTxt);

            return string.Empty;
        }

        // === PROPIEDADES DE AYUDA PARA LA UI ===

        /// <summary>
        /// Texto formateado para mostrar en la UI.
        /// </summary>
        public string InfoFormateada =>
            Anio > 0
                ? $"{Artista}  ·  {Album}  ·  {Anio}"
                : $"{Artista}  ·  {Album}";

        public string DuracionTexto =>
            Duracion.TotalHours >= 1
                ? Duracion.ToString(@"h\:mm\:ss")
                : Duracion.ToString(@"m\:ss");

        public override string ToString() => $"{Artista} - {Titulo}";
    }
}