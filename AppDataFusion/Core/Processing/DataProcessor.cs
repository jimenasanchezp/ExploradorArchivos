using System;
using System.Collections.Generic;
using System.Linq;
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Processing;

/// <summary>
/// Clase estática encargada del procesamiento, filtrado, ordenamiento e indexación de datos en memoria.
/// </summary>
public static class DataProcessor
{
    // ==============================================================
    //  SECCIÓN: ADMINISTRACIÓN Y ORGANIZACIÓN DE DATOS (NIVEL 4)
    // ==============================================================

    /// <summary>
    /// Método: AgregarDatos
    /// - Operación: Copia masivamente elementos de la lista 'origen' a la lista 'destino' usando AddRange.
    /// </summary>
    public static List<DataItem> AgregarDatos(List<DataItem> destino, List<DataItem> origen)
    {
        destino.AddRange(origen);
        return destino;
    }

    /// <summary>
    /// Método: AgruparPorCategoria
    /// - Inicializa: Dictionary con comparador insensible a mayúsculas.
    /// - Operación: Agrupa elementos por su Categoria, normalizando valores nulos o vacíos.
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
    /// Método: IndexarPorId
    /// - Inicializa: Dictionary de enteros y DataItem.
    /// - Operación: Crea un índice de acceso rápido por ID único, omitiendo duplicados.
    /// </summary>
    public static Dictionary<int, DataItem> IndexarPorId(List<DataItem> datos)
    {
        var dict = new Dictionary<int, DataItem>();
        foreach (var item in datos)
        {
            dict.TryAdd(item.Id, item);
        }
        return dict;
    }

    // ==============================================================
    //  SECCIÓN: FILTRADO Y PROCESAMIENTO DINÁMICO (NIVEL 5)
    // ==============================================================

    /// <summary>
    /// Método: Filtrar
    /// - Declaración: Búsqueda exacta (exactMatch), valor sanitizado (v), bandera de coincidencia (match).
    /// - Operación: Filtra colecciones buscando coincidencias (parciales o exactas) en propiedades nativas o CamposExtra.
    /// </summary>
    public static List<DataItem> Filtrar(List<DataItem> datos, string campo, string valor, bool exacto = false)
    {
        var resultado = new List<DataItem>();

        bool exactMatch = exacto || (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length >= 2);

        string v = (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length >= 2)
            ? valor.Substring(1, valor.Length - 2).ToLower()
            : valor.ToLower();

        string campoLow = campo.ToLowerInvariant();

        for (int i = 0; i < datos.Count; i++)
        {
            var item = datos[i];

            bool CheckMatch(string? field)
            {
                string f = (field ?? "").Trim().ToLower();
                return exactMatch ? f == v : f.Contains(v);
            }

            // Búsqueda numérica flexible: buscar "100" debe encontrar 100, 100.00, 100.5
            bool CheckNumero(double num)
            {
                string s1 = num.ToString("G").ToLower();
                string s2 = num.ToString("F2").ToLower();
                string s3 = ((long)num).ToString();
                return exactMatch
                    ? s1 == v || s2 == v || s3 == v
                    : s1.Contains(v) || s2.Contains(v) || s3.Contains(v);
            }

            // Búsqueda normalizada en CamposExtra
            string BuscarExtra()
            {
                if (item.CamposExtra.TryGetValue(campo, out var ev)) return ev ?? "";
                string normCampo = DataItem.NormalizarParaComparar(campo);
                foreach (var kv in item.CamposExtra)
                    if (DataItem.NormalizarParaComparar(kv.Key) == normCampo)
                        return kv.Value ?? "";
                return "";
            }

            bool match = campoLow switch
            {
                "nombre"    => CheckMatch(item.Nombre),
                "categoria" => CheckMatch(item.Categoria),
                "fuente"    => CheckMatch(item.Fuente),
                "id"        => item.Id.ToString() == v || item.Id.ToString().Contains(v),
                "valor"     => CheckNumero(item.Valor),
                "fecha"     => CheckMatch(item.Fecha.ToString("yyyy-MM-dd")),
                "latitude"  => CheckMatch(item.Latitude?.ToString("F6")),
                "longitude" => CheckMatch(item.Longitude?.ToString("F6")),

                // Campo dinámico: busca case-insensitive en CamposExtra
                // Si no hay match en CamposExtra (campo no existe), hace búsqueda global
                _ => BuscarExtra() is { } extraVal && extraVal.Length > 0
                        ? CheckMatch(extraVal)
                        : CheckMatch(item.Nombre) || CheckMatch(item.Categoria)
                          || CheckNumero(item.Valor)
                          || item.CamposExtra.Values.Any(ev => CheckMatch(ev))
            };

            if (match)
                resultado.Add(item);
        }
        return resultado;
    }

    /// <summary>
    /// Método: DetectarDuplicados
    /// - Inicializa: Dictionary (vistos) para registrar firmas de registros.
    /// - Operación: Detecta elementos que tengan igual Id, Nombre y Categoría.
    /// </summary>
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

    // ==============================================================
    //  SECCIÓN: CONSULTAS AVANZADAS CON LINQ
    // ==============================================================

    /// <summary>
    /// Método: OrdenarLinq
    /// - Operación: Ordena una colección (ascendente o descendente) dinámicamente con LINQ usando ObtenerLlaveOrdenamiento.
    /// </summary>
    public static List<DataItem> OrdenarLinq(List<DataItem> datos, string campo, bool ascendente = true)
    {
        return ascendente 
            ? datos.OrderBy(d => ObtenerLlaveOrdenamiento(d, campo)).ToList()
            : datos.OrderByDescending(d => ObtenerLlaveOrdenamiento(d, campo)).ToList();
    }

    /// <summary>
    /// Método: ObtenerLlaveOrdenamiento
    /// - Operación: Mapea el campo de ordenamiento a su tipo de dato nativo (int, double, DateTime, string) para que el ordenamiento de LINQ funcione de forma correcta.
    /// </summary>
    private static object ObtenerLlaveOrdenamiento(DataItem d, string campo)
    {
        string campoLow = campo.ToLowerInvariant();
        return campoLow switch
        {
            "id"        => d.Id,
            "valor"     => d.Valor,
            "nombre"    => d.Nombre ?? "",
            "categoria" => d.Categoria ?? "",
            "fecha"     => d.Fecha,
            "fuente"    => d.Fuente ?? "",
            "latitude"  => d.Latitude ?? 0.0,
            "longitude" => d.Longitude ?? 0.0,
            // Búsqueda case-insensitive en CamposExtra
            _ => BuscarExtraOrden(d, campo)
        };
    }

    private static string BuscarExtraOrden(DataItem d, string campo)
    {
        if (d.CamposExtra.TryGetValue(campo, out var v)) return v ?? "";
        string normCampo = DataItem.NormalizarParaComparar(campo);
        foreach (var kv in d.CamposExtra)
            if (DataItem.NormalizarParaComparar(kv.Key) == normCampo)
                return kv.Value ?? "";
        return "";
    }

    /// <summary>
    /// Método: NormalizarCategoria
    /// - Operación: Valida si la categoría es nula o vacía y le asigna el valor "Sin categoría".
    /// </summary>
    private static string NormalizarCategoria(string? categoria)
        => string.IsNullOrWhiteSpace(categoria) ? "Sin categoría" : categoria.Trim();
}
