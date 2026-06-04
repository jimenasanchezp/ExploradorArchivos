using ExploradorArchivos.AppDataFusion.Database;
using ExploradorArchivos.UI;

namespace ExploradorArchivos.AppDataFusion;

// --------------------------------------------------------------
//  DIÃLOGO â€“ ConexiÃ³n BD con detecciÃ³n automÃ¡tica de tablas
/// <summary>
/// Formulario dinámico que permite conectarse o migrar datos desde/hacia un servidor de base de datos
/// (PostgreSQL o MariaDB). Detecta y lista automáticamente las bases de datos y tablas disponibles.
/// </summary>
public class FormConexionBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla { get; private set; } = "";
    public bool UsarPrimaryKey { get; private set; } = true;

    private readonly TextBox txtHost, txtPuerto, txtUsuario, txtContrasena;
    private readonly ComboBox cmbBD;
    private readonly ComboBox cmbTabla;
    private readonly Button btnCargarBDs;
    private readonly Button btnDetectarTablas;
    private readonly Label lblEstadoTablas;
    private readonly CheckBox chkPrimaryKey;
    private readonly bool _esPg;
    private readonly string _pd, _ud;

    public FormConexionBD(string motor, bool esEscritura = false)
    {
        _esPg = motor == "PostgreSQL";
        _pd = _esPg ? "5432" : "3306";
        _ud = _esPg ? "postgres" : "root";

        Text = esEscritura ? $"Migrar datos a {motor}" : $"Conexión a {motor}";
        Size = new Size(490, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        int y = 18;
        const int lx = 15, cx = 140, cw = 320;

        Label Lbl(string t) => new()
        {
            Text = t,
            AutoSize = true
        };
        TextBox Txt(string d, bool p = false) => new()
        {
            Width = cw,
            Text = d,
            BackColor = Color.White,
            ForeColor = ThemeRenderer.MainText,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = p
        };

        var l1 = Lbl("Host:"); l1.Location = new Point(lx, y + 3);
        txtHost = Txt("localhost"); txtHost.Location = new Point(cx, y); y += 34;

        var l2 = Lbl("Puerto:"); l2.Location = new Point(lx, y + 3);
        txtPuerto = Txt(_pd); txtPuerto.Location = new Point(cx, y); y += 34;

        var l3 = Lbl("Base de datos:"); l3.Location = new Point(lx, y + 3);
        cmbBD = new ComboBox
        {
            Location = new Point(cx, y + 1),
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.White,
            ForeColor = ThemeRenderer.MainText,
            FlatStyle = FlatStyle.Flat
        };
        btnCargarBDs = new Button
        {
            Text = "Cargar BDs",
            Location = new Point(cx + 218, y),
            Width = 107,
            Height = 28,
            FlatStyle = FlatStyle.Flat
        };
        btnCargarBDs.FlatAppearance.BorderSize = 0;
        btnCargarBDs.Click += BtnCargarBDs_Click!;
        btnCargarBDs.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnCargarBDs.ClientRectangle, true);
        y += 34;

        var l4 = Lbl("Usuario:"); l4.Location = new Point(lx, y + 3);
        txtUsuario = Txt(_ud); txtUsuario.Location = new Point(cx, y); y += 34;

        var l5 = Lbl("Contraseña:"); l5.Location = new Point(lx, y + 3);
        txtContrasena = Txt("", true); txtContrasena.Location = new Point(cx, y); y += 34;

        var sep = new Label
        {
            Location = new Point(lx, y),
            Size = new Size(445, 1),
            BackColor = Color.Lavender
        };
        y += 10;

        var lTabla = Lbl("Tabla:"); lTabla.Location = new Point(lx, y + 5);
        cmbTabla = new ComboBox
        {
            Location = new Point(cx, y + 1),
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.White,
            ForeColor = ThemeRenderer.MainText,
            FlatStyle = FlatStyle.Flat
        };
        if (esEscritura) cmbTabla.Text = "datos_migrados";

        btnDetectarTablas = new Button
        {
            Text = "Detectar tablas",
            Location = new Point(cx + 218, y),
            Width = 107,
            Height = 28,
            FlatStyle = FlatStyle.Flat
        };
        btnDetectarTablas.FlatAppearance.BorderSize = 0;
        btnDetectarTablas.Click += BtnDetectarTablas_Click!;
        btnDetectarTablas.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnDetectarTablas.ClientRectangle, true);
        y += 38;

        lblEstadoTablas = new Label
        {
            Text = esEscritura
                ? "Escribe el nombre de la nueva tabla o pulsa 'Detectar tablas'."
                : "Ingresa los datos y pulsa 'Cargar BDs' o 'Detectar tablas'.",
            Location = new Point(cx, y),
            Size = new Size(cw, 18),
            ForeColor = ThemeRenderer.SecondaryText
        };
        y += 24;

        chkPrimaryKey = new CheckBox
        {
            Text = "Establecer la primera columna como Primary Key",
            Location = new Point(cx, y),
            AutoSize = true,
            Checked = true,
            Visible = esEscritura,
            ForeColor = ThemeRenderer.MainText
        };
        y += 30;

        var ok = new Button
        {
            Text = esEscritura ? "Migrar" : "Conectar",
            Location = new Point(270, y),
            Width = 100,
            Height = 28,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, ok.ClientRectangle, true);

        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(378, y),
            Width = 87,
            Height = 28,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat
        };
        can.FlatAppearance.BorderSize = 0;
        can.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, can.ClientRectangle, true);

        ok.Click += (_, _) =>
        {
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
            string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
            string db = cmbBD.Text.Trim();
            CadenaConexion = _esPg
                ? DatabaseWriter.BuildPostgreSqlConnectionString(h, p, db, u, txtContrasena.Text)
                : DatabaseWriter.BuildMariaDbConnectionString(h, p, db, u, txtContrasena.Text);
            NombreTabla = cmbTabla.Text.Trim();
            UsarPrimaryKey = chkPrimaryKey.Checked;
            if (string.IsNullOrWhiteSpace(db))
            {
                MessageBox.Show("Debes especificar la base de datos.",
                    "Base de datos requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrWhiteSpace(NombreTabla))
            {
                MessageBox.Show("Debes especificar el nombre de la tabla.",
                    "Tabla requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        ClientSize = new Size(475, y + 50);
        Controls.AddRange(new Control[]
        {
            l1, txtHost, l2, txtPuerto, l3, cmbBD, btnCargarBDs,
            l4, txtUsuario, l5, txtContrasena,
            sep, lTabla, cmbTabla, btnDetectarTablas, lblEstadoTablas,
            chkPrimaryKey, ok, can
        });
        AcceptButton = ok;
        CancelButton = can;
        ThemeRenderer.ApplyTheme(this);
    }

    private async void BtnCargarBDs_Click(object sender, EventArgs e)
    {
        string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
        string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
        string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
        string pass = txtContrasena.Text;

        btnCargarBDs.Enabled = false;
        lblEstadoTablas.Text = "Conectando al servidor...";
        lblEstadoTablas.ForeColor = Color.FromArgb(180, 140, 30);

        try
        {
            List<string> dbs = await Task.Run(() =>
                _esPg
                    ? DatabaseWriter.ObtenerBasesDatosPostgreSQL(h, p, u, pass)
                    : DatabaseWriter.ObtenerBasesDatosMariaDB(h, p, u, pass));

            string dbActual = cmbBD.Text;
            cmbBD.Items.Clear();
            foreach (var db in dbs) cmbBD.Items.Add(db);

            if (dbs.Count > 0)
            {
                int idx = cmbBD.FindStringExact(dbActual);
                cmbBD.SelectedIndex = idx >= 0 ? idx : 0;
                lblEstadoTablas.Text = $"{dbs.Count} base(s) de datos encontrada(s).";
                lblEstadoTablas.ForeColor = Color.FromArgb(52, 180, 120);
            }
            else
            {
                lblEstadoTablas.Text = "Sin bases de datos en el servidor.";
                lblEstadoTablas.ForeColor = Color.FromArgb(180, 140, 50);
            }
        }
        catch (Exception ex)
        {
            lblEstadoTablas.Text = $"Error: {ex.Message}";
            lblEstadoTablas.ForeColor = Color.FromArgb(200, 80, 80);
        }
        finally
        {
            btnCargarBDs.Enabled = true;
        }
    }

    private async void BtnDetectarTablas_Click(object sender, EventArgs e)
    {
        string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
        string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
        string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
        string db = cmbBD.Text.Trim();

        if (string.IsNullOrWhiteSpace(db))
        {
            MessageBox.Show("Selecciona o escribe el nombre de la base de datos primero.",
                "Base de datos requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string cadena = _esPg
            ? DatabaseWriter.BuildPostgreSqlConnectionString(h, p, db, u, txtContrasena.Text)
            : DatabaseWriter.BuildMariaDbConnectionString(h, p, db, u, txtContrasena.Text);

        btnDetectarTablas.Enabled = false;
        lblEstadoTablas.Text = "Conectando...";
        lblEstadoTablas.ForeColor = Color.FromArgb(180, 140, 30);

        try
        {
            List<string> tablas = await Task.Run(() =>
                _esPg
                    ? DatabaseWriter.ObtenerTablasPostgreSQL(cadena)
                    : DatabaseWriter.ObtenerTablasMariaDB(cadena));

            string tablaActual = cmbTabla.Text;
            cmbTabla.Items.Clear();
            foreach (var t in tablas) cmbTabla.Items.Add(t);

            if (tablas.Count > 0)
            {
                int idx = cmbTabla.FindStringExact(tablaActual);
                cmbTabla.SelectedIndex = idx >= 0 ? idx : 0;
                lblEstadoTablas.Text = $"{tablas.Count} tabla(s) encontrada(s).";
                lblEstadoTablas.ForeColor = Color.FromArgb(52, 180, 120);
            }
            else
            {
                lblEstadoTablas.Text = "Sin tablas. Escribe el nombre manualmente.";
                lblEstadoTablas.ForeColor = Color.FromArgb(180, 140, 50);
            }
        }
        catch (Exception ex)
        {
            lblEstadoTablas.Text = $"Error: {ex.Message}";
            lblEstadoTablas.ForeColor = Color.FromArgb(200, 80, 80);
        }
        finally
        {
            btnDetectarTablas.Enabled = true;
        }
    }
}



// --------------------------------------------------------------
//  DIÃLOGO â€“ SelecciÃ³n de columnas
/// <summary>
/// Formulario que asiste al usuario en la vinculación (mapeo) semántica de columnas 
/// procedentes de un esquema de base de datos desconocido, hacia el modelo interno normalizado (<c>DataItem</c>).
/// </summary>
public class FormSeleccionColumnas : Form
{
    public string ColId { get; private set; } = "";
    public string ColCategoria { get; private set; } = "";
    public string ColValor { get; private set; } = "";
    public string ColNombre { get; private set; } = "";
    public string ColFecha { get; private set; } = "";

    public FormSeleccionColumnas(List<string> columnas, Dictionary<string, string> mapeoActual)
    {
        Text = "Mapeo de columnas";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        string sI = mapeoActual.FirstOrDefault(kv => kv.Value == "id").Key ?? "";
        string sC = mapeoActual.FirstOrDefault(kv => kv.Value == "categoria").Key ?? "";
        string sV = mapeoActual.FirstOrDefault(kv => kv.Value == "valor").Key ?? "";
        string sN = mapeoActual.FirstOrDefault(kv => kv.Value == "nombre").Key ?? "";
        string sF = mapeoActual.FirstOrDefault(kv => kv.Value == "fecha").Key ?? "";

        var todas = new List<string> { "(ninguna)" }; todas.AddRange(columnas);
        var arr = todas.ToArray<object>();
        int y = 15;
        const int lx = 15, cx = 215, lw = 195, cw = 230;

        var intro = new Label
        {
            Text = "Elige qué columna de tu tabla representa cada concepto:",
            Location = new Point(lx, y),
            Size = new Size(440, 20),
            ForeColor = ThemeRenderer.SecondaryText
        };
        y += 30;

        Label Lbl(string t, string s) => new()
        {
            Text = s.Length > 0 ? $"{t}  (auto: {s})" : t,
            Location = new Point(lx, y),
            Size = new Size(lw, 20),
            ForeColor = ThemeRenderer.SecondaryText
        };

        ComboBox Cmb(string s)
        {
            var c = new ComboBox
            {
                Location = new Point(cx, y - 2),
                Width = cw,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = ThemeRenderer.MainText,
                FlatStyle = FlatStyle.Flat
            };
            c.Items.AddRange(arr);
            int idx = s.Length > 0 ? todas.IndexOf(s) : 0;
            c.SelectedIndex = Math.Max(0, idx);
            return c;
        }

        var lI = Lbl("ID / Identificador principal:", sI); var cI = Cmb(sI); y += 30;
        var lC = Lbl("Categoría (eje X / agrupación):", sC); var cC = Cmb(sC); y += 30;
        var lV = Lbl("Valor numérico (eje Y / suma):", sV); var cV = Cmb(sV); y += 30;
        var lN = Lbl("Nombre / etiqueta:", sN); var cN = Cmb(sN); y += 30;
        var lF = Lbl("Fecha:", sF); var cF = Cmb(sF); y += 38;

        var nota = new Label
        {
            Text = "Deja en (ninguna) si la columna no aplica.",
            Location = new Point(lx, y),
            Size = new Size(440, 18),
            ForeColor = ThemeRenderer.SecondaryText
        };
        y += 28;

        var btnOk = new Button
        {
            Text = "Confirmar",
            Location = new Point(248, y),
            Width = 105,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnOk.ClientRectangle, true);

        var btnCan = new Button
        {
            Text = "Cancelar",
            Location = new Point(362, y),
            Width = 85,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat
        };
        btnCan.FlatAppearance.BorderSize = 0;
        btnCan.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnCan.ClientRectangle, true);

        btnOk.Click += (_, _) =>
        {
            ColId = cI.Text == "(ninguna)" ? "" : cI.Text;
            ColCategoria = cC.Text == "(ninguna)" ? "" : cC.Text;
            ColValor = cV.Text == "(ninguna)" ? "" : cV.Text;
            ColNombre = cN.Text == "(ninguna)" ? "" : cN.Text;
            ColFecha = cF.Text == "(ninguna)" ? "" : cF.Text;

            var usados = new[] { ColId, ColCategoria, ColValor, ColNombre, ColFecha }
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (usados.Count != usados.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                MessageBox.Show(
                    "No uses la misma columna para más de un campo.",
                    "Mapeo inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        ClientSize = new Size(460, y + 50);
        Controls.AddRange(new Control[]
            { intro, lI, cI, lC, cC, lV, cV, lN, cN, lF, cF, nota, btnOk, btnCan });
        AcceptButton = btnOk;
        CancelButton = btnCan;
        ThemeRenderer.ApplyTheme(this);
    }
}

