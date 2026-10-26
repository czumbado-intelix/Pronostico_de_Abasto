using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PronosticosAbasto.Core.Analysis;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// One expedition line in the Expediciones view: its demand, how much the
/// principal covers, how much to bring from satellite (and from which zones), and
/// what stays pending.
/// </summary>
public sealed partial class ExpedicionRowViewModel : ObservableObject, IClipboardRow
{
    private readonly Action<ExpedicionRowViewModel>? _onOrderChanged;
    private readonly Action<ExpedicionRowViewModel>? _onQuantityChanged;
    private bool _initializing = true;
    private bool _syncingTransferQuantityInput;
    private bool _updatingPalletSelection;

    public ExpedicionRowViewModel(
        ExpeditionLineResult line,
        bool isSatellitePrepared = false,
        string tarimaSizeText = "—",
        bool isOrdered = false,
        Action<ExpedicionRowViewModel>? onOrderChanged = null,
        Action<ExpedicionRowViewModel>? onQuantityChanged = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        _onOrderChanged = onOrderChanged;
        _onQuantityChanged = onQuantityChanged;

        Article = line.Article;
        Description = string.IsNullOrWhiteSpace(line.Description) ? "Sin descripcion" : line.Description.Trim();
        ExpeditionNumber = string.IsNullOrWhiteSpace(line.ExpeditionNumber)
            ? ExpeditionNumberFormatter.EmptyPlaceholder
            : line.ExpeditionNumber.Trim();
        var expeditionNumbers = ExpeditionNumberFormatter.ExtractNumbers(ExpeditionNumber);
        ExpeditionItems = expeditionNumbers
            .Select((number, index) => new ExpeditionNumberItemViewModel($"{index + 1}.", number))
            .ToArray();
        PrimaryExpeditionText = ExpeditionNumberFormatter.FormatPrimary(expeditionNumbers);
        ExtraExpeditionsBadgeText = ExpeditionNumberFormatter.FormatExtraBadge(expeditionNumbers);
        ExtraExpeditionsBadgeVisibility = expeditionNumbers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        HasExpeditionsVisibility = expeditionNumbers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ExpeditionCountTitle = ExpeditionNumberFormatter.FormatCountTitle(expeditionNumbers);
        CopyExpeditionsButtonText = ExpeditionNumberFormatter.FormatCopyButtonText(expeditionNumbers);
        ExpeditionListMultilineText = ExpeditionNumberFormatter.FormatMultiline(expeditionNumbers);
        ExpeditionTooltipText = ExpeditionNumberFormatter.FormatTooltip(expeditionNumbers);
        ArticleDescriptionText = ArticleDisplayFormatter.FormatWithDescription(Article, Description);
        DemandQuantity = line.DemandQuantity;
        PrincipalQuantity = line.PrincipalInventoryQuantity;
        SatelliteQuantity = line.SatelliteInventoryQuantity;
        PendingTransitQuantity = line.PendingTransitQuantity;
        NewTransferQuantity = line.NewTransferSuggestionQuantity;
        CalculatedTransferNeedQuantity = line.CalculatedTransferNeedQuantity;
        DemandText = line.DemandQuantity.ToString("N0");
        PrincipalInventoryText = line.PrincipalInventoryQuantity.ToString("N0");
        DifferenceText = DifferenceQuantity.ToString("N0");
        SatelliteAvailableText = line.SatelliteInventoryQuantity.ToString("N0");
        PendingTransitText = line.PendingTransitQuantity.ToString("N0");
        CalculatedTransferNeedText = line.CalculatedTransferNeedQuantity.ToString("N0");
        TarimaSizeText = string.IsNullOrWhiteSpace(tarimaSizeText) ? "—" : tarimaSizeText.Trim();
        IsSatellitePrepared = isSatellitePrepared;
        PalletOptions = BuildPalletOptions(line);
        CanOrder = !isSatellitePrepared && line.TransferSuggestionQuantity > 0m && PalletOptions.Count > 0;
        SatellitePreparedText = isSatellitePrepared ? "Si" : "No";
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

        AvailablePalletsText = FormatPallets(
            (line.AvailablePallets.Count > 0 ? line.AvailablePallets : line.SuggestedPallets));
        TransferQuantityInput = TransferSuggestionText;
        IsOrdered = isOrdered && CanOrder;
        _initializing = false;
    }

    public string Article { get; }

    public string Description { get; }

    public string ExpeditionNumber { get; }

    public IReadOnlyList<ExpeditionNumberItemViewModel> ExpeditionItems { get; }

    public string PrimaryExpeditionText { get; }

    public string ExtraExpeditionsBadgeText { get; }

    public Visibility ExtraExpeditionsBadgeVisibility { get; }

    public Visibility HasExpeditionsVisibility { get; }

    public string ExpeditionCountTitle { get; }

    public string CopyExpeditionsButtonText { get; }

    public string ExpeditionListMultilineText { get; }

    public string ExpeditionTooltipText { get; }

    public string ArticleDescriptionText { get; }

    public decimal DemandQuantity { get; }

    public decimal PrincipalQuantity { get; }

    public decimal SatelliteQuantity { get; }

    public decimal PendingTransitQuantity { get; }

    public decimal TransferQuantity => TransferSuggestionQuantity;

    public decimal NewTransferQuantity { get; }

    public decimal RemainingShortageQuantity => Math.Max(0m, CalculatedTransferNeedQuantity - TransferSuggestionQuantity);

    public decimal CalculatedTransferNeedQuantity { get; }

    public decimal PalletRoundUpSurplusQuantity => Math.Max(0m, TransferSuggestionQuantity - CalculatedTransferNeedQuantity);

