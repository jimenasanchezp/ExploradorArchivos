using ExploradorArchivos.AppDataFusion.Database;
using ExploradorArchivos.AppDataFusion.Models;
using ExploradorArchivos.AppDataFusion.Processing;
using ExploradorArchivos.AppDataFusion.Readers;
using ExploradorArchivos.AppDataFusion.Services;
using System.Data;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppDataFusion;

/// <summary>
/// Interfaz unificada "Data Fusion Arena". 
/// Orquesta la carga (JSON, CSV, XML, TXT, Bases de datos),
/// normalización, geocodificación, visualización en DataGridView y generación de gráficos interactivos.
/// </summary>
public partial class MainForm : Form
{
    // â”€â”€ Data state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly List<DataItem> _datos = new();
    private List<DataItem> _datosBase = new();
    private List<DataItem> _datosVista = new();
    private string? _rutaInicial;
    private bool isDragging = false;
    private bool _isBinding = false;
    private Point lastCursor;

    private Dictionary<string, List<DataItem>> _porCategoria = new();
    private Dictionary<int, DataItem> _porId = new();
    private QualityReport? _qualityReport;

    private PostgreSqlConnector? _lastPgConnector;
    private MariaDbConnector? _lastMdConnector;

    private const int DISPLAY_LIMIT = 75_000;

