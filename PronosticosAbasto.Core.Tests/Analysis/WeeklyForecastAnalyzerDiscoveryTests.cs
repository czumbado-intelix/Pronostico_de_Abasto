using System.Reflection;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class WeeklyForecastAnalyzerDiscoveryTests
{
    [Fact]
    public void WeeklyForecastAnalyzer_type_is_exposed_by_the_core_library()
    {
        var analyzerType = typeof(PronosticosAbasto.Core.Analysis.WeeklyForecastAnalyzer).Assembly.GetType(
            "PronosticosAbasto.Core.Analysis.WeeklyForecastAnalyzer");

        Assert.NotNull(analyzerType);
    }
}
