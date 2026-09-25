using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Analysis;

/// <summary>
/// For each expedition line, compares its demand against the principal warehouse
/// and, when short, suggests how much to bring from the satellite warehouses —
/// the same transfer math as <see cref="WeeklyForecastAnalyzer"/> but driven by
/// the dispatch report. Expedition demand is grouped by article before comparing
/// against principal stock so the same internal inventory is not reused across
/// repeated expedition lines for the same article.
/// </summary>
public sealed class ExpedicionesAnalyzer
{
    public ExpedicionesAnalysisResult Analyze(
        IEnumerable<ExpeditionLine> lines,
        IEnumerable<InventoryPosition> inventory,
        IEnumerable<string>? satelliteZones = null,
        IReadOnlyDictionary<string, decimal>? pendingTransitQuantities = null,
        IEnumerable<string>? ignoredStorageZones = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(inventory);

        var summaries = InventoryZoneClassifier.Summarize(inventory, satelliteZones, ignoredStorageZones);
        var pendingTransit = NormalizeQuantities(pendingTransitQuantities);

        var results = new List<ExpeditionLineResult>();
        foreach (var articleGroup in lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Article))
            .GroupBy(
                line => InventoryZoneClassifier.NormalizeArticleKey(line.Article),
                StringComparer.OrdinalIgnoreCase))
        {
            summaries.TryGetValue(articleGroup.Key, out var summary);

            var firstLine = articleGroup.First();
            var demandQuantity = articleGroup.Sum(line => line.Quantity);
            var description = articleGroup
                .Select(line => line.Description)
                .FirstOrDefault(description => !string.IsNullOrWhiteSpace(description)) ?? string.Empty;
            var expeditionNumber = string.Join(
                "; ",
                articleGroup
                    .Select(line => line.ExpeditionNumber?.Trim() ?? string.Empty)
                    .Where(expedition => !string.IsNullOrWhiteSpace(expedition))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            var principal = summary?.PrincipalQuantity ?? 0m;
            var satellite = summary?.SatelliteQuantity ?? 0m;
            var satelliteZoneDetails = summary?.SatelliteZones ?? Array.Empty<ZoneInventoryDetail>();
            var pendingTransitQuantity = pendingTransit.GetValueOrDefault(
                articleGroup.Key,
                0m);
            var transferPlan = TransferPlanner.Plan(
                demandQuantity,
                principal,
                satellite,
                pendingTransitQuantity,
                summary?.SatellitePallets ?? Array.Empty<PalletInventoryDetail>());

            results.Add(new ExpeditionLineResult(
                firstLine.Article,
                description,
                expeditionNumber,
                demandQuantity,
                principal,
                satellite,
                transferPlan.TransferSuggestionQuantity,
                transferPlan.RemainingShortageAfterTransfer,
                transferPlan.RecommendationState,
                satelliteZoneDetails,
                pendingTransitQuantity,
                transferPlan.NewTransferSuggestionQuantity,
                transferPlan.SuggestedPallets,
                transferPlan.CalculatedNeedQuantity,
                transferPlan.PalletRoundUpSurplusQuantity,
                summary?.SatellitePallets ?? Array.Empty<PalletInventoryDetail>()));
        }

        return new ExpedicionesAnalysisResult(results);
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
}
