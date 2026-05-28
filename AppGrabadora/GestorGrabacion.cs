using System;
using NAudio.Wave;

namespace ExploradorArchivos.AppGrabadora
{
    /// <summary>
    /// Maneja la captura asíncrona de audio desde el micrófono utilizando <c>NAudio</c>.
    /// Escribe los flujos en crudo directamente a un archivo WAV en disco.
    /// </summary>
    public class GestorGrabacion : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;

        /// <summary>
        /// Inicializa el dispositivo de grabación y comienza a volcar el audio capturado al archivo especificado.
        /// </summary>
        /// <param name="rutaArchivoSalida">Ruta absoluta del archivo destino (.wav).</param>
        public void IniciarGrabacion(string rutaArchivoSalida)
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(44100, 1)
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _writer = new WaveFileWriter(rutaArchivoSalida, _waveIn.WaveFormat);
            _waveIn.StartRecording();
        }

        /// <summary>
        /// Detiene la grabación actual y cierra el flujo hacia el archivo.
        /// </summary>
        public void DetenerGrabacion()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _writer?.Dispose();
            _writer = null;

            if (_waveIn != null)
            {
                _waveIn.Dispose();
                _waveIn = null;
            }
        }

        public void Dispose()
        {
            DetenerGrabacion();
        }
    }
}
