using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ExploradorArchivos.Models;
using ExploradorArchivos.Services;
using ExploradorArchivos.UI;
using ExploradorArchivos.Mp3;
using ExploradorArchivos.AppVideo;
using ExploradorArchivos.AppFoto;
using ExploradorArchivos.AppDataFusion;
using ExploradorArchivos.AppCapturaPantalla;

namespace ExploradorArchivos;

/// <summary>
/// Formulario principal del Explorador de Archivos (Estilo Clásico / Soft Pastel).
/// Contiene la inicialización del formulario raíz, declaración de variables globales y las opciones principales de manipulación de archivos.
/// </summary>
public partial class Form1 : Form
{
    // === Estado de navegación ===
    private readonly NavigationService _navigationService = new(); // valor de ruta actual y lógica de navegación entre directorios, favoritos, accesos directos, etc.
    private string _rutaActual { get => _navigationService.RutaActual; set => _navigationService.NavegarA(value, false); }
    private List<FileSystemItem> _itemsActuales = new List<FileSystemItem>(); // Lista de items actualmente mostrados (para filtros y búsqueda)
    private string _filtroActivo = "Todos"; // filtro de visualización activo (Todos, Imágenes, Audio, Video, Texto/Código, Otros)
    private static readonly List<string> _accesosDirectos = new List<string>();
    private static readonly List<string> _elementosFavoritos = new List<string>();
    
