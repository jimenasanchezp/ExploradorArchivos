using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using ExploradorArchivos.UI;

namespace ExploradorArchivos;

public class MediaPlayerForm : Form
{
    private WebView2 _webView;
    private string _filePath;

    public MediaPlayerForm(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        InitializePlayer();
    }

    private void InitializeComponent()
    {
        this.Text = "Reproductor Multimedia 🎵";
        this.Size = new System.Drawing.Size(1000, 750);
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = ThemeRenderer.MainBg;

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeRenderer.MainBg
        };

        this.Controls.Add(_webView);
        
        this.FormClosing += (s, e) => {
            _webView.Dispose();
        };
    }

    private async void InitializePlayer()
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();
            
            string extension = Path.GetExtension(_filePath).ToLower();
            bool isVideo = new[] { ".mp4", ".mov", ".webm", ".avi", ".mkv" }.Contains(extension);
            
            // Convertimos la ruta local a una URL amigable para WebView2
            string fileUrl = new Uri(_filePath).AbsoluteUri;

            string html = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{
                        margin: 0;
                        padding: 0;
                        background-color: #FFF5F8;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        overflow: hidden;
                    }}
                    .container {{
                        width: 90%;
                        max-width: 900px;
                        text-align: center;
                        background: white;
                        padding: 30px;
                        border-radius: 30px;
                        box-shadow: 0 15px 35px rgba(244, 143, 177, 0.2);
                    }}
                    video, audio {{
                        width: 100%;
                        border-radius: 15px;
                        outline: none;
                    }}
                    h2 {{
                        color: #D81B60;
                        margin-bottom: 25px;
                        font-size: 1.2rem;
                        white-space: nowrap;
                        overflow: hidden;
                        text-overflow: ellipsis;
                    }}
                    .icon {{
                        font-size: 80px;
                        margin-bottom: 20px;
                        display: block;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>{Path.GetFileName(_filePath)}</h2>
                    {(isVideo ? 
                        $"<video controls autoplay src='{fileUrl}'></video>" : 
                        $@"<span class='icon'>🎵</span>
                           <audio controls autoplay src='{fileUrl}'></audio>"
                    )}
                </div>
            </body>
            </html>";

            _webView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al inicializar el reproductor: {ex.Message}");
        }
    }
}
