using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Configuration;

namespace PronosticosAbasto.Services;

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
                    var overrides = stored.ToDictionary(
                        entry => entry.Key,
                        entry => (IReadOnlyList<string>)entry.Value,
                        StringComparer.OrdinalIgnoreCase);
                    return SatelliteZoneSettings.Create(_companies, InventoryExcludedZoneDefaults.Zones, overrides);
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
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = _settings.ToDictionary().ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(payload, SerializerOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence; the in-memory settings remain correct for this session.
        }
    }

    private static string DefaultFilePath()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseFolder, "PronosticosAbasto", "inventory-excluded-zones.json");
    }
}
