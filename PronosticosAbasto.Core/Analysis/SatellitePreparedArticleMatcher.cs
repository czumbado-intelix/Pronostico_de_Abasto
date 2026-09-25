using System.Globalization;
using System.Text;

namespace PronosticosAbasto.Core.Analysis;

public static class SatellitePreparedArticleMatcher
{
    private static readonly string[] PreparedDescriptionPhrases =
    [
        "RODAPIE",
        "PISO DECK",
        "DECK",
    ];

    public static bool IsPrepared(
        IReadOnlySet<string> preparedArticles,
        string article,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(preparedArticles);

        if (IsCatalogArticle(preparedArticles, article))
        {
            return true;
        }

        return MatchesPreparedDescription(description);
    }

    private static bool IsCatalogArticle(IReadOnlySet<string> preparedArticles, string article)
    {
        if (preparedArticles.Contains(article))
        {
            return true;
        }

        var normalized = InventoryZoneClassifier.NormalizeArticleKey(article);
        return !string.IsNullOrWhiteSpace(normalized) && preparedArticles.Contains(normalized);
    }

    private static bool MatchesPreparedDescription(string? description)
    {
        var normalized = NormalizeDescription(description);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var searchable = $" {normalized} ";
        return PreparedDescriptionPhrases.Any(phrase => searchable.Contains($" {phrase} ", StringComparison.Ordinal));
    }

    private static string NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSeparator = true;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim();
    }
}
