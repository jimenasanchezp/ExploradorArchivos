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
*   **Música (`.mp3`, `.wav`):** Se abrirá el `MusicPlayerForm`. Soporta listas de reproducción, modo aleatorio (shuffle) con restauración al orden secuencial original al desactivarse, muestra las carátulas incrustadas, descarga la letra de la canción de internet automáticamente y **permite editar el título, artista y foto de portada (cover)** haciendo click en el botón ✏️ o mediante click derecho en cualquier elemento de la cola.
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
7.  **`LibreOffice` (Herramienta Externa):** Suite ofimática invocada en modo headless (`soffice.exe --headless`) para la conversión y exportación de documentos (DOCX, XLSX, PPTX, TXT) a PDF u otros formatos con fidelidad total del 100%.

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

### E) Flujo de Envío de Archivos por Correo (Protocolo SMTP)
El explorador permite enviar cualquier archivo adjunto directamente desde su interfaz sin depender de un cliente de correo local, utilizando el protocolo de red estándar **SMTP** (Simple Mail Transfer Protocol).
1. **Validación de Tamaño:** Antes de iniciar la conexión, el sistema comprueba el tamaño del archivo mediante `FileInfo.Length`. Si supera los **25 MB** (límite estándar para adjuntos en proveedores comunes como Gmail u Outlook), se cancela la operación informando al usuario para prevenir fallos en la transferencia SMTP.
2. **Carga y Almacenamiento de Configuración:** Las opciones de configuración del servidor SMTP se guardan localmente en `smtp_config.json` dentro del directorio `%APPDATA%\ExploradorArchivos`. En cada envío, el sistema lee este archivo JSON para prellenar el formulario y, si el envío tiene éxito, lo actualiza para futuras sesiones.
3. **Autenticación SMTP:** Para enviar correos, la aplicación requiere de credenciales (`NetworkCredential`) del remitente. Debido a los protocolos de seguridad modernos (como MFA y el fin del soporte a autenticación básica de Google/Microsoft), se requiere y advierte al usuario que debe utilizar una **Contraseña de Aplicación** (App Token) configurada en su proveedor, en lugar de su contraseña habitual.
4. **Envío Asíncrono en Segundo Plano:** El proceso de envío se ejecuta de forma asíncrona mediante `Task.Run` llamando al método `EnviarCorreoAsync()`. Esto mantiene la interfaz de usuario receptiva y previene bloqueos en la UI de Windows Forms mientras se establece el canal con el servidor SMTP.
5. **Establecimiento de Sesión y Envío:** Se inicializan las instancias de `MailMessage` (para definir remitente, destinatario, asunto, cuerpo y adjuntar el archivo vía `new Attachment(filePath)`) y `SmtpClient`. Se configura el host, el puerto (normalmente `587` para STARTTLS o `465` para SSL explícito), las credenciales de red y la habilitación de SSL/TLS (`EnableSsl = true`). Finalmente, se ejecuta `SmtpClient.Send` enviando el paquete del correo.

### F) Flujo de Conversión de Documentos Híbrido (LibreOffice + Fallback Interno C#)
La conversión de archivos (por ejemplo, al exportar o previsualizar documentos en formato PDF) se realiza a través de un flujo híbrido diseñado para garantizar tanto una alta fidelidad visual como la resiliencia del sistema:
1. **Punto de decisión y búsqueda:** El método `FileConverterService.Convertir` recibe la ruta del archivo origen y el formato de destino (e.g., `.pdf`). Llama internamente a `BuscarSOffice()`, el cual escanea las rutas de instalación predeterminadas de LibreOffice en Windows.
2. **Evaluación de condiciones:**
   - **Caso A (LibreOffice instalado y no es imagen):** El sistema levanta el proceso asíncrono `soffice.exe` en modo headless (`--headless --convert-to [filtro]`). El archivo se genera en un directorio temporal único bajo `%TEMP%` para evitar colisiones y luego se mueve al destino final. Se cuenta con un control de tiempo de espera (`Timeout`) de 3 minutos para prevenir bloqueos por documentos corruptos.
   - **Caso B (LibreOffice ausente o el archivo es una imagen):** Se redirige el flujo al motor fallback interno basado en C# (con `PdfSharpCore`, `ClosedXML` y `DocX`).
