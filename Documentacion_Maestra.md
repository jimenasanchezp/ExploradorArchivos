# 📘 Documentación Maestra: Explorador de Archivos y Suite de Productividad

Este documento centraliza **absolutamente toda la información** técnica, funcional y estructural del sistema. Reúne en un único lugar la justificación de diseño, el manual de usuario, la arquitectura de código, las dependencias y los diagramas de flujo de ejecución.

---

## 🛠️ 1. Propósito y Justificación del Sistema

El proyecto ha crecido mucho más allá de un simple explorador de archivos tradicional. Actualmente incorpora edición fotográfica con geolocalización, manipulación de metadatos de audio, codificación de video en tiempo real, conectividad a bases de datos y ciencia de datos.

*   **Prevención de Deuda Técnica:** Al tener tantas responsabilidades, este documento actúa como mapa crítico. Asegura que el conocimiento no resida únicamente en la memoria del autor y facilita la curva de aprendizaje (Onboarding).
*   **Aislamiento y Modularidad:** El código está fuertemente modularizado (`AppCamara`, `AppFoto`, `AppVideo`, `AppDataFusion`, `Mp3`). Si en el futuro se desea reemplazar el reproductor de video, se puede hacer sin afectar el motor central del explorador.
*   **Manejo de Memoria No Administrada:** El sistema hace uso intensivo de `P/Invoke` nativo, cursores COM de Windows, flujos de bytes (NAudio) e interprocesos (FFmpeg). Esto requiere liberación estricta (`Dispose()`) para evitar fugas de RAM o bloqueos (Memory Leaks), lo cual está altamente documentado aquí.

---

## 📖 2. Guía de Funcionalidades y Flujos de Uso (Manual de Usuario)

### Nivel 1: Operaciones Básicas (El Explorador)
*   **Navegación:** Haz doble clic en las carpetas de la lista principal para entrar a ellas. Usa el botón "Atrás" para regresar.
*   **Archivos de Texto/Datos:** Al dar doble clic en archivos `.txt`, `.json`, `.csv`, `.xml`, etc., el sistema abrirá automáticamente el `FileViewerForm` o `QuickLookForm` para vista rápida.
*   **Gestión:** Clic derecho sobre cualquier archivo para renombrar, eliminar o ver propiedades básicas.
*   **QuickLook (Vista Rápida):** Presiona la barra `Espacio` sobre cualquier archivo seleccionado para abrir su previsualización instantánea. Cuenta con barra de arrastre superior, maximización por doble clic y botones "semáforo" (Cerrar 🔴, Minimizar 🟡, Maximizar 🟢) pintados con GDI+. No se cierra al perder el foco y soporta múltiples extensiones (`.cs`, `.html`, `.css`, `.js`, `.py`, `.bat`, `.cmd`, `.dat`, `.md`, etc.) además de PDFs e imágenes.

### Nivel 2: Productividad y Multimedia
*   **Fotografías (`.jpg`, `.png`):** Se abrirán en el `AppFotoForm`. Puedes aplicar filtros retro (Blanco y Negro, Sepia), alterar contraste/brillo, dibujar firmas y exportar las imágenes. Si la foto fue tomada con un celular, mostrará su ubicación GPS en un mapa interactivo.
*   **Música (`.mp3`, `.wav`):** Se abrirá el `MusicPlayerForm`. Soporta listas de reproducción, muestra las carátulas incrustadas en los archivos y descarga la letra de la canción desde internet automáticamente.
*   **Video (`.mp4`, `.avi`):** Se reproducirán de forma nativa. Incluye herramientas avanzadas para silenciar el video, extraer su pista de audio como MP3, o extraer una ráfaga de imágenes (Frames).

