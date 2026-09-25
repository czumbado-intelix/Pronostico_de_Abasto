using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Services;

public sealed class TarimaSizeStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IReadOnlyList<string> _companies;
    private readonly string _filePath;
    private Dictionary<string, List<TarimaSizeEntry>> _items;

    public TarimaSizeStore(IEnumerable<string> companies)
        : this(companies, DefaultFilePath())
    {
    }

    public TarimaSizeStore(IEnumerable<string> companies, string filePath)
    {
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _companies = companies.ToArray();
        _filePath = filePath;
        _items = Load();
    }

    public IReadOnlyList<TarimaSizeEntry> GetItemsFor(string company) =>
        _items.TryGetValue(company, out var sizes)
            ? sizes
                .OrderBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<TarimaSizeEntry>();

    public IReadOnlyDictionary<string, TarimaSizeEntry> GetMapFor(string company) =>
        GetItemsFor(company)
            .Where(item => !string.IsNullOrWhiteSpace(item.Article))
            .GroupBy(item => InventoryZoneClassifier.NormalizeArticleKey(item.Article), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);

    public void Save(string company, IReadOnlyList<TarimaSizeEntry> sizes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        ArgumentNullException.ThrowIfNull(sizes);

        _items[company] = Normalize(sizes);
        Persist();
    }

    private Dictionary<string, List<TarimaSizeEntry>> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var stored = JsonSerializer.Deserialize<Dictionary<string, List<TarimaSizeEntry>>>(json, SerializerOptions);
                if (stored is not null)
                {
                    return SeedMissingCompanies(stored.ToDictionary(
                        entry => entry.Key,
                        entry => Normalize(entry.Value),
                        StringComparer.OrdinalIgnoreCase));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep defaults if the local catalog cannot be read.
        }

        return SeedMissingCompanies(new Dictionary<string, List<TarimaSizeEntry>>(StringComparer.OrdinalIgnoreCase));
    }

    private Dictionary<string, List<TarimaSizeEntry>> SeedMissingCompanies(
        Dictionary<string, List<TarimaSizeEntry>> source)
    {
        foreach (var company in _companies)
        {
            source.TryAdd(company, new List<TarimaSizeEntry>());
        }

        return source;
    }

    private static List<TarimaSizeEntry> Normalize(IEnumerable<TarimaSizeEntry> sizes)
    {
        var map = new Dictionary<string, TarimaSizeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var size in sizes)
        {
            var article = InventoryZoneClassifier.NormalizeArticleKey(size.Article);
            var sizeCode = TarimaSizeNormalizer.NormalizeOrEmpty(size.SizeCode);
            if (string.IsNullOrWhiteSpace(article) || string.IsNullOrWhiteSpace(sizeCode))
            {
                continue;
            }

            map[article] = new TarimaSizeEntry(article, size.Description?.Trim() ?? string.Empty, sizeCode);
        }

        return map.Values
            .OrderBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

            File.WriteAllText(_filePath, JsonSerializer.Serialize(_items, SerializerOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence; the in-memory catalog remains correct for this session.
        }
    }

    private static string DefaultFilePath()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseFolder, "PronosticosAbasto", "tarima-sizes.json");
    }
}
