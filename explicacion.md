# Form1, Services, Models, AppFoto, AppCamara, AppGrabadora, Mp3, AppVideo y AppDataFusion

Este documento ofrece una explicación completa y detallada de la arquitectura de la aplicación, analizando la clase principal `Form1` (dividida en archivos parciales), cada una de las clases de soporte del directorio `Services`, las de `Models`, las de las sub-aplicaciones `AppFoto`, `AppCamara`, `AppGrabadora`, `Mp3` y `AppVideo`, y las del módulo de integración `AppDataFusion`, así como la manera en que se complementan para lograr una separación clara de responsabilidades.

---

## 1. Clase Form1 (Clase Parcial)

`Form1` está implementado como una **clase parcial** (`partial class`) dividida en varios archivos físicos para separar las responsabilidades de la interfaz de usuario:

*   **[Form1.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Form1/Form1.cs)**: Inicialización base, constantes globales y lógica de menús contextuales.
*   **[Form1.Interaccion.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Form1/Form1.Interaccion.cs)**: Manejo de eventos del usuario (doble clic, drag & drop, atajos de teclado, búsquedas).
*   **[Form1.Navegacion.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Form1/Form1.Navegacion.cs)**: Lógica de carga de directorios reales/virtuales (Inicio, Favoritos, EsteEquipo), breadcrumbs y actualización del TreeView lateral.
*   **[Form1.Visualizacion.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Form1/Form1.Visualizacion.cs)**: Presentación del ListView principal, ordenamiento por columnas, chips de filtrado rápido y control del modo miniaturas.

### Propósito
Es el controlador y vista principal del explorador. Se encarga de capturar las acciones del usuario, mostrar el sistema de archivos (y las vistas virtuales), y redirigir el flujo a las aplicaciones correspondientes (Visor de texto, reproductor de música, cámara, convertidores de archivos, etc.).

### Estructuras de Datos Utilizadas
*   `List<FileSystemItem>`: Almacena los elementos cargados en memoria en el directorio actual.
*   `List<string>`: Usada para almacenar los accesos directos y los elementos favoritos persistentes.
*   `Dictionary<string, string>`: Mapea las carpetas del sistema del usuario (como Escritorio, Descargas, Documentos) en la vista virtual de Inicio.
*   `string[]`: Vectores estáticos para agrupar extensiones de archivos (`ImgExtensions`, `MediaExtensions`, `VideoExtensions`, `TextExtensions`, `DataExtensions`).
*   `ListViewGroup`: Para estructurar y agrupar visualmente elementos en el ListView.

### Declaraciones y Variables Clave
*   `_navigationService` (tipo `NavigationService`): Instancia para controlar el historial de retroceso.
*   `_rutaActual` (tipo `string`): Propiedad que encapsula la ruta física o virtual activa.
*   `_filtroActivo` (tipo `string`): Almacena la categoría de filtro seleccionada en los chips (ej. "Todos", "Imágenes", "Audio").
*   `_accesosDirectos` y `_elementosFavoritos` (tipo `List<string>`): Listas estáticas que contienen rutas favoritas.
*   Controles UI: `listViewPrincipal`, `treeViewLateral`, `txtBuscar`, `lblStatus`, `_flpBreadcrumbs` (panel de breadcrumbs).

### Uso de LINQ (Language Integrated Query)
*   **Filtrado y Ordenamiento**:
    ```csharp
    var itemsOrdenados = _itemsActuales
        .Where(x => _filtroActivo == "Todos" || x.CategoriaVisual == _filtroActivo)
        .OrderByDescending(x => x.FechaModificacion)
        .ToList();
    ```
*   **Búsqueda en memoria**:
    ```csharp
    var resultados = _itemsActuales
        .Where(x => x.Nombre.ToLower().Contains(filtro))
        .OrderByDescending(x => x.FechaModificacion)
        .ToList();
    ```
*   **Cálculo de Estadísticas**:
    ```csharp
    int carpetas = _itemsActuales.Count(x => x.EsCarpeta);
    int archivos = _itemsActuales.Count(x => !x.EsCarpeta);
    int img = _itemsActuales.Count(x => x.CategoriaVisual == "Imágenes");
    ```

### Bucles Utilizados
*   `foreach`:
    *   Para iterar los elementos seleccionados al copiar o mover archivos (`listViewPrincipal.SelectedItems`).
    *   Para generar dinámicamente los botones de los filtros rápidos ("Chips").
    *   Para agregar los nodos correspondientes en el TreeView lateral (`PoblarTreeViewNormal`).
*   `for`:
    *   Para construir los botones de la barra de direcciones (Breadcrumbs) a partir del separador de directorios:
        ```csharp
        for (int i = 0; i < partes.Length; i++) { ... }
        ```

---

## 2. Clases del Directorio Services

### A. [FileService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/FileService.cs)
*   **Propósito**: Encapsula las operaciones del sistema de archivos físico (crear, eliminar, mover, copiar, etc.). También implementa interoperabilidad nativa de Windows (P/Invoke) para enviar archivos a la Papelera de Reciclaje en lugar de borrarlos permanentemente.
*   **Estructuras de Datos**: Estructura `SHFILEOPSTRUCT` (configurada para interactuar con la API nativa `shell32.dll`), `List<FileSystemItem>` para devolver el contenido de un directorio.
*   **Declaraciones / Variables**: `FO_DELETE`, `FOF_ALLOWUNDO`, `FOF_NOCONFIRMATION` (constantes de la API de Windows).
*   **LINQ**: No utiliza.
*   **Bucles**: `foreach` para recorrer los elementos devueltos por `DirectoryInfo.GetDirectories()` y `DirectoryInfo.GetFiles()`, y para copiar directorios de manera recursiva; `while` en `FormatearTamano` para escalar bytes a su unidad legible.

### B. [NavigationService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/NavigationService.cs)
*   **Propósito**: Controla el historial de navegación para permitir retroceder en las carpetas visitadas.
*   **Estructuras de Datos**: `Stack<string>` (Pila LIFO) para guardar las rutas anteriores.
*   **Declaraciones / Variables**: `RutaActual` (propiedad de lectura/escritura) e historial en pila.
*   **LINQ / Bucles**: No requiere.

### C. [PersistenceService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/PersistenceService.cs)
*   **Propósito**: Carga y guarda en disco las configuraciones de texto persistentes, como la lista de favoritos y accesos directos.
*   **Estructuras de Datos**: `List<string>`, `IEnumerable<string>`.
*   **LINQ**: No requiere.
*   **Bucles**: Un bucle `foreach` para verificar si las líneas cargadas desde el archivo de texto corresponden a carpetas o archivos existentes físicamente antes de añadirlos.

### D. [RecentFilesService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/RecentFilesService.cs)
*   **Propósito**: Mantiene un registro de los últimos 15 archivos abiertos por el usuario y los guarda en `recientes.txt` (ubicado en `AppData\Local`).
*   **Estructuras de Datos**: `List<string>` para guardar las líneas leídas, y `List<FileInfo>` para representar los archivos existentes.
*   **LINQ**: Usa `Take(15)` para truncar y guardar únicamente los últimos 15 elementos, y `ToList()`.
*   **Bucles**: `foreach` para validar qué archivos históricos aún existen físicamente en el disco.

