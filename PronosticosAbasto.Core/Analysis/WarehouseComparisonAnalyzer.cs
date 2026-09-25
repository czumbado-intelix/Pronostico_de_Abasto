using System.Globalization;
using System.Text;

namespace PronosticosAbasto.Core.Analysis;

public sealed class WarehouseComparisonAnalyzer
{
    public WarehouseComparisonResult Analyze(
        IEnumerable<InventoryPosition> inventory,
        IEnumerable<ForecastEntry> forecast,
        IEnumerable<DateOnly> availablePeriods,
        DateOnly startPeriod,
        IEnumerable<string>? ignoredStorageZones = null,
        IReadOnlyDictionary<string, string>? referenceDescriptions = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(forecast);
        ArgumentNullException.ThrowIfNull(availablePeriods);

        var ignoredZoneSet = BuildStorageZoneSet(ignoredStorageZones);
        var futurePeriods = availablePeriods
            .Where(period => period >= startPeriod)
            .Distinct()
            .OrderBy(period => period)
            .ToArray();

        if (futurePeriods.Length == 0)
        {
            futurePeriods = forecast
                .Select(entry => entry.Week)
                .Where(period => period >= startPeriod)
                .Distinct()
                .OrderBy(period => period)
                .ToArray();
        }

        var futurePeriodSet = futurePeriods.ToHashSet();
        var validInventory = inventory
            .Where(position => !string.IsNullOrWhiteSpace(position.Article))
            .Where(position => !IsMerma(position.PalletType))
            .Where(position => !IsIgnoredStorageZone(position.StorageZone, ignoredZoneSet))
            .ToArray();

        var inventoryGroups = validInventory
            .GroupBy(position => InventoryZoneClassifier.NormalizeArticleKey(position.Article), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var oloQuantity = group
                        .Where(position => !IsServica(position.StorageZone))
                        .Sum(position => position.Quantity);
                    var servicaQuantity = group
                        .Where(position => IsServica(position.StorageZone))
                        .Sum(position => position.Quantity);
                    var description = group
                        .Select(position => position.Description?.Trim())
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

                    return new InventoryComparisonGroup(group.First().Article.Trim(), description, oloQuantity, servicaQuantity);
                },
                StringComparer.OrdinalIgnoreCase);

