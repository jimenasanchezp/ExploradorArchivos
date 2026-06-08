# 📁 Explorador de Archivos — Suite de Productividad Multimedia

> **Explorador de archivos para Windows** construido desde cero con **.NET 8 y Windows Forms**.
> No es solo un navegador de carpetas: integra edición de fotos, reproducción de música y video,
> grabación de audio, captura y grabación de pantalla, ciencia de datos y conectividad a bases de datos — todo en una sola aplicación nativa.

---

## 📋 Tabla de contenidos

- [Vista general](#-vista-general)
- [Requisitos previos](#-requisitos-previos)
- [Instalación y ejecución](#-instalación-y-ejecución)
- [Arquitectura del proyecto](#-arquitectura-del-proyecto)
- [Lógica de Navegación y Persistencia](#-lógica-de-navegación-y-persistencia)
- [Módulos del sistema](#-módulos-del-sistema)
- [Motor Universal de Conversión y Exportación](#-motor-universal-de-conversión-y-exportación)
- [Envío de Correos y Cliente SMTP](#-envío-de-correos-y-cliente-smtp)
- [Dependencias](#-dependencias)
- [Atajos de teclado](#-atajos-de-teclado)
- [Autoras](#-autoras)

---

## 🔭 Vista general

El proyecto nació como un explorador de archivos tradicional y evolucionó hasta convertirse en una **suite de productividad multimedia y científica**. Cada tipo de archivo se abre en un módulo especializado construido a medida:

| Tipo de archivo / Origen | Módulo que lo maneja | Qué puedes hacer |
|---|---|---|
| `.jpg`, `.png`, `.bmp` | **AppFoto** | Filtros (BN, Sepia, Soft), ajuste de brillo/contraste/saturación, dibujo libre, recorte, lectura y escritura de coordenadas GPS (EXIF), visualización en mapa interactivo. |
| `.mp3`, `.wav` | **Mp3** | Reproducción con cola de reproducción, modo aleatorio (shuffle) con restauración al orden original, carátulas ID3, letras online, barra de progreso personalizada, **edición y persistencia de metadatos (título, artista y foto de portada)**. |
| `.mp4`, `.avi`, `.mkv` | **AppVideo** | Reproducción con LibVLC, extracción de audio a MP3, silenciado de video, extracción de fotogramas, recorte de video no bloqueante en barra de reproducción. |
| `.csv`, `.json`, `.xml`, `.txt` | **AppDataFusion** | Lectura inteligente sin esquema fijo, grilla virtualizada de alto rendimiento, ordenamiento (QuickSort), filtrado avanzado (búsqueda exacta con `""` y geolocalización), exportación y migración masiva Bulk a PostgreSQL y MariaDB. |
| Cámara web | **AppCamara** | Captura de video en vivo desde la webcam usando P/Invoke nativo a `avicap32.dll`. |
| Micrófono | **AppGrabadora** | Grabación de audio WAV desde el micrófono del sistema usando NAudio. |
| Pantalla / Escritorio | **AppCapturaPantalla** | Captura de pantalla completa o por región en PNG, y grabación de pantalla completa o por región en MP4 (con AVI temporal como fallback) mediante timers y FFmpeg. |
| Cualquier otro | **UI/FileViewerForm** o **QuickLookForm** | Visor de texto con edición, visor de imágenes, vista previa rápida (QuickLook con controles semáforo, arrastre de ventana y soporte para múltiples extensiones). |

---

## ✅ Requisitos previos

- **Windows 10/11** (la aplicación usa Windows Forms y controles COM nativos).
- **.NET 8.0 SDK** — [Descargar aquí](https://dotnet.microsoft.com/download/dotnet/8.0).
- **FFmpeg** (opcional) — necesario para la conversión de video a MP4 y la extracción de audio. Debe estar accesible en el `PATH` del sistema o en la misma carpeta del ejecutable (`ffmpeg.exe`).
- **LibreOffice** (opcional) — si está instalado en su ruta estándar de Windows, la aplicación lo detectará de forma automática para realizar conversiones de documentos (DOCX, XLSX, PPTX, TXT) a PDF de alta fidelidad.
- **PostgreSQL o MariaDB** (opcional) — necesario solo si deseas usar la función de migración de datos masiva de DataFusion.

---

## 🚀 Instalación y ejecución

```bash
# 1. Clonar el repositorio
git clone https://github.com/jimenasanchezp/ExploradorArchivos.git
cd ExploradorArchivos

# 2. Restaurar las dependencias NuGet
dotnet restore

# 3. Compilar el proyecto
dotnet build

# 4. Ejecutar la aplicación
dotnet run
```

También puedes abrir `ExploradorArchivos.slnx` directamente en **Visual Studio 2022** y presionar `F5`.

---

## 🏛️ Arquitectura del proyecto

La aplicación sigue un **patrón de enrutador centralizado**: la clase `Form1` es el motor central del explorador. Cuando el usuario hace doble clic en un archivo, `Form1` inspecciona la extensión y lanza el módulo correspondiente. Cada módulo es autónomo y no depende de los demás.

El diseño utiliza **clases parciales** (`partial class`) para separar responsabilidades de UI, navegación e interacción del explorador principal sin perder cohesión.

### Estructura de Directorios

```
ExploradorArchivos/
│
├── Program.cs                      # Punto de entrada ([STAThread] → Form1)
│
├── Form1/                          # 🧭 Motor central del explorador
│   ├── Form1.cs                    #   Estado, carga de directorios, papelera, barra de estado
│   ├── Form1.Designer.cs           #   Layout auto-generado de controles WinForms
│   ├── Form1.Interaccion.cs        #   Doble clic, menú contextual, drag & drop
│   ├── Form1.Navegacion.cs         #   Historial (pila Atrás/Adelante), barra de direcciones
│   └── Form1.Visualizacion.cs      #   Modos de vista (íconos, lista, detalle), ordenamiento
│
├── Models/
│   └── FileSystemItem.cs           # 📦 Modelo: nombre, ruta, tamaño, fecha, ícono del OS
│
├── Services/                       # ⚙️ Lógica de negocio transversal
│   ├── FileService.cs              #   Copiar, mover, eliminar (async) e interactuar con la Papelera
│   ├── FileConverterService.cs     #   Motor universal de conversión y exportación
│   ├── FileConverterFactory.cs     #   Fábrica de conversores de formatos
│   ├── LibreOfficeFileConverter.cs #   Conversor exacto mediante LibreOffice headless
│   ├── DocxFileConverter.cs        #   Conversor de respaldo DOCX (DocX)
│   ├── XlsxFileConverter.cs        #   Conversor de respaldo XLSX (ClosedXML)
│   ├── PptxFileConverter.cs        #   Conversor de respaldo PPTX
│   ├── PdfFileConverter.cs         #   Generación y manipulación de PDF (PdfSharpCore)
│   ├── CsvIndexer.cs               #   Genera índice CSV del directorio actual
│   ├── EmailService.cs             #   Compartir archivos vía MAPI / mailto nativo
│   ├── RecentFilesService.cs       #   Historial de archivos recientes
│   ├── LoggerService.cs            #   Registro estructurado de logs y excepciones locales
│   ├── SmtpMailService.cs          #   Envío de correos asíncronos mediante protocolo SMTP
│   ├── TextFileFormatterService.cs #   Formateo estético de archivos estructurados (JSON, XML, CSV)
│   ├── CameraCaptureService.cs     #   Interacción y captura de video/fotos desde webcam
│   └── ScreenCaptureService.cs     #   Lógica de captura de pantalla y grabación de video
│
├── UI/                             # 🎨 Componentes visuales reutilizables
│   ├── ThemeRenderer.cs            #   Sistema de temas (Dark Mode / Classic Glass) con OwnerDraw
│   ├── FileViewerForm.cs           #   Visor/editor de archivos de texto y datos
│   ├── ImageViewerForm.cs          #   Visor simple de imágenes
│   ├── QuickLookForm.cs            #   Vista previa rápida (estilo macOS) con semáforos GDI+
│   ├── ListViewSorter.cs           #   Ordenamiento por columnas en el ListView
│   ├── InputDialog.cs              #   Diálogo de entrada de texto reutilizable
│   ├── SendMailForm.cs             #   Formulario de envío directo de correo (SMTP)
│   └── ClassicDesignHelper.cs      #   Utilidades de estilo visual
│
├── AppFoto/                        # 🖼️ Editor fotográfico completo
│   ├── AppFotoForm.cs              #   Ventana principal (PictureBox + controles GDI+)
│   ├── AppFotoProcessor.cs         #   Filtros con ColorMatrix, rotación, recorte, dibujo
│   ├── AppFotoExifService.cs       #   Lectura de metadatos EXIF (cámara, fecha, GPS)
│   ├── AppFotoMetadata.cs          #   Modelo de metadatos fotográficos
│   └── AppFotoMapService.cs        #   HTML dinámico con Leaflet.js para mapas GPS (WebView2)
│
├── AppVideo/                       # 🎥 Reproductor y procesador de video
│   ├── AppVideoForm.cs             #   Reproductor con LibVLC embebido
│   ├── AppVideoProcessor.cs        #   CLI a FFmpeg: extraer audio, silenciar, extraer frames
│   ├── AppVideoMetadata.cs         #   Modelo de metadatos de video
│   └── AppVideoMapService.cs       #   Mapas para geolocalización de videos
│
├── Mp3/                            # 🎵 Reproductor de música
│   ├── MusicPlayerForm.cs          #   Interfaz del reproductor (cola, carátulas, letras)
│   ├── GestorReproduccion.cs       #   Motor de audio NAudio (WaveOutEvent + AudioFileReader)
│   ├── Cancion.cs                  #   Modelo con metadatos ID3 (título, artista, álbum)
│   ├── LyricsService.cs            #   Búsqueda de letras vía API REST (lyrics.ovh)
│   ├── PortadaService.cs           #   Extracción de carátulas embebidas
│   ├── MetadataService.cs          #   Lectura de tags MP3 con TagLib
│   └── CustomTrackBar.cs           #   Barra de progreso dibujada a medida
│
├── AppCamara/                      # 📷 Captura de video desde webcam
│   ├── AppCamaraForm.cs            #   Interfaz de cámara (preview en vivo)
│   └── AviGrabador.cs              #   P/Invoke a avicap32.dll para captura nativa
│
├── AppGrabadora/                   # 🎙️ Grabadora de audio
│   └── GestorGrabacion.cs          #   Captura WAV desde micrófono con NAudio (WaveInEvent)
│
├── AppCapturaPantalla/             # 📺 Módulo de captura y grabación de pantalla
│   ├── AppCapturaPantallaForm.cs   #   Interfaz principal de captura/grabación
│   └── RegionSelectorForm.cs       #   Overlay transparente para selección de región
│
└── AppDataFusion/                  # 📊 Suite de ciencia de datos
    ├── Core/
    │   ├── Models/
    │   │   └── DataItem.cs         #   Modelo flexible con Dictionary<string,string> CamposExtra
    │   ├── Readers/
    │   │   ├── CsvDataReader.cs    #   Lector CSV robusto (Regex para comas en comillas)
    │   │   ├── JsonDataReader.cs   #   Lector JSON con aplanamiento de objetos anidados
    │   │   ├── XmlDataReader.cs    #   Lector XML con inferencia de nodos
    │   │   └── TxtDataReader.cs    #   Lector de texto plano delimitado
    │   ├── Processing/
    │   │   ├── DataProcessor.cs    #   QuickSort in-situ, filtrado, estadísticas
    │   │   └── DataQualityAnalyzer.cs # Analizador de calidad de datos y detección de anomalías
    │   ├── Database/
    │   │   ├── DatabaseWriter.cs   #   Generador dinámico de DDL + importación Bulk masiva
    │   │   ├── PostgreSqlConnector.cs # Conector PostgreSQL (Npgsql)
    │   │   └── MariaDbConnector.cs  #   Conector MariaDB (MySqlConnector)
    │   └── Services/
    │       ├── FileExportService.cs #   Exportación a .docx y .xlsx
    │       └── GeocodingService.cs #   Geocodificación inversa de coordenadas
    └── UI/
        ├── MainForm.cs             #   Panel de control: grilla, gráficas, conexión a BBDD
        ├── MainForm.Designer.cs    #   Layout auto-generado
        ├── ChartPanel.cs           #   Gráficas estadísticas (barras, pie, líneas)
        └── Dialogs.cs              #   Diálogos de conexión y configuración
```

---

## 🧭 Lógica de Navegación y Persistencia

El explorador implementa un sistema robusto de navegación física e histórica:
- **NavigationService:** Mantiene el estado de la ruta física actual. Utiliza dos pilas (`Stack<string>`) para gestionar el historial ("Atrás" y "Adelante"), facilitando un recorrido no lineal de los directorios.
- **Persistencia de Favoritos y Accesos Directos:** Los accesos directos anclados y las carpetas marcadas como favoritas son persistidos localmente a través de `PersistenceService`. La información se almacena en archivos de texto en la ruta de sistema `%APPDATA%\ExploradorArchivos\accesos_directos.txt` y `favoritos.txt`, cargándose automáticamente en el árbol de navegación (`TreeView`) al iniciar la aplicación.

---

## 🧩 Módulos del sistema

### 🖥️ Form1 — El enrutador central
El corazón de la aplicación. Muestra el árbol de directorios de Windows en un `ListView` con íconos nativos del sistema operativo extraídos vía `Shell32.dll` con P/Invoke. 
- **OwnerDraw:** Para lograr una estética clásica y limpia, el renderizado de cabeceras, ítems y nodos se delega a `ThemeRenderer.cs`.
- **Navegación Inteligente:** Al hacer doble clic en un elemento, el enrutador evalúa si es un directorio (navega) o un archivo (lanza el módulo asociado según su extensión).

### 🖼️ AppFoto — Editor fotográfico GDI+
Motor de procesamiento de imágenes que trabaja directamente con la API gráfica de Windows (GDI+):
- **Filtros con `ColorMatrix`:** Transforma los canales RGBA de los píxeles en un solo paso de GPU para aplicar filtros (Blanco y Negro, Sepia, Soft).
- **EXIF y GPS:** Lee y escribe metadatos binarios de las fotos (orientación, cámara, fecha, coordenadas). Inyecta coordenadas GPS escribiendo los bytes Rational EXIF directamente al archivo JPEG.
- **Mapas interactivos:** Genera un HTML dinámico con `Leaflet.js` y lo renderiza dentro de un control `WebView2`. La interacción bidireccional (clic en el mapa) se realiza mediante `window.chrome.webview.postMessage` para enviar coordenadas de JS a C#.

### 🎵 Mp3 — Reproductor de música
- **Motor de audio:** Usa `NAudio` (`WaveOutEvent` + `AudioFileReader`) para comunicarse con los drivers de audio del sistema operativo (WASAPI/DirectSound), eliminando la dependencia de componentes de Windows Media Player.
- **Edición de Metadatos y Tags ID3:** Utiliza la librería `TagLibSharp`. Permite la edición y persistencia de título, artista y portada directamente en el archivo físico. Controla de forma automática la liberación y reanudación del flujo de audio para evadir bloqueos de acceso del archivo en NAudio.
- **Letras automáticas:** Realiza peticiones HTTP asíncronas a la API REST de `lyrics.ovh` y parsea dinámicamente el JSON devuelto.

### 🎥 AppVideo — Reproductor y procesador de video
- **Reproducción Embebida:** Utiliza `LibVLCSharp` (el motor nativo de VLC) integrado directamente en el formulario.
- **Edición de Video con FFmpeg:** Invoca el binario de FFmpeg en segundo plano (`CreateNoWindow = true`) usando el parámetro `-nostdin` para evitar bloqueos interactivos. Utiliza `TaskCompletionSource<bool>` para convertir el ciclo de vida del subproceso en una tarea asíncrona awaitable.
- **Recorte Integrado:** Implementa una interfaz interactiva de selección de tiempo (Inicio y Fin) sobre la barra de reproducción, realizando el recorte en segundo plano.
- **Geolocalización Híbrida (Android + iOS):** Además de usar `TagLibSharp` para metadatos técnicos, implementa un parser binario nativo que extrae la geolocalización desde videos grabados por dispositivos Android (caja `©xyz`) y dispositivos iOS/iPhone (resolviendo el árbol de átomos `keys` e `ilst` para la clave `com.apple.quicktime.location.ISO6709`), cargándolos en el mapa interactivo.

### 📊 AppDataFusion — Suite de ciencia de datos
El módulo más complejo del sistema, diseñado para manipular conjuntos de datos con esquemas desconocidos en tiempo de compilación:
- **Lectura Polimórfica:** Cuatro lectores especializados (`CsvDataReader`, `JsonDataReader`, `XmlDataReader`, `TxtDataReader`) convierten los conjuntos de datos a una lista unificada de objetos `DataItem`. Las columnas que no se conocen de antemano se capturan dinámicamente en el diccionario `CamposExtra`.
- **Virtualización de Grilla:** El control `DataGridView` se alimenta de una estructura optimizada en memoria limitando la visualización a 75,000 registros para evitar cuellos de botella de renderizado y alto consumo de memoria RAM.
- **Filtrado Avanzado:** Implementa búsqueda exacta cuando el término está entre comillas dobles (ej. `"valor"`) y filtrado nativo para coordenadas de geolocalización (`latitude` y `longitude`).
- **Análisis de Calidad de Datos:** `DataQualityAnalyzer` escanea en tiempo real anomalías como campos vacíos, correos o teléfonos mal formateados, inconsistencias lógicas en fechas y registros duplicados, generando un reporte de calidad (`QualityReport`) con sugerencias de limpieza.
- **Migración Masiva (Bulk Copy) asíncrona:** Genera sentencias optimizadas de inserción por lote. Utiliza el importador binario `NpgsqlBinaryImporter` para PostgreSQL (protocolo `COPY BINARY`) y `MySqlBulkCopy` en MariaDB (con `AllowLoadLocalInfile=true`), logrando importar decenas de miles de registros por segundo de forma asíncrona.

### 📺 AppCapturaPantalla — Captura y grabación de pantalla
Módulo especializado en capturar la actividad del escritorio de manera local:
- **Captura (Screenshot):** Utiliza `Graphics.CopyFromScreen` para tomar instantáneas de la pantalla completa o de una región específica. El formulario se minimiza asíncronamente antes de la captura para que no interfiera en la imagen final, guardando el archivo en formato PNG en *Mis Imágenes*.
- **Grabación de Pantalla (Screen Recording):** Graba la actividad del escritorio de forma asíncrona a ~15 FPS. Un bucle en segundo plano (`BucleCapturaAsync` a través de `Task.Run` y `CancellationToken`) captura la región seleccionada y la escribe secuencialmente en un archivo AVI temporal utilizando el componente nativo `AviGrabador` para no congelar la UI.
- **Conversión a MP4 con FFmpeg:** Al finalizar la grabación, invoca asíncronamente a FFmpeg en segundo plano para convertir el archivo `.avi` temporal a `.mp4` con codificación eficiente. Si FFmpeg no está instalado, se mantiene el archivo `.avi` de respaldo en la carpeta *Mis Videos*.
- **Selector de Región (Overlay):** `RegionSelectorForm` crea una capa negra semitransparente con `Opacity = 0.45` que cubre toda la pantalla. Permite al usuario dibujar con el ratón un rectángulo en cualquier dirección, normalizando las coordenadas para obtener la región exacta de la pantalla.

### 📷 AppCamara — Captura de webcam nativa
Abre un canal directo de comunicación con el driver USB de la cámara mediante llamadas a la API de Windows a través de **P/Invoke** a `avicap32.dll`. No requiere bibliotecas externas pesadas para mostrar la vista previa en vivo y guardar fotos locales.

### 🎙️ AppGrabadora — Grabación de audio
Captura el audio del micrófono del sistema utilizando la clase `WaveInEvent` de `NAudio`, escribiendo los bytes de sonido en tiempo real directamente al disco en formato WAV (`.wav`) mediante `WaveFileWriter`.

### 🔎 QuickLook — Vista previa rápida (Estilo macOS)
Permite previsualizar archivos de forma instantánea al presionar la barra espaciadora:
- **Barra de Título Interactiva:** Soporta arrastre de ventana y maximizado mediante doble clic.
- **Semáforos de Control:** Botones de control estilo macOS (rojo para cerrar, amarillo para minimizar, verde para maximizar) dibujados a mano con suavizado antialias mediante GDI+.
- **Soporte de Formatos:** Previsualiza imágenes, PDFs, páginas web y una amplia selección de archivos de texto y código fuente (`.cs`, `.html`, `.css`, `.js`, `.py`, `.md`, etc.) en un control enriquecido con scroll automático.

---

## 🔄 Motor Universal de Conversión y Exportación

La aplicación cuenta con un servicio unificado de conversión de formatos (`FileConverterService`):
- **Flujo Principal (LibreOffice Headless):** Si el binario de LibreOffice (`soffice.exe`) es detectado en las rutas estándar del sistema operativo Windows, las conversiones de archivos de texto y hojas de cálculo (DOCX, XLSX, PPTX, TXT) a formato PDF se delegan a su motor nativo, garantizando una fidelidad del 100% en la exportación.
- **Flujo de Respaldo (Fallback en C# puro):** Si LibreOffice no está disponible, la aplicación activa automáticamente conversores Open Source compilados en el proyecto:
  - **ClosedXML:** Para la estructuración y lectura de hojas de cálculo de Excel.
  - **DocX:** Para la manipulación de documentos de Word.
  - **PdfSharpCore:** Para construir, procesar y renderizar los archivos resultantes en formato PDF.

---

## ✉️ Envío de Correos y Cliente SMTP

La aplicación expone dos opciones para compartir archivos por correo electrónico:
- **Envío Directo SMTP (`SendMailForm`):** Interfaz clásica que implementa un cliente SMTP autónomo utilizando `System.Net.Mail.SmtpClient`. Almacena de manera segura las credenciales de salida, el servidor, el puerto (con soporte para STARTTLS/SSL) y los datos del remitente en un archivo JSON local (`smtp_config.json` en `%APPDATA%`). El envío se realiza asíncronamente mediante `Task.Run` para evitar el congelamiento de la interfaz de usuario. Por seguridad moderna, requiere el uso de **Contraseñas de Aplicación (Tokens de aplicación)** de proveedores como Gmail o Outlook. Limita físicamente el tamaño de los adjuntos a un máximo de **25 MB**.
- **Compartición Nativa MAPI / mailto:** Actúa como un fallback rápido si el usuario no desea configurar credenciales. Invoca la librería nativa de Windows `Mapi32.dll` o el esquema URI `mailto:` para abrir el cliente de correo predeterminado del sistema (Outlook, Thunderbird, etc.) con el archivo ya adjunto.

---

## 📦 Dependencias

Las dependencias NuGet se gestionan automáticamente al ejecutar `dotnet restore`. Están definidas en el archivo de proyecto `ExploradorArchivos.csproj`:

| Paquete NuGet | Versión | Para qué se usa |
|---|---|---|
| `NAudio` | 2.2.1 | Grabación y reproducción de audio en tiempo real desde hardware. |
| `TagLibSharp` | 2.3.0 | Extracción e inyección de metadatos (ID3 en audio y EXIF en video). |
| `LibVLCSharp` | 3.9.3 | Motor de reproducción de video multiplataforma. |
| `LibVLCSharp.WinForms` | 3.9.3 | Control de Windows Forms para renderizar el reproductor de VLC. |
| `VideoLAN.LibVLC.Windows` | 3.0.21 | Binarios nativos de VLC para la ejecución en Windows de 32 y 64 bits. |
| `Microsoft.Web.WebView2` | 1.0.3912.50 | Renderizador web basado en Edge (Chromium) para mapas y visor de PDF. |
| `DocumentFormat.OpenXml` | 3.5.1 | Estructuración y manipulación de documentos de Microsoft Office Open XML. |
| `ClosedXML` | 0.105.0 | Motor de respaldo para la exportación de hojas de cálculo a XLSX. |
| `DocX` | 5.0.0 | Manipulación rápida y estructuración de archivos Word (.docx) sin Office. |
| `PdfSharpCore` | 1.3.67 | Motor de renderizado y exportación de archivos a PDF. |
| `Npgsql` | 9.0.2 | Conector y proveedor de datos ADO.NET para bases de datos PostgreSQL. |
| `MySqlConnector` | 2.3.7 | Conector optimizado y asíncrono para bases de datos MariaDB y MySQL. |
| `AForge.Video.DirectShow` | 2.2.5 | Detección y comunicación con dispositivos de captura de video (webcams). |

### Dependencias Externas (no NuGet):
- **FFmpeg (`ffmpeg.exe`):** Necesario para la extracción y manipulación asíncrona de video/audio en los módulos `AppVideo` y `AppCapturaPantalla`. Debe estar en la carpeta raíz del proyecto o en el `PATH`.
- **LibreOffice:** Suite de oficina que se ejecuta en segundo plano (`headless`) para realizar exportaciones de documentos a PDF de alta fidelidad.

---

## ⌨️ Atajos de teclado

| Atajo | Acción |
|---|---|
| `Enter` | Abrir archivo o carpeta seleccionada. |
| `Espacio` | Abrir o cerrar la vista previa rápida (QuickLook). |
| `Backspace` | Navegar a la carpeta contenedora anterior. |
| `Delete` | Eliminar el archivo seleccionado enviándolo a la Papelera. |
| `F2` | Iniciar edición del nombre del archivo o carpeta. |
| `Ctrl + C` | Copiar archivos seleccionados al portapapeles. |
| `Ctrl + V` | Pegar archivos desde el portapapeles en el directorio activo. |
| `Ctrl + E` | Exportar de forma automática un índice CSV del directorio actual. |

---

## 👤 Autoras

Desarrollado con 💜 en C# y .NET 8 por:
- **Jimena Sanchez**
- **Carolina Sustaita**

*Desarrollo y programación asistidos por **Antigravity**, un agente de IA para codificación avanzada desarrollado por el equipo de Google DeepMind.*