3. **Procesamiento Fallback:** Las funciones de fallback manipulan el contenido a bajo nivel, procesando por ejemplo archivos de texto línea por línea (limitado a 50,000 líneas en Word por consumo de RAM), particionando líneas de texto que exceden el ancho de la página en PDF, o aplanando tablas de Excel y distribuyéndolas en múltiples hojas si sobrepasan el límite de 1,000,000 de filas.

### G) Flujo de Captura y Grabación de Pantalla (AppCapturaPantalla)
El proceso de captura y grabación de pantalla está optimizado para hilos de fondo mediante un modelo asíncrono:
1. **Captura instantánea:** Minimiza el formulario del explorador, realiza una pausa de `250 ms` para limpiar la UI y copia los píxeles usando `Graphics.CopyFromScreen` directamente a un archivo PNG.
2. **Grabación de vídeo:** Inicializa `AviGrabador` guardando en disco a través de la transferencia directa de punteros de memoria de Bitmap (`Scan0`).
3. **Bucle asíncrono:** Ejecuta `BucleCapturaAsync` en un hilo secundario con `Task.Run` y control por token (`CancellationTokenSource`), manteniendo la tasa en ~15 FPS.
4. **Compresión:** Al detener la grabación, se invoca de manera asíncrona a `ffmpeg.exe` en segundo plano para convertir el archivo de contenedor temporal AVI sin comprimir a un archivo MP4 codificado en H.264 para uso final.

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

### 🎥 AppVideo (Visualización y Procesamiento)
*   `AppVideoForm`: Usa el control `LibVLCSharp` embebido para reproducir de forma nativa e independiente, renderizando metadatos y un mapa dinámico con `WebView2`.
*   `AppVideoProcessor`: Envuelve binarios de FFmpeg y el parser binario de geolocalización. Expone `ExtraerAudio()`, `SilenciarVideo()`, `ExtraerFrames()`, `ConvertirAviAMp4()` y el motor híbrido `ExtractGpsFromMp4()` (que soporta los formatos de Android `©xyz` e iOS `com.apple.quicktime.location.ISO6709`).

### 🎵 Mp3 (Motor de Audio y REST)
*   `MusicPlayerForm`: Administra la lista de reproducción, la interfaz con tema visual rosa pastel y los disparadores de edición de metadatos.
*   `GestorReproduccion`: Instancia un `WaveOutEvent` y `AudioFileReader` de NAudio. Controla el flujo de audio en memoria y provee la función de liberación temporal de archivos para permitir la edición de metadatos en pistas activas.
*   `MetadataEditorForm`: Diálogo que permite modificar interactivamente el título, artista y cambiar o eliminar la portada de un archivo de audio.
*   `LyricsService`: Usa `HttpClient` y `JsonDocument` para hacer llamadas asíncronas REST a `api.lyrics.ovh`.

### 🎙️ AppGrabadora (Microfonía en Crudo)
*   `AppGrabadoraForm`: Vista minimalista para grabación.
*   `GestorGrabacion`: Usa `WaveInEvent` (NAudio) para capturar el búfer de entrada del micrófono de la PC y escribir directamente al disco duro (`WaveFileWriter`) previniendo desbordamientos de memoria (Buffer Overruns).

