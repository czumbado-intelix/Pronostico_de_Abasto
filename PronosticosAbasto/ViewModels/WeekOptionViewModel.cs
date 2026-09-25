using System.Globalization;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.ViewModels;

public sealed class WeekOptionViewModel
{
    public WeekOptionViewModel(DateOnly value, ForecastGranularity granularity = ForecastGranularity.Weekly)
    {
        Value = value;
        Label = granularity == ForecastGranularity.Monthly
            ? value.ToString("MMM yyyy", CultureInfo.InvariantCulture)
            : value.ToString("dd.MM.yy", CultureInfo.InvariantCulture);
    }

    public DateOnly Value { get; }

    public string Label { get; }
}