### E. [ThumbnailService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/ThumbnailService.cs)
*   **Propósito**: Carga imágenes de forma segura en memoria (evitando el bloqueo del archivo en disco) para generar vistas en miniatura y crear iconos basados en emojis usando GDI+.
*   **Estructuras de Datos**: `MemoryStream` para manejar los bytes de la imagen.
*   **LINQ / Bucles**: No utiliza.

### F. [CsvIndexer](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/CsvIndexer.cs)
*   **Propósito**: Escanea directorios de forma recursiva y asíncrona para generar un índice estadístico exportable a formato CSV.
*   **Estructuras de Datos**: `StreamWriter`, `DirectoryInfo`.
*   **Declaraciones / Variables**: `CancellationToken` (token para cancelación de tareas de fondo) e `IProgress<string>` (para reportar progreso a la UI).
*   **LINQ**: No requiere.
*   **Bucles**: `foreach` en la recursión para recorrer subdirectorios.

### G. [LoggerService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/LoggerService.cs)
*   **Propósito**: Registra errores inesperados escribiendo en un archivo de log físico (`app_errors.log`).
*   **Estructuras de Datos**: Cadenas de caracteres formateadas.
*   **LINQ / Bucles**: No requiere.

### H. [IFileConverter](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/IFileConverter.cs) (Interfaz)
*   **Propósito**: Define el contrato estándar de diseño (*Estrategia*) para todas las clases de conversión de archivos de la aplicación.
*   **Estructuras de Datos**: N/A.
*   **Declaraciones / Variables**: Firma del método `Convertir`.
*   **LINQ / Bucles**: N/A.

### I. [FileConverterService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/FileConverterService.cs)
*   **Propósito**: Motor universal de conversión de archivos. Resuelve nombres de destino únicos para evitar colisiones y provee un método centralizado (`ExtraerLineas`) para leer texto de varios formatos (.docx, .xlsx, .pptx y texto plano) usando bibliotecas de terceros.
*   **Estructuras de Datos**: `IEnumerable<string>` (retorno diferido con `yield return`), `System.Text.StringBuilder` (para reconstruir líneas eficientemente), arreglos estáticos de tipos (`extensionesTextoValidas`), clases del SDK de OpenXML (`DocX`, `XLWorkbook`, `PresentationDocument`).
*   **Declaraciones / Variables**:
    *   `directorioDestino`, `nombreSinExtension`, `rutaArchivoDestino` para armar la ruta final.
    *   `numeroIntento`: Contador entero para renombrar archivos colisionados (e.g. `Archivo (1).pdf`).
    *   `esArchivoImagen`: Bandera booleana.
*   **LINQ**: `extensionesTextoValidas.Contains(extension)` para evaluar si la extensión de entrada es de texto plano.
*   **Bucles**:
    *   `while (File.Exists(...))` para encontrar un nombre de destino libre.
    *   `foreach (char c in texto)` en `SanitizarTextoXml` para descartar caracteres de control XML inválidos.
    *   `for` anidado en la sección de `.xlsx` para recorrer filas y columnas de la hoja de cálculo.
    *   `foreach` anidado para extraer texto de las diapositivas de PowerPoint (`SlideId`, `TextBody`, `D.Paragraph`, `D.Run`).
    *   `while ((lineaTexto = lectorFlujo.ReadLine()) != null)` para leer archivos planos secuencialmente.

### J. [FileConverterFactory](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/FileConverterFactory.cs)
*   **Propósito**: Implementa el patrón *Simple Factory* para instanciar dinámicamente la clase conversora correcta basándose en el formato objetivo (.pdf, .docx, .xlsx, .pptx) y si es imagen.
*   **Estructuras de Datos**: Arreglos estáticos de cadenas (`extensionesDocumento` y `rutasCandidatas`).
*   **Declaraciones / Variables**: `rutaSOffice` (ubicación de LibreOffice si existe), `sourceExtension`, `targetExtension`.
*   **LINQ**: `rutasCandidatas.FirstOrDefault(File.Exists)` para encontrar el primer ejecutable de LibreOffice existente en el disco.
*   **Bucles**: N/A.

### K. [DocxFileConverter](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/DocxFileConverter.cs)
*   **Propósito**: Estrategia que convierte un archivo a formato de Microsoft Word (`.docx`). Si la fuente es una imagen, la inserta en el cuerpo; si es texto, escribe las líneas en párrafos de Word.
*   **Estructuras de Datos**: `Paragraph`, `Image`, `Picture` (del espacio de nombres `Xceed.Document.NET`).
*   **Declaraciones / Variables**: `lineasProcesadas` (entero), `limiteMaximoLineas` (constante establecida en `50000` para evitar desbordar memoria).
*   **LINQ**: No requiere.
*   **Bucles**: `foreach` para leer las líneas desde `FileConverterService.ExtraerLineas(rutaOrigen)`. Utiliza un condicional `lineasProcesadas % 100 == 0` para crear un nuevo párrafo físico en Word cada 100 líneas.

### L. [PdfFileConverter](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/PdfFileConverter.cs)
*   **Propósito**: Estrategia encargada de generar archivos PDF. Dibuja las imágenes directamente en la página con su escala proporcional original, o bien dibuja el texto línea por línea respetando los márgenes físicos de la página PDF y creando nuevas hojas automáticamente si hay desbordamiento vertical.
*   **Estructuras de Datos**: `PdfDocument`, `PdfPage`, `XGraphics`, `XImage`, `XFont` (de `PdfSharpCore`).
*   **Declaraciones / Variables**:
    *   `escalaProporcional`: Factor decimal (`double`) calculado con la relación de aspecto de la imagen versus el tamaño de la hoja PDF.
    *   `margenPagina`, `posicionY`, `posicionX`, `altoLinea`, `anchoDisponible` (variables de control de maquetación en la hoja).
*   **LINQ**: No requiere.
*   **Bucles**:
    *   `foreach (string lineaTexto in FileConverterService.ExtraerLineas(rutaOrigen))` para leer el texto.
    *   `foreach (string lineaAjustada in lineasFragmentadas)` para pintar las líneas que fueron ajustadas en ancho.
    *   `foreach (string palabra in palabras)` en `DividirTextoEnLineasCortas` para acumular texto y medir si cabe en el ancho máximo disponible (`MeasureString`).

### M. [LibreOfficeFileConverter](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/LibreOfficeFileConverter.cs)
*   **Propósito**: Conversor de documentos de Office de alta fidelidad que ejecuta LibreOffice en modo desatendido (`--headless`) para exportar a PDF, DOCX, XLSX o PPTX mediante la consola de comandos de Windows.
*   **Estructuras de Datos**: `ProcessStartInfo` para parametrizar el proceso del sistema.
*   **Declaraciones / Variables**: `_rutaSOffice` (ruta al ejecutable `soffice.exe`), `dirTemporal` (ruta de salida temporal generada mediante un `Guid`), `filtro` (cadena que indica a LibreOffice el formato de conversión exacto).
*   **LINQ / Bucles**: No requiere.

