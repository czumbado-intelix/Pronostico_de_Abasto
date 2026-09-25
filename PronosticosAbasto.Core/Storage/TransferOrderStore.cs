using System.IO;
using System.Text.Json;

namespace PronosticosAbasto.Core.Storage;

/// <summary>
/// Tracks which articles and quantities have been marked as "ordered" (mandado
/// a traer) per company and per analyzed period (the horizon start week). Persisted to
/// <c>%LOCALAPPDATA%\PronosticosAbasto\transfer-orders.json</c> so the marks
/// survive closing and reopening the same requisition period.
/// </summary>
public sealed class TransferOrderStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    // company -> periodKey -> article code -> ordered quantity for that period
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, decimal>>> _data =
        new(StringComparer.OrdinalIgnoreCase);

    public TransferOrderStore()
        : this(DefaultFilePath())
    {
    }

    public TransferOrderStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Load();
    }

    public IReadOnlySet<string> GetOrdered(string company, string periodKey)
    {
        if (!string.IsNullOrWhiteSpace(company) &&
            !string.IsNullOrWhiteSpace(periodKey) &&
            _data.TryGetValue(company.Trim(), out var byPeriod) &&
            byPeriod.TryGetValue(periodKey, out var byArticle))
        {
            return new HashSet<string>(byArticle.Keys, StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, decimal> GetOrderedQuantities(string company, string periodKey)
    {
        if (!string.IsNullOrWhiteSpace(company) &&
            !string.IsNullOrWhiteSpace(periodKey) &&
            _data.TryGetValue(company.Trim(), out var byPeriod) &&
            byPeriod.TryGetValue(periodKey, out var byArticle))
        {
            return new Dictionary<string, decimal>(byArticle, StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, decimal> GetPendingTransit(string company, string? currentPeriodKey = null)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(company) ||
            !_data.TryGetValue(company.Trim(), out var byPeriod))
        {
            return result;
        }

        var hasCurrentPeriod = TryParsePeriodKey(currentPeriodKey, out var currentPeriod);
        foreach (var (periodKey, byArticle) in byPeriod)
        {
            if (hasCurrentPeriod &&
                TryParsePeriodKey(periodKey, out var period) &&
                period >= currentPeriod)
            {
                continue;
            }

            foreach (var (article, quantity) in byArticle)
            {
                if (string.IsNullOrWhiteSpace(article) || quantity <= 0m)
                {
                    continue;
                }

                result[article] = result.GetValueOrDefault(article, 0m) + quantity;
            }
        }

        return result;
    }

    public void SetOrdered(string company, string periodKey, string article, bool ordered, decimal quantity = 0m)
    {
        if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(periodKey) || string.IsNullOrWhiteSpace(article))
        {
            return;
        }

        var byArticle = GetOrCreate(company.Trim(), periodKey);
        var articleKey = article.Trim();
        var changed = false;
        if (ordered)
        {
            quantity = Math.Max(0m, quantity);
            changed = !byArticle.TryGetValue(articleKey, out var currentQuantity) || currentQuantity != quantity;
            byArticle[articleKey] = quantity;
        }
        else
        {
            changed = byArticle.Remove(articleKey);
        }

        if (changed)
        {
            Persist();
        }
    }

    /// <summary>
    /// Reemplaza de una sola vez todas las marcas del periodo. Pensado para las
    /// operaciones masivas (enviar a transito, limpiar marcas): evita una
    /// escritura a disco por cada fila tocada.
    /// </summary>
    public void ReplacePeriod(string company, string periodKey, IReadOnlyDictionary<string, decimal> orderedQuantities)
    {
        ArgumentNullException.ThrowIfNull(orderedQuantities);
        if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(periodKey))
        {
            return;
        }

        var byArticle = GetOrCreate(company.Trim(), periodKey);
        byArticle.Clear();
        foreach (var (article, quantity) in orderedQuantities)
        {
            if (!string.IsNullOrWhiteSpace(article))
            {
                byArticle[article.Trim()] = Math.Max(0m, quantity);
            }
        }

        Persist();
    }

    public void ClearPeriod(string company, string periodKey)
    {
        if (!string.IsNullOrWhiteSpace(company) &&
            !string.IsNullOrWhiteSpace(periodKey) &&
            _data.TryGetValue(company.Trim(), out var byPeriod) &&
            byPeriod.Remove(periodKey))
        {
            Persist();
        }
    }

    private Dictionary<string, decimal> GetOrCreate(string company, string periodKey)
    {
        if (!_data.TryGetValue(company, out var byPeriod))
        {
            byPeriod = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);
            _data[company] = byPeriod;
        }

        if (!byPeriod.TryGetValue(periodKey, out var byArticle))
        {
            byArticle = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            byPeriod[periodKey] = byArticle;
        }

        return byArticle;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(_filePath));
            LoadFromJson(document.RootElement);
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
            var payload = _data.ToDictionary(
                company => company.Key,
                company => company.Value.ToDictionary(
                    period => period.Key,
                    period => period.Value.ToDictionary(article => article.Key, article => article.Value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.Ordinal),
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
        return LocalJsonStorage.PathFor("transfer-orders.json");
    }

    private void LoadFromJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var companyProperty in root.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(companyProperty.Name) ||
                companyProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var periodProperty in companyProperty.Value.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(periodProperty.Name))
                {
                    continue;
                }

                LoadPeriod(companyProperty.Name.Trim(), periodProperty.Name, periodProperty.Value);
            }
        }
    }

    private void LoadPeriod(string company, string periodKey, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var articleElement in value.EnumerateArray())
            {
                if (articleElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(articleElement.GetString()))
                {
                    GetOrCreate(company, periodKey)[articleElement.GetString()!.Trim()] = 0m;
                }
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var articleProperty in value.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(articleProperty.Name))
            {
                continue;
            }

            var quantity = ReadQuantity(articleProperty.Value);
            GetOrCreate(company, periodKey)[articleProperty.Name.Trim()] = Math.Max(0m, quantity);
        }
    }

    private static decimal ReadQuantity(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var quantity))
        {
            return quantity;
        }

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("quantity", out var quantityProperty) &&
            quantityProperty.ValueKind == JsonValueKind.Number &&
            quantityProperty.TryGetDecimal(out quantity))
        {
            return quantity;
        }

        return 0m;
    }

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
