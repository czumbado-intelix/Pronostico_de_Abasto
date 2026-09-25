using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.IO;

public enum ForecastGranularity
{
    Weekly = 0,
    Monthly = 1,
}

public sealed record ForecastWorkbook(
    IReadOnlyList<ForecastEntry> Entries,
    IReadOnlyList<DateOnly> AvailableWeeks,
    ForecastGranularity Granularity = ForecastGranularity.Weekly)
{
    /// <summary>Optional article code -> description, when the file carries one.</summary>
    public IReadOnlyDictionary<string, string> Descriptions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