    private readonly string _dirDatos = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "TestData");

    private List<(string Display, string Clave)> _infoColumnas = new()
    {
        ("ID","id"),("Nombre","nombre"),("CategorÃ­a","categoria"),
        ("Valor","valor"),("Fecha","fecha"),("Fuente","fuente")
    };

    private string _ultimoTipoCargado = "";
    private int _activeUpdates = 0;

    private static readonly List<(string Display, string Clave)> _colsDefault = new()
    {
        ("ID","id"),("Nombre","nombre"),("CategorÃ­a","categoria"),
        ("Valor","valor"),("Fecha","fecha"),("Fuente","fuente"),
        ("Latitud","latitude"),("Longitud","longitude")
    };

    private readonly HashSet<string> _numericDisplays = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _currencyDisplays = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _kwMoneda = {
        "price","precio","monto","costo","cost","revenue",
        "salary","salario","ventas","sales","importe","amount",
        "fee","wage","income","ingreso","earning","pago","payment","ganancia"
    };
    private static bool EsMonedaDisplay(string display) =>
        _kwMoneda.Any(k => display.ToLower().Contains(k));

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  CONSTRUCTOR
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    public MainForm(string? rutaInicial = null)
    {
        _rutaInicial = rutaInicial;
        InitializeComponent();
        ConfigurarSemaforo();
        ConfigurarArrastre();
        ConfigurarEstiloTabControl();

        ConfigurarDataGridViews();
        dgvTodos.CellPainting += DgvTodos_CellPainting!;
        dgvTodos.CellValueChanged += DgvTodos_CellValueChanged!;
        dgvTodos.DataError += DgvTodos_DataError!;
        ThemeRenderer.ApplyTheme(this);

        Text = "DATA FUSION ARENA Â· DATA ENGINE";
        
        Load += async (s, e) =>
        {
            splitCategoria.SplitterDistance = 220;
            
            if (!string.IsNullOrEmpty(_rutaInicial) && File.Exists(_rutaInicial))
            {
                string ext = Path.GetExtension(_rutaInicial).TrimStart('.').ToLower();
                await CargarArchivoAsync(_rutaInicial, ext);
            }
        };

        tabControl1.SelectedIndexChanged += (s, e) =>
        {
            if (tabControl1.SelectedTab == tabGraficas)
                ActualizarChart();
        };

        // API Geocoding Button in sidebar
        btnSbApi.Click += async (s, e) => 
        {
            if (_datos == null || _datos.Count == 0)
            {
                MessageBox.Show("Carga algunos datos primero para usar la API.", "API Geocoding", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            ActualizarEstadoBarra("Llamando a la API de Geocoding...");
            await GeocodingService.IdentificarCoordenadasAsync(_datos);
            await ActualizarTodoAsync();
            ActualizarEstadoBarra("GeocodificaciÃ³n completada.");
        };
    }

    /// <summary>
    /// Configura los botones estilo "Semáforo" (Cerrar, Minimizar, Maximizar) típicos de macOS
    /// para la barra superior de la aplicación.
    /// </summary>
    private void ConfigurarSemaforo()
    {
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0);
        btnClose.Click += (s, e) => Close();
        
        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        
        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40);
        btnMax.Click += (s, e) => 
        {
            this.WindowState = this.WindowState == FormWindowState.Maximized ? 
                FormWindowState.Normal : FormWindowState.Maximized;
        };
        
        pnlSemaforo.Controls.Add(btnClose);
        pnlSemaforo.Controls.Add(btnMin);
        pnlSemaforo.Controls.Add(btnMax);
    }

    /// <summary>
    /// Configura los eventos del mouse para permitir arrastrar la ventana
    /// haciendo clic en el panel superior (pnlLogo).
    /// </summary>
    private void ConfigurarArrastre()
    {
        pnlLogo.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlLogo.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlLogo.MouseUp += (s, e) => { isDragging = false; };
    }

    /// <summary>
    /// Crea un botón circular con estilo plano para el semáforo de la barra de título.
    /// </summary>
    /// <param name="color">Color de fondo del botón.</param>
    /// <param name="x">Posición horizontal del botón dentro del panel.</param>
    /// <returns>El botón configurado.</returns>
    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button btn = new Button
        {
            Size = new Size(12, 12),
            Location = new Point(x, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        
        GraphicsPath path = new GraphicsPath();
        path.AddEllipse(0, 0, 12, 12);
        btn.Region = new Region(path);
        
        return btn;
    }

    /// <summary>
    /// Aplica estilos visuales fijos al TabControl principal para mantener
    /// coherencia con el tema del módulo Data Fusion.
    /// </summary>
    private void ConfigurarEstiloTabControl()
    {
        tabControl1.Appearance = TabAppearance.Normal;
        tabControl1.ItemSize = new Size(160, 42);
        tabControl1.SizeMode = TabSizeMode.Fixed;

        ActualizarEstadoBarra("Listo, carga datos para comenzar.");
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  COLUMN / COMBOBOX MANAGEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Traduce un nombre de columna amigable (Display) a su clave original o interna.
    /// </summary>
    /// <param name="display">Nombre de la columna a buscar.</param>
    /// <returns>La clave correspondiente, o el mismo nombre en minúsculas si no se encuentra.</returns>
    private string TraducirClave(string display)
    {
        foreach (var (d, c) in _infoColumnas)
            if (string.Equals(d, display, StringComparison.OrdinalIgnoreCase))
                return c;
        return display.ToLower();
    }

    /// <summary>
    /// Reconstruye la lista de columnas disponibles (`_infoColumnas`) basándose en el último 
    /// conector o lector de datos utilizado (JSON, CSV, BD, etc.), aplicando mapeos personalizados.
    /// </summary>
    private void ReconstruirInfoColumnas()
    {
        _infoColumnas.Clear();

        bool usarPg = _ultimoTipoCargado == "postgresql" && (_lastPgConnector?.UltimasColumnas.Count ?? 0) > 0;
        bool usarMd = _ultimoTipoCargado == "mariadb" && (_lastMdConnector?.UltimasColumnas.Count ?? 0) > 0;
        bool usarCsv = _ultimoTipoCargado == "csv" && CsvDataReader.UltimasColumnas.Count > 0;
        bool usarJson = _ultimoTipoCargado == "json" && JsonDataReader.UltimasColumnas.Count > 0;
        bool usarXml = _ultimoTipoCargado == "xml" && XmlDataReader.UltimasColumnas.Count > 0;
        bool usarTxt = _ultimoTipoCargado == "txt" && TxtDataReader.UltimasColumnas.Count > 0;

        if (usarPg) BuildFromConnector(_lastPgConnector!.UltimasColumnas, _lastPgConnector.MapeoColumnas);
        else if (usarMd) BuildFromConnector(_lastMdConnector!.UltimasColumnas, _lastMdConnector.MapeoColumnas);
        else if (usarCsv) BuildFromReader(CsvDataReader.UltimasColumnas, CsvDataReader.MapeoColumnas);
        else if (usarJson) BuildFromReader(JsonDataReader.UltimasColumnas, JsonDataReader.MapeoColumnas);
        else if (usarXml) BuildFromReader(XmlDataReader.UltimasColumnas, XmlDataReader.MapeoColumnas);
        else if (usarTxt) BuildFromReader(TxtDataReader.UltimasColumnas, TxtDataReader.MapeoColumnas);
        else
        {
            foreach (var col in _colsDefault) _infoColumnas.Add(col);
            var ya = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "id","nombre","categoria","valor","fecha","fuente" };
            foreach (var k in _datos
                .SelectMany(d => d.CamposExtra.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(k => !ya.Contains(k.ToLower()))
                .OrderBy(k => k))
                _infoColumnas.Add((k, k.ToLower()));
        }
    }

    /// <summary>
    /// Construye la lista de columnas disponibles a partir de los datos leídos de un archivo,
    /// aplicando el mapeo de columnas configurado por el usuario.
    /// </summary>
    /// <param name="columnas">Lista de nombres de columnas originales.</param>
    /// <param name="mapeo">Diccionario de mapeo de columnas a roles predefinidos.</param>
    private void BuildFromReader(List<string> columnas, Dictionary<string, string> mapeo)
    {
        var ya = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnas)
        {
            string clave = mapeo.TryGetValue(col, out var p) ? p.ToLower() : col.ToLowerInvariant();
            _infoColumnas.Add((col, clave));
            ya.Add(col.ToLowerInvariant());
        }
        if (!ya.Contains("fuente")) _infoColumnas.Add(("Fuente", "fuente"));
        if (!ya.Contains("latitude") && _datos.Any(d => d.Latitude.HasValue && d.Latitude != 0))
            _infoColumnas.Add(("Latitud", "latitude"));
        if (!ya.Contains("longitude") && _datos.Any(d => d.Longitude.HasValue && d.Longitude != 0))
            _infoColumnas.Add(("Longitud", "longitude"));
    }

    /// <summary>
    /// Construye la lista de columnas disponibles a partir de una conexión a Base de Datos,
    /// aplicando el mapeo de columnas.
    /// </summary>
    /// <param name="columnas">Lista de nombres de columnas originales de la BD.</param>
    /// <param name="mapeo">Diccionario de mapeo de columnas a roles predefinidos.</param>
    private void BuildFromConnector(List<string> columnas, Dictionary<string, string> mapeo)
    {
        var ya = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnas)
        {
            string clave = mapeo.TryGetValue(col, out var p) ? p.ToLower() : col.ToLowerInvariant();
            _infoColumnas.Add((col, clave));
            ya.Add(col.ToLowerInvariant());
        }
        if (!ya.Contains("fuente")) _infoColumnas.Add(("Fuente", "fuente"));
        if (!ya.Contains("latitude") && _datos.Any(d => d.Latitude.HasValue && d.Latitude != 0))
            _infoColumnas.Add(("Latitud", "latitude"));
        if (!ya.Contains("longitude") && _datos.Any(d => d.Longitude.HasValue && d.Longitude != 0))
            _infoColumnas.Add(("Longitud", "longitude"));
    }

    /// <summary>
    /// Refresca los elementos de los Comboboxes de la interfaz de búsqueda, ordenamiento 
    /// y filtros basándose en la lista de columnas actualizada.
    /// </summary>
    private void RefrescarComboboxes()
    {
        var items = _infoColumnas.Select(c => c.Display).Distinct().ToArray<object>();

        string pf = cmbCampoBusqueda.Text;
        cmbCampoBusqueda.Items.Clear(); cmbCampoBusqueda.Items.AddRange(items);
        int f = cmbCampoBusqueda.FindStringExact(pf);
        cmbCampoBusqueda.SelectedIndex = f >= 0 ? f : 0;

        string po = cmbCampoOrden.Text;
        cmbCampoOrden.Items.Clear(); cmbCampoOrden.Items.AddRange(items);
        int o = cmbCampoOrden.FindStringExact(po);
        if (o < 0) o = _infoColumnas.FindIndex(c => c.Clave == "valor");
        cmbCampoOrden.SelectedIndex = Math.Max(0, o);

        if (cmbLinqCampo != null)
        {
            string pl = cmbLinqCampo.Text;
            cmbLinqCampo.Items.Clear(); cmbLinqCampo.Items.AddRange(items);
            int l = cmbLinqCampo.FindStringExact(pl);
            cmbLinqCampo.SelectedIndex = Math.Max(0, l >= 0 ? l : 0);
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  NUMERIC / CURRENCY DETECTION
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Analiza una muestra de los datos cargados para detectar automáticamente qué columnas
    /// son mayoritariamente numéricas y cuáles podrían representar valores de moneda.
    /// Útil para la renderización dinámica de celdas en el DataGridView y opciones de gráficas.
    /// </summary>
    private void DetectarNumericosYMoneda()
    {
        _numericDisplays.Clear();
        _currencyDisplays.Clear();

        var sample = (_datosBase.Count > 0 ? _datosBase : _datos).Take(40).ToList();

        foreach (var (display, clave) in _infoColumnas)
        {
            if (clave is "id" or "nombre" or "categoria" or "fecha" or "fuente") continue;
            if (clave == "valor")
            {
                if (EsMonedaDisplay(display)) _currencyDisplays.Add(display);
                continue;
            }
            int num = 0, total = 0;
            foreach (var item in sample)
            {
                string v = BuscarExtra(item, clave);
                if (string.IsNullOrEmpty(v)) continue;
                total++;
                if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) num++;
            }
            if (total > 0 && num >= total * 0.75)
            {
                _numericDisplays.Add(display);
                if (EsMonedaDisplay(display)) _currencyDisplays.Add(display);
            }
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  CHART
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Refresca las opciones de los comboboxes de la pestaña de Gráficas
    /// para permitir agrupar y visualizar por las columnas detectadas.
    /// </summary>
    private void RefrescarCombosGrafica()
    {
        if (cmbGrupoGrafica == null || cmbMetricaGrafica == null) return;

        string prevGrupo = cmbGrupoGrafica.Text;
        string prevMetrica = cmbMetricaGrafica.Text;

        cmbGrupoGrafica.Items.Clear();
        cmbMetricaGrafica.Items.Clear();
        cmbMetricaGrafica.Items.Add("Contar registros");

        foreach (var (display, clave) in _infoColumnas)
        {
            cmbGrupoGrafica.Items.Add(display);
            cmbMetricaGrafica.Items.Add(display);
        }

        int gi = cmbGrupoGrafica.FindStringExact(prevGrupo);
        if (gi < 0) { gi = cmbGrupoGrafica.FindStringExact("CategorÃ­a"); if (gi < 0 && cmbGrupoGrafica.Items.Count > 0) gi = 0; }
        if (gi >= 0 && gi < cmbGrupoGrafica.Items.Count) cmbGrupoGrafica.SelectedIndex = gi;

        int mi = cmbMetricaGrafica.FindStringExact(prevMetrica);
        if (mi < 0) mi = 0; // Default to "Contar registros" (index 0)
        if (mi >= 0 && mi < cmbMetricaGrafica.Items.Count) cmbMetricaGrafica.SelectedIndex = mi;
    }

    /// <summary>
    /// Re-calcula los valores agrupados o métricas según la configuración seleccionada por el usuario
    /// y actualiza el control Chart (gráfico) principal.
    /// </summary>
    private void ActualizarChart()
    {
        try
        {
            var fuente = _datosBase.Count > 0 ? _datosBase : _datos.Count > 0 ? _datos : null;
            if (fuente == null || fuente.Count == 0) { chartMain.Limpiar(); return; }

            string grupoDisplay = cmbGrupoGrafica?.Text ?? "";
            string metricaDisplay = cmbMetricaGrafica?.Text ?? "";
            bool contar = metricaDisplay == "Contar registros" || string.IsNullOrEmpty(metricaDisplay);

            string grupoClv = string.IsNullOrEmpty(grupoDisplay)
                ? "categoria"
                : _infoColumnas.FirstOrDefault(c => c.Display == grupoDisplay).Clave ?? "categoria";

            string metricaClv = contar ? ""
                : _infoColumnas.FirstOrDefault(c => c.Display == metricaDisplay).Clave ?? "valor";

            bool esSuma = contar ||
                new[] { "ventas", "sales", "revenue", "total", "ingreso", "cantidad", "count", "units", "unidades" }
                    .Any(k => metricaDisplay.ToLower().Contains(k));

            string GetGrupo(DataItem item)
            {
                if (grupoClv == "fecha") return item.Fecha.ToString("yyyy-MM");
                if (grupoClv == "nombre") return item.Nombre;
                if (grupoClv == "categoria") return string.IsNullOrWhiteSpace(item.Categoria) ? "(sin categorÃ­a)" : item.Categoria;
                if (grupoClv == "fuente") return item.Fuente;
                if (grupoClv == "valor") return item.Valor.ToString("F2");
                if (grupoClv == "id") return item.Id.ToString();
                if (grupoClv == "latitude") return item.Latitude?.ToString("F4") ?? "(vacÃ­o)";
                if (grupoClv == "longitude") return item.Longitude?.ToString("F4") ?? "(vacÃ­o)";
                
                return BuscarExtra(item, grupoClv) is { Length: > 0 } ev ? ev : "(vacÃ­o)";
            }

            double GetValor(DataItem item)
            {
                if (contar) return 1;
                if (metricaClv == "valor") return item.Valor;
                if (metricaClv == "id") return item.Id;
                if (metricaClv == "fecha") return item.Fecha.Day; // fallback
                if (metricaClv == "latitude") return item.Latitude ?? 0;
                if (metricaClv == "longitude") return item.Longitude ?? 0;

                string valStr = BuscarExtra(item, metricaClv);
                return double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
            }

            // Acumular suma Y conteo por grupo
            var sumaGrupo = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var conteoGrupo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in fuente)
            {
                string grupo = GetGrupo(item);
                double val = GetValor(item);

                if (!sumaGrupo.ContainsKey(grupo)) { sumaGrupo[grupo] = 0; conteoGrupo[grupo] = 0; }
                sumaGrupo[grupo] += val;
                conteoGrupo[grupo] += 1;
            }

            // Calcular valor final: promedio o suma segÃºn el tipo de mÃ©trica
            var agrupado = sumaGrupo.ToDictionary(
                kv => kv.Key,
                kv => esSuma
                    ? kv.Value
                    : kv.Value / Math.Max(1, conteoGrupo[kv.Key]),
                StringComparer.OrdinalIgnoreCase);

            var data = agrupado
                .OrderByDescending(kv => kv.Value)
                .Take(12)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();

            if (data.Count == 0) { chartMain.Limpiar(); return; }

            if (!contar && data.All(d => d.Value == 0))
            {
                ActualizarEstadoBarra($"Aviso: La mÃ©trica '{metricaDisplay}' no contiene valores numÃ©ricos vÃ¡lidos.");
            }

            // TÃ­tulo descriptivo: "Conteo", "Suma de X", "Promedio de X"
            string metricaLabel = contar ? "Conteo"
                : esSuma ? $"Suma de {metricaDisplay}"
                : $"Promedio de {metricaDisplay}";

            string grupoLabel = string.IsNullOrEmpty(grupoDisplay) ? "CategorÃ­a" : grupoDisplay;

            var tipo = cmbTipoGrafica.Text switch
            {
                "Barras" => TipoGrafica.Barras,
                "Pastel" => TipoGrafica.Pastel,
                _ => TipoGrafica.Columnas
            };

            chartMain.SetData(data, tipo, $"{metricaLabel}  por  {grupoLabel}");
        }
        catch (Exception ex)
        { chartMain.Limpiar(); ActualizarEstadoBarra($"Error en grÃ¡fica: {ex.Message}"); }
    }

    private void BtnActualizarGrafica_Click(object sender, EventArgs e) => ActualizarChart();
    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();
    private void CmbGrupoGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();
    private void CmbMetricaGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FILE LOADING
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private async void BtnCargarJson_Click(object? sender, EventArgs e) => await CargarConDialogoAsync("json");
    private async void BtnCargarCsv_Click(object? sender, EventArgs e) => await CargarConDialogoAsync("csv");
    private async void BtnCargarXml_Click(object? sender, EventArgs e) => await CargarConDialogoAsync("xml");
    private async void BtnCargarTxt_Click(object? sender, EventArgs e) => await CargarConDialogoAsync("txt");

    /// <summary>
    /// Abre un cuadro de diálogo para que el usuario seleccione un archivo local
    /// e invoca el proceso asíncrono de carga.
    /// </summary>
    /// <param name="extension">La extensión esperada (json, csv, xml, txt).</param>
    private async Task CargarConDialogoAsync(string extension)
    {
        using var dlg = new OpenFileDialog
        {
            Title = $"Seleccionar archivo {extension.ToUpper()}",
            Filter = $"{extension.ToUpper()} (*.{extension})|*.{extension}|Todos los archivos (*.*)|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            await CargarArchivoAsync(dlg.FileName, extension);
    }

    private async void BtnCargarTodo_Click(object? sender, EventArgs e)
    {
        await CargarArchivoAsync(Path.Combine(_dirDatos, "products.json"), "json", true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "sales.csv"), "csv", true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "employees.xml"), "xml", true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "records.txt"), "txt", true);
        _ultimoTipoCargado = "";
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"Todos los archivos cargados â€” {_datos.Count} registros.");
        MessageBox.Show($"Archivos cargados correctamente.\n\nTotal: {_datos.Count} registros.",
            "Data Fusion Arena", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void MenuCargarPersonalizado_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Selecciona un archivo de datos",
            Filter = "Archivos soportados|*.json;*.csv;*.xml;*.txt|JSON|*.json|CSV|*.csv|XML|*.xml|TXT|*.txt"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            await CargarArchivoAsync(dlg.FileName,
                Path.GetExtension(dlg.FileName).TrimStart('.').ToLower());
    }


    /// <summary>
    /// Core de carga de archivos: lee el archivo especificado utilizando el parser
    /// correspondiente según su tipo, lo normaliza a objetos DataItem, y lo agrega al grid central.
    /// </summary>
    /// <param name="ruta">Ruta física del archivo a cargar.</param>
    /// <param name="tipo">Formato del archivo (json, csv, xml, txt).</param>
    /// <param name="silencioso">Si es verdadero, no mostrará pop-ups en caso de error o éxito (útil para cargas batch).</param>
    private async Task CargarArchivoAsync(string ruta, string tipo, bool silencioso = false)
    {
        if (!File.Exists(ruta))
        {
            if (!silencioso)
                MessageBox.Show($"Archivo no encontrado:\n{ruta}",
                    "Archivo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ActualizarEstadoBarra($"Leyendo {Path.GetFileName(ruta)}...");
        var nuevos = await Task.Run(() =>
        {
            switch (tipo)
            {
                case "json": return JsonDataReader.Leer(ruta);
                case "csv": return CsvDataReader.Leer(ruta);
                case "xml":
                    var items = XmlDataReader.Leer(ruta);
                    foreach (var it in items)
                    {
                        if (it.CamposExtra.TryGetValue("departamento", out var dep))
                        { it.Categoria = dep; it.CamposExtra.Remove("departamento"); }
                        if (it.CamposExtra.TryGetValue("salario", out var sal) &&
                            double.TryParse(sal, NumberStyles.Any, CultureInfo.InvariantCulture, out double sv))
                        { it.Valor = sv; it.CamposExtra.Remove("salario"); }
                    }
                    return items;
                case "txt": return TxtDataReader.Leer(ruta);
                default: return new List<DataItem>();
            }
        });

        DataProcessor.AgregarDatos(_datos, nuevos);
        _ultimoTipoCargado = tipo;
        await ActualizarTodoAsync();
        if (!silencioso)
        {
            int emptyCount = 0;
            int duplicateCount = _qualityReport?.DuplicateItems.Count ?? 0;
            int emailCount = 0;
            int phoneCount = 0;
            int dateCount = 0;

            if (_qualityReport != null)
            {
                foreach (var kvp in _qualityReport.ItemErrors.Values)
                {
                    foreach (var err in kvp.Values)
                    {
                        switch (err.ErrorType)
                        {
                            case "Empty": emptyCount++; break;
                            case "Email": emailCount++; break;
                            case "Phone": phoneCount++; break;
                            case "Date": dateCount++; break;
                        }
                    }
                }
            }
            int totalAnomalies = emptyCount + duplicateCount + emailCount + phoneCount + dateCount;

            string statusMsg = $"{nuevos.Count} registros cargados desde {Path.GetFileName(ruta)} - Total: {_datos.Count}";
            ActualizarEstadoBarra(statusMsg);

            if (totalAnomalies > 0)
            {
                MessageBox.Show($"¡Análisis de Calidad Completado!\nSe detectaron {totalAnomalies} anomalías en el archivo:\n\n" +
                                $"- Campos vacíos: {emptyCount}\n" +
                                $"- Registros duplicados: {duplicateCount}\n" +
                                $"- Correos inválidos: {emailCount}\n" +
                                $"- Teléfonos mal formateados: {phoneCount}\n" +
                                $"- Fechas inconsistentes: {dateCount}\n\n" +
                                "Las celdas afectadas se han resaltado en la cuadrícula.",
                                "Control de Calidad de Datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show("¡Análisis de Calidad Completado!\nNo se encontraron anomalías en el archivo cargado.",
                                "Control de Calidad de Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }



    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  EXPORT FILES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void BtnExportarCsv_Click(object? sender, EventArgs e) => _ = ExportarAsync("csv");
    private void BtnExportarJson_Click(object? sender, EventArgs e) => _ = ExportarAsync("json");
    private void BtnExportarXml_Click(object? sender, EventArgs e) => _ = ExportarAsync("xml");
    private void BtnExportarTxt_Click(object? sender, EventArgs e) => _ = ExportarAsync("txt");

    private async void BtnEnviarCorreo_Click(object? sender, EventArgs e)
    {
        var datos = _datosVista.Count > 0 ? _datosVista : _datosBase;
        if (datos.Count == 0)
        {
            MessageBox.Show("No hay datos para enviar.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string tempFile = Path.Combine(Path.GetTempPath(), $"DataFusionArena_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var columnas = _infoColumnas.Select(c => c.Display).ToList();
        var mapeo = _infoColumnas.ToDictionary(c => c.Display, c => c.Clave, StringComparer.OrdinalIgnoreCase);

        ActualizarEstadoBarra($"Preparando archivo para enviar por correo...");
        await Task.Run(() => FileExportService.ExportarCsv(tempFile, datos, columnas, mapeo));

        try
        {
            using var dlg = new FormEnviarCorreoSmtp(tempFile, datos.Count);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ActualizarEstadoBarra("Correo enviado con éxito.");
            }
            else
            {
                ActualizarEstadoBarra("Envío de correo cancelado.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el formulario de envío:\n\n{ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


    /// <summary>
    /// Procesa la exportación asíncrona de los datos actualmente visualizados o cargados
    /// a un archivo local en el formato seleccionado.
    /// </summary>
    /// <param name="formato">El formato de destino (csv, json, xml, txt).</param>
    private async Task ExportarAsync(string formato)
    {
        var datos = _datosVista.Count > 0 ? _datosVista : _datosBase;
        if (datos.Count == 0)
        {
            MessageBox.Show("No hay datos para exportar.",
                "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string filter = formato switch
        {
            "csv" => "CSV (*.csv)|*.csv",
            "json" => "JSON (*.json)|*.json",
            "xml" => "XML (*.xml)|*.xml",
            "txt" => "TXT pipe-separated (*.txt)|*.txt",
            _ => "Todos|*.*"
        };

        using var dlg = new SaveFileDialog
        {
            Title = $"Exportar datos a {formato.ToUpper()}",
            Filter = filter,
            FileName = $"DataFusionArena_Export_{DateTime.Now:yyyyMMdd_HHmmss}.{formato}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        var columnas = _infoColumnas.Select(c => c.Display).ToList();
        var mapeo = _infoColumnas.ToDictionary(
            c => c.Display, c => c.Clave, StringComparer.OrdinalIgnoreCase);

        ActualizarEstadoBarra($"Exportando {datos.Count} registros a {formato.ToUpper()}...");

        try
        {
            var snapshot = new List<DataItem>(datos);
            await Task.Run(() =>
            {
                switch (formato)
                {
                    case "csv": FileExportService.ExportarCsv(dlg.FileName, snapshot, columnas, mapeo); break;
                    case "json": FileExportService.ExportarJson(dlg.FileName, snapshot, columnas, mapeo); break;
                    case "xml": FileExportService.ExportarXml(dlg.FileName, snapshot, columnas, mapeo); break;
                    case "txt": FileExportService.ExportarTxt(dlg.FileName, snapshot, columnas, mapeo); break;
                }
            });

            long bytes = new FileInfo(dlg.FileName).Length;
            string size = bytes >= 1_048_576
                ? $"{bytes / 1_048_576.0:F1} MB"
                : $"{bytes / 1024.0:F0} KB";

            ActualizarEstadoBarra($"Exportado: {Path.GetFileName(dlg.FileName)} ({size})");
            MessageBox.Show(
                $"Exportado correctamente\n\n" +
                $"Formato:   {formato.ToUpper()}\n" +
                $"Registros: {snapshot.Count:N0}\n" +
                $"Tamaño:    {size}\n\n" +
                $"{dlg.FileName}",
                "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ActualizarEstadoBarra($"Error al exportar: {ex.Message}");
            MessageBox.Show($"Error al exportar:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  DATABASE CONNECTION (READ)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Maneja la conexión bidireccional con PostgreSQL. Permite tanto importar datos de la base de datos
    /// al DataGrid, como migrar/guardar los datos actuales en memoria hacia una tabla de PostgreSQL.
    /// </summary>
    private async void BtnConectarPostgres_Click(object sender, EventArgs e)
    {
        bool esEscritura = false;
        if (_datosBase.Count > 0)
        {
            var res = MessageBox.Show(
                $"Se detectaron {_datosBase.Count} registros en memoria.\n\n" +
                "¿Deseas MIGRAR (guardar) estos registros en una tabla de PostgreSQL?\n\n" +
                "- Presiona SÍ para migrar los datos locales a la base de datos.\n" +
                "- Presiona NO para conectar e importar (leer) datos desde la base de datos.",
                "Seleccionar Acción - PostgreSQL",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (res == DialogResult.Cancel) return;
            if (res == DialogResult.Yes) esEscritura = true;
        }

        using var dlg = new FormConexionBD("PostgreSQL", esEscritura);
        if (dlg.ShowDialog() != DialogResult.OK) return;

        if (esEscritura)
        {
            ActualizarEstadoBarra($"Migrando {_datosBase.Count} registros a PostgreSQL (tabla: {dlg.NombreTabla})...");
            try
            {
                var snapshot = new List<DataItem>(_datosVista.Count > 0 ? _datosVista : _datosBase);
                var progreso = new Progress<int>(pct => ActualizarEstadoBarra($"Migrando a PostgreSQL: {pct}%"));

                var result = await DatabaseWriter.EscribirEnPostgreSQLAsync(
                    dlg.CadenaConexion, dlg.NombreTabla, snapshot, _infoColumnas, progreso);

                if (result.Exito)
                {
                    ActualizarEstadoBarra($"Migrado a PostgreSQL: {result.Mensaje}");
                    MessageBox.Show(result.Mensaje, "Migración Completada", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    var pg = new PostgreSqlConnector(dlg.CadenaConexion, dlg.NombreTabla);
                    await Task.Run(() => pg.ObtenerNombresColumnas());
                    _lastPgConnector = pg;
                    _lastMdConnector = null;
                    _ultimoTipoCargado = "postgresql";
                    await ActualizarTodoAsync();
                }
                else
                {
                    ActualizarEstadoBarra($"Error PostgreSQL: {result.Mensaje}");
                    MessageBox.Show(result.Mensaje, "Error en la Migración", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                ActualizarEstadoBarra($"Error: {ex.Message}");
                MessageBox.Show($"Error al migrar a PostgreSQL:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            var pg = new PostgreSqlConnector(dlg.CadenaConexion, dlg.NombreTabla);
            ActualizarEstadoBarra("Conectando a PostgreSQL...");

            bool ok = await Task.Run(() => pg.ProbarConexion(out _));
            if (!ok)
            {
                pg.ProbarConexion(out string err);
                MessageBox.Show($"Error:\n{err}", "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ActualizarEstadoBarra("Error al conectar con PostgreSQL."); return;
            }

            var cols = await Task.Run(() => pg.ObtenerNombresColumnas());
            using var dlgCols = new FormSeleccionColumnas(cols, pg.MapeoColumnas);
            if (dlgCols.ShowDialog() != DialogResult.OK) return;

            pg.SobreescribirMapeo(dlgCols.ColId, dlgCols.ColCategoria, dlgCols.ColValor,
                                  dlgCols.ColNombre, dlgCols.ColFecha);

            ActualizarEstadoBarra("Cargando datos PostgreSQL...");
            var datos = await Task.Run(() => pg.LeerDatos());

            _lastPgConnector = pg;
            _ultimoTipoCargado = "postgresql";
            _datos.RemoveAll(d => d.Fuente == "postgresql");
            DataProcessor.AgregarDatos(_datos, datos);
            await ActualizarTodoAsync();
            ActualizarEstadoBarra($"PostgreSQL: {datos.Count} registros cargados.");
        }
    }

    /// <summary>
    /// Maneja la conexión bidireccional con MariaDB/MySQL. Permite importar datos desde el servidor
    /// o migrar los datos locales masivamente hacia una tabla en MariaDB.
    /// </summary>
    private async void BtnConectarMariaDB_Click(object sender, EventArgs e)
    {
        bool esEscritura = false;
        if (_datosBase.Count > 0)
        {
            var res = MessageBox.Show(
                $"Se detectaron {_datosBase.Count} registros en memoria.\n\n" +
                "¿Deseas MIGRAR (guardar) estos registros en una tabla de MariaDB?\n\n" +
                "- Presiona SÍ para migrar los datos locales a la base de datos.\n" +
                "- Presiona NO para conectar e importar (leer) datos desde la base de datos.",
                "Seleccionar Acción - MariaDB",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (res == DialogResult.Cancel) return;
            if (res == DialogResult.Yes) esEscritura = true;
        }

        using var dlg = new FormConexionBD("MariaDB", esEscritura);
        if (dlg.ShowDialog() != DialogResult.OK) return;

        if (esEscritura)
        {
            ActualizarEstadoBarra($"Migrando {_datosBase.Count} registros a MariaDB (tabla: {dlg.NombreTabla})...");
            try
            {
                var snapshot = new List<DataItem>(_datosVista.Count > 0 ? _datosVista : _datosBase);
                var progreso = new Progress<int>(pct => ActualizarEstadoBarra($"Migrando a MariaDB: {pct}%"));

                var result = await DatabaseWriter.EscribirEnMariaDBAsync(
                    dlg.CadenaConexion, dlg.NombreTabla, snapshot, _infoColumnas, progreso);

                if (result.Exito)
                {
                    ActualizarEstadoBarra($"Migrado a MariaDB: {result.Mensaje}");
                    MessageBox.Show(result.Mensaje, "Migración Completada", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    var md = new MariaDbConnector(dlg.CadenaConexion, dlg.NombreTabla);
                    await Task.Run(() => md.ObtenerNombresColumnas());
                    _lastMdConnector = md;
                    _lastPgConnector = null;
                    _ultimoTipoCargado = "mariadb";
                    await ActualizarTodoAsync();
                }
                else
                {
                    ActualizarEstadoBarra($"Error MariaDB: {result.Mensaje}");
                    MessageBox.Show(result.Mensaje, "Error en la Migración", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                ActualizarEstadoBarra($"Error: {ex.Message}");
                MessageBox.Show($"Error al migrar a MariaDB:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            var md = new MariaDbConnector(dlg.CadenaConexion, dlg.NombreTabla);
            ActualizarEstadoBarra("Conectando a MariaDB...");

            bool ok = await Task.Run(() => md.ProbarConexion(out _));
            if (!ok)
            {
                md.ProbarConexion(out string err);
                MessageBox.Show($"Error:\n{err}", "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ActualizarEstadoBarra("Error al conectar con MariaDB."); return;
            }

            var cols = await Task.Run(() => md.ObtenerNombresColumnas());
            using var dlgCols = new FormSeleccionColumnas(cols, md.MapeoColumnas);
            if (dlgCols.ShowDialog() != DialogResult.OK) return;

            md.SobreescribirMapeo(dlgCols.ColId, dlgCols.ColCategoria, dlgCols.ColValor,
                                  dlgCols.ColNombre, dlgCols.ColFecha);

            ActualizarEstadoBarra("Cargando datos MariaDB...");
            var datos = await Task.Run(() => md.LeerDatos());

            _lastMdConnector = md;
            _ultimoTipoCargado = "mariadb";
            _datos.RemoveAll(d => d.Fuente == "mariadb");
            DataProcessor.AgregarDatos(_datos, datos);
            await ActualizarTodoAsync();
            ActualizarEstadoBarra($"MariaDB: {datos.Count} registros cargados.");
        }
    }

    /// <summary>
    /// Vuelve a consultar la base de datos activa (PostgreSQL o MariaDB) para recargar
    /// los datos más recientes y sincronizar la vista local.
    /// </summary>
    private async void BtnRefresh_Click(object sender, EventArgs e)
    {
        if (_lastPgConnector == null && _lastMdConnector == null)
        {
            MessageBox.Show("No hay bases de datos conectadas.",
                "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        dgvTodos.EndEdit();

        // Esperar a que terminen las actualizaciones asíncronas activas (máximo 3 segundos)
        int intentos = 0;
        while (_activeUpdates > 0 && intentos < 30)
        {
            await Task.Delay(100);
            intentos++;
        }

        _datos.RemoveAll(d => d.Fuente is "postgresql" or "mariadb");
        if (_lastPgConnector != null)
        {
            ActualizarEstadoBarra("Actualizando PostgreSQL...");
            DataProcessor.AgregarDatos(_datos, await Task.Run(() => _lastPgConnector.LeerDatos()));
        }
        if (_lastMdConnector != null)
        {
            ActualizarEstadoBarra("Actualizando MariaDB...");
            DataProcessor.AgregarDatos(_datos, await Task.Run(() => _lastMdConnector.LeerDatos()));
        }
        _ultimoTipoCargado =
            (_lastPgConnector != null && _lastMdConnector == null) ? "postgresql" :
            (_lastMdConnector != null && _lastPgConnector == null) ? "mariadb" : "";
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"Datos actualizados - Total: {_datos.Count}");
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FILTER / SORT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Ejecuta una búsqueda y filtrado de datos en memoria según la columna seleccionada,
    /// actualizando el DataGrid con los resultados.
    /// </summary>
    private async void BtnFiltrar_Click(object? sender, EventArgs e)
    {
        string display = cmbCampoBusqueda.SelectedItem?.ToString() ?? cmbCampoBusqueda.Text;
        string clave = TraducirClave(display), valor = txtBusqueda.Text.Trim();
        ActualizarEstadoBarra("Filtrando...");
        _datosVista = string.IsNullOrEmpty(valor)
            ? new List<DataItem>(_datosBase)
            : await Task.Run(() => DataProcessor.Filtrar(_datosBase, clave, valor, true));
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Filtro '{display}' = '{valor}' -> {_datosVista.Count} resultados.");
    }

    /// <summary>
    /// Restablece la vista de datos al estado original (sin filtros).
    /// </summary>
    private async void BtnLimpiarFiltro_Click(object? sender, EventArgs e)
    {
        txtBusqueda.Text = "";
        _datosVista = new List<DataItem>(_datosBase);
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Filtro limpiado - {_datosVista.Count} registros.");
    }

    /// <summary>
    /// Ordena ascendente o descendentemente la vista actual de datos utilizando LINQ
    /// según la columna configurada.
    /// </summary>
    private async void BtnOrdenar_Click(object? sender, EventArgs e)
    {
        string display = cmbCampoOrden.Text, clave = TraducirClave(display);
        bool asc = rbAscendente.Checked;
        ActualizarEstadoBarra("Ordenando con LINQ...");
        _datosVista = await Task.Run(() => DataProcessor.OrdenarLinq(_datosVista, clave, asc));
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"LINQ: Ordenado por '{display}' {(asc ? "Asc" : "Desc")} - {_datosVista.Count} registros.");
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  CATEGORIES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private async void LstCategorias_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (lstCategorias.SelectedItem is string cat && _porCategoria.TryGetValue(cat, out var items))
        {
            lblCatInfo.Text = $"{cat}: {items.Count} registros";
            await BindGridAsync(dgvCategoria, items, null);
        }
    }



    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  STATISTICS TAB
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•



    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  PROCESSING / LINQ
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Analiza el conjunto de datos activo en busca de registros idénticos o 
    /// extremadamente similares basados en criterios lógicos, y los muestra en la pestaña de procesamiento.
    /// </summary>
    private async void BtnDetectarDuplicados_Click(object? sender, EventArgs e)
    {
        ActualizarEstadoBarra("Detectando duplicados...");
        var dupes = await Task.Run(() => DataProcessor.DetectarDuplicados(_datos));
        await BindGridAsync(dgvProcesamiento, dupes, null);
        lblProcInfo.Text = dupes.Count == 0
            ? "No se encontraron duplicados."
            : $"{dupes.Count} duplicados encontrados.";
        if (dupes.Count > 0) btnEliminarDuplicados.Enabled = true;
        ActualizarEstadoBarra($"Duplicados: {dupes.Count}");
    }

    /// <summary>
    /// Confirma y ejecuta la eliminación de los duplicados detectados del conjunto global de datos.
    /// </summary>
    private async void BtnEliminarDuplicados_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("¿Eliminar duplicados? Esta acción no se puede deshacer.",
            "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int antes = _datos.Count;
        var limpia = await Task.Run(() => DataProcessor.EliminarDuplicados(_datos));
        _datos.Clear(); _datos.AddRange(limpia);
        await ActualizarTodoAsync();
        lblProcInfo.Text = $"Eliminados {antes - _datos.Count}. Quedan {_datos.Count}.";
        btnEliminarDuplicados.Enabled = false;
    }

    /// <summary>
    /// Normaliza una cadena de texto (remueve tildes, diacríticos y convierte a minúsculas)
    /// para facilitar comparaciones y búsquedas insensibles a mayúsculas o acentos.
    /// </summary>
    private static string Normalizar(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";
        var fd = t.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(fd.Length);
        foreach (char c in fd)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>
    /// Ejecuta una consulta dinámica LINQ (.Where) sobre los datos y muestra los resultados en la pestaña de procesos.
    /// </summary>
    private async void BtnLinqWhere_Click(object? sender, EventArgs e)
    {
        string display = cmbLinqCampo.Text, clave = TraducirClave(display), valor = txtLinqFiltro.Text.Trim();
        ActualizarEstadoBarra("LINQ Where...");
        var res = await Task.Run(() => DataProcessor.Filtrar(_datosBase, clave, valor));
        await BindGridAsync(dgvProcesamiento, res, null);
        lblProcInfo.Text = $"LINQ .Where(): {res.Count} registros encontrados.";
    }

    /// <summary>
    /// Ejecuta una consulta dinámica LINQ (.GroupBy) agrupando por categoría y sumarizando los valores.
    /// </summary>
    private async void BtnLinqGroupBy_Click(object? sender, EventArgs e)
    {
        ActualizarEstadoBarra("LINQ GroupBy (Categoría)...");
        var res = await Task.Run(() => _datosBase.GroupBy(d => d.Categoria)
            .Select(g => new DataItem { 
                Id = 0, 
                Nombre = $"Resumen Grupo: {g.Key}", 
                Categoria = g.Key, 
                Valor = g.Sum(x => x.Valor), 
                Fuente = "LINQ GroupBy",
                Fecha = DateTime.Now
            }).ToList());
            
        await BindGridAsync(dgvProcesamiento, res, null);
        lblProcInfo.Text = $"LINQ .GroupBy(): {res.Count} grupos creados por Categoría.";
    }

    /// <summary>
    /// Ejecuta un ordenamiento (.OrderBy) utilizando LINQ puro en memoria.
    /// </summary>
    private async void BtnLinqOrderBy_Click(object? sender, EventArgs e)
    {
        string display = cmbLinqCampo.Text, clave = TraducirClave(display);
        ActualizarEstadoBarra("LINQ OrderBy...");
        var res = await Task.Run(() => DataProcessor.OrdenarLinq(_datosBase, clave, true));
        await BindGridAsync(dgvProcesamiento, res, null);
        lblProcInfo.Text = $"LINQ .OrderBy(): {res.Count} registros ordenados por {display}.";
    }

    private void BtnLinqLimpiar_Click(object? sender, EventArgs e)
    {
        txtLinqFiltro.Text = "";
        dgvProcesamiento.DataSource = null;
        lblProcInfo.Text = "Procesamiento limpiado.";
    }




    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  UPDATE ALL
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Orquestador central de la interfaz. Vuelve a calcular índices, agrupaciones, columnas
    /// y redibuja las gráficas y DataGridViews cuando los datos principales han cambiado.
    /// </summary>
    private async Task ActualizarTodoAsync()
    {
        // El geocoding ahora solo se activa manualmente con el botón de la API para mayor velocidad de carga.
        // await GeocodingService.IdentificarCoordenadasAsync(_datos);

        _porCategoria = DataProcessor.AgruparPorCategoria(_datos);
        _porId = DataProcessor.IndexarPorId(_datos);

        ActualizarFuentesCheckedList();
        _datosBase = GetDatosBase();
        _datosVista = new List<DataItem>(_datosBase);

        ReconstruirInfoColumnas();
        _qualityReport = DataQualityAnalyzer.Analyze(_datosVista, _infoColumnas);
        DetectarNumericosYMoneda();
        RefrescarCombosGrafica();
        RefrescarComboboxes();

        _porCategoria = DataProcessor.AgruparPorCategoria(_datosBase);
        lstCategorias.Items.Clear();
        foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
            lstCategorias.Items.Add(cat);

        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarChart();

        // Update stats bar
        UpdateStatLabels();
    }

    private void UpdateStatLabels()
    {
        int count = _datos.Count;
        int sources = _datos.Select(d => d.Fuente).Distinct().Count();
        lblSubtext.Text = $"{count:N0} registros fusionados - {sources} fuente{(sources != 1 ? "s" : "")} activa{(sources != 1 ? "s" : "")}";

        // Calcular conteo de anomalías
        int emptyCount = 0;
        int duplicateCount = _qualityReport?.DuplicateItems.Count ?? 0;
        int emailCount = 0;
        int phoneCount = 0;
        int dateCount = 0;

        if (_qualityReport != null)
        {
            foreach (var kvp in _qualityReport.ItemErrors.Values)
            {
                foreach (var err in kvp.Values)
                {
                    switch (err.ErrorType)
                    {
                        case "Empty": emptyCount++; break;
                        case "Email": emailCount++; break;
                        case "Phone": phoneCount++; break;
                        case "Date": dateCount++; break;
                    }
                }
            }
        }
        int totalAnomalies = emptyCount + duplicateCount + emailCount + phoneCount + dateCount;

        if (totalAnomalies > 0)
        {
            ActualizarEstadoBarra($"⚠️ Calidad: {totalAnomalies} anomalías detectadas ({emptyCount} vacíos, {duplicateCount} duplicados, {emailCount} correos, {phoneCount} tels, {dateCount} fechas).");
        }
        else
        {
            ActualizarEstadoBarra($"✓ Calidad de datos óptima: 0 anomalías detectadas.");
        }
    }

    private List<DataItem> GetDatosBase()
    {
        if (clbFuentes.Items.Count == 0) return new List<DataItem>(_datos);
        var sel = clbFuentes.CheckedItems.Cast<string>()
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return sel.Count == 0 ? new List<DataItem>(_datos)
                              : _datos.Where(d => sel.Contains(d.Fuente)).ToList();
    }

    private void ActualizarFuentesCheckedList()
    {
        var prevSel = clbFuentes.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prevExist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < clbFuentes.Items.Count; i++)
            prevExist.Add(clbFuentes.Items[i]?.ToString() ?? "");

        clbFuentes.ItemCheck -= ClbFuentes_ItemCheck!;
        clbFuentes.Items.Clear();
        foreach (var f in _datos.Select(d => d.Fuente).Distinct().OrderBy(f => f))
        {
            bool esNueva = !prevExist.Contains(f);
            clbFuentes.Items.Add(f, esNueva || prevSel.Count == 0 || prevSel.Contains(f));
        }
        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
    }

    private async void ClbFuentes_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        BeginInvoke(async () =>
        {
            _datosBase = GetDatosBase();
            _datosVista = new List<DataItem>(_datosBase);
            _porCategoria = DataProcessor.AgruparPorCategoria(_datosBase);
            lstCategorias.Items.Clear();
            foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
                lstCategorias.Items.Add(cat);
            await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
            ActualizarChart();
            UpdateStatLabels();
            ActualizarEstadoBarra($"Mostrando {_datosVista.Count} registros de: " +
                string.Join(", ", clbFuentes.CheckedItems.Cast<string>()));
        });
    }

   
    /// <summary>
    /// Genera y vincula un DataTable dinámico de manera asíncrona a un DataGridView,
    /// aplicando un límite de visualización para mantener el rendimiento gráfico.
    /// </summary>
    private async Task BindGridAsync(DataGridView dgv, List<DataItem> items,
        Label? contadorLabel, bool usarColsDefault = false)
    {
        if (contadorLabel?.IsHandleCreated == true)
            contadorLabel.Invoke(() => contadorLabel.Text = "Cargando...");
        bool limitado = items.Count > DISPLAY_LIMIT;
        var itemsDisplay = limitado ? items.Take(DISPLAY_LIMIT).ToList() : items;
        var colInfos = usarColsDefault
            ? new List<(string, string)>(_colsDefault)
            : new List<(string, string)>(_infoColumnas);

        var numSnap = new HashSet<string>(_numericDisplays, StringComparer.OrdinalIgnoreCase);
        var curSnap = new HashSet<string>(_currencyDisplays, StringComparer.OrdinalIgnoreCase);

        var dt = await Task.Run(() => BuildDataTable(itemsDisplay, colInfos, numSnap, curSnap));

        if (!dgv.IsHandleCreated) return;
        if (dgv.InvokeRequired)
            dgv.Invoke(() => AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel, colInfos));
        else
            AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel, colInfos);
    }

    /// <summary>
    /// Asigna el DataTable creado al DataGridView en el hilo de la interfaz de usuario,
    /// configurando automáticamente el ancho, alineación y estilo de las columnas generadas.
    /// </summary>
    private void AplicarDataTable(DataGridView dgv, DataTable dt, int totalReal, bool limitado,
        Label? contadorLabel, List<(string Display, string Clave)> colInfos)
    {
        _isBinding = true;
        try
        {
            dgv.DataSource = null; dgv.Columns.Clear(); dgv.AutoGenerateColumns = false;
            var cm = colInfos.ToDictionary(c => c.Display, c => c.Clave, StringComparer.OrdinalIgnoreCase);
            string? nombreDisplay = colInfos.FirstOrDefault(c => c.Clave == "nombre").Display;

            foreach (DataColumn col in dt.Columns)
            {
                if (col.ColumnName == "_itemRef_") continue;
                string clave = cm.TryGetValue(col.ColumnName, out var cv) ? cv : col.ColumnName.ToLower();
                var dgvCol = new DataGridViewTextBoxColumn
                {
                    Name = col.ColumnName,
                    HeaderText = col.ColumnName,
                    DataPropertyName = col.ColumnName,
                    ReadOnly = (dgv != dgvTodos) || (clave == "id"),
                    SortMode = DataGridViewColumnSortMode.Automatic,
                    MinimumWidth = 60,
                };
                dgvCol.Width = clave switch
                {
                    "id" => 60,
                    "nombre" => 200,
                    "categoria" => 140,
                    "valor" => 100,
                    "fecha" => 105,
                    "fuente" => 90,
                    _ => 120
                };

                if (!string.IsNullOrEmpty(nombreDisplay) && col.ColumnName == nombreDisplay)
                { dgvCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; dgvCol.MinimumWidth = 150; }

                bool esNumerico = clave is "id" or "valor" || _numericDisplays.Contains(col.ColumnName);
                if (esNumerico)
                {
                    dgvCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dgvCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                dgv.Columns.Add(dgvCol);
            }

            dgv.DataSource = dt;
            dgv.CellFormatting -= DgvCellFormatting!;
            dgv.CellFormatting += DgvCellFormatting!;

            // Forzar dibujo de lÃ­neas si no estÃ¡n por defecto
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.GridColor = Color.LightGray;

            if (contadorLabel != null)
                contadorLabel.Text = limitado
                    ? $"Mostrando {DISPLAY_LIMIT:N0} de {totalReal:N0}"
                    : $"{totalReal:N0} registros";
        }
        finally
        {
            _isBinding = false;
        }
    }

    /// <summary>
    /// Construye en memoria un objeto DataTable mapeando las propiedades fijas y dinámicas (CamposExtra)
    /// de los DataItems según la información de columnas especificada.
    /// </summary>
    private static DataTable BuildDataTable(
        List<DataItem> items,
        List<(string Display, string Clave)> colInfos,
        HashSet<string> numericDisplays,
        HashSet<string> currencyDisplays)
    {
        var dt = new DataTable();
        foreach (var (display, clave) in colInfos)
        {
            var tipo = clave switch { "id" => typeof(int), "valor" => typeof(double), _ => typeof(string) };
            if (!dt.Columns.Contains(display)) dt.Columns.Add(display, tipo);
        }
        dt.Columns.Add("_itemRef_", typeof(DataItem));

        dt.BeginLoadData();
        foreach (var item in items)
        {
            var row = dt.NewRow();
            row["_itemRef_"] = item;
            foreach (var (display, clave) in colInfos)
            {
                if (!dt.Columns.Contains(display)) continue;
                switch (clave)
                {
                    case "id": row[display] = (object)item.Id; break;
                    case "nombre": row[display] = item.Nombre; break;
                    case "categoria": row[display] = item.Categoria; break;
                    case "valor": row[display] = (object)item.Valor; break;
                    case "fecha": row[display] = item.Fecha.ToString("yyyy-MM-dd"); break;
                    case "fuente": row[display] = item.Fuente; break;
                    case "latitude": row[display] = item.Latitude.HasValue ? item.Latitude.Value.ToString("F6") : ""; break;
                    case "longitude": row[display] = item.Longitude.HasValue ? item.Longitude.Value.ToString("F6") : ""; break;
                    default:
                        string raw = BuscarExtra(item, clave);
                        if (!string.IsNullOrEmpty(raw) && currencyDisplays.Contains(display)
                            && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double cv))
                            row[display] = "$" + cv.ToString("N2");
                        else
                            row[display] = raw;
                        break;
                }
            }
            dt.Rows.Add(row);
        }
        dt.EndLoadData();
        return dt;
    }

    /// <summary>
    /// Formatea las celdas en tiempo de renderizado: aplica colores de fondo distintivos
    /// según la fuente de datos y formatea los valores numéricos detectados como moneda.
    /// </summary>
    private void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var dgv = (DataGridView)sender;

        // A. Coloreado predeterminado según fuente
        if (dgv.Columns.Contains("Fuente"))
        {
            try
            {
                var bgColor = dgv.Rows[e.RowIndex].Cells["Fuente"].Value?.ToString() switch
                {
                    "json" => Color.FromArgb(255, 240, 245),
                    "csv" => Color.FromArgb(255, 248, 240),
                    "xml" => Color.FromArgb(240, 248, 255),
                    "txt" => Color.FromArgb(250, 240, 255),
                    "postgresql" => Color.FromArgb(245, 250, 255),
                    "mariadb" => Color.FromArgb(255, 245, 245),
                    "LINQ GroupBy" => Color.FromArgb(240, 255, 245),
                    _ => Color.White
                };
                e.CellStyle.BackColor = bgColor;
                e.CellStyle.ForeColor = Color.FromArgb(43, 45, 66);
                e.CellStyle.SelectionBackColor = Color.FromArgb(255, 77, 109);
                e.CellStyle.SelectionForeColor = Color.White;
            }
            catch { }
        }

        // B. Formateo numérico y de moneda
        if (e.ColumnIndex >= 0 && e.ColumnIndex < dgv.Columns.Count && e.Value is double dv)
        {
            string colHeader = dgv.Columns[e.ColumnIndex].HeaderText;
            if (_currencyDisplays.Contains(colHeader))
            {
                e.Value = "$" + dv.ToString("N2");
                e.FormattingApplied = true;
            }
        }

        // C. Coloreado específico para anomalías de calidad de datos
        try
        {
            var rowView = dgv.Rows[e.RowIndex].DataBoundItem as DataRowView;
            var item = rowView?.Row["_itemRef_"] as DataItem;

            if (item != null && _qualityReport != null)
            {
                string colHeader = dgv.Columns[e.ColumnIndex].HeaderText;
                string? clave = _infoColumnas.FirstOrDefault(c => string.Equals(c.Display, colHeader, StringComparison.OrdinalIgnoreCase)).Clave;
                if (clave == null)
                {
                    clave = _colsDefault.FirstOrDefault(c => string.Equals(c.Display, colHeader, StringComparison.OrdinalIgnoreCase)).Clave;
                }

                // 1. Marcar duplicados (toda la fila)
                if (_qualityReport.DuplicateItems.Contains(item))
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 192, 203); // Rojo suave / Rosa pastel
                    e.CellStyle.ForeColor = Color.Black;
                    dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = "Registro Duplicado.";
                }

                // 2. Colorear celdas con errores específicos
                if (clave != null && _qualityReport.ItemErrors.TryGetValue(item, out var colErrors) && colErrors.TryGetValue(clave, out var err))
                {
                    Color bg = err.ErrorType switch
                    {
                        "Empty" => Color.FromArgb(255, 255, 224), // Amarillo pastel
                        "Email" => Color.FromArgb(255, 228, 181), // Naranja pastel
                        "Phone" => Color.FromArgb(230, 230, 250), // Morado claro
                        "Date" => Color.FromArgb(224, 247, 250),  // Azul suave
                        _ => e.CellStyle.BackColor
                    };

                    e.CellStyle.BackColor = bg;
                    e.CellStyle.ForeColor = Color.Black;
                    dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = err.SuggestedFix;
                }
            }
        }
        catch { }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  GRID SETUP
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Aplica estilos visuales premium a todos los controles DataGridView de la aplicación
    /// de forma estandarizada.
    /// </summary>
    private void ConfigurarDataGridViews()
    {
        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        {
            if (dgv == null) continue;
            dgv.AutoGenerateColumns = false;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.FixedSingle;
            dgv.ReadOnly = (dgv != dgvTodos);
            if (dgv == dgvTodos)
            {
                dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;
            }
            
            // Lineas fijas
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.GridColor = Color.FromArgb(220, 220, 230);

            // Scroll y Columnas (PrevenciÃ³n de Columnas Aplastadas)
            dgv.ScrollBars = ScrollBars.Both;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // Permite el scroll horizontal
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv.RowHeadersWidth = 25;
            
            // Estilo visual premium (Rose/Dark)
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(43, 45, 66);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 48, 90);
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  STATUS BAR
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Actualiza el texto de la barra de estado de manera thread-safe.
    /// </summary>
    private void ActualizarEstadoBarra(string mensaje)
    {
        if (lblStatus.GetCurrentParent()?.InvokeRequired == true)
            lblStatus.GetCurrentParent().Invoke(() => lblStatus.Text = mensaje);
        else
            lblStatus.Text = mensaje;
        Application.DoEvents();
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  HELPERS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Busca de manera segura un valor dentro del diccionario de campos dinámicos (CamposExtra)
    /// ignorando diferencias entre mayúsculas y minúsculas.
    /// </summary>
    private static string BuscarExtra(DataItem item, string clave)
    {
        if (string.IsNullOrEmpty(clave)) return "";
        if (item.CamposExtra.TryGetValue(clave, out var v)) return v;
        foreach (var kv in item.CamposExtra)
            if (string.Equals(kv.Key, clave, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return "";
    }

    /// <summary>
    /// Limpia por completo la memoria de la aplicación, eliminando datos, cachés,
    /// selecciones y reiniciando el estado visual a cero.
    /// </summary>
    private void MenuLimpiarDatos_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("Â¿Limpiar todos los datos en memoria?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _datos.Clear(); _porCategoria.Clear(); _porId.Clear();
        _datosBase.Clear(); _datosVista.Clear();
        _lastPgConnector = null; _lastMdConnector = null; _ultimoTipoCargado = "";
        _numericDisplays.Clear(); _currencyDisplays.Clear();

        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        { if (dgv != null) { dgv.DataSource = null; dgv.Columns.Clear(); } }

        dgvEstadisticas.Rows.Clear();
        lstCategorias.Items.Clear();
        clbFuentes.Items.Clear();
        cmbGrupoGrafica?.Items.Clear();
        cmbMetricaGrafica?.Items.Clear();
        txtLinqFiltro.Text = "";
        lblProcInfo.Text = "Selecciona una operaciÃ³n.";
        btnEliminarDuplicados.Enabled = false;
        chartMain.Limpiar();
        lblContadorTodos.Text = "0 registros";

        _infoColumnas.Clear();
        cmbCampoBusqueda.Items.Clear();
        cmbCampoOrden.Items.Clear();
        cmbLinqCampo?.Items.Clear();

        lblTotalRegistros.Text = "0";
        lblTotalCategorias.Text = "0";
        lblTotalFuentes.Text = "0";

        ActualizarEstadoBarra("Datos limpiados.");
    }

    private void MenuAcercaDe_Click(object sender, EventArgs e) =>
        MessageBox.Show(
            "Data Fusion Arena\n" +
            "AdministraciÃ³n y OrganizaciÃ³n de Datos\n\n" +
            "IngenierÃ­a Â· 4.Âº Semestre Â· C# .NET 10 Â· WinForms\n\n" +
            "Fuentes:  JSON Â· CSV Â· XML Â· TXT Â· PostgreSQL Â· MariaDB\n" +
            "Exportar: CSV Â· JSON Â· XML Â· TXT Â· BD\n" +
            "Estructuras: List<T> Â· Dictionary<TKey,TValue> Â· LINQ",
            "Acerca de Data Fusion Arena",
            MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void MenuSalir_Click(object sender, EventArgs e) => Close();

    private void PrecargarDatosChipsets()
    {
        _datos.Clear();
        
        var comparison = new[]
        {
            ("Socket / ZÃ³calo", "LGA 1851 (Arrow Lake)", "AM5"),
            ("Carriles PCIe 4.0", "24 PCIe 4.0", "12 PCIe 4.0"),
            ("Carriles PCIe 5.0", "24 PCIe 5.0", "24 PCIe 5.0"),
            ("Overclocking RAM", "SÃ­ (CPU y RAM)", "SÃ­ (CPU y todo)"),
            ("Overclocking CPU", "SÃ­ (en CPU)", "SÃ­ (en todo)"),
            ("Puertos USB", "Hasta 4 puertos", "Hasta 8 puertos")
        };

        int id = 1;
        foreach (var (feat, intel, amd) in comparison)
        {
            var item = new DataItem
            {
                Id = id++,
                Nombre = feat,
                Categoria = "CaracterÃ­stica",
                Valor = 100,
                Fuente = "csv",
                Fecha = DateTime.Now
            };
            item.CamposExtra["Intel (Z890)"] = intel;
            item.CamposExtra["AMD (X870E)"] = amd;
            _datos.Add(item);
        }

        _ultimoTipoCargado = "mixed";
        _ = ActualizarTodoAsync();
    }

    /// <summary>
    /// Evento de pintado personalizado (Custom Paint) para dibujar "Badges" (etiquetas redondeadas)
    /// en la columna 'Fuente' de la tabla, logrando un estilo UI moderno.
    /// </summary>
    private void DgvTodos_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (dgvTodos.Columns[e.ColumnIndex].HeaderText != "Fuente") return;

        e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

        var val = e.Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(val)) return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var clrBadge = Color.FromArgb(255, 240, 244);
        var clrText = Color.FromArgb(232, 48, 90);

        var size = g.MeasureString(val, dgvTodos.Font);
        int bw = (int)size.Width + 12;
        int bh = (int)size.Height + 2;
        var rect = new Rectangle(
            e.CellBounds.X + (e.CellBounds.Width - bw) / 2,
            e.CellBounds.Y + (e.CellBounds.Height - bh) / 2,
            bw, bh
        );

        using var br = new SolidBrush(clrBadge);
        var path = GetRoundedRect(rect, 4);
        g.FillPath(br, path);

        using var textBr = new SolidBrush(clrText);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(val, new Font("Syne", 7f, FontStyle.Bold), textBr, rect, sf);

        e.Handled = true;
    }


    private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRect(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Evento disparado cada vez que el usuario edita directamente una celda del DataGridView.
    /// Actualiza el objeto en memoria y lanza asíncronamente la sincronización hacia las bases de datos.
    /// </summary>
    private void DgvTodos_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (_isBinding) return;
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var dgv = (DataGridView)sender;
        var col = dgv.Columns[e.ColumnIndex];
        var row = dgv.Rows[e.RowIndex];

        if (row.DataBoundItem is DataRowView drv)
        {
            var dr = drv.Row;
            if (dr.Table.Columns.Contains("_itemRef_") && dr["_itemRef_"] is DataItem item)
            {
                object newVal = dr[col.Name];
                string valStr = newVal?.ToString() ?? "";

                // Buscar Clave correspondiente en _infoColumnas o _colsDefault
                string? clave = null;
                foreach (var info in _infoColumnas)
                {
                    if (string.Equals(info.Display, col.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        clave = info.Clave;
                        break;
                    }
                }
                if (clave == null)
                {
                    foreach (var info in _colsDefault)
                    {
                        if (string.Equals(info.Display, col.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            clave = info.Clave;
                            break;
                        }
                    }
                }
                if (clave == null) clave = col.Name;

                switch (clave.ToLowerInvariant())
                {
                    case "id":
                        if (newVal is int idInt) item.Id = idInt;
                        else if (int.TryParse(valStr, out int idVal)) item.Id = idVal;
                        break;
                    case "nombre":
                        item.Nombre = valStr;
                        break;
                    case "categoria":
                        item.Categoria = valStr;
                        break;
                    case "valor":
                        if (newVal is double valDbl) item.Valor = valDbl;
                        else if (double.TryParse(valStr, out double dblVal)) item.Valor = dblVal;
                        break;
                    case "fecha":
                        if (newVal is DateTime dtVal) item.Fecha = dtVal;
                        else if (DateTime.TryParse(valStr, out DateTime dtVal2)) item.Fecha = dtVal2;
                        break;
                    case "fuente":
                        item.Fuente = valStr;
                        break;
                    case "latitude":
                        if (double.TryParse(valStr, out double latVal)) item.Latitude = latVal;
                        else item.Latitude = null;
                        break;
                    case "longitude":
                        if (double.TryParse(valStr, out double lonVal)) item.Longitude = lonVal;
                        else item.Longitude = null;
                        break;
                    default:
                        item.CamposExtra[clave] = valStr;
                        break;
                }

                // Actualizar cachés y gráficos de forma ligera
                _porCategoria = DataProcessor.AgruparPorCategoria(_datosBase);
                _porId = DataProcessor.IndexarPorId(_datos);

                ActualizarChart();

                // Refrescar lista de categorías
                lstCategorias.Items.Clear();
                foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
                    lstCategorias.Items.Add(cat);

                ActualizarEstadoBarra($"Registro ID {item.Id} editado: '{col.Name}' modificado a '{valStr}'.");

                // Sincronizar con la base de datos de forma asíncrona si hay una conexión activa
                if (string.Equals(item.Fuente, "postgresql", StringComparison.OrdinalIgnoreCase) && _lastPgConnector != null)
                {
                    _ = SincronizarFilaPostgreSQLAsync(_lastPgConnector, item, col.Name, valStr);
                }
                else if (string.Equals(item.Fuente, "mariadb", StringComparison.OrdinalIgnoreCase) && _lastMdConnector != null)
                {
                    _ = SincronizarFilaMariaDBAsync(_lastMdConnector, item, col.Name, valStr);
                }
                else
                {
                    // Fallbacks por si el Fuente no coincide pero hay una única BD activa
                    if (_lastPgConnector != null && _lastMdConnector == null)
                    {
                        _ = SincronizarFilaPostgreSQLAsync(_lastPgConnector, item, col.Name, valStr);
                    }
                    else if (_lastMdConnector != null && _lastPgConnector == null)
                    {
                        _ = SincronizarFilaMariaDBAsync(_lastMdConnector, item, col.Name, valStr);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sincroniza un cambio de celda específico hacia la base de datos PostgreSQL en tiempo real.
    /// Resuelve automáticamente la Primary Key o el identificador único para construir el comando UPDATE.
    /// </summary>
    private async Task SincronizarFilaPostgreSQLAsync(PostgreSqlConnector connector, DataItem item, string colName, string valStr)
    {
        System.Threading.Interlocked.Increment(ref _activeUpdates);
        try
        {
            // 1. Determinar la columna identificadora (WHERE) usando la PK real de la BD
            string? colId = null;

            // Prioridad 1: PK real detectada desde information_schema
            if (!string.IsNullOrEmpty(connector.ColPrimaryKey) &&
                connector.UltimasColumnas.Any(c => string.Equals(c, connector.ColPrimaryKey, StringComparison.OrdinalIgnoreCase)))
            {
                colId = connector.ColPrimaryKey;
            }

            // Prioridad 2: Columna mapeada como "id"
            if (string.IsNullOrEmpty(colId))
                colId = connector.MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;

            // Prioridad 3: Columna cuyo nombre sea exactamente "id"
            if (string.IsNullOrEmpty(colId))
                colId = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, "id", StringComparison.OrdinalIgnoreCase));

            // Prioridad 4: Primera columna que contenga "id" en su nombre
            if (string.IsNullOrEmpty(colId))
                colId = connector.UltimasColumnas.FirstOrDefault(c => c.ToLowerInvariant().Contains("id"));

            // Prioridad 5 (último recurso): primera columna de la tabla
            if (string.IsNullOrEmpty(colId) && connector.UltimasColumnas.Count > 0)
            {
                colId = connector.UltimasColumnas[0];
                Console.WriteLine($"[PostgreSQL Sync] ⚠ Sin PK — usando primera columna '{colId}' como identificador");
            }

            if (string.IsNullOrEmpty(colId))
            {
                ActualizarEstadoBarra("⚠️ No se puede sincronizar: La tabla PostgreSQL está vacía o sin columnas.");
                return;
            }

            // Normalizar al nombre exacto de la columna en la BD
            var colIdExact = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, colId, StringComparison.OrdinalIgnoreCase));
            if (colIdExact != null) colId = colIdExact;

            // 2. Resolver el nombre de columna de la BD para el campo editado
            string dbColName = colName;
            var match = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, colName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                dbColName = match;
            }
            else
            {
                string sanitized = Database.DatabaseWriter.SanitizarNombre(colName);
                match = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, sanitized, StringComparison.OrdinalIgnoreCase));
                if (match != null) dbColName = match;
                else dbColName = sanitized;
            }

            // 3. Obtener el valor del identificador para el WHERE
            object idVal = item.Id;
            string? extraId = null;

            // Buscar en CamposExtra por nombre exacto de la PK
            foreach (var kv in item.CamposExtra)
            {
                if (string.Equals(kv.Key, colId, StringComparison.OrdinalIgnoreCase))
                {
                    extraId = kv.Value;
                    break;
                }
            }

            // Si no se encontró, buscar por columna mapeada como "id"
            if (string.IsNullOrEmpty(extraId))
            {
                string? mappedIdCol = connector.MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;
                if (mappedIdCol != null)
                {
                    foreach (var kv in item.CamposExtra)
                    {
                        if (string.Equals(kv.Key, mappedIdCol, StringComparison.OrdinalIgnoreCase))
                        {
                            extraId = kv.Value;
                            break;
                        }
                    }
                }
            }

            // Si aún no, buscar en propiedades mapeadas del item
            if (string.IsNullOrEmpty(extraId))
            {
                string? mappedRole = connector.MapeoColumnas.TryGetValue(colId, out var role) ? role : null;
                if (mappedRole != null)
                {
                    extraId = mappedRole switch
                    {
                        "valor"     => item.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "nombre"    => item.Nombre,
                        "categoria" => item.Categoria,
                        "fecha"     => item.Fecha.ToString("yyyy-MM-dd"),
                        _           => null
                    };
                }
            }

            if (!string.IsNullOrEmpty(extraId))
            {
                if (int.TryParse(extraId, out int idInt))
                    idVal = idInt;
                else
                    idVal = extraId;
            }

            Console.WriteLine($"[PostgreSQL Sync] Tabla={connector.Tabla} PK={colId} idVal={idVal} col={dbColName} val={valStr}");

            // 4. Convertir el valor al tipo correcto
            object? dbValue = valStr;
            if (string.Equals(dbColName, "valor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dbColName, "value", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dbColName, "price", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                    dbValue = dblVal;
                else
                    dbValue = DBNull.Value;
            }
            else if (string.IsNullOrEmpty(valStr))
            {
                dbValue = DBNull.Value;
            }

            ActualizarEstadoBarra($"Sincronizando cambio en BD PostgreSQL (tabla: {connector.Tabla})...");
            var (ok, error) = await Database.DatabaseWriter.ActualizarCampoPostgreSQLAsync(
                connector.CadenaConexion,
                connector.Tabla,
                colId,
                idVal,
                dbColName,
                dbValue
            );

            if (ok)
            {
                ActualizarEstadoBarra($"✓ Base de datos PostgreSQL actualizada (ID {idVal}: '{dbColName}' = '{valStr}').");
            }
            else
            {
                ActualizarEstadoBarra($"❌ Error al sincronizar cambio en BD PostgreSQL (ID {idVal}): {error}");
                MessageBox.Show($"Error al guardar cambios en la base de datos PostgreSQL (ID: {idVal}):\n{error}", 
                    "Error de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            ActualizarEstadoBarra($"❌ Error inesperado al sincronizar cambio en PostgreSQL: {ex.Message}");
            MessageBox.Show($"Error inesperado en PostgreSQL:\n{ex.Message}", "Error de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            System.Threading.Interlocked.Decrement(ref _activeUpdates);
        }
    }


    /// <summary>
    /// Sincroniza un cambio de celda específico hacia la base de datos MariaDB en tiempo real.
    /// Resuelve automáticamente la Primary Key o el identificador único para construir el comando UPDATE.
    /// </summary>
    private async Task SincronizarFilaMariaDBAsync(MariaDbConnector connector, DataItem item, string colName, string valStr)
    {
        System.Threading.Interlocked.Increment(ref _activeUpdates);
        try
        {
            // 1. Determinar la columna identificadora (WHERE) usando la PK real de la BD
            string? colId = null;

            // Prioridad 1: PK real detectada desde INFORMATION_SCHEMA
            if (!string.IsNullOrEmpty(connector.ColPrimaryKey) &&
                connector.UltimasColumnas.Any(c => string.Equals(c, connector.ColPrimaryKey, StringComparison.OrdinalIgnoreCase)))
            {
                colId = connector.ColPrimaryKey;
            }

            // Prioridad 2: Columna mapeada como "id"
            if (string.IsNullOrEmpty(colId))
                colId = connector.MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;

            // Prioridad 3: Columna cuyo nombre sea exactamente "id"
            if (string.IsNullOrEmpty(colId))
                colId = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, "id", StringComparison.OrdinalIgnoreCase));

            // Prioridad 4: Primera columna que contenga "id" en su nombre
            if (string.IsNullOrEmpty(colId))
                colId = connector.UltimasColumnas.FirstOrDefault(c => c.ToLowerInvariant().Contains("id"));

            // Prioridad 5 (último recurso): primera columna de la tabla
            if (string.IsNullOrEmpty(colId) && connector.UltimasColumnas.Count > 0)
            {
                colId = connector.UltimasColumnas[0];
                Console.WriteLine($"[MariaDB Sync] ⚠ Sin PK \u2014 usando primera columna '{colId}' como identificador");
            }

            if (string.IsNullOrEmpty(colId))
            {
                ActualizarEstadoBarra("⚠️ No se puede sincronizar: La tabla está vacía o sin columnas.");
                return;
            }


            // Normalizar al nombre exacto de la columna en la BD (case-sensitive)
            var colIdExact = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, colId, StringComparison.OrdinalIgnoreCase));
            if (colIdExact != null) colId = colIdExact;

            // 2. Resolver el nombre de columna de la BD para el campo editado
            string dbColName = colName;
            var match = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, colName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                dbColName = match;
            }
            else
            {
                string sanitized = Database.DatabaseWriter.SanitizarNombre(colName);
                match = connector.UltimasColumnas.FirstOrDefault(c => string.Equals(c, sanitized, StringComparison.OrdinalIgnoreCase));
                if (match != null) dbColName = match;
                else dbColName = sanitized;
            }

            // 3. Obtener el valor del identificador para el WHERE
            //    Buscar primero en CamposExtra usando el nombre real de la PK
            object idVal = item.Id;
            string? extraId = null;

            // Buscar por el nombre exacto de la columna PK en CamposExtra
            foreach (var kv in item.CamposExtra)
            {
                if (string.Equals(kv.Key, colId, StringComparison.OrdinalIgnoreCase))
                {
                    extraId = kv.Value;
                    break;
                }
            }

            // Si no encontramos por el nombre de PK, intentar por la columna mapeada como "id"
            if (string.IsNullOrEmpty(extraId))
            {
                string? mappedIdCol = connector.MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;
                if (mappedIdCol != null)
                {
                    foreach (var kv in item.CamposExtra)
                    {
                        if (string.Equals(kv.Key, mappedIdCol, StringComparison.OrdinalIgnoreCase))
                        {
                            extraId = kv.Value;
                            break;
                        }
                    }
                }
            }

            // Si aún no, buscar en las propiedades mapeadas del item
            // (por si el identificador es una columna mapeada como valor/nombre/etc.)
            if (string.IsNullOrEmpty(extraId))
            {
                string? mappedRole = connector.MapeoColumnas.TryGetValue(colId, out var role) ? role : null;
                if (mappedRole != null)
                {
                    extraId = mappedRole switch
                    {
                        "valor"    => item.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "nombre"   => item.Nombre,
                        "categoria"=> item.Categoria,
                        "fecha"    => item.Fecha.ToString("yyyy-MM-dd"),
                        _          => null
                    };
                }
            }

            if (!string.IsNullOrEmpty(extraId))
            {
                if (int.TryParse(extraId, out int idInt))
                    idVal = idInt;
                else
                    idVal = extraId;
            }


            Console.WriteLine($"[MariaDB Sync] Tabla={connector.Tabla} PK={colId} idVal={idVal} col={dbColName} val={valStr}");

            // 4. Convertir el valor al tipo correcto si es necesario
            object? dbValue = valStr;
            if (string.Equals(dbColName, "valor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dbColName, "value", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dbColName, "price", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                    dbValue = dblVal;
                else
                    dbValue = DBNull.Value;
            }
            else if (string.IsNullOrEmpty(valStr))
            {
                dbValue = DBNull.Value;
            }

            ActualizarEstadoBarra($"Sincronizando cambio en BD MariaDB (tabla: {connector.Tabla})...");
            var (ok, error) = await Database.DatabaseWriter.ActualizarCampoMariaDBAsync(
                connector.CadenaConexion,
                connector.Tabla,
                colId,
                idVal,
                dbColName,
                dbValue
            );

            if (ok)
            {
                ActualizarEstadoBarra($"✓ Base de datos MariaDB actualizada (ID {idVal}: '{dbColName}' = '{valStr}').");
            }
            else
            {
                ActualizarEstadoBarra($"❌ Error al sincronizar cambio en BD MariaDB (ID {idVal}): {error}");
                MessageBox.Show($"Error al guardar cambios en la base de datos MariaDB (ID: {idVal}):\n{error}", 
                    "Error de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            ActualizarEstadoBarra($"❌ Error inesperado al sincronizar cambio en MariaDB: {ex.Message}");
            MessageBox.Show($"Error inesperado en MariaDB:\n{ex.Message}", "Error de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            System.Threading.Interlocked.Decrement(ref _activeUpdates);
        }
    }

    private void DgvTodos_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        ActualizarEstadoBarra($"⚠️ Error de formato: Valor inválido para la columna '{dgvTodos.Columns[e.ColumnIndex].HeaderText}'.");
    }
}

