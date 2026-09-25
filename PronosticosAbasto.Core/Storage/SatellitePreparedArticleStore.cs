using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Storage;

public sealed class SatellitePreparedArticleStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IReadOnlyList<string> _companies;
    private readonly string _filePath;
    private Dictionary<string, List<SatellitePreparedArticle>> _items;

    public SatellitePreparedArticleStore(IEnumerable<string> companies)
        : this(companies, DefaultFilePath())
    {
    }

    public SatellitePreparedArticleStore(IEnumerable<string> companies, string filePath)
    {
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _companies = companies.ToArray();
        _filePath = filePath;
        _items = Load();
    }

    public IReadOnlyList<SatellitePreparedArticle> GetItemsFor(string company) =>
        _items.TryGetValue(company, out var articles)
            ? articles
                .OrderBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<SatellitePreparedArticle>();

    public IReadOnlySet<string> GetArticleSetFor(string company) =>
        GetItemsFor(company)
            .Select(item => InventoryZoneClassifier.NormalizeArticleKey(item.Article))
            .Where(article => !string.IsNullOrWhiteSpace(article))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Save(string company, IReadOnlyList<SatellitePreparedArticle> articles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        ArgumentNullException.ThrowIfNull(articles);

        _items[company] = Normalize(articles);
        Persist();
    }

    private Dictionary<string, List<SatellitePreparedArticle>> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var stored = JsonSerializer.Deserialize<Dictionary<string, List<SatellitePreparedArticle>>>(json, SerializerOptions);
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

        return SeedMissingCompanies(new Dictionary<string, List<SatellitePreparedArticle>>(StringComparer.OrdinalIgnoreCase));
    }

    private Dictionary<string, List<SatellitePreparedArticle>> SeedMissingCompanies(
        Dictionary<string, List<SatellitePreparedArticle>> source)
    {
        foreach (var company in _companies)
        {
            source.TryAdd(company, new List<SatellitePreparedArticle>());
        }

        return source;
    }

    private static List<SatellitePreparedArticle> Normalize(IEnumerable<SatellitePreparedArticle> articles)
    {
        var map = new Dictionary<string, SatellitePreparedArticle>(StringComparer.OrdinalIgnoreCase);
        foreach (var article in articles)
        {
            var code = InventoryZoneClassifier.NormalizeArticleKey(article.Article);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var description = article.Description?.Trim() ?? string.Empty;
            if (!map.TryGetValue(code, out var existing) ||
                (string.IsNullOrWhiteSpace(existing.Description) && !string.IsNullOrWhiteSpace(description)))
            {
                map[code] = new SatellitePreparedArticle(code, description);
            }
        }

        return map.Values
            .OrderBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Persist()
    {
        try
        {
            LocalJsonStorage.WriteAtomic(_filePath, _items, SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence; the in-memory catalog remains correct for this session.
        }
    }

    private static string DefaultFilePath()
    {
        return LocalJsonStorage.PathFor("satellite-prepared-articles.json");
    }
}
