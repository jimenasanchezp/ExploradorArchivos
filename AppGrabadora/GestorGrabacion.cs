using System;
using NAudio.Wave;

namespace ExploradorArchivos.AppGrabadora
{
    public class GestorGrabacion : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;

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
