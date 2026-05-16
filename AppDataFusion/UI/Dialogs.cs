using ExploradorArchivos.AppDataFusion.Database;

namespace ExploradorArchivos.AppDataFusion;

// --------------------------------------------------------------
//  DIÁLOGO – Conexión BD con detección automática de tablas
// --------------------------------------------------------------
public class FormConexionBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla { get; private set; } = "";

    private readonly TextBox txtHost, txtPuerto, txtBD, txtUsuario, txtContrasena;
    private readonly ComboBox cmbTabla;
    private readonly Button btnDetectarTablas;
    private readonly Label lblEstadoTablas;
    private readonly bool _esPg;
    private readonly string _pd, _ud;

    public FormConexionBD(string motor)
    {
        _esPg = motor == "PostgreSQL";
        _pd = _esPg ? "5432" : "3306";
        _ud = _esPg ? "postgres" : "root";

        Text = $"Conexión a {motor}";
        Size = new Size(490, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(18, 18, 26);
        ForeColor = Color.FromArgb(220, 220, 232);
        Font = new Font("Segoe UI", 9f);

        int y = 18;
        const int lx = 15, cx = 140, cw = 320;

        Label Lbl(string t) => new()
        {
            Text = t,
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 130)
        };
        TextBox Txt(string d, bool p = false) => new()
        {
            Width = cw,
            Text = d,
            BackColor = Color.FromArgb(26, 26, 36),
            ForeColor = Color.FromArgb(220, 220, 232),
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = p,
            Font = new Font("Segoe UI", 9f)
        };

        var l1 = Lbl("Host:"); l1.Location = new Point(lx, y + 3);
        txtHost = Txt("localhost"); txtHost.Location = new Point(cx, y); y += 34;

        var l2 = Lbl("Puerto:"); l2.Location = new Point(lx, y + 3);
        txtPuerto = Txt(_pd); txtPuerto.Location = new Point(cx, y); y += 34;

        var l3 = Lbl("Base de datos:"); l3.Location = new Point(lx, y + 3);
        txtBD = Txt(""); txtBD.Location = new Point(cx, y); y += 34;

        var l4 = Lbl("Usuario:"); l4.Location = new Point(lx, y + 3);
        txtUsuario = Txt(_ud); txtUsuario.Location = new Point(cx, y); y += 34;

        var l5 = Lbl("Contraseña:"); l5.Location = new Point(lx, y + 3);
        txtContrasena = Txt("", true); txtContrasena.Location = new Point(cx, y); y += 34;

        var sep = new Label
        {
            Location = new Point(lx, y),
            Size = new Size(445, 1),
            BackColor = Color.FromArgb(36, 36, 52)
        };
        y += 10;

        var lTabla = Lbl("Tabla:"); lTabla.Location = new Point(lx, y + 5);
        cmbTabla = new ComboBox
        {
            Location = new Point(cx, y + 1),
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.FromArgb(26, 26, 36),
            ForeColor = Color.FromArgb(220, 220, 232),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        btnDetectarTablas = new Button
        {
            Text = "Detectar tablas",
            Location = new Point(cx + 218, y),
            Width = 107,
            Height = 28,
            BackColor = Color.FromArgb(13, 50, 35),
            ForeColor = Color.FromArgb(52, 211, 153),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f)
        };
        btnDetectarTablas.FlatAppearance.BorderSize = 0;
        btnDetectarTablas.Click += BtnDetectarTablas_Click!;
        y += 38;

        lblEstadoTablas = new Label
        {
            Text = "Ingresa los datos y pulsa 'Detectar tablas'.",
            Location = new Point(cx, y),
            Size = new Size(cw, 18),
            ForeColor = Color.FromArgb(80, 130, 100),
            Font = new Font("Segoe UI", 7.8f)
        };
        y += 30;

        var ok = new Button
        {
            Text = "Conectar",
            Location = new Point(270, y),
            Width = 100,
            Height = 28,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(13, 61, 40),
            ForeColor = Color.FromArgb(52, 211, 153),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        ok.FlatAppearance.BorderSize = 0;

        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(378, y),
            Width = 87,
            Height = 28,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(40, 18, 18),
            ForeColor = Color.FromArgb(200, 100, 100),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        can.FlatAppearance.BorderSize = 0;

        ok.Click += (_, _) =>
        {
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
            string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
            CadenaConexion = _esPg
                ? $"Host={h};Port={p};Database={txtBD.Text.Trim()};Username={u};Password={txtContrasena.Text};"
                : $"Server={h};Port={p};Database={txtBD.Text.Trim()};User={u};Password={txtContrasena.Text};";
            NombreTabla = cmbTabla.Text.Trim();
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
            l1, txtHost, l2, txtPuerto, l3, txtBD,
            l4, txtUsuario, l5, txtContrasena,
            sep, lTabla, cmbTabla, btnDetectarTablas, lblEstadoTablas,
            ok, can
        });
        AcceptButton = ok;
        CancelButton = can;
    }

    private async void BtnDetectarTablas_Click(object sender, EventArgs e)
    {
        string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
        string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
        string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
        string db = txtBD.Text.Trim();

        if (string.IsNullOrWhiteSpace(db))
        {
            MessageBox.Show("Escribe el nombre de la base de datos primero.",
                "Base de datos requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string cadena = _esPg
            ? $"Host={h};Port={p};Database={db};Username={u};Password={txtContrasena.Text};"
            : $"Server={h};Port={p};Database={db};User={u};Password={txtContrasena.Text};";

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
//  DIÁLOGO – Exportar datos a BD
// --------------------------------------------------------------
public class FormExportarBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla { get; private set; } = "";
    public string Motor { get; private set; } = "PostgreSQL";

    private readonly RadioButton rbPostgres, rbMariaDB;
    private readonly TextBox txtHost, txtPuerto, txtBD, txtUsuario, txtContrasena;
    private readonly ComboBox cmbTabla;
    private readonly Button btnDetectarTablas;
    private readonly Label lblEstadoTablas;

    public FormExportarBD(int totalRegistros)
    {
        Text = "Exportar a Base de Datos";
        Size = new Size(510, 490);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(18, 18, 26);
        ForeColor = Color.FromArgb(220, 220, 232);
        Font = new Font("Segoe UI", 9f);

        int y = 12;
        const int lx = 15, cx = 140, cw = 340;

        var lblInfo = new Label
        {
            Text = $"Se enviarán {totalRegistros:N0} registros. La tabla se creará si no existe.",
            Location = new Point(lx, y),
            Size = new Size(460, 32),
            ForeColor = Color.FromArgb(52, 211, 153),
            Font = new Font("Segoe UI", 8.5f)
        };
        y += 44;

        var sep0 = new Label { Location = new Point(lx, y), Size = new Size(455, 1), BackColor = Color.FromArgb(36, 36, 52) };
        y += 10;

        var lblMotor = new Label { Text = "Motor:", Location = new Point(lx, y + 3), AutoSize = true, ForeColor = Color.FromArgb(100, 100, 130) };
        rbPostgres = new RadioButton
        {
            Text = "PostgreSQL",
            Location = new Point(cx, y),
            AutoSize = true,
            Checked = true,
            ForeColor = Color.FromArgb(125, 211, 252),
            BackColor = Color.Transparent
        };
        rbMariaDB = new RadioButton
        {
            Text = "MariaDB",
            Location = new Point(cx + 140, y),
            AutoSize = true,
            ForeColor = Color.FromArgb(251, 191, 36),
            BackColor = Color.Transparent
        };
        rbPostgres.CheckedChanged += Motor_Changed!;
        rbMariaDB.CheckedChanged += Motor_Changed!;
        y += 30;

        var sep1 = new Label { Location = new Point(lx, y), Size = new Size(455, 1), BackColor = Color.FromArgb(36, 36, 52) };
        y += 10;

        Label Lbl(string t) => new() { Text = t, AutoSize = true, ForeColor = Color.FromArgb(100, 100, 130) };
        TextBox Txt(string d, bool p = false) => new()
        {
            Width = cw,
            Text = d,
            BackColor = Color.FromArgb(26, 26, 36),
            ForeColor = Color.FromArgb(220, 220, 232),
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = p,
            Font = new Font("Segoe UI", 9f)
        };

        var l1 = Lbl("Host:"); l1.Location = new Point(lx, y + 3);
        txtHost = Txt("localhost"); txtHost.Location = new Point(cx, y); y += 32;

        var l2 = Lbl("Puerto:"); l2.Location = new Point(lx, y + 3);
        txtPuerto = Txt("5432"); txtPuerto.Location = new Point(cx, y); y += 32;

        var l3 = Lbl("Base de datos:"); l3.Location = new Point(lx, y + 3);
        txtBD = Txt(""); txtBD.Location = new Point(cx, y); y += 32;

        var l4 = Lbl("Usuario:"); l4.Location = new Point(lx, y + 3);
        txtUsuario = Txt("postgres"); txtUsuario.Location = new Point(cx, y); y += 32;

        var l5 = Lbl("Contraseña:"); l5.Location = new Point(lx, y + 3);
        txtContrasena = Txt("", true); txtContrasena.Location = new Point(cx, y); y += 32;

        var sep2 = new Label { Location = new Point(lx, y), Size = new Size(455, 1), BackColor = Color.FromArgb(36, 36, 52) };
        y += 10;

        var lTabla = Lbl("Tabla destino:"); lTabla.Location = new Point(lx, y + 5);
        cmbTabla = new ComboBox
        {
            Location = new Point(cx, y + 1),
            Width = 215,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.FromArgb(26, 26, 36),
            ForeColor = Color.FromArgb(220, 220, 232),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbTabla.Text = "datos_exportados";

        btnDetectarTablas = new Button
        {
            Text = "Ver tablas",
            Location = new Point(cx + 223, y),
            Width = 117,
            Height = 28,
            BackColor = Color.FromArgb(8, 34, 55),
            ForeColor = Color.FromArgb(125, 211, 252),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f)
        };
        btnDetectarTablas.FlatAppearance.BorderSize = 0;
        btnDetectarTablas.Click += BtnDetectarTablas_Click!;
        y += 36;

        lblEstadoTablas = new Label
        {
            Text = "Ingresa la conexión y pulsa 'Ver tablas', o escribe un nombre nuevo.",
            Location = new Point(cx, y),
            Size = new Size(cw, 30),
            ForeColor = Color.FromArgb(80, 130, 100),
            Font = new Font("Segoe UI", 7.8f)
        };
        y += 36;

        var ok = new Button
        {
            Text = $"Enviar {totalRegistros:N0} registros",
            Location = new Point(cx, y),
            Width = 200,
            Height = 30,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(13, 61, 40),
            ForeColor = Color.FromArgb(52, 211, 153),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        ok.FlatAppearance.BorderSize = 0;

        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(cx + 210, y),
            Width = 90,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(40, 18, 18),
            ForeColor = Color.FromArgb(200, 100, 100),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        can.FlatAppearance.BorderSize = 0;

        ok.Click += (_, _) =>
        {
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? (rbPostgres.Checked ? "5432" : "3306") : txtPuerto.Text.Trim();
            string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? (rbPostgres.Checked ? "postgres" : "root") : txtUsuario.Text.Trim();
            string db = txtBD.Text.Trim();

            if (string.IsNullOrWhiteSpace(db))
            {
                MessageBox.Show("Escribe el nombre de la base de datos.",
                    "Datos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            NombreTabla = cmbTabla.Text.Trim();
            if (string.IsNullOrWhiteSpace(NombreTabla))
            {
                MessageBox.Show("Escribe o selecciona el nombre de la tabla destino.",
                    "Tabla requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            Motor = rbPostgres.Checked ? "PostgreSQL" : "MariaDB";
            CadenaConexion = rbPostgres.Checked
                ? $"Host={h};Port={p};Database={db};Username={u};Password={txtContrasena.Text};"
                : $"Server={h};Port={p};Database={db};User={u};Password={txtContrasena.Text};";
        };

        ClientSize = new Size(478, y + 55);
        Controls.AddRange(new Control[]
        {
            lblInfo, sep0, lblMotor, rbPostgres, rbMariaDB, sep1,
            l1, txtHost, l2, txtPuerto, l3, txtBD,
            l4, txtUsuario, l5, txtContrasena,
            sep2, lTabla, cmbTabla, btnDetectarTablas, lblEstadoTablas,
            ok, can
        });
        AcceptButton = ok;
        CancelButton = can;
    }

    private void Motor_Changed(object sender, EventArgs e)
    {
        txtPuerto.Text = rbPostgres.Checked ? "5432" : "3306";
        txtUsuario.Text = rbPostgres.Checked ? "postgres" : "root";
        cmbTabla.Items.Clear();
        lblEstadoTablas.Text = "Motor cambiado. Vuelve a detectar tablas si lo necesitas.";
        lblEstadoTablas.ForeColor = Color.FromArgb(140, 140, 160);
    }

    private async void BtnDetectarTablas_Click(object sender, EventArgs e)
    {
        string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
        string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? (rbPostgres.Checked ? "5432" : "3306") : txtPuerto.Text.Trim();
        string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? (rbPostgres.Checked ? "postgres" : "root") : txtUsuario.Text.Trim();
        string db = txtBD.Text.Trim();

        if (string.IsNullOrWhiteSpace(db))
        {
            MessageBox.Show("Escribe el nombre de la base de datos primero.",
                "Base de datos requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string cadena = rbPostgres.Checked
            ? $"Host={h};Port={p};Database={db};Username={u};Password={txtContrasena.Text};"
            : $"Server={h};Port={p};Database={db};User={u};Password={txtContrasena.Text};";

        btnDetectarTablas.Enabled = false;
        lblEstadoTablas.Text = "Detectando tablas...";
        lblEstadoTablas.ForeColor = Color.FromArgb(180, 140, 30);

        try
        {
            string tablaActual = cmbTabla.Text;
            List<string> tablas = await Task.Run(() =>
                rbPostgres.Checked
                    ? DatabaseWriter.ObtenerTablasPostgreSQL(cadena)
                    : DatabaseWriter.ObtenerTablasMariaDB(cadena));

            cmbTabla.Items.Clear();
            foreach (var t in tablas) cmbTabla.Items.Add(t);

            if (tablas.Count > 0)
            {
                int idx = cmbTabla.FindStringExact(tablaActual);
                if (idx >= 0) cmbTabla.SelectedIndex = idx;
                else if (!string.IsNullOrWhiteSpace(tablaActual)) cmbTabla.Text = tablaActual;
                lblEstadoTablas.Text = $"{tablas.Count} tabla(s). Elige una o escribe un nombre nuevo.";
                lblEstadoTablas.ForeColor = Color.FromArgb(52, 180, 120);
            }
            else
            {
                lblEstadoTablas.Text = "Sin tablas existentes. Se creará la tabla que escribas.";
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
//  DIÁLOGO – Selección de columnas
// --------------------------------------------------------------
public class FormSeleccionColumnas : Form
{
    public string ColCategoria { get; private set; } = "";
    public string ColValor { get; private set; } = "";
    public string ColNombre { get; private set; } = "";
    public string ColFecha { get; private set; } = "";

    public FormSeleccionColumnas(List<string> columnas, Dictionary<string, string> mapeoActual)
    {
        Text = "Mapeo de columnas";
        Size = new Size(480, 295);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(18, 18, 26);
        ForeColor = Color.FromArgb(220, 220, 232);
        Font = new Font("Segoe UI", 9f);

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
            ForeColor = Color.FromArgb(100, 100, 130),
            Font = new Font("Segoe UI", 8.5f)
        };
        y += 30;

        Label Lbl(string t, string s) => new()
        {
            Text = s.Length > 0 ? $"{t}  (auto: {s})" : t,
            Location = new Point(lx, y),
            Size = new Size(lw, 20),
            ForeColor = Color.FromArgb(52, 211, 153),
            Font = new Font("Segoe UI", 8.5f)
        };

        ComboBox Cmb(string s)
        {
            var c = new ComboBox
            {
                Location = new Point(cx, y - 2),
                Width = cw,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(26, 26, 36),
                ForeColor = Color.FromArgb(220, 220, 232),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
            c.Items.AddRange(arr);
            int idx = s.Length > 0 ? todas.IndexOf(s) : 0;
            c.SelectedIndex = Math.Max(0, idx);
            return c;
        }

        var lC = Lbl("Categoría (eje X / agrupación):", sC); var cC = Cmb(sC); y += 30;
        var lV = Lbl("Valor numérico (eje Y / suma):", sV); var cV = Cmb(sV); y += 30;
        var lN = Lbl("Nombre / etiqueta:", sN); var cN = Cmb(sN); y += 30;
        var lF = Lbl("Fecha:", sF); var cF = Cmb(sF); y += 38;

        var nota = new Label
        {
            Text = "Deja en (ninguna) si la columna no aplica.",
            Location = new Point(lx, y),
            Size = new Size(440, 18),
            ForeColor = Color.FromArgb(70, 70, 95),
            Font = new Font("Segoe UI", 8f)
        };
        y += 28;

        var btnOk = new Button
        {
            Text = "Confirmar",
            Location = new Point(248, y),
            Width = 105,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(13, 61, 40),
            ForeColor = Color.FromArgb(52, 211, 153),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        btnOk.FlatAppearance.BorderSize = 0;

        var btnCan = new Button
        {
            Text = "Cancelar",
            Location = new Point(362, y),
            Width = 85,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(40, 18, 18),
            ForeColor = Color.FromArgb(200, 100, 100),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        btnCan.FlatAppearance.BorderSize = 0;

        btnOk.Click += (_, _) =>
        {
            ColCategoria = cC.Text == "(ninguna)" ? "" : cC.Text;
            ColValor = cV.Text == "(ninguna)" ? "" : cV.Text;
            ColNombre = cN.Text == "(ninguna)" ? "" : cN.Text;
            ColFecha = cF.Text == "(ninguna)" ? "" : cF.Text;

            var usados = new[] { ColCategoria, ColValor, ColNombre, ColFecha }
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
            { intro, lC, cC, lV, cV, lN, cN, lF, cF, nota, btnOk, btnCan });
        AcceptButton = btnOk;
        CancelButton = btnCan;
    }
}

