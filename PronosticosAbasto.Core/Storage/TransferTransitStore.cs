using System.IO;
using System.Text.Json;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Storage;

public sealed class TransferTransitStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly List<TransferTransitItem> _items = [];

    public TransferTransitStore()
        : this(DefaultFilePath())
    {
    }

    public TransferTransitStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Load();
    }

    public IReadOnlyList<TransferTransitItem> GetPending(string company) =>
        _items
            .Where(item => IsCompany(item, company) && !item.IsReceived)
            .OrderBy(item => item.Origin, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ValidationDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.Pallet, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyDictionary<string, decimal> GetPendingTransit(string company, string? currentPeriodKey = null)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var hasCurrentPeriod = TryParsePeriodKey(currentPeriodKey, out var currentPeriod);

        foreach (var item in _items.Where(item => IsCompany(item, company) && !item.IsReceived && item.Quantity > 0m))
        {
            if (hasCurrentPeriod &&
                TryParsePeriodKey(item.PeriodKey, out var itemPeriod) &&
                itemPeriod >= currentPeriod)
            {
                continue;
            }

            result[item.Article] = result.GetValueOrDefault(item.Article, 0m) + item.Quantity;
        }

        return result;
    }

    public IReadOnlySet<string> GetPendingPalletKeys(string company) =>
        GetPending(company)
            .Select(item => BuildPalletKey(item.Article, item.Pallet, item.StorageZone, item.Location))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public TransitAddResult AddPending(IEnumerable<TransferTransitItem> items)
    {
        var added = 0;
        var skipped = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Company) ||
                string.IsNullOrWhiteSpace(item.Article) ||
                string.IsNullOrWhiteSpace(item.Pallet) ||
                item.Quantity <= 0m)
            {
                skipped++;
                continue;
            }

            var key = BuildPalletKey(item.Article, item.Pallet, item.StorageZone, item.Location);
            var exists = _items.Any(existing =>
                IsCompany(existing, item.Company) &&
                !existing.IsReceived &&
                string.Equals(
                    BuildPalletKey(existing.Article, existing.Pallet, existing.StorageZone, existing.Location),
                    key,
                    StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                skipped++;
                continue;
            }

            _items.Add(item with
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
                Company = item.Company.Trim(),
                PeriodKey = item.PeriodKey.Trim(),
                Article = item.Article.Trim(),
                Description = item.Description.Trim(),
                Pallet = item.Pallet.Trim(),
                StorageZone = item.StorageZone.Trim(),
                Location = item.Location.Trim(),
                TarimaSize = item.TarimaSize.Trim(),
                Origin = string.IsNullOrWhiteSpace(item.Origin)
                    ? StorageOriginClassifier.Resolve(item.StorageZone)
                    : item.Origin.Trim(),
                CreatedAt = item.CreatedAt == default ? DateTimeOffset.Now : item.CreatedAt,
            });
            added++;
        }

        if (added > 0)
        {
            Persist();
        }

        return new TransitAddResult(added, skipped);
    }

    public int MarkReceived(IEnumerable<string> ids)
    {
        var idSet = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (idSet.Count == 0)
        {
            return 0;
        }

        var count = 0;
        var now = DateTimeOffset.Now;
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            if (!item.IsReceived && idSet.Contains(item.Id))
            {
                _items[index] = item with { ReceivedAt = now };
                count++;
            }
        }

        if (count > 0)
        {
            Persist();
        }

        return count;
    }

    public int MarkArticleReceived(string company, string article)
    {
        if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(article))
        {
            return 0;
        }

        var ids = _items
            .Where(item =>
                IsCompany(item, company) &&
                !item.IsReceived &&
                string.Equals(item.Article, article.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToArray();
        return MarkReceived(ids);
    }

    public static string BuildPalletKey(string article, string pallet, string storageZone, string location) =>
        PalletIdentity.Build(article, pallet, storageZone, location);

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var items = JsonSerializer.Deserialize<List<TransferTransitItem>>(File.ReadAllText(_filePath));
            if (items is not null)
            {
                _items.AddRange(items.Where(item => !string.IsNullOrWhiteSpace(item.Id)));
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Unreadable file: start fresh.
        }
    }

    private void Persist()
    {
        try
        {
            LocalJsonStorage.WriteAtomic(_filePath, _items, SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence.
        }
    }

    private static string DefaultFilePath()
    {
        return LocalJsonStorage.PathFor("transfer-transit.json");
    }

    private static bool IsCompany(TransferTransitItem item, string company) =>
        !string.IsNullOrWhiteSpace(company) &&
        string.Equals(item.Company.Trim(), company.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryParsePeriodKey(string? periodKey, out DateOnly period)
    {
        if (!string.IsNullOrWhiteSpace(periodKey) &&
            DateOnly.TryParseExact(periodKey, "yyyy-MM-dd", out period))
        {
            return true;
        }

        period = default;
        return false;
    }
}

public sealed record TransferTransitItem(
    string Id,
    string Company,
    string PeriodKey,
    string Article,
    string Description,
    string Pallet,
    decimal Quantity,
    string StorageZone,
    string Location,
    DateOnly? ValidationDate,
    string TarimaSize,
    string Origin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReceivedAt = null)
{
    public bool IsReceived => ReceivedAt is not null;
}

public sealed record TransitAddResult(int Added, int Skipped);
