using PronosticosAbasto.Core.Analysis;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// One expedition line in the Expediciones view: its demand, how much the
/// principal covers, how much to bring from satellite (and from which zones), and
/// what stays pending. Immutable display row (no checkbox), so a plain class.
/// </summary>
public sealed class ExpedicionRowViewModel : IClipboardRow
{
    public ExpedicionRowViewModel(
        ExpeditionLineResult line,
        bool isSatellitePrepared = false,
        string tarimaSizeText = "—")
    {
        ArgumentNullException.ThrowIfNull(line);

        Article = line.Article;
        Description = string.IsNullOrWhiteSpace(line.Description) ? "Sin descripcion" : line.Description.Trim();
        ExpeditionNumber = string.IsNullOrWhiteSpace(line.ExpeditionNumber)
            ? "Sin expedicion"
            : line.ExpeditionNumber.Trim();
        ArticleDescriptionText = ArticleDisplayFormatter.FormatWithDescription(Article, Description);
        DemandQuantity = line.DemandQuantity;
        PrincipalQuantity = line.PrincipalInventoryQuantity;
        SatelliteQuantity = line.SatelliteInventoryQuantity;
        PendingTransitQuantity = line.PendingTransitQuantity;
        TransferQuantity = line.TransferSuggestionQuantity;
        NewTransferQuantity = line.NewTransferSuggestionQuantity;
        RemainingShortageQuantity = line.RemainingShortageAfterTransfer;
        CalculatedTransferNeedQuantity = line.CalculatedTransferNeedQuantity;
        PalletRoundUpSurplusQuantity = line.PalletRoundUpSurplusQuantity;
        SuggestedPalletCount = line.SuggestedPalletCount;
        DemandText = line.DemandQuantity.ToString("N0");
        PrincipalInventoryText = line.PrincipalInventoryQuantity.ToString("N0");
        SatelliteAvailableText = line.SatelliteInventoryQuantity.ToString("N0");
        PendingTransitText = line.PendingTransitQuantity.ToString("N0");
        TransferSuggestionText = line.TransferSuggestionQuantity.ToString("N0");
        RemainingShortageText = line.RemainingShortageAfterTransfer.ToString("N0");
        CalculatedTransferNeedText = line.CalculatedTransferNeedQuantity.ToString("N0");
        PalletRoundUpSurplusText = line.PalletRoundUpSurplusQuantity.ToString("N0");
        SuggestedPalletCountText = line.SuggestedPalletCount.ToString("N0");
        SuggestedPalletNumbersText = FormatPalletNumbers(line.SuggestedPallets);
        SuggestedPalletsText = FormatPallets(line.SuggestedPallets);
        AvailablePalletsText = FormatPallets(line.AvailablePallets);
        TarimaSizeText = string.IsNullOrWhiteSpace(tarimaSizeText) ? "—" : tarimaSizeText.Trim();
        IsSatellitePrepared = isSatellitePrepared;
        SatellitePreparedText = isSatellitePrepared ? "Si" : "No";
        HasPending = line.RemainingShortageAfterTransfer > 0;
        StatusLabel = ResolveStatusLabel();
        StatusBackgroundBrush = ResolveStatusBackgroundBrush();
        StatusForegroundBrush = ResolveStatusForegroundBrush();
        StatusBorderBrush = ResolveStatusBorderBrush();

        // One zone per line, most stock first (the convenient zone to pull from).
        SatelliteZonesText = string.Join(
            Environment.NewLine,
            line.SatelliteZones
                .Where(zone => zone.Quantity > 0)
                .OrderByDescending(zone => zone.Quantity)
                .Select(zone => $"{zone.Quantity:N0} — {zone.StorageZone}"));
        if (string.IsNullOrEmpty(SatelliteZonesText))
        {
            SatelliteZonesText = "Sin zona";
        }
    }

    public string Article { get; }

    public string Description { get; }

    public string ExpeditionNumber { get; }

    public string ArticleDescriptionText { get; }

    public decimal DemandQuantity { get; }

    public decimal PrincipalQuantity { get; }

    public decimal SatelliteQuantity { get; }

    public decimal PendingTransitQuantity { get; }

    public decimal TransferQuantity { get; }

    public decimal NewTransferQuantity { get; }

    public decimal RemainingShortageQuantity { get; }

    public decimal CalculatedTransferNeedQuantity { get; }

    public decimal PalletRoundUpSurplusQuantity { get; }

    public int SuggestedPalletCount { get; }

    public string DemandText { get; }

    public string PrincipalInventoryText { get; }

    public string SatelliteAvailableText { get; }