### 📹 AppCapturaPantalla (Grabación y Captura de Pantalla)
*   `AppCapturaPantallaForm`: Interfaz de usuario que orquesta el modo de captura (pantalla completa o región selector), la visualización de la previsualización del video e inicia/detiene la grabación o captura. Implementa un bucle asíncrono de fotogramas (`BucleCapturaAsync`) con `CancellationTokenSource` y `Task.Run` para no congelar la interfaz.
*   `RegionSelectorForm`: Overlay transparente de pantalla completa (`Opacity = 0.45`, `Cursors.Cross`) que permite al usuario seleccionar interactivamente una porción de la pantalla arrastrando el ratón en cualquier dirección (normalización de rectángulos).
*   `ScreenCaptureService`: Lógica pura que captura frames mediante `Graphics.CopyFromScreen` y los envía a disco o los inyecta en el grabador de video en tiempo real.
*   `AviGrabador`: Utiliza P/Invoke nativo (`avifil32.dll` de Windows) para escribir los fotogramas en disco (AVI crudo) pasando directamente el puntero `Scan0` para evitar basura de memoria.

### 📊 AppDataFusion (Data Science y ETL - Core)

La carpeta `Core` constituye el motor de lógica de negocio y persistencia de **AppDataFusion**. A continuación se presenta un desglose exhaustivo de cada subcarpeta, clase, método, estructura de datos y variable dentro del espacio de nombres `ExploradorArchivos.AppDataFusion`.

#### 1. Subcarpeta: Models/

Esta subcarpeta define la entidad de datos universal que fluye a través de toda la aplicación.

##### 📄 Class: `DataItem.cs`
Es el modelo de datos flexible y unificado. Su principal objetivo es permitir la interoperabilidad entre esquemas heterogéneos.

*   **Variables y Propiedades (Miembros de Instancia):**
    *   `Id` (`int`): Identificador único numérico del registro.
    *   `Nombre` (`string`): Cadena descriptiva del elemento. Inicializada por defecto como `string.Empty`.
    *   `Categoria` (`string`): Categoría del elemento. Inicializada por defecto como `string.Empty`.
    *   `Valor` (`double`): Medida cuantitativa asociada.
    *   `Fuente` (`string`): Indica de dónde proviene el registro (ej. `"csv"`, `"json"`, `"xml"`, `"txt"`, `"postgresql"`, `"mariadb"`).
    *   `Fecha` (`DateTime`): Registro temporal, inicializado en `DateTime.Now`.
    *   `Latitude` (`double?`) y `Longitude` (`double?`): Coordenadas geográficas anulables.
    *   `CamposExtra` (`Dictionary<string, string>`): Estructura clave-valor instanciada con el comparador `StringComparer.OrdinalIgnoreCase`. Almacena dinámicamente cualquier columna adicional no contemplada en las propiedades fijas (ej. `"price_per_kilogram"`, `"store type"`).

*   **Métodos y Algoritmos:**
    *   `NormalizarParaComparar(string s)`:
        *   **Algoritmo:** Toma una cadena de texto, la descompone en diacríticos usando la normalización unicode Form D (`NormalizationForm.FormD`), recorre carácter por carácter omitiendo marcas de no espaciado (acentos, diéresis), y extrae únicamente letras y números en minúsculas.
        *   **Utilidad:** Homogeneiza búsquedas como `"Store Type"` y `"store_type"`, reduciendo ambas a `"storetype"`.
    *   `Clonar()`: Retorna un duplicado superficial (`MemberwiseClone`).
    *   `Equals(object?)` y `GetHashCode()`: Sobreescritos para realizar comparaciones de identidad de registro basándose en las propiedades `Id`, `Nombre` (case-insensitive) y `Categoria` (case-insensitive).

#### 2. Subcarpeta: Database/

Esta subcarpeta gestiona la persistencia de datos relacionales e implementa una capa polimórfica para interactuar con motores de bases de datos.

##### 📄 Interface: `IDbConnector.cs`
Define el contrato abstracto y uniforme que deben implementar los controladores de bases de datos.

*   **Propiedades obligatorias:**
    *   `CadenaConexion` (`string`), `Tabla` (`string`), `LimiteFilas` (`int`).
    *   `UltimasColumnas` (`List<string>`): Columnas físicas presentes en la tabla.
    *   `MapeoColumnas` (`Dictionary<string, string>`): Relación de las columnas de base de datos con los roles lógicos de la aplicación (ej: `{"id_cliente": "id", "precio_venta": "valor"}`).
    *   `ColPrimaryKey` (`string?`): Nombre físico del identificador de tabla de la BD.