    public int SuggestedPalletCount => SelectedPallets.Count;

    public string DemandText { get; }

    public string PrincipalInventoryText { get; }

    public string DifferenceText { get; }

    public string SatelliteAvailableText { get; }

    public string PendingTransitText { get; }

    public string TransferSuggestionText => TransferSuggestionQuantity.ToString("N0");

    public string RemainingShortageText => RemainingShortageQuantity.ToString("N0");

    public string CalculatedTransferNeedText { get; }

    public string PalletRoundUpSurplusText => PalletRoundUpSurplusQuantity.ToString("N0");

    public string SuggestedPalletCountText => SuggestedPalletCount.ToString("N0");

    public string SuggestedPalletNumbersText => FormatPalletNumbers(SelectedPallets);

    public string SuggestedPalletsText => FormatPallets(SelectedPallets);

    public string AvailablePalletsText { get; }

    public string TarimaSizeText { get; }

    public string SatellitePreparedText { get; }

    public bool IsSatellitePrepared { get; }

    public bool CanOrder { get; }

    public string SatelliteZonesText { get; }

    public bool HasPending => RemainingShortageQuantity > 0;

    public decimal DifferenceQuantity => PrincipalQuantity - DemandQuantity;

    public decimal TransferSuggestionQuantity => SelectedPallets.Sum(pallet => pallet.Quantity);

    public IReadOnlyList<PalletInventoryDetail> SelectedPallets =>
        PalletOptions.Where(option => option.IsSelected).Select(option => option.ToPalletDetail()).ToArray();

    public ObservableCollection<PalletSelectionViewModel> PalletOptions { get; }

    [ObservableProperty]
    public partial bool IsOrdered { get; set; }

    [ObservableProperty]
    public partial string TransferQuantityInput { get; set; } = string.Empty;

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

    private ObservableCollection<PalletSelectionViewModel> BuildPalletOptions(ExpeditionLineResult line)
    {
        var selectedKeys = line.SuggestedPallets
            .Select(pallet => PalletIdentity.Build(line.Article, pallet))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var source = line.AvailablePallets.Count > 0
            ? line.AvailablePallets
            : line.SuggestedPallets;

        return new ObservableCollection<PalletSelectionViewModel>(
            source.Select(pallet => new PalletSelectionViewModel(
                pallet,
                selectedKeys.Contains(PalletIdentity.Build(line.Article, pallet)),
                OnPalletSelectionChanged)));
    }

    private void OnPalletSelectionChanged()
    {
        if (_initializing || _updatingPalletSelection)
        {
            return;
        }

        SyncTransferQuantityInput();
        NotifyTransferQuantitiesChanged();
        _onQuantityChanged?.Invoke(this);
    }

    partial void OnTransferQuantityInputChanged(string value)
    {
        if (_initializing || _syncingTransferQuantityInput)
        {
            return;
        }

        if (!TryParseQuantity(value, out var requestedQuantity))
        {
            return;
        }

        SelectFifoPalletsForQuantity(requestedQuantity);
        SyncTransferQuantityInput();
        NotifyTransferQuantitiesChanged();
        _onQuantityChanged?.Invoke(this);
    }

    private void SelectFifoPalletsForQuantity(decimal requestedQuantity)
    {
        var remainingQuantity = Math.Max(0m, requestedQuantity);
        _updatingPalletSelection = true;
        try
        {
            foreach (var option in PalletOptions)
            {
                option.IsSelected = false;
            }

            foreach (var option in PalletOptions
                .Where(option => option.Quantity > 0m)
                .OrderBy(option => option.ToPalletDetail().ValidationDate ?? DateOnly.MaxValue)
                .ThenBy(option => option.Pallet, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.StorageZone, StringComparer.OrdinalIgnoreCase))
            {
                if (remainingQuantity <= 0m)
                {
                    break;
                }

                option.IsSelected = true;
                remainingQuantity -= option.Quantity;
            }
        }
        finally
        {
            _updatingPalletSelection = false;
        }
    }

    private void SyncTransferQuantityInput()
    {
        _syncingTransferQuantityInput = true;
        TransferQuantityInput = TransferSuggestionText;
        _syncingTransferQuantityInput = false;
    }

    private void NotifyTransferQuantitiesChanged()
    {
        OnPropertyChanged(nameof(TransferQuantity));
        OnPropertyChanged(nameof(TransferSuggestionQuantity));
        OnPropertyChanged(nameof(PalletRoundUpSurplusQuantity));
        OnPropertyChanged(nameof(RemainingShortageQuantity));
        OnPropertyChanged(nameof(SuggestedPalletCount));
        OnPropertyChanged(nameof(SelectedPallets));
        OnPropertyChanged(nameof(TransferSuggestionText));
        OnPropertyChanged(nameof(PalletRoundUpSurplusText));
        OnPropertyChanged(nameof(RemainingShortageText));
        OnPropertyChanged(nameof(SuggestedPalletCountText));
        OnPropertyChanged(nameof(SuggestedPalletNumbersText));
        OnPropertyChanged(nameof(SuggestedPalletsText));
        OnPropertyChanged(nameof(TransferQuantityInput));
        OnPropertyChanged(nameof(HasPending));
    }

    private static bool TryParseQuantity(string value, out decimal quantity)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            quantity = 0m;
            return true;
        }

        var trimmed = value.Trim();
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out quantity) ||
            decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity) ||
            decimal.TryParse(trimmed.Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.CurrentCulture, out quantity) ||
            decimal.TryParse(trimmed.Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out quantity);
    }

    partial void OnIsOrderedChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _onOrderChanged?.Invoke(this);
    }

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

public sealed record ExpeditionNumberItemViewModel(string IndexLabel, string Number);
