using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Processing;

/// <summary>
/// Clase encargada de analizar la calidad de los datos de un conjunto de DataItems
/// y generar un informe detallado de anomalías mapeado directamente a los objetos.
/// </summary>
public static class DataQualityAnalyzer
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

    private static readonly string[] PhoneKeywords = { "phone", "telefono", "teléfono", "tel", "celular", "mobile", "cell", "contacto" };
    private static readonly string[] EmailKeywords = { "email", "correo", "mail", "e-mail" };
    private static readonly string[] DateKeywords = { "fecha", "date" };

    public static QualityReport Analyze(List<DataItem> items, List<(string Display, string Clave)> colInfos)
    {
        var report = new QualityReport();
        var vistos = new Dictionary<string, DataItem>();

        // 1. Identificar tipos de columnas
        var phoneKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emailKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, clave) in colInfos)
        {
            string cLow = clave.ToLower();
            if (PhoneKeywords.Any(k => cLow.Contains(k))) phoneKeys.Add(clave);
            if (EmailKeywords.Any(k => cLow.Contains(k))) emailKeys.Add(clave);
            if (DateKeywords.Any(k => cLow.Contains(k))) dateKeys.Add(clave);
        }

        foreach (var item in items)
        {
            // 2. Detección de Duplicados
            // Definimos firma única por contenido estándar relevante
            string firma = $"{item.Id}|{item.Nombre.Trim().ToLower()}|{item.Categoria.Trim().ToLower()}|{item.Valor}";
            if (vistos.ContainsKey(firma))
            {
                report.DuplicateItems.Add(vistos[firma]);
                report.DuplicateItems.Add(item);
            }
            else
            {
                vistos[firma] = item;
            }

            // Registrar errores del item
            var itemErrors = new Dictionary<string, (string ErrorType, string OriginalValue, string SuggestedFix)>(StringComparer.OrdinalIgnoreCase);

            foreach (var (display, clave) in colInfos)
            {
                string val = ObtenerValorCampo(item, clave);
                string claveLow = clave.ToLowerInvariant();

                // A. Campos Vacíos o Nulos
                // latitude y longitude son opcionales — no se marcan como vacíos
                bool esOpcional = claveLow is "latitude" or "longitude";
                if (!esOpcional && string.IsNullOrWhiteSpace(val))
                {
                    itemErrors[clave] = ("Empty", val, "El campo no puede estar vacío");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(val)) continue; // opcionales vacíos: sin validaciones adicionales

                // B. Validación de Correo
                if (emailKeys.Contains(clave))
                {
                    if (!IsValidEmail(val))
                    {
                        itemErrors[clave] = ("Email", val, "Formato de correo electrónico inválido (ej. usuario@dominio.com)");
                    }
                }

                // C. Validación de Teléfono
                if (phoneKeys.Contains(clave))
                {
                    string? fix = ValidateAndFixPhone(val);
                    if (fix != null)
                    {
                        string desc = fix.StartsWith("⚠") ? "Número de teléfono inválido" : "Número con formato inconsistente";
                        itemErrors[clave] = ("Phone", val, $"{desc}. Sugerencia: {fix.Replace("⚠", "")}");
                    }
                }

                // D. Validación de Fecha
                if (dateKeys.Contains(clave))
                {
                    string? fix = DetectAndFixDate(val);
                    if (fix != null && fix != val)
                    {
                        string desc = fix.StartsWith("⚠") ? "Fecha con inconsistencia lógica" : "Formato de fecha no estándar";
                        itemErrors[clave] = ("Date", val, $"{desc}. Sugerencia: {fix.Replace("⚠", "")}");
                    }
                }
            }

            if (itemErrors.Count > 0)
            {
                report.ItemErrors[item] = itemErrors;
            }
        }

        return report;
    }

    private static string ObtenerValorCampo(DataItem item, string clave)
    {
        string claveLow = clave.ToLowerInvariant();
        return claveLow switch
        {
            "id"        => item.Id.ToString(),
            "nombre"    => item.Nombre ?? "",
            "categoria" => item.Categoria ?? "",
            "valor"     => item.Valor.ToString("G"),
            "fecha"     => item.Fecha == default ? "" : item.Fecha.ToString("yyyy-MM-dd"),
            "fuente"    => item.Fuente ?? "",
            "latitude"  => item.Latitude?.ToString() ?? "",
            "longitude" => item.Longitude?.ToString() ?? "",
            // Búsqueda case-insensitive en CamposExtra
            _ => BuscarEnExtra(item, clave)
        };
    }

    /// <summary>
    /// Busca un valor en <see cref="DataItem.CamposExtra"/> de forma case-insensitive.
    /// Intenta primero la clave exacta; si no la encuentra, recorre el diccionario
    /// comparando sin importar mayúsculas ni espacios extra al inicio/fin.
    /// </summary>
    private static string BuscarEnExtra(DataItem item, string clave)
    {
        // 1. Clave exacta (camino rápido)
        if (item.CamposExtra.TryGetValue(clave, out var exacto))
            return exacto ?? "";

        // 2. Búsqueda normalizada
        string normClave = DataItem.NormalizarParaComparar(clave);
        foreach (var kv in item.CamposExtra)
        {
            if (DataItem.NormalizarParaComparar(kv.Key) == normClave)
                return kv.Value ?? "";
        }

        return "";
    }


    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        email = email.Trim();
        if (!email.Contains('@')) return false;

        string[] parts = email.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1])) return false;

        if (!parts[1].Contains('.') || parts[1].StartsWith('.') || parts[1].EndsWith('.')) return false;

        string tld = parts[1].Substring(parts[1].LastIndexOf('.') + 1);
        return tld.Length >= 2;
    }

    private static string? ValidateAndFixPhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string digitsOnly = new string(raw.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length == 0)
            return "⚠(sin dígitos)";

        if (digitsOnly.Length == 10)
        {
            return raw.Trim() != digitsOnly ? digitsOnly : null;
        }

        if (digitsOnly.Length > 10 && digitsOnly.Length <= 15)
            return digitsOnly.Substring(digitsOnly.Length - 10);

        return $"⚠{digitsOnly} ({digitsOnly.Length}d - inválido)";
    }

    private static string? DetectAndFixDate(string val)
    {
        if (Regex.IsMatch(val, @"^\d{4}-\d{2}-\d{2}$")) return null;

        // DD/MM/YYYY o YYYY/MM/DD
        var m1 = Regex.Match(val, @"^(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})$");
        if (m1.Success)
        {
            int a = int.Parse(m1.Groups[1].Value);
            int b = int.Parse(m1.Groups[2].Value);
            int y = int.Parse(m1.Groups[3].Value);
            if (y < 100) y += 2000;

            if (a > 12 && b <= 12) return TryDate(y, b, a);
            if (b > 12 && a <= 12) return TryDate(y, a, b);
            return TryDate(y, b, a);
        }

        var m2 = Regex.Match(val, @"^(\d{4})[/\.](\d{1,2})[/\.](\d{1,2})$");
        if (m2.Success)
            return TryDate(int.Parse(m2.Groups[1].Value), int.Parse(m2.Groups[2].Value), int.Parse(m2.Groups[3].Value));

        return "⚠Formato de fecha inválido";
    }

    private static string? TryDate(int y, int m, int d)
    {
        try { return new DateTime(y, m, d).ToString("yyyy-MM-dd"); }
        catch { return "⚠Fecha lógica imposible"; }
    }
}

public class QualityReport
{
    public Dictionary<DataItem, Dictionary<string, (string ErrorType, string OriginalValue, string SuggestedFix)>> ItemErrors { get; } = new();
    public HashSet<DataItem> DuplicateItems { get; } = new();
}
