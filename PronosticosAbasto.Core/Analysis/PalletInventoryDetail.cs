namespace PronosticosAbasto.Core.Analysis;

public sealed record PalletInventoryDetail(
    string Pallet,
    string StorageZone,
    decimal Quantity,
    DateOnly? ValidationDate,
    string PalletType,
    string Location = "");
