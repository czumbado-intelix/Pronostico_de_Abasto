using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PronosticosAbasto.Core.Analysis;
using SkiaSharp;
using PronosticosAbasto.Core.IO;
using PronosticosAbasto.Core.Storage;
using PronosticosAbasto.Services;
using Windows.Storage;

namespace PronosticosAbasto.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly InventoryWorkbookParser _inventoryParser;
    private readonly ForecastWorkbookParser _forecastParser;
    private readonly WeeklyForecastAnalyzer _weeklyForecastAnalyzer;
    private readonly AnalysisWorkbookExporter _analysisWorkbookExporter;
    private readonly WorkbookFileDialogService _workbookFileDialogService;
    private readonly SatelliteZoneStore _satelliteZoneStore;
    private readonly InventoryExcludedZoneStore _inventoryExcludedZoneStore;
    private readonly SatellitePreparedArticleStore _satellitePreparedArticleStore;
    private readonly TarimaSizeStore _tarimaSizeStore;
    private readonly WarehouseComparisonCommentStore _warehouseComparisonCommentStore;
    private readonly CoverageThresholdStore _coverageThresholdStore;
    private readonly SettingsDialogService _settingsDialogService;
    private readonly TransferTransitStore _transferTransitStore;
    private readonly TransferOrderStore _transferOrderStore;
    private readonly ExpedicionesWorkbookParser _expedicionesParser = new();
    private readonly SatellitePreparedArticleWorkbookParser _satellitePreparedArticleParser = new();
    private readonly TarimaSizeWorkbookParser _tarimaSizeParser = new();
    private readonly ExpedicionesAnalyzer _expedicionesAnalyzer = new();
    private readonly ScenarioSimulator _scenarioSimulator = new();
    private readonly WarehouseComparisonAnalyzer _warehouseComparisonAnalyzer = new();
    private static readonly string[] CompanyCatalog = ["EPA", "Cofersa"];
    private readonly Dictionary<string, CompanyAnalysisSession> _sessions =
        CompanyCatalog.ToDictionary(
            company => company,
            _ => new CompanyAnalysisSession(),
            StringComparer.OrdinalIgnoreCase);

    private bool _isRestoringState;

    /// <summary>
    /// Activo mientras una operacion masiva toca muchas filas: suprime la
    /// escritura por fila para dejar un solo guardado al final.
    /// </summary>
    private bool _isBulkOrderUpdate;

    private WeeklyAnalysisResult? _filteredAnalysis;
    private IReadOnlyList<ArticleResultRowViewModel> _allResults = Array.Empty<ArticleResultRowViewModel>();
    private IReadOnlyList<ArticleResultRowViewModel> _filteredResults = Array.Empty<ArticleResultRowViewModel>();
    private IReadOnlyList<ZoneDetailRowViewModel> _zoneDetails = Array.Empty<ZoneDetailRowViewModel>();
    private IReadOnlyList<WarehouseComparisonRowViewModel> _allWarehouseComparisonRows = Array.Empty<WarehouseComparisonRowViewModel>();
    private IReadOnlyList<WarehouseComparisonRowViewModel> _warehouseComparisonRows = Array.Empty<WarehouseComparisonRowViewModel>();
    private TextSortState? _tableTextSort;
    private TextSortState? _transferTextSort;
    private TextSortState? _expedicionTextSort;
    private TextSortState? _warehouseComparisonTextSort;

    public MainPageViewModel()
        : this(
            new InventoryWorkbookParser(),
            new ForecastWorkbookParser(),
            new WeeklyForecastAnalyzer(),
            new AnalysisWorkbookExporter(),
            new WorkbookFileDialogService(),
            new SatelliteZoneStore(CompanyCatalog),
            new InventoryExcludedZoneStore(CompanyCatalog),
            new SatellitePreparedArticleStore(CompanyCatalog),
            new TarimaSizeStore(CompanyCatalog),
            new WarehouseComparisonCommentStore(CompanyCatalog),
            new CoverageThresholdStore(),
            new SettingsDialogService(),
            new TransferTransitStore(),
            new TransferOrderStore())
    {
    }

    public MainPageViewModel(
        InventoryWorkbookParser inventoryParser,
        ForecastWorkbookParser forecastParser,
        WeeklyForecastAnalyzer weeklyForecastAnalyzer,
        AnalysisWorkbookExporter analysisWorkbookExporter,
        WorkbookFileDialogService workbookFileDialogService,
        SatelliteZoneStore satelliteZoneStore,
        InventoryExcludedZoneStore inventoryExcludedZoneStore,
        SatellitePreparedArticleStore satellitePreparedArticleStore,
        TarimaSizeStore tarimaSizeStore,
        WarehouseComparisonCommentStore warehouseComparisonCommentStore,
        CoverageThresholdStore coverageThresholdStore,
        SettingsDialogService settingsDialogService,
        TransferTransitStore transferTransitStore,
        TransferOrderStore transferOrderStore)
    {
        _inventoryParser = inventoryParser;
        _forecastParser = forecastParser;
        _weeklyForecastAnalyzer = weeklyForecastAnalyzer;
        _analysisWorkbookExporter = analysisWorkbookExporter;
        _workbookFileDialogService = workbookFileDialogService;
        _satelliteZoneStore = satelliteZoneStore;
        _inventoryExcludedZoneStore = inventoryExcludedZoneStore;
        _satellitePreparedArticleStore = satellitePreparedArticleStore;
        _tarimaSizeStore = tarimaSizeStore;
        _warehouseComparisonCommentStore = warehouseComparisonCommentStore;
        _coverageThresholdStore = coverageThresholdStore;
        _settingsDialogService = settingsDialogService;
        _transferTransitStore = transferTransitStore;
        _transferOrderStore = transferOrderStore;

        TableArticuloFilter = new TextColumnFilter(FilterTableRows);
        TableForecastFilter = new TextColumnFilter(FilterTableRows);
        TablePrincipalFilter = new TextColumnFilter(FilterTableRows);
        TableDifferenceFilter = new TextColumnFilter(FilterTableRows);
        TableStatusFilter = new TextColumnFilter(FilterTableRows);
        TableSatelliteFilter = new TextColumnFilter(FilterTableRows);
        TrasladosArticuloFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosDescripcionFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosCoberturaFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosPrincipalFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosStockExtFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosTransitoFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosTraerFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosPalletCountFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosPaletsFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosTarimaFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosAlistadoFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosZonasFilter = new TextColumnFilter(FilterTransferRows);
        TrasladosEstadoFilter = new TextColumnFilter(FilterTransferRows);
        ExpExpedicionFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpArticuloFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpDescripcionFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpCantidadFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpPrincipalFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpExternasFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpTransitoFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpTraerFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpPalletCountFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpPaletsFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpTarimaFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpAlistadoFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpZonasFilter = new TextColumnFilter(FilterExpedicionRows);
        ExpPendienteFilter = new TextColumnFilter(FilterExpedicionRows);
        ComparisonArticuloFilter = new TextColumnFilter(FilterWarehouseComparisonRows);
        ComparisonDescripcionFilter = new TextColumnFilter(FilterWarehouseComparisonRows);
        ComparisonOloFilter = new TextColumnFilter(FilterWarehouseComparisonRows);
        ComparisonServicaFilter = new TextColumnFilter(FilterWarehouseComparisonRows);
        ComparisonCoverageFilter = new TextColumnFilter(FilterWarehouseComparisonRows);
        ComparisonComentariosFilter = new TextColumnFilter(FilterWarehouseComparisonRows);

        ApplySessionToUi();
        RefreshTransitRows();
    }

    public IReadOnlyList<string> CompanyOptions { get; } = CompanyCatalog;

    public IReadOnlyList<StatusFilterOptionViewModel> StatusFilterOptions { get; } =
        new[]
        {
            new StatusFilterOptionViewModel(StatusFilterOption.All, "Todos"),
            new StatusFilterOptionViewModel(StatusFilterOption.Critical, "Rojo"),
            new StatusFilterOptionViewModel(StatusFilterOption.Warning, "Amarillo"),
            new StatusFilterOptionViewModel(StatusFilterOption.Healthy, "Verde"),
        };

    public ObservableCollection<WeekOptionViewModel> AvailableWeeks { get; } = new();

    public ObservableCollection<HorizonOptionViewModel> HorizonOptions { get; } = new();

    private IReadOnlyList<TransferRowViewModel> _transferRows = Array.Empty<TransferRowViewModel>();
    private IReadOnlyList<TransferRowViewModel> _allTransferRows = Array.Empty<TransferRowViewModel>();

    // Populated as a single array assignment (one notification) instead of
    // ObservableCollection.Add per item, so the virtualized ListView rebinds
    // once even with thousands of rows. See BuildTransferView.
    public IReadOnlyList<TransferRowViewModel> TransferRows
    {
        get => _transferRows;
        private set
        {
            if (SetProperty(ref _transferRows, value))
            {
                EnsureSelectedTransferRowIsVisible();
            }
        }
    }

    [ObservableProperty]
    public partial TransferRowViewModel? SelectedTransferRow { get; set; }

    public bool HasSelectedTransferRow =>
        SelectedTransferRow is { PalletOptions.Count: > 0 };

    public Visibility TransferPalletPaneVisibility =>
        ShowTransferView && HasSelectedTransferRow ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string TransferSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedTransferTab { get; set; } = "Todos";

    // Google-Sheets-style per-column filters (Traslados table).
    public TextColumnFilter TrasladosArticuloFilter { get; }

    public TextColumnFilter TrasladosDescripcionFilter { get; }

    public TextColumnFilter TrasladosCoberturaFilter { get; }

    public TextColumnFilter TrasladosPrincipalFilter { get; }

    public TextColumnFilter TrasladosStockExtFilter { get; }

    public TextColumnFilter TrasladosTransitoFilter { get; }

    public TextColumnFilter TrasladosTraerFilter { get; }

    public TextColumnFilter TrasladosPalletCountFilter { get; }

    public TextColumnFilter TrasladosPaletsFilter { get; }

    public TextColumnFilter TrasladosTarimaFilter { get; }

    public TextColumnFilter TrasladosAlistadoFilter { get; }

    public TextColumnFilter TrasladosZonasFilter { get; }

    public TextColumnFilter TrasladosEstadoFilter { get; }

    [ObservableProperty]
    public partial StatusFilterOptionViewModel? SelectedTrasladosEstadoOption { get; set; }

    public bool TrasladosEstadoFilterActive =>
        TrasladosEstadoFilter.IsActive;

    partial void OnSelectedTrasladosEstadoOptionChanged(StatusFilterOptionViewModel? value)
    {
        OnPropertyChanged(nameof(TrasladosEstadoFilterActive));
        FilterTransferRows();
    }

    partial void OnSelectedTransferRowChanged(TransferRowViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedTransferRow));
        OnPropertyChanged(nameof(TransferPalletPaneVisibility));
    }

    // Google-Sheets-style per-column filters (Expediciones table).
    public TextColumnFilter ExpExpedicionFilter { get; }

    public TextColumnFilter ExpArticuloFilter { get; }

    public TextColumnFilter ExpDescripcionFilter { get; }

    public TextColumnFilter ExpCantidadFilter { get; }

    public TextColumnFilter ExpPrincipalFilter { get; }

    public TextColumnFilter ExpExternasFilter { get; }

    public TextColumnFilter ExpTransitoFilter { get; }

    public TextColumnFilter ExpTraerFilter { get; }

    public TextColumnFilter ExpPalletCountFilter { get; }

    public TextColumnFilter ExpPaletsFilter { get; }

    public TextColumnFilter ExpTarimaFilter { get; }

    public TextColumnFilter ExpAlistadoFilter { get; }

    public TextColumnFilter ExpZonasFilter { get; }

    public TextColumnFilter ExpPendienteFilter { get; }

    // Google-Sheets-style per-column filters (Comparativa table).
    public TextColumnFilter ComparisonArticuloFilter { get; }

    public TextColumnFilter ComparisonDescripcionFilter { get; }

    public TextColumnFilter ComparisonOloFilter { get; }

    public TextColumnFilter ComparisonServicaFilter { get; }

    public TextColumnFilter ComparisonCoverageFilter { get; }

    public TextColumnFilter ComparisonComentariosFilter { get; }

    public IReadOnlyList<WarehouseComparisonRowViewModel> WarehouseComparisonRows
    {
        get => _warehouseComparisonRows;
        private set
        {
            if (SetProperty(ref _warehouseComparisonRows, value))
            {
                OnPropertyChanged(nameof(HasWarehouseComparisonRows));
                OnPropertyChanged(nameof(WarehouseComparisonSummaryText));
                ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasWarehouseComparisonRows => WarehouseComparisonRows.Count > 0;

    public bool HasWarehouseComparisonFilters =>
        ComparisonArticuloFilter.IsActive ||
        ComparisonDescripcionFilter.IsActive ||
        ComparisonOloFilter.IsActive ||
        ComparisonServicaFilter.IsActive ||
        ComparisonCoverageFilter.IsActive ||
        ComparisonComentariosFilter.IsActive;

    [ObservableProperty]
    public partial string SelectedWarehouseComparisonView { get; set; } = "Tabla";

    public bool IsWarehouseComparisonTableSelected => SelectedWarehouseComparisonView == "Tabla";

    public bool IsWarehouseComparisonHistogramSelected => SelectedWarehouseComparisonView == "Histograma";

    public Visibility WarehouseComparisonTableVisibility =>
        IsWarehouseComparisonTableSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WarehouseComparisonHistogramVisibility =>
        IsWarehouseComparisonHistogramSelected ? Visibility.Visible : Visibility.Collapsed;

    public bool IsTabTodos => SelectedTransferTab == "Todos";

    public bool IsTabParaTraer => SelectedTransferTab == "ParaTraer";

    public bool IsTabCriticos => SelectedTransferTab == "Criticos";

    public bool IsTabPendientes => SelectedTransferTab == "Pendientes";

    public bool IsTabAltoVolumen => SelectedTransferTab == "AltoVolumen";

    public bool IsTabAlistadoSatelital => SelectedTransferTab == "AlistadoSatelital";

    [ObservableProperty]
    public partial string SelectedExpedicionTab { get; set; } = "Todos";

    [ObservableProperty]
    public partial string ExpedicionSearchText { get; set; } = string.Empty;

    public bool IsExpTabTodos => SelectedExpedicionTab == "Todos";

    public bool IsExpTabParaTraer => SelectedExpedicionTab == "ParaTraer";

    public bool IsExpTabCubiertas => SelectedExpedicionTab == "Cubiertas";

    public bool IsExpTabSinSatelite => SelectedExpedicionTab == "SinSatelite";

    public bool IsExpTabAlistadoSatelital => SelectedExpedicionTab == "AlistadoSatelital";

    partial void OnTransferSearchTextChanged(string value) => FilterTransferRows();

    partial void OnExpedicionSearchTextChanged(string value) => FilterExpedicionRows();

    partial void OnSelectedTransferTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsTabTodos));
        OnPropertyChanged(nameof(IsTabParaTraer));
        OnPropertyChanged(nameof(IsTabCriticos));
        OnPropertyChanged(nameof(IsTabPendientes));
        OnPropertyChanged(nameof(IsTabAltoVolumen));
        OnPropertyChanged(nameof(IsTabAlistadoSatelital));
        FilterTransferRows();
    }

    /// <summary>Sets the Traslados filter tab.</summary>
    public void SetTransferTab(string tab) => SelectedTransferTab = tab;

    partial void OnSelectedExpedicionTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsExpTabTodos));
        OnPropertyChanged(nameof(IsExpTabParaTraer));
        OnPropertyChanged(nameof(IsExpTabCubiertas));
        OnPropertyChanged(nameof(IsExpTabSinSatelite));
        OnPropertyChanged(nameof(IsExpTabAlistadoSatelital));
        FilterExpedicionRows();
    }

    /// <summary>Sets the Expediciones filter tab.</summary>
    public void SetExpedicionTab(string tab) => SelectedExpedicionTab = tab;

    partial void OnSelectedWarehouseComparisonViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsWarehouseComparisonTableSelected));
        OnPropertyChanged(nameof(IsWarehouseComparisonHistogramSelected));
        OnPropertyChanged(nameof(WarehouseComparisonTableVisibility));
        OnPropertyChanged(nameof(WarehouseComparisonHistogramVisibility));
    }

    [RelayCommand]
    private void SetWarehouseComparisonView(string view)
    {
        if (!string.IsNullOrWhiteSpace(view))
        {
            SelectedWarehouseComparisonView = view;
        }
    }

    private void FilterTransferRows()
    {
        if (TrasladosArticuloFilter is null)
        {
            return;
        }

        var showPreparedOnly = SelectedTransferTab == "AlistadoSatelital";
        IEnumerable<TransferRowViewModel> tabQuery = showPreparedOnly
            ? _allTransferRows.Where(row => row.IsSatellitePrepared)
            : _allTransferRows.Where(row => !row.IsSatellitePrepared);

        tabQuery = SelectedTransferTab switch
        {
            "ParaTraer" => tabQuery.Where(row => row.CanOrder && row.TransferSuggestionQuantity > 0m),
            "Criticos" => tabQuery.Where(row => row.Status == CoverageStatus.Critical),
            "Pendientes" => tabQuery.Where(row => row.HasPending),
            "AltoVolumen" => HighVolume(tabQuery),
            _ => tabQuery,
        };

        var tabRows = tabQuery.ToList();
        TransferTabTotal = tabRows.Count;
        RefreshTransferFilterOptions(tabRows);
        IEnumerable<TransferRowViewModel> query = tabRows;

        var search = TransferSearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(row =>
                row.Article.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = query.Where(row =>
            TrasladosArticuloFilter.Matches(row.ArticleDescriptionText) &&
            TrasladosDescripcionFilter.Matches(row.Description) &&
            TrasladosCoberturaFilter.Matches(row.CoverageText) &&
            TrasladosPrincipalFilter.Matches(row.PrincipalInventoryText) &&
            TrasladosStockExtFilter.Matches(row.SatelliteAvailableText) &&
            TrasladosTransitoFilter.Matches(row.PendingTransitText) &&
            TrasladosTraerFilter.Matches(row.TransferSuggestionText) &&
            TrasladosPalletCountFilter.Matches(row.SuggestedPalletCountText) &&
            TrasladosPaletsFilter.Matches(row.SuggestedPalletNumbersText) &&
            TrasladosTarimaFilter.Matches(row.TarimaSizeText) &&
            TrasladosAlistadoFilter.Matches(row.SatellitePreparedText) &&
            TrasladosZonasFilter.Matches(row.SatelliteZonesText) &&
            TrasladosEstadoFilter.Matches(row.StatusLabel));

        var visibleRows = ApplyTransferTextSort(query).ToList();
        TransferRows = visibleRows;
        TransferVisibleTotal = visibleRows.Count;
    }

    private void EnsureSelectedTransferRowIsVisible()
    {
        if (SelectedTransferRow is not { } selected)
        {
            return;
        }

        if (!TransferRows.Contains(selected))
        {
            SelectedTransferRow = null;
        }
    }

    private static bool MatchesStatusOption(CoverageStatus status, StatusFilterOption filter) =>
        filter switch
        {
            StatusFilterOption.Critical => status == CoverageStatus.Critical,
            StatusFilterOption.Warning => status == CoverageStatus.Warning,
            StatusFilterOption.Healthy => status == CoverageStatus.Healthy,
            _ => true,
        };

    private void FilterExpedicionRows()
    {
        if (ExpArticuloFilter is null)
        {
            return;
        }

        var tabRows = _allExpedicionRows
            .Where(row => SelectedExpedicionTab switch
            {
                "AlistadoSatelital" => row.IsSatellitePrepared,
                "ParaTraer" => !row.IsSatellitePrepared && row.TransferQuantity > 0,
                "Cubiertas" => !row.IsSatellitePrepared && row.TransferQuantity <= 0 && !row.HasPending,
                "SinSatelite" => !row.IsSatellitePrepared && row.SatelliteQuantity <= 0 && row.HasPending,
                _ => !row.IsSatellitePrepared,
            })
            .ToList();
        ExpTabTotal = tabRows.Count;
        RefreshExpedicionFilterOptions(tabRows);

        IEnumerable<ExpedicionRowViewModel> query = tabRows;
        var search = ExpedicionSearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(row =>
                row.ExpeditionNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Article.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var visibleRows = query
            .Where(row =>
                ExpExpedicionFilter.Matches(row.ExpeditionNumber) &&
                ExpArticuloFilter.Matches(row.ArticleDescriptionText) &&
                ExpDescripcionFilter.Matches(row.Description) &&
                ExpCantidadFilter.Matches(row.DemandText) &&
                ExpPrincipalFilter.Matches(row.PrincipalInventoryText) &&
                ExpExternasFilter.Matches(row.SatelliteAvailableText) &&
                ExpTransitoFilter.Matches(row.PendingTransitText) &&
                ExpTraerFilter.Matches(row.TransferSuggestionText) &&
                ExpPalletCountFilter.Matches(row.SuggestedPalletCountText) &&
                ExpPaletsFilter.Matches(row.SuggestedPalletNumbersText) &&
                ExpTarimaFilter.Matches(row.TarimaSizeText) &&
                ExpAlistadoFilter.Matches(row.SatellitePreparedText) &&
                ExpZonasFilter.Matches(row.SatelliteZonesText) &&
                ExpPendienteFilter.Matches(row.RemainingShortageText))
            .ToArray();

        ExpedicionRows = ApplyExpedicionTextSort(visibleRows)
            .ToList();
        ExpedicionesLineCount = ExpedicionRows.Count;
    }

    private void FilterWarehouseComparisonRows()
    {
        if (ComparisonArticuloFilter is null)
        {
            return;
        }

        RefreshWarehouseComparisonFilterOptions(_allWarehouseComparisonRows);

        var visibleRows = ApplyWarehouseComparisonTextSort(_allWarehouseComparisonRows
            .Where(row =>
                ComparisonArticuloFilter.Matches(row.ArticleDescriptionText) &&
                ComparisonDescripcionFilter.Matches(row.Description) &&
                ComparisonOloFilter.Matches(row.OloQuantityText) &&
                ComparisonServicaFilter.Matches(row.ServicaQuantityText) &&
                ComparisonCoverageFilter.Matches(row.CoverageText) &&
                ComparisonComentariosFilter.Matches(row.CommentText)))
            .ToArray();

        WarehouseComparisonRows = visibleRows;
        WarehouseComparisonVisibleTotal = visibleRows.Length;
        RefreshWarehouseComparisonCoverageChart(visibleRows);
        OnPropertyChanged(nameof(HasWarehouseComparisonFilters));
        ClearWarehouseComparisonFiltersCommand.NotifyCanExecuteChanged();
        ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
    }

    private void RefreshWarehouseComparisonCoverageChart(IReadOnlyList<WarehouseComparisonRowViewModel> rows)
    {
        var periodUnit = CurrentComparisonGranularity == ForecastGranularity.Monthly ? "mes" : "sem";
        var periodCount = CurrentSession.LastWarehouseComparison?.PeriodCount ?? 0;
        var noSolicitaCount = rows.Count(IsWarehouseComparisonNoSolicita);
        var buckets = BuildWarehouseComparisonHistogramBuckets(rows, periodUnit, periodCount);
        var graphedCount = buckets.Sum(bucket => bucket.ArticleCount);

        if (periodCount <= 0 || graphedCount == 0)
        {
            WarehouseComparisonCoverageSeries = [];
            WarehouseComparisonCoverageXAxes = [];
            WarehouseComparisonCoverageYAxes = [];
            WarehouseComparisonCoverageSummaryText =
                $"{rows.Count:N0} articulos visibles; {noSolicitaCount:N0} no solicita no se grafican.";
            return;
        }

        var labels = buckets.Select(bucket => bucket.Label).ToArray();
        var histogramValues = buckets.Select(bucket => (double)bucket.ArticleCount).ToArray();
        var histogramPercentages = buckets.Select(bucket => (double)bucket.Percentage).ToArray();
        var maxHistogramValue = histogramValues.Length == 0 ? 0d : histogramValues.Max();

        WarehouseComparisonCoverageSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Articulos",
                Values = histogramValues,
                Fill = new SolidColorPaint(SKColor.Parse("#00A88F")),
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsPosition = DataLabelsPosition.Top,
                DataLabelsSize = 12,
                DataLabelsFormatter = point => point.Coordinate.PrimaryValue <= 0d
                    ? string.Empty
                    : point.Coordinate.PrimaryValue.ToString("N0", CultureInfo.CurrentCulture),
                YToolTipLabelFormatter = point =>
                {
                    var index = Math.Clamp(point.Index, 0, histogramValues.Length - 1);
                    var label = index < labels.Length ? labels[index] : $"{point.Index} {periodUnit}";
                    return $"{label}: {histogramValues[index]:N0} articulos ({histogramPercentages[index]:0.#}% del total)";
                },
            },
        ];
        WarehouseComparisonCoverageXAxes = [new Axis { Labels = labels, LabelsRotation = labels.Length > 12 ? 45 : 0 }];
        WarehouseComparisonCoverageYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = maxHistogramValue <= 0d ? 1d : Math.Ceiling(maxHistogramValue * 1.15d),
            },
        ];
        WarehouseComparisonCoverageSummaryText =
            $"{graphedCount:N0} articulos visibles con forecast; conteo exacto por {periodUnit}; {noSolicitaCount:N0} no solicita no se grafican.";
    }

    private static IReadOnlyList<WarehouseComparisonHistogramBucket> BuildWarehouseComparisonHistogramBuckets(
        IReadOnlyList<WarehouseComparisonRowViewModel> rows,
        string periodUnit,
        int periodCount)
    {
        if (periodCount <= 0)
        {
            return Array.Empty<WarehouseComparisonHistogramBucket>();
        }

        var rowsWithForecast = rows
            .Where(row => row.Source.HasFutureForecast &&
                row.CoveragePeriods is not null &&
                !IsWarehouseComparisonNoSolicita(row))
            .ToArray();
        if (rowsWithForecast.Length == 0)
        {
            return Array.Empty<WarehouseComparisonHistogramBucket>();
        }

        var buckets = new List<WarehouseComparisonHistogramBucket>(periodCount + 1);
        for (var period = 0; period <= periodCount; period++)
        {
            var count = rowsWithForecast.Count(row => GetWarehouseComparisonCoverageBucket(row, periodCount) == period);
            var percentage = rowsWithForecast.Length == 0 ? 0m : count / (decimal)rowsWithForecast.Length * 100m;
            buckets.Add(new WarehouseComparisonHistogramBucket($"{period} {periodUnit}", count, percentage));
        }

        return buckets;
    }

    private static bool IsWarehouseComparisonNoSolicita(WarehouseComparisonRowViewModel row) =>
        !row.Source.HasFutureForecast ||
        string.Equals(row.CommentText?.Trim(), "no solicita", StringComparison.OrdinalIgnoreCase);

    private static int GetWarehouseComparisonCoverageBucket(WarehouseComparisonRowViewModel row, int maxPeriod)
    {
        var coverage = row.CoveragePeriods.GetValueOrDefault();
        if (coverage <= 0m)
        {
            return 0;
        }

        var bucket = (int)Math.Floor(coverage);
        return Math.Clamp(bucket, 0, maxPeriod);
    }

    private void RefreshTransferFilterOptions(IReadOnlyList<TransferRowViewModel> rows)
    {
        TrasladosArticuloFilter.SetOptions(rows.Select(row => row.ArticleDescriptionText));
        TrasladosDescripcionFilter.SetOptions(rows.Select(row => row.Description));
        TrasladosCoberturaFilter.SetOptions(rows.Select(row => row.CoverageText));
        TrasladosPrincipalFilter.SetOptions(rows.Select(row => row.PrincipalInventoryText));
        TrasladosStockExtFilter.SetOptions(rows.Select(row => row.SatelliteAvailableText));
        TrasladosTransitoFilter.SetOptions(rows.Select(row => row.PendingTransitText));
        TrasladosTraerFilter.SetOptions(rows.Select(row => row.TransferSuggestionText));
        TrasladosPalletCountFilter.SetOptions(rows.Select(row => row.SuggestedPalletCountText));
        TrasladosPaletsFilter.SetOptions(rows.Select(row => row.SuggestedPalletNumbersText));
        TrasladosTarimaFilter.SetOptions(rows.Select(row => row.TarimaSizeText));
        TrasladosAlistadoFilter.SetOptions(rows.Select(row => row.SatellitePreparedText));
        TrasladosZonasFilter.SetOptions(rows.Select(row => row.SatelliteZonesText));
        TrasladosEstadoFilter.SetOptions(rows.Select(row => row.StatusLabel));
    }

    private void RefreshExpedicionFilterOptions(IReadOnlyList<ExpedicionRowViewModel> rows)
    {
        ExpExpedicionFilter.SetOptions(rows.Select(row => row.ExpeditionNumber));
        ExpArticuloFilter.SetOptions(rows.Select(row => row.ArticleDescriptionText));
        ExpDescripcionFilter.SetOptions(rows.Select(row => row.Description));
        ExpCantidadFilter.SetOptions(rows.Select(row => row.DemandText));
        ExpPrincipalFilter.SetOptions(rows.Select(row => row.PrincipalInventoryText));
        ExpExternasFilter.SetOptions(rows.Select(row => row.SatelliteAvailableText));
        ExpTransitoFilter.SetOptions(rows.Select(row => row.PendingTransitText));
        ExpTraerFilter.SetOptions(rows.Select(row => row.TransferSuggestionText));
        ExpPalletCountFilter.SetOptions(rows.Select(row => row.SuggestedPalletCountText));
        ExpPaletsFilter.SetOptions(rows.Select(row => row.SuggestedPalletNumbersText));
        ExpTarimaFilter.SetOptions(rows.Select(row => row.TarimaSizeText));
        ExpAlistadoFilter.SetOptions(rows.Select(row => row.SatellitePreparedText));
        ExpZonasFilter.SetOptions(rows.Select(row => row.SatelliteZonesText));
        ExpPendienteFilter.SetOptions(rows.Select(row => row.RemainingShortageText));
    }

    private void RefreshWarehouseComparisonFilterOptions(IReadOnlyList<WarehouseComparisonRowViewModel> rows)
    {
        ComparisonArticuloFilter.SetOptions(rows.Select(row => row.ArticleDescriptionText));
        ComparisonDescripcionFilter.SetOptions(rows.Select(row => row.Description));
        ComparisonOloFilter.SetOptions(rows.Select(row => row.OloQuantityText));
        ComparisonServicaFilter.SetOptions(rows.Select(row => row.ServicaQuantityText));
        ComparisonCoverageFilter.SetOptions(rows.Select(row => row.CoverageText));
        ComparisonComentariosFilter.SetOptions(rows.Select(row => row.CommentText));
    }

    [RelayCommand]
    private void SortTextColumnAscending(string? request) => SortTextColumn(request, ascending: true);

    [RelayCommand]
    private void SortTextColumnDescending(string? request) => SortTextColumn(request, ascending: false);

    private void SortTextColumn(string? request, bool ascending)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return;
        }

        var parts = request.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return;
        }

        var state = new TextSortState(parts[1], ascending);
        switch (parts[0])
        {
            case "Table":
                _tableTextSort = state;
                ApplyFilters();
                break;
            case "Transfer":
                _transferTextSort = state;
                FilterTransferRows();
                break;
            case "Expedicion":
                _expedicionTextSort = state;
                FilterExpedicionRows();
                break;
            case "Comparison":
                _warehouseComparisonTextSort = state;
                FilterWarehouseComparisonRows();
                break;
        }
    }

    private IEnumerable<ArticleResultRowViewModel> ApplyTableTextSort(IEnumerable<ArticleResultRowViewModel> rows)
    {
        if (_tableTextSort is not { } sort)
        {
            return rows;
        }

        switch (sort.Column)
        {
            case "Forecast":
                return ApplyComparableSort(rows, row => row.ForecastQuantity, sort.Ascending);
            case "Principal":
                return ApplyComparableSort(rows, row => row.PrincipalInventoryQuantity, sort.Ascending);
            case "Difference":
                return ApplyComparableSort(rows, row => row.Difference, sort.Ascending);
            case "Satellite":
                return ApplyComparableSort(rows, row => row.SatelliteInventoryQuantity, sort.Ascending);
        }

        Func<ArticleResultRowViewModel, string> selector = sort.Column switch
        {
            "Article" => row => row.ArticleDescriptionText,
            "Status" => row => row.StatusLabel,
            _ => row => row.ArticleDescriptionText,
        };

        return ApplyTextSort(rows, selector, sort.Ascending);
    }

    private IEnumerable<TransferRowViewModel> ApplyTransferTextSort(IEnumerable<TransferRowViewModel> rows)
    {
        if (_transferTextSort is not { } sort)
        {
            return rows;
        }

        switch (sort.Column)
        {
            case "Coverage":
                return ApplyNullableDecimalSort(rows, row => row.CoverageWeeksValue, sort.Ascending);
            case "Principal":
                return ApplyComparableSort(rows, row => row.PrincipalInventoryQuantity, sort.Ascending);
            case "StockExt":
                return ApplyComparableSort(rows, row => row.SatelliteAvailableQuantity, sort.Ascending);
            case "Transito":
                return ApplyComparableSort(rows, row => row.PendingTransitQuantity, sort.Ascending);
            case "Traer":
                return ApplyComparableSort(rows, row => row.TransferSuggestionQuantity, sort.Ascending);
            case "PalletCount":
                return ApplyComparableSort(rows, row => row.SuggestedPalletCount, sort.Ascending);
        }

        Func<TransferRowViewModel, string> selector = sort.Column switch
        {
            "Article" => row => row.ArticleDescriptionText,
            "Description" => row => row.Description,
            "Pallets" => row => row.SuggestedPalletNumbersText,
            "Tarima" => row => row.TarimaSizeText,
            "Alistado" => row => row.SatellitePreparedText,
            "Zonas" => row => row.SatelliteZonesText,
            "Status" => row => row.StatusLabel,
            _ => row => row.ArticleDescriptionText,
        };

        return ApplyTextSort(rows, selector, sort.Ascending);
    }

    private IEnumerable<ExpedicionRowViewModel> ApplyExpedicionTextSort(IEnumerable<ExpedicionRowViewModel> rows)
    {
        if (_expedicionTextSort is not { } sort)
        {
            return rows;
        }

        switch (sort.Column)
        {
            case "Cantidad":
                return ApplyComparableSort(rows, row => row.DemandQuantity, sort.Ascending);
            case "Principal":
                return ApplyComparableSort(rows, row => row.PrincipalQuantity, sort.Ascending);
            case "Externas":
                return ApplyComparableSort(rows, row => row.SatelliteQuantity, sort.Ascending);
            case "Transito":
                return ApplyComparableSort(rows, row => row.PendingTransitQuantity, sort.Ascending);
            case "Traer":
                return ApplyComparableSort(rows, row => row.TransferQuantity, sort.Ascending);
            case "PalletCount":
                return ApplyComparableSort(rows, row => row.SuggestedPalletCount, sort.Ascending);
            case "Pendiente":
                return ApplyComparableSort(rows, row => row.RemainingShortageQuantity, sort.Ascending);
        }

        Func<ExpedicionRowViewModel, string> selector = sort.Column switch
        {
            "Expedicion" => row => row.ExpeditionNumber,
            "Article" => row => row.ArticleDescriptionText,
            "Description" => row => row.Description,
            "Pallets" => row => row.SuggestedPalletNumbersText,
            "Tarima" => row => row.TarimaSizeText,
            "Alistado" => row => row.SatellitePreparedText,
            "Zonas" => row => row.SatelliteZonesText,
            _ => row => row.ArticleDescriptionText,
        };

        return ApplyTextSort(rows, selector, sort.Ascending);
    }

    private IEnumerable<WarehouseComparisonRowViewModel> ApplyWarehouseComparisonTextSort(
        IEnumerable<WarehouseComparisonRowViewModel> rows)
    {
        if (_warehouseComparisonTextSort is not { } sort)
        {
            return rows;
        }

        switch (sort.Column)
        {
            case "Olo":
                return ApplyComparableSort(rows, row => row.OloQuantity, sort.Ascending);
            case "Servica":
                return ApplyComparableSort(rows, row => row.ServicaQuantity, sort.Ascending);
            case "Coverage":
                return ApplyNullableDecimalSort(rows, row => row.CoveragePeriods, sort.Ascending);
        }

        Func<WarehouseComparisonRowViewModel, string> selector = sort.Column switch
        {
            "Article" => row => row.ArticleDescriptionText,
            "Description" => row => row.Description,
            "Comments" => row => row.CommentText,
            _ => row => row.ArticleDescriptionText,
        };

        return ApplyTextSort(rows, selector, sort.Ascending);
    }

    private static IEnumerable<T> ApplyTextSort<T>(
        IEnumerable<T> rows,
        Func<T, string> selector,
        bool ascending) =>
        ascending
            ? rows.OrderBy(selector, StringComparer.OrdinalIgnoreCase)
            : rows.OrderByDescending(selector, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<T> ApplyComparableSort<T, TKey>(
        IEnumerable<T> rows,
        Func<T, TKey> selector,
        bool ascending) =>
        ascending
            ? rows.OrderBy(selector)
            : rows.OrderByDescending(selector);

    private static IEnumerable<T> ApplyNullableDecimalSort<T>(
        IEnumerable<T> rows,
        Func<T, decimal?> selector,
        bool ascending) =>
        ascending
            ? rows.OrderBy(row => selector(row).HasValue ? 0 : 1)
                .ThenBy(row => selector(row).GetValueOrDefault())
            : rows.OrderBy(row => selector(row).HasValue ? 0 : 1)
                .ThenByDescending(row => selector(row).GetValueOrDefault());

    private void RefreshTransitRows()
    {
        var rows = _transferTransitStore
            .GetPending(SelectedCompany)
            .Select(item => new TransitRowViewModel(item, OnTransitRowSelectionChanged))
            .ToArray();

        _allTransitRows = rows;
        TransitPendingCount = rows.Length;
        TransitPendingQuantity = rows.Sum(row => row.Quantity);
        FilterTransitRows();
    }

    private void FilterTransitRows()
    {
        var search = TransitSearchText?.Trim();
        IEnumerable<TransitRowViewModel> query = _allTransitRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(row =>
                row.Article.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Pallet.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.StorageZone.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Origin.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        TransitRows = query.ToArray();
        TransitVisibleCount = TransitRows.Count;
        TransitSelectedCount = _allTransitRows.Count(row => row.IsSelected);
    }

    private void OnTransitRowSelectionChanged(TransitRowViewModel row)
    {
        TransitSelectedCount = _allTransitRows.Count(item => item.IsSelected);
        ConfirmSelectedTransitCommand.NotifyCanExecuteChanged();
    }

    private static IEnumerable<TransferRowViewModel> HighVolume(IEnumerable<TransferRowViewModel> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return list;
        }

        var average = list.Average(row => (double)row.TransferSuggestionQuantity);
        return list.Where(row => (double)row.TransferSuggestionQuantity >= average);
    }

    [ObservableProperty]
    public partial bool ShowTransferView { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTableView { get; set; }

    [ObservableProperty]
    public partial bool ShowExpedicionesView { get; set; }

    [ObservableProperty]
    public partial bool ShowTransitView { get; set; }

    [ObservableProperty]
    public partial bool ShowWarehouseComparisonView { get; set; }

    [ObservableProperty]
    public partial bool ShowSimulacionView { get; set; }

    [ObservableProperty]
    public partial int WarehouseComparisonTotal { get; set; }

    [ObservableProperty]
    public partial int WarehouseComparisonVisibleTotal { get; set; }

    [ObservableProperty]
    public partial int WarehouseComparisonNoForecastTotal { get; set; }

    public string WarehouseComparisonSummaryText =>
        $"{WarehouseComparisonVisibleTotal:N0} de {WarehouseComparisonTotal:N0} articulos con SERVICA | {WarehouseComparisonNoForecastTotal:N0} no solicita";

    public string WarehouseComparisonCaptionText
    {
        get
        {
            var granularity = CurrentComparisonGranularity;
            var periodName = granularity == ForecastGranularity.Monthly ? "mes" : "semana";
            var periodPlural = granularity == ForecastGranularity.Monthly ? "meses" : "semanas";

            if (CurrentSession.LastWarehouseComparison is not { } comparison)
            {
                return $"Compara inventario valido de OLO contra SERVICA, mostrando solo articulos con Inv SERVICA mayor a 0, y calcula cobertura de OLO desde el {periodName} elegido del forecast en adelante; si el articulo pide al menos 1, las {periodPlural} en 0 tambien cuentan como cubiertas.";
            }

            return $"Compara inventario valido de OLO contra SERVICA, mostrando solo articulos con Inv SERVICA mayor a 0, y calcula cobertura de OLO desde el {periodName} elegido {FormatComparisonPeriod(comparison.StartPeriod, granularity)} en adelante ({comparison.PeriodCount:N0} {periodPlural} del forecast; repetidos se suman y las {periodPlural} en 0 cuentan si el articulo pide al menos 1).";
        }
    }

    [ObservableProperty]
    public partial int FunnelEvaluated { get; set; }

    [ObservableProperty]
    public partial int FunnelCovered { get; set; }

    [ObservableProperty]
    public partial int FunnelToTransfer { get; set; }

    [ObservableProperty]
    public partial int FunnelWithoutExternal { get; set; }

    public string FunnelCoveredPercentText =>
        FunnelEvaluated > 0 ? $"{(int)System.Math.Round(FunnelCovered * 100.0 / FunnelEvaluated)}% del total" : "0% del total";

    public double FunnelToTransferProgress =>
        FunnelEvaluated > 0 ? FunnelToTransfer * 100.0 / FunnelEvaluated : 0;

    public string FunnelWithoutExternalSubtext =>
        FunnelWithoutExternal > 0 ? "Requiere atencion" : "Sin faltantes";

    partial void OnFunnelEvaluatedChanged(int value)
    {
        OnPropertyChanged(nameof(FunnelCoveredPercentText));
        OnPropertyChanged(nameof(FunnelToTransferProgress));
    }

    partial void OnFunnelCoveredChanged(int value) => OnPropertyChanged(nameof(FunnelCoveredPercentText));

    partial void OnFunnelToTransferChanged(int value) => OnPropertyChanged(nameof(FunnelToTransferProgress));

    partial void OnFunnelWithoutExternalChanged(int value) => OnPropertyChanged(nameof(FunnelWithoutExternalSubtext));

    partial void OnWarehouseComparisonTotalChanged(int value) => OnPropertyChanged(nameof(WarehouseComparisonSummaryText));

    partial void OnWarehouseComparisonVisibleTotalChanged(int value) => OnPropertyChanged(nameof(WarehouseComparisonSummaryText));

    partial void OnWarehouseComparisonNoForecastTotalChanged(int value) => OnPropertyChanged(nameof(WarehouseComparisonSummaryText));

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportRequisitionCommand))]
    public partial int TransferTotal { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearOrdersCommand))]
    public partial int OrderedCount { get; set; }

    [ObservableProperty]
    public partial int TransferPreparedTotal { get; set; }

    [ObservableProperty]
    public partial int TransferVisibleTotal { get; set; }

    [ObservableProperty]
    public partial int TransferTabTotal { get; set; }

    public Visibility TransferViewVisibility => ShowTransferView ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TableViewVisibility => ShowTableView ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ExpedicionesViewVisibility => ShowExpedicionesView ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TransitViewVisibility => ShowTransitView ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WarehouseComparisonViewVisibility => ShowWarehouseComparisonView ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SimulacionViewVisibility => ShowSimulacionView ? Visibility.Visible : Visibility.Collapsed;

    // The load bubble (Inventario/Forecast/Semana/Horizonte) applies to
    // Traslados/Comparativa; Expediciones, Transito and Simulacion don't use it.
    public Visibility LoadPanelVisibility =>
        ShowExpedicionesView || ShowTransitView || ShowSimulacionView ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AnalysisPeriodControlsVisibility =>
        Visibility.Visible;

    public Visibility HorizonControlsVisibility =>
        ShowWarehouseComparisonView ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NonComparisonLoadControlsVisibility =>
        ShowWarehouseComparisonView ? Visibility.Collapsed : Visibility.Visible;

    public GridLength LoadHorizonColumnWidth =>
        ShowWarehouseComparisonView ? new GridLength(0) : new GridLength(300);

    /// <summary>Selects the active section (driven by the NavigationView).</summary>
    public void SelectSection(string? tag)
    {
        var section = string.Equals(tag, "Tabla", StringComparison.OrdinalIgnoreCase) ? "Traslados" : tag;

        ShowTransferView = section == "Traslados";
        ShowTableView = false;
        ShowExpedicionesView = section == "Expediciones";
        ShowTransitView = section == "Transito";
        ShowWarehouseComparisonView = section == "Comparativa";
        ShowSimulacionView = section == "Simulacion";

        UpdateVisibleLoadFileNames();
        ApplyWeeksFromSession();
        RestoreActiveSectionPresentation();
        AnalyzeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();

        if (ShowSimulacionView)
        {
            EjecutarSimulacion();
        }

        if (ShowTransferView && CurrentSession.LastAnalysis is null)
        {
            _ = TryAutoAnalyzeAsync();
        }
        else if (ShowWarehouseComparisonView && CurrentSession.LastWarehouseComparison is null)
        {
            _ = TryAutoAnalyzeAsync();
        }
    }

    private void RestoreActiveSectionPresentation()
    {
        var session = CurrentSession;
        if (ShowTransferView)
        {
            ApplyAnalysis(session.LastAnalysis);
        }
        else if (ShowExpedicionesView)
        {
            BuildExpedicionesView(session.LastExpedicionesAnalysis);
        }
        else if (ShowWarehouseComparisonView)
        {
            BuildWarehouseComparisonView(session.LastWarehouseComparison);
        }
    }

    public string SectionTitle =>
        ShowExpedicionesView ? "Expediciones" :
        ShowTransitView ? "Transito" :
        ShowWarehouseComparisonView ? "Comparativa e histograma" :
        ShowSimulacionView ? "Simulacion Logistica" :
        "Traslados";

    public string SectionIconGlyph =>
        ShowExpedicionesView ? "\uE558" :
        ShowTransitView ? "\uE8B5" :
        ShowWarehouseComparisonView ? "\uE26B" :
        ShowSimulacionView ? "\uEA4B" :
        "\uE8D4";

    [ObservableProperty]
    public partial ISeries[] WarehouseComparisonCoverageSeries { get; set; } = [];

    [ObservableProperty]
    public partial IEnumerable<ICartesianAxis> WarehouseComparisonCoverageXAxes { get; set; } = [];

    [ObservableProperty]
    public partial IEnumerable<ICartesianAxis> WarehouseComparisonCoverageYAxes { get; set; } = [];

    [ObservableProperty]
    public partial string WarehouseComparisonCoverageSummaryText { get; set; } =
        "Carga inventario y forecast para ver la cobertura por semana.";

    // ---- Simulacion Logistica ----
    [ObservableProperty]
    public partial double HorizonMonths { get; set; } = 3;

    [ObservableProperty]
    public partial double SafetyBufferPercent { get; set; } = 15;

    [ObservableProperty]
    public partial ISeries[] SimDemandSeries { get; set; } = [];

    [ObservableProperty]
    public partial ISeries[] SimLoadSeries { get; set; } = [];

    [ObservableProperty]
    public partial IEnumerable<ICartesianAxis> SimDemandXAxes { get; set; } = [];

    [ObservableProperty]
    public partial IEnumerable<ICartesianAxis> SimLoadXAxes { get; set; } = [];

    [ObservableProperty]
    public partial string SimulacionStatusText { get; set; } =
        "Carga inventario y forecast (en Traslados) de EPA o Cofersa y ejecuta la simulacion.";

    public string HorizonLabel => $"{(int)HorizonMonths} mes(es)";

    public string SafetyBufferLabel => $"{(int)SafetyBufferPercent}%";

    partial void OnHorizonMonthsChanged(double value) => OnPropertyChanged(nameof(HorizonLabel));

    partial void OnSafetyBufferPercentChanged(double value) => OnPropertyChanged(nameof(SafetyBufferLabel));

    [RelayCommand]
    private void EjecutarSimulacion()
    {
        var epa = BuildScenarioData("EPA");
        var cofersa = BuildScenarioData("Cofersa");
        if (epa is null && cofersa is null)
        {
            SimDemandSeries = [];
            SimLoadSeries = [];
            SimDemandXAxes = [];
            SimLoadXAxes = [];
            SimulacionStatusText = "Carga inventario y forecast (en Traslados) de EPA o Cofersa para simular.";
            return;
        }

        var result = _scenarioSimulator.Simulate(epa, cofersa, (int)HorizonMonths, (decimal)SafetyBufferPercent);
        var labels = result.Periods.Select(period => period.Label).ToArray();

        var demand = new List<ISeries>();
        if (result.HasEpa)
        {
            demand.Add(new LineSeries<double>
            {
                Name = "Pronostico EPA",
                Values = result.Periods.Select(period => (double)period.EpaDemand).ToArray(),
                Fill = null,
                GeometrySize = 9,
                Stroke = new SolidColorPaint(SKColor.Parse("#00A88F")) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#00A88F")) { StrokeThickness = 3 },
            });
        }

        if (result.HasCofersa)
        {
            demand.Add(new LineSeries<double>
            {
                Name = "Pronostico Cofersa",
                Values = result.Periods.Select(period => (double)period.CofersaDemand).ToArray(),
                Fill = null,
                GeometrySize = 9,
                Stroke = new SolidColorPaint(SKColor.Parse("#00A88F")) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#00A88F")) { StrokeThickness = 2 },
            });
        }

        SimDemandSeries = [.. demand];
        SimDemandXAxes = [new Axis { Labels = labels }];
        SimLoadSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Carga de traslado",
                Values = result.Periods.Select(period => (double)period.TransferLoad).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#00A88F")),
            },
        ];
        SimLoadXAxes = [new Axis { Labels = labels }];
        SimulacionStatusText =
            $"{result.Periods.Count} periodos · horizonte {(int)HorizonMonths} mes(es) · buffer {(int)SafetyBufferPercent}%";
    }

    [RelayCommand]
    private void RestablecerSimulacion()
    {
        HorizonMonths = 3;
        SafetyBufferPercent = 15;
        EjecutarSimulacion();
    }

    private ScenarioCompanyData? BuildScenarioData(string company)
    {
        var session = _sessions[company];
        if (session.InventoryWorkbook is null || session.ForecastWorkbook is null)
        {
            return null;
        }

        var start = session.SelectedWeek ?? session.ForecastWorkbook.AvailableWeeks.FirstOrDefault();
        return new ScenarioCompanyData(
            session.ForecastWorkbook.Entries,
            session.InventoryWorkbook.Positions,
            _satelliteZoneStore.GetZonesFor(company),
            _inventoryExcludedZoneStore.GetZonesFor(company),
            start);
    }

    // Collapsed automatically after a successful analysis to give room to results;
    // reopened with the Expander chevron.
    [ObservableProperty]
    public partial bool IsLoadPanelExpanded { get; set; } = true;

    public string LoadSummaryText =>
        ShowWarehouseComparisonView
            ? CurrentSession.ComparisonInventoryWorkbook is null && CurrentSession.ComparisonForecastWorkbook is null
                ? "Cargar inventario y forecast de Comparativa"
                : $"{InventoryFileName}  ·  {ForecastFileName}  ·  desde {SelectedWeek?.Label ?? "sin periodo"}"
            : CurrentSession.InventoryWorkbook is null && CurrentSession.ForecastWorkbook is null
                ? "Cargar inventario y forecast de Traslados"
                : $"{InventoryFileName}  ·  {ForecastFileName}  ·  {SelectedHorizon?.Label ?? "4 semanas"}";

    public string TransferSummaryText
    {
        get
        {
            var visibleText = TransferVisibleTotal == TransferTabTotal
                ? $"{TransferVisibleTotal:N0} visibles"
                : $"{TransferVisibleTotal:N0} de {TransferTabTotal:N0} visibles";
            var orderText = $"{OrderedCount:N0} de {TransferTotal:N0} seleccionados";
            return TransferPreparedTotal > 0
                ? $"{visibleText} | {orderText} | {TransferPreparedTotal:N0} alistado sat."
                : $"{visibleText} | {orderText}";
        }
    }

    public bool HasTransferRows => TransferTotal > 0;

    partial void OnShowTransferViewChanged(bool value)
    {
        OnPropertyChanged(nameof(TransferViewVisibility));
        OnPropertyChanged(nameof(TransferPalletPaneVisibility));
        OnPropertyChanged(nameof(LoadPanelVisibility));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIconGlyph));
    }

    partial void OnShowTableViewChanged(bool value)
    {
        OnPropertyChanged(nameof(TableViewVisibility));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIconGlyph));
    }

    partial void OnShowExpedicionesViewChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpedicionesViewVisibility));
        OnPropertyChanged(nameof(LoadPanelVisibility));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIconGlyph));
    }

    partial void OnShowTransitViewChanged(bool value)
    {
        OnPropertyChanged(nameof(TransitViewVisibility));
        OnPropertyChanged(nameof(LoadPanelVisibility));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIconGlyph));
    }

    partial void OnShowWarehouseComparisonViewChanged(bool value)
    {
        OnPropertyChanged(nameof(WarehouseComparisonViewVisibility));
        OnPropertyChanged(nameof(LoadPanelVisibility));
        OnPropertyChanged(nameof(AnalysisPeriodControlsVisibility));
        OnPropertyChanged(nameof(HorizonControlsVisibility));
        OnPropertyChanged(nameof(NonComparisonLoadControlsVisibility));
        OnPropertyChanged(nameof(LoadHorizonColumnWidth));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIconGlyph));
        OnPropertyChanged(nameof(LoadSummaryText));
        OnPropertyChanged(nameof(SelectedWeekLabel));
        OnPropertyChanged(nameof(PeriodSelectorHeader));
    }

    partial void OnShowSimulacionViewChanged(bool value)
    {
        OnPropertyChanged(nameof(SimulacionViewVisibility));
        OnPropertyChanged(nameof(LoadPanelVisibility));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionIconGlyph));
    }

    partial void OnTransferTotalChanged(int value)
    {
        OnPropertyChanged(nameof(TransferSummaryText));
        OnPropertyChanged(nameof(HasTransferRows));
    }

    partial void OnOrderedCountChanged(int value) => OnPropertyChanged(nameof(TransferSummaryText));

    partial void OnTransferPreparedTotalChanged(int value) => OnPropertyChanged(nameof(TransferSummaryText));

    partial void OnTransferVisibleTotalChanged(int value) => OnPropertyChanged(nameof(TransferSummaryText));

    partial void OnTransferTabTotalChanged(int value) => OnPropertyChanged(nameof(TransferSummaryText));

    partial void OnInventoryFileNameChanged(string value) => OnPropertyChanged(nameof(LoadSummaryText));

    partial void OnForecastFileNameChanged(string value) => OnPropertyChanged(nameof(LoadSummaryText));

    [ObservableProperty]
    public partial string ExpedicionesFileName { get; set; } = "Expediciones pendiente";

    [ObservableProperty]
    public partial string ExpedicionesInventoryFileName { get; set; } = "Inventario de expediciones pendiente";

    [ObservableProperty]
    public partial int ExpEvaluated { get; set; }

    [ObservableProperty]
    public partial int ExpCovered { get; set; }

    [ObservableProperty]
    public partial int ExpToTransfer { get; set; }

    [ObservableProperty]
    public partial int ExpWithoutExternal { get; set; }

    [ObservableProperty]
    public partial int ExpedicionesLineCount { get; set; }

    [ObservableProperty]
    public partial int ExpPreparedTotal { get; set; }

    [ObservableProperty]
    public partial int ExpTabTotal { get; set; }

    public string ExpedicionesSummaryText
    {
        get
        {
            var visibleText = ExpedicionesLineCount == ExpTabTotal
                ? $"{ExpedicionesLineCount:N0} lineas"
                : $"{ExpedicionesLineCount:N0} de {ExpTabTotal:N0} lineas";
            return ExpPreparedTotal > 0
                ? $"{visibleText} | {ExpPreparedTotal:N0} alistado sat."
                : visibleText;
        }
    }

    public bool HasExpediciones => ExpedicionesLineCount > 0;

    partial void OnExpedicionesLineCountChanged(int value)
    {
        OnPropertyChanged(nameof(ExpedicionesSummaryText));
        OnPropertyChanged(nameof(HasExpediciones));
    }

    partial void OnExpPreparedTotalChanged(int value) => OnPropertyChanged(nameof(ExpedicionesSummaryText));

    partial void OnExpTabTotalChanged(int value) => OnPropertyChanged(nameof(ExpedicionesSummaryText));

    partial void OnTransitSearchTextChanged(string value) => FilterTransitRows();

    partial void OnTransitPendingCountChanged(int value)
    {
        OnPropertyChanged(nameof(TransitSummaryText));
        OnPropertyChanged(nameof(HasTransitRows));
    }

    partial void OnTransitVisibleCountChanged(int value)
    {
        OnPropertyChanged(nameof(TransitSummaryText));
        OnPropertyChanged(nameof(HasTransitRows));
        ConfirmVisibleTransitCommand.NotifyCanExecuteChanged();
    }

    partial void OnTransitSelectedCountChanged(int value)
    {
        OnPropertyChanged(nameof(TransitSummaryText));
        ConfirmSelectedTransitCommand.NotifyCanExecuteChanged();
    }

    partial void OnTransitPendingQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(TransitPendingQuantityText));
        OnPropertyChanged(nameof(TransitSummaryText));
    }

    private IReadOnlyList<ExpedicionRowViewModel> _expedicionRows = Array.Empty<ExpedicionRowViewModel>();
    private IReadOnlyList<ExpedicionRowViewModel> _allExpedicionRows = Array.Empty<ExpedicionRowViewModel>();

    public IReadOnlyList<ExpedicionRowViewModel> ExpedicionRows
    {
        get => _expedicionRows;
        private set => SetProperty(ref _expedicionRows, value);
    }

    private IReadOnlyList<TransitRowViewModel> _transitRows = Array.Empty<TransitRowViewModel>();
    private IReadOnlyList<TransitRowViewModel> _allTransitRows = Array.Empty<TransitRowViewModel>();

    public IReadOnlyList<TransitRowViewModel> TransitRows
    {
        get => _transitRows;
        private set => SetProperty(ref _transitRows, value);
    }

    [ObservableProperty]
    public partial string TransitSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TransitPendingCount { get; set; }

    [ObservableProperty]
    public partial int TransitVisibleCount { get; set; }

    [ObservableProperty]
    public partial int TransitSelectedCount { get; set; }

    [ObservableProperty]
    public partial decimal TransitPendingQuantity { get; set; }

    public string TransitPendingQuantityText => TransitPendingQuantity.ToString("N0");

    public string TransitSummaryText =>
        $"{TransitVisibleCount:N0} de {TransitPendingCount:N0} pallets | {TransitPendingQuantityText} unidades pendientes | {TransitSelectedCount:N0} seleccionados";

    public bool HasTransitRows => TransitRows.Count > 0;

    // Per-column filters for the "Tabla completa" view.
    public TextColumnFilter TableArticuloFilter { get; }

    public TextColumnFilter TableForecastFilter { get; }

    public TextColumnFilter TablePrincipalFilter { get; }

    public TextColumnFilter TableDifferenceFilter { get; }

    public TextColumnFilter TableStatusFilter { get; }

    public TextColumnFilter TableSatelliteFilter { get; }

    [ObservableProperty]
    public partial string ForecastMinText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ForecastMaxText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PrincipalMinText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PrincipalMaxText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DiffMinText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DiffMaxText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SatMinText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SatMaxText { get; set; } = string.Empty;

    public bool ArticleFilterActive =>
        !string.IsNullOrWhiteSpace(SearchText) || TableArticuloFilter.IsActive;

    public bool StatusFilterActive =>
        GeneralStatusFilter != StatusFilterOption.All || TableStatusFilter.IsActive;

    public bool ForecastFilterActive =>
        HasText(ForecastMinText) || HasText(ForecastMaxText) || TableForecastFilter.IsActive;

    public bool PrincipalFilterActive =>
        HasText(PrincipalMinText) || HasText(PrincipalMaxText) || TablePrincipalFilter.IsActive;

    public bool DiffFilterActive =>
        HasText(DiffMinText) || HasText(DiffMaxText) || TableDifferenceFilter.IsActive;

    public bool SatFilterActive =>
        HasText(SatMinText) || HasText(SatMaxText) || TableSatelliteFilter.IsActive;

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    partial void OnForecastMinTextChanged(string value) => OnColumnRangeChanged(nameof(ForecastFilterActive));

    partial void OnForecastMaxTextChanged(string value) => OnColumnRangeChanged(nameof(ForecastFilterActive));

    partial void OnPrincipalMinTextChanged(string value) => OnColumnRangeChanged(nameof(PrincipalFilterActive));

    partial void OnPrincipalMaxTextChanged(string value) => OnColumnRangeChanged(nameof(PrincipalFilterActive));

    partial void OnDiffMinTextChanged(string value) => OnColumnRangeChanged(nameof(DiffFilterActive));

    partial void OnDiffMaxTextChanged(string value) => OnColumnRangeChanged(nameof(DiffFilterActive));

    partial void OnSatMinTextChanged(string value) => OnColumnRangeChanged(nameof(SatFilterActive));

    partial void OnSatMaxTextChanged(string value) => OnColumnRangeChanged(nameof(SatFilterActive));

    private void OnColumnRangeChanged(string activeProperty)
    {
        OnPropertyChanged(activeProperty);
        OnPropertyChanged(nameof(HasActiveFilters));
        if (_isRestoringState)
        {
            return;
        }

        ApplyFilters();
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    private void FilterTableRows()
    {
        OnPropertyChanged(nameof(ArticleFilterActive));
        OnPropertyChanged(nameof(ForecastFilterActive));
        OnPropertyChanged(nameof(PrincipalFilterActive));
        OnPropertyChanged(nameof(DiffFilterActive));
        OnPropertyChanged(nameof(StatusFilterActive));
        OnPropertyChanged(nameof(SatFilterActive));
        OnPropertyChanged(nameof(HasActiveFilters));

        if (_isRestoringState)
        {
            return;
        }

        ApplyFilters();
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<ArticleResultRowViewModel> FilteredResults
    {
        get => _filteredResults;
        private set
        {
            if (SetProperty(ref _filteredResults, value))
            {
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(Results));
            }
        }
    }

    public IReadOnlyList<ArticleResultRowViewModel> Results => FilteredResults;

    public IReadOnlyList<ZoneDetailRowViewModel> ZoneDetails
    {
        get => _zoneDetails;
        private set => SetProperty(ref _zoneDetails, value);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadInventoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadForecastCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadExpedicionesInventoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadExpedicionesCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreparedArticlesCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadTarimaSizesCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ManageSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportRequisitionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportWarehouseComparisonCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearWarehouseComparisonFiltersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearOrdersCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendSelectedToTransitCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmSelectedTransitCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmVisibleTransitCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string SelectedCompany { get; set; } = "EPA";

    public bool IsCompanyEpa => string.Equals(SelectedCompany, "EPA", StringComparison.OrdinalIgnoreCase);

    public bool IsCompanyCofersa => string.Equals(SelectedCompany, "Cofersa", StringComparison.OrdinalIgnoreCase);

    /// <summary>Selects the active company (driven by the EPA/Cofersa tab switcher).</summary>
    public void SelectCompany(string company)
    {
        if (!string.IsNullOrWhiteSpace(company))
        {
            SelectedCompany = company;
        }
    }

    // The signed-in Windows user, shown in the sidebar profile (resolved once).
    private static readonly (string Name, string Subtitle, string Initials) WindowsUser = ResolveWindowsUser();

    public string UserDisplayName => WindowsUser.Name;

    public string UserSubtitle => WindowsUser.Subtitle;

    public string UserInitials => WindowsUser.Initials;

    private static (string Name, string Subtitle, string Initials) ResolveWindowsUser()
    {
        var account = string.IsNullOrWhiteSpace(Environment.UserName) ? "Usuario" : Environment.UserName;
        var display = TryGetWindowsDisplayName();
        if (string.IsNullOrWhiteSpace(display))
        {
            display = account;
        }

        var subtitle = string.Equals(display, account, StringComparison.OrdinalIgnoreCase)
            ? Environment.UserDomainName
            : account;

        return (display!, subtitle, BuildInitials(display!, account));
    }

    private static string BuildInitials(string display, string account)
    {
        var parts = display.Split([' ', '.', ',', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));
        }

        var basis = string.IsNullOrWhiteSpace(account) ? display : account;
        return basis.Length >= 2 ? basis[..2].ToUpperInvariant() : basis.ToUpperInvariant();
    }

    [System.Runtime.InteropServices.DllImport("secur32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern byte GetUserNameExW(int nameFormat, System.Text.StringBuilder lpNameBuffer, ref uint lpnSize);

    private static string? TryGetWindowsDisplayName()
    {
        try
        {
            uint size = 256;
            var buffer = new System.Text.StringBuilder((int)size);
            // EXTENDED_NAME_FORMAT.NameDisplay = 3
            if (GetUserNameExW(3, buffer, ref size) != 0 && buffer.Length > 0)
            {
                return buffer.ToString();
            }
        }
        catch
        {
            // Local accounts often have no display name; fall back to the login.
        }

        return null;
    }

    [ObservableProperty]
    public partial string InventoryFileName { get; set; } = "Inventario pendiente";

    [ObservableProperty]
    public partial string ForecastFileName { get; set; } = "Forecast pendiente";

    public string PreparedArticlesCatalogText =>
        $"{_satellitePreparedArticleStore.GetItemsFor(SelectedCompany).Count:N0} alistados sat.";

    public string TarimaSizesCatalogText =>
        $"{_tarimaSizeStore.GetItemsFor(SelectedCompany).Count:N0} tamanos tarima";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial WeekOptionViewModel? SelectedWeek { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial HorizonOptionViewModel? SelectedHorizon { get; set; }

    [ObservableProperty]
    public partial ArticleResultRowViewModel? SelectedArticle { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    public partial string SearchText { get; set; } = string.Empty;

    // Bind the filter combos via SelectedItem (not SelectedValue): x:Bind TwoWay on
    // ComboBox.SelectedValue + SelectedValuePath throws a NullReferenceException in
    // the generated binding when the value is changed in code (e.g. Limpiar).
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    public partial StatusFilterOptionViewModel? SelectedGeneralStatusOption { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    public partial StatusFilterOptionViewModel? SelectedSatelliteStatusOption { get; set; }

    public StatusFilterOption GeneralStatusFilter => SelectedGeneralStatusOption?.Value ?? StatusFilterOption.All;

    public StatusFilterOption SatelliteStatusFilter => SelectedSatelliteStatusOption?.Value ?? StatusFilterOption.All;

    [ObservableProperty]
    public partial int TotalArticles { get; set; }

    [ObservableProperty]
    public partial int CriticalCount { get; set; }

    [ObservableProperty]
    public partial int WarningCount { get; set; }

    [ObservableProperty]
    public partial int HealthyCount { get; set; }

    [ObservableProperty]
    public partial int SatelliteCriticalCount { get; set; }

    [ObservableProperty]
    public partial int SatelliteWarningCount { get; set; }

    [ObservableProperty]
    public partial int SatelliteHealthyCount { get; set; }

    [ObservableProperty]
    public partial string DetailTitle { get; set; } = "Detalle por zonas";

    [ObservableProperty]
    public partial string DetailSubtitle { get; set; } = "Selecciona un articulo para revisar su stock por zona.";

    [ObservableProperty]
    public partial string DetailForecastText { get; set; } = "0";

    [ObservableProperty]
    public partial string DetailPrincipalInventoryText { get; set; } = "0";

    [ObservableProperty]
    public partial string DetailTotalInventoryText { get; set; } = "0";

    [ObservableProperty]
    public partial string DetailSatelliteInventoryText { get; set; } = "0";

    [ObservableProperty]
    public partial string DetailTransferSuggestionText { get; set; } = "0";

    [ObservableProperty]
    public partial string DetailRemainingShortageText { get; set; } = "0";

    [ObservableProperty]
    public partial string DetailTransferStateText { get; set; } = "Estado de traslado: No requerido";

    [ObservableProperty]
    public partial string DetailSatelliteDifferenceText { get; set; } = "0";

    [ObservableProperty]
    public partial bool IsMessageOpen { get; set; }

    [ObservableProperty]
    public partial string MessageTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; set; } = InfoBarSeverity.Informational;

    public string SelectedWeekLabel =>
        ShowWarehouseComparisonView
            ? CurrentSession.LastWarehouseComparison is { } comparison
                ? $"Desde {FormatComparisonPeriod(comparison.StartPeriod, CurrentComparisonGranularity)} | {comparison.PeriodCount:N0} {(CurrentComparisonGranularity == ForecastGranularity.Monthly ? "meses" : "semanas")}"
                : $"{PeriodSelectorHeader}: {SelectedWeek?.Label ?? "Sin periodo"}"
            : CurrentSession.LastAnalysis is { } analysis
            ? $"{analysis.HorizonStartWeek:dd.MM.yy} a {analysis.HorizonEndWeek:dd.MM.yy} | {analysis.HorizonDescription} ({analysis.AnalyzedWeeks} sem){(analysis.IsIncompleteHorizon ? " parcial" : string.Empty)}"
            : $"{SelectedWeek?.Label ?? "Sin semana"} | {(SelectedHorizon?.Label ?? "4 semanas")}";

    private string CurrentHorizonShortLabel =>
        (CurrentSession.LastAnalysis?.Horizon ?? SelectedHorizon?.Value ?? AnalysisHorizon.Weeks(4)).ToShortLabel();

    public string ForecastColumnHeader => $"Forecast {CurrentHorizonShortLabel}";

    public string ForecastHeaderCaption => $"FORECAST {CurrentHorizonShortLabel}";

    public string PeriodSelectorHeader =>
        (ShowWarehouseComparisonView ? CurrentComparisonGranularity : CurrentSession.Granularity) == ForecastGranularity.Monthly
            ? "Mes inicio"
            : "Semana inicio";

    public bool HasResults => FilteredResults.Count > 0;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        GeneralStatusFilter != StatusFilterOption.All ||
        SatelliteStatusFilter != StatusFilterOption.All ||
        TableArticuloFilter.IsActive ||
        TableForecastFilter.IsActive ||
        TablePrincipalFilter.IsActive ||
        TableDifferenceFilter.IsActive ||
        TableStatusFilter.IsActive ||
        TableSatelliteFilter.IsActive ||
        ForecastFilterActive ||
        PrincipalFilterActive ||
        DiffFilterActive ||
        SatFilterActive;

    private CompanyAnalysisSession CurrentSession => _sessions[SelectedCompany];

    private ForecastGranularity CurrentComparisonGranularity =>
        CurrentSession.ComparisonForecastWorkbook?.Granularity ?? CurrentSession.ComparisonGranularity;

    private void UpdateVisibleLoadFileNames()
    {
        var session = CurrentSession;
        if (ShowWarehouseComparisonView)
        {
            InventoryFileName = session.ComparisonInventoryFileName ?? "Inventario de comparativa pendiente";
            ForecastFileName = session.ComparisonForecastFileName ?? "Forecast de comparativa pendiente";
        }
        else
        {
            InventoryFileName = session.InventoryFileName ?? "Inventario pendiente";
            ForecastFileName = session.ForecastFileName ?? "Forecast pendiente";
        }

        OnPropertyChanged(nameof(LoadSummaryText));
    }

    partial void OnSelectedCompanyChanged(string value)
    {
        OnPropertyChanged(nameof(IsCompanyEpa));
        OnPropertyChanged(nameof(IsCompanyCofersa));
        OnPropertyChanged(nameof(PreparedArticlesCatalogText));
        OnPropertyChanged(nameof(TarimaSizesCatalogText));
        ApplySessionToUi();
        RefreshTransitRows();
    }

    partial void OnSelectedWeekChanged(WeekOptionViewModel? value)
    {
        if (_isRestoringState)
        {
            OnPropertyChanged(nameof(SelectedWeekLabel));
            return;
        }

        var session = CurrentSession;
        if (ShowWarehouseComparisonView)
        {
            session.ComparisonSelectedPeriod = value?.Value;
            session.LastWarehouseComparison = null;
            ResetWarehouseComparisonUi();
            AnalyzeCommand.NotifyCanExecuteChanged();
            ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedWeekLabel));
            OnPropertyChanged(nameof(LoadSummaryText));
            OnPropertyChanged(nameof(WarehouseComparisonCaptionText));
            _ = TryAutoAnalyzeAsync();
            return;
        }

        session.SelectedWeek = value?.Value;
        session.LastAnalysis = null;
        ResetTransferAnalysisUi();
        AnalyzeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedWeekLabel));
        OnPropertyChanged(nameof(LoadSummaryText));
        _ = TryAutoAnalyzeAsync();
    }

    partial void OnSelectedHorizonChanged(HorizonOptionViewModel? value)
    {
        OnPropertyChanged(nameof(ForecastColumnHeader));
        OnPropertyChanged(nameof(ForecastHeaderCaption));
        OnPropertyChanged(nameof(SelectedWeekLabel));
        OnPropertyChanged(nameof(LoadSummaryText));
        if (_isRestoringState)
        {
            return;
        }

        var session = CurrentSession;
        session.Horizon = value?.Value ?? AnalysisHorizon.Weeks(4);
        session.LastAnalysis = null;
        ResetTransferAnalysisUi();
        AnalyzeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
        _ = TryAutoAnalyzeAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ArticleFilterActive));
        if (_isRestoringState)
        {
            return;
        }

        CurrentSession.SearchText = value;
        ApplyFilters();
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedGeneralStatusOptionChanged(StatusFilterOptionViewModel? value)
    {
        OnPropertyChanged(nameof(GeneralStatusFilter));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(StatusFilterActive));
        if (_isRestoringState)
        {
            return;
        }

        CurrentSession.GeneralStatusFilter = GeneralStatusFilter;
        ApplyFilters();
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSatelliteStatusOptionChanged(StatusFilterOptionViewModel? value)
    {
        OnPropertyChanged(nameof(SatelliteStatusFilter));
        OnPropertyChanged(nameof(HasActiveFilters));
        if (_isRestoringState)
        {
            return;
        }

        CurrentSession.SatelliteStatusFilter = SatelliteStatusFilter;
        ApplyFilters();
    }

    private StatusFilterOptionViewModel OptionFor(StatusFilterOption value) =>
        StatusFilterOptions.FirstOrDefault(option => option.Value == value) ?? StatusFilterOptions[0];

    partial void OnSelectedArticleChanged(ArticleResultRowViewModel? value)
    {
        if (value is null)
        {
            DetailTitle = "Detalle por zonas";
            DetailSubtitle = "Selecciona un articulo para revisar su stock principal, satelital y la sugerencia de traslado.";
            DetailForecastText = "0";
            DetailPrincipalInventoryText = "0";
            DetailTotalInventoryText = "0";
            DetailSatelliteInventoryText = "0";
            DetailTransferSuggestionText = "0";
            DetailRemainingShortageText = "0";
            DetailTransferStateText = "Estado de traslado: No requerido";
            DetailSatelliteDifferenceText = "0";
            ZoneDetails = Array.Empty<ZoneDetailRowViewModel>();
            return;
        }

        DetailTitle = value.Article;
        DetailSubtitle = $"Principal {value.StatusLabel} | Traslado {value.TransferStateLabel} | {value.ZoneCountText} | {value.SatelliteZoneCountText}";
        DetailForecastText = value.ForecastText;
        DetailPrincipalInventoryText = value.PrincipalInventoryText;
        DetailTotalInventoryText = value.TotalInventoryText;
        DetailSatelliteInventoryText = value.SatelliteInventoryText;
        DetailTransferSuggestionText = value.TransferSuggestionText;
        DetailRemainingShortageText = value.RemainingShortageText;
        DetailTransferStateText = $"Estado de traslado: {value.TransferStateLabel}";
        DetailSatelliteDifferenceText = value.SatelliteDifferenceText;
        ZoneDetails = value.Zones
            .OrderByDescending(zone => zone.IsSatellite)
            .ThenBy(zone => zone.StorageZone, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool CanLoadInventory() => !IsBusy;

    private bool CanLoadForecast() => !IsBusy;

    private bool CanLoadReferenceWorkbook() => !IsBusy;

    private bool CanAnalyze() =>
        ShowWarehouseComparisonView
            ? !IsBusy &&
                CurrentSession.ComparisonInventoryWorkbook is not null &&
                CurrentSession.ComparisonForecastWorkbook is not null &&
                SelectedWeek is not null
            : !IsBusy &&
                CurrentSession.InventoryWorkbook is not null &&
                CurrentSession.ForecastWorkbook is not null &&
                SelectedWeek is not null &&
                SelectedHorizon is not null;

    private bool CanExport() =>
        !IsBusy &&
        _filteredAnalysis?.Articles.Count > 0;

    private bool CanExportWarehouseComparison() =>
        !IsBusy &&
        CurrentSession.LastWarehouseComparison is not null &&
        WarehouseComparisonRows.Count > 0;

    private bool CanClearFilters() =>
        !IsBusy &&
        HasActiveFilters;

    private bool CanClearWarehouseComparisonFilters() =>
        !IsBusy &&
        HasWarehouseComparisonFilters;

    [RelayCommand(CanExecute = nameof(CanLoadInventory))]
    private async Task LoadInventoryAsync()
    {
        var file = await _workbookFileDialogService.PickWorkbookAsync();
        if (file is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var inventoryWorkbook = await ParseInventoryWorkbookAsync(file);

            var session = CurrentSession;
            if (ShowWarehouseComparisonView)
            {
                session.ComparisonInventoryWorkbook = inventoryWorkbook;
                session.ComparisonInventoryFileName = file.Name;
                session.LastWarehouseComparison = null;

                ResetWarehouseComparisonUi();
                ResetWarehouseComparisonFiltersForNewLoad();
                UpdateVisibleLoadFileNames();
                ShowMessage("Inventario de comparativa cargado", $"{file.Name} se valido correctamente para Comparativa.", InfoBarSeverity.Success);
            }
            else
            {
                session.InventoryWorkbook = inventoryWorkbook;
                session.InventoryFileName = file.Name;
                session.LastAnalysis = null;

                ResetTransferAnalysisUi();
                UpdateVisibleLoadFileNames();
                ShowMessage("Inventario de traslados cargado", $"{file.Name} se valido correctamente para Traslados.", InfoBarSeverity.Success);
            }
        });

        await TryAutoAnalyzeAsync();
    }

    [RelayCommand(CanExecute = nameof(CanLoadForecast))]
    private async Task LoadForecastAsync()
    {
        var file = await _workbookFileDialogService.PickWorkbookAsync();
        if (file is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var forecastWorkbook = await ParseForecastWorkbookAsync(file);

            var session = CurrentSession;
            if (ShowWarehouseComparisonView)
            {
                session.ComparisonForecastWorkbook = forecastWorkbook;
                session.ComparisonForecastFileName = file.Name;
                session.ComparisonGranularity = forecastWorkbook.Granularity;
                session.ComparisonSelectedPeriod = ResolveSelectableComparisonStartPeriod(
                    forecastWorkbook,
                    session.ComparisonSelectedPeriod);
                session.LastWarehouseComparison = null;

                ApplyWeeksFromSession();
                ResetWarehouseComparisonUi();
                ResetWarehouseComparisonFiltersForNewLoad();
                UpdateVisibleLoadFileNames();
            }
            else
            {
                session.ForecastWorkbook = forecastWorkbook;
                session.ForecastFileName = file.Name;
                session.Granularity = forecastWorkbook.Granularity;
                session.LastAnalysis = null;
                session.SelectedWeek = forecastWorkbook.AvailableWeeks.FirstOrDefault();

                ApplyWeeksFromSession();
                ResetTransferAnalysisUi();
                UpdateVisibleLoadFileNames();
            }

            var periodNoun = forecastWorkbook.Granularity == ForecastGranularity.Monthly ? "meses" : "semanas";
            ShowMessage(
                ShowWarehouseComparisonView ? "Forecast de comparativa cargado" : "Forecast de traslados cargado",
                $"{file.Name} listo con {forecastWorkbook.AvailableWeeks.Count} {periodNoun} disponibles.",
                InfoBarSeverity.Success);
        });

        await TryAutoAnalyzeAsync();
    }

    [RelayCommand(CanExecute = nameof(CanLoadReferenceWorkbook))]
    private async Task LoadPreparedArticlesAsync()
    {
        var file = await _workbookFileDialogService.PickWorkbookAsync();
        if (file is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
            var workbook = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, writable: false);
                return _satellitePreparedArticleParser.Parse(stream);
            });

            _satellitePreparedArticleStore.Save(SelectedCompany, workbook.Articles);
            RefreshCatalogDependentViews();
            ShowMessage(
                "Alistado satelital cargado",
                $"{file.Name}: {workbook.Articles.Count:N0} articulos listos para usar en {SelectedCompany}.",
                InfoBarSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanLoadReferenceWorkbook))]
    private async Task LoadTarimaSizesAsync()
    {
        var file = await _workbookFileDialogService.PickWorkbookAsync();
        if (file is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
            var workbook = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, writable: false);
                return _tarimaSizeParser.Parse(stream);
            });

            _tarimaSizeStore.Save(SelectedCompany, workbook.Entries);
            RefreshCatalogDependentViews();
            ShowMessage(
                "Tamanos de tarima cargados",
                $"{file.Name}: {workbook.Entries.Count:N0} tamanos listos para usar en {SelectedCompany}.",
                InfoBarSeverity.Success);
        });
    }

    private async Task TryAutoAnalyzeAsync()
    {
        if (CanAnalyze())
        {
            await AnalyzeAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadExpediciones))]
    private async Task LoadExpedicionesAsync()
    {
        var file = await _workbookFileDialogService.PickWorkbookAsync();
        if (file is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
            var (expediciones, fileInventory) = await Task.Run(() =>
            {
                using var expedicionesStream = new MemoryStream(bytes, writable: false);
                var parsedExpediciones = _expedicionesParser.Parse(expedicionesStream);

                InventoryWorkbook? parsedInventory = null;
                using var inventoryStream = new MemoryStream(bytes, writable: false);
                try
                {
                    parsedInventory = _inventoryParser.Parse(inventoryStream);
                }
                catch (WorkbookValidationException)
                {
                    // Some expedition exports contain only demand lines. In that
                    // case the module uses the inventory loaded in Expediciones.
                }

                return (parsedExpediciones, parsedInventory);
            });

            var session = CurrentSession;
            var inventory = fileInventory ?? session.ExpedicionesInventory;
            if (inventory is null)
            {
                throw new WorkbookValidationException(
                    "El archivo de expediciones se leyo correctamente, pero no trae inventario por zona. Cargue primero el inventario dentro del modulo Expediciones y luego vuelva a cargar expediciones.");
            }

            var analysis = await BuildExpedicionesAnalysisAsync(expediciones, inventory);

            session.ExpedicionesWorkbook = expediciones;
            session.ExpedicionesInventory = inventory;
            session.ExpedicionesFileName = file.Name;
            if (fileInventory is not null)
            {
                session.ExpedicionesInventoryFileName = file.Name;
                ExpedicionesInventoryFileName = file.Name;
            }

            session.LastExpedicionesAnalysis = analysis;

            ExpedicionesFileName = file.Name;
            ResetExpedicionFiltersForNewLoad();
            BuildExpedicionesView(analysis);
            ShowExpedicionesView = true;
            var inventorySource = fileInventory is not null
                ? "inventario del mismo archivo"
                : $"inventario de expediciones ({session.ExpedicionesInventoryFileName ?? "anterior"})";
            ShowMessage(
                "Expediciones cargadas",
                $"{file.Name}: {analysis.Lines.Count} articulos, {ExpToTransfer} para traer de bodega externa; usando {inventorySource}.",
                InfoBarSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanLoadExpedicionesInventory))]
    private async Task LoadExpedicionesInventoryAsync()
    {
        var file = await _workbookFileDialogService.PickWorkbookAsync();
        if (file is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var inventoryWorkbook = await ParseInventoryWorkbookAsync(file);
            var session = CurrentSession;

            session.ExpedicionesInventory = inventoryWorkbook;
            session.ExpedicionesInventoryFileName = file.Name;
            ExpedicionesInventoryFileName = file.Name;

            if (session.ExpedicionesWorkbook is not null)
            {
                var analysis = await BuildExpedicionesAnalysisAsync(session.ExpedicionesWorkbook, inventoryWorkbook);
                session.LastExpedicionesAnalysis = analysis;
                BuildExpedicionesView(analysis);
            }
            else
            {
                session.LastExpedicionesAnalysis = null;
                BuildExpedicionesView(null);
            }

            ShowMessage(
                "Inventario de expediciones cargado",
                $"{file.Name} se valido correctamente para Expediciones.",
                InfoBarSeverity.Success);
        });
    }

    private bool CanLoadExpedicionesInventory() => !IsBusy;

    private bool CanLoadExpediciones() => !IsBusy;

    private async Task<ExpedicionesAnalysisResult> BuildExpedicionesAnalysisAsync(
        ExpedicionesWorkbook expediciones,
        InventoryWorkbook inventory)
    {
        // Ver AnalyzeTransferAsync: la empresa y su configuracion se leen en el
        // hilo de UI, antes de saltar al hilo de fondo.
        var company = SelectedCompany;
        var satelliteZones = _satelliteZoneStore.GetZonesFor(company);
        var pendingTransit = _transferTransitStore.GetPendingTransit(company);
        var excludedZones = _inventoryExcludedZoneStore.GetZonesFor(company);

        return await Task.Run(() =>
            _expedicionesAnalyzer.Analyze(
                expediciones.Lines,
                inventory.Positions,
                satelliteZones,
                pendingTransit,
                excludedZones));
    }

    private void ResetExpedicionFiltersForNewLoad()
    {
        _expedicionTextSort = null;
        SelectedExpedicionTab = "Todos";

        ExpExpedicionFilter.ClearCommand.Execute(null);
        ExpArticuloFilter.ClearCommand.Execute(null);
        ExpDescripcionFilter.ClearCommand.Execute(null);
        ExpCantidadFilter.ClearCommand.Execute(null);
        ExpPrincipalFilter.ClearCommand.Execute(null);
        ExpExternasFilter.ClearCommand.Execute(null);
        ExpTransitoFilter.ClearCommand.Execute(null);
        ExpTraerFilter.ClearCommand.Execute(null);
        ExpPalletCountFilter.ClearCommand.Execute(null);
        ExpPaletsFilter.ClearCommand.Execute(null);
        ExpTarimaFilter.ClearCommand.Execute(null);
        ExpAlistadoFilter.ClearCommand.Execute(null);
        ExpZonasFilter.ClearCommand.Execute(null);
        ExpPendienteFilter.ClearCommand.Execute(null);
    }

    private async Task RefreshExpedicionesAnalysisAsync()
    {
        var session = CurrentSession;
        if (session.ExpedicionesWorkbook is null || session.ExpedicionesInventory is null)
        {
            return;
        }

        var analysis = await BuildExpedicionesAnalysisAsync(
            session.ExpedicionesWorkbook,
            session.ExpedicionesInventory);

        session.LastExpedicionesAnalysis = analysis;
        BuildExpedicionesView(analysis);
    }

    private void RefreshCatalogDependentViews()
    {
        OnPropertyChanged(nameof(PreparedArticlesCatalogText));
        OnPropertyChanged(nameof(TarimaSizesCatalogText));

        var session = CurrentSession;
        if (session.LastAnalysis is not null)
        {
            BuildTransferView(session.LastAnalysis);
        }

        if (session.LastExpedicionesAnalysis is not null)
        {
            BuildExpedicionesView(session.LastExpedicionesAnalysis);
        }
    }

    private void BuildExpedicionesView(ExpedicionesAnalysisResult? analysis)
    {
        if (analysis is null)
        {
            _allExpedicionRows = Array.Empty<ExpedicionRowViewModel>();
            ExpedicionRows = Array.Empty<ExpedicionRowViewModel>();
            ExpEvaluated = 0;
            ExpCovered = 0;
            ExpToTransfer = 0;
            ExpWithoutExternal = 0;
            ExpPreparedTotal = 0;
            ExpTabTotal = 0;
            ExpedicionesLineCount = 0;
            return;
        }

        ExpEvaluated = analysis.Evaluated;
        ExpCovered = analysis.Covered;
        ExpWithoutExternal = analysis.WithoutExternal;

        // Expediciones works as an exception list: completed lines or lines
        // already covered by OLO/principal stay out of the visible workload.
        var preparedArticles = _satellitePreparedArticleStore.GetArticleSetFor(SelectedCompany);
        var tarimaSizes = _tarimaSizeStore.GetMapFor(SelectedCompany);
        var rows = analysis.Lines
            .Where(ShouldShowExpedicionLine)
            .OrderByDescending(line => line.TransferSuggestionQuantity)
            .ThenByDescending(line => line.RemainingShortageAfterTransfer)
            .ThenBy(line => line.ExpeditionNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.Article, StringComparer.OrdinalIgnoreCase)
            .Select(line => new ExpedicionRowViewModel(
                line,
                SatellitePreparedArticleMatcher.IsPrepared(preparedArticles, line.Article, line.Description),
                TryGetTarimaSizeText(tarimaSizes, line.Article)))
            .ToList();

        _allExpedicionRows = rows;
        ExpPreparedTotal = rows.Count(row => row.IsSatellitePrepared);
        ExpToTransfer = rows.Count(row => row.TransferQuantity > 0 && !row.IsSatellitePrepared);
        FilterExpedicionRows();
    }

    private static bool ShouldShowExpedicionLine(ExpeditionLineResult line) =>
        line.DemandQuantity > 0m &&
        (line.TransferSuggestionQuantity > 0m ||
            line.RemainingShortageAfterTransfer > 0m ||
            line.PendingTransitQuantity > 0m);

    /// <remarks>
    /// <paramref name="excludedZones"/> y <paramref name="descriptions"/> se
    /// reciben ya resueltos porque este metodo corre en un hilo de fondo: no
    /// debe leer estado observable del ViewModel.
    /// </remarks>
    private WarehouseComparisonResult? AnalyzeWarehouseComparison(
        CompanyAnalysisSession session,
        DateOnly startPeriod,
        IReadOnlyList<string> excludedZones,
        IReadOnlyDictionary<string, string> descriptions)
    {
        if (session.ComparisonInventoryWorkbook is null || session.ComparisonForecastWorkbook is null)
        {
            return null;
        }

        return _warehouseComparisonAnalyzer.Analyze(
            session.ComparisonInventoryWorkbook.Positions,
            session.ComparisonForecastWorkbook.Entries,
            session.ComparisonForecastWorkbook.AvailableWeeks,
            startPeriod,
            excludedZones,
            descriptions);
    }

    private static DateOnly? ResolveSelectableComparisonStartPeriod(
        ForecastWorkbook forecastWorkbook,
        DateOnly? preferredPeriod)
    {
        var availablePeriods = forecastWorkbook.AvailableWeeks
            .Distinct()
            .OrderBy(period => period)
            .ToArray();

        if (availablePeriods.Length == 0)
        {
            return null;
        }

        if (preferredPeriod is { } preferred &&
            availablePeriods.Contains(preferred))
        {
            return preferred;
        }

        var currentPeriod = ResolveCurrentComparisonPeriod(forecastWorkbook.Granularity);
        return availablePeriods
            .Cast<DateOnly?>()
            .FirstOrDefault(period => period >= currentPeriod) ?? availablePeriods[0];
    }

    private static DateOnly ResolveCurrentComparisonPeriod(ForecastGranularity granularity)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (granularity == ForecastGranularity.Monthly)
        {
            return new DateOnly(today.Year, today.Month, 1);
        }

        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysSinceMonday);
    }

    private static string FormatComparisonPeriod(DateOnly period, ForecastGranularity granularity) =>
        granularity == ForecastGranularity.Monthly
            ? period.ToString("MM.yyyy", CultureInfo.InvariantCulture)
            : period.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private void BuildWarehouseComparisonView(WarehouseComparisonResult? comparison)
    {
        if (comparison is null)
        {
            _allWarehouseComparisonRows = Array.Empty<WarehouseComparisonRowViewModel>();
            WarehouseComparisonRows = Array.Empty<WarehouseComparisonRowViewModel>();
            WarehouseComparisonTotal = 0;
            WarehouseComparisonVisibleTotal = 0;
            WarehouseComparisonNoForecastTotal = 0;
            WarehouseComparisonCoverageSeries = [];
            WarehouseComparisonCoverageXAxes = [];
            WarehouseComparisonCoverageYAxes = [];
            WarehouseComparisonCoverageSummaryText = "Carga inventario y forecast para ver la cobertura por semana.";
            OnPropertyChanged(nameof(WarehouseComparisonCaptionText));
            return;
        }

        var comments = _warehouseComparisonCommentStore.GetCommentsFor(SelectedCompany);
        var periodUnit = CurrentComparisonGranularity == ForecastGranularity.Monthly ? "mes" : "sem";
        _allWarehouseComparisonRows = comparison.Rows
            .Where(row => row.ServicaQuantity > 0m)
            .Select(row => new WarehouseComparisonRowViewModel(
                row,
                ResolveWarehouseComparisonComment(comments, row.Article),
                periodUnit,
                OnWarehouseComparisonCommentChanged))
            .ToArray();

        WarehouseComparisonTotal = _allWarehouseComparisonRows.Count;
        WarehouseComparisonNoForecastTotal = _allWarehouseComparisonRows.Count(IsWarehouseComparisonNoSolicita);
        OnPropertyChanged(nameof(WarehouseComparisonCaptionText));
        FilterWarehouseComparisonRows();
    }

    private static string ResolveWarehouseComparisonComment(
        IReadOnlyDictionary<string, string> comments,
        string article)
    {
        if (comments.TryGetValue(article, out var comment) ||
            comments.TryGetValue(InventoryZoneClassifier.NormalizeArticleKey(article), out comment))
        {
            return comment;
        }

        return string.Empty;
    }

    private void OnWarehouseComparisonCommentChanged(WarehouseComparisonRowViewModel row)
    {
        _warehouseComparisonCommentStore.SaveComment(SelectedCompany, row.Article, row.CommentText);
        WarehouseComparisonNoForecastTotal = _allWarehouseComparisonRows.Count(IsWarehouseComparisonNoSolicita);
        OnPropertyChanged(nameof(WarehouseComparisonSummaryText));
        if (ComparisonComentariosFilter.IsActive)
        {
            FilterWarehouseComparisonRows();
            return;
        }

        RefreshWarehouseComparisonCoverageChart(WarehouseComparisonRows);
    }

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        if (ShowWarehouseComparisonView)
        {
            await AnalyzeWarehouseComparisonAsync();
            return;
        }

        await AnalyzeTransferAsync();
    }

    private async Task AnalyzeTransferAsync()
    {
        await ExecuteBusyActionAsync(async () =>
        {
            var session = CurrentSession;
            var selectedWeek = SelectedWeek?.Value
                ?? throw new InvalidOperationException("No hay una semana seleccionada.");
            var horizon = SelectedHorizon?.Value ?? AnalysisHorizon.Weeks(4);
            var periodKey = selectedWeek.ToString("yyyy-MM-dd");

            // Todo lo que depende de la empresa se resuelve aqui, en el hilo de
            // UI: leer SelectedCompany dentro del Task.Run permitia analizar con
            // la empresa cambiada a medias si el usuario la alternaba.
            var company = SelectedCompany;
            var thresholds = _coverageThresholdStore.GetFor(company);
            var satelliteZones = _satelliteZoneStore.GetZonesFor(company);
            var pendingTransit = _transferTransitStore.GetPendingTransit(company, periodKey);
            var excludedZones = _inventoryExcludedZoneStore.GetZonesFor(company);

            var analysis = await Task.Run(() =>
                _weeklyForecastAnalyzer.Analyze(
                    session.InventoryWorkbook!.Positions,
                    session.ForecastWorkbook!.Entries,
                    selectedWeek,
                    horizon,
                    thresholds,
                    satelliteZones,
                    pendingTransit,
                    excludedZones));

            session.SelectedWeek = selectedWeek;
            session.Horizon = horizon;
            session.LastAnalysis = analysis;
            ResetFilters(session);
            RestoreFiltersFromSession(session);
            ClearColumnRanges();

            ApplyAnalysis(analysis, preserveSelection: false);
            IsLoadPanelExpanded = false;
            ShowMessage(
                analysis.IsIncompleteHorizon ? "Analisis completado con horizonte parcial" : "Analisis completado",
                $"{analysis.Articles.Count} articulos evaluados desde {analysis.HorizonStartWeek:dd.MM.yy} hasta {analysis.HorizonEndWeek:dd.MM.yy} | horizonte {analysis.HorizonDescription} ({analysis.AnalyzedWeeks} semanas).",
                analysis.IsIncompleteHorizon ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        });
    }

    private async Task AnalyzeWarehouseComparisonAsync()
    {
        await ExecuteBusyActionAsync(async () =>
        {
            var session = CurrentSession;
            var comparisonStartPeriod = SelectedWeek?.Value
                ?? session.ComparisonSelectedPeriod
                ?? throw new InvalidOperationException("No hay una semana de inicio seleccionada para Comparativa.");
            var excludedZones = _inventoryExcludedZoneStore.GetZonesFor(SelectedCompany);
            var descriptions = CurrentComparisonDescriptions(session);
            var comparison = await Task.Run(() =>
                AnalyzeWarehouseComparison(session, comparisonStartPeriod, excludedZones, descriptions));

            session.ComparisonSelectedPeriod = comparisonStartPeriod;
            session.LastWarehouseComparison = comparison;
            BuildWarehouseComparisonView(comparison);
            IsLoadPanelExpanded = false;
            ShowMessage(
                "Comparativa actualizada",
                comparison is null
                    ? "Carga inventario y forecast de Comparativa para analizar."
                    : $"{comparison.Rows.Count:N0} articulos evaluados desde {FormatComparisonPeriod(comparison.StartPeriod, session.ComparisonGranularity)}.",
                comparison is null ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var analysis = _filteredAnalysis;
        if (analysis is null)
        {
            return;
        }

        var suggestedFileName = $"{SelectedCompany}-requisicion-{analysis.SelectedWeek:yyyyMMdd}";
        var destination = await _workbookFileDialogService.PickExportFileAsync(suggestedFileName);
        if (destination is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var company = SelectedCompany;
            var preparedArticles = _satellitePreparedArticleStore.GetArticleSetFor(company);
            var tarimaSizes = _tarimaSizeStore.GetMapFor(company);
            var bytes = await Task.Run(() => _analysisWorkbookExporter.Export(company, analysis, preparedArticles, tarimaSizes));
            await FileIO.WriteBytesAsync(destination, bytes);
            ShowMessage(
                "Excel exportado",
                $"Se guardo el analisis en {destination.Name}.",
                InfoBarSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanExportWarehouseComparison))]
    private async Task ExportWarehouseComparisonAsync()
    {
        var comparison = CurrentSession.LastWarehouseComparison;
        if (comparison is null || WarehouseComparisonRows.Count == 0)
        {
            return;
        }

        var suggestedFileName = $"{SelectedCompany}-comparativa-servica-olo-{comparison.StartPeriod:yyyyMMdd}";
        var destination = await _workbookFileDialogService.PickExportFileAsync(suggestedFileName);
        if (destination is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var visibleRows = WarehouseComparisonRows.Select(row => row.Source).ToArray();
            var comments = WarehouseComparisonRows.ToDictionary(
                row => InventoryZoneClassifier.NormalizeArticleKey(row.Article),
                row => row.CommentText?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
            var periodUnit = CurrentComparisonGranularity == ForecastGranularity.Monthly ? "mes" : "sem";
            var histogramBuckets = BuildWarehouseComparisonHistogramBuckets(
                WarehouseComparisonRows,
                periodUnit,
                comparison.PeriodCount);
            var company = SelectedCompany;
            var bytes = await Task.Run(() => _analysisWorkbookExporter.ExportWarehouseComparison(
                company,
                comparison,
                visibleRows,
                comments,
                histogramBuckets));
            await FileIO.WriteBytesAsync(destination, bytes);
            ShowMessage(
                "Comparativa exportada",
                $"Se guardo la comparativa en {destination.Name}.",
                InfoBarSeverity.Success);
        });
    }

    [RelayCommand(CanExecute = nameof(CanExportRequisition))]
    private async Task ExportRequisitionAsync()
    {
        var analysis = CurrentSession.LastAnalysis;
        if (analysis is null)
        {
            return;
        }

        var suggestedFileName = $"{SelectedCompany}-traslados-{analysis.SelectedWeek:yyyyMMdd}";
        var destination = await _workbookFileDialogService.PickExportFileAsync(suggestedFileName);
        if (destination is null)
        {
            return;
        }

        await ExecuteBusyActionAsync(async () =>
        {
            var ordered = _allTransferRows
                .Where(row => row.IsOrdered && row.CanOrder)
                .Select(row => row.Article)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var descriptions = CurrentDescriptions();
            var company = SelectedCompany;
            var preparedArticles = _satellitePreparedArticleStore.GetArticleSetFor(company);
            var tarimaSizes = _tarimaSizeStore.GetMapFor(company);
            var selectedPallets = _allTransferRows
                .ToDictionary(
                    row => InventoryZoneClassifier.NormalizeArticleKey(row.Article),
                    row => (IReadOnlyList<PalletInventoryDetail>)row.SelectedPallets,
                    StringComparer.OrdinalIgnoreCase);
            var pendingPalletKeys = _transferTransitStore.GetPendingPalletKeys(company);
            var bytes = await Task.Run(() => _analysisWorkbookExporter.ExportTransferRequisition(
                company,
                analysis,
                ordered,
                descriptions,
                preparedArticles,
                tarimaSizes,
                selectedPallets,
                pendingPalletKeys));
            await FileIO.WriteBytesAsync(destination, bytes);
            ShowMessage(
                "Requisicion exportada",
                $"Se guardo la lista para traer en {destination.Name}.",
                InfoBarSeverity.Success);
        });
    }

    private bool CanExportRequisition() => !IsBusy && TransferTotal > 0;

    [RelayCommand(CanExecute = nameof(CanClearOrders))]
    private void ClearOrders()
    {
        _isBulkOrderUpdate = true;
        try
        {
            foreach (var row in _allTransferRows.Where(row => row.CanOrder))
            {
                row.IsOrdered = false;
            }
        }
        finally
        {
            _isBulkOrderUpdate = false;
        }

        var periodKey = CurrentPeriodKey;
        if (!string.IsNullOrEmpty(periodKey))
        {
            _transferOrderStore.ClearPeriod(SelectedCompany, periodKey);
        }

        OrderedCount = 0;
        SendSelectedToTransitCommand.NotifyCanExecuteChanged();
    }

    private bool CanClearOrders() => !IsBusy && OrderedCount > 0;

    [RelayCommand(CanExecute = nameof(CanSendSelectedToTransit))]
    private async Task SendSelectedToTransitAsync()
    {
        var selectedRows = _allTransferRows
            .Where(row => row.IsOrdered && row.CanOrder && row.SelectedPallets.Count > 0)
            .ToArray();
        if (selectedRows.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var items = selectedRows.SelectMany(row => row.SelectedPallets.Select(pallet => new TransferTransitItem(
            string.Empty,
            SelectedCompany,
            CurrentPeriodKey,
            row.Article,
            row.Description,
            pallet.Pallet,
            pallet.Quantity,
            pallet.StorageZone,
            pallet.Location,
            pallet.ValidationDate,
            row.TarimaSizeText,
            StorageOriginClassifier.Resolve(pallet.StorageZone),
            now)));

        var result = _transferTransitStore.AddPending(items);
        _isBulkOrderUpdate = true;
        try
        {
            foreach (var row in selectedRows)
            {
                row.IsOrdered = false;
            }
        }
        finally
        {
            _isBulkOrderUpdate = false;
        }

        // Las filas enviadas dejan de estar marcadas, pero las que seguian
        // marcadas sin palets seleccionados conservan su marca: un solo guardado
        // con el estado resultante del periodo.
        var sentPeriodKey = CurrentPeriodKey;
        if (!string.IsNullOrEmpty(sentPeriodKey))
        {
            _transferOrderStore.ReplacePeriod(
                SelectedCompany,
                sentPeriodKey,
                _allTransferRows
                    .Where(row => row.CanOrder && row.IsOrdered)
                    .ToDictionary(row => row.Article, row => row.TransferSuggestionQuantity, StringComparer.OrdinalIgnoreCase));
        }

        OrderedCount = _allTransferRows.Count(row => row.IsOrdered && row.CanOrder);
        ClearOrdersCommand.NotifyCanExecuteChanged();
        await RefreshAfterTransitChangeAsync();

        ShowMessage(
            result.Added > 0 ? "Pallets enviados a transito" : "Sin pallets nuevos",
            result.Skipped > 0
                ? $"{result.Added:N0} pallets agregados; {result.Skipped:N0} ya estaban en transito o no eran validos."
                : $"{result.Added:N0} pallets agregados al modulo Transito.",
            result.Added > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
    }

    private bool CanSendSelectedToTransit() =>
        !IsBusy &&
        CurrentSession.LastAnalysis is not null &&
        _allTransferRows.Any(row => row.IsOrdered && row.CanOrder && row.SelectedPallets.Count > 0);

    [RelayCommand(CanExecute = nameof(CanConfirmSelectedTransit))]
    private async Task ConfirmSelectedTransitAsync()
    {
        var ids = _allTransitRows.Where(row => row.IsSelected).Select(row => row.Id).ToArray();
        await ConfirmTransitIdsAsync(ids, "Pallets recibidos");
    }

    private bool CanConfirmSelectedTransit() => !IsBusy && TransitSelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanConfirmVisibleTransit))]
    private async Task ConfirmVisibleTransitAsync()
    {
        var ids = TransitRows.Select(row => row.Id).ToArray();
        await ConfirmTransitIdsAsync(ids, "Transito visible recibido");
    }

    private bool CanConfirmVisibleTransit() => !IsBusy && TransitVisibleCount > 0;

    public async Task ConfirmTransitPalletAsync(TransitRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        await ConfirmTransitIdsAsync([row.Id], "Pallet recibido");
    }

    public async Task ConfirmTransitArticleAsync(TransitRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var received = _transferTransitStore.MarkArticleReceived(SelectedCompany, row.Article);
        await RefreshAfterTransitChangeAsync();
        ShowMessage(
            "Articulo recibido",
            $"{received:N0} pallets de {row.Article} se marcaron como recibidos.",
            received > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
    }

    private async Task ConfirmTransitIdsAsync(IReadOnlyList<string> ids, string title)
    {
        var received = _transferTransitStore.MarkReceived(ids);
        await RefreshAfterTransitChangeAsync();
        ShowMessage(
            title,
            $"{received:N0} pallets se marcaron como recibidos.",
            received > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
    }

    private async Task RefreshAfterTransitChangeAsync()
    {
        RefreshTransitRows();
        SendSelectedToTransitCommand.NotifyCanExecuteChanged();
        ConfirmSelectedTransitCommand.NotifyCanExecuteChanged();
        ConfirmVisibleTransitCommand.NotifyCanExecuteChanged();

        if (CanAnalyze())
        {
            await AnalyzeAsync();
        }

        await RefreshExpedicionesAnalysisAsync();
    }

    private string CurrentPeriodKey =>
        CurrentSession.LastAnalysis is { } analysis ? analysis.HorizonStartWeek.ToString("yyyy-MM-dd") : string.Empty;

    private IReadOnlyDictionary<string, string> CurrentDescriptions()
    {
        var session = CurrentSession;
        return BuildDescriptions(session.InventoryWorkbook, session.ForecastWorkbook);
    }

    private IReadOnlyDictionary<string, string> CurrentComparisonDescriptions(CompanyAnalysisSession session) =>
        BuildDescriptions(session.ComparisonInventoryWorkbook, session.ComparisonForecastWorkbook);

    private static IReadOnlyDictionary<string, string> BuildDescriptions(
        InventoryWorkbook? inventoryWorkbook,
        ForecastWorkbook? forecastWorkbook)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (inventoryWorkbook is { } inventory)
        {
            foreach (var pair in inventory.Descriptions)
            {
                AddDescription(map, pair.Key, pair.Value);
            }
        }

        if (forecastWorkbook is { } forecast)
        {
            foreach (var pair in forecast.Descriptions)
            {
                AddDescription(map, pair.Key, pair.Value);
            }
        }

        return map;
    }

    private static void AddDescription(Dictionary<string, string> map, string article, string description)
    {
        if (string.IsNullOrWhiteSpace(article) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        var cleanArticle = article.Trim();
        var cleanDescription = description.Trim();
        map[cleanArticle] = cleanDescription;

        var normalizedArticle = InventoryZoneClassifier.NormalizeArticleKey(cleanArticle);
        if (!string.IsNullOrWhiteSpace(normalizedArticle))
        {
            map[normalizedArticle] = cleanDescription;
        }

        if (normalizedArticle.All(char.IsDigit))
        {
            var withoutLeadingZeroes = normalizedArticle.TrimStart('0');
            map[string.IsNullOrEmpty(withoutLeadingZeroes) ? "0" : withoutLeadingZeroes] = cleanDescription;
        }
    }

    private void BuildTransferView(WeeklyAnalysisResult? analysisResult)
    {
        if (analysisResult is null)
        {
            _allTransferRows = Array.Empty<TransferRowViewModel>();
            TransferRows = Array.Empty<TransferRowViewModel>();
            FunnelEvaluated = 0;
            FunnelCovered = 0;
            FunnelToTransfer = 0;
            FunnelWithoutExternal = 0;
            TransferTotal = 0;
            OrderedCount = 0;
            TransferPreparedTotal = 0;
            TransferVisibleTotal = 0;
            TransferTabTotal = 0;
            return;
        }

        var funnel = TransferFunnel.From(analysisResult);
        FunnelEvaluated = funnel.Evaluated;
        FunnelCovered = funnel.Covered;
        FunnelWithoutExternal = funnel.WithoutExternal;

        var descriptions = CurrentDescriptions();
        var preparedArticles = _satellitePreparedArticleStore.GetArticleSetFor(SelectedCompany);
        var tarimaSizes = _tarimaSizeStore.GetMapFor(SelectedCompany);
        var periodUnit = CurrentSession.Granularity == ForecastGranularity.Monthly ? "mes" : "sem";

        // Marcas "mandado a traer" guardadas para esta empresa y este periodo: sin
        // esto, re-analizar o reabrir la app borraba en silencio lo que el
        // operador ya habia marcado.
        var orderedArticles = _transferOrderStore.GetOrdered(SelectedCompany, CurrentPeriodKey);

        var rows = new List<TransferRowViewModel>();
        foreach (var article in analysisResult.Articles
            .Where(article => article.TransferSuggestionQuantity > 0)
            .OrderByDescending(article => article.TransferSuggestionQuantity)
            .ThenBy(article => article.Article, StringComparer.OrdinalIgnoreCase))
        {
            var description = descriptions.GetValueOrDefault(article.Article, string.Empty);
            var isPrepared = SatellitePreparedArticleMatcher.IsPrepared(
                preparedArticles,
                article.Article,
                description);
            var row = new TransferRowViewModel(
                article,
                orderedArticles.Contains(article.Article),
                OnTransferRowOrderedChanged,
                description,
                BuildCoverageText(article.CoverageWeeks, analysisResult.AnalyzedWeeks, periodUnit),
                isPrepared,
                TryGetTarimaSizeText(tarimaSizes, article.Article),
                OnTransferRowQuantityChanged);

            rows.Add(row);
        }

        _allTransferRows = rows;
        TransferPreparedTotal = rows.Count(row => row.IsSatellitePrepared);
        TransferTotal = rows.Count(row => row.CanOrder);
        FunnelToTransfer = TransferTotal;
        OrderedCount = rows.Count(row => row.IsOrdered && row.CanOrder);
        FilterTransferRows();
    }

    private static string BuildCoverageText(decimal? coverageWeeks, int analyzedPeriods, string unit) =>
        coverageWeeks is not decimal covered ? "—"
        : covered >= analyzedPeriods ? $"≥{analyzedPeriods} {unit}"
        : $"{covered:0.#} {unit}";

    private static string TryGetTarimaSizeText(IReadOnlyDictionary<string, TarimaSizeEntry> tarimaSizes, string article) =>
        tarimaSizes.TryGetValue(InventoryZoneClassifier.NormalizeArticleKey(article), out var size) &&
        !string.IsNullOrWhiteSpace(size.SizeCode)
            ? size.SizeCode
            : "—";

    private void OnTransferRowOrderedChanged(TransferRowViewModel row)
    {
        if (!row.CanOrder)
        {
            return;
        }

        PersistOrderedMark(row);
        OrderedCount = _allTransferRows.Count(transferRow => transferRow.IsOrdered && transferRow.CanOrder);
        SendSelectedToTransitCommand.NotifyCanExecuteChanged();
    }

    private void OnTransferRowQuantityChanged(TransferRowViewModel row)
    {
        if (row.CanOrder && row.IsOrdered)
        {
            PersistOrderedMark(row);
        }

        OrderedCount = _allTransferRows.Count(transferRow => transferRow.IsOrdered && transferRow.CanOrder);
        TransferVisibleTotal = TransferRows.Count;
        RefreshTransferFilterOptions(TransferRows);
        SendSelectedToTransitCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Guarda (o borra) la marca "mandado a traer" de una fila para la empresa y
    /// el periodo analizado. La cantidad se guarda solo como referencia: al
    /// recargar se restaura la marca, no la cantidad, porque esta se deriva de
    /// los palets seleccionados y debe recalcularse contra el inventario vigente.
    /// </summary>
    private void PersistOrderedMark(TransferRowViewModel row)
    {
        var periodKey = CurrentPeriodKey;
        if (_isBulkOrderUpdate || string.IsNullOrEmpty(periodKey))
        {
            return;
        }

        _transferOrderStore.SetOrdered(
            SelectedCompany,
            periodKey,
            row.Article,
            row.IsOrdered,
            row.TransferSuggestionQuantity);
    }

    [RelayCommand(CanExecute = nameof(CanManageSettings))]
    private async Task ManageSettingsAsync()
    {
        var company = SelectedCompany;
        var currentThresholds = _coverageThresholdStore.GetFor(company);
        var currentZones = _satelliteZoneStore.GetZonesFor(company);
        var currentIgnoredZones = _inventoryExcludedZoneStore.GetZonesFor(company);
        var currentPreparedArticles = _satellitePreparedArticleStore.GetItemsFor(company);
        var currentTarimaSizes = _tarimaSizeStore.GetItemsFor(company);

        var edit = await _settingsDialogService.EditAsync(
            company,
            currentThresholds,
            currentZones,
            currentIgnoredZones,
            currentPreparedArticles,
            currentTarimaSizes);
        if (edit is null)
        {
            return;
        }

        _coverageThresholdStore.Save(company, edit.Thresholds);
        _satelliteZoneStore.Save(company, edit.Zones);
        _inventoryExcludedZoneStore.Save(company, edit.IgnoredZones);
        _satellitePreparedArticleStore.Save(company, edit.PreparedArticles);
        _tarimaSizeStore.Save(company, edit.TarimaSizes);
        var savedZones = _satelliteZoneStore.GetZonesFor(company);
        var savedIgnoredZones = _inventoryExcludedZoneStore.GetZonesFor(company);
        var savedPreparedArticles = _satellitePreparedArticleStore.GetItemsFor(company);
        var savedTarimaSizes = _tarimaSizeStore.GetItemsFor(company);
        OnPropertyChanged(nameof(PreparedArticlesCatalogText));
        OnPropertyChanged(nameof(TarimaSizesCatalogText));
        var willReanalyze = CanAnalyze();

        if (willReanalyze)
        {
            await AnalyzeAsync();
        }
        else
        {
            RefreshCatalogDependentViews();
        }

        var summary = $"{company}: rojo si cobertura < {edit.Thresholds.RedPercent:0}% (o sin stock), verde si >= {edit.Thresholds.HealthyPercent:0}%, {savedZones.Count} zonas externas, {savedIgnoredZones.Count} zonas excluidas, {savedPreparedArticles.Count} alistados sat., {savedTarimaSizes.Count} tamanos.";
        if (savedZones.Count == 0)
        {
            ShowMessage(
                "Ajustes guardados",
                summary + " Sin zonas externas: en el analisis todo el inventario se trataria como externo.",
                InfoBarSeverity.Warning);
        }
        else if (!willReanalyze)
        {
            ShowMessage("Ajustes guardados", summary + " Se aplicaran al ejecutar Analizar.", InfoBarSeverity.Success);
        }
    }

    private bool CanManageSettings() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanClearFilters))]
    private void ClearFilters()
    {
        var session = CurrentSession;
        ResetFilters(session);
        RestoreFiltersFromSession(session);
        ClearColumnRanges();
        ApplyFilters();
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanClearWarehouseComparisonFilters))]
    private void ClearWarehouseComparisonFilters()
    {
        ResetWarehouseComparisonFiltersForNewLoad();
        FilterWarehouseComparisonRows();
    }

    private void ResetWarehouseComparisonFiltersForNewLoad()
    {
        _warehouseComparisonTextSort = null;
        ComparisonArticuloFilter.ClearCommand.Execute(null);
        ComparisonDescripcionFilter.ClearCommand.Execute(null);
        ComparisonOloFilter.ClearCommand.Execute(null);
        ComparisonServicaFilter.ClearCommand.Execute(null);
        ComparisonCoverageFilter.ClearCommand.Execute(null);
        ComparisonComentariosFilter.ClearCommand.Execute(null);
    }

    private void ClearColumnRanges()
    {
        var wasRestoring = _isRestoringState;
        _isRestoringState = true;
        try
        {
            ForecastMinText = string.Empty;
            ForecastMaxText = string.Empty;
            PrincipalMinText = string.Empty;
            PrincipalMaxText = string.Empty;
            DiffMinText = string.Empty;
            DiffMaxText = string.Empty;
            SatMinText = string.Empty;
            SatMaxText = string.Empty;
            TableArticuloFilter.ClearCommand.Execute(null);
            TableForecastFilter.ClearCommand.Execute(null);
            TablePrincipalFilter.ClearCommand.Execute(null);
            TableDifferenceFilter.ClearCommand.Execute(null);
            TableStatusFilter.ClearCommand.Execute(null);
            TableSatelliteFilter.ClearCommand.Execute(null);
        }
        finally
        {
            _isRestoringState = wasRestoring;
        }
    }

    [RelayCommand]
    private void CloseMessage()
    {
        IsMessageOpen = false;
    }

    private async Task ExecuteBusyActionAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (WorkbookValidationException exception)
        {
            ShowMessage("Archivo invalido", exception.Message, InfoBarSeverity.Error);
        }
        catch (Exception exception)
        {
            ShowMessage("Operacion fallida", exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            LoadInventoryCommand.NotifyCanExecuteChanged();
            LoadForecastCommand.NotifyCanExecuteChanged();
            LoadExpedicionesInventoryCommand.NotifyCanExecuteChanged();
            AnalyzeCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
            ClearWarehouseComparisonFiltersCommand.NotifyCanExecuteChanged();
            ExportRequisitionCommand.NotifyCanExecuteChanged();
            ClearOrdersCommand.NotifyCanExecuteChanged();
            SendSelectedToTransitCommand.NotifyCanExecuteChanged();
            ConfirmSelectedTransitCommand.NotifyCanExecuteChanged();
            ConfirmVisibleTransitCommand.NotifyCanExecuteChanged();
        }
    }

    private void ApplySessionToUi()
    {
        var session = CurrentSession;
        _isRestoringState = true;
        try
        {
            UpdateVisibleLoadFileNames();
            ExpedicionesFileName = session.ExpedicionesFileName ?? "Expediciones pendiente";
            ExpedicionesInventoryFileName = session.ExpedicionesInventoryFileName ?? "Inventario de expediciones pendiente";
            ApplyWeeksFromSession();
            SearchText = session.SearchText;
            SelectedGeneralStatusOption = OptionFor(session.GeneralStatusFilter);
            SelectedSatelliteStatusOption = OptionFor(session.SatelliteStatusFilter);
        }
        finally
        {
            _isRestoringState = false;
        }

        ClearColumnRanges();
        ApplyAnalysis(session.LastAnalysis, preserveSelection: false);
        BuildExpedicionesView(session.LastExpedicionesAnalysis);
        BuildWarehouseComparisonView(session.LastWarehouseComparison);
        AnalyzeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ExportWarehouseComparisonCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(LoadSummaryText));
        OnPropertyChanged(nameof(LoadPanelVisibility));
    }

    private void ApplyWeeksFromSession()
    {
        var session = CurrentSession;
        var forecastWorkbook = ShowWarehouseComparisonView
            ? session.ComparisonForecastWorkbook
            : session.ForecastWorkbook;
        var granularity = ShowWarehouseComparisonView
            ? session.ComparisonGranularity
            : session.Granularity;
        var weekToSelect = ShowWarehouseComparisonView
            ? session.ComparisonSelectedPeriod
            : session.SelectedWeek;

        AvailableWeeks.Clear();
        if (forecastWorkbook is not null)
        {
            foreach (var week in forecastWorkbook.AvailableWeeks.OrderBy(week => week))
            {
                AvailableWeeks.Add(new WeekOptionViewModel(week, granularity));
            }
        }

        var wasRestoring = _isRestoringState;
        _isRestoringState = true;
        try
        {
            SelectedWeek =
                AvailableWeeks.FirstOrDefault(option => option.Value == weekToSelect) ??
                AvailableWeeks.FirstOrDefault();
        }
        finally
        {
            _isRestoringState = wasRestoring;
        }

        if (ShowWarehouseComparisonView)
        {
            session.ComparisonSelectedPeriod = SelectedWeek?.Value;
        }
        else
        {
            session.SelectedWeek = SelectedWeek?.Value;
        }

        RebuildHorizonOptions(session);

        OnPropertyChanged(nameof(SelectedWeekLabel));
        OnPropertyChanged(nameof(PeriodSelectorHeader));
        OnPropertyChanged(nameof(ForecastColumnHeader));
        OnPropertyChanged(nameof(ForecastHeaderCaption));
        OnPropertyChanged(nameof(LoadSummaryText));
    }

    private void RebuildHorizonOptions(CompanyAnalysisSession session)
    {
        var desired = session.Horizon;
        var wasRestoring = _isRestoringState;
        _isRestoringState = true;
        try
        {
            HorizonOptions.Clear();
            if (session.Granularity != ForecastGranularity.Monthly)
            {
                HorizonOptions.Add(new HorizonOptionViewModel(AnalysisHorizon.Weeks(4), "4 semanas"));
                HorizonOptions.Add(new HorizonOptionViewModel(AnalysisHorizon.Weeks(5), "5 semanas"));
            }

            HorizonOptions.Add(new HorizonOptionViewModel(AnalysisHorizon.Months(1), "1 mes"));
            HorizonOptions.Add(new HorizonOptionViewModel(AnalysisHorizon.Months(2), "2 meses"));
            HorizonOptions.Add(new HorizonOptionViewModel(AnalysisHorizon.Months(3), "3 meses"));

            SelectedHorizon =
                HorizonOptions.FirstOrDefault(option => option.Value == desired) ?? HorizonOptions[0];
            session.Horizon = SelectedHorizon.Value;
        }
        finally
        {
            _isRestoringState = wasRestoring;
        }
    }

    private void ApplyAnalysis(WeeklyAnalysisResult? analysisResult, bool preserveSelection = true)
    {
        var descriptions = CurrentDescriptions();
        _allResults = analysisResult is null
            ? Array.Empty<ArticleResultRowViewModel>()
            : analysisResult.Articles
            .Select(
                article => new ArticleResultRowViewModel(
                    article.Article,
                    descriptions.TryGetValue(article.Article, out var description) ? description : string.Empty,
                    article.PrincipalInventoryQuantity,
                    article.TotalInventoryQuantity,
                    article.ForecastQuantity,
                    article.Difference,
                    article.Status,
                    article.SatelliteInventoryQuantity,
                    article.SatelliteDifference,
                    article.SatelliteStatus,
                    article.TransferSuggestionQuantity,
                    article.RemainingShortageAfterTransfer,
                    article.TransferRecommendationState,
                    article.Zones
                        .OrderByDescending(zone => zone.IsSatellite)
                        .ThenBy(zone => zone.StorageZone, StringComparer.OrdinalIgnoreCase)
                        .Select(zone => new ZoneDetailRowViewModel(zone.StorageZone, zone.Quantity, zone.IsSatellite))
                        .ToArray()))
            .ToArray();

        OnPropertyChanged(nameof(SelectedWeekLabel));
        OnPropertyChanged(nameof(ForecastColumnHeader));
        OnPropertyChanged(nameof(ForecastHeaderCaption));
        BuildTransferView(analysisResult);
        ApplyFilters(preserveSelection);
    }

    private void ApplyFilters(bool preserveSelection = true)
    {
        var analysis = CurrentSession.LastAnalysis;
        if (analysis is null)
        {
            UpdateFilteredPresentation(null, Array.Empty<ArticleResultRowViewModel>(), preserveSelection);
            return;
        }

        RefreshTableFilterOptions(_allResults);

        var visibleResults = ApplyTableTextSort(_allResults
            .Where(MatchesColumnFilters))
            .ToArray();

        var visibleCodes = visibleResults
            .Select(result => result.Article)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var visibleArticles = analysis.Articles
            .Where(article => visibleCodes.Contains(article.Article))
            .ToArray();
        var filteredAnalysis = BuildFilteredResult(analysis, visibleArticles);

        UpdateFilteredPresentation(filteredAnalysis, visibleResults, preserveSelection);
    }

    private void RefreshTableFilterOptions(IReadOnlyList<ArticleResultRowViewModel> rows)
    {
        TableArticuloFilter.SetOptions(rows.Select(row => row.ArticleDescriptionText));
        TableForecastFilter.SetOptions(rows.Select(row => row.ForecastText));
        TablePrincipalFilter.SetOptions(rows.Select(row => row.PrincipalInventoryText));
        TableDifferenceFilter.SetOptions(rows.Select(row => row.DifferenceText));
        TableStatusFilter.SetOptions(rows.Select(row => row.StatusLabel));
        TableSatelliteFilter.SetOptions(rows.Select(row => row.SatelliteInventoryText));
    }

    private bool MatchesColumnFilters(ArticleResultRowViewModel row)
    {
        var search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search) &&
            !row.ArticleDescriptionText.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesStatus(row.Status, GeneralStatusFilter)
            && MatchesStatus(row.SatelliteStatus, SatelliteStatusFilter)
            && InRange(row.ForecastQuantity, ForecastMinText, ForecastMaxText)
            && InRange(row.PrincipalInventoryQuantity, PrincipalMinText, PrincipalMaxText)
            && InRange(row.Difference, DiffMinText, DiffMaxText)
            && InRange(row.SatelliteInventoryQuantity, SatMinText, SatMaxText)
            && TableArticuloFilter.Matches(row.ArticleDescriptionText)
            && TableForecastFilter.Matches(row.ForecastText)
            && TablePrincipalFilter.Matches(row.PrincipalInventoryText)
            && TableDifferenceFilter.Matches(row.DifferenceText)
            && TableStatusFilter.Matches(row.StatusLabel)
            && TableSatelliteFilter.Matches(row.SatelliteInventoryText);
    }

    private static bool MatchesStatus(CoverageStatus status, StatusFilterOption filter) =>
        filter switch
        {
            StatusFilterOption.Critical => status == CoverageStatus.Critical,
            StatusFilterOption.Warning => status == CoverageStatus.Warning,
            StatusFilterOption.Healthy => status == CoverageStatus.Healthy,
            _ => true,
        };

    private static bool InRange(decimal value, string? minText, string? maxText)
    {
        if (TryParseDecimal(minText, out var min) && value < min)
        {
            return false;
        }

        if (TryParseDecimal(maxText, out var max) && value > max)
        {
            return false;
        }

        return true;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            value = 0m;
            return false;
        }

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static WeeklyAnalysisResult BuildFilteredResult(
        WeeklyAnalysisResult source,
        IReadOnlyList<ArticleAnalysisResult> articles) =>
        new(
            source.SelectedWeek,
            source.HorizonStartWeek,
            source.HorizonEndWeek,
            source.RequestedWeeks,
            source.AnalyzedWeeks,
            source.IsIncompleteHorizon,
            articles,
            articles.Count(article => article.Status == CoverageStatus.Critical),
            articles.Count(article => article.Status == CoverageStatus.Warning),
            articles.Count(article => article.Status == CoverageStatus.Healthy),
            articles.Count(article => article.SatelliteStatus == CoverageStatus.Critical),
            articles.Count(article => article.SatelliteStatus == CoverageStatus.Warning),
            articles.Count(article => article.SatelliteStatus == CoverageStatus.Healthy),
            source.Horizon);

    private void UpdateFilteredPresentation(
        WeeklyAnalysisResult? filteredAnalysis,
        IReadOnlyList<ArticleResultRowViewModel> visibleResults,
        bool preserveSelection)
    {
        _filteredAnalysis = filteredAnalysis;
        var previousArticle = preserveSelection ? SelectedArticle?.Article : null;

        FilteredResults = visibleResults;
        UpdateCounts(filteredAnalysis);

        SelectedArticle =
            visibleResults.FirstOrDefault(result => result.Article.Equals(previousArticle, StringComparison.OrdinalIgnoreCase)) ??
            visibleResults.FirstOrDefault();

        ExportCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCounts(WeeklyAnalysisResult? analysisResult)
    {
        TotalArticles = analysisResult?.Articles.Count ?? 0;
        CriticalCount = analysisResult?.CriticalCount ?? 0;
        WarningCount = analysisResult?.WarningCount ?? 0;
        HealthyCount = analysisResult?.HealthyCount ?? 0;
        SatelliteCriticalCount = analysisResult?.SatelliteCriticalCount ?? 0;
        SatelliteWarningCount = analysisResult?.SatelliteWarningCount ?? 0;
        SatelliteHealthyCount = analysisResult?.SatelliteHealthyCount ?? 0;
    }

    private void ResetFilters(CompanyAnalysisSession session)
    {
        session.SearchText = string.Empty;
        session.GeneralStatusFilter = StatusFilterOption.All;
        session.SatelliteStatusFilter = StatusFilterOption.All;
    }

    private void RestoreFiltersFromSession(CompanyAnalysisSession session)
    {
        _isRestoringState = true;
        try
        {
            SearchText = session.SearchText;
            SelectedGeneralStatusOption = OptionFor(session.GeneralStatusFilter);
            SelectedSatelliteStatusOption = OptionFor(session.SatelliteStatusFilter);
        }
        finally
        {
            _isRestoringState = false;
        }
    }

    private void ResetTransferAnalysisUi()
    {
        CurrentSession.LastAnalysis = null;
        ApplyAnalysis(null);
    }

    private void ResetWarehouseComparisonUi()
    {
        CurrentSession.LastWarehouseComparison = null;
        BuildWarehouseComparisonView(null);
    }

    private void ShowMessage(string title, string text, InfoBarSeverity severity)
    {
        MessageTitle = title;
        MessageText = text;
        MessageSeverity = severity;
        IsMessageOpen = true;
    }

    private async Task<InventoryWorkbook> ParseInventoryWorkbookAsync(StorageFile file)
    {
        var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return _inventoryParser.Parse(stream);
        });
    }

    private async Task<ForecastWorkbook> ParseForecastWorkbookAsync(StorageFile file)
    {
        var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return _forecastParser.Parse(stream);
        });
    }

    private readonly record struct TextSortState(string Column, bool Ascending);
}