*   **Métodos obligatorios:**
    *   `ObtenerNombresColumnas()`, `SobreescribirMapeo(...)`, `LeerDatos()`, `ProbarConexion(out string mensaje)`.

##### 📄 Class: `DbConnectorFactory.cs` (Fábrica)
Clase estática que implementa el patrón de creación de software *Factory*.
*   `Crear(string tipo, string cadenaConexion, string tabla)`: Evalúa el tipo de motor y retorna un objeto de tipo `IDbConnector` (instanciando un `PostgreSqlConnector` o un `MariaDbConnector`).

##### 📄 Class: `PostgreSqlConnector.cs` y `MariaDbConnector.cs`
Controladores concretos de base de datos. PostgreSQL utiliza `Npgsql` y MariaDB utiliza `MySqlConnector`.

*   **Métodos Clave y Lógica Interna:**
    *   `ObtenerNombresColumnas()`:
        *   **Lógica:** Ejecuta una consulta ligera de esquema (`SELECT * FROM tabla LIMIT 0`) para poblar `UltimasColumnas` sin descargar registros de la red. Llama a `ObtenerPrimaryKeyColumna` para rastrear las claves primarias en los metadatos de sistema.
    *   `ObtenerPrimaryKeyColumna(connection)`:
        *   **Lógica:** Consulta de forma parametrizada las tablas del sistema (`INFORMATION_SCHEMA` en MariaDB; `information_schema.table_constraints` e `information_schema.key_column_usage` en Postgres) buscando restricciones `PRIMARY KEY` o `UNIQUE`. Si no existen, toma la primera columna por defecto como fallback.
    *   `LeerDatos()`:
        *   **Lógica:** Ejecuta `SELECT * FROM tabla` (añadiendo cláusula de límite si `LimiteFilas > 0`). Utiliza un diccionario de mapeo interno `Dictionary<string, int>` para acelerar el acceso a los ordinales del lector de datos.
        *   Normaliza los mapeos de columnas dinámicas con `DataItem.NormalizarParaComparar` y excluye las columnas mapeadas a propiedades fijas para prevenir redundancias. Almacena las columnas sobrantes en `CamposExtra`.
    *   `EnriquecerCamposFaltantes(...)` (Heurísticas de autodescubrimiento):
        *   Si el usuario no definió columnas para `Nombre`, `Categoria` o `Valor`, la clase analiza estadísticamente los datos cargados en `CamposExtra`:
            *   `BuscarMejorClaveCategoria`: Busca columnas con baja cardinalidad (número de valores únicos menor al total de registros) y que contengan mayoritariamente texto descriptivo (no numérico).
            *   `BuscarMejorClaveNumerica`: Selecciona la columna con la mayor tasa de conversión a `double`.
            *   `BuscarMejorClaveTexto`: Selecciona columnas de texto descriptivo sobrantes que no coincidan con la categoría elegida.

##### 📄 Class: `DatabaseWriter.cs` (Escritura Masiva y Celda a Celda)
Clase estática para inserciones y actualizaciones sobre las bases de datos.

*   **Inserciones Bulk (Masivas):**
    *   `EscribirEnPostgreSQL(...)`:
        *   **Algoritmo:** Utiliza el exportador binario masivo `conn.BeginBinaryImport` de `Npgsql`. Crea la tabla física si no existe mediante comandos DDL dinámicos (`CrearTablaPostgreSQL`). Escribe directamente en el búfer de red de Postgres controlando y recalculando identificadores secuenciales únicos para evitar violaciones de clave primaria.
    *   `EscribirEnMariaDB(...)`:
        *   **Algoritmo:** Invoca a la clase de utilidad `MySqlBulkCopy`. Para evitar duplicados en memoria o intermediarios pesados (como `DataTable` o `DataSet`), implementa la clase interna privada `FastDataReader` que hereda de `IDataReader`.
        *   **`FastDataReader` (Custom implementation):** Actúa como un iterador directo sobre la colección `List<DataItem>`. Al llamar a `Read()`, avanza el puntero por la lista en memoria y entrega los valores formateados en `GetValue(int idx)`, resolviendo autoincrementos e IDs duplicados en tiempo real.

