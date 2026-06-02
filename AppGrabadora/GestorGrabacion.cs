using System;
using NAudio.Wave;

namespace ExploradorArchivos.AppGrabadora;

/// <summary>
/// Maneja la captura asíncrona de audio desde el micrófono utilizando <c>NAudio</c>.
/// Escribe los flujos en crudo directamente a un archivo WAV en disco.
/// </summary>
public class GestorGrabacion : IDisposable
{
    // Inicialización y Declaración: Campos privados para controlar el flujo de entrada de audio y la escritura en archivo
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;

    /// <summary>
    /// Inicializa el dispositivo de grabación y comienza a volcar el audio capturado al archivo especificado.
    /// </summary>
    /// <param name="rutaArchivoSalida">Ruta absoluta del archivo destino (.wav).</param>
    public void IniciarGrabacion(string rutaArchivoSalida)
    {
        // Inicialización y Declaración: Dispositivo de captura de entrada de audio (WaveInEvent)
        // Configura el canal de grabación estándar (DeviceNumber 0, 44100Hz, Mono)
        _waveIn = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(44100, 1)
        };

        // Operación: Registrar controladores de eventos para captura de datos y finalización de grabación
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        // Inicialización y Declaración: Escritor WaveFileWriter para guardar los bytes a disco
        _writer = new WaveFileWriter(rutaArchivoSalida, _waveIn.WaveFormat);
        
        // Operación: Iniciar la captura de audio asíncrona
        _waveIn.StartRecording();
    }

    /// <summary>
    /// Detiene la grabación actual de forma segura y cierra el flujo hacia el archivo.
    /// </summary>
    public void DetenerGrabacion()
    {
        // Operación: Solicitar la detención de la captura al dispositivo WaveIn si está activo
        if (_waveIn != null)
        {
            _waveIn.StopRecording();
        }
    }

    /// <summary>
    /// Controlador de evento disparado cuando NAudio tiene un buffer de audio capturado listo para procesar.
    /// </summary>
    /// <param name="sender">Origen del evento.</param>
    /// <param name="e">Argumentos del evento que contienen el buffer de bytes grabados.</param>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Operación: Escribir los bytes capturados del búfer directamente en el archivo destino
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    /// <summary>
    /// Controlador de evento disparado cuando se detiene el proceso de grabación en NAudio.
    /// Libera los recursos del escritor y del dispositivo.
    /// </summary>
    /// <param name="sender">Origen del evento.</param>
    /// <param name="e">Argumentos de detención.</param>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // Operación: Cerrar y liberar el escritor de archivos
        _writer?.Dispose();
        _writer = null;

        // Operación: Liberar y desechar el dispositivo WaveIn
        if (_waveIn != null)
        {
            _waveIn.Dispose();
            _waveIn = null;
        }
    }

    /// <summary>
    /// Libera los recursos administrados por la clase.
    /// </summary>
    public void Dispose()
    {
        // Operación: Detener grabación y suprimir la finalización por parte del recolector de basura
        DetenerGrabacion();
        GC.SuppressFinalize(this);
    }
}
