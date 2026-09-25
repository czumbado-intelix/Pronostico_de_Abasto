namespace PronosticosAbasto.Core.Analysis;

public sealed class WeeklyForecastAnalyzer
{
    public WeeklyAnalysisResult Analyze(
        IEnumerable<InventoryPosition> inventory,
        IEnumerable<ForecastEntry> forecast,
        DateOnly selectedWeek,
        IEnumerable<string>? satelliteZones = null,
        IReadOnlyDictionary<string, decimal>? pendingTransitQuantities = null,
        IEnumerable<string>? ignoredStorageZones = null)
        => Analyze(
            inventory,
            forecast,
            selectedWeek,
            AnalysisHorizon.Weeks(4),
            CoverageThresholds.Default,
            satelliteZones,
            pendingTransitQuantities,
            ignoredStorageZones);

    public WeeklyAnalysisResult Analyze(
        IEnumerable<InventoryPosition> inventory,
        IEnumerable<ForecastEntry> forecast,
        DateOnly selectedWeek,
        AnalysisHorizon horizon,
        IEnumerable<string>? satelliteZones = null,
        IReadOnlyDictionary<string, decimal>? pendingTransitQuantities = null,
        IEnumerable<string>? ignoredStorageZones = null)
        => Analyze(
            inventory,
            forecast,
            selectedWeek,
            horizon,
            CoverageThresholds.Default,
            satelliteZones,
            pendingTransitQuantities,
            ignoredStorageZones);

    public WeeklyAnalysisResult Analyze(
        IEnumerable<InventoryPosition> inventory,
        IEnumerable<ForecastEntry> forecast,
        DateOnly selectedWeek,
        AnalysisHorizon horizon,
        CoverageThresholds thresholds,
        IEnumerable<string>? satelliteZones = null,
        IReadOnlyDictionary<string, decimal>? pendingTransitQuantities = null,
        IEnumerable<string>? ignoredStorageZones = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(forecast);

        var pendingTransit = NormalizeQuantities(pendingTransitQuantities);

        var futureWeeks = forecast
            .Select(entry => entry.Week)
            .Where(week => week >= selectedWeek)
            .Distinct()
            .OrderBy(week => week)
            .ToArray();

        var (horizonWeeks, requestedWeeks, isIncompleteHorizon) = ResolveHorizon(futureWeeks, selectedWeek, horizon);

        var analyzedWeeks = horizonWeeks.Length;
        var horizonEndWeek = analyzedWeeks > 0
            ? horizonWeeks[^1]
            : selectedWeek;
        var horizonWeekSet = horizonWeeks.ToHashSet();

        var inventoryGroups = InventoryZoneClassifier.Summarize(inventory, satelliteZones, ignoredStorageZones);

        var forecastGroups = forecast
            .Where(entry => horizonWeekSet.Contains(entry.Week) && !string.IsNullOrWhiteSpace(entry.Article))
            .GroupBy(entry => InventoryZoneClassifier.NormalizeArticleKey(entry.Article), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Article = group.First().Article.Trim(),
                    ForecastQuantity = group.Sum(entry => entry.Quantity),
                    WeeklyDemands = group
                        .GroupBy(entry => entry.Week)
                        .ToDictionary(weekGroup => weekGroup.Key, weekGroup => weekGroup.Sum(entry => entry.Quantity)),
                },
                StringComparer.OrdinalIgnoreCase);

