using System;
using System.Collections;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

/// <summary>
/// Ordenador avanzado que implementa <see cref="IComparer"/> para ordenar registros 
/// del ListView de forma inteligente (ej. considerando unidades de tamaño en lugar de orden alfabético simple).
/// </summary>
public class ListViewSorter : IComparer
{
    public int ColumnToSort { get; set; }
    public SortOrder OrderOfSort { get; set; }

    public ListViewSorter()
    {
        ColumnToSort = 0; // Por defecto ordena por Nombre
        OrderOfSort = SortOrder.Ascending;
    }

    /// <summary>
    /// Compara dos filas del ListView. Mantiene las carpetas siempre arriba y evalúa 
    /// los valores según el tipo de columna (Fechas reales, Tamaños en bytes, Textos).
    /// </summary>
    public int Compare(object? x, object? y)
    {
        ListViewItem? itemX = x as ListViewItem;
        ListViewItem? itemY = y as ListViewItem;

        if (itemX == null || itemY == null) return 0;

        // 1. REGLA DE ORO MAC/WINDOWS: Las carpetas SIEMPRE van primero
        bool xEsCarpeta = itemX.SubItems[1].Text == "Carpeta";
        bool yEsCarpeta = itemY.SubItems[1].Text == "Carpeta";

        if (xEsCarpeta && !yEsCarpeta) return -1;
        if (!xEsCarpeta && yEsCarpeta) return 1;

        int compareResult = 0;

        // 2. EXTRAER LOS TEXTOS A COMPARAR
        string textX = itemX.SubItems[ColumnToSort].Text;
        string textY = itemY.SubItems[ColumnToSort].Text;

        // 3. ORDENAMIENTO INTELIGENTE DEPENDIENDO DE LA COLUMNA
        if (ColumnToSort == 2) // Columna "Tamaño"
        {
            double bytesX = ParsearTamano(textX);
            double bytesY = ParsearTamano(textY);
            compareResult = bytesX.CompareTo(bytesY);
        }
        else if (ColumnToSort == 4) // <--- NUEVA: Columna "Fecha Modificación"
        {
            DateTime.TryParse(textX, out DateTime dateX);
            DateTime.TryParse(textY, out DateTime dateY);
            compareResult = DateTime.Compare(dateX, dateY);
        }
        else
        {
            // Ordenamiento alfabético normal (Nombre, Tipo, Info)
            compareResult = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
        }

        // 4. APLICAR DIRECCIÓN (Ascendente o Descendente)
        if (OrderOfSort == SortOrder.Descending)
        {
            compareResult = -compareResult; // Invierte el resultado matemático
        }

        return compareResult;
    }

    /// <summary>
    /// Convierte textos legibles de tamaño (ej: "1.5 MB") a números puros en bytes 
    /// para permitir una comparación matemática precisa en lugar de una comparación alfabética.
    /// </summary>
    /// <param name="tamanoTexto">Cadena de texto con el formato de tamaño.</param>
    /// <returns>Valor numérico del peso en bytes.</returns>
    private double ParsearTamano(string tamanoTexto)
    {
        if (string.IsNullOrWhiteSpace(tamanoTexto) || tamanoTexto == "Carpeta" || tamanoTexto == "0 KB") return 0;

        // Limpiar el texto de posibles caracteres no numéricos excepto el punto/coma y el espacio
        string[] partes = tamanoTexto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 1) return 0;

        string valorTexto = partes[0].Replace(",", "."); // Normalizar a punto decimal
        if (!double.TryParse(valorTexto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valor)) 
            return 0;

        if (partes.Length < 2) return valor;

        return partes[1].ToUpper() switch
        {
            "KB" => valor * 1024,
            "MB" => valor * 1048576,
            "GB" => valor * 1073741824,
            "TB" => valor * 1099511627776,
            _ => valor // "B" (Bytes)
        };
    }
}