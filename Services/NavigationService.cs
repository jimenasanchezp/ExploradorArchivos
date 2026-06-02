using System.Collections.Generic;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Gestiona el historial y la ruta de navegación activa dentro de la aplicación.
    /// </summary>
    public class NavigationService
    {
        private readonly Stack<string> _historial = new Stack<string>();

        /// <summary>
        /// Obtiene la ruta actual de navegación.
        /// </summary>
        public string RutaActual { get; private set; } = "Inicio";

        /// <summary>
        /// Indica si hay elementos en el historial para retroceder.
        /// </summary>
        public bool PuedeRetroceder => _historial.Count > 0;

        /// <summary>
        /// Registra una nueva ruta de navegación y guarda la anterior en el historial si es diferente.
        /// </summary>
        public void NavegarA(string nuevaRuta, bool guardarHistorial = true)
        {
            if (guardarHistorial && !string.IsNullOrEmpty(RutaActual) && RutaActual != nuevaRuta)
            {
                _historial.Push(RutaActual);
            }
            RutaActual = nuevaRuta;
        }

        /// <summary>
        /// Extrae la última ruta del historial y la establece como la ruta actual.
        /// </summary>
        /// <returns>La ruta a la que se navegó al retroceder.</returns>
        public string Retroceder()
        {
            if (PuedeRetroceder)
            {
                RutaActual = _historial.Pop();
            }
            return RutaActual;
        }

        /// <summary>
        /// Vacía el historial de navegación actual.
        /// </summary>
        public void LimpiarHistorial()
        {
            _historial.Clear();
        }
    }
}