### Nivel 3: Herramientas Avanzadas (Data Science & OS)
*   **DataFusion:** Si seleccionas un archivo `.csv` o `.json` masivo, puedes enviarlo al módulo `AppDataFusion`. Este reconstruirá las columnas, permitirá ver los datos de forma ágil sin consumir toda la RAM (grilla virtualizada hasta 75,000 registros), aplicar filtros avanzados con búsquedas exactas utilizando comillas dobles (ej. `"valor"`), buscar u ordenar por geolocalización (`latitude` y `longitude`), exportar a múltiples formatos y enviar los datos directamente por correo electrónico (el botón `📧 Enviar` genera un CSV temporal en `%TEMP%` y abre el cliente de correo predeterminado mediante el protocolo `mailto:`). Además, puedes migrar los datos hacia una base de datos PostgreSQL o MariaDB.
*   **Terminal Rápida:** Si necesitas ejecutar un comando en la ruta actual, escribe `cmd [tu comando]` en la barra de direcciones superior.

---

## ⚙️ 3. Infraestructura Tecnológica, Herramientas y Dependencias

El sistema corre sobre **.NET 8.0 Windows Forms** utilizando un alto grado de asincronía (`async/await`) y programación funcional con **LINQ**.

### Dependencias Principales (NuGet & Binarios)
1.  **`NAudio`:** Para la captura de micrófono en crudo y la reproducción de MP3/WAV comunicándose con WASAPI.
2.  **`TagLibSharp`:** Extracción de metadatos EXIF de fotos (GPS, Cámara) y etiquetas ID3 de MP3 (Álbum, Carátulas).
3.  **`FFmpeg`:** Binario C externo invocado vía CLI (`Process.Start`) para la manipulación destructiva de video y audio.
4.  **`Npgsql`:** Driver de ADO.NET para inyectar sentencias SQL hacia bases de datos PostgreSQL de forma transaccional.
5.  **`DocumentFormat.OpenXml`:** SDK de Microsoft para generar archivos Word (`.docx`) y Excel (`.xlsx`) sin necesidad de tener Office instalado.
6.  **`WebView2` (Edge Chromium):** Motor de renderizado web incrustado usado para visualizar PDF y renderizar los mapas interactivos de `Leaflet.js`.

---

## 🏛️ 4. Arquitectura del Motor Central

El sistema utiliza un **Patrón de Enrutador Centralizado**. El `Form1` principal no sabe cómo reproducir un MP3 ni cómo editar una foto; su única responsabilidad es construir el árbol de directorios y delegar (Enrutar) el trabajo.

```mermaid
graph TD
    A[Usuario: Doble Clic en Lista] --> B{¿Es Carpeta?}
    B -- Sí --> C[Apilar en Historial e invocar LoadDirectory]
    B -- No --> D[Extraer Extensión]
    D --> E{Switch Case (Router)}
    E -- .mp3 / .wav --> F[Instanciar MusicPlayerForm]
    E -- .jpg / .png --> G[Instanciar AppFotoForm]
    E -- .mp4 / .avi --> H[Instanciar AppVideoForm]
    E -- Otros --> I[Lanzar ShellExecute nativo de Windows]
```

---

## 🔄 5. Flujos de Instrucciones Avanzados

### A) Flujo de DataFusion (Inferencia de Esquemas)
1. **Inyección:** Se lee el archivo `.csv`.
2. **Inferencia:** El sistema no usa modelos rígidos. Usa `Dictionary<string, string> CamposExtra` en un bucle heurístico para capturar columnas desconocidas dinámicamente.
3. **Paginación Virtual:** La tabla gráfica (`DataGridView`) no recibe el millón de registros de golpe; se carga una vista parcial en memoria (`Take`) para no crashear la UI.
4. **Volcado a PostgreSQL:** Se crea un bloque de texto dinámico `CREATE TABLE` deduciendo las columnas del diccionario, seguido de un comando transaccional `BULK INSERT`.

### B) Flujo de Invocación CLI (Video a MP3)
1. **Comando:** El usuario hace clic en "Extraer Audio".
2. **Construcción:** Se crea un argumento `-i "ruta.mp4" -vn -acodec libmp3lame "salida.mp3"`.
3. **Aislamiento:** Se levanta un proceso fantasma de `FFmpeg` con `CreateNoWindow = true`.
4. **Sincronía:** Se utiliza un `TaskCompletionSource` atado al evento `Exited` del proceso, convirtiendo un evento nativo asíncrono del OS en una promesa `Task` controlable.

