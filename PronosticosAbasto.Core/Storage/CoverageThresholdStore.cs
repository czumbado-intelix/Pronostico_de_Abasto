using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Storage;

/// <summary>
/// Loads and persists the per-company traffic-light thresholds to
/// <c>%LOCALAPPDATA%\PronosticosAbasto\coverage-thresholds.json</c>. Any company
/// without a saved entry falls back to <see cref="CoverageThresholds.Default"/>
/// (green >= 100%, yellow >= 50%).
/// </summary>
public sealed class CoverageThresholdStore
{
    private sealed class ThresholdDto
    {
        public decimal Red { get; set; }

        public decimal Healthy { get; set; }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly Dictionary<string, CoverageThresholds> _byCompany = new(StringComparer.OrdinalIgnoreCase);

    public CoverageThresholdStore()
        : this(DefaultFilePath())
    {
    }

    public CoverageThresholdStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Load();
    }

    public CoverageThresholds GetFor(string company)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        return _byCompany.TryGetValue(company.Trim(), out var thresholds)
            ? thresholds
            : CoverageThresholds.Default;
    }

    public void Save(string company, CoverageThresholds thresholds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        _byCompany[company.Trim()] = thresholds;
        Persist();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var stored = JsonSerializer.Deserialize<Dictionary<string, ThresholdDto>>(File.ReadAllText(_filePath), SerializerOptions);
            if (stored is null)
            {
                return;
            }

            foreach (var (company, dto) in stored)
            {
                if (string.IsNullOrWhiteSpace(company) || dto is null)
                {
                    continue;
                }

                try
                {
                    _byCompany[company.Trim()] = new CoverageThresholds(dto.Red, dto.Healthy);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Ignore a corrupt/invalid persisted entry; default will be used.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Unreadable config: fall back to defaults.
        }
    }

    private void Persist()
    {
        try
        {
            var payload = _byCompany.ToDictionary(
                entry => entry.Key,
                entry => new ThresholdDto { Red = entry.Value.RedPercent, Healthy = entry.Value.HealthyPercent },
                StringComparer.OrdinalIgnoreCase);
            LocalJsonStorage.WriteAtomic(_filePath, payload, SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence.
        }
    }

    private static string DefaultFilePath()
    {
        return LocalJsonStorage.PathFor("coverage-thresholds.json");
    }
}
