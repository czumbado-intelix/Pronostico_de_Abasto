using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Configuration;

namespace PronosticosAbasto.Core.Storage;

/// <summary>
/// Loads and persists the per-company external (satellite) zone configuration.
/// Settings live in <c>%LOCALAPPDATA%\PronosticosAbasto\satellite-zones.json</c>
/// so they survive app updates and work both packaged and unpackaged. Any
/// missing company is seeded from <see cref="SatelliteZoneDefaults"/>, and a
/// corrupt or unreadable file silently falls back to defaults.
/// </summary>
public sealed class SatelliteZoneStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IReadOnlyList<string> _companies;
    private readonly string _filePath;
    private SatelliteZoneSettings _settings;

    public SatelliteZoneStore(IEnumerable<string> companies)
        : this(companies, DefaultFilePath())
    {
    }

    public SatelliteZoneStore(IEnumerable<string> companies, string filePath)
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
                    return SatelliteZoneSettings.Create(_companies, SatelliteZoneDefaults.Zones, overrides);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable config: fall back to defaults so the app stays usable.
        }

        return SatelliteZoneSettings.Create(_companies, SatelliteZoneDefaults.Zones);
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
        return LocalJsonStorage.PathFor("satellite-zones.json");
    }
}
