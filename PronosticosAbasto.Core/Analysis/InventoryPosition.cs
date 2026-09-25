namespace PronosticosAbasto.Core.Analysis;

public sealed record InventoryPosition(
    string Article,
    string StorageZone,
    decimal Quantity,
    string Description = "",
    string Pallet = "",
    DateOnly? ValidationDate = null,
    string PalletType = "",
    string Location = "");
