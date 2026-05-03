using System;
using System.Collections;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

public class ListViewSorter : IComparer
{
    public int ColumnToSort { get; set; }
    public SortOrder OrderOfSort { get; set; }

    public ListViewSorter()
    {
        ColumnToSort = 0; // Por defecto ordena por Nombre
        OrderOfSort = SortOrder.Ascending;
    }

    public int Compare(object x, object y)
    {
        ListViewItem itemX = x as ListViewItem;
        ListViewItem itemY = y as ListViewItem;

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
        else if (ColumnToSort == 3 && DateTime.TryParse(textX, out DateTime dateX) && DateTime.TryParse(textY, out DateTime dateY))
        {
            // Por si en el futuro decides poner Fechas en la última columna
            compareResult = DateTime.Compare(dateX, dateY);
        }
        else
        {
            // Ordenamiento alfabético normal (Nombre, Tipo)
            compareResult = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
        }

        // 4. APLICAR DIRECCIÓN (Ascendente o Descendente)
        if (OrderOfSort == SortOrder.Descending)
        {
            compareResult = -compareResult; // Invierte el resultado matemático
        }

        return compareResult;
    }

    // Helper: Convierte "1.5 MB" a número real para que no se ordene alfabéticamente
    private double ParsearTamano(string tamanoTexto)
    {
        if (string.IsNullOrWhiteSpace(tamanoTexto) || tamanoTexto == "Carpeta") return 0;

        string[] partes = tamanoTexto.Split(' ');
        if (partes.Length != 2) return 0;

        if (!double.TryParse(partes[0], out double valor)) return 0;

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