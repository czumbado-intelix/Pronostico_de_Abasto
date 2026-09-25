using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PronosticosAbasto;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Tema unico de la app.
    /// </summary>
    /// <remarks>
    /// La guia visual es explicita: "las superficies no usan fondos oscuros ni
    /// colores adicionales como tema dominante". Habia un boton para alternar
    /// claro/oscuro, pero los diccionarios Light y Dark de App.xaml son
    /// identicos color por color: alternar no cambiaba ni un pincel propio y
    /// solo daba vuelta los controles del sistema (cuadros de texto, listas
    /// desplegables, dialogos), dejando texto claro sobre superficies blancas.
    /// El tema queda fijo en claro; los diccionarios gemelos se conservan a
    /// proposito para que el tema del sistema tampoco altere nada.
    /// Usar esta propiedad al crear <c>ContentDialog</c> y menus, que se montan
    /// fuera del arbol de la ventana y no heredan el <c>RequestedTheme</c> de la
    /// ventana.
    /// </remarks>
    public static ElementTheme CurrentTheme => ElementTheme.Light;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnAppUnhandledException;
    }

    /// <summary>Ruta del log de errores no controlados.</summary>
    public static string CrashLogPath =>
        System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            Core.Storage.LocalJsonStorage.FolderName,
            "crash.log");

    /// <summary>Evita una cascada de dialogos si la misma falla se repite en rafaga.</summary>
    private bool _isReportingUnhandledException;

    /// <summary>
    /// Ultimo recurso: registra la excepcion y mantiene la app viva en lugar de
    /// terminar, pero <b>avisando al operador</b>. Tragarla en silencio dejaba la
    /// app corriendo en un estado posiblemente inconsistente sin que nadie lo
    /// supiera; el aviso deja claro que la ultima accion pudo no completarse.
    /// </summary>
    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CrashLogPath)!);
            System.IO.File.AppendAllText(
                CrashLogPath,
                $"=== {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={System.Environment.NewLine}{e.Message}{System.Environment.NewLine}{e.Exception}{System.Environment.NewLine}{System.Environment.NewLine}");
        }
        catch
        {
            // Never let logging itself crash the app.
        }

        e.Handled = true;
        NotifyUnhandledException(e.Message);
    }

    private void NotifyUnhandledException(string message)
    {
        if (_isReportingUnhandledException)
        {
            return;
        }

        var xamlRoot = (Window?.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is null || DispatcherQueue is null)
        {
            return;
        }

        _isReportingUnhandledException = true;

        // Fuera del handler: mostrar un dialogo desde dentro del propio evento
        // de excepcion no controlada puede reentrar.
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = xamlRoot,
                    Title = "Ocurrio un error inesperado",
                    Content =
                        $"{message}{System.Environment.NewLine}{System.Environment.NewLine}" +
                        "La app sigue abierta, pero la ultima accion pudo no completarse. " +
                        "Verifica el resultado antes de continuar." +
                        $"{System.Environment.NewLine}{System.Environment.NewLine}Detalle en: {CrashLogPath}",
                    CloseButtonText = "Entendido",
                };

                await dialog.ShowAsync();
            }
            catch
            {
                // Si ni siquiera se puede mostrar el dialogo, el log ya quedo escrito.
            }
            finally
            {
                _isReportingUnhandledException = false;
            }
        });
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (Window.Content is FrameworkElement root)
        {
            root.RequestedTheme = CurrentTheme;
        }

        Window.Activate();
    }
}