### N. [PptxFileConverter](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/PptxFileConverter.cs)
*   **Propósito**: Conversor que genera diapositivas de PowerPoint (`.pptx`) con cajas de texto autogeneradas a partir del contenido de texto origen.
*   **Estructuras de Datos**: Clases del SDK de OpenXML (`PresentationDocument`, `SlidePart`, `ShapeTree`, `Shape`, `TextBody`).
*   **Declaraciones / Variables**:
    *   `contadorLineasDiapositiva`: Entero para contar líneas físicas escritas en una sola diapositiva.
    *   `textoAcumuladoDiapositiva`: Cadena de texto acumulado.
    *   `indiceIdDiapositiva`: Identificador numérico secuencial (`uint`) para indexar las diapositivas creadas.
*   **LINQ**: No requiere.
*   **Bucles**: `foreach` para recorrer secuencialmente las líneas de texto del archivo original. Genera una nueva diapositiva cada vez que se superan las 22 líneas de texto.

### O. [XlsxFileConverter](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/XlsxFileConverter.cs)
*   **Propósito**: Estrategia que convierte un archivo a un libro de Microsoft Excel (`.xlsx`). Si el origen es un archivo delimitado (CSV o TXT), separa el texto por tabulaciones o comas e inyecta cada palabra en su celda (columna) correspondiente.
*   **Estructuras de Datos**: `XLWorkbook`, `IXLWorksheet` (de `ClosedXML.Excel`), arreglos de texto (`columnasTexto`).
*   **Declaraciones / Variables**:
    *   `indiceHoja`: Contador de pestañas del libro de Excel.
    *   `filaActualExcel`: Contador de renglones escritos.
    *   `caracterDelimitador`: Carácter para separar celdas (`\t` o `,`).
*   **LINQ**: No requiere.
*   **Bucles**:
    *   `foreach (string lineaTexto in FileConverterService.ExtraerLineas(rutaOrigen))` para avanzar línea por línea.
    *   `for (int indiceColumna = 0; ...)` para iterar las palabras separadas por delimitador e insertarlas en celdas consecutivas horizontales.

### P. [EmailService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/EmailService.cs)
*   **Propósito**: Gestiona el envío de correos utilizando la API nativa de Windows MAPI (Simple MAPI) para invocar al cliente de correo instalado por defecto (Outlook, Thunderbird, etc.) adjuntando el archivo seleccionado de forma nativa. Si MAPI falla, implementa fallbacks ejecutando comandos de consola directos para Outlook, Thunderbird o enlaces URI `mailto:`.
*   **Estructuras de Datos**: Estructuras no administradas estructuradas secuencialmente en memoria: `MapiMessage` y `MapiFileDesc`.
*   **Declaraciones / Variables**:
    *   `punteroArchivoMapi` (tipo `IntPtr`): Dirección de memoria no administrada para transferir los metadatos del adjunto a la API de Windows.
    *   `codigoError`: Entero devuelto por la API nativa.
*   **LINQ / Bucles**: No requiere.

### Q. [SmtpMailService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/SmtpMailService.cs)
*   **Propósito**: Provee lógica para guardar y cargar configuraciones de servidores de correo emisor SMTP en formato JSON, y procesa el envío de correos directos y adjuntos de manera asíncrona mediante el protocolo de red.
*   **Estructuras de Datos**: Clase de configuración `SmtpConfig`, clase `MailMessage`, y `Attachment`.
*   **Declaraciones / Variables**:
    *   `ConfigFilePath`: Ruta persistente del archivo JSON de configuración.
*   **LINQ / Bucles**: No requiere.

### R. [CameraCaptureService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/CameraCaptureService.cs)
*   **Propósito**: Abstrae el hardware y las APIs de cámara web (usando la biblioteca `AForge`). Permite listar cámaras disponibles, tomar instantáneas (guardando fotos como JPEG) y controlar grabaciones de video AVI que luego son transcodificadas asíncronamente a MP4.
*   **Estructuras de Datos**: `FilterInfoCollection` (lista de dispositivos de hardware conectados).
*   **Declaraciones / Variables**:
    *   `timestamp`: Cadena de fecha y hora para nombrar fotos y videos de forma única.
    *   `rutaAviTemporal` y `rutaMp4Final`: Rutas absolutas del video bruto y comprimido.
*   **LINQ / Bucles**: No requiere.

### S. [TextFileFormatterService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Services/TextFileFormatterService.cs)
*   **Propósito**: Lee y analiza estructuras de datos textuales populares (.json, .xml, .csv) para devolver una visualización formateada, indentada y con tabulaciones ordenadas que sea legible para el usuario en el visor interno.
*   **Estructuras de Datos**: `JsonDocument`, `XDocument`, `List<string[]>`.
*   **Declaraciones / Variables**: `content` (texto plano del archivo), `extension`, `nombreArchivo`, `maxLen` (máximo número de columnas de un CSV).
*   **LINQ**:
    *   `lines.Select(l => l.Split(',').Length).Max()` para obtener el número máximo de columnas en todo el archivo CSV.
    *   `lines.Select(l => l.Split(','))` para proyectar las líneas en arreglos individuales en una sola lista en memoria.
*   **Bucles**:
    *   `foreach (var row in grid)` para recorrer las filas del CSV y calcular el ancho máximo de caracteres que tiene cada columna en específico (`colsWidth[i]`).
    *   `for (int i = 0; i < row.Length; i++)` para rellenar con espacios a la derecha (`PadRight`) según el ancho máximo calculado y así alinear verticalmente el CSV como si fuera una tabla.

---

## 3. Clases del Directorio Models

### A. [FileSystemItem](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Models/FileSystemItem.cs)
*   **Propósito**: Clase de modelo de dominio o DTO (Data Transfer Object) que actúa como una abstracción unificada de un archivo o directorio físico del disco. Su propósito principal es encapsular todos los metadatos relevantes para que puedan ser mostrados de forma homogénea en la interfaz gráfica.
*   **Estructuras de Datos**: Colecciones externas como listas (`List<FileSystemItem>`) o agrupaciones.
*   **Declaraciones / Variables**:
    *   Propiedades autoimplementadas: `Nombre`, `RutaCompleta`, `EsCarpeta` (booleano), `Tipo`, `TamanoTexto`, `InfoAdicional`, `FechaModificacion`.
    *   Propiedad calculada `CategoriaVisual` con bloque `get` condicional.
*   **LINQ / Bucles**: No requiere (la lógica condicional se resuelve mediante declaraciones directas y de patrón `is`).

---

## 4. Clases del Directorio AppFoto

### A. [AppFotoMetadata](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppFoto/AppFotoMetadata.cs)
*   **Propósito**: DTO de metadatos de imágenes. Modela la información técnica y de ubicación geográfica extraída de la cabecera EXIF de una fotografía.
*   **Estructuras de Datos**: N/A.
*   **Declaraciones / Variables**: Propiedades autoimplementadas (`RutaArchivo`, `Nombre`, `FechaCaptura`, `ModeloCamara`, `Latitud`, `Longitud`, `Dimensiones`, `Resolucion`) y propiedad calculada booleana `TieneUbicacion`.
*   **LINQ / Bucles**: No requiere.