        var articles = inventoryGroups.Keys
            .Union(forecastGroups.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                inventoryGroups.TryGetValue(key, out var inventoryInfo);
                forecastGroups.TryGetValue(key, out var forecastInfo);

                var article = inventoryInfo?.Article ?? forecastInfo!.Article;
                var principalInventoryQuantity = inventoryInfo?.PrincipalQuantity ?? 0m;
                var totalInventoryQuantity = inventoryInfo?.TotalQuantity ?? 0m;
                var satelliteInventoryQuantity = inventoryInfo?.SatelliteQuantity ?? 0m;
                var forecastQuantity = forecastInfo?.ForecastQuantity ?? 0m;
                var difference = principalInventoryQuantity - forecastQuantity;
                var satelliteDifference = satelliteInventoryQuantity - forecastQuantity;
                var status = ResolveStatus(principalInventoryQuantity, forecastQuantity, thresholds);
                var satelliteStatus = ResolveStatus(satelliteInventoryQuantity, forecastQuantity, thresholds);
                var pendingTransitQuantity = pendingTransit.GetValueOrDefault(key, 0m);
                var transferPlan = TransferPlanner.Plan(
                    forecastQuantity,
                    principalInventoryQuantity,
                    satelliteInventoryQuantity,
                    pendingTransitQuantity,
                    inventoryInfo?.SatellitePallets ?? Array.Empty<PalletInventoryDetail>());
                var coverageWeeks = ComputeCoverageWeeks(
                    principalInventoryQuantity,
                    forecastInfo?.WeeklyDemands,
                    horizonWeeks);

                return new ArticleAnalysisResult(
                    article,
                    principalInventoryQuantity,
                    totalInventoryQuantity,
                    forecastQuantity,
                    difference,
                    status,
                    inventoryInfo?.Zones ?? Array.Empty<ZoneInventoryDetail>(),
                    satelliteInventoryQuantity,
                    satelliteDifference,
                    satelliteStatus,
                    transferPlan.TransferSuggestionQuantity,
                    transferPlan.RemainingShortageAfterTransfer,
                    transferPlan.RecommendationState,
                    inventoryInfo?.SatelliteZones ?? Array.Empty<ZoneInventoryDetail>())
                {
                    CoverageWeeks = coverageWeeks,
                    CalculatedTransferNeedQuantity = transferPlan.CalculatedNeedQuantity,
                    PalletRoundUpSurplusQuantity = transferPlan.PalletRoundUpSurplusQuantity,
                    PendingTransitQuantity = pendingTransitQuantity,
                    NewTransferSuggestionQuantity = transferPlan.NewTransferSuggestionQuantity,
                    SuggestedPallets = transferPlan.SuggestedPallets,
                    AvailablePallets = inventoryInfo?.SatellitePallets ?? Array.Empty<PalletInventoryDetail>(),
                };
            })
            .OrderBy(result => result.Status)
            .ThenBy(result => result.SatelliteStatus)
            .ThenBy(result => result.Article, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WeeklyAnalysisResult(
            selectedWeek,
            selectedWeek,
            horizonEndWeek,
            requestedWeeks,
            analyzedWeeks,
            isIncompleteHorizon,
            articles,
            articles.Count(result => result.Status == CoverageStatus.Critical),
            articles.Count(result => result.Status == CoverageStatus.Warning),
            articles.Count(result => result.Status == CoverageStatus.Healthy),
            articles.Count(result => result.SatelliteStatus == CoverageStatus.Critical),
            articles.Count(result => result.SatelliteStatus == CoverageStatus.Warning),
            articles.Count(result => result.SatelliteStatus == CoverageStatus.Healthy),
            horizon);
    }

    private static (DateOnly[] HorizonWeeks, int RequestedWeeks, bool IsIncompleteHorizon) ResolveHorizon(
        DateOnly[] futureWeeks,
        DateOnly selectedWeek,
        AnalysisHorizon horizon)
    {
        if (horizon.Unit == HorizonUnit.Months)
        {
            var endExclusive = selectedWeek.AddMonths(horizon.Amount);
            var monthWeeks = futureWeeks.Where(week => week < endExclusive).ToArray();
            var lastAvailableWeek = futureWeeks.Length > 0 ? futureWeeks[^1] : selectedWeek;

            // The rolling-month horizon is partial when the forecast data runs
            // out more than a week before the end of the requested range.
            var isIncomplete = lastAvailableWeek < endExclusive.AddDays(-7);
            return (monthWeeks, monthWeeks.Length, isIncomplete);
        }

        var requestedWeeks = horizon.Amount;
        var weekModeWeeks = futureWeeks.Take(requestedWeeks).ToArray();
        return (weekModeWeeks, requestedWeeks, weekModeWeeks.Length < requestedWeeks);
    }

    /// <summary>
    /// Forward coverage: walks the horizon periods in order, spending the
    /// principal stock against each period's demand. Only fully covered
    /// periods count; the period where stock runs out does not. Periods with
    /// no demand are covered by definition. Null when the article has no demand.
    /// </summary>
    private static decimal? ComputeCoverageWeeks(
        decimal principalInventoryQuantity,
        IReadOnlyDictionary<DateOnly, decimal>? weeklyDemands,
        DateOnly[] horizonWeeks)
    {
        if (horizonWeeks.Length == 0 || weeklyDemands is null || weeklyDemands.Values.Sum() <= 0)
        {
            return null;
        }

        var remaining = principalInventoryQuantity;
        decimal covered = 0m;
        foreach (var week in horizonWeeks)
        {
            var demand = weeklyDemands.GetValueOrDefault(week, 0m);
            if (demand <= 0)
            {
                covered += 1;
                continue;
            }

            if (remaining < demand)
            {
                break;
            }

            covered += 1;
            remaining -= demand;
        }

        return covered;
    }

    private static IReadOnlyDictionary<string, decimal> NormalizeQuantities(IReadOnlyDictionary<string, decimal>? quantities)
    {
        if (quantities is null || quantities.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        return quantities
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .GroupBy(pair => InventoryZoneClassifier.NormalizeArticleKey(pair.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static CoverageStatus ResolveStatus(decimal inventoryQuantity, decimal forecastQuantity, CoverageThresholds thresholds)
    {
        if (forecastQuantity <= 0)
        {
            return CoverageStatus.Healthy;
        }

        if (inventoryQuantity <= 0)
        {
            return CoverageStatus.Critical;
        }

        if (inventoryQuantity < forecastQuantity * (thresholds.RedPercent / 100m))
        {
            return CoverageStatus.Critical;
        }

        return inventoryQuantity >= forecastQuantity * (thresholds.HealthyPercent / 100m)
            ? CoverageStatus.Healthy
            : CoverageStatus.Warning;
    }

}
