using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PronosticosAbasto.ViewModels;
using System.Windows.Input;

namespace PronosticosAbasto.Controls;

public sealed partial class ExcelTextFilterPanel : UserControl
{
    public static readonly DependencyProperty FilterProperty =
        DependencyProperty.Register(
            nameof(Filter),
            typeof(TextColumnFilter),
            typeof(ExcelTextFilterPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(ExcelTextFilterPanel),
            new PropertyMetadata(string.Empty, OnHeaderTextChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(ExcelTextFilterPanel),
            new PropertyMetadata("Buscar valores"));

    public static readonly DependencyProperty PanelWidthProperty =
        DependencyProperty.Register(
            nameof(PanelWidth),
            typeof(double),
            typeof(ExcelTextFilterPanel),
            new PropertyMetadata(260d));

    public static readonly DependencyProperty SortAscendingCommandProperty =
        DependencyProperty.Register(
            nameof(SortAscendingCommand),
            typeof(ICommand),
            typeof(ExcelTextFilterPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SortDescendingCommandProperty =
        DependencyProperty.Register(
            nameof(SortDescendingCommand),
            typeof(ICommand),
            typeof(ExcelTextFilterPanel),
            new PropertyMetadata(null));

    public ExcelTextFilterPanel()
    {
        InitializeComponent();
    }

    public TextColumnFilter? Filter
    {
        get => (TextColumnFilter?)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public double PanelWidth
    {
        get => (double)GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    public ICommand? SortAscendingCommand
    {
        get => (ICommand?)GetValue(SortAscendingCommandProperty);
        set => SetValue(SortAscendingCommandProperty, value);
    }

    public ICommand? SortDescendingCommand
    {
        get => (ICommand?)GetValue(SortDescendingCommandProperty);
        set => SetValue(SortDescendingCommandProperty, value);
    }

    public string ClearFilterText => string.IsNullOrWhiteSpace(HeaderText)
        ? "Borrar filtro"
        : $"Borrar filtro de \"{HeaderText.Replace("Filtrar ", string.Empty, StringComparison.OrdinalIgnoreCase)}\"";

    private static void OnHeaderTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ExcelTextFilterPanel panel)
        {
            panel.Bindings.Update();
        }
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (Filter?.ApplyCommand.CanExecute(null) == true)
        {
            Filter.ApplyCommand.Execute(null);
        }

        HideContainingFlyout();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (Filter?.CancelCommand.CanExecute(null) == true)
        {
            Filter.CancelCommand.Execute(null);
        }

        HideContainingFlyout();
    }

    private void HideContainingFlyout()
    {
        if (XamlRoot is not null)
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
            {
                if (popup.IsOpen && popup.Child is not null && ContainsElement(popup.Child, this))
                {
                    popup.IsOpen = false;
                    return;
                }
            }
        }

        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is Popup popup)
            {
                popup.IsOpen = false;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private static bool ContainsElement(DependencyObject root, DependencyObject target)
    {
        if (ReferenceEquals(root, target))
        {
            return true;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            if (ContainsElement(VisualTreeHelper.GetChild(root, index), target))
            {
                return true;
            }
        }

        return false;
    }
}
