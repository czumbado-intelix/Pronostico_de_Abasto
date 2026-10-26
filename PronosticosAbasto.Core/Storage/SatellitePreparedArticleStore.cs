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
    private readonly string? _packagedDefaultPath;
    private Dictionary<string, List<SatellitePreparedArticle>> _items;

    public SatellitePreparedArticleStore(IEnumerable<string> companies)
        : this(
            companies,
            DefaultFilePath(),
            LocalJsonStorage.PackagedDefaultsPathFor("satellite-prepared-articles.json"))
    {
    }

    public SatellitePreparedArticleStore(IEnumerable<string> companies, string filePath)
        : this(companies, filePath, packagedDefaultPath: null)
    {
    }

    public SatellitePreparedArticleStore(IEnumerable<string> companies, string filePath, string? packagedDefaultPath)
    {
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _companies = companies.ToArray();
        _filePath = filePath;
        _packagedDefaultPath = packagedDefaultPath;
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
        var local = TryLoadFile(_filePath)
            ?? new Dictionary<string, List<SatellitePreparedArticle>>(StringComparer.OrdinalIgnoreCase);
        var defaults = !string.IsNullOrWhiteSpace(_packagedDefaultPath)
            ? TryLoadFile(_packagedDefaultPath)
            : null;

        if (defaults is not null)
        {
            foreach (var company in _companies)
            {
                if ((!local.TryGetValue(company, out var items) || items.Count == 0) &&
                    defaults.TryGetValue(company, out var defaultItems) &&
                    defaultItems.Count > 0)
                {
                    local[company] = defaultItems;
                }
            }
        }

        return SeedMissingCompanies(local);
    }

    private static Dictionary<string, List<SatellitePreparedArticle>>? TryLoadFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var stored = JsonSerializer.Deserialize<Dictionary<string, List<SatellitePreparedArticle>>>(json, SerializerOptions);
            return stored?.ToDictionary(
                entry => entry.Key,
                entry => Normalize(entry.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep defaults if the local catalog cannot be read.
        }

        return null;
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