### C) Flujo de Envío de Datos por Correo (DataFusion)
1. **Validación:** Se verifica que existan registros visualizados en la grilla.
2. **Generación Temporal:** Se crea un archivo CSV en el directorio temporal del sistema (`%TEMP%`) conteniendo la lista de datos procesados (incluyendo mapeo de columnas dinámicas).
3. **Invocación Protocolo mailto:** Se genera una URI con el esquema `mailto:` codificando los parámetros de asunto (`subject`) y cuerpo del mensaje (`body`), el cual incluye la ruta física del archivo CSV temporal.
4. **Ejecución Shell:** Se inicia un proceso mediante `Process.Start` con `UseShellExecute = true` apuntando a la URI, lo que abre el cliente de correo predeterminado del sistema operativo (Outlook, Thunderbird, etc.) listo para que el usuario adjunte y envíe el archivo.

### D) Mecánica de Filtrado Exacto e Inferencia de Coordenadas en DataProcessor
1. **Detección de Búsqueda Exacta:** En `DataProcessor.Filtrar`, se determina si el parámetro `exacto` es `true` o si el término de búsqueda está encerrado en comillas dobles (ej. `"valor"`). Si es así, se descartan las comillas y se activa la comparación exacta.
2. **Comparación Case-Insensitive:** La coincidencia se realiza comparando la cadena normalizada en minúsculas. Si la búsqueda es exacta, se evalúa la igualdad estricta (`==`); si no lo es, se busca la coincidencia parcial (`Contains`).
3. **Campos Especiales (Coordenadas):** Si el campo a filtrar es `latitude` o `longitude`, el procesador realiza una comparación con el formato de coma flotante de alta precisión (`F6`). En el ordenamiento (`DataProcessor.CompararCampo`), se realiza una conversión a valores numéricos `double` para ordenar los registros con base en su posición geográfica real, evitando la ordenación alfabética errónea de números decimales.

---

## 📚 6. Referencia Exhaustiva y Diccionario Completo de Clases

Esta sección documenta **todas las clases** y funciones de los módulos del sistema.

### 📷 AppCamara (Captura de Video Nativa)
*   `CamaraForm`: Formulario que muestra el feed en vivo usando DirectShow o MediaFoundation a través de un WebView.
*   `CamaraService`: Usa P/Invoke (`capCreateCaptureWindowA` de `avicap32.dll`) para conectarse a nivel kernel con el driver USB de la webcam.

### 🖼️ AppFoto (Edición GDI+ y Geolocalización)
*   `AppFotoForm`: La ventana principal de edición visual. Usa un `PictureBox` con repintado inteligente.
*   `AppFotoProcessor`: Algoritmos GDI+ puros. Posee los métodos estáticos `CorrectOrientation()`, `AplicarFiltro()`, `AjustarImagen()` y `GuardarConGps()`.
*   `AppFotoMetadata`: Usa `TagLib.Image.File` para extraer la latitud y longitud. Convierte fracciones sexagesimales a decimales.
*   `AppFotoMapService`: Retorna strings HTML que inyectan `Leaflet.js` dentro del `WebView2` para trazar los mapas base de OpenStreetMap.

### 🎥 AppVideo (Visualización y FFmpeg)
*   `AppVideoForm`: Usa el control `WindowsMediaPlayer` COM incrustado.
*   `AppVideoProcessor`: Envuelve binarios de FFmpeg. Expone `ExtraerAudio()`, `SilenciarVideo()`, y `ExtraerFrames()` orquestando el `System.Diagnostics.Process`.

### 🎵 Mp3 (Motor de Audio y REST)
*   `MusicPlayerForm`: Administra la lista de reproducción y la interfaz fluida del reproductor.
*   `GestorReproduccion`: Instancia un `WaveOutEvent` y `AudioFileReader` de NAudio. Controla la memoria binaria (PCM).
*   `LyricsService`: Usa `HttpClient` y `JsonDocument` para hacer llamadas asíncronas REST a `api.lyrics.ovh`.

### 🎙️ AppGrabadora (Microfonía en Crudo)
*   `AppGrabadoraForm`: Vista minimalista para grabación.
*   `GestorGrabacion`: Usa `WaveInEvent` (NAudio) para capturar el búfer de entrada del micrófono de la PC y escribir directamente al disco duro (`WaveFileWriter`) previniendo desbordamientos de memoria (Buffer Overruns).

