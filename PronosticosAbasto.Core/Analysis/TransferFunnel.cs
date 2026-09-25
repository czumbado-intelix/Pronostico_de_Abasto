namespace PronosticosAbasto.Core.Analysis;

/// <summary>
/// Live counts that map the decision flowchart onto an analysis result:
/// Compare forecast vs principal -> does principal cover? -> if not, is there
/// external (satellite) stock to transfer? Only articles actually requested by
/// the forecast (ForecastQuantity &gt; 0) are counted.
/// </summary>
public sealed record TransferFunnel(
    int Evaluated,
    int Covered,
    int Short,
    int ToTransfer,
    int WithoutExternal)
{
    public static TransferFunnel From(WeeklyAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var evaluated = 0;
        var covered = 0;
        var shortfall = 0;
        var toTransfer = 0;
        var withoutExternal = 0;

        foreach (var article in analysis.Articles)
        {
            if (article.ForecastQuantity <= 0 && article.PendingTransitQuantity <= 0)
            {
                continue;
            }

            evaluated++;

            if (article.Difference >= 0 && article.PendingTransitQuantity <= 0)
            {
                covered++;
                continue;
            }

            shortfall++;

            if (article.TransferSuggestionQuantity > 0)
            {
                toTransfer++;
            }
            else
            {
                withoutExternal++;
            }
        }

        return new TransferFunnel(evaluated, covered, shortfall, toTransfer, withoutExternal);
    }
}
