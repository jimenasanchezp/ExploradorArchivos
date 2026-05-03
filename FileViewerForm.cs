using System.Text.Json;
using System.Xml.Linq;

namespace ExploradorArchivos;

public class FileViewerForm : Form
{
    private RichTextBox _richTextBox;

    public FileViewerForm(string filePath)
    {
        InitializeComponent();
        LoadFile(filePath);
    }

    private void InitializeComponent()
    {
        this.Text = "Visualizador de Texto 📄";
        this.Size = new Size(900, 700);
        this.StartPosition = FormStartPosition.CenterParent;

        _richTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 11f),
            ReadOnly = true,
          //  BackColor = Form1.MainBg,
            //ForeColor = Form1.MainText,
            //BorderStyle = BorderStyle.None,
            WordWrap = false
        };

        this.Controls.Add(_richTextBox);
        //Form1.ApplyTheme(this);
    }

    private async void LoadFile(string path)
    {
        try
        {
            string content = await File.ReadAllTextAsync(path);
            string extension = Path.GetExtension(path).ToLower();

            if (extension == ".json")
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    content = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    this.Text += " [JSON]";
                }
                catch { /* fallback to plain text */ }
            }
            else if (extension == ".xml")
            {
                try
                {
                    var doc = XDocument.Parse(content);
                    content = doc.ToString();
                    this.Text += " [XML]";
                }
                catch { /* fallback to plain text */ }
            }

            else if (extension == ".csv")
            {
                try
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var maxLen = lines.Select(l => l.Split(',').Length).Max();
                        var colsWidth = new int[maxLen];
                        var grid = lines.Select(l => l.Split(',')).ToList();
                        
                        foreach (var row in grid)
                            for (int i = 0; i < row.Length; i++)
                                colsWidth[i] = Math.Max(colsWidth[i], row[i].Trim().Length);
                        
                        var sb = new System.Text.StringBuilder();
                        foreach (var row in grid)
                        {
                            for (int i = 0; i < row.Length; i++)
                                sb.Append(row[i].Trim().PadRight(colsWidth[i] + 4));
                            sb.AppendLine();
                        }
                        content = sb.ToString();
                        this.Text += " [CSV Format]";
                    }
                }
                catch { /* fallback to plain text */ }
            }

            _richTextBox.Text = content;
        }
        catch (Exception ex)
        {
            _richTextBox.Text = $"Error al leer el archivo: {ex.Message}";
            _richTextBox.ForeColor = Color.Salmon;
        }
    }
}
