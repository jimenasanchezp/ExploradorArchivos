# Documentación de Arquitectura: Explorador de Archivos (Kawaii 95 / Soft Retro)

Este documento detalla la arquitectura técnica completa, las estructuras de datos, cómo operan los motores internos y cómo está estructurada cada pieza de la visualización y las aplicaciones modulares integradas.

---

## 1. Arquitectura Completa y Patrones
La aplicación sigue un modelo orientado a servicios y separación de capas dentro de WinForms.

*   **Capa de Datos (Models):** Clases simples (POCOs) que representan entidades (Archivos, Canciones).
*   **Capa de Lógica (Services):** Clases estáticas o asíncronas (`FileService`, `CsvIndexer`) que se encargan de las llamadas al sistema operativo (I/O, P/Invoke) de forma independiente de la interfaz gráfica.
*   **Capa de Presentación (UI/Form1):** Formularios que consumen los servicios e inyectan los datos en controles personalizados (`TreeView`, `ListView`) dibujados con `ThemeRenderer`.

---

## 2. Estructuras de Datos (Modelos)
La estructura central que permite que todo el explorador funcione es la clase `FileSystemItem`. En lugar de manejar diccionarios complejos para la vista general, el motor utiliza **Listas Genéricas (`List<FileSystemItem>`)** que son muy rápidas para iterar y enlazar a la interfaz.

```csharp
// Models/FileSystemItem.cs
public class FileSystemItem
{
    public string Nombre { get; set; } = string.Empty;
    public string RutaCompleta { get; set; } = string.Empty;
    public bool EsCarpeta { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string TamanoTexto { get; set; } = string.Empty;
    public DateTime FechaModificacion { get; set; }

    // Propiedad calculada: Funciona como un índice o "categorizador" al vuelo
    public string CategoriaVisual
    {
        get
        {
            if (EsCarpeta) return "Carpetas";
            string ext = Path.GetExtension(Nombre).ToLower();
            if (ext is ".jpg" or ".png" or ".webp") return "Imágenes";
            if (ext is ".mp3" or ".wav") return "Audio";
            if (ext is ".mp4" or ".mkv") return "Video";
            return "Otros";
        }
    }
}
```

---

## 3. Navegación, Interacción y Búsqueda

El formulario principal (`Form1`) está dividido en clases parciales para mantener el código limpio:

### El Enrutador y Vistas Especiales (`Form1.Navegacion.cs`)
*   Crea **"Vistas Virtuales"** como "Inicio" (Dashboard con archivos recientes usando LINQ: `.OrderByDescending(f => f.LastWriteTime).Take(15)`) y "Este Equipo" (leyendo `DriveInfo.GetDrives()`).
*   **Breadcrumbs:** Corta el string de la ruta por las barras y crea botones (`Button`) dinámicamente. Si haces clic en un botón del historial, te regresa a esa carpeta instantáneamente.

### Interacción y Búsqueda (`Form1.Interaccion.cs`)
*   **El "Router" de Archivos (Doble Clic):** Al dar doble clic a un archivo, lee la extensión e instancia los módulos correspondientes (`AppFotoForm`, `MusicPlayerForm`, etc.).
*   **Búsqueda "En Memoria":** No vuelve a leer el disco al buscar. Filtra la lista en RAM usando `.Where(x => x.Nombre.ToLower().Contains(filtro))`, haciendo la búsqueda instantánea sin lag.
*   **Quick Look (macOS style):** Presionar la barra espaciadora lanza un visor flotante ultra rápido centrado en pantalla.

---

## 4. El Motor de la Papelera de Reciclaje (P/Invoke)
Para enviar archivos a la Papelera de Reciclaje (y no borrarlos permanentemente), C# puro no tiene un método directo fácil. Por lo tanto, en `FileService.cs` se implementó una llamada de bajo nivel a la API nativa de Windows (P/Invoke) utilizando `shell32.dll`.

```csharp
// Services/FileService.cs
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
struct SHFILEOPSTRUCT {
    public IntPtr hwnd;
    public uint wFunc;
    public string pFrom;
    // ... otros campos
    public ushort fFlags;
}

[DllImport("shell32.dll", CharSet = CharSet.Auto)]
static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

private const uint FO_DELETE = 0x0003;
private const ushort FOF_ALLOWUNDO = 0x0040; // <-- La bandera mágica que lo manda a la papelera

public static bool EnviarAPapelera(string ruta)
{
    SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT {
        wFunc = FO_DELETE,
        pFrom = ruta + '\0' + '\0', // Requiere doble terminación nula en C
        fFlags = FOF_ALLOWUNDO | 0x0010 // 0x0010 evita que te pregunte "¿Estás seguro?"
    };
    return SHFileOperation(ref shf) == 0;
}
```

---

