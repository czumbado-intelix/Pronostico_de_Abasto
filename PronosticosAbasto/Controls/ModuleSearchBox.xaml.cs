using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PronosticosAbasto.Services;
using PronosticosAbasto.ViewModels;
using Windows.System;

namespace PronosticosAbasto.Controls;

/// <summary>
/// Buscador de un modulo: el mismo en Traslados, Expediciones, Transito y
/// Comparativa.
/// </summary>
/// <remarks>
/// La logica de busqueda (palabras en cualquier orden, sin tildes, listas de
/// codigos) vive en <c>Core/Tables/SearchQuery</c>; este control solo se ocupa
/// del teclado, del portapapeles y de mostrar el resumen:
/// <list type="bullet">
/// <item>Enter aplica ya la busqueda (sin esperar la pausa de escritura).</item>
/// <item>Esc la borra.</item>
/// <item>Pegar varias lineas (una columna de Excel, una lista de un correo) las
/// convierte en una lista separada por comas. Un TextBox de una sola linea, por
/// su cuenta, se quedaria solo con la primera.</item>
/// <item>Si se buscaron varias cosas, una insignia dentro de la caja dice cuantas
/// aparecieron, y al tocarla se ven (y se copian) las que no.</item>
/// </list>
/// </remarks>
public sealed partial class ModuleSearchBox : UserControl
{
    private const double InputLeftPadding = 38;
    private const double InputRightPadding = 12;
    private const double InputVerticalPadding = 7;
    private const double BadgeGap = 6;