        var forecastGroups = forecast
            .Where(entry => futurePeriodSet.Contains(entry.Week) && !string.IsNullOrWhiteSpace(entry.Article))
            .GroupBy(entry => InventoryZoneClassifier.NormalizeArticleKey(entry.Article), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var periodDemands = group
                        .GroupBy(entry => entry.Week)
                        .ToDictionary(weekGroup => weekGroup.Key, weekGroup => weekGroup.Sum(entry => entry.Quantity));

                    return new ForecastComparisonGroup(
                        group.First().Article.Trim(),
                        periodDemands.Values.Sum(),
                        periodDemands);
                },
                StringComparer.OrdinalIgnoreCase);

        var articleKeys = inventoryGroups.Keys
            .Union(forecastGroups.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = articleKeys
            .Select(key =>
            {
                inventoryGroups.TryGetValue(key, out var inventoryInfo);
                forecastGroups.TryGetValue(key, out var forecastInfo);

                var article = inventoryInfo?.Article ?? forecastInfo?.Article ?? key;
                var description = ResolveDescription(article, inventoryInfo, referenceDescriptions);
                var oloQuantity = inventoryInfo?.OloQuantity ?? 0m;
                var servicaQuantity = inventoryInfo?.ServicaQuantity ?? 0m;
                var forecastQuantity = forecastInfo?.ForecastQuantity ?? 0m;
                var hasFutureForecast = forecastQuantity > 0m;
                var coveragePeriods = ComputeCoveragePeriods(oloQuantity, forecastInfo?.PeriodDemands, futurePeriods);
                var automaticComment = hasFutureForecast ? string.Empty : "no solicita";

                return new WarehouseComparisonRow(
                    article,
                    description,
                    oloQuantity,
                    servicaQuantity,
                    forecastQuantity,
                    coveragePeriods,
                    hasFutureForecast,
                    automaticComment);
            })
            .OrderBy(row => row.CoveragePeriods ?? decimal.MaxValue)
            .ThenByDescending(row => row.ForecastQuantity)
            .ThenBy(row => row.Article, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WarehouseComparisonResult(
            startPeriod,
            futurePeriods.Length > 0 ? futurePeriods[^1] : startPeriod,
            futurePeriods.Length,
            rows);
    }

    private static decimal? ComputeCoveragePeriods(
        decimal inventoryQuantity,
        IReadOnlyDictionary<DateOnly, decimal>? periodDemands,
        DateOnly[] futurePeriods)
    {
        if (futurePeriods.Length == 0 || periodDemands is null || periodDemands.Values.Sum() <= 0m)
        {
            return null;
        }

        if (inventoryQuantity <= 0m)
        {
            return 0m;
        }

        var remaining = inventoryQuantity;
        decimal covered = 0m;
        foreach (var period in futurePeriods)
        {
            var demand = periodDemands.GetValueOrDefault(period, 0m);
            if (demand <= 0m)
            {
                covered += 1m;
                continue;
            }

            if (remaining < demand)
            {
                break;
            }

            covered += 1m;
            remaining -= demand;
        }

        return covered;
    }

    private static string ResolveDescription(
        string article,
        InventoryComparisonGroup? inventoryInfo,
        IReadOnlyDictionary<string, string>? referenceDescriptions)
    {
        if (!string.IsNullOrWhiteSpace(inventoryInfo?.Description))
        {
            return inventoryInfo.Description;
        }

        if (referenceDescriptions is null)
        {
            return string.Empty;
        }

        foreach (var key in BuildArticleDescriptionKeys(article))
        {
            if (referenceDescriptions.TryGetValue(key, out var referenceDescription) &&
                !string.IsNullOrWhiteSpace(referenceDescription))
            {
                return referenceDescription.Trim();
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> BuildArticleDescriptionKeys(string article)
    {
        var normalized = InventoryZoneClassifier.NormalizeArticleKey(article);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            yield return normalized;
        }

        if (normalized.All(char.IsDigit))
        {
            var withoutLeadingZeroes = normalized.TrimStart('0');
            yield return string.IsNullOrEmpty(withoutLeadingZeroes) ? "0" : withoutLeadingZeroes;
        }
    }

    private static bool IsServica(string storageZone) =>
        string.Equals(StorageOriginClassifier.Resolve(storageZone), "SERVICA", StringComparison.OrdinalIgnoreCase);

    private static bool IsMerma(string palletType) =>
        NormalizeToken(palletType).Contains("MERMA", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> BuildStorageZoneSet(IEnumerable<string>? storageZones)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (storageZones is null)
        {
            return result;
        }

        foreach (var storageZone in storageZones)
        {
            if (!string.IsNullOrWhiteSpace(storageZone))
            {
                var normalized = NormalizeZoneKey(storageZone);
                result.Add(normalized);

                var code = ExtractZoneCode(normalized);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    result.Add(code);
                }
            }
        }

        return result;
    }

    private static bool IsIgnoredStorageZone(string storageZone, IReadOnlySet<string> ignoredZoneSet)
    {
        if (ignoredZoneSet.Count == 0)
        {
            return false;
        }

        var normalized = NormalizeZoneKey(storageZone);
        if (ignoredZoneSet.Contains(normalized))
        {
            return true;
        }

        var code = ExtractZoneCode(normalized);
        return !string.IsNullOrWhiteSpace(code) && ignoredZoneSet.Contains(code);
    }

    private static string ExtractZoneCode(string normalizedZone)
    {
        var separator = normalizedZone.IndexOf('-');
        return separator > 0 ? normalizedZone[..separator].Trim() : normalizedZone.Trim();
    }

    private static string NormalizeZoneKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "SIN ZONA";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string NormalizeToken(string value)
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

    private sealed record InventoryComparisonGroup(
        string Article,
        string Description,
        decimal OloQuantity,
        decimal ServicaQuantity);

    private sealed record ForecastComparisonGroup(
        string Article,
        decimal ForecastQuantity,
        IReadOnlyDictionary<DateOnly, decimal> PeriodDemands);
}
