using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.IO;

public sealed record ExpedicionesExportRow(
    string Article,
    string Description,
    string TarimaSize,
    IReadOnlyList<PalletInventoryDetail> Pallets);
