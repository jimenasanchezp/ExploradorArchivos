using System.Text.RegularExpressions;
using System.Xml.Linq;
using ExploradorArchivos.AppDataFusion.Models;

namespace ExploradorArchivos.AppDataFusion.Readers;

public static class XmlDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _idAliases = {
        "_id", "id", "Id", "ID", "codigo", "code", "num", "numero", "idx", "index", "rank", "no"
    };
    private static readonly string[] _nombreAliases = {
        "micrositio", "nombre", "name", "titulo", "title", "descripcion", "description",
        "jugador", "player", "empleado", "employee", "persona", "person",
        "producto", "item", "autor", "author", "atleta", "athlete"
    };
    private static readonly string[] _categoriaAliases = {
        "categoria", "category", "departamento", "department",
        "genero", "gender", "sexo", "sex",
        "tipo", "type", "clase", "class", "grupo", "group",
        "region", "pais", "country", "nivel", "level",
        "clasificacion", "division", "segmento", "segment"
    };
    private static readonly string[] _valorAliases = {
        "Numero_de_notas_transmitidas",
        "valor", "value", "salario", "salary", "precio", "price",
        "score", "puntos", "points", "total", "monto", "amount",
        "frec", "frecuencia", "frequency", "count", "cantidad",
        "ventas", "sales", "edad_media", "edad", "age", "promedio", "avg"
    };
    private static readonly string[] _fechaAliases = {
        "periodo", "fecha", "date", "fechaContratacion", "hireDate",
        "anio", "year", "period",
        "created_at", "updated_at", "timestamp", "fecha_registro"
    };

    // Regex: captura tags cuyo nombre contiene espacios
    // Ejemplo: <Numero de notas transmitidas> o </Numero de notas transmitidas>
    private static readonly Regex _tagConEspacios = new(
        @"<(/?)\s*([^>\s/""'=]+(?:\s+[^>=/\s""']+)+)\s*(/?)\s*>",
        RegexOptions.Compiled);

    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();
        UltimasColumnas = new List<string>();
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[XML] Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            // Nota: El reemplazo de tags con espacios requiere cargar el contenido en memoria
            // para aplicar el Regex, a menos que se use un XmlReader personalizado.
            // Para mantener la lógica de heurística y limpieza, usamos un StreamReader.
            string contenido;
            using (var sr = new StreamReader(File.OpenRead(rutaArchivo)))
            {
                contenido = sr.ReadToEnd();
            }

            // Reemplazar espacios en nombres de tags ANTES de parsear
            contenido = _tagConEspacios.Replace(contenido, m =>
            {
                string slash1 = m.Groups[1].Value;          // "/" si es cierre
                string nombre = m.Groups[2].Value.Replace(" ", "_").Trim();
                string selfClose = m.Groups[3].Value;        // "/" si es self-closing
                return $"<{slash1}{nombre}{(selfClose == "/" ? "/" : "")}>";
            });

            var doc = XDocument.Parse(contenido);
            var root = doc.Root;
            if (root == null) return lista;

            var elementos = ObtenerElementosDatos(root);

            if (elementos.Count == 0)
            {
                Console.WriteLine($"[XML] No se encontraron elementos en {Path.GetFileName(rutaArchivo)}");
                return lista;
            }

            DetectarMetadatos(elementos[0]);

            bool faltaCategoria = !MapeoColumnas.ContainsValue("categoria");
            bool faltaValor = !MapeoColumnas.ContainsValue("valor");
            bool faltaNombre = !MapeoColumnas.ContainsValue("nombre");

            int contador = 1;
            foreach (var el in elementos)
            {
                try
                {
                    var item = new DataItem { Fuente = "xml" };

                    item.Id = LeerEnteroAtributoOHijo(el, _idAliases) ?? contador;
                    item.Nombre = LeerCadena(el, _nombreAliases) ?? "";
                    item.Categoria = LeerCadena(el, _categoriaAliases) ?? "";
                    item.Valor = LeerDouble(el, _valorAliases) ?? 0;
                    item.Fecha = LeerFecha(el, _fechaAliases) ?? DateTime.Now;

                    // Solo excluir los campos que realmente fueron mapeados a propiedades
                    // estándar. No usar todos los aliases — si un campo coincide con un alias
                    // pero otro campo ya tomó ese rol, el primero debe ir a CamposExtra.
                    var mapeadas = new HashSet<string>(
                        MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);

                    foreach (var attr in el.Attributes())
                    {
                        if (mapeadas.Contains(attr.Name.LocalName)) continue;
                        item.CamposExtra[attr.Name.LocalName] = attr.Value;
                    }
                    foreach (var hijo in el.Elements())
                    {
                        string localName = hijo.Name.LocalName;
                        if (mapeadas.Contains(localName)) continue;
                        item.CamposExtra[localName] = hijo.Value.Trim();
                    }

                    lista.Add(item);
                    contador++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[XML] Error en elemento #{contador}: {ex.Message}");
                    contador++;
                }
            }

            if (lista.Count > 0)
            {
                if (faltaCategoria) AplicarHeuristicaCategoria(lista);
                if (faltaValor) AplicarHeuristicaValor(lista);
                if (faltaNombre) AplicarHeuristicaNombre(lista);
            }

            Console.WriteLine($"[XML] {lista.Count} registros leidos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[XML] Error leyendo XML: {ex.Message}");
        }

        return lista;
    }

    // Obtiene el nivel correcto: <data><row> -> devuelve los <row>
    private static List<XElement> ObtenerElementosDatos(XElement root)
    {
        var hijos = root.Elements().ToList();
        if (hijos.Count == 0) return hijos;

        if (hijos[0].Attributes().Any() || hijos[0].Elements().Any())
            return hijos;

        var nietos = hijos.SelectMany(h => h.Elements()).ToList();
        if (nietos.Count > 0 && (nietos[0].Attributes().Any() || nietos[0].Elements().Any()))
            return nietos;

        return hijos;
    }

    private static void DetectarMetadatos(XElement primerElemento)
    {
        var nombres = new List<string>();
        foreach (var attr in primerElemento.Attributes())
            nombres.Add(attr.Name.LocalName);
        foreach (var hijo in primerElemento.Elements())
            nombres.Add(hijo.Name.LocalName);

        MapeoColumnas.Clear();
        string? c;
        c = BuscarEnLista(nombres, _idAliases); if (c != null) MapeoColumnas[c] = "id";
        c = BuscarEnLista(nombres, _nombreAliases); if (c != null) MapeoColumnas[c] = "nombre";
        c = BuscarEnLista(nombres, _categoriaAliases); if (c != null) MapeoColumnas[c] = "categoria";
        c = BuscarEnLista(nombres, _valorAliases); if (c != null) MapeoColumnas[c] = "valor";
        c = BuscarEnLista(nombres, _fechaAliases); if (c != null) MapeoColumnas[c] = "fecha";

        UltimasColumnas = new List<string>(nombres);
    }

    private static void AplicarHeuristicaCategoria(List<DataItem> items)
    {
        string? k = BuscarMejorClaveCategoria(items);
        if (k == null) return;
        foreach (var item in items)
            if (item.CamposExtra.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
            { item.Categoria = v.Trim(); item.CamposExtra.Remove(k); }
        MapeoColumnas[k] = "categoria";
    }

    private static void AplicarHeuristicaValor(List<DataItem> items)
    {
        string? k = BuscarMejorClaveNumerica(items);
        if (k == null) return;
        foreach (var item in items)
            if (item.CamposExtra.TryGetValue(k, out var raw) &&
                double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
            { item.Valor = v; item.CamposExtra.Remove(k); }
        MapeoColumnas[k] = "valor";
    }

    private static void AplicarHeuristicaNombre(List<DataItem> items)
    {
        string? k = BuscarMejorClaveTexto(items, null);
        if (k == null) return;
        foreach (var item in items)
            if (item.CamposExtra.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) &&
                item.Nombre.StartsWith("Item-", StringComparison.OrdinalIgnoreCase))
            { item.Nombre = v.Trim(); item.CamposExtra.Remove(k); }
        MapeoColumnas[k] = "nombre";
    }

    private static string? BuscarMejorClaveCategoria(List<DataItem> items)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        string? mejor = null; int mejorPuntaje = 0;
        foreach (var key in candidatos)
        {
            int noVacios = 0, noNumericos = 0;
            var unicos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (!item.CamposExtra.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) continue;
                noVacios++; unicos.Add(raw.Trim());
                if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _)) noNumericos++;
            }
            if (noVacios == 0 || noNumericos < Math.Max(2, noVacios / 2)) continue;
            if (unicos.Count <= 1 || unicos.Count > Math.Max(2, items.Count / 2)) continue;
            int puntaje = noVacios + Math.Min(unicos.Count * 2, 30);
            if (puntaje > mejorPuntaje) { mejorPuntaje = puntaje; mejor = key; }
        }
        return mejor;
    }

    private static string? BuscarMejorClaveNumerica(List<DataItem> items)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        string? mejor = null; int mejorPuntaje = 0;
        foreach (var key in candidatos)
        {
            int numericos = 0;
            foreach (var item in items)
                if (item.CamposExtra.TryGetValue(key, out var raw) &&
                    double.TryParse(raw, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    numericos++;
            if (numericos < Math.Max(2, items.Count / 3)) continue;
            if (numericos > mejorPuntaje) { mejorPuntaje = numericos; mejor = key; }
        }
        return mejor;
    }

    private static string? BuscarMejorClaveTexto(List<DataItem> items, string? evitar)
    {
        var candidatos = items.SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !string.Equals(k, evitar, StringComparison.OrdinalIgnoreCase)).ToList();
        string? mejor = null; int mejorPuntaje = 0;
        foreach (var key in candidatos)
        {
            int noVacios = 0, noNumericos = 0;
            foreach (var item in items)
            {
                if (!item.CamposExtra.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) continue;
                noVacios++;
                if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _)) noNumericos++;
            }
            if (noVacios == 0 || noNumericos < Math.Max(2, noVacios / 2)) continue;
            if (noVacios > mejorPuntaje) { mejorPuntaje = noVacios; mejor = key; }
        }
        return mejor;
    }

    private static string? LeerCadenaAtributoOHijo(XElement el, params string[] claves)
    {
        foreach (var c in claves)
        {
            var attr = el.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, c, StringComparison.OrdinalIgnoreCase));
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value)) return attr.Value.Trim();

            var hijo = el.Elements()
                .FirstOrDefault(h => string.Equals(h.Name.LocalName, c, StringComparison.OrdinalIgnoreCase));
            if (hijo != null && !string.IsNullOrWhiteSpace(hijo.Value)) return hijo.Value.Trim();
        }
        return null;
    }

    private static int? LeerEnteroAtributoOHijo(XElement el, params string[] claves)
    {
        var s = LeerCadenaAtributoOHijo(el, claves);
        return s != null && int.TryParse(s, out int v) ? v : (int?)null;
    }

    private static string? LeerCadena(XElement el, params string[] claves)
        => LeerCadenaAtributoOHijo(el, claves);

    private static double? LeerDouble(XElement el, params string[] claves)
    {
        var s = LeerCadena(el, claves);
        return s != null && double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
    }

    private static DateTime? LeerFecha(XElement el, params string[] claves)
    {
        var s = LeerCadena(el, claves);
        return s != null && DateTime.TryParse(s, out DateTime d) ? d : (DateTime?)null;
    }

    private static string? BuscarEnLista(List<string> lista, string[] aliases)
    {
        foreach (var alias in aliases)
            foreach (var item in lista)
                if (string.Equals(item, alias, StringComparison.OrdinalIgnoreCase))
                    return item;
        return null;
    }
}

