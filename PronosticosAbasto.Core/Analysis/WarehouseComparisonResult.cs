namespace PronosticosAbasto.Core.Analysis;

public sealed record WarehouseComparisonResult(
    DateOnly StartPeriod,
    DateOnly EndPeriod,
    int PeriodCount,
    IReadOnlyList<WarehouseComparisonRow> Rows);

public sealed record WarehouseComparisonRow(
    string Article,
    string Description,
    decimal OloQuantity,
    decimal ServicaQuantity,
    decimal ForecastQuantity,
    decimal? CoveragePeriods,
    bool HasFutureForecast,
    string AutomaticComment)
{
    public decimal TotalQuantity => OloQuantity + ServicaQuantity;
}

public sealed record WarehouseComparisonHistogramBucket(
    string Label,
    int ArticleCount,
    decimal Percentage);
