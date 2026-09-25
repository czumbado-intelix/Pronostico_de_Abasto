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
    /// Initializes the singleton application object.
    /// </summary>
    private const string ThemeSettingKey = "AppTheme";

    /// <summary>The active app theme (Light default, mirroring the Fluent design).</summary>
    public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Light;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnAppUnhandledException;
    }

    /// <summary>Applies a theme to the whole window tree and persists the choice.</summary>
    public static void ApplyTheme(ElementTheme theme)
    {
        CurrentTheme = theme;
        if (Window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        try
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] = theme.ToString();
        }
        catch
        {
            // Persisting the theme is best-effort.
        }
    }

    /// <summary>Flips between light and dark.</summary>
    public static void ToggleTheme() =>
        ApplyTheme(CurrentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark);

    private static ElementTheme LoadSavedTheme()
    {
        try
        {
            if (Windows.Storage.ApplicationData.Current.LocalSettings.Values.TryGetValue(ThemeSettingKey, out var value) &&
                value is string text &&
                Enum.TryParse<ElementTheme>(text, out var theme) &&
                theme != ElementTheme.Default)
            {
                return theme;
            }
        }
        catch
        {
            // Fall back to the default below.
        }

        return ElementTheme.Light;
    }

    /// <summary>
    /// Last-resort handler: log the exception so it is not lost and keep the app
    /// alive instead of terminating on an unhandled UI-thread exception.
    /// </summary>
    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var directory = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "PronosticoDeAbasto");
            System.IO.Directory.CreateDirectory(directory);
            var logPath = System.IO.Path.Combine(directory, "crash.log");
            System.IO.File.AppendAllText(
                logPath,
                $"=== {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={System.Environment.NewLine}{e.Message}{System.Environment.NewLine}{e.Exception}{System.Environment.NewLine}{System.Environment.NewLine}");
        }
        catch
        {
            // Never let logging itself crash the app.
        }

        e.Handled = true;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
        ApplyTheme(LoadSavedTheme());
    }
}