### 📊 AppDataFusion (Data Science y ETL)
*   `MainForm`: Panel de control de datos. Maneja la grilla virtualizada con controles de filtrado en la toolbar, ordenación interactiva, botón de exportación por correo (`BtnEnviarCorreo_Click`) y conexiones BBDD.
*   `CsvDataReader`: Algoritmo inteligente con expresiones regulares `Regex` que lee archivos sin romper los datos por comas dentro de strings.
*   `DataProcessor`: Contiene la lógica central de ordenación y filtrado. Expone el método `Filtrar` con soporte para coincidencia exacta (`exacto`), filtrado y ordenamiento de geolocalización (`latitude`/`longitude`) y `QuickSort` in-situ de alto rendimiento.
*   `DatabaseWriter`: Generador dinámico de código SQL (Data Definition Language) que construye las tablas en base a los diccionarios C# y envía el `INSERT` transaccional.

### 📁 Form1 y Base
*   `Form1`: El navegador clásico. Coordina el árbol de carpetas de Windows y lanza las aplicaciones correspondientes (El Enrutador).
*   `FileSystemItem`: Modelo de datos puro (POCO) con nombre, fecha, peso y ruta.
*   `SystemIconManager`: P/Invoke a `Shell32.dll` (`SHGetFileInfo`) para extraer los íconos oficiales del Sistema Operativo por extensión en HD.

### ⚙️ Services y UI
*   `FileOperationsService`: Wrapper de `System.IO` para copias, eliminaciones y movimientos asíncronos.
*   `FileConverterService`: OpenXML SDK para transformar archivos a formatos ofimáticos `.docx` y `.xlsx`.
*   `ThemeRenderer`: Sobrescribe los eventos de pintado (`OnPaint`) de los controles de Windows para aplicar paletas oscuras (Dark Mode) esquivando el estilo obsoleto por defecto.
*   `QuickLookForm`: Formulario sin bordes nativos que previsualiza archivos rápidamente. Implementa una barra de título personalizada para arrastrar la ventana, botones "semáforo" (Cerrar, Minimizar, Maximizar) pintados con GDI+ y lógica persistente al desactivarse. Soporta carga de texto extendida, imágenes, PDFs y WebView2.

---

## 🧩 7. Código Fuente y Explicación Estructural

A continuación se incluyen pedazos de código clave del sistema que sustentan las explicaciones dadas anteriormente. Se omiten interfaces visuales repetitivas y se enfoca estrictamente en la lógica nativa y algoritmos de alto rendimiento.

### 🎵 7.1 Motor de Audio: `GestorReproduccion.cs`
Este archivo es el núcleo de control multihilo para la reproducción de MP3. Utiliza la librería de bajo nivel `NAudio` para interactuar con los canales PCM de Windows.

```csharp
using NAudio.Wave;
using TagLib;

public class GestorReproduccion : IDisposable {
    private WaveOutEvent _waveOut; // Dispositivo de hardware nativo
    private AudioFileReader _audioReader; // Lector de bytes en disco
    public List<Cancion> _cola = new List<Cancion>();
    
    public void Play() {
        if (_waveOut != null && _waveOut.PlaybackState != PlaybackState.Playing) {
            _waveOut.Play(); // Envía el buffer binario a la tarjeta de sonido
        }
    }

    public void Seek(double porcentaje) {
        if (_audioReader != null) {
            // Convierte un % visual de la UI (0.0 a 1.0) a un bloque exacto de bytes (Int64)
            long totalBytes = _audioReader.Length;
            long nuevosBytes = (long)(totalBytes * porcentaje);
            _audioReader.Position = nuevosBytes;
        }
    }

    private void OnPlaybackStopped(object sender, StoppedEventArgs e) {
        // Evento disparado por NAudio en un hilo secundario cuando el buffer se vacía
        // Se requiere un delegado asíncrono para no bloquear la UI.
        Task.Run(() => AvanzarCancionPendiente());
    }
}
```

### 🌐 7.2 Cliente REST Musical: `LyricsService.cs`
Esta clase demuestra cómo el explorador se conecta asíncronamente a internet para enriquecer la experiencia.

