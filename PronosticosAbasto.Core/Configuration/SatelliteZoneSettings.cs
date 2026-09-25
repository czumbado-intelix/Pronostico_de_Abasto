namespace PronosticosAbasto.Core.Configuration;

/// <summary>
/// Immutable per-company catalog of external (satellite) storage zones. Company
/// keys are matched case-insensitively and each zone list is trimmed, de-duped
/// and stripped of blanks so the analyzer always receives a clean set.
/// </summary>
public sealed class SatelliteZoneSettings
{
    private readonly Dictionary<string, IReadOnlyList<string>> _zonesByCompany;

    private SatelliteZoneSettings(IDictionary<string, IReadOnlyList<string>> zonesByCompany)
    {
        _zonesByCompany = new Dictionary<string, IReadOnlyList<string>>(zonesByCompany, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Companies => _zonesByCompany.Keys.ToArray();

    public IReadOnlyList<string> GetZonesFor(string company)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        return _zonesByCompany.TryGetValue(company.Trim(), out var zones)
            ? zones
            : Array.Empty<string>();
    }

    public SatelliteZoneSettings WithZonesFor(string company, IEnumerable<string> zones)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        ArgumentNullException.ThrowIfNull(zones);

        var updated = new Dictionary<string, IReadOnlyList<string>>(_zonesByCompany, StringComparer.OrdinalIgnoreCase)
        {
            [company.Trim()] = NormalizeZones(zones),
        };

        return new SatelliteZoneSettings(updated);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ToDictionary() =>
        new Dictionary<string, IReadOnlyList<string>>(_zonesByCompany, StringComparer.OrdinalIgnoreCase);

    public static SatelliteZoneSettings Create(
        IEnumerable<string> companies,
        IReadOnlyList<string> defaultZones,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentNullException.ThrowIfNull(defaultZones);

        var normalizedDefaults = NormalizeZones(defaultZones);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            if (!string.IsNullOrWhiteSpace(company))
            {
                result[company.Trim()] = normalizedDefaults;
            }
        }

        if (overrides is not null)
        {
            foreach (var (company, zones) in overrides)
            {
                if (!string.IsNullOrWhiteSpace(company) && zones is not null)
                {
                    result[company.Trim()] = NormalizeZones(zones);
                }
            }
        }

        return new SatelliteZoneSettings(result);
    }

    private static IReadOnlyList<string> NormalizeZones(IEnumerable<string> zones)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();

        foreach (var zone in zones)
        {
            if (string.IsNullOrWhiteSpace(zone))
            {
                continue;
            }

            var trimmed = zone.Trim();
            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }
}