    public string PendingTransitText { get; }

    public string TransferSuggestionText { get; }

    public string RemainingShortageText { get; }

    public string CalculatedTransferNeedText { get; }

    public string PalletRoundUpSurplusText { get; }

    public string SuggestedPalletCountText { get; }

    public string SuggestedPalletNumbersText { get; }

    public string SuggestedPalletsText { get; }

    public string AvailablePalletsText { get; }

    public string TarimaSizeText { get; }

    public string SatellitePreparedText { get; }

    public bool IsSatellitePrepared { get; }

    public string SatelliteZonesText { get; }

    public bool HasPending { get; }

    public string StatusLabel { get; }

    public Brush StatusBackgroundBrush { get; }

    public Brush StatusForegroundBrush { get; }

    public Brush StatusBorderBrush { get; }

    public string ToClipboardText() => string.Join(
        "\t",
        ExpeditionNumber,
        Article,
        Description,
        DemandText,
        PrincipalInventoryText,
        SatelliteAvailableText,
        PendingTransitText,
        CalculatedTransferNeedText,
        TransferSuggestionText,
        PalletRoundUpSurplusText,
        SuggestedPalletCountText,
        SuggestedPalletNumbersText,
        SuggestedPalletsText.Replace(Environment.NewLine, "; "),
        TarimaSizeText,
        SatellitePreparedText,
        SatelliteZonesText.Replace(Environment.NewLine, "; "),
        RemainingShortageText);

    private static string FormatPalletNumbers(IReadOnlyList<PalletInventoryDetail> pallets)
    {
        var numbers = pallets
            .Select(pallet => pallet.Pallet)
            .Where(pallet => !string.IsNullOrWhiteSpace(pallet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return numbers.Length == 0 ? "Sin pallets" : string.Join("; ", numbers);
    }

    private static string FormatPallets(IReadOnlyList<PalletInventoryDetail> pallets)
    {
        if (pallets.Count == 0)
        {
            return "Sin pallets";
        }

        return string.Join(
            Environment.NewLine,
            pallets.Select(pallet =>
                $"{FormatDate(pallet.ValidationDate)} - {pallet.Pallet} - {pallet.Quantity:N0} - {FormatLocation(pallet.Location)} - {pallet.StorageZone} - {StorageOriginClassifier.Resolve(pallet.StorageZone)}"));
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd") ?? "Sin fecha";

    private static string FormatLocation(string location) =>
        string.IsNullOrWhiteSpace(location) ? "Sin ubicacion" : location.Trim();

    private string ResolveStatusLabel()
    {
        if (IsSatellitePrepared)
        {
            return "Alistado sat.";
        }

        if (TransferQuantity > 0 && HasPending)
        {
            return "Parcial externa";
        }

        if (TransferQuantity > 0)
        {
            return "Traslado sugerido";
        }

        return SatelliteQuantity <= 0 && HasPending
            ? "Sin satelite"
            : "Cubierto";
    }

    private SolidColorBrush ResolveStatusBackgroundBrush() =>
        StatusLabel switch
        {
            "Traslado sugerido" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xE6, 0xF7, 0xF4)),
            "Parcial externa" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0xFB, 0xEB)),
            "Sin satelite" => new SolidColorBrush(ColorHelper.FromArgb(255, 0x00, 0x00, 0x00)),
            "Alistado sat." => new SolidColorBrush(ColorHelper.FromArgb(255, 0xF3, 0xF4, 0xF6)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0xFF, 0xFF)),
        };

    private SolidColorBrush ResolveStatusForegroundBrush() =>
        StatusLabel switch
        {
            "Traslado sugerido" => new SolidColorBrush(ColorHelper.FromArgb(255, 0x00, 0x8F, 0x7A)),
            "Parcial externa" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xB4, 0x53, 0x09)),
            "Sin satelite" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0xFF, 0xFF)),
            "Alistado sat." => new SolidColorBrush(ColorHelper.FromArgb(255, 0x37, 0x41, 0x51)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0x4B, 0x55, 0x63)),
        };

    private SolidColorBrush ResolveStatusBorderBrush() =>
        StatusLabel switch
        {
            "Traslado sugerido" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xC2, 0xEF, 0xEB)),
            "Parcial externa" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFE, 0xD7, 0xAA)),
            "Sin satelite" => new SolidColorBrush(ColorHelper.FromArgb(255, 0x00, 0x00, 0x00)),
            "Alistado sat." => new SolidColorBrush(ColorHelper.FromArgb(255, 0xE5, 0xE7, 0xEB)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0xE5, 0xE7, 0xEB)),
        };
}