*   **Actualizaciones en Caliente (Celda a Celda):**
    *   `ActualizarCampoPostgreSQL` / `ActualizarCampoMariaDB`:
        *   **Lógica:** Cuando el usuario edita una celda en la pantalla de App Data, estas funciones ejecutan de manera asíncrona un comando parametrizado `UPDATE tabla SET columna = @val WHERE idCol = @id`.
        *   En Postgres, previamente realiza una consulta a `information_schema.columns` para recuperar el tipo de dato físico de la columna (ej: `integer`, `double precision`, `boolean`, `text`). Esto permite castear de forma segura la cadena de texto de la UI al tipo nativo de base de datos antes de enviarlo, evitando errores de tipado de datos en el motor SQL.

#### 3. Subcarpeta: Readers/

Esta sección procesa archivos planos de disco y los convierte en objetos `DataItem` utilizando mecanismos de streaming para proteger el consumo de memoria.

*   📄 `CsvDataReader.cs`:
    *   **Especialización:** Lector de archivos de valores separados por comas, tabuladores o puntos y comas.
    *   **Algoritmo de Parseo (`SepararCsvRobust`):** Procesa la línea carácter por carácter manteniendo un estado booleano (`enComillas`) e índices de profundidad de corchetes (`[ ]`) o llaves (`{ }`). Esto permite ignorar el carácter delimitador (ej: una coma `,`) si se encuentra encerrado entre comillas dobles o forma parte de un objeto JSON estructurado dentro del CSV.
*   📄 `JsonDataReader.cs`:
    *   **Especialización:** Utiliza `JsonDocument` para analizar archivos JSON.
    *   **Lógica adaptativa:** Si la raíz del archivo es un arreglo directo de objetos, los procesa linealmente. Si la raíz es un objeto complejo, utiliza búsquedas recursivas en sus propiedades buscando la primera propiedad que almacene un array de objetos, aplanándola de forma automatizada.
*   📄 `XmlDataReader.cs`:
    *   **Especialización:** Analiza archivos XML utilizando `System.Xml.Linq`. Extrae información de atributos XML y subnodos, volcando todo elemento no mapeado al diccionario dinámico `CamposExtra`.
*   📄 `TxtDataReader.cs`:
    *   **Especialización:** Carga archivos delimitados o planos, calculando heurísticamente cuál es el carácter separador preponderante en las primeras líneas.

#### 4. Subcarpeta: Processing/

Contiene los algoritmos para transformar y auditar los datos de la aplicación.

##### 📄 Class: `DataProcessor.cs`
*   `Filtrar(List<DataItem> datos, string campo, string valor, bool exactMatch)`:
    *   **Lógica:** Realiza búsquedas de texto e introduce búsquedas numéricas flexibles (ej: buscar `"100"` evalúa la conversión del valor a double, entero y texto con decimales para hallar correspondencias válidas). Si el campo a buscar no pertenece a los campos fijos, realiza una consulta normalizada en `CamposExtra`. Si no halla el campo, ejecuta una búsqueda global transversal en todos los campos y valores extras de la fila.
*   `OrdenarLinq(List<DataItem> datos, string campo, bool ascendente)`:
    *   **Lógica:** Realiza ordenación dinámica en caliente a través de LINQ. Emplea `ObtenerLlaveOrdenamiento` para extraer el tipo de dato correspondiente (tipo `int`, `double`, `DateTime` o `string`), asegurando que las columnas numéricas o de fechas se clasifiquen correctamente bajo sus respectivos tipos de datos.

