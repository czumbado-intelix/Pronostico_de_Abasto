using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.IO;

public sealed record InventoryWorkbook(IReadOnlyList<InventoryPosition> Positions)
{
    /// <summary>Optional article code -> description, when the file carries one.</summary>
    public IReadOnlyDictionary<string, string> Descriptions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
