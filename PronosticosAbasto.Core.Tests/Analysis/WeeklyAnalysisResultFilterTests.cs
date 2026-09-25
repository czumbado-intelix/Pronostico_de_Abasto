using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class WeeklyAnalysisResultFilterTests
{
    [Fact]
    public void Apply_filters_by_article_fragment_case_insensitive_and_recalculates_counts()
    {
        var analysis = CreateAnalysis();

        var filtered = WeeklyAnalysisResultFilter.Apply(
            analysis,
            searchText: "abc",
            generalStatusFilter: StatusFilterOption.All,
            satelliteStatusFilter: StatusFilterOption.All);

        var article = Assert.Single(filtered.Articles);
        Assert.Equal("ABC999", article.Article);
        Assert.Equal(new DateOnly(2026, 6, 1), filtered.SelectedWeek);
        Assert.Equal(new DateOnly(2026, 6, 1), filtered.HorizonStartWeek);
        Assert.Equal(new DateOnly(2026, 6, 22), filtered.HorizonEndWeek);
        Assert.Equal(4, filtered.RequestedWeeks);
        Assert.Equal(4, filtered.AnalyzedWeeks);
        Assert.False(filtered.IsIncompleteHorizon);
        Assert.Equal(0, filtered.CriticalCount);
        Assert.Equal(0, filtered.WarningCount);
        Assert.Equal(1, filtered.HealthyCount);
        Assert.Equal(0, filtered.SatelliteCriticalCount);
        Assert.Equal(1, filtered.SatelliteWarningCount);
        Assert.Equal(0, filtered.SatelliteHealthyCount);
    }

    [Fact]
    public void Apply_combines_general_and_satellite_filters_with_and_semantics()
    {
        var analysis = CreateAnalysis();

        var filtered = WeeklyAnalysisResultFilter.Apply(
            analysis,
            searchText: string.Empty,
            generalStatusFilter: StatusFilterOption.Warning,
            satelliteStatusFilter: StatusFilterOption.Healthy);

        var article = Assert.Single(filtered.Articles);
        Assert.Equal("100000540", article.Article);
        Assert.Equal(2, article.Zones.Count);
        Assert.Contains(article.Zones, zone => zone.StorageZone == "EPA-PRINCIPAL");
        Assert.Contains(article.Zones, zone => zone.StorageZone == "SAT-1");
        Assert.Equal(0, filtered.CriticalCount);
        Assert.Equal(1, filtered.WarningCount);
        Assert.Equal(0, filtered.HealthyCount);
        Assert.Equal(0, filtered.SatelliteCriticalCount);
        Assert.Equal(0, filtered.SatelliteWarningCount);
        Assert.Equal(1, filtered.SatelliteHealthyCount);
    }

    [Fact]
    public void Apply_returns_empty_analysis_and_zero_counts_when_no_articles_match()
    {
        var analysis = CreateAnalysis();

        var filtered = WeeklyAnalysisResultFilter.Apply(
            analysis,
            searchText: "sin-coincidencias",
            generalStatusFilter: StatusFilterOption.All,
            satelliteStatusFilter: StatusFilterOption.All);

        Assert.Empty(filtered.Articles);
        Assert.Equal(new DateOnly(2026, 6, 1), filtered.SelectedWeek);
        Assert.Equal(0, filtered.CriticalCount);
        Assert.Equal(0, filtered.WarningCount);
        Assert.Equal(0, filtered.HealthyCount);
        Assert.Equal(0, filtered.SatelliteCriticalCount);
        Assert.Equal(0, filtered.SatelliteWarningCount);
        Assert.Equal(0, filtered.SatelliteHealthyCount);
    }

    private static WeeklyAnalysisResult CreateAnalysis()
    {
        return new WeeklyAnalysisResult(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 22),
            requestedWeeks: 4,
            analyzedWeeks: 4,
            isIncompleteHorizon: false,
            new[]
            {
                new ArticleAnalysisResult(
                    article: "100000513",
                    principalInventoryQuantity: 0,
                    totalInventoryQuantity: 0,
                    forecastQuantity: 4,
                    difference: -4,
                    status: CoverageStatus.Critical,
                    zones:
                    [
                        new ZoneInventoryDetail("EPA-PRINCIPAL", 0),
                        new ZoneInventoryDetail("SAT-ROJO", 0, isSatellite: true),
                    ],
                    satelliteInventoryQuantity: 0,
                    satelliteDifference: -4,
                    satelliteStatus: CoverageStatus.Critical,
                    transferSuggestionQuantity: 0,
                    remainingShortageAfterTransfer: 4,
                    transferRecommendationState: TransferRecommendationState.Unavailable,
                    satelliteZones:
                    [
                        new ZoneInventoryDetail("SAT-ROJO", 0, isSatellite: true),
                    ]),
                new ArticleAnalysisResult(
                    article: "100000540",
                    principalInventoryQuantity: 2,
                    totalInventoryQuantity: 3,
                    forecastQuantity: 6,
                    difference: -4,
                    status: CoverageStatus.Warning,
                    zones:
                    [
                        new ZoneInventoryDetail("EPA-PRINCIPAL", 2),
                        new ZoneInventoryDetail("SAT-1", 1, isSatellite: true),
                    ],
                    satelliteInventoryQuantity: 1,
                    satelliteDifference: 0,
                    satelliteStatus: CoverageStatus.Healthy,
                    transferSuggestionQuantity: 1,
                    remainingShortageAfterTransfer: 3,
                    transferRecommendationState: TransferRecommendationState.Partial,
                    satelliteZones:
                    [
                        new ZoneInventoryDetail("SAT-1", 1, isSatellite: true),
                    ]),
                new ArticleAnalysisResult(
                    article: "ABC999",
                    principalInventoryQuantity: 7,
                    totalInventoryQuantity: 9,
                    forecastQuantity: 4,
                    difference: 3,
                    status: CoverageStatus.Healthy,
                    zones:
                    [
                        new ZoneInventoryDetail("COFERSA-CENTRAL", 7),
                        new ZoneInventoryDetail("SAT-2", 2, isSatellite: true),
                    ],
                    satelliteInventoryQuantity: 2,
                    satelliteDifference: -2,
                    satelliteStatus: CoverageStatus.Warning,
                    transferSuggestionQuantity: 0,
                    remainingShortageAfterTransfer: 0,
                    transferRecommendationState: TransferRecommendationState.NotRequired,
                    satelliteZones:
                    [
                        new ZoneInventoryDetail("SAT-2", 2, isSatellite: true),
                    ]),
            },
            criticalCount: 1,
            warningCount: 1,
            healthyCount: 1,
            satelliteCriticalCount: 1,
            satelliteWarningCount: 1,
            satelliteHealthyCount: 1);
    }
}
