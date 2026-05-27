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

        btnOk = new Button
        {
            Text = "Aceptar",
            Location = new Point(165, 85),
            Size = new Size(95, 32),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnOk.ClientRectangle, true);

        btnCancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(270, 85),
            Size = new Size(95, 32),
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, btnCancel.ClientRectangle, true);

        this.Controls.AddRange(new Control[] { lblPrompt, txtInput, btnOk, btnCancel });
        
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