### B. [AppFotoExifService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppFoto/AppFotoExifService.cs)
*   **Propósito**: Decodifica de forma segura la cabecera binaria EXIF de imágenes (metadatos de cámara, fechas y coordenadas GPS en formato racional EXIF) y la mapea a un objeto `AppFotoMetadata`.
*   **Estructuras de Datos**: `Dictionary<int, PropertyItem>` creado para agilizar búsquedas de IDs de tags EXIF en memoria.
*   **Declaraciones / Variables**: `meta` (objeto `AppFotoMetadata`), `dNum`, `dDen` (y sus contrapartes `m` y `s` para minutos y segundos, de tipo `uint`), `degrees`, `minutes`, `seconds` (`double`).
*   **LINQ**: Convierte el arreglo de propiedades nativas en un diccionario ordenado por identificador EXIF: `img.PropertyItems.ToDictionary(p => p.Id)`.
*   **Bucles**: No utiliza.

### C. [AppFotoMapService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppFoto/AppFotoMapService.cs)
*   **Propósito**: Genera plantillas dinámicas en código HTML y JavaScript que integran la biblioteca **Leaflet.js** y mapas de **OpenStreetMap** para cargarse en controles `WebView2`. Provee tanto un visor estático (con marcador) como un selector interactivo ("Map Picker") que envía coordenadas cliqueadas por el usuario de vuelta a C#.
*   **Estructuras de Datos**: N/A.
*   **Declaraciones / Variables**: `latStr` y `lonStr` formateadas en el estándar invariant (con punto decimal en lugar de coma).
*   **LINQ / Bucles**: No utiliza.

### D. [AppFotoProcessor](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppFoto/AppFotoProcessor.cs)
*   **Propósito**: Motor de procesamiento gráfico que permite aplicar filtros cromáticos (BN, Sepia, Soft), ajustar brillo/contraste/saturación/sombras con matrices, rotar, recortar, dibujar trazos vectoriales o texto, y codificar metadatos GPS en archivos físicos JPEG.
*   **Estructuras de Datos**: Matrices flotantes de dimensión 5x5 (`ColorMatrix`), arreglos binarios para los bytes del encabezado EXIF (`byte[]`), y `Rectangle` para representar áreas de recorte.
*   **Declaraciones / Variables**: Coeficientes de ajuste normalizados (`b`, `c`, `s`, `l`, `sh`), constantes de luminancia NTSC (`lumR`, `lumG`, `lumB`), y valores racionales EXIF.
*   **LINQ**: No utiliza.
*   **Bucles**: `foreach` para recorrer los codecs disponibles del sistema en `GetEncoder`.

### E. [AppFotoForm](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppFoto/AppFotoForm.cs)
*   **Propósito**: Formulario gráfico para visualizar y editar imágenes. Permite al usuario ajustar brillo, aplicar filtros, recortar interactivamente, dibujar trazos con el ratón, escribir anotaciones de texto y geolocalizar la foto con el panel del mapa.
*   **Estructuras de Datos**: Listas de puntos (`List<Point>`) para el dibujo de trazos.
*   **LINQ / Bucles**: `foreach` para recorrer trazos y redibujarlos sobre el lienzo; `for` para iterar selectores visuales.

---

## 5. Clases del Directorio AppCamara

### A. [AviGrabador](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppCamara/AviGrabador.cs)
*   **Propósito**: Graba flujos de video en un contenedor físico AVI (.avi) de forma nativa e interactuando directamente con la API clásica de Windows **Video for Windows (VfW)** de `avifil32.dll` mediante interoperabilidad P/Invoke.
*   **Estructuras de Datos**: Estructuras nativas del sistema operativo (`AVISTREAMINFO`, `AVICOMPRESSOPTIONS`, `BITMAPINFOHEADER`).
*   **Declaraciones / Variables**: Manejadores de memoria no administrada (`_pFile`, `_pStream`, `_pCompressed`), `_lastWrittenFrameIndex` (entero), `_width` y `_height` (dimensions físicas del video).
*   **LINQ**: No requiere.
*   **Bucles**:
    *   `for (int y = 0; y < _height; y++)` para invertir el orden vertical de las filas de píxeles en memoria, dado que el formato DIB/BMP requiere almacenamiento Bottom-Up.
    *   `for (int f = startFrame; f <= targetFrameIndex; f++)` para escribir fotogramas duplicados si el grabador debe compensar retrasos en la captura física.

### B. [AppCamaraForm](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppCamara/AppCamaraForm.cs)
*   **Propósito**: Formulario de interfaz de usuario de la cámara. Permite seleccionar una cámara web, inicializar el feed de video en tiempo real, tomar capturas de fotos e iniciar/detener grabaciones de video de forma controlada.
*   **Estructuras de Datos**: Objetos de cámara (`VideoCaptureDevice` de AForge), `Stopwatch` para cronometrar la grabación, y `Queue` u objetos de fotogramas pendientes.
*   **Declaraciones / Variables**: `_videoSource` (cámara web activa), `_aviGrabador` (grabador activo), `_isRecording` (booleano), `_startTime` (DateTime), `_stopwatch` (cronómetro).
*   **LINQ**: No utiliza.
*   **Bucles**: `foreach` para poblar el combobox de cámaras disponibles en el equipo a partir de los dispositivos de entrada encontrados.

---

## 6. Clases del Directorio AppGrabadora

### A. [GestorGrabacion](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppGrabadora/GestorGrabacion.cs)
*   **Propósito**: Maneja la captura física de audio desde el micrófono del equipo usando la biblioteca **NAudio** y escribe de forma asíncrona los bytes de sonido capturados en un archivo de formato físico Wave (.wav) en el disco.
*   **Estructuras de Datos**: Búferes de bytes crudos administrados (`byte[]`).
*   **Declaraciones / Variables**: `_waveIn` (dispositivo de entrada `WaveInEvent`) y `_writer` (descriptor de archivos `WaveFileWriter`).
*   **LINQ / Bucles**: No requiere (basado en controladores de eventos asíncronos `DataAvailable` y `RecordingStopped`).

---

## 7. Clases del Directorio Mp3

### A. [Cancion](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/Cancion.cs)
*   **Propósito**: Modelo de datos que representa una pista musical. Utiliza la biblioteca **TagLib** para la inspección y carga física de metadatos ID3 de la canción (título, autor, disco, año, letras y carátula embebida), además de buscar portadas o letras de texto externas.
*   **Estructuras de Datos**: Colección de extensiones de archivos (`string[]`) y palabras clave para buscar portadas locales.
*   **Declaraciones / Variables**: Propiedades autoimplementadas del modelo (`RutaArchivo`, `Titulo`, `Artista`, `Album`, `Anio`, `Duracion`, `Letra`, `Portada`) y de interfaz (`InfoFormateada`, `DuracionTexto`).
*   **LINQ**:
    *   Filtra imágenes en la carpeta del audio mediante comparación de extensiones:
        ```csharp
        var archivosImagen = Directory.GetFiles(directorio)
            .Where(f => extensiones.Contains(Path.GetExtension(f).ToLower()))
            .ToList();
        ```
    *   Busca la carátula coincidente con palabras clave comunes:
        ```csharp
        var imagenCoincidente = archivosImagen.FirstOrDefault(img => {
            string nombre = Path.GetFileNameWithoutExtension(img).ToLower();
            return palabrasClave.Any(kw => nombre.Contains(kw));
        });
        ```
