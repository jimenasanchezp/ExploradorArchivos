using System;
using System.IO;
using System.Collections.Generic;
using System.Linq; // Importa LINQ para realizar consultas sobre colecciones.
using System.Xml.Linq; // Importa XDocument y XElement para manipular XML.
using System.Text.RegularExpressions; // Importa soporte para expresiones regulares.
using ExploradorArchivos.AppDataFusion.Models; // Importa el modelo DataItem.

namespace ExploradorArchivos.AppDataFusion.Readers;

/// <summary>
/// Clase encargada de leer y procesar archivos de formato XML usando LINQ y mapeo semántico.
/// </summary>
public static class XmlDataReader
{
    // Almacena las columnas/nodos detectados en el último archivo XML analizado.
    public static List<string> UltimasColumnas { get; private set; } = new List<string>();

    // Guarda el mapeo de nombres de etiquetas XML a las propiedades estándar.
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Listas de alias en minúsculas para asociar automáticamente las columnas a las propiedades del DataItem.
    private static readonly string[] _idAliases = { "_id", "id", "Id", "ID", "codigo", "code", "num", "numero" };
    private static readonly string[] _nombreAliases = { "nombre", "name", "titulo", "title", "descripcion", "description", "producto", "item" };
    private static readonly string[] _categoriaAliases = { "categoria", "category", "departamento", "department", "tipo", "type", "grupo", "group" };
    private static readonly string[] _valorAliases = { "valor", "value", "precio", "price", "total", "monto", "cantidad", "count", "Numero_de_notas_transmitidas" };
    private static readonly string[] _fechaAliases = { "fecha", "date", "periodo", "period", "anio", "year", "timestamp" };

    // Expresión regular para corregir nombres de etiquetas XML con espacios (ej. "<Numero de notas>" a "<Numero_de_notas>").
    private static readonly Regex _tagConEspacios = new Regex(@"<(/?)\s*([^>\s/""'=]+(?:\s+[^>=/\s""']+)+)\s*(/?)\s*>", RegexOptions.Compiled);

