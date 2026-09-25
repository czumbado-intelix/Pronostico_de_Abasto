using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// Backs the redesigned "Ajustes de Abasto" dialog: the coverage traffic-light
/// thresholds (critical/warning %) with a live gradient bar, and the editable
/// list of external (satellite) zones.
/// </summary>
public partial class SettingsDialogViewModel : ObservableObject
{
    public SettingsDialogViewModel(
        string company,
        CoverageThresholds thresholds,
        IReadOnlyList<string> zones,
        IReadOnlyList<string> ignoredZones,
        IReadOnlyList<SatellitePreparedArticle> preparedArticles,
        IReadOnlyList<TarimaSizeEntry> tarimaSizes)
    {
        CompanyTitle = $"Ajustes de Abasto - {company}";
        RedPercent = (double)thresholds.RedPercent;
        HealthyPercent = (double)thresholds.HealthyPercent;
        Zones = new ObservableCollection<string>(zones);
        IgnoredZones = new ObservableCollection<string>(ignoredZones);
        PreparedArticles = new ObservableCollection<PreparedArticleEditViewModel>(
            preparedArticles.Select(article => new PreparedArticleEditViewModel(article.Article, article.Description)));
        TarimaSizes = new ObservableCollection<TarimaSizeEditViewModel>(
            tarimaSizes.Select(size => new TarimaSizeEditViewModel(size.Article, size.Description, size.SizeCode)));
    }

    public string CompanyTitle { get; }

    [ObservableProperty]
    public partial double RedPercent { get; set; }

    [ObservableProperty]
    public partial double HealthyPercent { get; set; }

    public ObservableCollection<string> Zones { get; }

    public ObservableCollection<string> IgnoredZones { get; }

    public ObservableCollection<PreparedArticleEditViewModel> PreparedArticles { get; }

    public ObservableCollection<TarimaSizeEditViewModel> TarimaSizes { get; }

    [ObservableProperty]
    public partial string NewZoneText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewIgnoredZoneText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPreparedArticleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPreparedDescriptionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreparedArticleSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTarimaArticleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTarimaDescriptionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTarimaSizeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TarimaSizeSearchText { get; set; } = string.Empty;

    public IReadOnlyList<PreparedArticleEditViewModel> FilteredPreparedArticles =>
        PreparedArticles
            .Where(item => MatchesSearch(item.Article, item.Description, PreparedArticleSearchText))
            .OrderBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<TarimaSizeEditViewModel> FilteredTarimaSizes =>
        TarimaSizes
            .Where(item => MatchesSearch(item.Article, $"{item.Description} {item.SizeCode}", TarimaSizeSearchText))
            .OrderBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public string PreparedArticleSummaryText =>
        $"{FilteredPreparedArticles.Count:N0} de {PreparedArticles.Count:N0} articulos";

    public string TarimaSizeSummaryText =>
        $"{FilteredTarimaSizes.Count:N0} de {TarimaSizes.Count:N0} tamanos";

    public string IgnoredZoneSummaryText =>
        $"{IgnoredZones.Count:N0} zonas excluidas";

    // Gradient-bar segment widths reflect the thresholds: red [0, critical),
    // amber [critical, warning), green [warning, 100].
    public GridLength RedColWidth => new(Math.Max(0.01, RedPercent), GridUnitType.Star);

    public GridLength AmberColWidth => new(Math.Max(0.01, HealthyPercent - RedPercent), GridUnitType.Star);

    public GridLength GreenColWidth => new(Math.Max(0.01, 100 - HealthyPercent), GridUnitType.Star);

    public string HealthyNoteText =>
        $"Cualquier valor superior al {(int)Math.Round(HealthyPercent)}% se considerara Saludable (Verde).";

    partial void OnRedPercentChanged(double value)
    {
        OnPropertyChanged(nameof(RedColWidth));
        OnPropertyChanged(nameof(AmberColWidth));
    }

    partial void OnHealthyPercentChanged(double value)
    {
        OnPropertyChanged(nameof(AmberColWidth));
        OnPropertyChanged(nameof(GreenColWidth));
        OnPropertyChanged(nameof(HealthyNoteText));
    }

    partial void OnPreparedArticleSearchTextChanged(string value) => NotifyPreparedArticlesChanged();

    partial void OnTarimaSizeSearchTextChanged(string value) => NotifyTarimaSizesChanged();

    [RelayCommand]
    private void AddZone()
    {
        var zone = NewZoneText?.Trim();
        if (!string.IsNullOrEmpty(zone) && !Zones.Contains(zone, StringComparer.OrdinalIgnoreCase))
        {
            Zones.Add(zone);
        }

        NewZoneText = string.Empty;
    }

    public void RemoveZone(string zone)
    {
        if (!string.IsNullOrEmpty(zone))
        {
            Zones.Remove(zone);
        }
    }

