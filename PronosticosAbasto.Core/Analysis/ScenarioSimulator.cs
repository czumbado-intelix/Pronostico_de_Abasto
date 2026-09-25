namespace PronosticosAbasto.Core.Analysis;

/// <summary>Loaded data for one company feeding the scenario simulation.</summary>
public sealed record ScenarioCompanyData(
    IReadOnlyList<ForecastEntry> Forecast,
    IReadOnlyList<InventoryPosition> Inventory,
    IReadOnlyList<string> SatelliteZones,
    IReadOnlyList<string> IgnoredStorageZones,
    DateOnly StartWeek);

/// <summary>One period of the scenario: demand per company and the feasible transfer load.</summary>
public sealed record ScenarioPeriod(string Label, decimal EpaDemand, decimal CofersaDemand, decimal TransferLoad);

public sealed record ScenarioResult(IReadOnlyList<ScenarioPeriod> Periods, bool HasEpa, bool HasCofersa)
{
    public decimal MaxLoad => Periods.Count == 0 ? 0m : Periods.Max(period => period.TransferLoad);
}

/// <summary>
/// Projects, over a rolling horizon and with a safety-stock buffer, the demand of
/// each company and the feasible transfer workload per period (Σ of what can be
/// pulled from satellite to cover the principal shortage, using current stock).
/// Reuses the same zone split as the analyzers via <see cref="InventoryZoneClassifier"/>.
/// </summary>
public sealed class ScenarioSimulator
{
    public ScenarioResult Simulate(
        ScenarioCompanyData? epa,
        ScenarioCompanyData? cofersa,
        int horizonMonths,
        decimal safetyBufferPercent)
    {
        var factor = 1m + (safetyBufferPercent / 100m);
        var periods = CollectPeriods(epa, cofersa, Math.Max(1, horizonMonths));

        var epaSummary = epa is null ? null : InventoryZoneClassifier.Summarize(epa.Inventory, epa.SatelliteZones, epa.IgnoredStorageZones);
        var cofersaSummary = cofersa is null ? null : InventoryZoneClassifier.Summarize(cofersa.Inventory, cofersa.SatelliteZones, cofersa.IgnoredStorageZones);

        var result = new List<ScenarioPeriod>(periods.Length);
        foreach (var week in periods)
        {
            var epaDemand = DemandFor(epa, week) * factor;
            var cofersaDemand = DemandFor(cofersa, week) * factor;
            var load = TransferLoadFor(epa, epaSummary, week, factor)
                + TransferLoadFor(cofersa, cofersaSummary, week, factor);
            result.Add(new ScenarioPeriod(week.ToString("dd MMM"), epaDemand, cofersaDemand, load));
        }

        return new ScenarioResult(result, epa is not null, cofersa is not null);
    }

    private static DateOnly[] CollectPeriods(ScenarioCompanyData? a, ScenarioCompanyData? b, int horizonMonths)
    {
        DateOnly? start = null;
        if (a is not null)
        {
            start = a.StartWeek;
        }

        if (b is not null)
        {
            start = start is null ? b.StartWeek : (b.StartWeek < start.Value ? b.StartWeek : start.Value);
        }

        if (start is null)
        {
            return [];
        }

        var endExclusive = start.Value.AddMonths(horizonMonths);
        var weeks = new SortedSet<DateOnly>();
        AddWeeks(a, start.Value, endExclusive, weeks);
        AddWeeks(b, start.Value, endExclusive, weeks);
        return weeks.ToArray();
    }

    private static void AddWeeks(ScenarioCompanyData? data, DateOnly start, DateOnly endExclusive, SortedSet<DateOnly> weeks)
    {
        if (data is null)
        {
            return;
        }

        foreach (var entry in data.Forecast)
        {
            if (entry.Week >= start && entry.Week < endExclusive)
            {
                weeks.Add(entry.Week);
            }
        }
    }

    private static decimal DemandFor(ScenarioCompanyData? data, DateOnly week) =>
        data is null ? 0m : data.Forecast.Where(entry => entry.Week == week).Sum(entry => entry.Quantity);

    private static decimal TransferLoadFor(
        ScenarioCompanyData? data,
        IReadOnlyDictionary<string, ArticleInventorySummary>? summary,
        DateOnly week,
        decimal factor)
    {
        if (data is null || summary is null)
        {
            return 0m;
        }

        var load = 0m;
        foreach (var group in data.Forecast
            .Where(entry => entry.Week == week && !string.IsNullOrWhiteSpace(entry.Article))
            .GroupBy(entry => entry.Article.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var demand = group.Sum(entry => entry.Quantity) * factor;
            summary.TryGetValue(group.Key, out var article);
            var principal = article?.PrincipalQuantity ?? 0m;
            var satellite = article?.SatelliteQuantity ?? 0m;
            var shortage = Math.Max(0m, demand - principal);
            load += Math.Min(shortage, satellite);
        }

        return load;
    }
}
