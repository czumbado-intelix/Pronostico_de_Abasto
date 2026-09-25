namespace PronosticosAbasto.Core.Analysis;

public enum HorizonUnit
{
    Weeks = 0,
    Months = 1,
}

/// <summary>
/// Defines how far ahead the forecast is accumulated from the selected start
/// week: either a fixed number of weeks (the classic 4-week horizon) or a
/// rolling number of months. A rolling month advances from the start week into
/// the next calendar month if needed, so "1 month" from 22.06 covers up to
/// ~22.07 regardless of where the start week falls in the month.
/// </summary>
public readonly record struct AnalysisHorizon
{
    public AnalysisHorizon(HorizonUnit unit, int amount)
    {
        if (amount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "El horizonte debe abarcar al menos 1.");
        }

        Unit = unit;
        Amount = amount;
    }

    public HorizonUnit Unit { get; }

    public int Amount { get; }

    public static AnalysisHorizon Weeks(int count) => new(HorizonUnit.Weeks, count);

    public static AnalysisHorizon Months(int count) => new(HorizonUnit.Months, count);

    public string ToDescription() => Unit switch
    {
        HorizonUnit.Months => Amount == 1 ? "1 mes" : $"{Amount} meses",
        _ => Amount == 1 ? "1 semana" : $"{Amount} semanas",
    };

    public string ToShortLabel() => Unit switch
    {
        HorizonUnit.Months => $"{Amount}M",
        _ => $"{Amount}S",
    };
}
