using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Storage;

public sealed class TransferTransitStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly List<TransferTransitItem> _items = [];
    private bool _backupCreated;

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

    public IReadOnlyList<TransferTransitItem> GetAll(string company) =>
        _items
            .Where(item => IsCompany(item, company))
            .OrderBy(item => TransitStatusSort(item))
            .ThenBy(item => item.Origin, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Article, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ValidationDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.Pallet, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<TransferTransitItem> GetPending(string company) =>
        GetAll(company)
            .Where(item => !item.IsReceived && !item.IsReconciled)
            .ToArray();

    public IReadOnlyList<TransferTransitItem> GetReceivedPendingInventory(string company) =>
        GetAll(company)
            .Where(item => item.IsReceived && !item.IsReconciled)
            .ToArray();

    public IReadOnlyList<TransferTransitItem> GetReconciled(string company) =>
        GetAll(company)
            .Where(item => item.IsReconciled)
            .ToArray();

    public IReadOnlyDictionary<string, decimal> GetPendingTransit(string company, string? currentPeriodKey = null)
    {
        // Transit only represents pallets that are still pending to receive.
        // Once a pallet is marked as received it stays in history, but stops
        // reducing new transfer analysis.
        _ = currentPeriodKey;
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _items.Where(item => IsCompany(item, company) && !item.IsReceived && !item.IsReconciled && item.Quantity > 0m))
        {
            result[item.Article] = result.GetValueOrDefault(item.Article, 0m) + item.Quantity;
        }

        return result;
    }

    public IReadOnlySet<string> GetPendingPalletKeys(string company) =>
        GetPending(company)
            .Select(item => BuildTransitPalletKey(item.Article, item.Pallet))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> GetOutstandingPalletKeys(string company, string? currentPeriodKey = null)
    {
        // Only active transit should affect a new analysis. Received rows stay
        // available as history, but they must not hide inventory from the Excel
        // import or create false shortages on another workstation.
        _ = currentPeriodKey;
        return GetPendingPalletKeys(company);
    }

    public TransitAddResult AddPending(IEnumerable<TransferTransitItem> items)
    {
        var added = 0;
        var skipped = 0;

        foreach (var item in items)
        {
            var normalized = NormalizeItem(item);
            if (!CanPersist(normalized))
            {
                skipped++;
                continue;
            }

            var key = BuildTransitPalletKey(normalized.Article, normalized.Pallet);
            var exists = _items.Any(existing =>
                IsCompany(existing, normalized.Company) &&
                !existing.IsReceived &&
                !existing.IsReconciled &&
                string.Equals(
                    BuildTransitPalletKey(existing.Article, existing.Pallet),
                    key,
                    StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                skipped++;
                continue;
            }

            _items.Add(normalized);
            added++;
        }

        var saved = true;
        if (added > 0)
        {
            saved = Persist();
        }

        return new TransitAddResult(added, skipped, saved);
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
            if (!item.IsReceived && !item.IsReconciled && idSet.Contains(item.Id))
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
                !item.IsReconciled &&
                string.Equals(item.Article, article.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToArray();
        return MarkReceived(ids);
    }

    public int ReconcileReceived(string company, IReadOnlySet<string> principalPalletKeys)
    {
        _ = company;
        _ = principalPalletKeys;
        return 0;
    }

    public static string BuildPalletKey(string article, string pallet, string storageZone, string location) =>
        PalletIdentity.Build(article, pallet, storageZone, location);

    private static string BuildTransitPalletKey(string article, string pallet) =>
        PalletIdentity.BuildArticlePallet(article, pallet);

    private static TransferTransitItem NormalizeItem(TransferTransitItem item)
    {
        var storageZone = TrimOrEmpty(item.StorageZone);

        return item with
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim(),
            Company = TrimOrEmpty(item.Company),
            PeriodKey = TrimOrEmpty(item.PeriodKey),
            Article = TrimOrEmpty(item.Article),
            Description = TrimOrEmpty(item.Description),
            Pallet = TrimOrEmpty(item.Pallet),
            StorageZone = storageZone,
            Location = TrimOrEmpty(item.Location),
            TarimaSize = TrimOrEmpty(item.TarimaSize),
            Origin = string.IsNullOrWhiteSpace(item.Origin)
                ? StorageOriginClassifier.Resolve(storageZone)
                : item.Origin.Trim(),
            CreatedAt = item.CreatedAt == default ? DateTimeOffset.Now : item.CreatedAt,
        };
    }

    private static bool CanPersist(TransferTransitItem item) =>
        !string.IsNullOrWhiteSpace(item.Company) &&
        !string.IsNullOrWhiteSpace(item.Article) &&
        !string.IsNullOrWhiteSpace(item.Pallet) &&
        item.Quantity > 0m;

    private static string TrimOrEmpty(string? value) => value?.Trim() ?? string.Empty;

    private static int TransitStatusSort(TransferTransitItem item) =>
        item.IsReconciled ? 2 :
        item.IsReceived ? 1 :
        0;

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
                _items.AddRange(items.Select(NormalizeItem).Where(CanPersist));
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Unreadable file: start fresh.
        }
    }

    private bool Persist()
    {
        try
        {
            EnsureBackupBeforeFirstWrite();
            LocalJsonStorage.WriteAtomic(_filePath, _items, SerializerOptions);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence.
            return false;
        }
    }

    private void EnsureBackupBeforeFirstWrite()
    {
        if (_backupCreated || !File.Exists(_filePath))
        {
            _backupCreated = true;
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{_filePath}.bak-{timestamp}";
        File.Copy(_filePath, backupPath, overwrite: false);
        _backupCreated = true;
    }

    private static string DefaultFilePath()
    {
        return LocalJsonStorage.PathFor("transfer-transit.json");
    }

    private static bool IsCompany(TransferTransitItem item, string company) =>
        !string.IsNullOrWhiteSpace(company) &&
        string.Equals(TrimOrEmpty(item.Company), company.Trim(), StringComparison.OrdinalIgnoreCase);

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
    DateTimeOffset? ReceivedAt = null,
    DateTimeOffset? ReconciledAt = null)
{
    [JsonIgnore]
    public bool IsReceived => ReceivedAt is not null;

    [JsonIgnore]
    public bool IsReconciled => ReconciledAt is not null;
}

public sealed record TransitAddResult(int Added, int Skipped, bool Saved = true);