##### 📄 Class: `DataQualityAnalyzer.cs`
*   **Lógica del Análisis de Calidad:**
    *   Agrupa las columnas del dataset buscando semánticas de teléfonos, correos y fechas en base a arrays de palabras clave (`PhoneKeywords`, `EmailKeywords`, `DateKeywords`).
    *   Detecta duplicados mediante una firma de contenido (`Id + Nombre + Categoria + Valor`).
    *   Rastrea campos vacíos omitiendo los campos opcionales `latitude` y `longitude`.
    *   Valida formatos de correos mediante la expresión regular `EmailRegex`.
    *   Valida y sugiere formatos para teléfonos (`ValidateAndFixPhone`) corrigiendo prefijos o caracteres no válidos.
    *   Valida formatos de fechas (`DetectAndFixDate`) sugiriendo la conversión a formatos estándar ISO (`yyyy-MM-dd`).

#### 5. Subcarpeta: Services/

Servicios compartidos de interacción externa y exportación.

##### 📄 Class: `GeocodingService.cs`
*   **Estructuras y Conexiones:**
    *   `_httpClient`: Instancia estática reutilizable de `HttpClient` configurada con el encabezado `User-Agent` obligatorio (`"DataFusionArena/1.0"`) para cumplir con los lineamientos de Nominatim.
    *   `_cache` (`Dictionary<string, (double, double)?>`): Caché que guarda las geolocalizaciones ya resueltas.
    *   **Algoritmo:** Busca claves en `CamposExtra` que contengan palabras como `"ciudad"`, `"city"`, `"location"`. Ejecuta una consulta HTTP asíncrona a `https://nominatim.openstreetmap.org/search` limitando la respuesta a un resultado en formato JSON.
    *   **Throttle y Limitaciones:** Restringe las peticiones a un lote máximo de 20 consultas por proceso (`MAX_GEOCODE_PER_BATCH`) e introduce un retardo de `Task.Delay(1000)` tras cada petición para respetar la directiva de 1 petición por segundo.

##### 📄 Class: `FileExportService.cs`
*   **Lógica de Exportación:**
    *   Toma la colección en memoria y recopila mediante LINQ todas las claves distintas que existen en `CamposExtra` de todos los elementos (`SelectMany(d => d.CamposExtra.Keys).Distinct()`).
    *   Reconstruye el esquema tabular completo con todas las columnas dinámicas y fijas, escribiéndolas en disco en el formato solicitado (CSV, JSON, XML o TXT).

### 📁 Form1 y Base
*   `Form1`: El navegador clásico. Coordina el árbol de carpetas de Windows y lanza las aplicaciones correspondientes (El Enrutador).
*   `FileSystemItem`: Modelo de datos puro (POCO) con nombre, fecha, peso y ruta.
*   `SystemIconManager`: P/Invoke a `Shell32.dll` (`SHGetFileInfo`) para extraer los íconos oficiales del Sistema Operativo por extensión en HD.

### ⚙️ Services y UI
*   `FileService`: Wrapper de `System.IO` para operaciones seguras (copias, movimientos, eliminación enviando a la Papelera de Reciclaje de Windows mediante P/Invoke a `shell32.dll`) ejecutadas de manera asíncrona.
*   `LoggerService`: Servicio estructurado de registro de excepciones y logs locales en `app_errors.log` dentro de ApplicationData, eliminando bloques `catch` vacíos o silenciosos.
*   `SmtpMailService`: Centraliza la lógica de envío asíncrono de correos electrónicos con archivos adjuntos usando credenciales de red, cifrado SSL/TLS, límite de tamaño físico (25 MB) y persistencia local de la configuración del servidor en `smtp_config.json`.
*   `TextFileFormatterService`: Contiene las rutinas para la lectura y el formateo estético inteligente de archivos de texto estructurados (como JSON, XML, CSV).
*   `CameraCaptureService`: Centraliza el listado de cámaras web conectadas, el flujo de previsualización de vídeo y el control de grabación o captura de imágenes.
*   `FileConverterService`: Motor de conversión y exportación universal de archivos.
    *   `Convertir(string rutaOrigen, string formatoDestino)`: Orquesta el proceso principal de conversión. Cuenta con lógica para resolver nombres duplicados incrementando secuencialmente un número al final del nombre (ej., `documento (1).pdf`) si ya existe el archivo en la ruta destino.
    *   `BuscarSOffice()`: Escanea directorios de Windows para hallar el ejecutable de LibreOffice (`soffice.exe`).
    *   `ConvertirConLibreOffice(...)`: Realiza la conversión con LibreOffice headless y redirige la salida a una ruta limpia temporal. Si el subproceso excede un tiempo de espera de 180,000 ms, lo fuerza a cerrarse.
    *   `ConvertirADocx()`, `ConvertirAXlsx()`, `ConvertirAPdf()`, `ConvertirAPptx()`: Rutinas de fallback en C# nativo que leen y recrean los documentos usando librerías NuGet.
