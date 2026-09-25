using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.ViewModels;

internal sealed class CompanyAnalysisSession
{
    public string? InventoryFileName { get; set; }

    public string? ForecastFileName { get; set; }

    public InventoryWorkbook? InventoryWorkbook { get; set; }

    public ForecastWorkbook? ForecastWorkbook { get; set; }

    public DateOnly? SelectedWeek { get; set; }

    public AnalysisHorizon Horizon { get; set; } = AnalysisHorizon.Weeks(4);

    public ForecastGranularity Granularity { get; set; } = ForecastGranularity.Weekly;

    public WeeklyAnalysisResult? LastAnalysis { get; set; }

    public string? ComparisonInventoryFileName { get; set; }

    public string? ComparisonForecastFileName { get; set; }

    public InventoryWorkbook? ComparisonInventoryWorkbook { get; set; }

    public ForecastWorkbook? ComparisonForecastWorkbook { get; set; }

    public ForecastGranularity ComparisonGranularity { get; set; } = ForecastGranularity.Weekly;

    public DateOnly? ComparisonSelectedPeriod { get; set; }

    public WarehouseComparisonResult? LastWarehouseComparison { get; set; }

    public string? ExpedicionesFileName { get; set; }

    public string? ExpedicionesInventoryFileName { get; set; }

    public ExpedicionesWorkbook? ExpedicionesWorkbook { get; set; }

    public InventoryWorkbook? ExpedicionesInventory { get; set; }

    public ExpedicionesAnalysisResult? LastExpedicionesAnalysis { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public StatusFilterOption GeneralStatusFilter { get; set; } = StatusFilterOption.All;

    public StatusFilterOption SatelliteStatusFilter { get; set; } = StatusFilterOption.All;
}
