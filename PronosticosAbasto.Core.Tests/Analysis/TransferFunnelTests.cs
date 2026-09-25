using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class TransferFunnelTests
{
    [Fact]
    public void From_classifies_each_article_into_the_flow_buckets()
    {
        var articles = new[]
        {
            // Covered: principal >= forecast.
            Article("COVERED", forecast: 10, principal: 12, satellite: 0, transfer: 0, remaining: 0, TransferRecommendationState.NotRequired),
            // To transfer (satellite fully covers the shortage).
            Article("SUGGESTED", forecast: 10, principal: 0, satellite: 10, transfer: 10, remaining: 0, TransferRecommendationState.Suggested),
            // To transfer (satellite helps but a gap remains).
            Article("PARTIAL", forecast: 10, principal: 2, satellite: 3, transfer: 3, remaining: 5, TransferRecommendationState.Partial),
            // Short with no external stock.
            Article("UNAVAILABLE", forecast: 10, principal: 1, satellite: 0, transfer: 0, remaining: 9, TransferRecommendationState.Unavailable),
            // No forecast -> not evaluated.
            Article("NO-FORECAST", forecast: 0, principal: 5, satellite: 0, transfer: 0, remaining: 0, TransferRecommendationState.NotRequired),
        };

        var analysis = new WeeklyAnalysisResult(new DateOnly(2026, 6, 1), articles, criticalCount: 0, warningCount: 0, healthyCount: 0);

        var funnel = TransferFunnel.From(analysis);

        Assert.Equal(4, funnel.Evaluated);
        Assert.Equal(1, funnel.Covered);
        Assert.Equal(3, funnel.Short);
        Assert.Equal(2, funnel.ToTransfer);
        Assert.Equal(1, funnel.WithoutExternal);
    }

    private static ArticleAnalysisResult Article(
        string code,
        decimal forecast,
        decimal principal,
        decimal satellite,
        decimal transfer,
        decimal remaining,
        TransferRecommendationState state) =>
        new(
            code,
            principal,
            principal + satellite,
            forecast,
            principal - forecast,
            CoverageStatus.Critical,
            Array.Empty<ZoneInventoryDetail>(),
            satellite,
            satellite - forecast,
            CoverageStatus.Critical,
            transfer,
            remaining,
            state,
            Array.Empty<ZoneInventoryDetail>());
}
