# Módulo AppCapturaPantalla — Documentación Técnica

## Arquitectura general

El módulo sigue el mismo patrón que el resto del proyecto: **Form + Service**.

```
AppCapturaPantallaForm  ←→  ScreenCaptureService  ←→  AviGrabador + FFmpeg
      (UI / Control)              (Lógica pura)          (Escritura de video)
```

### Archivos del módulo

| Archivo | Responsabilidad |
|---|---|
| `AppCapturaPantalla/AppCapturaPantallaForm.cs` | Interfaz de usuario, orquesta las acciones |
| `AppCapturaPantalla/RegionSelectorForm.cs` | Overlay transparente para seleccionar región con el mouse |
| `Services/ScreenCaptureService.cs` | Toda la lógica de captura y grabación |

---

## 📸 Captura de Pantalla (Screenshot)

**Tecnología:** `Graphics.CopyFromScreen` — nativo de .NET, sin librerías externas.

### Flujo al presionar "📸 Capturar"

```
1. Usuario presiona el botón
2. [Si eligió región y no la seleccionó] → abre RegionSelectorForm (overlay)
3. Form se minimiza  →  await Task.Delay(250ms)  →  la ventana desaparece de pantalla
4. Graphics.CopyFromScreen copia los píxeles de la pantalla a un Bitmap en memoria
5. Bitmap.Save() escribe el archivo como .png en Mis Imágenes
6. Form regresa (WindowState.Normal + BringToFront() + Activate())
7. Muestra MessageBox con la ruta del archivo guardado
```

### Código clave en `ScreenCaptureService`

```csharp
// Captura de región (o pantalla completa si region = Screen.Bounds)
using var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
using var g   = Graphics.FromImage(bmp);
g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
bmp.Save(rutaArchivo, ImageFormat.Png);
```

> [!NOTE]
> El form se minimiza **antes** de tomar la captura para que la ventana del módulo no aparezca en la imagen final. Se usa `await Task.Delay(250)` en lugar de `Thread.Sleep` para no bloquear el hilo UI durante la espera.

---

## 🔴 Grabación de Pantalla (Screen Recording)

**Tecnología:** `WinForms Timer` + `AviGrabador` (ya existente en el proyecto) + `FFmpeg` (ya incluido como `ffmpeg.exe`).

### Flujo al presionar "🔴 Grabar"

```
IniciarGrabacion()  [async Task]
  │
  ├── Crea AviGrabador  →  abre archivo .avi temporal en Mis Videos
  ├── Inicia Stopwatch  →  referencia de tiempo para los frames
  ├── Inicia _timerGrabacion (Interval: 1000ms)
  │         └── Actualiza etiqueta "🔴 MM:SS" en pantalla
  │
  └── Inicia _timerFrames (Interval: 66ms ≈ 15 fps)
              │
              ▼ cada 66ms: TimerFrames_Tick()
              │
              └── Task.Run (hilo de fondo — NO bloquea la UI)
                    ├── ScreenCaptureService.CapturarFrame(region)
                    │         └── CopyFromScreen → devuelve Bitmap del frame
                    └── ScreenCaptureService.EscribirFrame(grabador, frame, elapsed)
                              └── AviGrabador.EscribirFrame() → escribe al .avi
```

> [!IMPORTANT]
> **¿Por qué `Task.Run`?** La captura de pantalla con `CopyFromScreen` y la escritura al archivo AVI son operaciones I/O pesadas. Ejecutarlas en el hilo UI cada 66ms congelaría toda la aplicación. Al delegarlas a un hilo de fondo con `Task.Run`, el hilo UI queda libre para actualizar el timer, responder clicks y mantener la ventana viva.

### Flujo al presionar "⏹ Detener"

