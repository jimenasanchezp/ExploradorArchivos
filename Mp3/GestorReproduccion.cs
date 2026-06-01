using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;

namespace ExploradorArchivos.Mp3;

public enum ModoRepetir { Desactivado, RepetirUno, RepetirTodos }

/// <summary>
/// Motor principal de audio que utiliza la biblioteca NAudio.
/// Maneja la decodificación de streams, el dispositivo de salida <c>WaveOutEvent</c>,
/// colas de reproducción, orden aleatorio y sincronización con la UI.
/// </summary>
public class GestorReproduccion : IDisposable
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioReader;
    private readonly System.Windows.Forms.Timer _timerPosicion;


    private List<Cancion> _cola = new();
    private List<int> _ordenReproduccion = new();
    private int _indiceCola = -1;
    private volatile bool _cambiando = false;

    private bool _modoAleatorio;
    private ModoRepetir _modoRepetir = ModoRepetir.Desactivado;
    private float _volumen = 0.7f;

    public event Action<Cancion>? CancionCambiada;
    public event Action<TimeSpan, TimeSpan>? PosicionActualizada;
    public event Action<bool>? EstadoCambiado;
    public event Action? ReproduccionTerminada;
    public int IndiceCola => _indiceCola;

    public Cancion? CancionActual => _indiceCola >= 0 && _indiceCola < _ordenReproduccion.Count
        ? _cola[_ordenReproduccion[_indiceCola]]
        : null;

    public bool EstaReproduciendo => _waveOut?.PlaybackState == PlaybackState.Playing;

    public bool ModoAleatorio
    {
        get => _modoAleatorio;
        set { _modoAleatorio = value; RegenerarOrden(); }
    }

    public ModoRepetir ModoRepetir { get => _modoRepetir; set => _modoRepetir = value; }

    public float Volumen
    {
        get => _volumen;
        set
        {
            _volumen = Math.Clamp(value, 0f, 1f);
            if (_audioReader != null) _audioReader.Volume = _volumen;
        }
    }

    public GestorReproduccion()
    {
        _timerPosicion = new System.Windows.Forms.Timer { Interval = 250 };
        _timerPosicion.Tick += (s, e) =>
        {
            if (_audioReader != null && _waveOut?.PlaybackState == PlaybackState.Playing)
                PosicionActualizada?.Invoke(_audioReader.CurrentTime, _audioReader.TotalTime);
        };
    }

    /// <summary>
    /// Limpia la cola actual y encola de manera asíncrona una nueva lista de pistas.
    /// Inicia la reproducción automáticamente con la primera canción o con la pista especificada.
    /// </summary>
    /// <param name="rutas">Lista de rutas físicas de archivos de audio.</param>
    /// <param name="rutaInicial">Archivo por el que debe comenzar a reproducir (opcional).</param>
    public void CargarCola(List<string> rutas, string? rutaInicial = null)
    {
        DetenerInterno();
        _cola.Clear();
        foreach (var ruta in rutas)
        {
            try { _cola.Add(new Cancion(ruta)); } catch { }
        }

        if (_cola.Count == 0) return;
        RegenerarOrden();

        if (rutaInicial != null)
        {
            int idxReal = _cola.FindIndex(c => c.RutaArchivo == rutaInicial);
            _indiceCola = idxReal >= 0 ? _ordenReproduccion.IndexOf(idxReal) : 0;
        }
        else { _indiceCola = 0; }

        ReproducirActual();
    }

    /// <summary>
    /// Inicia o reanuda la reproducción de la canción actual. Si no hay instancia activa, 
    /// la crea invocando el motor de lectura interno.
    /// </summary>
    public void Play()
    {
        if (_waveOut == null && CancionActual != null) { ReproducirActual(); return; }
        if (_waveOut?.PlaybackState == PlaybackState.Paused)
        {
            _waveOut.Play();
            _timerPosicion.Start();
            EstadoCambiado?.Invoke(true);
        }
    }

    public void Pause()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            _timerPosicion.Stop();
            EstadoCambiado?.Invoke(false);
        }
    }

    public void TogglePlayPause() { if (EstaReproduciendo) Pause(); else Play(); }

    public void Stop() { DetenerInterno(); EstadoCambiado?.Invoke(false); }

    /// <summary>
    /// Avanza a la siguiente pista de la cola. Su comportamiento difiere según el modo de repetición
    /// (reproducir de nuevo la misma, seguir la cola aleatoria, o detenerse al final).
    /// </summary>
    public void Siguiente()
    {
        if (_cola.Count == 0) return;
        _indiceCola++;

        if (_indiceCola >= _ordenReproduccion.Count)
        {
            if (_modoRepetir == ModoRepetir.RepetirTodos)
            {
                _indiceCola = 0;
                if (_modoAleatorio) RegenerarOrden();
            }
            else
            {
                _indiceCola = _ordenReproduccion.Count - 1;
                Stop();
                ReproduccionTerminada?.Invoke();
                return;
            }
        }
        ReproducirActual();
    }

    /// <summary>
    /// Regresa a la pista anterior, o reinicia la canción actual si ya han pasado más de 3 segundos
    /// de reproducción (comportamiento estándar de reproductores comerciales).
    /// </summary>
    public void Anterior()
    {
        if (_cola.Count == 0) return;
        if (_audioReader != null && _audioReader.CurrentTime.TotalSeconds > 3)
        {
            _audioReader.CurrentTime = TimeSpan.Zero;
            return;
        }

        _indiceCola--;
        if (_indiceCola < 0)
        {
            _indiceCola = _modoRepetir == ModoRepetir.RepetirTodos ? _ordenReproduccion.Count - 1 : 0;
        }
        ReproducirActual();
    }

    public void Seek(double porcentaje)
    {
        if (_audioReader != null)
        {
            _audioReader.CurrentTime = TimeSpan.FromSeconds(_audioReader.TotalTime.TotalSeconds * Math.Clamp(porcentaje, 0, 1));
            PosicionActualizada?.Invoke(_audioReader.CurrentTime, _audioReader.TotalTime);
        }
    }

    // CORRECCIÓN: Ahora mapea correctamente el índice seleccionado en la UI
    public void ReproducirPorIndice(int indiceReal)
    {
        int idxEnCola = _ordenReproduccion.IndexOf(indiceReal);
        if (idxEnCola >= 0)
        {
            _indiceCola = idxEnCola;
            ReproducirActual();
        }
    }

    /// <summary>
    /// Núcleo de la reproducción. Detiene el stream actual, libera buffers 
    /// y carga asíncronamente el nuevo archivo en el hardware de audio,
    /// disparando adicionalmente la búsqueda de letras por internet.
    /// </summary>
    private void ReproducirActual()
    {
        if (_cambiando) return;
        _cambiando = true;
        
        try
        {
            DetenerInterno();
            var cancion = CancionActual;
            if (cancion == null) return;

            _audioReader = new AudioFileReader(cancion.RutaArchivo) { Volume = _volumen };
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Play();
            _timerPosicion.Start();

            // Buscar letra automáticamente si no tiene una local
            if (string.IsNullOrEmpty(cancion.Letra))
            {
                _ = Task.Run(async () =>
                {
                    var letraOnline = await LyricsService.BuscarLetraAsync(cancion.Artista, cancion.Titulo);
                    if (!string.IsNullOrEmpty(letraOnline))
                    {
                        cancion.Letra = letraOnline;
                        CancionCambiada?.Invoke(cancion);
                    }
                });
            }

            CancionCambiada?.Invoke(cancion);
            EstadoCambiado?.Invoke(true);
        }
        catch { }
        finally
        {
            _cambiando = false;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // OnPlaybackStopped se ejecuta en un hilo de NAudio.
        // Despachamos la lógica al timer (hilo de UI) para evitar condiciones de carrera.
        _timerPosicion.Tick -= AvanzarCancionPendiente; // evitar suscripciones duplicadas
        _timerPosicion.Tick += AvanzarCancionPendiente;
    }

    private void AvanzarCancionPendiente(object? sender, EventArgs e)
    {
        _timerPosicion.Tick -= AvanzarCancionPendiente;
        
        // Solo avanzamos si la canción terminó naturalmente (no por Stop() manual)
        if (_waveOut == null && _audioReader == null) return;
        if (_audioReader != null && _audioReader.CurrentTime < _audioReader.TotalTime - TimeSpan.FromMilliseconds(500)) return;

        if (_modoRepetir == ModoRepetir.RepetirUno)
        {
            if (_audioReader != null) _audioReader.CurrentTime = TimeSpan.Zero;
            _waveOut?.Play();
            EstadoCambiado?.Invoke(true);
        }
        else
        {
            Siguiente();
        }
    }

    /// <summary>
    /// Libera los recursos de hardware (WaveOut) y los descriptores de archivo (AudioFileReader)
    /// para evitar bloqueos del sistema o fugas de memoria al cambiar de pista.
    /// </summary>
    private void DetenerInterno()
    {
        _timerPosicion.Stop();
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }
        if (_audioReader != null) { _audioReader.Dispose(); _audioReader = null; }
    }

    /// <summary>
    /// Reconstruye el orden interno de reproducción. Si el <c>ModoAleatorio</c> está activo, 
    /// implementa un algoritmo de Fisher-Yates shuffle sobre la cola actual, manteniendo
    /// la canción en reproducción en la posición 0.
    /// </summary>
    private void RegenerarOrden()
    {
        int cancionActualRealIdx = (_indiceCola >= 0 && _indiceCola < _ordenReproduccion.Count)
                                   ? _ordenReproduccion[_indiceCola] : -1;

        _ordenReproduccion = Enumerable.Range(0, _cola.Count).ToList();

        if (_modoAleatorio)
        {
            var rng = new Random();
            for (int i = _ordenReproduccion.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_ordenReproduccion[i], _ordenReproduccion[j]) = (_ordenReproduccion[j], _ordenReproduccion[i]);
            }

            if (cancionActualRealIdx >= 0)
            {
                _ordenReproduccion.Remove(cancionActualRealIdx);
                _ordenReproduccion.Insert(0, cancionActualRealIdx);
                _indiceCola = 0;
            }
        }
        else if (cancionActualRealIdx >= 0) { _indiceCola = cancionActualRealIdx; }
    }

    public List<(int IndiceReal, Cancion Cancion)> ObtenerColaOrdenada() =>
        _ordenReproduccion.Select(idx => (idx, _cola[idx])).ToList();

    /// <summary>
    /// Guarda los metadatos modificados de una canción en disco y actualiza el reproductor.
    /// Si la canción es la que se reproduce actualmente, detiene temporalmente la reproducción
    /// para liberar el bloqueo del archivo físico por NAudio, escribe el cambio y reanuda el audio.
    /// </summary>
    public bool GuardarMetadatos(Cancion cancion, string nuevoTitulo, string nuevoArtista, Image? nuevaPortada)
    {
        bool esCancionActual = (cancion == CancionActual);
        bool estabaReproduciendo = false;
        TimeSpan posicionActual = TimeSpan.Zero;

        if (esCancionActual)
        {
            estabaReproduciendo = EstaReproduciendo;
            if (_audioReader != null)
            {
                posicionActual = _audioReader.CurrentTime;
            }
            DetenerInterno();
        }

        // Respaldar metadatos anteriores para restaurar en caso de fallo
        string tituloOriginal = cancion.Titulo;
        string artistaOriginal = cancion.Artista;
        Image? portadaOriginal = cancion.Portada;

        // Asignar nuevos valores
        cancion.Titulo = nuevoTitulo;
        cancion.Artista = nuevoArtista;
        cancion.Portada = nuevaPortada != null ? (Image)nuevaPortada.Clone() : null;

        // Intentar guardar físicamente
        bool guardado = MetadataService.GuardarCambios(cancion);

        if (!guardado)
        {
            // Revertir cambios en memoria
            cancion.Titulo = tituloOriginal;
            cancion.Artista = artistaOriginal;
            cancion.Portada = portadaOriginal;
        }
        else
        {
            // Si guardó con éxito, liberamos la portada vieja
            portadaOriginal?.Dispose();
        }

        if (esCancionActual)
        {
            // Recargar la canción actual
            try
            {
                if (System.IO.File.Exists(cancion.RutaArchivo))
                {
                    _audioReader = new AudioFileReader(cancion.RutaArchivo) { Volume = _volumen };
                    _audioReader.CurrentTime = posicionActual;

                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_audioReader);
                    _waveOut.PlaybackStopped += OnPlaybackStopped;

                    if (estabaReproduciendo)
                    {
                        _waveOut.Play();
                        _timerPosicion.Start();
                    }
                }
            }
            catch { }

            CancionCambiada?.Invoke(cancion);
            EstadoCambiado?.Invoke(estabaReproduciendo);
        }

        return guardado;
    }

    public void Dispose() { DetenerInterno(); _timerPosicion.Dispose(); }
}