    /// <summary>
    /// Ancho de la columna del boton X propio del TextBox. Ese boton vive fuera
    /// del area de texto (su propia columna en la plantilla), asi que cuando se
    /// ve, el padding no tiene que cubrirlo de nuevo.
    /// </summary>
    private const double ClearButtonColumnWidth = 30;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ModuleSearchBox),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(ModuleSearchBox),
            new PropertyMetadata("Buscar..."));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(
            nameof(Summary),
            typeof(SearchResultSummary),
            typeof(ModuleSearchBox),
            new PropertyMetadata(SearchResultSummary.None, OnSummaryChanged));

    public static readonly DependencyProperty SubmitCommandProperty =
        DependencyProperty.Register(
            nameof(SubmitCommand),
            typeof(ICommand),
            typeof(ModuleSearchBox),
            new PropertyMetadata(null));

    public ModuleSearchBox()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Resumen de una busqueda con varias alternativas.</summary>
    public SearchResultSummary Summary
    {
        get => (SearchResultSummary?)GetValue(SummaryProperty) ?? SearchResultSummary.None;
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>Se ejecuta con Enter para aplicar la busqueda sin esperar.</summary>
    public ICommand? SubmitCommand
    {
        get => (ICommand?)GetValue(SubmitCommandProperty);
        set => SetValue(SubmitCommandProperty, value);
    }

    public Visibility SummaryVisibility => Summary.IsVisible ? Visibility.Visible : Visibility.Collapsed;

    public string SummaryBadge => Summary.Badge;

    public string SummaryTooltip => Summary.HasUnmatched
        ? $"{Summary.Unmatched.Count:N0} sin resultado. Toca para ver cuales."
        : "Se encontraron todos. Toca para ver el detalle.";

    public string SummaryDetail => Summary.Detail;

    /// <summary>Uno por linea, listo para pegar de vuelta en Excel.</summary>
    public string UnmatchedText => string.Join(Environment.NewLine, Summary.Unmatched);

    public Visibility UnmatchedVisibility => Summary.HasUnmatched ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Verde suave si aparecieron todos; ambar suave si falta alguno. El texto siempre negro (guia visual).</summary>
    public Brush SummaryBackground => LookupBrush(Summary.HasUnmatched ? "GuardianWarningLowBrush" : "GuardianPrimaryMediumBrush");

    public Brush SummaryForeground => LookupBrush("GuardianTextPrimaryBrush");

    /// <summary>Pone el foco en la caja y selecciona lo escrito, como Ctrl+F en cualquier programa.</summary>
    public void FocusSearch()
    {
        Input.Focus(FocusState.Keyboard);
        Input.SelectAll();
    }

    /// <summary>
    /// Una columna de Excel o una lista de un correo llega como varias lineas
    /// (y con tabuladores si se copiaron varias celdas por fila). Se convierte en
    /// una lista separada por comas, que el buscador entiende como alternativas.
    /// </summary>
    internal static string FlattenPastedLines(string? pasted)
    {
        if (string.IsNullOrEmpty(pasted))
        {
            return string.Empty;
        }

        var parts = pasted.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length <= 1 ? pasted.Trim() : string.Join(", ", parts);
    }

    private static void OnSummaryChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ModuleSearchBox box)
        {
            box.Bindings.Update();
            box.UpdateInputPadding();
        }
    }

    private static Brush LookupBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private void SummaryButton_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateInputPadding();

    private void Input_FocusChanged(object sender, RoutedEventArgs e) => UpdateInputPadding();

    /// <summary>
    /// Que el texto escrito termine justo antes de la insignia, ni debajo de ella
    /// ni mucho antes.
    /// </summary>
    /// <remarks>
    /// La insignia esta a <c>Margin.Right</c> del borde derecho. Con foco y texto,
    /// el TextBox muestra su X en una columna propia a la derecha del area de
    /// texto, que ya descuenta ese ancho; sin foco esa columna no existe.
    /// </remarks>
    private void UpdateInputPadding()
    {
        if (!Summary.IsVisible || SummaryButton.ActualWidth <= 0)
        {
            Input.Padding = new Thickness(InputLeftPadding, InputVerticalPadding, InputRightPadding, InputVerticalPadding);
            return;
        }

        var badgeFromRightEdge = SummaryButton.Margin.Right + SummaryButton.ActualWidth + BadgeGap;
        var clearButtonVisible = Input.FocusState != FocusState.Unfocused && !string.IsNullOrEmpty(Input.Text);
        var right = clearButtonVisible
            ? Math.Max(InputRightPadding, badgeFromRightEdge - ClearButtonColumnWidth)
            : badgeFromRightEdge;
        Input.Padding = new Thickness(InputLeftPadding, InputVerticalPadding, right, InputVerticalPadding);
    }

    private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                if (SubmitCommand?.CanExecute(null) == true)
                {
                    SubmitCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case VirtualKey.Escape when !string.IsNullOrEmpty(Input.Text):
                Input.Text = string.Empty;
                e.Handled = true;
                break;
        }
    }

    private void Input_Paste(object sender, TextControlPasteEventArgs e)
    {
        // El portapapeles de WinUI 3 (Clipboard.GetContent) sufre de un fallo nativo
        // documentado en Windows App SDK (#6754) que genera STATUS_STOWED_EXCEPTION
        // (0xc000027b) en Microsoft.UI.Xaml.dll cuando otra aplicacion (Excel, SAP,
        // RDP) bloquea el portapapeles o durante cambios rapidos de foco. SafeClipboard
        // utiliza la API Win32 inmune a esta falla.
        if (!SafeClipboard.TryGetText(out var pasted) || string.IsNullOrEmpty(pasted))
        {
            return;
        }

        e.Handled = true;

        var flattened = FlattenPastedLines(pasted);
        var current = Input.Text ?? string.Empty;
        var start = Math.Clamp(Input.SelectionStart, 0, current.Length);
        var length = Math.Clamp(Input.SelectionLength, 0, current.Length - start);
        Input.Text = current.Remove(start, length).Insert(start, flattened);
        Input.SelectionStart = start + flattened.Length;
        Input.SelectionLength = 0;
    }

    private void CopyUnmatched_Click(object sender, RoutedEventArgs e)
    {
        if (!Summary.HasUnmatched)
        {
            return;
        }

        SafeClipboard.TrySetText(UnmatchedText);
    }
}