```csharp
using System.Net.Http;
using System.Text.Json;

public static class LyricsService {
    private static readonly HttpClient _cliente = new HttpClient();

    public static async Task<string> BuscarLetraAsync(string artista, string titulo) {
        try {
            string url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artista)}/{Uri.EscapeDataString(titulo)}";
            HttpResponseMessage respuesta = await _cliente.GetAsync(url);
            
            if (respuesta.IsSuccessStatusCode) {
                string json = await respuesta.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    return doc.RootElement.GetProperty("lyrics").GetString();
                }
            }
            return "Letra no encontrada en internet.";
        } catch {
            return "Error de red al buscar la letra.";
        }
    }
}
```

### 🎥 7.3 Codificación de Video Oculta: `AppVideoProcessor.cs`
La edición de video nativa es lenta. El sistema usa CLI inter-procesos, comunicándose invisiblemente con `FFmpeg`.

```csharp
using System.Diagnostics;

public static class AppVideoProcessor {
    public static async Task ExtraerAudio(string rutaEntrada, string rutaSalida) {
        string args = $"-i \"{rutaEntrada}\" -vn -acodec libmp3lame -ab 192k \"{rutaSalida}\"";
        
        var tcs = new TaskCompletionSource<bool>();
        Process proceso = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "ffmpeg.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true // Oculta la terminal negra al usuario
            },
            EnableRaisingEvents = true
        };

        proceso.Exited += (s, e) => {
            proceso.Dispose();
            tcs.SetResult(true); // Desbloquea la promesa asíncrona
        };

        proceso.Start();
        await tcs.Task;
    }
}
```

### 📊 7.4 Lectura Polimórfica sin Esquemas (DataFusion)
En ciencia de datos, los CSV raramente tienen las mismas columnas. El sistema las infiere en tiempo de ejecución.

```csharp
using System.Text.RegularExpressions;

public class CsvDataReader {
    public async Task<List<DataItem>> Leer(string ruta) {
        var lista = new List<DataItem>();
        string[] lineas = await File.ReadAllLinesAsync(ruta);
        
        // Regex para soportar comas literales dentro de comillas ("México, D.F.")
        var csvSplit = new Regex("(?:^|,)(\"(?:[^\"]+|\"\")*\"|[^,]*)", RegexOptions.Compiled);
        string[] encabezados = SepararCsvRobust(lineas[0], csvSplit);

        for (int i = 1; i < lineas.Length; i++) {
            if (string.IsNullOrWhiteSpace(lineas[i])) continue;
            string[] celdas = SepararCsvRobust(lineas[i], csvSplit);
            
            var item = new DataItem { Id = Guid.NewGuid().ToString() };

            // Magia de esquema dinámico: las columnas dinámicas se guardan en el Hash Dictionary
            for (int j = 0; j < encabezados.Length; j++) {
                if (j < celdas.Length) item.CamposExtra[encabezados[j]] = celdas[j];
            }
            lista.Add(item);
        }
        return lista;
    }
}
```

### 🚀 7.5 Virtualización de Interfaz (Alto Rendimiento)
Para no saturar la memoria RAM con cientos de miles de registros, se usa una DataTable temporal desconectada.

```csharp
private async Task BindGridAsync() {
    DgvTodos.Visible = false; // Detener motor GDI+
    
    // Virtualización Parcial de Memoria
    var mostrar = _datos.Take(75000).ToList();

    DataTable dt = new DataTable();
    dt.Columns.Add("Id");

    // Inferencia inversa para armar la tabla visual a partir de llaves dinámicas
    var columnasExtra = new HashSet<string>();
    foreach (var d in mostrar) {
        foreach (var key in d.CamposExtra.Keys) columnasExtra.Add(key);
    }
    foreach (var col in columnasExtra) dt.Columns.Add(col);

    await Task.Run(() => {
        foreach (var d in mostrar) {
            DataRow row = dt.NewRow();
            row["Id"] = d.Id;
            foreach (var kvp in d.CamposExtra) row[kvp.Key] = kvp.Value;
            dt.Rows.Add(row);
        }
    });

    DgvTodos.DataSource = dt;
    DgvTodos.Visible = true; // Repintar en bloque
}
```

