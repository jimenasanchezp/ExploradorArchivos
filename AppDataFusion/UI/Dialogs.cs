using ExploradorArchivos.AppDataFusion.Database;
using ExploradorArchivos.UI;

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

        ThemeRenderer.ApplyTheme(this);
        Text = $"Conexión a {motor}";
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