## 5. Índices y Motor de Búsqueda (CSV Indexer)
En lugar de tener una base de datos pesada como SQL, el programa cuenta con un motor de indexación asíncrono (`CsvIndexer.cs`). Este motor recorre de forma recursiva (carpeta por carpeta) todo tu disco duro y exporta las métricas a un archivo CSV. Esto sirve para generar "mapas" del disco rápidamente sin trabar la interfaz.

```csharp
// Services/CsvIndexer.cs
public static async Task ExportarAsync(string rootPath, string outputFile, IProgress<string>? progress, CancellationToken token)
{
    await Task.Run(() => {
        using StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8);
        writer.WriteLine("\"Ruta Completa\",\"Nombre Carpeta\",\"Carpetas\",\"Archivos Total\",\"Último Acceso\"");
        
        // Llamada recursiva
        ExportarRecursivo(rootPath, writer, progress, token); 
    }, token);
}
```
*Utiliza `CancellationToken` para que puedas detener la búsqueda a la mitad, y `IProgress<string>` para reportar a la UI qué carpeta está escaneando.*

---

## 6. Los Módulos de Aplicaciones Integrados (Sub-Apps)

### A. Reproductor Premium: NAudio y TagLib# (`Mp3/`)
*   **El Motor de Audio (NAudio):** `GestorReproduccion` utiliza `AudioFileReader` y `WaveOutEvent` para reproducir el MP3 en un hilo paralelo de hardware. Permite alterar el volumen matemáticamente y hacer "Seek" con precisión cambiando el `CurrentTime`.
*   **Edición de Etiquetas (TagLib#):** `MetadataService` es capaz de abrir la cabecera binaria del MP3, inyectar datos (Título, Artista, Letra de la canción) y volverla a sellar sin romper el audio.

### B. Gestor de Video: Procesamiento con FFmpeg (`AppVideo/`)
Para procesar video de forma nativa sin requerir licencias de códecs complejos, la app utiliza un enfoque CLI llamando a **`ffmpeg.exe`** en segundo plano (ocultando la ventana de terminal `cmd` con `CreateNoWindow = true`).
*   **Recorte rápido:** Pasa el comando `-c copy` para cortar el video al instante sin recodificar.
*   **Filtros de Color:** Inyecta filtros Kawaii bajando la saturación matemática: `"hue=s=0.8, curves=m='0/0.1 0.5/0.5 1/0.9'"`.
*   **Extracción de Audio:** Remueve la pista de video (`-vn`) y exporta un archivo en calidad 192k.

### C. Editor de Imágenes y GPS (`AppFoto/`)
Modifica los bytes internos de la imagen para inyectar datos GPS (`EXIF`). Utiliza `System.Drawing.Imaging` y clases como `PropertyItem`. Convierte coordenadas de grados decimales a formato DMS (Grados, Minutos, Segundos) usando matemáticas fraccionarias puras y escribirlas en las propiedades hexadecimales (Ej: `0x0002` para Latitud) del archivo para su almacenamiento permanente.

---

## 7. Visualización y Renderizado (ThemeRenderer)
El núcleo gráfico de tu explorador reside en la carpeta `UI/`. Para lograr el estilo "Kawaii 95" (colores pastel, morado, botones 3D), la aplicación interrumpe el dibujado normal de Windows e inyecta su propio código de pintura (`OwnerDraw`).

*   **ListViewSorter:** Implementa la interfaz `IComparer`. Es el motor que entra en acción cuando haces clic en la columna "Tamaño" o "Fecha". Convierte los strings de "10 MB" a bytes reales (`double`) matemáticamente para ordenarlos correctamente (y que 2 MB no aparezca antes que 10 KB).
*   **ThemeRenderer:** Es una clase estática de utilidades que usa el objeto `Graphics`.

```csharp
// Ejemplo conceptual de cómo el ThemeRenderer dibuja las carpetas de distintos colores
public static void DrawListViewItem(DrawListViewItemEventArgs e, FileSystemItem item)
{
    // Diccionario mental de colores según categoría
    Color bgColor = Color.White;
    if (item.CategoriaVisual == "Audio") bgColor = Color.Lavender; // Morado pastel
    if (item.CategoriaVisual == "Video") bgColor = Color.MistyRose; // Rosa pastel
    
    // Dibuja el fondo
    using (SolidBrush brush = new SolidBrush(bgColor))
    {
        e.Graphics.FillRectangle(brush, e.Bounds);
    }
    // Dibuja el texto con fuente Retro
    e.Graphics.DrawString(item.Nombre, new Font("MS Sans Serif", 8), Brushes.Black, e.Bounds.X + 20, e.Bounds.Y);
}
```

En resumen, la aplicación combina llamadas al sistema de bajo nivel (como la Papelera), procesamiento multihilo (para el CSV y el explorador de archivos), y gráficos GDI+ dibujados a mano para crear un explorador que es ligero pero extremadamente personalizable.