*   **Bucles**: No utiliza directamente.

### B. [GestorReproduccion](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/GestorReproduccion.cs)
*   **Propósito**: Motor principal del reproductor de música. Configura y controla el flujo de sonido y la salida WaveOut de **NAudio**, administrando la cola de canciones, la reproducción aleatoria y la repetición de pistas.
*   **Estructuras de Datos**: `List<Cancion>` para la cola de pistas, `List<int>` para almacenar el orden de reproducción, y la enumeración `ModoRepetir`.
*   **Declaraciones / Variables**: `_waveOut` (`WaveOutEvent`), `_audioReader` (`AudioFileReader`), `_timerPosicion` (Timer de UI para actualización), `_indiceCola` (entero).
*   **LINQ**:
    *   Proyecta rutas a objetos de canción válidos eliminando nulos:
        ```csharp
        var cancionesValidas = rutas
            .Select(ruta => { ... })
            .Where(c => c != null)
            .Cast<Cancion>();
        ```
    *   Genera el orden de índices original:
        ```csharp
        _ordenReproduccion = Enumerable.Range(0, _cola.Count).ToList();
        ```
*   **Bucles**:
    *   **Algoritmo de barajado Fisher-Yates** (`for` invertido) para la reproducción aleatoria:
        ```csharp
        for (int i = _ordenReproduccion.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            (_ordenReproduccion[i], _ordenReproduccion[j]) = (_ordenReproduccion[j], _ordenReproduccion[i]);
        }
        ```

### C. [LyricsService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/LyricsService.cs)
*   **Propósito**: Descarga asíncronamente las letras de las canciones desde la API pública de **LRCLIB** mediante llamadas HTTP.
*   **Estructuras de Datos**: `HttpClient` estático y `JsonDocument` para parsear la respuesta.
*   **Declaraciones / Variables**: `url` codificada para llamada HTTP y `response`.
*   **LINQ / Bucles**: No utiliza.

### D. [PortadaService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/PortadaService.cs)
*   **Propósito**: Descarga de forma asíncrona carátulas de álbumes de alta calidad desde la API pública de **iTunes** cuando no hay portadas locales.
*   **Estructuras de Datos**: `HttpClient` e `JsonDocument`.
*   **Declaraciones / Variables**: `artworkUrl` (reemplazando "100x100" por "600x600" para mayor definición de carátula).
*   **LINQ / Bucles**: No utiliza.

### E. [MetadataService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/MetadataService.cs)
*   **Propósito**: Persiste de forma segura las etiquetas de audio ID3 editadas por el usuario en el archivo de audio físico mediante la biblioteca **TagLib**.
*   **Estructuras de Datos**: `MemoryStream` para la conversión de la imagen de carátula.
*   **LINQ / Bucles**: No utiliza.

### F. [CustomTrackBar](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/CustomTrackBar.cs)
*   **Propósito**: Control de interfaz de usuario personalizado (Pastel/Moderno) que reemplaza la barra de progreso del sistema. Permite cambiar el volumen y el progreso de la canción.
*   **Estructuras de Datos**: Contenedor GDI+ para pintar los sliders en pantalla.
*   **LINQ / Bucles**: No requiere.

### G. [MusicPlayerForm](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/MusicPlayerForm.cs)
*   **Propósito**: Formulario gráfico del reproductor. Administra los clicks de los botones de control, muestra carátulas y letras, y provee visualización de la cola.
*   **LINQ / Bucles**: `foreach` para poblar el listado visual en el control de lista `ListBox`.
*   *Nota de Ordenamiento*: Las canciones aparecen en el reproductor ordenadas alfabéticamente debido a que los listados de directorios se obtienen nativamente usando `Directory.GetFiles()` y `Directory.GetDirectories()` en Windows (NTFS), los cuales retornan los archivos en orden alfabético por defecto.

### H. [MetadataEditorForm](file:///c:/Users/jimes/source/repos/ExploradorArchivos/Mp3/MetadataEditorForm.cs)
*   **Propósito**: Cuadro de diálogo interactivo para la edición de las etiquetas ID3 del archivo de audio seleccionado.

---

## 8. Clases del Directorio AppVideo

### A. [AppVideoMetadata](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppVideo/AppVideoMetadata.cs)
*   **Propósito**: DTO técnico que sirve como una "ficha de datos" para almacenar metadatos de un video (como su duración, resolución, codec, tamaño de archivo en bytes y coordenadas geográficas de captura).
*   **Estructuras de Datos**: N/A.
*   **Declaraciones / Variables**: Propiedades simples (`RutaArchivo`, `Nombre`, `Extension`, `Duracion`, `Resolucion`, `Codec`, `TamanoBytes`, `Latitud`, `Longitud`) y bandera calculada `TieneUbicacion`.
*   **LINQ / Bucles**: No utiliza.

### B. [AppVideoMapService](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppVideo/AppVideoMapService.cs)
*   **Propósito**: Genera dinámicamente plantillas HTML/JS inyectando la biblioteca de mapas **Leaflet.js** y **OpenStreetMap** para renderizarse en controles `WebView2`. Genera mapas estáticos de visualización con marcadores y mapas interactivos de tipo selector ("Picker") para registrar coordenadas GPS.
*   **Estructuras de Datos**: Cadenas HTML/JS.
*   **Declaraciones / Variables**: Coordenadas transformadas a cadena con formato de punto decimal universal.
*   **LINQ / Bucles**: No utiliza.

### C. [AppVideoProcessor](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppVideo/AppVideoProcessor.cs)
*   **Propósito**: Motor que interactúa de manera asíncrona con el software externo **FFmpeg** para recortar videos, aplicarles filtros cromáticos en blanco y negro, convertir videos de cámara (AVI a MP4) y extraer audio como MP3. Todos los comandos de FFmpeg incluyen el parámetro `-nostdin` para evitar bloqueos del proceso causados por la redirección interactiva. También cuenta con algoritmos a bajo nivel para escanear y encontrar coordenadas GPS decodificando la estructura binaria del video (cajas `©xyz` o metadatos QuickTime de Apple), con soporte de persistencia local en JSON.
*   **Estructuras de Datos**: Búferes de lectura (`byte[]`) y clases de control de procesos nativos (`ProcessStartInfo`).
*   **Declaraciones / Variables**: `FfmpegPath` (ruta del binario ejecutable) y variables de offsets de bytes.
*   **LINQ**: Obtiene la primera descripción del códec válida del listado extraído con `TagLib`: `System.Linq.Enumerable.FirstOrDefault`.
*   **Bucles**:
    *   `while` para lectura de streams.
    *   Bucles `for` para escanear coincidencias binarias de coordenadas en `FindPatternInRange` y `FindPatternBackwards`.

### D. [AppVideoForm](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppVideo/AppVideoForm.cs)
*   **Propósito**: Ventana y reproductor gráfico del estudio de video. Permite pausar y reproducir videos usando **LibVLCSharp** (VLC Engine), visualizar metadatos técnicos en el sidebar, y geolocalizar o registrar manualmente ubicaciones interactuando con mapas interactivos incrustados (`WebView2`). Integra los controles de recorte directamente en la barra de reproducción inferior (`txtTrimInicio`, `txtTrimFin`, `btnEjecutarRecorte`, `lblTrimStatus`) de forma asíncrona y no bloqueante para el explorador principal.
*   **Estructuras de Datos**: Controles UI estándar de Windows Forms (`Panel`, `TextBox`, `Button`, `Label`).

