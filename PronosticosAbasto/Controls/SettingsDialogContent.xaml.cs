using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PronosticosAbasto.ViewModels;

namespace PronosticosAbasto.Controls;

public sealed partial class SettingsDialogContent : UserControl
{
    public SettingsDialogContent(SettingsDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SettingsDialogViewModel ViewModel { get; }

    private void RemoveZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string zone })
        {
            ViewModel.RemoveZone(zone);
        }
    }

    private void RemoveIgnoredZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string zone })
        {
            ViewModel.RemoveIgnoredZone(zone);
        }
    }

    private void RemovePreparedArticle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PreparedArticleEditViewModel item })
        {
            ViewModel.RemovePreparedArticle(item);
        }
    }

    private void RemoveTarimaSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TarimaSizeEditViewModel item })
        {
            ViewModel.RemoveTarimaSize(item);
        }
    }
}