    /// <summary>
    /// Lee un archivo XML, procesa su contenido y devuelve una lista de objetos DataItem.
    /// </summary>
    public static List<DataItem> Leer(string rutaArchivo)
    {
        // Instancia la lista que almacenará los registros resultantes.
        var lista = new List<DataItem>();
        // Limpia el historial de columnas detectadas anteriormente.
        UltimasColumnas.Clear();
        // Limpia el historial de mapeo de columnas anterior.
        MapeoColumnas.Clear();

        // Verifica si el archivo físico existe en la ruta proporcionada.
        if (!File.Exists(rutaArchivo))
        {
            // Escribe en consola la advertencia del archivo no encontrado.
            Console.WriteLine($"[XML] Archivo no encontrado: {rutaArchivo}");
            // Retorna la lista vacía de forma segura.
            return lista;
        }

        try
        {
            // Lee todo el contenido de texto plano del archivo XML.
            string contenido = File.ReadAllText(rutaArchivo);

            // Reemplaza los espacios por guiones bajos en los nombres de las etiquetas.
            contenido = _tagConEspacios.Replace(contenido, m =>
            {
                // Agrupa el carácter '/' de cierre si existe (ej. </tag>).
                string slash1 = m.Groups[1].Value;
                // Extrae el nombre del tag y cambia todos los espacios internos por guiones bajos.
                string nombre = m.Groups[2].Value.Replace(" ", "_").Trim();
                // Agrupa el carácter '/' de auto-cierre si existe (ej. <tag/>).
                string selfClose = m.Groups[3].Value;
                // Retorna la etiqueta formateada correctamente.
                return $"<{slash1}{nombre}{(selfClose == "/" ? "/" : "")}>";
            });

            // Parsea la cadena de texto normalizada a una estructura XDocument en memoria.
            var doc = XDocument.Parse(contenido);
            // Obtiene el nodo raíz del documento parseado.
            var root = doc.Root;
            // Si el nodo raíz es nulo, retorna la lista vacía de inmediato.
            if (root == null) return lista;

            // Busca los elementos del XML que representan los registros de datos reales.
            var elementos = ObtenerElementosDatos(root);
            // Si no se encuentra ningún registro, retorna la lista vacía de inmediato.
            if (elementos.Count == 0) return lista;

            // Detecta la estructura de campos y crea el mapeo automático usando el primer elemento.
            DetectarMetadatos(elementos[0]);

            // Inicializa un contador para asignar IDs autoincrementables de respaldo.
            int contador = 1;
            // Recorre cada uno de los elementos XML detectados como registros.
            foreach (var el in elementos)
            {
                try
                {
                    // Instancia un nuevo objeto de tipo DataItem y asigna su fuente como XML.
                    var item = new DataItem { Fuente = "xml" };

                    // Mapea la propiedad ID usando aliases; si no existe, asigna el contador incremental.
                    item.Id = LeerEntero(el, _idAliases) ?? contador;
                    // Mapea la propiedad Nombre buscando coincidencias con sus alias.
                    item.Nombre = LeerCadena(el, _nombreAliases) ?? "";
                    // Mapea la propiedad Categoría buscando coincidencias con sus alias.
                    item.Categoria = LeerCadena(el, _categoriaAliases) ?? "";
                    // Mapea la propiedad Valor buscando coincidencias con sus alias; de lo contrario asigna 0.
                    item.Valor = LeerDouble(el, _valorAliases) ?? 0;
                    // Mapea la propiedad Fecha buscando coincidencias con sus alias; si no hay, asigna la fecha actual.
                    item.Fecha = LeerFecha(el, _fechaAliases) ?? DateTime.Now;

                    // Crea un conjunto hash con las columnas mapeadas para excluirlas de los campos adicionales.
                    var mapeadas = new HashSet<string>(MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);

                    // Agrega todos los atributos no mapeados al diccionario CamposExtra del DataItem usando LINQ.
                    foreach (var attr in el.Attributes().Where(a => !mapeadas.Contains(a.Name.LocalName)))
                    {
                        // Guarda el atributo como un campo de texto adicional.
                        item.CamposExtra[attr.Name.LocalName] = attr.Value;
                    }

                    // Agrega todos los subelementos no mapeados al diccionario CamposExtra del DataItem usando LINQ.
                    foreach (var hijo in el.Elements().Where(h => !mapeadas.Contains(h.Name.LocalName)))
                    {
                        // Guarda el subelemento limpio como un campo de texto adicional.
                        item.CamposExtra[hijo.Name.LocalName] = hijo.Value.Trim();
                    }

                    // Añade el registro mapeado a la lista de salida.
                    lista.Add(item);
                    // Incrementa el contador de respaldo.
                    contador++;
                }
                catch (Exception ex)
                {
                    // Escribe en consola el error si falla el mapeo de un elemento específico.
                    Console.WriteLine($"[XML] Error en elemento #{contador}: {ex.Message}");
                    // Incrementa el contador para mantener el índice de la siguiente fila correcto.
                    contador++;
                }
            }

            // Muestra en consola la cantidad de registros leídos satisfactoriamente.
            Console.WriteLine($"[XML] {lista.Count} registros leídos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            // Registra en consola cualquier excepción ocurrida en el proceso principal.
            Console.WriteLine($"[XML] Error leyendo XML: {ex.Message}");
        }

        // Retorna la lista resultante.
        return lista;
    }

    /// <summary>
    /// Encuentra los elementos reales de datos, bajando de nivel si la estructura tiene contenedores intermediarios.
    /// </summary>
    private static List<XElement> ObtenerElementosDatos(XElement root)
    {
        // Obtiene todos los elementos hijos del nodo raíz.
        var hijos = root.Elements().ToList();
        // Si no existen hijos, retorna la lista vacía inmediatamente.
        if (hijos.Count == 0) return hijos;

        // Si el primer hijo posee atributos o tiene a su vez elementos internos, es el nivel de datos correcto.
        if (hijos[0].Attributes().Any() || hijos[0].Elements().Any())
        {
            // Retorna los elementos secundarios.
            return hijos;
        }

        // Si es una envoltura sin datos directos, baja un nivel recolectando todos los nietos con LINQ.
        var nietos = hijos.SelectMany(h => h.Elements()).ToList();
        // Si existen nietos y el primero de ellos tiene atributos o subelementos, este es el nivel correcto.
        if (nietos.Count > 0 && (nietos[0].Attributes().Any() || nietos[0].Elements().Any()))
        {
            // Retorna los elementos de tercer nivel.
            return nietos;
        }

        // Retorna los hijos si no se pudo profundizar más.
        return hijos;
    }

    /// <summary>
    /// Detecta y mapea las columnas/etiquetas a sus roles según los aliases usando LINQ.
    /// </summary>
    private static void DetectarMetadatos(XElement primerElemento)
    {
        // Reúne los nombres de todos los atributos y elementos secundarios en una sola lista mediante LINQ.
        var nombres = primerElemento.Attributes().Select(a => a.Name.LocalName)
            .Concat(primerElemento.Elements().Select(e => e.Name.LocalName))
            .ToList();

        // Limpia el mapeo global previo.
        MapeoColumnas.Clear();

        // Busca y asigna la columna que representa el ID.
        string? cId = BuscarEnLista(nombres, _idAliases);
        if (cId != null) MapeoColumnas[cId] = "id";

        // Busca y asigna la columna que representa el Nombre.
        string? cNom = BuscarEnLista(nombres, _nombreAliases);
        if (cNom != null) MapeoColumnas[cNom] = "nombre";

        // Busca y asigna la columna que representa la Categoría.
        string? cCat = BuscarEnLista(nombres, _categoriaAliases);
        if (cCat != null) MapeoColumnas[cCat] = "categoria";

        // Busca y asigna la columna que representa el Valor.
        string? cVal = BuscarEnLista(nombres, _valorAliases);
        if (cVal != null) MapeoColumnas[cVal] = "valor";

        // Busca y asigna la columna que representa la Fecha.
        string? cFec = BuscarEnLista(nombres, _fechaAliases);
        if (cFec != null) MapeoColumnas[cFec] = "fecha";

        // Almacena la lista de nombres de columnas detectados en la variable global.
        UltimasColumnas = nombres;
    }

    /// <summary>
    /// Busca un valor de texto en los atributos o hijos del elemento usando LINQ.
    /// </summary>
    private static string? LeerCadena(XElement el, string[] aliases)
    {
        // Itera sobre cada alias de la lista de alias dada.
        foreach (var alias in aliases)
        {
            // Intenta encontrar el primer atributo que coincida de forma insensible con el alias.
            var attr = el.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, alias, StringComparison.OrdinalIgnoreCase));
            // Si el atributo existe y tiene valor, lo devuelve limpio.
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
            {
                return attr.Value.Trim();
            }

            // Intenta encontrar el primer subelemento que coincida de forma insensible con el alias.
            var hijo = el.Elements().FirstOrDefault(h => string.Equals(h.Name.LocalName, alias, StringComparison.OrdinalIgnoreCase));
            // Si el subelemento existe y contiene un texto válido, lo devuelve limpio.
            if (hijo != null && !string.IsNullOrWhiteSpace(hijo.Value))
            {
                return hijo.Value.Trim();
            }
        }
        // Retorna null si el alias no fue localizado o estaba vacío.
        return null;
    }

    // Lee un número entero de un atributo o subelemento XML.
    private static int? LeerEntero(XElement el, string[] aliases)
    {
        // Llama a LeerCadena para obtener el valor del texto crudo.
        string? texto = LeerCadena(el, aliases);
        // Intenta parsear el texto a un tipo int de C# y lo retorna si es exitoso.
        return (texto != null && int.TryParse(texto, out int valor)) ? valor : null;
    }

    // Lee un número decimal double de un atributo o subelemento XML.
    private static double? LeerDouble(XElement el, string[] aliases)
    {
        // Llama a LeerCadena para obtener el valor del texto crudo.
        string? texto = LeerCadena(el, aliases);
        // Intenta parsear a double usando el formato de cultura Invariante (ej. separadores por punto).
        return (texto != null && double.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valor)) ? valor : null;
    }

    // Lee un tipo DateTime de un atributo o subelemento XML.
    private static DateTime? LeerFecha(XElement el, string[] aliases)
    {
        // Llama a LeerCadena para obtener el valor del texto crudo.
        string? texto = LeerCadena(el, aliases);
        // Intenta parsear el texto a una estructura DateTime estándar de C#.
        return (texto != null && DateTime.TryParse(texto, out DateTime valor)) ? valor : null;
    }

    // Encuentra la primera coincidencia de alias dentro de una lista de nombres de columnas.
    private static string? BuscarEnLista(List<string> lista, string[] aliases)
    {
        // Retorna el primer alias que coincida de forma insensible con algún elemento de la lista.
        return aliases.FirstOrDefault(alias => lista.Any(item => string.Equals(item, alias, StringComparison.OrdinalIgnoreCase)));
    }
}
