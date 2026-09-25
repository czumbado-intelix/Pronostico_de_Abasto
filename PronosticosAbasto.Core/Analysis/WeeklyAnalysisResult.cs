namespace PronosticosAbasto.Core.Analysis;

public sealed class WeeklyAnalysisResult
{
    public WeeklyAnalysisResult(
        DateOnly selectedWeek,
        IReadOnlyList<ArticleAnalysisResult> articles,
        int criticalCount,
        int warningCount,
        int healthyCount)
        : this(
            selectedWeek,
            selectedWeek,
            selectedWeek,
            1,
            1,
            false,
            articles,
            criticalCount,
            warningCount,
            healthyCount,
            criticalCount,
            warningCount,
            healthyCount)
    {
    }

    public WeeklyAnalysisResult(
        DateOnly selectedWeek,
        IReadOnlyList<ArticleAnalysisResult> articles,
        int criticalCount,
        int warningCount,
        int healthyCount,
        int satelliteCriticalCount,
        int satelliteWarningCount,
        int satelliteHealthyCount)
        : this(
            selectedWeek,
            selectedWeek,
            selectedWeek,
            1,
            1,
            false,
            articles,
            criticalCount,
            warningCount,
            healthyCount,
            satelliteCriticalCount,
            satelliteWarningCount,
            satelliteHealthyCount)
    {
    }

    public WeeklyAnalysisResult(
        DateOnly selectedWeek,
        DateOnly horizonStartWeek,
        DateOnly horizonEndWeek,
        int requestedWeeks,
        int analyzedWeeks,
        bool isIncompleteHorizon,
        IReadOnlyList<ArticleAnalysisResult> articles,
        int criticalCount,
        int warningCount,
        int healthyCount,
        int satelliteCriticalCount,
        int satelliteWarningCount,
        int satelliteHealthyCount,
        AnalysisHorizon? horizon = null)
    {
        SelectedWeek = selectedWeek;
        HorizonStartWeek = horizonStartWeek;
        HorizonEndWeek = horizonEndWeek;
        RequestedWeeks = requestedWeeks;
        AnalyzedWeeks = analyzedWeeks;
        IsIncompleteHorizon = isIncompleteHorizon;
        Horizon = horizon ?? AnalysisHorizon.Weeks(Math.Max(1, requestedWeeks));
        Articles = articles;
        CriticalCount = criticalCount;
        WarningCount = warningCount;
        HealthyCount = healthyCount;
        SatelliteCriticalCount = satelliteCriticalCount;
        SatelliteWarningCount = satelliteWarningCount;
        SatelliteHealthyCount = satelliteHealthyCount;
    }

    public DateOnly SelectedWeek { get; }

    public DateOnly HorizonStartWeek { get; }

    public DateOnly HorizonEndWeek { get; }

    public int RequestedWeeks { get; }

    public int AnalyzedWeeks { get; }

    public bool IsIncompleteHorizon { get; }

    public AnalysisHorizon Horizon { get; }

    public string HorizonDescription => Horizon.ToDescription();

    public string HorizonShortLabel => Horizon.ToShortLabel();

    public IReadOnlyList<ArticleAnalysisResult> Articles { get; }

    public int CriticalCount { get; }

    public int WarningCount { get; }

    public int HealthyCount { get; }

    public int SatelliteCriticalCount { get; }

    public int SatelliteWarningCount { get; }

    public int SatelliteHealthyCount { get; }
}
