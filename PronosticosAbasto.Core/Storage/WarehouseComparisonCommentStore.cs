using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Storage;

public sealed class WarehouseComparisonCommentStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly IReadOnlyList<string> _companies;
    private readonly string _filePath;
    private Dictionary<string, Dictionary<string, string>> _comments;

    public WarehouseComparisonCommentStore(IEnumerable<string> companies)
        : this(companies, DefaultFilePath())
    {
    }

    public WarehouseComparisonCommentStore(IEnumerable<string> companies, string filePath)
    {
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _companies = companies.ToArray();
        _filePath = filePath;
        _comments = Load();
    }

    public IReadOnlyDictionary<string, string> GetCommentsFor(string company) =>
        _comments.TryGetValue(company, out var comments)
            ? new Dictionary<string, string>(comments, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string GetComment(string company, string article)
    {
        var normalized = InventoryZoneClassifier.NormalizeArticleKey(article);
        return _comments.TryGetValue(company, out var comments) &&
            comments.TryGetValue(normalized, out var comment)
                ? comment
                : string.Empty;
    }

    public void SaveComment(string company, string article, string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);

        var normalized = InventoryZoneClassifier.NormalizeArticleKey(article);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!_comments.TryGetValue(company, out var comments))
        {
            comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _comments[company] = comments;
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            comments.Remove(normalized);
        }
        else
        {
            comments[normalized] = comment.Trim();
        }

        Persist();
    }

    private Dictionary<string, Dictionary<string, string>> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var stored = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, SerializerOptions);
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
            // Keep the app usable even if the local comments file is unreadable.
        }

        return SeedMissingCompanies(new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase));
    }

    private Dictionary<string, Dictionary<string, string>> SeedMissingCompanies(
        Dictionary<string, Dictionary<string, string>> source)
    {
        foreach (var company in _companies)
        {
            source.TryAdd(company, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        return source;
    }

    private static Dictionary<string, string> Normalize(Dictionary<string, string> comments)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var comment in comments)
        {
            var article = InventoryZoneClassifier.NormalizeArticleKey(comment.Key);
            if (!string.IsNullOrWhiteSpace(article) && !string.IsNullOrWhiteSpace(comment.Value))
            {
                result[article] = comment.Value.Trim();
            }
        }

        return result;
    }

    private void Persist()
    {
        try
        {
            LocalJsonStorage.WriteAtomic(_filePath, _comments, SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence; in-memory comments remain available.
        }
    }

    private static string DefaultFilePath()
    {
        return LocalJsonStorage.PathFor("warehouse-comparison-comments.json");
    }
}