### 🗄️ 7.6 Base de Datos Dinámica: `DatabaseWriter.cs`
Convierte diccionarios de memoria a PostgreSQL (`DDL` y transacciones masivas).

```csharp
public static async Task EscribirEnPostgreSQLAsync(string conexion, string tabla, List<DataItem> datos) {
    using var conn = new NpgsqlConnection(conexion);
    await conn.OpenAsync();

    // 1. DDL: Crear Tabla dinámicamente
    var colExtra = new HashSet<string>();
    foreach (var d in datos) foreach (var key in d.CamposExtra.Keys) colExtra.Add(key);

    StringBuilder sbCreate = new StringBuilder($"CREATE TABLE {tabla} (Id VARCHAR PRIMARY KEY");
    foreach (var col in colExtra) sbCreate.Append($", \"{col}\" TEXT");
    sbCreate.Append(");");

    using (var cmd = new NpgsqlCommand(sbCreate.ToString(), conn)) await cmd.ExecuteNonQueryAsync();

    // 2. BULK INSERT: Transacción rápida para el disco
    using var tx = await conn.BeginTransactionAsync();
    foreach (var d in datos) {
        StringBuilder sbCols = new StringBuilder("Id"), sbVals = new StringBuilder($"'{d.Id}'");
        foreach (var kvp in d.CamposExtra) {
            sbCols.Append($", \"{kvp.Key}\"");
            sbVals.Append($", '{kvp.Value.Replace("'","''")}'"); // Escapado SQL Seguro
        }
        string sql = $"INSERT INTO {tabla} ({sbCols}) VALUES ({sbVals});";
        using var cmdIns = new NpgsqlCommand(sql, conn, tx);
        await cmdIns.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
}
```

### 🔍 7.7 Filtrado Exacto y Case-Insensitive: `DataProcessor.cs`
Muestra el algoritmo adaptado para discernir y ejecutar búsquedas exactas con comillas dobles, así como el tratamiento de geolocalización.

```csharp
public static List<DataItem> Filtrar(List<DataItem> datos, string campo, string valor, bool exacto = false)
{
    var resultado = new List<DataItem>();
    // Comprobar si el término de búsqueda viene entrecomillado
    bool exactMatch = exacto || (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length >= 2);
    string v = (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length >= 2) 
        ? valor.Substring(1, valor.Length - 2).ToLower() 
        : valor.ToLower();
    string campoLow = campo.ToLower();

    for (int i = 0; i < datos.Count; i++)
    {
        var item = datos[i];
        
        bool CheckMatch(string? field)
        {
            string f = (field ?? "").ToLower();
            return exactMatch ? f == v : f.Contains(v);
        }

        bool match = campoLow switch
        {
            "nombre" => CheckMatch(item.Nombre),
            "categoria" => CheckMatch(item.Categoria),
            "fuente" => CheckMatch(item.Fuente),
            "id" => item.Id.ToString() == v,
            "valor" => CheckMatch(item.Valor.ToString("F2")),
            "fecha" => CheckMatch(item.Fecha.ToString("yyyy-MM-dd")),
            "latitude" => CheckMatch(item.Latitude?.ToString("F6")),
            "longitude" => CheckMatch(item.Longitude?.ToString("F6")),
            _ => item.CamposExtra != null && item.CamposExtra.TryGetValue(campoLow, out var ev) && ev != null
                 ? CheckMatch(ev)
                 : CheckMatch(item.Nombre) || CheckMatch(item.Categoria)
        };
        if (match) resultado.Add(item);
    }
    return resultado;
}
```

### 🎨 7.8 Pintura GDI+ de Semáforos de Ventana: `QuickLookForm.cs`
Dibuja botones circulares estilo macOS en la barra de título superior con suavizado antialias.

```csharp
private Button CrearBotonSemaforo(Color color, int x, Color backColor)
{
    Button b = new Button { Name = "btnSemaforo", Location = new Point(x, 0), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
    b.FlatAppearance.BorderSize = 0;
    b.Paint += (s, e) => {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(backColor);
        e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
        e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
    };
    return b;
}
```