    [RelayCommand]
    private void AddIgnoredZone()
    {
        var zone = NewIgnoredZoneText?.Trim();
        if (!string.IsNullOrEmpty(zone) && !IgnoredZones.Contains(zone, StringComparer.OrdinalIgnoreCase))
        {
            IgnoredZones.Add(zone);
            OnPropertyChanged(nameof(IgnoredZoneSummaryText));
        }

        NewIgnoredZoneText = string.Empty;
    }

    public void RemoveIgnoredZone(string zone)
    {
        if (!string.IsNullOrEmpty(zone))
        {
            IgnoredZones.Remove(zone);
            OnPropertyChanged(nameof(IgnoredZoneSummaryText));
        }
    }

    [RelayCommand]
    private void AddPreparedArticle()
    {
        var article = NewPreparedArticleText?.Trim();
        if (!string.IsNullOrEmpty(article) &&
            PreparedArticles.Any(item => item.Article.Equals(article, StringComparison.OrdinalIgnoreCase)))
        {
            PreparedArticleSearchText = article;
        }
        else if (!string.IsNullOrEmpty(article))
        {
            PreparedArticles.Add(new PreparedArticleEditViewModel(article, NewPreparedDescriptionText?.Trim() ?? string.Empty));
            NotifyPreparedArticlesChanged();
        }

        NewPreparedArticleText = string.Empty;
        NewPreparedDescriptionText = string.Empty;
    }

    public void RemovePreparedArticle(PreparedArticleEditViewModel item)
    {
        PreparedArticles.Remove(item);
        NotifyPreparedArticlesChanged();
    }

    [RelayCommand]
    private void AddTarimaSize()
    {
        var article = NewTarimaArticleText?.Trim();
        var sizeCode = TarimaSizeNormalizer.NormalizeOrEmpty(NewTarimaSizeText ?? string.Empty);
        if (!string.IsNullOrEmpty(article) && !string.IsNullOrEmpty(sizeCode))
        {
            var existing = TarimaSizes.FirstOrDefault(item =>
                item.Article.Equals(article, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Description = NewTarimaDescriptionText?.Trim() ?? string.Empty;
                existing.SizeCode = sizeCode;
                TarimaSizeSearchText = article;
                NotifyTarimaSizesChanged();
            }
            else
            {
                TarimaSizes.Add(new TarimaSizeEditViewModel(article, NewTarimaDescriptionText?.Trim() ?? string.Empty, sizeCode));
                NotifyTarimaSizesChanged();
            }
        }

        NewTarimaArticleText = string.Empty;
        NewTarimaDescriptionText = string.Empty;
        NewTarimaSizeText = string.Empty;
    }

    public void RemoveTarimaSize(TarimaSizeEditViewModel item)
    {
        TarimaSizes.Remove(item);
        NotifyTarimaSizesChanged();
    }

    public IReadOnlyList<SatellitePreparedArticle> ToPreparedArticles() =>
        PreparedArticles
            .Where(item => !string.IsNullOrWhiteSpace(item.Article))
            .Select(item => new SatellitePreparedArticle(item.Article.Trim(), item.Description?.Trim() ?? string.Empty))
            .ToArray();

    public IReadOnlyList<TarimaSizeEntry> ToTarimaSizes() =>
        TarimaSizes
            .Select(item => new TarimaSizeEntry(
                item.Article?.Trim() ?? string.Empty,
                item.Description?.Trim() ?? string.Empty,
                TarimaSizeNormalizer.NormalizeOrEmpty(item.SizeCode ?? string.Empty)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Article) && !string.IsNullOrWhiteSpace(item.SizeCode))
            .ToArray();

    private void NotifyPreparedArticlesChanged()
    {
        OnPropertyChanged(nameof(FilteredPreparedArticles));
        OnPropertyChanged(nameof(PreparedArticleSummaryText));
    }

    private void NotifyTarimaSizesChanged()
    {
        OnPropertyChanged(nameof(FilteredTarimaSizes));
        OnPropertyChanged(nameof(TarimaSizeSummaryText));
    }

    private static bool MatchesSearch(string article, string detail, string searchText)
    {
        var search = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return (article?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (detail?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}

public sealed partial class PreparedArticleEditViewModel : ObservableObject
{
    public PreparedArticleEditViewModel(string article, string description)
    {
        Article = article;
        Description = description;
    }

    [ObservableProperty]
    public partial string Article { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }
}

public sealed partial class TarimaSizeEditViewModel : ObservableObject
{
    public TarimaSizeEditViewModel(string article, string description, string sizeCode)
    {
        Article = article;
        Description = description;
        SizeCode = TarimaSizeNormalizer.NormalizeOrEmpty(sizeCode);
    }

    [ObservableProperty]
    public partial string Article { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial string SizeCode { get; set; }
}
