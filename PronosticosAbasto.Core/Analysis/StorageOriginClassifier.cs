using System.Globalization;
using System.Text;

namespace PronosticosAbasto.Core.Analysis;

public static class StorageOriginClassifier
{
    private static readonly string[] CocoZonePrefixes = ["ZA47", "ZA48", "ZA49", "ZA50", "ZA52", "ZA55"];

    public static string Resolve(string storageZone)
    {
        var normalized = Normalize(storageZone);
        if (normalized.Contains("SERVICA", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("ZA46ZA45", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("ZA46", StringComparison.OrdinalIgnoreCase))
        {
            return "SERVICA";
        }

        if (normalized.Contains("GUACIMA", StringComparison.OrdinalIgnoreCase) ||
            CocoZonePrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return "COCO";
        }

        return "OTRAS";
    }

    private static string Normalize(string value)
    {
        var decomposed = (value ?? string.Empty).Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
