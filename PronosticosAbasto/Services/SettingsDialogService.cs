using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PronosticosAbasto.Controls;
using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;
using PronosticosAbasto.Core.Storage;
using PronosticosAbasto.ViewModels;

namespace PronosticosAbasto.Services;

public sealed record CompanySettingsEdit(
    CoverageThresholds Thresholds,
    IReadOnlyList<string> Zones,
    IReadOnlyList<string> IgnoredZones,
    IReadOnlyList<SatellitePreparedArticle> PreparedArticles,
    IReadOnlyList<TarimaSizeEntry> TarimaSizes);

/// <summary>
/// Per-company settings editor: the traffic-light thresholds (red and yellow
/// cutoffs as % of forecast) plus the external (satellite) storage zones.
/// Hosts the <see cref="SettingsDialogContent"/> control (matches the Fluent mockup).
/// </summary>
public sealed class SettingsDialogService
{
    public async Task<CompanySettingsEdit?> EditAsync(
        string companyName,
        CoverageThresholds thresholds,
        IReadOnlyList<string> currentZones,
        IReadOnlyList<string> currentIgnoredZones,
        IReadOnlyList<SatellitePreparedArticle> currentPreparedArticles,
        IReadOnlyList<TarimaSizeEntry> currentTarimaSizes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(currentZones);
        ArgumentNullException.ThrowIfNull(currentIgnoredZones);
        ArgumentNullException.ThrowIfNull(currentPreparedArticles);
        ArgumentNullException.ThrowIfNull(currentTarimaSizes);

        var viewModel = new SettingsDialogViewModel(
            companyName,
            thresholds,
            currentZones,
            currentIgnoredZones,
            currentPreparedArticles,
            currentTarimaSizes);

        var titleIcon = new FontIcon
        {
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["GuardianIconFontFamily"],
            Glyph = "",
            FontSize = 22,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GuardianPrimaryBrush"],
        };
        var titleText = new TextBlock
        {
            Text = viewModel.CompanyTitle,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 18,
        };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        title.Children.Add(titleIcon);
        title.Children.Add(titleText);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new SettingsDialogContent(viewModel),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 640,
            },
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            PrimaryButtonStyle = (Style)Application.Current.Resources["GuardianPrimaryButtonStyle"],
            CloseButtonStyle = (Style)Application.Current.Resources["GuardianSecondaryButtonStyle"],
            XamlRoot = ((FrameworkElement)App.Window.Content).XamlRoot,
            RequestedTheme = App.CurrentTheme,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760d;
        dialog.Resources["ContentDialogMinWidth"] = 720d;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var red = double.IsNaN(viewModel.RedPercent) || viewModel.RedPercent < 0 ? 0d : viewModel.RedPercent;
        var yellow = double.IsNaN(viewModel.HealthyPercent) || viewModel.HealthyPercent <= 0 ? 100d : viewModel.HealthyPercent;
        if (yellow < red)
        {
            yellow = red;
        }

        return new CompanySettingsEdit(
            new CoverageThresholds((decimal)red, (decimal)yellow),
            viewModel.Zones.ToList(),
            viewModel.IgnoredZones.ToList(),
            viewModel.ToPreparedArticles(),
            viewModel.ToTarimaSizes());
    }
}
