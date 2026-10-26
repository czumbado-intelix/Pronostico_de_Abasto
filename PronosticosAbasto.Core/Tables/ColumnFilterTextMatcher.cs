using System.Text;

namespace PronosticosAbasto.Core.Tables;

public static class ColumnFilterTextMatcher
{
    public static bool ContainsNormalized(string? value, string? query)
    {
        var normalizedQuery = NormalizeForSearch(query);
        return normalizedQuery.Length == 0 ||
            NormalizeForSearch(value).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    public static bool EqualsNormalized(string? value, string? query)
    {
        var normalizedQuery = NormalizeForSearch(query);
        return normalizedQuery.Length > 0 &&
            string.Equals(NormalizeForSearch(value), normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