```
DetenerGrabacion()  [async void]
  │
  ├── Para _timerFrames y _timerGrabacion
  ├── Detiene el Stopwatch
  ├── LimpiarGrabador()
  │         └── AviGrabador.Cerrar() → finaliza y cierra el archivo .avi
  │
  ├── Actualiza UI → "⏳ Convirtiendo a MP4..."
  │
  └── await ScreenCaptureService.ConvertirAviAMp4Async()
              │
              └── await AppVideoProcessor.ConvertirAviAMp4()
                        └── Lanza: ffmpeg.exe -i Grabacion_tmp.avi Grabacion.mp4
                                ├── Éxito → guarda .mp4, borra el .avi temporal
                                └── Fallo → conserva el .avi (FFmpeg no encontrado)
```

---

## ✂️ Selección de Región (Overlay)

`RegionSelectorForm` es un Form especial configurado como una capa transparente sobre toda la pantalla.

### Propiedades del overlay

| Propiedad | Valor | Efecto |
|---|---|---|
| `FormBorderStyle` | `None` | Sin bordes ni barra de título |
| `WindowState` | `Maximized` | Ocupa toda la pantalla |
| `TopMost` | `true` | Siempre encima de todas las ventanas |
| `Opacity` | `0.45` | Fondo semitransparente oscuro |
| `Cursor` | `Cursors.Cross` | Cruz de selección |
| `BackColor` | `Color.Black` | Base del efecto oscurecido |

### Flujo de selección con el mouse

```
MouseDown  →  guarda punto de inicio (_inicio)
MouseMove  →  recalcula rectángulo en tiempo real
           →  llama Invalidate() → OnPaint redibuja el rectángulo azul
           →  muestra dimensiones "640 × 480 px" junto al cursor
MouseUp    →  NormalizarRectangulo(inicio, fin)
                └── Calcula X,Y,W,H correctos sin importar la dirección del arrastre
           →  PointToScreen() convierte coordenadas del Form a coordenadas absolutas
           →  retorna Rectangle al form principal como RegionSeleccionada
ESC        →  cancela → vuelve a modo "Pantalla completa"
```

> [!TIP]
> La normalización del rectángulo permite que el usuario arrastre en cualquier dirección (de derecha a izquierda, de abajo hacia arriba, etc.) y siempre se obtenga un Rectangle válido con Width y Height positivos.

---

## 📁 Dónde se guardan los archivos

| Tipo | Carpeta del sistema | Nombre de archivo |
|---|---|---|
| Screenshot | `Environment.SpecialFolder.MyPictures` | `Captura_2026-06-03_193600.png` |
| Video (MP4) | `Environment.SpecialFolder.MyVideos` | `Grabacion_2026-06-03_193600.mp4` |
| Video (AVI fallback) | `Environment.SpecialFolder.MyVideos` | `Grabacion_2026-06-03_193600_tmp.avi` |

El archivo AVI es temporal — solo se conserva si FFmpeg no está disponible o falla la conversión.

---

## Flujo completo del módulo (diagrama resumido)

```
[Botón 📺 en Form1]
        ↓
AppCapturaPantallaForm.Show()
        │
        ├── Modo seleccionado: Pantalla completa
        │       └── _regionActiva = Rectangle.Empty
        │             → usa Screen.PrimaryScreen.Bounds en tiempo de ejecución
        │
        └── Modo seleccionado: Seleccionar región
                └── await SeleccionarRegion()
                      └── RegionSelectorForm.ShowDialog()
                            → devuelve Rectangle con coords absolutas de pantalla
        │
        ├── [📸 Capturar]
        │       └── Minimizar → await Task.Delay(250) → CopyFromScreen → .png
        │             → Restaurar form → Activate() → MessageBox con ruta
        │
        └── [🔴 Grabar]
                └── AviGrabador abierto
                      → Timer 66ms → Task.Run → CopyFromScreen → EscribirFrame
                      → Timer 1s   → actualiza "🔴 MM:SS"
                      [⏹ Detener]
                      → Cierra AviGrabador → await FFmpeg → .mp4 → Mis Videos
```