*   `EmailService`: Componente auxiliar de compartición de archivos. Utiliza llamadas nativas MAPI (`Mapi32.dll` -> `MAPISendMail`) para abrir el cliente de correo por defecto del sistema operativo con el archivo adjunto pre-cargado. Incluye fallbacks de ejecución de procesos para lanzar Outlook y Thunderbird vía CLI, u abrir un enlace con esquema `mailto:` en caso de error.
*   `SendMailForm`: Formulario clásico e interfaz de usuario para el envío directo y autónomo de archivos adjuntos mediante el protocolo SMTP. Utiliza las clases `SmtpClient` y `MailMessage` de .NET de manera asíncrona y persiste la configuración local en formato JSON.
*   `ThemeRenderer`: Sobrescribe los eventos de pintado (`OnPaint`) de los controles de Windows para aplicar paletas oscuras (Dark Mode) esquivando el estilo obsoleto por defecto.
*   `QuickLookForm`: Formulario sin bordes nativos que previsualiza archivos rápidamente. Implementa una barra de título personalizada para arrastrar la ventana, botones "semáforo" (Cerrar, Minimizar, Maximizar) pintados con GDI+ y lógica persistente al desactivarse. Soporta carga de texto extendida, imágenes, PDFs y WebView2.

---

## 🧩 7. Código Fuente y Explicación Estructural

A continuación se incluyen pedazos de código clave del sistema que sustentan las explicaciones dadas anteriormente. Se omiten interfaces visuales repetitivas y se enfoca estrictamente en la lógica nativa y algoritmos de alto rendimiento.