## 9. Clases del Módulo AppDataFusion (Core)

El módulo `AppDataFusion` en su sección `Core` provee lógica para la integración, mapeo, procesamiento y persistencia de datos relacionales y tabulares en memoria y en bases de datos externas.

### A. Directorio Models

#### * [DataItem.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Models/DataItem.cs)
*   **Propósito**: Es el modelo o "molde" universal de datos. Sirve para representar cualquier fila o registro de datos sin importar si proviene de archivos planos (CSV, JSON, XML, TXT) o de motores de base de datos (PostgreSQL, MariaDB).
*   **Estructuras de Datos**: `Dictionary<string, string>` (para `CamposExtra`) para almacenar columnas no mapeadas a campos estándar de forma dinámica e insensible a mayúsculas/minúsculas.
*   **Declaraciones / Variables Clave**: Propiedades autoimplementadas del modelo como `Id` (entero), `Nombre` (cadena), `Categoria` (cadena), `Valor` (double), `Fuente` (cadena), `Fecha` (DateTime), y `Latitude`/`Longitude` (double nulo).
*   **LINQ**: No utiliza.
*   **Bucles**: No utiliza.

### B. Directorio Services

#### * [GeocodingService.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Services/GeocodingService.cs)
*   **Propósito**: Servicio estático encargado de consultar la API pública de geocodificación Nominatim (OpenStreetMap) de forma controlada para traducir nombres de ubicación (ciudades, países) a coordenadas geográficas.
*   **Estructuras de Datos**: `HttpClient` compartido para reuso de sockets, `Dictionary<string, (double Lat, double Lon)?>` para caché en memoria de ubicaciones previamente resueltas, y un vector `_cityKeywords` de términos de búsqueda.
*   **Declaraciones / Variables Clave**: `MAX_GEOCODE_PER_BATCH` (límite de 20 peticiones simultáneas por lote de geocodificación) y `count` (contador de control).
*   **LINQ**: Filtra dinámicamente las claves de campos adicionales usando palabras clave de ubicación en minúsculas:
    ```csharp
    item.CamposExtra.Where(kv => _cityKeywords.Any(k => kv.Key.ToLowerInvariant().Contains(k)))
    ```
*   **Bucles**: `foreach` para iterar de manera controlada los elementos a geocodificar, aplicando una pausa con `await Task.Delay(1000)` entre consultas externas exitosas.

#### * [FileExportService.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Services/FileExportService.cs)
*   **Propósito**: Proveedor de servicios estáticos para guardar (exportar) colecciones de `DataItem` a formatos CSV, JSON, XML y TXT, incluyendo su serialización en memoria a arrays de bytes.
*   **Estructuras de Datos**: Listas de cadenas (`List<string>`) para buffering de filas de texto y `StringBuilder` para estructurar la serialización de XML.
*   **Declaraciones / Variables Clave**: `tmp` (ruta a archivos temporales generados con GUIDs aleatorios para recuperación segura de bytes).
*   **LINQ**:
    *   Para proyección rápida de campos y delimitación de celdas en CSV y TXT:
        ```csharp
        lineas.AddRange(datos.Select(item => string.Join(",", cols.Select(c => EscapeCsv(GetValorExport(item, c, map))))));
        ```
    *   Para extracción de columnas únicas dinámicas presentes en `CamposExtra`:
        ```csharp
        var extras = datos.SelectMany(d => d.CamposExtra.Keys).Distinct(StringComparer.OrdinalIgnoreCase)...
        ```
*   **Bucles**: Se apoya principalmente en la proyección declarativa de LINQ en lugar de bucles tradicionales.

### C. Directorio Processing

#### * [DataProcessor.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Processing/DataProcessor.cs)
*   **Propósito**: Agrupa la lógica principal de computación en memoria: ordenamiento dinámico, filtrado de texto parcial o exacto, filtrado numérico con coincidencia flexible, indexación rápida y algoritmos para detectar y eliminar filas duplicadas.
*   **Estructuras de Datos**: `Dictionary<string, List<DataItem>>` para agrupamiento, `Dictionary<int, DataItem>` para índices por identificador único, y `Dictionary<string, bool>` para registro rápido de firmas duplicadas.
*   **Declaraciones / Variables Clave**: `exactMatch` (bandera booleana deducida mediante la sintaxis del valor de búsqueda, e.g. si viene entre comillas dobles).
*   **Filtrado (`Filtrar`)**:
    *   **Búsqueda Exacta y Parcial**: Detecta si el valor contiene comillas `"valor"` para forzar coincidencia exacta, de lo contrario usa `Contains`.
    *   **Numérico Flexible (`CheckNumero`)**: Transforma los números a representaciones completas (`G`), truncadas (`F2`) y enteras para que buscar `"100"` coincida con `100.00` y `100`.
    *   **Campos Dinámicos**: Busca la clave solicitada en `CamposExtra` de forma insensible a mayúsculas. Si la columna buscada no existe en el registro, hace una búsqueda general en todas las propiedades del objeto.
*   **Ordenamiento (`OrdenarLinq`)**: Usa `LINQ OrderBy` invocando a `ObtenerLlaveOrdenamiento()`, que mapea el nombre de la columna al tipo de dato nativo real (e.g. `double` para el Valor, `DateTime` para la Fecha). Esto evita los errores comunes de ordenar números como si fueran texto (ej. evitar que `10` aparezca antes que `2`).
*   **Bucles**: Bucles indexados `for` e `foreach` tradicionales para recorrer colecciones al filtrar coincidencias elemento por elemento.

#### * [DataQualityAnalyzer.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Processing/DataQualityAnalyzer.cs)
*   **Propósito**: Analizador de calidad de datos que escanea el conjunto de registros (`DataItem`) e identifica anomalías (campos vacíos, correos electrónicos mal formateados, teléfonos inconsistentes, fechas inválidas o con formatos incorrectos y duplicados), produciendo un informe detallado con sugerencias de corrección.
*   **Estructuras de Datos**: `QualityReport` (modelo de salida que encapsula anomalías), `Dictionary` para el rastreo de firmas duplicadas e `ItemErrors` (mapeo del item a diccionarios de errores).
*   **Declaraciones / Variables Clave**: Expresión regular `EmailRegex` para validación y listas de palabras clave para identificación semántica de columnas (`PhoneKeywords`, `EmailKeywords`, `DateKeywords`).
*   **LINQ**: Determina si las columnas mapeadas corresponden semánticamente a teléfonos, correos o fechas:
    ```csharp
    PhoneKeywords.Any(k => cLow.Contains(k))
    ```
*   **Bucles**: `foreach` para validar cada columna en cada uno de los registros del conjunto de datos.

### D. Directorio Readers

