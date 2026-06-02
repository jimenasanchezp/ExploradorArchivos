using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExploradorArchivos.UI;

public class InputDialog : Form
{
    private readonly TextBox txtInput;
    private readonly Button btnOk;
    private readonly Button btnCancel;
    private readonly Label lblPrompt;

    public string InputText => txtInput.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        this.Text = title;
        this.Size = new Size(400, 170);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;

        lblPrompt = new Label
        {
            Text = prompt,
            Location = new Point(20, 20),
            Size = new Size(350, 20),
            ForeColor = ThemeRenderer.MainText
        };

        txtInput = new TextBox
        {
            Text = defaultValue,
            Location = new Point(20, 45),
            Size = new Size(345, 25),
            BackColor = Color.White,
            ForeColor = ThemeRenderer.MainText,
            BorderStyle = BorderStyle.FixedSingle
        };

        Button CrearBoton(string texto, Point ubicacion, DialogResult resultado)
        {
            var btn = new Button
            {
                Text = texto,
                Location = ubicacion,
                Size = new Size(95, 32),
                DialogResult = resultado,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, true);
            return btn;
        }

        btnOk = CrearBoton("Aceptar", new Point(165, 85), DialogResult.OK);
        btnCancel = CrearBoton("Cancelar", new Point(270, 85), DialogResult.Cancel);

        this.Controls.AddRange([lblPrompt, txtInput, btnOk, btnCancel]);
        
        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;

        // Apply classic standard theme styling
        ThemeRenderer.ApplyTheme(this);
        this.BackColor = ThemeRenderer.MainBg;
        
        // Force the input to focus when shown
        this.Load += (s, e) => {
            txtInput.Focus();
            txtInput.SelectAll();
        };
    }

    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        using var dlg = new InputDialog(title, prompt, defaultValue);
        return dlg.ShowDialog() == DialogResult.OK ? dlg.InputText : null;
    }
}