### 🎵 7.1 Motor de Audio y Edición de Metadatos: `GestorReproduccion.cs`
Este archivo es el núcleo de control multihilo para la reproducción de MP3. Utiliza la librería de bajo nivel `NAudio` para interactuar con los canales PCM de Windows y gestiona la liberación temporal de descriptores de archivo para posibilitar el guardado de metadatos físicos.

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

    /// <summary>
    /// Guarda metadatos físicos en el archivo de audio.
    /// Detiene y libera temporalmente el archivo en NAudio para evadir excepciones de proceso ocupado.
    /// </summary>
    public bool GuardarMetadatos(Cancion cancion, string nuevoTitulo, string nuevoArtista, Image? nuevaPortada) {
        bool esCancionActual = (cancion == CancionActual);
        bool estabaReproduciendo = false;
        TimeSpan posicionActual = TimeSpan.Zero;

        if (esCancionActual) {
            estabaReproduciendo = EstaReproduciendo;
            if (_audioReader != null) posicionActual = _audioReader.CurrentTime;
            DetenerInterno(); // Cierra el AudioFileReader y libera el handle del archivo
        }

        cancion.Titulo = nuevoTitulo;
        cancion.Artista = nuevoArtista;
        cancion.Portada = nuevaPortada != null ? (Image)nuevaPortada.Clone() : null;

        // Persistencia física vía TagLibSharp
        bool guardado = MetadataService.GuardarCambios(cancion);

        if (esCancionActual) {
            // Recargar el archivo y restaurar segundo y estado exacto de reproducción
            _audioReader = new AudioFileReader(cancion.RutaArchivo) { Volume = _volumen };
            _audioReader.CurrentTime = posicionActual;
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            if (estabaReproduciendo) _waveOut.Play();
        }
        return guardado;
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

### 🎥 7.3 Codificación de Video y Geolocalización Híbrida: `AppVideoProcessor.cs`
La edición de video nativa es lenta, por lo que el sistema usa CLI inter-procesos para comunicarse invisiblemente con `FFmpeg`. Asimismo, implementa un parser binario de alto rendimiento para extraer geolocalización tanto en dispositivos Android (caja `©xyz`) como iOS/iPhone (caja de metadatos `com.apple.quicktime.location.ISO6709` mediante búsqueda indexada inversa).

```csharp
using System.Diagnostics;
using System.IO;

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

    // Despachador híbrido de GPS (Android + iOS)
    private static (double? lat, double? lon) ExtractGpsFromMp4(string filePath) {
        try {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                // 1. Intentamos extraer via ©xyz (típico de Android y algunos iOS)
                var (lat, lon) = ExtractGpsFromLegacyXyz(stream);
                if (lat.HasValue && lon.HasValue) return (lat, lon);

                // 2. Si falla, intentamos extraer via Apple QuickTime metadata (keys/ilst)
                return ExtractGpsFromAppleMetadata(stream);
            }
        } catch {
            return (null, null);
        }
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

### ✉️ 7.9 Envío Directo de Correo Electrónico y Protocolo SMTP: `SendMailForm.cs`
A continuación se detalla el bloque de código encargado del envío de archivos de manera directa utilizando el protocolo SMTP. Este método se ejecuta en un hilo secundario para no congelar la interfaz gráfica clásica y utiliza SSL/TLS para asegurar la sesión de red.

```csharp
private async Task EnviarCorreoAsync()
{
    // ... Validaciones de campos (destinatario, credenciales) ...
    string recipient = txtTo.Text.Trim();
    string subject = txtSubject.Text.Trim();
    string body = txtBody.Text;

    // Validación física del tamaño del archivo adjunto (25 MB de límite)
    var fileInfo = new FileInfo(_filePath);
    if (fileInfo.Length > 25 * 1024 * 1024)
    {
        MessageBox.Show("El archivo seleccionado supera el límite de 25 MB permitido...");
        return;
    }

    // Configuración SMTP y persistencia local
    _config.Server = txtSmtpServer.Text.Trim();
    _config.Port = port;
    _config.EnableSsl = chkSsl.Checked;
    _config.SenderEmail = txtSenderEmail.Text.Trim();
    _config.SenderPassword = txtSenderPassword.Text;

    GuardarConfiguracion(); // Serializa a JSON en %APPDATA%

    // Cambiar estado visual del formulario a "Enviando"
    btnEnviar.Enabled = false;
    btnCancelar.Enabled = false;
    btnEnviar.Text = "Enviando...";
    this.Cursor = Cursors.WaitCursor;

    try
    {
        // Ejecución en segundo plano (asíncrona)
        await Task.Run(() =>
        {
            using var mail = new MailMessage();
            mail.From = new MailAddress(_config.SenderEmail);
            mail.To.Add(recipient);
            mail.Subject = subject;
            mail.Body = body;

            // Vincular archivo adjunto
            var attachment = new Attachment(_filePath);
            mail.Attachments.Add(attachment);

            // Cliente SMTP de .NET
            using var smtp = new SmtpClient(_config.Server, _config.Port);
            smtp.Credentials = new NetworkCredential(_config.SenderEmail, _config.SenderPassword);
            smtp.EnableSsl = _config.EnableSsl;

            // Envío por red usando comandos SMTP (EHLO, AUTH, MAIL FROM, RCPT TO, DATA)
            smtp.Send(mail);
        });

        MessageBox.Show("¡El correo electrónico y el archivo adjunto se enviaron con éxito!", "Éxito");
        this.Close();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ocurrió un error al enviar el correo:\n{ex.InnerException?.Message ?? ex.Message}", "Error al Enviar");
    }
    finally
    {
        btnEnviar.Enabled = true;
        btnCancelar.Enabled = true;
        btnEnviar.Text = "✉️ Enviar";
        this.Cursor = Cursors.Default;
    }
}
```
```