#### * [CsvDataReader.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Readers/CsvDataReader.cs)
*   **Propósito**: Lector que procesa archivos CSV en streaming. Cuenta con un parser robusto que divide textos respetando las comillas, corchetes y llaves de sub-objetos incrustados. Mapea cabeceras a roles estándar usando un diccionario global de equivalencias.
*   **Estructuras de Datos**: `StreamReader` de streaming físico y `HashSet<int>` para evitar la duplicidad de índices mapeados al poblar `CamposExtra`.
*   **Declaraciones / Variables Clave**: `enComillas` (booleano) y `profCorchete`/`profLlave` (control de profundidad de caracteres en parseo).
*   **LINQ**: Sanitiza e identifica cabeceras usando métodos funcionales:
    ```csharp
    SepararCsvRobust(lineaEncabezado, separador).Select(h => h.Trim().Replace("\"", "")).ToArray()
    ```
*   **Bucles**: `while` para la lectura asíncrona línea por línea y `for` para iterar los caracteres de cada cadena.

#### * [JsonDataReader.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Readers/JsonDataReader.cs)
*   **Propósito**: Lector inteligente de archivos JSON. Detecta de forma adaptativa el tipo de estructura de entrada (array de objetos, matriz de arrays, o formato tabular fields/records). Provee un lector sintáctico de emergencia para extraer objetos delimitados por llaves `{}` ante archivos corruptos.
*   **Estructuras de Datos**: `JsonDocument` y `JsonElement` para manipulación de árboles de nodos JSON.
*   **Declaraciones / Variables Clave**: Listas de alias estáticas de cabeceras (`_nombreKeys`, `_categoriaKeys`, etc.).
*   **LINQ**:
    *   Evalúa dinámicamente si la fila es de tipo cabecera:
        ```csharp
        primeraFila.All(e => e.ValueKind == JsonValueKind.String)
        ```
    *   Convierte sub-arrays del documento JSON a cadenas unificadas:
        ```csharp
        prop.Value.EnumerateArray().Select(e => e.GetString() ?? e.ToString()).ToList()
        ```
*   **Bucles**: `foreach` para recorrer nodos internos del árbol JSON y `while` en la búsqueda de balances de llaves.

#### * [TxtDataReader.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Readers/TxtDataReader.cs)
*   **Propósito**: Lector que procesa archivos de texto plano (.txt), infiriendo estadísticamente el mejor separador de campos y determinando heurísticamente si la primera línea contiene datos o encabezados.
*   **Estructuras de Datos**: Listas de cadenas y codificaciones UTF-8 con detección de marcas BOM.
*   **Declaraciones / Variables Clave**: Candidatos de delimitadores `candidatos` (`|`, `\t`, `;`, `,`) y puntaje de consistencia `mejorScore`.
*   **LINQ**: Cuenta qué elementos de los tokens coinciden con un formato genérico de cadena descriptiva en lugar de números o fechas:
    ```csharp
    tokens.Count(t => { ... })
    ```
*   **Bucles**: `foreach` para la muestra estadística y `while` para procesar el resto de líneas del stream.

#### * [XmlDataReader.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Readers/XmlDataReader.cs)
*   **Propósito**: Lector que interpreta archivos estructurados XML. Resuelve de manera automática namespaces o envolturas jerárquicas multinivel y normaliza mediante expresiones regulares nombres de etiquetas inválidos que contengan espacios.
*   **Estructuras de Datos**: `XDocument` y `XElement` para navegar los nodos del árbol XML.
*   **Declaraciones / Variables Clave**: `_tagConEspacios` (expresión regular compilada para limpieza pre-parseo).
*   **LINQ**: Une y localiza los nombres locales de atributos y elementos para mapearlos:
    ```csharp
    primerElemento.Attributes().Select(a => a.Name.LocalName).Concat(primerElemento.Elements().Select(e => e.Name.LocalName)).ToList()
    ```
*   **Bucles**: `foreach` para recorrer y mapear cada registro de datos del XML.

### E. Directorio Database

#### * [MariaDbConnector.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Database/MariaDbConnector.cs) y [PostgreSqlConnector.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Database/PostgreSqlConnector.cs)
*   **Propósito**: Conectores de acceso a datos para PostgreSQL y MariaDB/MySQL. Llevan a cabo pruebas de conectividad activa, recuperación ágil de metadatos de esquema, autodetectar claves primarias (o UNIQUE index) y mapeo de registros a `DataItem`.
*   **Estructuras de Datos**: Conexiones físicas (`NpgsqlConnection`, `MySqlConnection`) y lectores indexados (`NpgsqlDataReader`, `MySqlDataReader`).
*   **Declaraciones / Variables Clave**: `ColPrimaryKey` (nombre de la columna llave).
*   **Sincronización en Tiempo Real (`ActualizarCampoAsync`)**: Si el usuario edita una celda en la grilla visual, esta clase envía un comando `UPDATE` instantáneo al motor de base de datos usando el `ID` de la fila.

#### * [DatabaseWriter.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Database/DatabaseWriter.cs)
*   **Propósito**: Se encarga de la **migración masiva (Bulk Migration)** de los datos leídos en memoria hacia las bases de datos SQL. Construye consultas parametrizadas de alto rendimiento transaccional.
*   **Proceso de Migración (PostgreSQL / MariaDB)**:
    1.  **DDL (Data Definition Language)**: Si la tabla no existe, genera dinámicamente el bloque `CREATE TABLE`. Infiriendo todas las columnas dinámicas presentes en `CamposExtra`.
    2.  **Bulk Insert Transaccional**: No envía un `INSERT` por cada fila (lo que sería muy lento). En su lugar, usa `MySqlBulkLoader` para MariaDB (que crea un archivo temporal CSV interno y lo carga con `LOAD DATA LOCAL INFILE` en milisegundos), y el equivalente `Npgsql` por lotes bajo transacciones (`BEGIN TRANSACTION`).
    3.  **UI Feedback**: Mientras se procesa la migración o si ocurre un fallo en los tipos de datos, propaga errores o reporta progreso asíncronamente para que `MainForm` actualice el _Toast_ de estado y la UI no se congele.
    ```csharp
    items.SelectMany(i => i.CamposExtra.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Select(key => { ... })
    ```
*   **Bucles**: `while (reader.Read())` para avanzar y poblar la colección y `foreach` para calificar columnas candidato.

#### * [DatabaseWriter.cs](file:///c:/Users/jimes/source/repos/ExploradorArchivos/AppDataFusion/Core/Database/DatabaseWriter.cs)
*   **Propósito**: Utilidad centralizada para escribir o actualizar registros en PostgreSQL o MariaDB de manera asíncrona. Soporta migración masiva optimizada (Bulk) de alta velocidad usando `NpgsqlBinaryImporter` (vía comando `COPY BINARY` nativo de PostgreSQL) y `MySqlBulkCopy` (vía `LOAD DATA LOCAL INFILE` en MariaDB). Incluye scripts automáticos DDL para crear las tablas físicas infiriendo la estructura del modelo.
*   **Estructuras de Datos**: `NpgsqlBinaryImporter`, `MySqlBulkCopy`, `DataTable` y DTOs de control como `WriteResult`.
*   **Declaraciones / Variables Clave**: `progreso` (`IProgress<int>` para reportar porcentaje de carga masiva en lote), `AllowLoadLocalInfile` en MariaDB (para habilitar streaming cliente-servidor) e `idEsEntero` (bandera).
*   **LINQ**: Sanitiza nombres de cabeceras de columnas ingresadas por el usuario eliminando duplicados:
    ```csharp
    columnas.Select((c, i) => $"    `{c.NombreDB}` {TipoSQL(c.Clave, i)}")
    ```
