using System.Drawing;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

/// <summary>
/// Helper para aplicar estilos clásicos pastel a controles.
/// </summary>
public static class ClassicDesignHelper
{
    /// <summary>
    /// Aplica un borde clásico 3D a un control.
    /// </summary>
    public static void AplicarBordeClasico(Control control, Color colorBorde)
    {
        control.Paint += (s, e) =>
        {
            ThemeRenderer.DrawClassicBorder(e.Graphics, control.ClientRectangle, false);
        };
    }
}
