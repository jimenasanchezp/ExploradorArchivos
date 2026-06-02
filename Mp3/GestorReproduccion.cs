using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace ExploradorArchivos.Mp3;

public enum ModoRepetir { Desactivado, RepetirUno, RepetirTodos }

/// <summary>
/// Motor principal de audio que utiliza la biblioteca NAudio.
/// Maneja la decodificación de streams, el dispositivo de salida WaveOutEvent,
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
        set 
        { 
            _modoAleatorio = value; 
            RegenerarOrden(); 
        }
    }

    public ModoRepetir ModoRepetir { get => _modoRepetir; set => _modoRepetir = value; }

    public float Volumen
    {
        get => _volumen;
        set
        {
            _volumen = Math.Clamp(value, 0f, 1f);
            if (_audioReader != null) 
            {
                _audioReader.Volume = _volumen;
            }
        }
    }

    /// <summary>
    /// Inicializa una nueva instancia del gestor de reproducción y configura el temporizador de posición.
    /// </summary>
    public GestorReproduccion()
    {
        _timerPosicion = new System.Windows.Forms.Timer { Interval = 250 };
        _timerPosicion.Tick += (s, e) =>
        {
            if (_audioReader != null && _waveOut?.PlaybackState == PlaybackState.Playing)
            {
                PosicionActualizada?.Invoke(_audioReader.CurrentTime, _audioReader.TotalTime);
            }
        };
    }

    /// <summary>
    /// Limpia la cola actual y carga una nueva lista de pistas.
    /// Inicia la reproducción con la primera canción o una pista específica de forma predeterminada.
    /// </summary>
    /// <param name="rutas">Lista de rutas de archivos de audio a encolar.</param>
    /// <param name="rutaInicial">Ruta del archivo inicial por el que debe comenzar la reproducción.</param>
    public void CargarCola(List<string> rutas, string? rutaInicial = null)
    {
        DetenerInterno();
        _cola.Clear();

        // Mapear rutas a objetos Cancion filtrando archivos corruptos o inaccesibles
        var cancionesValidas = rutas
            .Select(ruta => {
                try { return new Cancion(ruta); }
                catch { return null; }
            })
            .Where(c => c != null)
            .Cast<Cancion>();

        _cola.AddRange(cancionesValidas);

        if (_cola.Count == 0) return;
        RegenerarOrden();

        if (rutaInicial != null)
        {
            int idxReal = _cola.FindIndex(c => c.RutaArchivo == rutaInicial);
            _indiceCola = idxReal >= 0 ? _ordenReproduccion.IndexOf(idxReal) : 0;
        }
        else 
        { 
            _indiceCola = 0; 
        }

        ReproducirActual();
    }

    /// <summary>
    /// Inicia o reanuda la reproducción.
    /// </summary>
    public void Play()
    {
        if (_waveOut == null && CancionActual != null) 
        { 
            ReproducirActual(); 
            return; 
        }
        if (_waveOut?.PlaybackState == PlaybackState.Paused)
        {
            _waveOut.Play();
            _timerPosicion.Start();
            EstadoCambiado?.Invoke(true);
        }
    }

    /// <summary>
    /// Pausa la reproducción del audio actual.
    /// </summary>
    public void Pause()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            _timerPosicion.Stop();
            EstadoCambiado?.Invoke(false);
        }
    }

    /// <summary>
    /// Alterna el estado de reproducción entre Reproducir y Pausar.
    /// </summary>
    public void TogglePlayPause() 
    { 
        if (EstaReproduciendo) 
            Pause(); 
        else 
            Play(); 
    }

    /// <summary>
    /// Detiene la reproducción y libera el stream de audio actual.
    /// </summary>
    public void Stop() 
    { 
        DetenerInterno(); 
        EstadoCambiado?.Invoke(false); 
    }

    /// <summary>
    /// Avanza a la siguiente pista de la cola respetando los modos de repetición y orden.
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
    /// Retrocede a la pista anterior, o reinicia la canción actual si tiene más de 3 segundos reproducidos.
    /// </summary>
    public void Anterior()
    {
        if (_cola.Count == 0) return;
        
        // Comportamiento comercial: si se lleva más de 3 seg de reproducción, se reinicia la pista
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

    /// <summary>
    /// Cambia la posición del cabezal de reproducción.
    /// </summary>
    /// <param name="porcentaje">Valor porcentual (0.0 a 1.0) al que se desea desplazar la reproducción.</param>
    public void Seek(double porcentaje)
    {
        if (_audioReader != null)
        {
            _audioReader.CurrentTime = TimeSpan.FromSeconds(_audioReader.TotalTime.TotalSeconds * Math.Clamp(porcentaje, 0, 1));
            PosicionActualizada?.Invoke(_audioReader.CurrentTime, _audioReader.TotalTime);
        }
    }

    /// <summary>
    /// Reproduce una canción específica de la lista ordenada por su índice físico real.
    /// </summary>
    /// <param name="indiceReal">Índice absoluto de la canción dentro de la lista.</param>
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
    /// Inicializa y comienza la reproducción de la pista seleccionada liberando previamente los recursos activos.
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

            // Buscar letras de canciones de forma asíncrona si no existen de forma local
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
        catch
        {
            // Ignora errores si hay fallas de inicialización de hardware o formatos no soportados
        }
        finally
        {
            _cambiando = false;
        }
    }

    /// <summary>
    /// Delegado para manejar el evento cuando el motor de audio detiene la reproducción.
    /// </summary>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // El fin del stream se ejecuta en un hilo secundario de NAudio.
        // Se despacha al timer en el hilo de UI para evitar condiciones de carrera.
        _timerPosicion.Tick -= AvanzarCancionPendiente;
        _timerPosicion.Tick += AvanzarCancionPendiente;
    }

    /// <summary>
    /// Lógica de transición de pista al finalizar la canción actual de forma natural.
    /// </summary>
    private void AvanzarCancionPendiente(object? sender, EventArgs e)
    {
        _timerPosicion.Tick -= AvanzarCancionPendiente;
        
        if (_waveOut == null && _audioReader == null) return;
        if (_audioReader != null && _audioReader.CurrentTime < _audioReader.TotalTime - TimeSpan.FromMilliseconds(500)) return;

        if (_modoRepetir == ModoRepetir.RepetirUno)
        {
            if (_audioReader != null) 
            {
                _audioReader.CurrentTime = TimeSpan.Zero;
            }
            _waveOut?.Play();
            EstadoCambiado?.Invoke(true);
        }
        else
        {
            Siguiente();
        }
    }

    /// <summary>
    /// Detiene y libera los descriptores de lectura de archivo y objetos de hardware.
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
        if (_audioReader != null) 
        { 
            _audioReader.Dispose(); 
            _audioReader = null; 
        }
    }

    /// <summary>
    /// Regenera la lista de orden de reproducción. En modo aleatorio baraja los índices mediante Fisher-Yates.
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
        else if (cancionActualRealIdx >= 0) 
        { 
            _indiceCola = cancionActualRealIdx; 
        }
    }

    /// <summary>
    /// Obtiene la cola actual mapeada con sus índices físicos en la lista original.
    /// </summary>
    public List<(int IndiceReal, Cancion Cancion)> ObtenerColaOrdenada() =>
        _ordenReproduccion.Select(idx => (idx, _cola[idx])).ToList();

    /// <summary>
    /// Escribe los metadatos editados al archivo físico liberando temporalmente los descriptores si coincide con la pista actual.
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

        // Guardar respaldo de los metadatos originales en memoria en caso de que ocurra un error de escritura física
        string tituloOriginal = cancion.Titulo;
        string artistaOriginal = cancion.Artista;
        Image? portadaOriginal = cancion.Portada;

        // Asignar temporalmente las nuevas propiedades al objeto de tipo Canción
        cancion.Titulo = nuevoTitulo;
        cancion.Artista = nuevoArtista;
        cancion.Portada = nuevaPortada != null ? (Image)nuevaPortada.Clone() : null;

        // Intentar escribir y persistir los cambios físicos en el archivo en disco
        bool guardado = MetadataService.GuardarCambios(cancion);

        if (!guardado)
        {
            // Revertir a los valores originales respaldados si la persistencia física en disco falló
            cancion.Titulo = tituloOriginal;
            cancion.Artista = artistaOriginal;
            cancion.Portada = portadaOriginal;
        }
        else
        {
            // Liberar la instancia de la imagen de portada anterior si se guardó la nueva con éxito
            portadaOriginal?.Dispose();
        }

        if (esCancionActual)
        {
            try
            {
                if (System.IO.File.Exists(cancion.RutaArchivo))
                {
                    // Recrear los objetos lectores de flujo y dispositivo de salida para reanudar la reproducción
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
            catch 
            {
                // Maneja posibles errores al reconstruir el reproductor
            }

            CancionCambiada?.Invoke(cancion);
            EstadoCambiado?.Invoke(estabaReproduciendo);
        }

        return guardado;
    }

    public void Dispose() 
    { 
        DetenerInterno(); 
        _timerPosicion.Dispose(); 
    }
}