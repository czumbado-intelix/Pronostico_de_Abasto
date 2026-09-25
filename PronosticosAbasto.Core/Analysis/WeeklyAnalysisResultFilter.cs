namespace PronosticosAbasto.Core.Analysis;

public static class WeeklyAnalysisResultFilter
{
    public static WeeklyAnalysisResult Apply(
        WeeklyAnalysisResult analysisResult,
        string? searchText,
        StatusFilterOption generalStatusFilter,
        StatusFilterOption satelliteStatusFilter)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var normalizedSearch = searchText?.Trim();
        var filteredArticles = analysisResult.Articles
            .Where(article =>
                MatchesSearch(article, normalizedSearch) &&
                MatchesStatus(article.Status, generalStatusFilter) &&
                MatchesStatus(article.SatelliteStatus, satelliteStatusFilter))
            .ToArray();

        return new WeeklyAnalysisResult(
            analysisResult.SelectedWeek,
            analysisResult.HorizonStartWeek,
            analysisResult.HorizonEndWeek,
            analysisResult.RequestedWeeks,
            analysisResult.AnalyzedWeeks,
            analysisResult.IsIncompleteHorizon,
            filteredArticles,
            criticalCount: filteredArticles.Count(article => article.Status == CoverageStatus.Critical),
            warningCount: filteredArticles.Count(article => article.Status == CoverageStatus.Warning),
            healthyCount: filteredArticles.Count(article => article.Status == CoverageStatus.Healthy),
            satelliteCriticalCount: filteredArticles.Count(article => article.SatelliteStatus == CoverageStatus.Critical),
            satelliteWarningCount: filteredArticles.Count(article => article.SatelliteStatus == CoverageStatus.Warning),
            satelliteHealthyCount: filteredArticles.Count(article => article.SatelliteStatus == CoverageStatus.Healthy),
            analysisResult.Horizon);
    }

    private static bool MatchesSearch(ArticleAnalysisResult article, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return article.Article.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatus(CoverageStatus status, StatusFilterOption filter) =>
        filter switch
        {
            StatusFilterOption.All => true,
            StatusFilterOption.Critical => status == CoverageStatus.Critical,
            StatusFilterOption.Warning => status == CoverageStatus.Warning,
            StatusFilterOption.Healthy => status == CoverageStatus.Healthy,
            _ => true,
        };
}
