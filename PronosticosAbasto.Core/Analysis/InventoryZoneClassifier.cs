using System.Text;

namespace PronosticosAbasto.Core.Analysis;

/// <summary>Per-article inventory split into principal vs satellite stock.</summary>
public sealed record ArticleInventorySummary(
    string Article,
    decimal PrincipalQuantity,
    decimal SatelliteQuantity,
    decimal TotalQuantity,
    IReadOnlyList<ZoneInventoryDetail> Zones,
    IReadOnlyList<ZoneInventoryDetail> SatelliteZones,
    IReadOnlyList<PalletInventoryDetail> SatellitePallets);

/// <summary>
/// Groups inventory positions by article and classifies each storage zone as
/// principal or satellite, using the company's configured external zones (empty =
/// treat everything as satellite). Mirrors the zone logic in
/// <see cref="WeeklyForecastAnalyzer"/> so other analyzers (e.g. expediciones) can
/// reuse the exact same principal/satellite split.
/// </summary>
public static class InventoryZoneClassifier
{
    public static string NormalizeArticleKey(string article) => article.Trim();

    public static IReadOnlyDictionary<string, ArticleInventorySummary> Summarize(
        IEnumerable<InventoryPosition> inventory,
        IEnumerable<string>? satelliteZones,
        IEnumerable<string>? ignoredStorageZones = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var satelliteZoneSet = BuildSatelliteZoneSet(satelliteZones);
        var ignoredZoneSet = BuildStorageZoneSet(ignoredStorageZones);
        var treatAllZonesAsSatellite = satelliteZoneSet.Count == 0;

        return inventory
            .Where(position => !string.IsNullOrWhiteSpace(position.Article))
            .Where(position => !IsIgnoredStorageZone(position.StorageZone, ignoredZoneSet))
            .GroupBy(position => NormalizeArticleKey(position.Article), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var classified = group
                        .Select(position =>
                        {
                            var storageZone = NormalizeZoneDisplayName(position.StorageZone);
                            var isSatellite = treatAllZonesAsSatellite ||
                                satelliteZoneSet.Contains(NormalizeZoneKey(storageZone));

                            return new ClassifiedInventoryPosition(position, storageZone, isSatellite);
                        })
                        .ToArray();

                    var zones = classified
                        .GroupBy(position => NormalizeZoneKey(position.StorageZone), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(zone => zone.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(zone =>
                        {
                            var storageZone = NormalizeZoneDisplayName(zone.First().StorageZone);
                            var isSatellite = zone.First().IsSatellite;

                            return new ZoneInventoryDetail(
                                storageZone,
                                zone.Sum(position => position.Position.Quantity),
                                isSatellite);
                        })
                        .ToArray();

                    var satelliteZoneDetails = zones.Where(zone => zone.IsSatellite).ToArray();
                    var satellitePallets = classified
                        .Where(position => position.IsSatellite && position.Position.Quantity > 0)
                        .GroupBy(
                            position => new
                            {
                                Pallet = NormalizePalletDisplayName(position.Position.Pallet),
                                Zone = NormalizeZoneKey(position.StorageZone),
                                Location = NormalizeLocationDisplayName(position.Position.Location),
                                ValidationDate = position.Position.ValidationDate,
                                PalletType = NormalizePalletType(position.Position.PalletType),
                            })
                        .Select(palletGroup =>
                        {
                            var first = palletGroup.First();
                            return new PalletInventoryDetail(
                                palletGroup.Key.Pallet,
                                first.StorageZone,
                                palletGroup.Sum(position => position.Position.Quantity),
                                palletGroup.Key.ValidationDate,
                                palletGroup.Key.PalletType,
                                palletGroup.Key.Location);
                        })
                        .OrderBy(pallet => pallet.ValidationDate ?? DateOnly.MaxValue)
                        .ThenBy(pallet => pallet.Pallet, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(pallet => pallet.StorageZone, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    return new ArticleInventorySummary(
                        group.First().Article.Trim(),
                        zones.Where(zone => !zone.IsSatellite).Sum(zone => zone.Quantity),
                        satelliteZoneDetails.Sum(zone => zone.Quantity),
                        group.Sum(position => position.Quantity),
                        zones,
                        satelliteZoneDetails,
                        satellitePallets);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> BuildSatelliteZoneSet(IEnumerable<string>? satelliteZones) =>
        BuildStorageZoneSet(satelliteZones);

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

    private static string NormalizeZoneDisplayName(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Sin zona" : value.Trim();

    private static string NormalizePalletDisplayName(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Sin pallet" : value.Trim();

    private static string NormalizePalletType(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeLocationDisplayName(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Sin ubicacion" : value.Trim();

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

    private sealed record ClassifiedInventoryPosition(
        InventoryPosition Position,
        string StorageZone,
        bool IsSatellite);
}
