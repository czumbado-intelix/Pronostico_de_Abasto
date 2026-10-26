using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Configuration;

namespace PronosticosAbasto.Core.Storage;

/// <summary>
/// Loads and persists zones that should be ignored from the inventory report.
/// Settings live in %LOCALAPPDATA%\PronosticosAbasto\inventory-excluded-zones.json.
/// </summary>
public sealed class InventoryExcludedZoneStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly IReadOnlyList<string> _companies;
    private readonly string _filePath;
    private SatelliteZoneSettings _settings;

    public InventoryExcludedZoneStore(IEnumerable<string> companies)
        : this(companies, DefaultFilePath())
    {
    }

    public InventoryExcludedZoneStore(IEnumerable<string> companies, string filePath)
    {
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _companies = companies.ToArray();
        _filePath = filePath;
        _settings = Load();
    }

    public IReadOnlyList<string> GetZonesFor(string company) => _settings.GetZonesFor(company);

    public void Save(string company, IReadOnlyList<string> zones)
    {
        _settings = _settings.WithZonesFor(company, zones);
        Persist();
    }

    private SatelliteZoneSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var stored = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, SerializerOptions);
                if (stored is not null)
                {
                    var overrides = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                    var anyModified = false;

                    foreach (var company in _companies)
                    {
                        var list = stored.TryGetValue(company, out var existing)
                            ? new List<string>(existing)
                            : new List<string>(InventoryExcludedZoneDefaults.Zones);

                        // Garantizar que zonas por defecto obligatorias (como ZA55) esten presentes,
                        // sin borrar ni alterar ninguna zona personalizada que el usuario haya agregado.
                        foreach (var defaultZone in InventoryExcludedZoneDefaults.Zones)
                        {
                            var defaultPrefix = defaultZone.Split(['-', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                            var exists = list.Any(z =>
                                string.Equals(z.Trim(), defaultZone.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(defaultPrefix) && z.Trim().StartsWith(defaultPrefix + "-", StringComparison.OrdinalIgnoreCase)));

                            if (!exists)
                            {
                                list.Add(defaultZone);
                                anyModified = true;
                            }
                        }

                        overrides[company] = list;
                    }

                    var settings = SatelliteZoneSettings.Create(_companies, InventoryExcludedZoneDefaults.Zones, overrides);
                    if (anyModified)
                    {
                        try
                        {
                            var payload = settings.ToDictionary().ToDictionary(
                                entry => entry.Key,
                                entry => entry.Value.ToList(),
                                StringComparer.OrdinalIgnoreCase);
                            LocalJsonStorage.WriteAtomic(_filePath, payload, SerializerOptions);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    return settings;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable config: fall back to defaults so analysis stays usable.
        }

        return SatelliteZoneSettings.Create(_companies, InventoryExcludedZoneDefaults.Zones);
    }

    private void Persist()
    {
        try
        {
            var payload = _settings.ToDictionary().ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);
            LocalJsonStorage.WriteAtomic(_filePath, payload, SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence; the in-memory settings remain correct for this session.
        }
    }

    private static string DefaultFilePath()
    {
        return LocalJsonStorage.PathFor("inventory-excluded-zones.json");
    }
}