*   **Bucles**: `foreach` para poblar el `DataTable` en MariaDB y escribir filas binarias secuenciales en PostgreSQL (`writer.StartRow()`).

---

## 10. ¿Cómo se complementan todos los módulos y sub-aplicaciones?

Esta arquitectura sigue el principio de **Separación de Responsabilidades** (Separation of Concerns):

1.  **Modelo de Datos Unificado (Models)**:
    `Models` (a través de `FileSystemItem` y `DataItem`) define la estructura básica de los datos. Esta estructura actúa como el lenguaje común entre los servicios y la UI.
2.  **Desacoplamiento de Interfaz y Lógica de Negocio (Services)**:
    `Form1` no contiene llamadas directas complejas a APIs del sistema operativo, ni interactúa con archivos de texto directamente. En su lugar, delega estas tareas a los servicios. Por ejemplo, `Form1` llama a `FileService.MoverArchivo` o `FileService.EnviarAPapelera`.
3.  **Gestión de Estado Centralizada**:
    El historial y la dirección de navegación se gestionan a través de `NavigationService`, lo cual previene inconsistencias de la ruta actual entre el árbol de navegación lateral (`TreeView`) y la vista principal (`ListView`).
4.  **Persistencia Transparente**:
    `Form1` llama a `CargarDatosPersistentes` y `GuardarDatosPersistentes` usando `PersistenceService` para guardar favoritos y accesos directos de manera automática, liberando al formulario de administrar directorios de sistema como `AppData`.
5.  **Optimización Asíncrona (Background Workers)**:
    Cuando el usuario presiona el botón "Exportar CSV" o entra a una carpeta con imágenes, `Form1` inicia tareas asíncronas (`Task.Run`) que llaman a `CsvIndexer.ExportarAsync` o `ThumbnailService.GenerarMiniatura`. Esto mantiene la interfaz de usuario fluida y libre de congelamientos, mientras que los servicios procesan los datos en hilos secundarios.
6.  **Extensibilidad en Formatos de Exportación**:
    Al usar la fábrica `FileConverterFactory` y la interfaz `IFileConverter`, el código del menú contextual de `Form1` permanece limpio. Si se añade un nuevo formato de destino, solo es necesario crear su correspondiente estrategia `IFileConverter` e incluirlo en la fábrica, sin modificar la interfaz gráfica principal.
7.  **Formateo y Envío de Información**:
    Cuando se ejecutan operaciones específicas como visualizar un archivo especial o enviar un correo, `Form1` se comunica con `TextFileFormatterService` para embellecer los contenidos, y con `EmailService` / `SmtpMailService` para realizar el envío, garantizando una arquitectura robusta y altamente mantenible.
8.  **Flujo Continuo de Captura y Edición de Imágenes (AppCamara y AppFoto)**:
    *   **Captura**: El usuario puede usar la cámara web integrada desde `AppCamaraForm` (usando `CameraCaptureService` y `AviGrabador`) para guardar fotos o videos locales.
    *   **Detección**: Una vez guardado el archivo en el directorio, `Form1` refresca su listado de forma automática cargando la lista de `FileSystemItem`.
    *   **Edición y Geolocalización**: Al abrir la imagen capturada, `AppFotoForm` carga la foto y sus metadatos EXIF (`AppFotoExifService`). Si el usuario decide geolocalizar la foto, interactúa con el visor `AppFotoMapService` (WebView2) y `AppFotoProcessor` guarda las coordenadas en la cabecera EXIF de la imagen. Esto cierra el ciclo completo de captura de video/fotos y su respectivo procesamiento de metadatos/imágenes.
9.  **Flujo de Creación, Edición y Escucha de Audio (AppGrabadora y Mp3)**:
    *   **Captura**: Un usuario puede capturar su voz o una pista acústica con `AppGrabadora` (`GestorGrabacion`). Al detener la grabación, se genera un archivo de audio estructurado físicamente en formato Wave (.wav).
    *   **Detección y Catalogación**: El Explorador de Archivos (`Form1`) detecta el nuevo archivo en disco y lo clasifica automáticamente en la categoría "Audio" debido a su extensión.
    *   **Reproducción Directa**: Al hacer doble clic en el archivo grabado, `Form1` invoca la ventana `MusicPlayerForm`. Esta ventana delega la carga y reproducción a `GestorReproduccion`.
    *   **Enriquecimiento de Metadatos**: El usuario puede hacer clic derecho en la canción para abrir el `MetadataEditorForm`, donde introduce información útil (título, autor, e imagen de carátula local). `MetadataService` inyecta de forma permanente estos metadatos en las cabeceras binarias del archivo. Al reproducir de nuevo, `MusicPlayerForm` lee la información ingresada mostrándola en la UI del reproductor.
10. **Flujo de Visualización, Edición y Georreferenciación de Video (AppVideo y AppCamara)**:
    *   **Origen**: Un video puede grabarse desde `AppCamaraForm` (usando la cámara y `AviGrabador`) o detectarse en carpetas físicas del equipo.
    *   **Análisis**: Al abrir el video, `AppVideoForm` lee los metadatos técnicos y geográficos decodificando los bytes del archivo binario (`AppVideoProcessor`).
    *   **Mapeo**: Si el video cuenta con GPS, `AppVideoMapService` inyecta los datos en un mapa de OpenStreetMap sobre `WebView2`. Si no cuenta con coordenadas, se habilita el modo "Picker" interactivo para asignarle coordenadas manualmente y guardarlas en un archivo companion `.meta.json`.
    *   **Edición**: Al aplicar filtros (B&N) o extraer audio, el reproductor LibVLC se desconecta temporalmente de forma asíncrona para liberar el archivo y permitir que `AppVideoProcessor` ejecute **FFmpeg** en segundo plano, reemplazando el video original una vez terminado el procesamiento de forma segura.
11. **Flujo de Integración y Fusión de Datos Tabulares (Módulo AppDataFusion)**:
    *   **Carga de Datos Multi-Fuente**: El módulo lee archivos CSV, JSON, XML y TXT mediante sus lectores específicos (`CsvDataReader`, `JsonDataReader`, `XmlDataReader`, `TxtDataReader`) y bases de datos relacionales (`MariaDbConnector`, `PostgreSqlConnector`).
    *   **Estandarización y Normalización**: Toda la información se convierte a una lista compartida de objetos `DataItem`, donde sus columnas variables se inyectan en `CamposExtra`.
    *   **Enriquecimiento de Coordenadas**: La lista de registros puede enviarse a `GeocodingService` para buscar asíncronamente en internet la latitud/longitud de las filas que tengan una ciudad asignada.
    *   **Procesamiento y Limpieza**: Los registros se depuran de duplicados y se filtran u ordenan en memoria llamando a los algoritmos estáticos en `DataProcessor`.
    *   **Persistencia y Exportación**: Finalmente, la lista limpia puede guardarse en un nuevo archivo utilizando `FileExportService` o subirse a tablas físicas de bases de datos mediante `DatabaseWriter`, completando el pipeline de fusión de datos.