    // === Categorías de extensiones de archivos ===
    private static readonly string[] ImgExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
    private static readonly string[] MediaExtensions = { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma", ".aac" };
    private static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v" };
    private static readonly string[] TextExtensions = { ".cs", ".html", ".css", ".js", ".md", ".py", ".bat", ".cmd", ".dat" };
    private static readonly string[] DataExtensions = { ".csv", ".json", ".xml", ".txt" };

    private static readonly string AccesosDirectosFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExploradorArchivos",
        "accesos_directos.txt"
    );
    private static readonly string ElementosFavoritosFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExploradorArchivos",
        "favoritos.txt"
    );

    /// <summary>
    /// Carga los datos almacenados en disco (como la lista de Favoritos y los Accesos Directos)
    /// desde la carpeta de datos de aplicación (<c>AppData\Roaming\ExploradorArchivos</c>).
    /// </summary>
    private void CargarDatosPersistentes()
    {
        _accesosDirectos.Clear();
        _accesosDirectos.AddRange(PersistenceService.CargarRutasExistentes(AccesosDirectosFilePath));

        _elementosFavoritos.Clear();
        _elementosFavoritos.AddRange(PersistenceService.CargarRutasExistentes(ElementosFavoritosFilePath));
    }

    /// <summary>
    /// Guarda en disco los cambios realizados en las listas de Favoritos y Accesos Directos
    /// para que persistan entre sesiones de la aplicación.
    /// </summary>
    private void GuardarDatosPersistentes()
    {
        PersistenceService.GuardarRutas(AccesosDirectosFilePath, _accesosDirectos);
        PersistenceService.GuardarRutas(ElementosFavoritosFilePath, _elementosFavoritos);
    }

    // === Componentes de UI ===
    private ListViewSorter _sorter = default!; // Ordenador de ListView para ordenamiento por columnas
    private QuickLookForm? _quickLookForm;
    private FlowLayoutPanel _pnlFiltros = default!;
    private ImageList _imageListMiniaturas = default!;
    private Button _btnToggleVista = default!;
    private Button _btnAppData = default!;
    private FlowLayoutPanel _flpBreadcrumbs = default!;
    private MusicPlayerForm? _reproductorMusica;

    public Form1()
    {
        InitializeComponent();
        CargarDatosPersistentes();
        ConfigurarUI();
        ConectarEventos();
        ConfigurarContextoMenu();
        CargarDirectorio(_rutaActual);
    }

    /// <summary>
    /// Construye dinámicamente el menú contextual de clic derecho (Abrir, QuickLook, Cortar, Copiar, Pegar, Eliminar, Propiedades)
    /// utilizando un renderizador personalizado de colores, y enlaza todos los eventos de interación.
    /// </summary>
    private class ContextMenuComponents
    {
        public ToolStripMenuItem Abrir { get; } = new("📄  Abrir");
        public ToolStripMenuItem AbrirCon { get; } = new("📁  Abrir con...");
        public ToolStripMenuItem Copiar { get; } = new("📋  Copiar");
        public ToolStripMenuItem Renombrar { get; } = new("✏️  Cambiar nombre");
        public ToolStripMenuItem Eliminar { get; } = new("🗑️  Eliminar");
        public ToolStripMenuItem EnviarCorreoSmtp { get; } = new("✉️  Enviar por correo (SMTP/Gmail)");
        public ToolStripMenuItem EnviarCorreoMapi { get; } = new("💻  Enviar por cliente local (Outlook)");
        public ToolStripMenuItem Propiedades { get; } = new("🛠️  Propiedades");
        public ToolStripMenuItem AppVideo { get; } = new("🎬  App Video");
        public ToolStripMenuItem AppFoto { get; } = new("🖼️  App Foto");
        public ToolStripMenuItem Music { get; } = new("🎵  App Music");
        public ToolStripMenuItem Texto { get; } = new("📝  Visor de Texto");
        public ToolStripMenuItem Exportar { get; } = new("📤  Exportar a...");
        public ToolStripMenuItem ExportarDocx { get; } = new("📄  .docx");
        public ToolStripMenuItem ExportarPptx { get; } = new("🖥️  .pptx");
        public ToolStripMenuItem ExportarXlsx { get; } = new("📊  .xlsx");
        public ToolStripMenuItem ExportarPdf { get; } = new("📕  .pdf");
        public ToolStripMenuItem Predeterminada { get; } = new("💻  Sistema (App Predeterminada)");
        public ToolStripMenuItem Fijar { get; } = new("📌  Fijar a acceso directo");
        public ToolStripMenuItem Favorito { get; } = new("⭐  Agregar a Favoritos");
        public ToolStripMenuItem VaciarFavoritos { get; } = new("⭐  Vaciar Favoritos");
        public ToolStripMenuItem Pegar { get; } = new("📋  Pegar");
        public ToolStripMenuItem NuevaCarpeta { get; } = new("📁  Nueva carpeta");
        public ToolStripMenuItem Actualizar { get; } = new("🔄  Actualizar");
        public ToolStripSeparator Sep1 { get; } = new();
        public ToolStripSeparator Sep2 { get; } = new();
        public ToolStripSeparator Sep3 { get; } = new();
    }

    /// <summary>
    /// Construye dinámicamente el menú contextual de clic derecho (Abrir, QuickLook, Cortar, Copiar, Pegar, Eliminar, Propiedades)
    /// utilizando un renderizador personalizado de colores, y enlaza todos los eventos de interacción.
    /// </summary>
    private void ConfigurarContextoMenu()
    {
        ContextMenuStrip menu = new ContextMenuStrip { Renderer = new CustomMenuRenderer() };
        ContextMenuComponents components = new ContextMenuComponents();

        AsociarEventosMenuContextual(components);
        EnsamblarMenuContextual(menu, components);
        RegistrarConfiguracionMenu(menu, components);
    }

    private void AsociarEventosMenuContextual(ContextMenuComponents c)
    {
        c.Abrir.Click += (s, e) => ListViewPrincipal_DoubleClick(s, e); 
        c.AppVideo.Click += (s, e) => AbrirCon(new AppVideoForm(GetSelectedPath()));
        c.AppFoto.Click += (s, e) => AbrirCon(new AppFotoForm(GetSelectedPath()));
        c.Music.Click += (s, e) => AbrirReproductor(new List<string> { GetSelectedPath() }, GetSelectedPath());
        c.Texto.Click += (s, e) => AbrirCon(new FileViewerForm(GetSelectedPath()));
        c.ExportarDocx.Click += (s, e) => ExportarArchivo(GetSelectedPath(), ".docx");
        c.ExportarPptx.Click += (s, e) => ExportarArchivo(GetSelectedPath(), ".pptx");
        c.ExportarXlsx.Click += (s, e) => ExportarArchivo(GetSelectedPath(), ".xlsx");
        c.ExportarPdf.Click += (s, e) => ExportarArchivo(GetSelectedPath(), ".pdf");
        c.Predeterminada.Click += (s, e) => AbrirConSistema(GetSelectedPath());
        c.Pegar.Click += (s, e) => PegarArchivos();
        c.NuevaCarpeta.Click += BtnNuevaCarpeta_Click;
        c.Actualizar.Click += (s, e) => CargarDirectorio(_rutaActual, false);
        c.Copiar.Click += (s, e) => CopiarSeleccionados();
        c.Renombrar.Click += (s, e) => listViewPrincipal.SelectedItems[0].BeginEdit();
        c.Eliminar.Click += (s, e) => EliminarArchivo(GetSelectedPath());
        
        c.EnviarCorreoSmtp.Click += (s, e) => {
            string ruta = GetSelectedPath();
            if (!string.IsNullOrEmpty(ruta) && File.Exists(ruta))
            {
                using var frm = new SendMailForm(ruta);
                frm.ShowDialog(this);
            }
        };

        c.EnviarCorreoMapi.Click += (s, e) => {
            string ruta = GetSelectedPath();
            if (!string.IsNullOrEmpty(ruta) && File.Exists(ruta))
            {
                EmailService.EnviarCorreoConAdjunto(this.Handle, ruta);
            }
        };
        
        c.Propiedades.Click += (s, e) => MostrarPropiedades(GetSelectedPath());

        c.Fijar.Click += (s, e) => {
            string ruta = GetSelectedPath();
            if (string.IsNullOrEmpty(ruta)) return;

            if (_accesosDirectos.Contains(ruta))
            {
                _accesosDirectos.Remove(ruta);
                MessageBox.Show("Elemento desanclado de accesos directos.", "Accesos Directos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _accesosDirectos.Add(ruta);
                MessageBox.Show("Elemento anclado a accesos directos.", "Accesos Directos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            GuardarDatosPersistentes();
            PoblarTreeViewNormal();
        };

        c.Favorito.Click += (s, e) => {
            string ruta = GetSelectedPath();
            if (string.IsNullOrEmpty(ruta)) return;

            if (_elementosFavoritos.Contains(ruta))
            {
                _elementosFavoritos.Remove(ruta);
                MessageBox.Show("Elemento quitado de Favoritos.", "Favoritos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _elementosFavoritos.Add(ruta);
                MessageBox.Show("Elemento agregado a Favoritos.", "Favoritos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            GuardarDatosPersistentes();
            PoblarTreeViewNormal();
            if (_rutaActual == "Favoritos")
            {
                GenerarVistaFavoritos();
            }
        };

        c.VaciarFavoritos.Click += (s, e) => {
            var confirm = MessageBox.Show(
                "¿Estás seguro de que deseas quitar todos los elementos de la lista de Favoritos?\n\n(Esto NO eliminará ni alterará los archivos o carpetas reales en tu disco duro, solo limpiará tu lista de favoritos).",
                "Vaciar Favoritos",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (confirm == DialogResult.Yes)
            {
                _elementosFavoritos.Clear();
                GuardarDatosPersistentes();
                PoblarTreeViewNormal();
                if (_rutaActual == "Favoritos")
                {
                    GenerarVistaFavoritos();
                }
                MessageBox.Show("La lista de Favoritos ha sido vaciada.", "Favoritos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
    }

    private void EnsamblarMenuContextual(ContextMenuStrip menu, ContextMenuComponents c)
    {
        c.Exportar.DropDownItems.AddRange(new ToolStripItem[] {
            c.ExportarDocx, c.ExportarPptx, c.ExportarXlsx, c.ExportarPdf
        });

        c.AbrirCon.DropDownItems.AddRange(new ToolStripItem[] {
            c.AppVideo, c.AppFoto, c.Music, c.Texto, new ToolStripSeparator(), c.Predeterminada
        });

        menu.Items.AddRange(new ToolStripItem[] {
            c.Abrir, c.AbrirCon, c.Exportar, c.Sep1,
            c.Copiar, c.Pegar, c.Renombrar, c.Eliminar, c.Sep2,
            c.EnviarCorreoSmtp, c.EnviarCorreoMapi, c.Fijar, c.Favorito, c.VaciarFavoritos, c.Sep3,
            c.NuevaCarpeta, c.Actualizar,
            c.Propiedades
        });
    }

    private void RegistrarConfiguracionMenu(ContextMenuStrip menu, ContextMenuComponents c)
    {
        listViewPrincipal.ContextMenuStrip = menu;
        listViewPrincipal.LabelEdit = true;
        listViewPrincipal.AfterLabelEdit += (s, e) => {
            if (string.IsNullOrEmpty(e.Label)) { e.CancelEdit = true; return; }
            string? rutaOld = listViewPrincipal.SelectedItems[0].Tag?.ToString();
            if (rutaOld != null) RenombrarArchivo(rutaOld, e.Label);
        };

        menu.Opening += (s, e) => {
            bool algunItemSeleccionado = listViewPrincipal.SelectedItems.Count > 0;
            
            c.Abrir.Visible = algunItemSeleccionado;
            c.AbrirCon.Visible = algunItemSeleccionado;
            c.Exportar.Visible = algunItemSeleccionado;
            c.Copiar.Visible = algunItemSeleccionado;
            c.Renombrar.Visible = algunItemSeleccionado;
            c.Eliminar.Visible = algunItemSeleccionado;
            c.Fijar.Visible = algunItemSeleccionado;
            c.Favorito.Visible = algunItemSeleccionado;
            c.EnviarCorreoSmtp.Visible = algunItemSeleccionado && File.Exists(GetSelectedPath());
            c.EnviarCorreoMapi.Visible = algunItemSeleccionado && File.Exists(GetSelectedPath());
            c.Propiedades.Visible = algunItemSeleccionado;

            bool esFavoritosView = _rutaActual == "Favoritos";
            bool esFavoritosItem = algunItemSeleccionado && listViewPrincipal.SelectedItems[0].Tag?.ToString() == "Favoritos";
            c.VaciarFavoritos.Visible = esFavoritosView || esFavoritosItem;

            c.Sep1.Visible = algunItemSeleccionado;
            c.Sep2.Visible = algunItemSeleccionado;
            c.Sep3.Visible = true;

            bool puedePegar = Clipboard.ContainsFileDropList() &&
                              _rutaActual != "Inicio" &&
                              _rutaActual != "Favoritos" &&
                              _rutaActual != "EsteEquipo" &&
                              Directory.Exists(_rutaActual);
            c.Pegar.Enabled = puedePegar;

            if (algunItemSeleccionado)
            {
                string ruta = GetSelectedPath();
                c.Fijar.Text = _accesosDirectos.Contains(ruta) ? "📌  Desanclar de acceso directo" : "📌  Fijar a acceso directo";
                c.Favorito.Text = _elementosFavoritos.Contains(ruta) ? "⭐  Quitar de Favoritos" : "⭐  Agregar a Favoritos";

                string ext = Path.GetExtension(ruta).ToLower();
                string[] imgExt = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                string[] mediaExt = { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma", ".aac" };
                string[] videoExt = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v" };
                string[] dataExt = { ".csv", ".json", ".xml", ".txt" };
                
                c.AppVideo.Visible = videoExt.Contains(ext);
                c.AppFoto.Visible = imgExt.Contains(ext);
                c.Music.Visible = mediaExt.Contains(ext);
                c.Texto.Visible = dataExt.Contains(ext) || new[] { ".cs", ".html", ".css", ".js", ".md", ".py" }.Contains(ext);
            }
        };
    }

    /// <summary>
    /// Obtiene la ruta completa del item seleccionado en el ListView.
    /// Útil para usar en acciones como abrir, cortar, copiar, eliminar, etc.
    /// </summary>
    /// <returns>La ruta física del elemento seleccionado, o una cadena vacía si no hay selección.</returns>
    private string GetSelectedPath() => listViewPrincipal.SelectedItems[0].Tag?.ToString() ?? "";


    /// <summary>
    /// Método auxiliar genérico que muestra subformularios del explorador en pantalla.
    /// </summary>
    /// <param name="frm">Instancia del formulario a mostrar (ej. AppVideoForm, AppFotoForm).</param>
    private void AbrirCon(Form frm) => frm.Show();

    /// <summary>
    /// Abre el reproductor de música reutilizando la ventana existente si ya está abierta.
    /// </summary>
    private void AbrirReproductor(List<string> rutas, string? rutaInicial = null)
    {
        if (_reproductorMusica == null || _reproductorMusica.IsDisposed)
        {
            _reproductorMusica = new MusicPlayerForm(rutas, rutaInicial);
            _reproductorMusica.Show();
        }
        else
        {
            _reproductorMusica.CargarNuevaCola(rutas, rutaInicial);
            if (_reproductorMusica.WindowState == FormWindowState.Minimized)
                _reproductorMusica.WindowState = FormWindowState.Normal;
            _reproductorMusica.BringToFront();
        }
    }

    /// <summary>
    /// Delega la ejecución del archivo al visor predeterminado del sistema operativo
    /// mediante <c>Process.Start</c> y <c>UseShellExecute = true</c>.
    /// </summary>
    /// <param name="ruta">Ruta física del archivo a ejecutar.</param>
    private void AbrirConSistema(string ruta) => Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });

    private async void ExportarArchivo(string ruta, string formatoDestino)
    {
        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return;
        
        try
        {
            // Deshabilitar la interfaz mientras carga para evitar clicks múltiples
            this.Enabled = false;
            
            await Task.Run(() => ExploradorArchivos.Services.FileConverterService.Convertir(ruta, formatoDestino));
            
            MessageBox.Show($"Archivo exportado a {formatoDestino} exitosamente.", "Exportación Completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            CargarDirectorio(_rutaActual, false);
        }
        catch (NotSupportedException ex)
        {
            MessageBox.Show(ex.Message, "Formato No Soportado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar el archivo: {ex.Message}", "Error de Exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.Enabled = true;
        }
    }

    /// <summary>
    /// Almacena la referencia del archivo seleccionado en el portapapeles del sistema para operaciones de mover.
    /// </summary>
    /// <param name="ruta">Ruta del archivo a cortar.</param>
    private void CortarArchivo(string ruta)
    {
        var paths = new System.Collections.Specialized.StringCollection { ruta };
        DataObject data = new DataObject();
        data.SetFileDropList(paths);
        data.SetData("Preferred DropEffect", DragDropEffects.Move);
        Clipboard.SetDataObject(data);
    }

    private void CopiarArchivo(string ruta)
    {
        var paths = new System.Collections.Specialized.StringCollection { ruta };
        Clipboard.SetFileDropList(paths);
    }

    private void CopiarSeleccionados()
    {
        if (listViewPrincipal.SelectedItems.Count == 0) return;
        var paths = new System.Collections.Specialized.StringCollection();
        foreach (ListViewItem item in listViewPrincipal.SelectedItems)
        {
            string? ruta = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(ruta))
            {
                paths.Add(ruta);
            }
        }
        if (paths.Count > 0)
        {
            Clipboard.SetFileDropList(paths);
        }
    }

    private void CortarSeleccionados()
    {
        if (listViewPrincipal.SelectedItems.Count == 0) return;
        var paths = new System.Collections.Specialized.StringCollection();
        foreach (ListViewItem item in listViewPrincipal.SelectedItems)
        {
            string? ruta = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(ruta))
            {
                paths.Add(ruta);
            }
        }
        if (paths.Count > 0)
        {
            DataObject data = new DataObject();
            data.SetFileDropList(paths);
            data.SetData("Preferred DropEffect", DragDropEffects.Move);
            Clipboard.SetDataObject(data);
        }
    }

    private void PegarArchivos()
    {
        try
        {
            if (!Clipboard.ContainsFileDropList()) return;
            var paths = Clipboard.GetFileDropList();
            if (paths.Count == 0) return;

            string destinoDir = _rutaActual;
            if (destinoDir == "Inicio" || destinoDir == "Favoritos" || destinoDir == "EsteEquipo" || !Directory.Exists(destinoDir))
            {
                MessageBox.Show("No se pueden pegar archivos en esta ubicación virtual.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool esMover = false;
            var dropEffect = Clipboard.GetData("Preferred DropEffect");
            if (dropEffect != null && (int)dropEffect == (int)DragDropEffects.Move)
            {
                esMover = true;
            }

            foreach (string origen in paths)
            {
                if (string.IsNullOrEmpty(origen)) continue;
                string nombre = Path.GetFileName(origen);
                string destino = Path.Combine(destinoDir, nombre);

                if (string.Equals(origen, destino, StringComparison.OrdinalIgnoreCase))
                {
                    if (esMover) continue;
                    string ext = Path.GetExtension(origen);
                    string nombreSinExt = Path.GetFileNameWithoutExtension(origen);
                    destino = FileService.GenerarRutaUnica(destinoDir, nombreSinExt, ext);
                }

                if (Directory.Exists(origen))
                {
                    if (esMover)
                    {
                        if (Directory.Exists(destino))
                        {
                            MessageBox.Show($"Ya existe una carpeta con el nombre '{nombre}' en el destino.", "Conflicto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }
                        Directory.Move(origen, destino);
                    }
                    else
                    {
                        FileService.CopiarDirectorio(origen, destino);
                    }
                }
                else if (File.Exists(origen))
                {
                    if (esMover)
                    {
                        if (File.Exists(destino))
                        {
                            var result = MessageBox.Show($"El archivo '{nombre}' ya existe. ¿Deseas reemplazarlo?\n\n[Sí] Reemplazar\n[No] Conservar ambos (renombrar)\n[Cancelar] Cancelar operación", "Confirmar reemplazo", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (result == DialogResult.Yes)
                            {
                                File.Delete(destino);
                                File.Move(origen, destino);
                            }
                            else if (result == DialogResult.No)
                            {
                                string ext = Path.GetExtension(origen);
                                string nombreSinExt = Path.GetFileNameWithoutExtension(origen);
                                destino = FileService.GenerarRutaUnica(destinoDir, nombreSinExt, ext, "");
                                File.Move(origen, destino);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            File.Move(origen, destino);
                        }
                    }
                    else
                    {
                        if (File.Exists(destino))
                        {
                            var result = MessageBox.Show($"El archivo '{nombre}' ya existe. ¿Deseas reemplazarlo?\n\n[Sí] Reemplazar\n[No] Conservar ambos (renombrar)\n[Cancelar] Cancelar operación", "Confirmar reemplazo", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (result == DialogResult.Yes)
                            {
                                File.Copy(origen, destino, true);
                            }
                            else if (result == DialogResult.No)
                            {
                                string ext = Path.GetExtension(origen);
                                string nombreSinExt = Path.GetFileNameWithoutExtension(origen);
                                destino = FileService.GenerarRutaUnica(destinoDir, nombreSinExt, ext, "");
                                File.Copy(origen, destino);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            File.Copy(origen, destino);
                        }
                    }
                }
            }

            if (esMover)
            {
                Clipboard.Clear();
            }

            CargarDirectorio(_rutaActual, false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al pegar elementos: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Invoca a <see cref="FileService.EnviarAPapelera"/> para eliminar el archivo de forma segura.
    /// En lugar de eliminar permanentemente, permite deshacer la acción desde la Papelera de Windows.
    /// </summary>
    /// <param name="ruta">Ruta absoluta del archivo a eliminar.</param>
    private void EliminarArchivo(string ruta)
    {
        if (MessageBox.Show("¿Seguro que deseas eliminar este archivo?", "Eliminar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            FileService.EnviarAPapelera(ruta);
            CargarDirectorio(_rutaActual, false);
        }
    }
    /// <summary>
    /// Maneja el renombrado físico de archivos y directorios en disco.
    /// Mantiene la extensión original automáticamente si el usuario no la proporciona.
    /// </summary>
    /// <param name="rutaOld">Ruta actual del archivo.</param>
    /// <param name="nuevoNombre">Nuevo nombre deseado.</param>
    private void RenombrarArchivo(string rutaOld, string nuevoNombre)
    {
        try
        {
            string dir = Path.GetDirectoryName(rutaOld)!; // Obtener el directorio padre para construir la nueva ruta
            string ext = Path.GetExtension(rutaOld); // Obtener la extensión del archivo original
            string rutaNew = Path.Combine(dir, nuevoNombre + (nuevoNombre.EndsWith(ext) ? "" : ext)); // Asegura que la extensión se mantenga si el usuario no la incluye al renombrar
            if (rutaOld != rutaNew)
            {
                if (Directory.Exists(rutaOld)) Directory.Move(rutaOld, rutaNew); 
                else File.Move(rutaOld, rutaNew); 
                CargarDirectorio(_rutaActual, false); 
            }
        }
        catch (Exception ex) { MessageBox.Show("Error al renombrar: " + ex.Message); }
    }

    /// <summary>
    /// Lanza un diálogo informativo detallado sobre el archivo.
    /// Muestra su peso exacto, fecha de creación y atributos.
    /// </summary>
    /// <param name="ruta">Ruta del archivo a inspeccionar.</param>
    private void MostrarPropiedades(string ruta)
    {
        var info = new FileInfo(ruta);
        string msg = $"Nombre: {info.Name}\n" +
                     $"Tamaño: {info.Length / 1024.0:F2} KB\n" +
                     $"Creado: {info.CreationTime}\n" +
                     $"Modificado: {info.LastWriteTime}";
        MessageBox.Show(msg, "Propiedades de Archivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Clase anidada de pintura GDI+ encargada de forzar el dibujo de bordes y rellenos en menús contextuales
    /// para integrarlos a la estética pastel de la aplicación.
    /// </summary>
    class CustomMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Rectangle rect = new Rectangle(Point.Empty, e.Item.Size);
                using (SolidBrush brush = new SolidBrush(ThemeRenderer.Selection))
                    e.Graphics.FillRectangle(brush, rect);
            }
        }
    }

    // ===== CONFIGURACIÓN DE INTERFAZ Y ESTILO VISUAL ===

    /// <summary>
    /// Inicializa y configura toda la interfaz visual utilizando el sistema de renderizado personalizado (ThemeRenderer).
    /// Asigna las vistas, columnas, colores pastel, eventos OwnerDraw y reordena los paneles para lograr el estilo Minimalista Glass.
    /// </summary>
    private void ConfigurarUI()
    {
        ThemeRenderer.ApplyTheme(this);
        ConfigurarSemaforos();
        this.BackColor = ThemeRenderer.MainBg;

        ConfigurarListViewPrincipal();
        ConfigurarPanelSuperior();
        ConfigurarPanelLateral();
        ConfigurarBarraFiltrosYDirecciones();
        ConfigurarElementosDecorativosYEstado();

        // --- Reordenar paneles ---
        splitContainerMain.Panel1.Controls.Clear();
        splitContainerMain.Panel2.Controls.Clear();

        splitContainerMain.Panel1.Controls.Add(pnlSearch);
        splitContainerMain.Panel1.Controls.Add(treeViewLateral);
        
        treeViewLateral.BringToFront();

        splitContainerMain.Panel2.Controls.Add(listViewPrincipal);
        splitContainerMain.Panel2.Controls.Add(_pnlFiltros);
        listViewPrincipal.BringToFront(); // <-- CLAVE: listViewPrincipal debe estar al frente para que dockee al último y no quede bajo _pnlFiltros

        splitContainerMain.SplitterDistance = 280; // Más espacio para el sidebar
    }

    private void ConfigurarListViewPrincipal()
    {
        typeof(ListView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(listViewPrincipal, true);

        typeof(TreeView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(treeViewLateral, true);

        listViewPrincipal.BackColor = Color.White;
        listViewPrincipal.BorderStyle = BorderStyle.Fixed3D;
        treeViewLateral.BackColor = ThemeRenderer.SecondaryBg;
        treeViewLateral.BorderStyle = BorderStyle.Fixed3D;
        treeViewLateral.ItemHeight = 28; // Mayor espaciado en sidebar
        
        // Configurar Vista de Detalles Clásica (Más ancha y limpia)
        listViewPrincipal.View = View.Details;
        listViewPrincipal.HeaderStyle = ColumnHeaderStyle.Clickable;
        
        // Aumentar la altura de las filas para que no se vea amontonado
        var spacerList = new ImageList { ImageSize = new Size(1, 36) }; // 36px de alto
        listViewPrincipal.SmallImageList = spacerList;
        
        listViewPrincipal.Columns.Clear();
        listViewPrincipal.Columns.Add("Nombre", 450);
        listViewPrincipal.Columns.Add("Tipo", 120);
        listViewPrincipal.Columns.Add("Tamaño", 100);
        listViewPrincipal.Columns.Add("Info / Contenido", 150);
        listViewPrincipal.Columns.Add("Fecha de modificación", 200);
        listViewPrincipal.GridLines = true;

        _sorter = new ListViewSorter();
        listViewPrincipal.ListViewItemSorter = _sorter;

        ConfigurarFiltrosRapidos();
        ConfigurarVistaMiniaturas();

        listViewPrincipal.ShowGroups = true;

        // OwnerDraw para diseño clásico 95
        listViewPrincipal.OwnerDraw = true;
        listViewPrincipal.DrawColumnHeader += ThemeRenderer.DrawListViewColumnHeader;
        listViewPrincipal.DrawItem += ThemeRenderer.DrawListViewItem;
        listViewPrincipal.DrawSubItem += ThemeRenderer.DrawListViewSubItem;

        treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
        treeViewLateral.DrawNode += ThemeRenderer.DrawTreeNode;
    }

    private void ConfigurarPanelSuperior()
    {
        pnlTop.BackColor = ThemeRenderer.SecondaryBg;
        pnlTop.Height = 80;
        pnlTop.Controls.Clear();
        pnlTop.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTop.ClientRectangle, true);

        // Arrastrar Ventana
        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlTop.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlTop.MouseUp += (s, e) => { isDragging = false; };

        ConfigurarSemaforos();

        // Grupo: Navegación
        Panel pnlNav = CrearGrupoHerramientas("", 15, 20, 130);
        pnlNav.Controls.Add(btnAtras); btnAtras.Location = new Point(10, 18); btnAtras.Size = new Size(35, 30);
        pnlNav.Controls.Add(btnSubir); btnSubir.Location = new Point(50, 18); btnSubir.Size = new Size(35, 30);
        pnlNav.Controls.Add(btnActualizar); btnActualizar.Location = new Point(90, 18); btnActualizar.Size = new Size(35, 30);
        pnlTop.Controls.Add(pnlNav);

        // Grupo: Dirección
        Panel pnlAddr = CrearGrupoHerramientas("", 155, 20, pnlTop.Width - 455);
        pnlAddr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Button btnCopiarRuta = new Button();
        btnCopiarRuta.Size = new Size(35, 36);
        btnCopiarRuta.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        ConfigurarBotonClasico(btnCopiarRuta, "📋");
        btnCopiarRuta.Click += (s, e) => {
            if (!string.IsNullOrEmpty(_rutaActual))
            {
                Clipboard.SetText(_rutaActual);
                MessageBox.Show("Ruta copiada al portapapeles:\n" + _rutaActual, "Copiar Ruta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        pnlAddr.Controls.Add(pnlAddressBorder);
        pnlAddressBorder.Location = new Point(10, 16);
        pnlAddressBorder.Size = new Size(pnlAddr.Width - 55, 36);
        pnlAddressBorder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        btnCopiarRuta.Location = new Point(pnlAddr.Width - 40, 16);
        pnlAddr.Controls.Add(btnCopiarRuta);

        pnlTop.Controls.Add(pnlAddr);

        // Grupo: Acciones
        Panel pnlActions = CrearGrupoHerramientas("", pnlTop.Width - 275, 20, 265);
        pnlActions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlActions.Controls.Add(btnNuevaCarpeta);    btnNuevaCarpeta.Location    = new Point(10,  18); btnNuevaCarpeta.Size    = new Size(35, 30);
        pnlActions.Controls.Add(btnExportarCSV);     btnExportarCSV.Location     = new Point(50,  18); btnExportarCSV.Size     = new Size(35, 30);
        pnlActions.Controls.Add(_btnToggleVista);    _btnToggleVista.Location    = new Point(90,  18); _btnToggleVista.Size    = new Size(35, 30);
        
        _btnAppData = new Button();
        pnlActions.Controls.Add(_btnAppData);        _btnAppData.Location        = new Point(130, 18); _btnAppData.Size        = new Size(35, 30);
        pnlActions.Controls.Add(btnCamara);          btnCamara.Location          = new Point(170, 18); btnCamara.Size          = new Size(35, 30);
        pnlActions.Controls.Add(btnCapturaPantalla); btnCapturaPantalla.Location = new Point(210, 18); btnCapturaPantalla.Size = new Size(35, 30);

        pnlTop.Controls.Add(pnlActions);
    }

    private void ConfigurarPanelLateral()
    {
        pnlSearch.BackColor = ThemeRenderer.SecondaryBg;
        pnlSearch.Height = 65;
        pnlSearch.Controls.Clear();
        pnlSearch.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlSearch.ClientRectangle, true);

        bool isDragging = false;
        Point lastCursor = Point.Empty;
        pnlSearch.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; lastCursor = e.Location; } };
        pnlSearch.MouseMove += (s, e) => { if (isDragging) { this.Location = new Point(this.Location.X + (e.X - lastCursor.X), this.Location.Y + (e.Y - lastCursor.Y)); } };
        pnlSearch.MouseUp += (s, e) => { isDragging = false; };

        pnlSearch.Controls.Add(pnlSearchBorder);
        pnlSearchBorder.Location = new Point(15, 15);
        pnlSearchBorder.Width = pnlSearch.Width - 40;
        pnlSearchBorder.Height = 30;
        pnlSearchBorder.BackColor = Color.White;
        
        pnlSearchBorder.Paint += (s, e) => {
            ThemeRenderer.DrawClassicBorder(e.Graphics, pnlSearchBorder.ClientRectangle, false); // Sunken
        };
        
        txtBuscar.BackColor = Color.White;
        txtBuscar.Location = new Point(5, 5);
        txtBuscar.Width = pnlSearchBorder.Width - 40;
        txtBuscar.Font = new Font(this.Font.FontFamily, 8);
        
        btnBuscar.Text = "🔍";
        btnBuscar.BackColor = Color.Transparent;
        btnBuscar.ForeColor = ThemeRenderer.SecondaryText;
        btnBuscar.Size = new Size(25, 25);
        btnBuscar.Location = new Point(pnlSearchBorder.Width - 30, 2);
        btnBuscar.FlatStyle = FlatStyle.Flat;
        btnBuscar.FlatAppearance.BorderSize = 0;

        pnlSearchBorder.Controls.Add(txtBuscar);
        pnlSearchBorder.Controls.Add(btnBuscar);
    }

    private void ConfigurarBarraFiltrosYDirecciones()
    {
        _pnlFiltros.BackColor = ThemeRenderer.MainBg;
        _pnlFiltros.Height = 55;
        _pnlFiltros.Padding = new Padding(15, 12, 0, 0);
        _pnlFiltros.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, _pnlFiltros.ClientRectangle, true);

        // --- Configurar Botones (Estilo Clásico 3D) ---
        ConfigurarBotonClasico(btnAtras, "◀️");
        ConfigurarBotonClasico(btnSubir, "🔼");
        ConfigurarBotonClasico(btnActualizar, "🔄");
        ConfigurarBotonClasico(btnNuevaCarpeta, "📁");
        ConfigurarBotonClasico(btnExportarCSV, "📤");
        ConfigurarBotonClasico(_btnToggleVista, "🖼️");
        ConfigurarBotonClasico(_btnAppData, "📊");
        ConfigurarBotonClasico(btnCamara, "📷");
        ConfigurarBotonClasico(btnCapturaPantalla, "📺");

        // --- Barra de Direcciones ---
        pnlAddressBorder.BackColor = Color.White;
        pnlAddressBorder.Paint += (s, e) => {
            ThemeRenderer.DrawClassicBorder(e.Graphics, pnlAddressBorder.ClientRectangle, false); // Sunken
        };
        
        _flpBreadcrumbs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            WrapContents = false,
            AutoScroll = false,
            Cursor = Cursors.IBeam,
            Padding = new Padding(10, 4, 0, 0)
        };
        _flpBreadcrumbs.Click += (s, e) => MostrarTextBoxDireccion();
        pnlAddressBorder.Controls.Add(_flpBreadcrumbs);
        _flpBreadcrumbs.BringToFront();

        txtDireccion.BackColor = Color.White;
        txtDireccion.Visible = false;
        txtDireccion.Leave += (s, e) => OcultarTextBoxDireccion();
    }

    private void ConfigurarElementosDecorativosYEstado()
    {
        pnlBottom.BackColor = ThemeRenderer.MainBg;
        pnlBottom.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, pnlBottom.ClientRectangle, true);

        lblStatus.ForeColor = ThemeRenderer.MainText;
        lblStatus.Font = new Font(this.Font.FontFamily, 8);
        lblStatus.AutoSize = false;
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.Text = "Vista de Inicio cargada";
        
        pnlTrash.BackColor = ThemeRenderer.MainBg;
        pnlTrash.Paint += (s, e) => {
            ThemeRenderer.DrawClassicBorder(e.Graphics, pnlTrash.ClientRectangle, false); // Sunken
        };
        lblTrash.ForeColor = ThemeRenderer.MainText;
        lblTrash.Text = "🗑️ Papelera";
    }

    private Panel CrearGrupoHerramientas(string titulo, int x, int y, int ancho)
    {
        Panel pnl = new Panel { Location = new Point(x, y), Size = new Size(ancho, 55), BackColor = Color.Transparent };
        Label lbl = new Label { Text = titulo, Location = new Point(5, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ThemeRenderer.SecondaryText };
        pnl.Controls.Add(lbl);
        pnl.Paint += (s, e) => ThemeRenderer.DrawClassicBorder(e.Graphics, new Rectangle(0, 15, pnl.Width - 1, pnl.Height - 16), false);
        return pnl;
    }

    private void ConfigurarSemaforos()
    {
        Panel pnlSemaforos = new Panel { Location = new Point(15, 5), Size = new Size(60, 20), BackColor = Color.Transparent };
        
        Button btnClose = CrearBotonSemaforo(Color.FromArgb(255, 95, 86), 0);
        btnClose.Click += (s, e) => this.Close();
        
        Button btnMin = CrearBotonSemaforo(Color.FromArgb(255, 189, 46), 20);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        
        Button btnMax = CrearBotonSemaforo(Color.FromArgb(39, 201, 63), 40);
        btnMax.Click += (s, e) => {
            this.WindowState = this.WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
        };

        pnlSemaforos.Controls.AddRange(new Control[] { btnClose, btnMin, btnMax });
        pnlTop.Controls.Add(pnlSemaforos);
    }

    private Button CrearBotonSemaforo(Color color, int x)
    {
        Button b = new Button { Location = new Point(x, 2), Size = new Size(14, 14), BackColor = color, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(pnlTop.BackColor);
            e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, b.Width - 1, b.Height - 1);
            e.Graphics.DrawEllipse(new Pen(Color.FromArgb(50, Color.Black)), 0, 0, b.Width - 1, b.Height - 1);
        };
        return b;
    }

    private void ConfigurarBotonClasico(Button btn, string text)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = ThemeRenderer.MainBg;
        btn.Text = text;
        btn.Font = new Font("MS Sans Serif", 9, FontStyle.Bold); // Botones más legibles
        btn.ForeColor = ThemeRenderer.MainText;
        
        bool isPressed = false;

        btn.MouseDown += (s, e) => { isPressed = true; btn.Invalidate(); };
        btn.MouseUp += (s, e) => { isPressed = false; btn.Invalidate(); };

        btn.Paint += (s, e) => {
            ThemeRenderer.DrawClassicBorder(e.Graphics, btn.ClientRectangle, !isPressed);
            if (isPressed)
            {
                TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, 
                    new Rectangle(btn.ClientRectangle.X + 1, btn.ClientRectangle.Y + 1, btn.ClientRectangle.Width, btn.ClientRectangle.Height), 
                    btn.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
    }

    private void ConectarEventos()
    {
        // Navegación
        btnAtras.Click += (s, e) => { if (_navigationService.PuedeRetroceder) CargarDirectorio(_navigationService.Retroceder(), false); };
        btnSubir.Click += (s, e) =>
        {
            var parent = Directory.GetParent(_rutaActual);
            if (parent != null) CargarDirectorio(parent.FullName);
        };
        btnActualizar.Click += (s, e) => CargarDirectorio(_rutaActual, false);
        txtDireccion.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CargarDirectorio(txtDireccion.Text); }
        };

        // ListView
        listViewPrincipal.DoubleClick += ListViewPrincipal_DoubleClick;
        listViewPrincipal.MouseMove += (s, e) =>
        {
            var item = listViewPrincipal.GetItemAt(e.X, e.Y);
            int newIndex = item?.Index ?? -1;
            if (newIndex != ThemeRenderer.GetHoverIndex())
            {
                ThemeRenderer.SetHoverIndex(newIndex);
                listViewPrincipal.Invalidate();
            }
        };
        listViewPrincipal.MouseLeave += (s, e) =>
        {
            ThemeRenderer.SetHoverIndex(-1);
            listViewPrincipal.Invalidate();
        };

        // Drag & Drop a Papelera y Mover
        listViewPrincipal.ItemDrag += (s, e) => listViewPrincipal.DoDragDrop(listViewPrincipal.SelectedItems, DragDropEffects.Move);
        pnlTrash.DragEnter += PnlTrash_DragEnter;
        pnlTrash.DragLeave += PnlTrash_DragLeave;
        pnlTrash.DragDrop += PnlTrash_DragDrop;

        listViewPrincipal.DragEnter += ListViewPrincipal_DragEnter;
        listViewPrincipal.DragOver += ListViewPrincipal_DragOver;
        listViewPrincipal.DragLeave += ListViewPrincipal_DragLeave;
        listViewPrincipal.DragDrop += ListViewPrincipal_DragDrop;

        treeViewLateral.AllowDrop = true;
        treeViewLateral.DragEnter += TreeViewLateral_DragEnter;
        treeViewLateral.DragOver += TreeViewLateral_DragOver;
        treeViewLateral.DragDrop += TreeViewLateral_DragDrop;

        // Búsqueda, Creación y Exportación
        txtBuscar.KeyDown += TxtBuscar_KeyDown;
        btnExportarCSV.Click += BtnExportarCSV_Click;
        btnNuevaCarpeta.Click += BtnNuevaCarpeta_Click;

        // Ordenamiento por columnas
        listViewPrincipal.ColumnClick += ListViewPrincipal_ColumnClick;

        // Quick Look
        listViewPrincipal.KeyDown += ListViewPrincipal_KeyDown;

        // TreeView
        treeViewLateral.NodeMouseDoubleClick += TreeViewLateral_NodeMouseDoubleClick;

        // Cámara
        btnCamara.Click += BtnCamara_Click;

        // Captura de Pantalla
        btnCapturaPantalla.Click += BtnCapturaPantalla_Click;

        // App Data Button click handler
        _btnAppData.Click += (s, e) => {
            string selectedPath = listViewPrincipal.SelectedItems.Count > 0 ? GetSelectedPath() : null;
            AbrirCon(new ExploradorArchivos.AppDataFusion.MainForm(selectedPath));
        };
    }

    private void BtnCamara_Click(object? sender, EventArgs e)
    {
        if (_rutaActual == "Inicio" || _rutaActual == "Favoritos" || _rutaActual == "EsteEquipo" || !Directory.Exists(_rutaActual))
        {
            MessageBox.Show("No se pueden guardar fotos en una ubicación virtual. Por favor, navega a una carpeta física.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using (var camaraForm = new ExploradorArchivos.AppCamara.AppCamaraForm(_rutaActual))
        {
            if (camaraForm.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(camaraForm.RutaFotoGuardada))
            {
                CargarDirectorio(_rutaActual, false);
                var editor = new AppFotoForm(camaraForm.RutaFotoGuardada);
                editor.Show();
            }
        }
    }

    /// <summary>
    /// Abre el módulo de captura y grabación de pantalla.
    /// Los archivos se guardan por defecto en Mis Imágenes (capturas) y Mis Videos (grabaciones).
    /// </summary>
    private void BtnCapturaPantalla_Click(object? sender, EventArgs e)
    {
        using var form = new AppCapturaPantallaForm();
        form.ShowDialog(this);
    }
}