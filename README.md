# 📁 Explorador de Archivos — Suite de Productividad Multimedia

> **Explorador de archivos para Windows** construido desde cero con **.NET 8 y Windows Forms**.
> No es solo un navegador de carpetas: integra edición de fotos, reproducción de música y video,
> grabación de audio, ciencia de datos y conectividad a bases de datos — todo en una sola aplicación nativa.

---

## 📋 Tabla de contenidos

- [Vista general](#-vista-general)
- [Capturas de pantalla](#-capturas-de-pantalla)
- [Requisitos previos](#-requisitos-previos)
- [Instalación y ejecución](#-instalación-y-ejecución)
- [Arquitectura del proyecto](#-arquitectura-del-proyecto)
- [Módulos del sistema](#-módulos-del-sistema)
- [Dependencias](#-dependencias)
- [Atajos de teclado](#-atajos-de-teclado)
- [Documentación técnica](#-documentación-técnica)

---

## 🔭 Vista general

El proyecto nació como un explorador de archivos tradicional y evolucionó hasta convertirse en una **suite de productividad completa**. Cada tipo de archivo se abre en un módulo especializado construido a medida:

| Tipo de archivo | Módulo que lo maneja | Qué puedes hacer |
|---|---|---|
| `.jpg`, `.png`, `.bmp` | **AppFoto** | Filtros (BN, Sepia, Soft), ajuste de brillo/contraste/saturación, dibujo libre, recorte, lectura y escritura de coordenadas GPS (EXIF), visualización en mapa |
| `.mp3`, `.wav` | **Mp3** | Reproducción con cola, modo aleatorio (shuffle) con restauración al orden secuencial original al desactivarse, carátulas ID3, búsqueda automática de letras por internet, barra de progreso personalizada, **edición y persistencia de metadatos (título, artista y foto de portada)** |
| `.mp4`, `.avi`, `.mkv` | **AppVideo** | Reproducción con LibVLC, extracción de audio a MP3, silenciado de video, extracción de fotogramas, recorte de video no bloqueante e integrado en la barra de reproducción — todo vía FFmpeg |
| `.csv`, `.json`, `.xml`, `.txt` | **AppDataFusion** | Lectura inteligente sin esquema fijo, grilla virtualizada, ordenamiento (QuickSort) y filtrado avanzado (búsqueda exacta con `""` y geolocalización), exportación a `.docx`/`.xlsx`/correo, migración masiva Bulk a PostgreSQL y MariaDB |
| Cámara web | **AppCamara** | Captura de video en vivo desde la webcam usando P/Invoke nativo a `avicap32.dll` |
| Micrófono | **AppGrabadora** | Grabación de audio WAV desde el micrófono del sistema usando NAudio |
| Cualquier otro | **UI/FileViewerForm** | Visor de texto con edición, visor de imágenes, vista previa rápida (QuickLook con controles semáforo, arrastre de ventana y soporte para múltiples extensiones) |

---

## 📸 Capturas de pantalla

<!-- Agrega aquí capturas de pantalla de tu aplicación cuando las tengas disponibles -->
*Próximamente.*

---

## ✅ Requisitos previos

- **Windows 10/11** (la aplicación usa Windows Forms y controles COM nativos)
- **.NET 8.0 SDK** — [Descargar aquí](https://dotnet.microsoft.com/download/dotnet/8.0)
- **FFmpeg** (opcional) — necesario solo si usas las funciones de extracción de audio/video. Debe estar accesible en el `PATH` del sistema o en la misma carpeta del ejecutable.
- **LibreOffice** (opcional) — si está instalado en su ruta estándar de Windows, la aplicación lo detectará de forma automática para realizar conversiones de documentos (DOCX, XLSX, PPTX, TXT) a PDF y otros formatos con fidelidad del 100% usando su motor headless.
- **PostgreSQL o MariaDB** (opcional) — solo si deseas usar la función de migración de datos de DataFusion.

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

La aplicación sigue un **patrón de enrutador centralizado**: `Form1` es el navegador de carpetas y el punto de decisión. Cuando el usuario hace doble clic en un archivo, `Form1` inspecciona la extensión y lanza el módulo correspondiente. Cada módulo es autónomo y no depende de los demás.

El **FileConverterService** actúa como el motor de exportación y conversión universal del sistema. Si LibreOffice está instalado localmente, la aplicación delega la conversión a su binario headless (`soffice.exe`) para lograr la máxima fidelidad posible. Si no se detecta, se activa automáticamente un motor fallback interno desarrollado en C# que utiliza librerías Open-Source como `PdfSharpCore`, `ClosedXML` y `DocX` para reconstruir los documentos en memoria.

```
ExploradorArchivos/
│
├── Program.cs                    # Punto de entrada ([STAThread] → Form1)
│
├── Form1/                        # 🧭 Motor central del explorador
│   ├── Form1.cs                  #   Estado, carga de directorios, papelera, barra de estado
│   ├── Form1.Designer.cs         #   Layout auto-generado de controles WinForms
│   ├── Form1.Interaccion.cs      #   Doble clic, menú contextual, drag & drop
│   ├── Form1.Navegacion.cs       #   Historial (pila Atrás/Adelante), barra de direcciones
│   └── Form1.Visualizacion.cs    #   Modos de vista (íconos, lista, detalle), ordenamiento
│
├── Models/
│   └── FileSystemItem.cs         # 📦 POCO: nombre, ruta, tamaño, fecha, ícono del OS
│
├── Services/                     # ⚙️ Lógica de negocio transversal
│   ├── FileService.cs            #   Copiar, mover, eliminar (async)
│   ├── FileConverterService.cs   #   Motor universal de conversión y exportación (con LibreOffice + fallbacks C#)
│   ├── CsvIndexer.cs             #   Genera índice CSV del directorio actual
│   ├── EmailService.cs           #   Compartir archivos vía MAPI / mailto nativo
│   ├── RecentFilesService.cs     #   Historial de archivos recientes
│   ├── LoggerService.cs          #   Registro estructurado de logs y excepciones locales
│   ├── SmtpMailService.cs        #   Envío de correos asíncronos mediante protocolo SMTP
│   ├── TextFileFormatterService.cs # Formateo estético de archivos estructurados (JSON, XML, CSV)
│   └── CameraCaptureService.cs   #   Interacción y captura de video/fotos desde webcam
│
├── UI/                           # 🎨 Componentes visuales reutilizables
│   ├── ThemeRenderer.cs          #   Sistema de temas (Dark Mode) con OwnerDraw
│   ├── FileViewerForm.cs         #   Visor/editor de archivos de texto y datos
│   ├── ImageViewerForm.cs        #   Visor simple de imágenes
│   ├── QuickLookForm.cs          #   Vista previa rápida (estilo macOS)
│   ├── ListViewSorter.cs         #   Ordenamiento por columnas en el ListView
│   ├── InputDialog.cs            #   Diálogo de entrada de texto reutilizable
│   ├── SendMailForm.cs           #   Formulario de envío directo de correo (SMTP)
│   └── ClassicDesignHelper.cs    #   Utilidades de estilo visual
│
├── AppFoto/                      # 🖼️ Editor fotográfico completo
│   ├── AppFotoForm.cs            #   Ventana principal (PictureBox + controles GDI+)
│   ├── AppFotoProcessor.cs       #   Filtros con ColorMatrix, rotación, recorte, dibujo
│   ├── AppFotoExifService.cs     #   Lectura de metadatos EXIF (cámara, fecha, GPS)
│   ├── AppFotoMetadata.cs        #   Modelo de metadatos fotográficos
│   └── AppFotoMapService.cs      #   HTML dinámico con Leaflet.js para mapas GPS
│
├── AppVideo/                     # 🎥 Reproductor y procesador de video
│   ├── AppVideoForm.cs           #   Reproductor con LibVLC embebido
│   ├── AppVideoProcessor.cs      #   CLI a FFmpeg: extraer audio, silenciar, extraer frames
│   ├── AppVideoMetadata.cs       #   Modelo de metadatos de video
│   └── AppVideoMapService.cs     #   Mapas para geolocalización de videos
│
├── Mp3/                          # 🎵 Reproductor de música
│   ├── MusicPlayerForm.cs        #   Interfaz del reproductor (cola, carátulas, letras)
│   ├── GestorReproduccion.cs     #   Motor de audio NAudio (WaveOutEvent + AudioFileReader)
│   ├── Cancion.cs                #   Modelo con metadatos ID3 (título, artista, álbum)
│   ├── LyricsService.cs          #   Búsqueda de letras vía API REST (lyrics.ovh)
│   ├── PortadaService.cs         #   Extracción de carátulas embebidas
│   ├── MetadataService.cs        #   Lectura de tags MP3 con TagLib
│   └── CustomTrackBar.cs         #   Barra de progreso dibujada a medida
│
├── AppCamara/                    # 📷 Captura de video desde webcam
│   ├── AppCamaraForm.cs          #   Interfaz de cámara (preview en vivo)
│   └── AviGrabador.cs            #   P/Invoke a avicap32.dll para captura nativa
│
├── AppGrabadora/                 # 🎙️ Grabadora de audio
│   └── GestorGrabacion.cs        #   Captura WAV desde micrófono con NAudio (WaveInEvent)
│
└── AppDataFusion/                # 📊 Suite de ciencia de datos
    ├── Core/
    │   ├── Models/
    │   │   └── DataItem.cs       #   Modelo flexible con Dictionary<string,string> CamposExtra
    │   ├── Readers/
    │   │   ├── CsvDataReader.cs  #   Lector CSV robusto (Regex para comas en comillas)
    │   │   ├── JsonDataReader.cs #   Lector JSON con aplanamiento de objetos anidados
    │   │   ├── XmlDataReader.cs  #   Lector XML con inferencia de nodos
    │   │   └── TxtDataReader.cs  #   Lector de texto plano delimitado
    │   ├── Processing/
    │   │   └── DataProcessor.cs  #   QuickSort in-situ, filtrado, estadísticas
    │   ├── Database/
    │   │   ├── DatabaseWriter.cs #   Generador dinámico de DDL + INSERT transaccional
    │   │   ├── PostgreSqlConnector.cs  # Conector PostgreSQL (Npgsql)
    │   │   └── MariaDbConnector.cs     # Conector MariaDB (MySqlConnector)
    │   └── Services/
    │       ├── FileExportService.cs    # Exportación a .docx y .xlsx
    │       └── GeocodingService.cs     # Geocodificación inversa de coordenadas
    └── UI/
        ├── MainForm.cs           #   Panel de control: grilla, gráficas, conexión a BBDD
        ├── MainForm.Designer.cs  #   Layout auto-generado
        ├── ChartPanel.cs         #   Gráficas estadísticas (barras, pie, líneas)
        └── Dialogs.cs            #   Diálogos de conexión y configuración
```

---

## 🧩 Módulos del sistema

### 🧭 Form1 — El enrutador central

El corazón de la aplicación. Muestra el árbol de directorios de Windows en un `ListView` con íconos nativos del sistema operativo (extraídos vía `Shell32.dll` con P/Invoke). Cuando detectas un doble clic:

1. **¿Es carpeta?** → Apila la ruta actual en el historial (`Stack<string>`) y navega.
2. **¿Es archivo?** → Extrae la extensión y lanza el módulo correspondiente con un `switch`.

Usa **clases parciales** (`partial class`) para separar responsabilidades en archivos diferentes sin romper la cohesión del formulario.

### 🖼️ AppFoto — Editor fotográfico GDI+

Motor de procesamiento de imágenes que trabaja directamente con la API gráfica de Windows (GDI+):

- **Filtros con `ColorMatrix`:** Cada filtro (Blanco y Negro, Sepia, Soft) es una matriz de 5×5 que transforma los canales RGBA de cada píxel en un solo paso de GPU.
- **EXIF y GPS:** Lee los tags binarios de las fotos (orientación, cámara, fecha, coordenadas) y puede **inyectar coordenadas GPS** escribiendo directamente los bytes Rational EXIF al archivo JPEG.
- **Mapas interactivos:** Genera HTML en memoria con `Leaflet.js` y lo renderiza dentro de un control `WebView2`. El usuario puede hacer clic en el mapa y las coordenadas viajan de JavaScript a C# mediante `window.chrome.webview.postMessage`.

### 🎵 Mp3 — Reproductor de música

- **Motor de audio:** Usa `NAudio` (`WaveOutEvent` + `AudioFileReader`) para comunicarse directamente con los drivers de audio del sistema operativo (WASAPI/DirectSound), sin depender de `MediaPlayer`.
- **Letras automáticas:** Hace una petición HTTP `async` a `api.lyrics.ovh` y parsea la respuesta JSON con `System.Text.Json` sin crear clases modelo.
- **Carátulas y Edición de Metadatos:** Extrae las imágenes embebidas en los tags ID3 del MP3 usando `TagLibSharp`. Permite **editar y guardar directamente en el archivo físico el título, artista y portada** (usando `MetadataEditorForm`), liberando y reanudando de forma automática el lector de audio para evadir bloqueos de acceso de NAudio.

### 🎥 AppVideo — Reproductor y procesador de video

- **Reproducción:** Usa `LibVLCSharp` (el motor de VLC Media Player) embebido nativamente en el formulario.
- **Procesamiento vía FFmpeg:** Invoca el binario de FFmpeg como un proceso hijo invisible (`CreateNoWindow = true`), agregando el parámetro `-nostdin` para prevenir bloqueos interactivos. Para convertir el evento `Process.Exited` en una promesa `async/await`, utiliza el patrón `TaskCompletionSource<bool>`.
- **Recorte Integrado No Bloqueante:** Implementa una interfaz de selección de tiempo (Inicio y Fin) y confirmación directamente acoplada a la barra de reproducción inferior, ejecutando la operación asíncronamente en segundo plano sin congelar la ventana del explorador principal.
- **Geolocalización Híbrida y Metadatos (Android + iOS):** Extrae de forma autónoma metadatos técnicos (duración, resolución, codec) usando `TagLibSharp`. Adicionalmente, cuenta con un parser binario nativo que extrae la geolocalización desde videos grabados por dispositivos **Android** (caja `©xyz`) y **iOS/iPhone** (resolviendo el árbol de átomos `keys` e `ilst` para la clave `com.apple.quicktime.location.ISO6709`), cargando automáticamente la ubicación en un mapa interactivo (Leaflet.js/WebView2).

### 📊 AppDataFusion — Suite de ciencia de datos

El módulo más complejo. Permite cargar archivos de datos con **esquemas desconocidos** y manipularlos sin necesidad de definir clases de antemano:

- **Lectura polimórfica:** Cuatro lectores (`CsvDataReader`, `JsonDataReader`, `XmlDataReader`, `TxtDataReader`) convierten cualquier formato a una lista uniforme de `DataItem`. El modelo usa un `Dictionary<string, string> CamposExtra` para capturar columnas que no se conocen en tiempo de compilación.
- **Virtualización de grilla:** Para no saturar la RAM, la `DataGridView` solo muestra los primeros 75,000 registros de cualquier dataset.
- **Filtrado Avanzado:** Implementa búsqueda exacta y sensible a mayúsculas cuando el término se escribe entre comillas dobles (ej. `"valor"`), además de filtrado y ordenamiento nativo para columnas de coordenadas (`latitude` y `longitude`).
- **Exportación por Correo:** Envío directo de los registros visualizados mediante la generación automática de un archivo CSV temporal en el directorio `%TEMP%` y la apertura del cliente de correo nativo (`mailto:`).
- **Migración Masiva Bulk a BBDD:** En lugar de inserciones secuenciales unitarias, genera dinámicamente sentencias optimizadas de carga por lote. Utiliza el importador binario `NpgsqlBinaryImporter` para PostgreSQL (protocolo `COPY BINARY`) y `MySqlBulkCopy` en MariaDB (haciendo uso de `AllowLoadLocalInfile=true`), logrando velocidades de importación asíncronas ultrarrápidas de miles de registros por segundo.

### 📷 AppCamara — Captura de webcam nativa

Usa **P/Invoke** a `avicap32.dll` (API de Windows para captura de video) para abrir un canal directo con el driver USB de la cámara web, sin librerías externas de alto nivel.

### 🎙️ AppGrabadora — Grabación de audio

Captura audio del micrófono usando `NAudio` (`WaveInEvent`) y lo escribe directamente a un archivo `.wav` en disco en tiempo real, byte por byte, usando `WaveFileWriter`.

### ✉️ Envío de Correos y Cliente SMTP

El sistema provee dos mecanismos para compartir y enviar archivos por correo electrónico:
- **Envío Directo SMTP (`SendMailForm`):** Disponible mediante clic derecho en cualquier archivo del explorador -> "Enviar por correo". Levanta una interfaz clásica que implementa un cliente SMTP autónomo usando `System.Net.Mail.SmtpClient`. Permite configurar el servidor de salida, puerto (STARTTLS/SSL) y credenciales. Almacena la configuración en un archivo local `smtp_config.json` en `%APPDATA%` y realiza el envío de forma asíncrona (`Task.Run`). Por motivos de seguridad modernos de proveedores (como Gmail u Outlook), requiere el uso de una **Contraseña de Aplicación** (App Token) en lugar del password normal. Limita el tamaño de adjuntos a un máximo de **25 MB**.
- **Compartición Nativa MAPI / mailto (`EmailService` / `AppDataFusion`):** La exportación rápida en DataFusion y el servicio `EmailService` sirven como fallbacks rápidos. Utilizan llamadas a `Mapi32.dll` o al esquema URI `mailto:` para lanzar el cliente nativo del sistema operativo (Outlook, Thunderbird, etc.), delegando la transferencia al software de correo local del usuario.

### 🔎 QuickLook — Vista previa rápida (Estilo macOS)

Permite previsualizar archivos de forma ultrarrápida presionando la barra espaciadora:
- **Barra de título interactiva:** Permite arrastrar la ventana y maximizarla/restaurarla mediante doble clic.
- **Semáforos de control:** Botones de control al estilo macOS (cerrar rojo, minimizar amarillo, maximizar verde) dibujados a mano con suavizado de bordes mediante GDI+ (`SmoothingMode.AntiAlias`).
- **Soporte extendido:** Soporta previsualizaciones de imágenes, PDFs, páginas web y una amplia selección de archivos de texto/código (como `.cs`, `.html`, `.css`, `.js`, `.py`, `.bat`, `.cmd`, `.dat`, `.md`, etc.) en un control enriquecido con scroll automático.
- **Persistencia:** Remueve el cierre automático al desactivar la ventana, permitiendo al usuario interactuar libremente con los botones de control de la barra.

---

## 📦 Dependencias

Todas las dependencias se instalan automáticamente con `dotnet restore`. Están declaradas en el archivo `ExploradorArchivos.csproj`:

| Paquete NuGet | Versión | Para qué se usa |
|---|---|---|
| `NAudio` | 2.2.1 | Reproducción y grabación de audio (PCM, WAV, MP3) |
| `TagLibSharp` | 2.3.0 | Lectura/escritura de metadatos ID3 y EXIF |
| `LibVLCSharp` + `LibVLCSharp.WinForms` | 3.9.3 | Reproducción de video embebida |
| `VideoLAN.LibVLC.Windows` | 3.0.21 | Binarios nativos del motor VLC para Windows |
| `Microsoft.Web.WebView2` | 1.0.3912.50 | Renderizado web embebido (mapas, PDFs) |
| `DocumentFormat.OpenXml` | 3.5.1 | Generación de archivos `.docx` sin Microsoft Office |
| `ClosedXML` | 0.105.0 | Generación de archivos `.xlsx` sin Microsoft Office |
| `DocX` | 5.0.0 | Manipulación alternativa de documentos Word |
| `PdfSharpCore` | 1.3.67 | Generación y manipulación de PDFs |
| `Npgsql` | 9.0.2 | Conector ADO.NET para PostgreSQL |
| `MySqlConnector` | 2.3.7 | Conector ADO.NET para MariaDB/MySQL |
| `AForge.Video.DirectShow` | 2.2.5 | Acceso a dispositivos de captura de video |

**Dependencias externas (no NuGet):**
- **FFmpeg** — binario de línea de comandos para procesamiento de audio/video. Se invoca como proceso hijo.
- **LibreOffice** — suite ofimática utilizada de forma automática (en modo headless) para la exportación y conversión de alta fidelidad de archivos a PDF y otros formatos.

---

## ⌨️ Atajos de teclado

| Atajo | Acción |
|---|---|
| `Enter` | Abrir archivo o carpeta seleccionada |
| `Espacio` | Abrir o cerrar la vista previa rápida (QuickLook) |
| `Backspace` | Navegar a la carpeta anterior |
| `Delete` | Eliminar el archivo seleccionado |
| `F2` | Renombrar archivo |
| `Ctrl + C` | Copiar archivo |
| `Ctrl + V` | Pegar archivo |
| `Ctrl + E` | Exportar índice CSV del directorio actual |

---

## 📖 Documentación técnica

Para una explicación exhaustiva de cada clase, método, estructura de datos, flujo de ejecución y justificación de diseño, consulta:

📄 **[Documentacion_Maestra.md](Documentacion_Maestra.md)**

Este documento contiene:
- Justificación arquitectónica del sistema
- Manual de usuario por niveles de complejidad
- Diagrama de flujo del enrutador central (Mermaid)
- Diccionario completo de clases y métodos
- Fragmentos de código clave con explicación línea por línea

---

## 🛠️ Tecnologías

- **Lenguaje:** C# 12
- **Framework:** .NET 8.0 (Windows Forms)
- **IDE recomendado:** Visual Studio 2022
- **SO:** Windows 10 / 11

---

## 👤 Autor

Desarrollado por **Jimena Sanchez** y **Carolina Sustaita**.

---

<p align="center">
  Hecho con 💜 en C# y .NET 8
</p>
