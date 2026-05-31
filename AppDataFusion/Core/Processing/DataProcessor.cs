using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Processing;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
///   NIVEL 4 · Administración y Organización de Datos
///   NIVEL 5 · Procesamiento sin LINQ
///   BONUS   · LINQ
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static class DataProcessor
{
    // ──────────────────────────────────────────────────────────────
    // NIVEL 4 – List<T> + Dictionary
    // ──────────────────────────────────────────────────────────────

    /// <summary>Agrega los elementos de 'origen' a 'destino' y devuelve la lista ampliada.</summary>
    public static List<DataItem> AgregarDatos(List<DataItem> destino, List<DataItem> origen)
    {
        destino.AddRange(origen);
        return destino;
    }

    /// <summary>
    /// Agrupa todos los items por Categoría.
    /// Clave → categoría | Valor → lista de DataItems de esa categoría.
    /// </summary>
    public static Dictionary<string, List<DataItem>> AgruparPorCategoria(List<DataItem> datos)
    {
        var dict = new Dictionary<string, List<DataItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in datos)
        {
            string categoria = NormalizarCategoria(item.Categoria);
            if (!dict.ContainsKey(categoria))
                dict[categoria] = new List<DataItem>();
            dict[categoria].Add(item);
        }
        return dict;
    }

    /// <summary>
    /// Índice de acceso rápido por ID.
    /// Clave → ID | Valor → DataItem (el primero si hay duplicados de ID).
    /// </summary>
    public static Dictionary<int, DataItem> IndexarPorId(List<DataItem> datos)
    {
        var dict = new Dictionary<int, DataItem>();
        foreach (var item in datos)
            dict.TryAdd(item.Id, item);
        return dict;
    }

    /// <summary>
    /// Índice de acceso rápido por Fuente.
    /// Clave → fuente ("json", "csv", etc.) | Valor → lista de DataItems de esa fuente.
    /// </summary>
    public static Dictionary<string, List<DataItem>> AgruparPorFuente(List<DataItem> datos)
    {
        var dict = new Dictionary<string, List<DataItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in datos)
        {
            if (!dict.ContainsKey(item.Fuente))
                dict[item.Fuente] = new List<DataItem>();
            dict[item.Fuente].Add(item);
        }
        return dict;
    }

    // ──────────────────────────────────────────────────────────────
    // NIVEL 5 – Procesamiento SIN LINQ
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Filtra la lista por campo y valor (sin LINQ).
    /// Soporta tanto las propiedades estándar de DataItem como cualquier clave de CamposExtra,
    /// lo que permite filtrar por columnas de datasets personalizados (ej. "fuel_type", "brand").
    /// </summary>
    public static List<DataItem> Filtrar(List<DataItem> datos, string campo, string valor, bool exacto = false)
    {
        var resultado = new List<DataItem>();
        bool exactMatch = exacto || (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length >= 2);
        string v = (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length >= 2) ? valor.Substring(1, valor.Length - 2).ToLower() : valor.ToLower();
        string campoLow = campo.ToLower();

        for (int i = 0; i < datos.Count; i++)
        {
            var item = datos[i];
            
            bool CheckMatch(string? field)
            {
                string f = (field ?? "").ToLower();
                return exactMatch ? f == v : f.Contains(v);
            }

            bool match = campoLow switch
            {
                "nombre" => CheckMatch(item.Nombre),
                "categoria" => CheckMatch(item.Categoria),
                "fuente" => CheckMatch(item.Fuente),
                "id" => item.Id.ToString() == v,
                "valor" => CheckMatch(item.Valor.ToString("F2")),
                "fecha" => CheckMatch(item.Fecha.ToString("yyyy-MM-dd")),
                "latitude" => CheckMatch(item.Latitude?.ToString("F6")),
                "longitude" => CheckMatch(item.Longitude?.ToString("F6")),
                // Cualquier otro campo → buscar en CamposExtra; si no existe, fallback amplio
                _ => item.CamposExtra != null && item.CamposExtra.TryGetValue(campoLow, out var ev) && ev != null
                     ? CheckMatch(ev)
                     : CheckMatch(item.Nombre) || CheckMatch(item.Categoria)
            };
            if (match) resultado.Add(item);
        }
        return resultado;
    }

    /// <summary>Ordena sin LINQ usando BubbleSort (didáctico) o QuickSort según tamaño.</summary>
    public static List<DataItem> Ordenar(List<DataItem> datos, string campo, bool ascendente = true)
    {
        // Copia para no modificar original
        var lista = new List<DataItem>(datos);

        if (lista.Count <= 20)
            BubbleSort(lista, campo, ascendente);    // Didáctico para pocos datos
        else
            QuickSort(lista, 0, lista.Count - 1, campo, ascendente);

        return lista;
    }

    /// <summary>Detecta duplicados sin LINQ (por Id + Nombre + Categoría).</summary>
    public static List<DataItem> DetectarDuplicados(List<DataItem> datos)
    {
        var duplicados = new List<DataItem>();
        var vistos = new Dictionary<string, bool>();

        for (int i = 0; i < datos.Count; i++)
        {
            string clave = $"{datos[i].Id}|{datos[i].Nombre.ToLower()}|{datos[i].Categoria.ToLower()}";
            if (vistos.ContainsKey(clave))
                duplicados.Add(datos[i]);
            else
                vistos[clave] = true;
        }
        return duplicados;
    }

    /// <summary>Elimina duplicados sin LINQ y devuelve lista limpia.</summary>
    public static List<DataItem> EliminarDuplicados(List<DataItem> datos)
    {
        var limpia = new List<DataItem>();
        var vistos = new Dictionary<string, bool>();

        for (int i = 0; i < datos.Count; i++)
        {
            string clave = $"{datos[i].Id}|{datos[i].Nombre.ToLower()}|{datos[i].Categoria.ToLower()}";
            if (!vistos.ContainsKey(clave))
            {
                vistos[clave] = true;
                limpia.Add(datos[i]);
            }
        }
        return limpia;
    }

    /// <summary>
    /// Estadísticas por categoría en un solo recorrido (evita recorrer múltiples veces).
    /// Usa Dictionary como índice para acceso O(1).
    /// </summary>
    public static Dictionary<string, EstadisticasCategoria> CalcularEstadisticas(List<DataItem> datos)
    {
        var stats = new Dictionary<string, EstadisticasCategoria>(StringComparer.OrdinalIgnoreCase);

        // UN SOLO recorrido para calcular todo
        for (int i = 0; i < datos.Count; i++)
        {
            var item = datos[i];
            string categoria = NormalizarCategoria(item.Categoria);
            if (!stats.ContainsKey(categoria))
                stats[categoria] = new EstadisticasCategoria { Categoria = categoria };

            var s = stats[categoria];
            s.Cantidad++;
            s.SumaValores += item.Valor;
            if (item.Valor > s.ValorMaximo) s.ValorMaximo = item.Valor;
            if (item.Valor < s.ValorMinimo || s.Cantidad == 1) s.ValorMinimo = item.Valor;
        }

        // Calcular promedio
        foreach (var s in stats.Values)
            s.Promedio = s.Cantidad > 0 ? s.SumaValores / s.Cantidad : 0;

        return stats;
    }

    // ──────────────────────────────────────────────────────────────
    // BONUS – LINQ
    // ──────────────────────────────────────────────────────────────

    /// <summary>LINQ: filtrar por categoría.</summary>
    public static IEnumerable<DataItem> FiltrarLinq(List<DataItem> datos, string categoria)
        => datos.Where(d => d.Categoria.Contains(categoria, StringComparison.OrdinalIgnoreCase));

    /// <summary>LINQ: agrupar por categoría.</summary>
    public static IEnumerable<IGrouping<string, DataItem>> AgruparLinq(List<DataItem> datos)
        => datos.GroupBy(d => d.Categoria);

    /// <summary>LINQ: ordenar dinámicamente por cualquier campo.</summary>
    public static List<DataItem> OrdenarLinq(List<DataItem> datos, string campo, bool ascendente = true)
    {
        var query = datos.AsQueryable();
        return ascendente 
            ? datos.OrderBy(d => CompararValor(d, campo)).ToList()
            : datos.OrderByDescending(d => CompararValor(d, campo)).ToList();
    }

    private static object CompararValor(DataItem d, string campo)
    {
        return campo.ToLower() switch
        {
            "id" => d.Id,
            "valor" => d.Valor,
            "nombre" => d.Nombre,
            "categoria" => d.Categoria,
            "fecha" => d.Fecha,
            "fuente" => d.Fuente,
            "latitude" => d.Latitude ?? 0,
            "longitude" => d.Longitude ?? 0,
            _ => d.CamposExtra.TryGetValue(campo.ToLower(), out var v) ? v : ""
        };
    }

    /// <summary>LINQ: top N por valor.</summary>
    public static IEnumerable<DataItem> TopN(List<DataItem> datos, int n = 10)
        => datos.OrderByDescending(d => d.Valor).Take(n);

    // ──────────────────────────────────────────────────────────────
    // Algoritmos de ordenamiento SIN LINQ
    // ──────────────────────────────────────────────────────────────

    private static void BubbleSort(List<DataItem> lista, string campo, bool asc)
    {
        int n = lista.Count;
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < n - i - 1; j++)
                if (Comparar(lista[j], lista[j + 1], campo) > 0 == asc)
                    (lista[j], lista[j + 1]) = (lista[j + 1], lista[j]);
    }

    private static void QuickSort(List<DataItem> lista, int bajo, int alto, string campo, bool asc)
    {
        if (bajo < alto)
        {
            int pi = Particionar(lista, bajo, alto, campo, asc);
            QuickSort(lista, bajo, pi - 1, campo, asc);
            QuickSort(lista, pi + 1, alto, campo, asc);
        }
    }

    private static int Particionar(List<DataItem> lista, int bajo, int alto, string campo, bool asc)
    {
        var pivote = lista[alto];
        int i = bajo - 1;
        for (int j = bajo; j < alto; j++)
        {
            bool cond = asc
                ? Comparar(lista[j], pivote, campo) <= 0
                : Comparar(lista[j], pivote, campo) >= 0;
            if (cond) { i++; (lista[i], lista[j]) = (lista[j], lista[i]); }
        }
        (lista[i + 1], lista[alto]) = (lista[alto], lista[i + 1]);
        return i + 1;
    }

    /// <summary>
    /// Compara dos DataItems por el campo indicado.
    /// Soporta propiedades estándar y cualquier campo en CamposExtra
    /// (con comparación numérica automática cuando los valores son números).
    /// </summary>
    private static int Comparar(DataItem a, DataItem b, string campo)
        => campo.ToLower() switch
        {
            "valor" => a.Valor.CompareTo(b.Valor),
            "nombre" => string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase),
            "categoria" => string.Compare(a.Categoria, b.Categoria, StringComparison.OrdinalIgnoreCase),
            "fecha" => a.Fecha.CompareTo(b.Fecha),
            "id" => a.Id.CompareTo(b.Id),
            "latitude" => (a.Latitude ?? 0).CompareTo(b.Latitude ?? 0),
            "longitude" => (a.Longitude ?? 0).CompareTo(b.Longitude ?? 0),
            // Cualquier campo desconocido → buscar en CamposExtra con comparación inteligente
            _ => CompararCampoExtra(a, b, campo.ToLower())
        };

    /// <summary>
    /// Compara valores de CamposExtra: intenta comparación numérica primero
    /// para que campos como "horsepower" o "mileage" se ordenen correctamente.
    /// </summary>
    private static int CompararCampoExtra(DataItem a, DataItem b, string clave)
    {
        var sa = a.CamposExtra.TryGetValue(clave, out var va) ? va : "";
        var sb = b.CamposExtra.TryGetValue(clave, out var vb) ? vb : "";

        // Intentar comparación numérica primero (evita que "9" > "180635")
        if (double.TryParse(sa, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double da) &&
            double.TryParse(sb, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double db))
            return da.CompareTo(db);

        return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizarCategoria(string? categoria)
        => string.IsNullOrWhiteSpace(categoria) ? "Sin categoría" : categoria.Trim();
}

// ──────────────────────────────────────────────────────────────
/// <summary>Estadísticas acumuladas por categoría.</summary>
public class EstadisticasCategoria
{
    public string Categoria { get; set; } = "";
    public int Cantidad { get; set; }
    public double SumaValores { get; set; }
    public double Promedio { get; set; }
    public double ValorMaximo { get; set; }
    public double ValorMinimo { get; set; }

    public override string ToString()
        => $"{Categoria,-20} | Cant: {Cantidad,4} | Prom: {Promedio,9:F2} | Max: {ValorMaximo,9:F2} | Min: {ValorMinimo,9:F2}";
}

