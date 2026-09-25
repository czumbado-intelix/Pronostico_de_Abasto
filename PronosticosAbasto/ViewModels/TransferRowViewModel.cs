using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// A single "para traer de bodega externa" row. FIFO pallets are selected by
/// default, but the operator can adjust the selected pallets before sending.
/// </summary>
public partial class TransferRowViewModel : ObservableObject, IClipboardRow
{
    private readonly Action<TransferRowViewModel>? _onOrderChanged;
    private readonly Action<TransferRowViewModel>? _onQuantityChanged;
    private bool _initializing = true;
    private bool _syncingTransferQuantityInput;
    private bool _updatingPalletSelection;

    public TransferRowViewModel(
        ArticleAnalysisResult article,
        bool isOrdered,
        Action<TransferRowViewModel>? onOrderChanged,
        string description = "",
        string coverageText = "",
        bool isSatellitePrepared = false,
        string tarimaSizeText = "—",
        Action<TransferRowViewModel>? onQuantityChanged = null)
    {
        ArgumentNullException.ThrowIfNull(article);
        _onOrderChanged = onOrderChanged;
        _onQuantityChanged = onQuantityChanged;

        Article = article.Article;
        Description = string.IsNullOrWhiteSpace(description) ? "Sin descripcion" : description.Trim();
        ArticleDescriptionText = ArticleDisplayFormatter.FormatWithDescription(Article, Description);
        Status = article.Status;
        StatusLabel = ToStatusLabel(article.Status);
        CalculatedTransferNeedQuantity = article.CalculatedTransferNeedQuantity;
        PendingTransitQuantity = article.PendingTransitQuantity;
        SatelliteAvailableQuantity = article.SatelliteInventoryQuantity;
        PrincipalInventoryQuantity = article.PrincipalInventoryQuantity;
        CoverageWeeksValue = article.CoverageWeeks;
        CoverageText = string.IsNullOrEmpty(coverageText) ? "—" : coverageText;
        ForecastText = article.ForecastQuantity.ToString("N0");
        PrincipalInventoryText = article.PrincipalInventoryQuantity.ToString("N0");
        PendingTransitText = article.PendingTransitQuantity.ToString("N0");
        SatelliteAvailableText = article.SatelliteInventoryQuantity.ToString("N0");
        CalculatedTransferNeedText = article.CalculatedTransferNeedQuantity.ToString("N0");
        TarimaSizeText = string.IsNullOrWhiteSpace(tarimaSizeText) ? "—" : tarimaSizeText.Trim();
        IsSatellitePrepared = isSatellitePrepared;
        CanOrder = !isSatellitePrepared;
        SatellitePreparedText = isSatellitePrepared ? "Si" : "No";

        PalletOptions = BuildPalletOptions(article);
        AvailablePalletsText = FormatPallets(PalletOptions.Select(option => option.ToPalletDetail()));
        TransferQuantityInput = TransferSuggestionText;

        SatelliteZonesText = string.Join(
            Environment.NewLine,
            article.SatelliteZones
                .Where(zone => zone.Quantity > 0)
                .OrderByDescending(zone => zone.Quantity)
                .Select(zone => $"{zone.Quantity:N0} — {zone.StorageZone}"));
        if (string.IsNullOrEmpty(SatelliteZonesText))
        {
            SatelliteZonesText = "Sin zona";
        }

        StatusBrush = new SolidColorBrush(article.Status switch
        {
            CoverageStatus.Critical => ColorHelper.FromArgb(255, 0xC4, 0x2B, 0x1C),
            CoverageStatus.Warning => ColorHelper.FromArgb(255, 0x9D, 0x5D, 0x00),
            _ => ColorHelper.FromArgb(255, 0x0F, 0x7B, 0x0F),
        });

        IsOrdered = isOrdered && CanOrder;
        _initializing = false;
    }

    public string Article { get; }

    public string Description { get; }

    public string ArticleDescriptionText { get; }

    public string ForecastText { get; }

    public string PrincipalInventoryText { get; }

    public string PendingTransitText { get; }

    public string TransferSuggestionText => TransferSuggestionQuantity.ToString("N0");

    public string SatelliteAvailableText { get; }

    public string SatelliteZonesText { get; }

    public string RemainingShortageText => RemainingShortageQuantity.ToString("N0");

    public string SuggestedPalletCountText => SuggestedPalletCount.ToString("N0");

    public string SuggestedPalletNumbersText => FormatPalletNumbers(SelectedPallets);

    public string SuggestedPalletsText => FormatPallets(SelectedPallets);

    public string AvailablePalletsText { get; }

    public string TarimaSizeText { get; }

    public string SatellitePreparedText { get; }

    public string CalculatedTransferNeedText { get; }

    public string PalletRoundUpSurplusText => PalletRoundUpSurplusQuantity.ToString("N0");

    public bool IsSatellitePrepared { get; }

    public bool CanOrder { get; }

    public bool HasPending => RemainingShortageQuantity > 0;

    public CoverageStatus Status { get; }

    public string StatusLabel { get; }

    public decimal CalculatedTransferNeedQuantity { get; }

    public decimal TransferSuggestionQuantity => SelectedPallets.Sum(pallet => pallet.Quantity);

    public decimal PendingTransitQuantity { get; }

    public decimal NewTransferSuggestionQuantity => Math.Max(0m, TransferSuggestionQuantity - PendingTransitQuantity);

    public decimal SatelliteAvailableQuantity { get; }

    public decimal PrincipalInventoryQuantity { get; }

    public int SuggestedPalletCount => SelectedPallets.Count;

    public decimal PalletRoundUpSurplusQuantity => Math.Max(0m, TransferSuggestionQuantity - CalculatedTransferNeedQuantity);

    public decimal RemainingShortageQuantity => Math.Max(0m, CalculatedTransferNeedQuantity - TransferSuggestionQuantity);

    public decimal? CoverageWeeksValue { get; }

    public IReadOnlyList<PalletInventoryDetail> SelectedPallets =>
        PalletOptions.Where(option => option.IsSelected).Select(option => option.ToPalletDetail()).ToArray();

    public ObservableCollection<PalletSelectionViewModel> PalletOptions { get; }

    /// <summary>Weeks/months of forecast demand the principal stock covers, e.g. "1.5 sem".</summary>
    public string CoverageText { get; }

    public Brush StatusBrush { get; }

    public string DetailsTooltip =>
        $"Forecast {ForecastText}  ·  Principal {PrincipalInventoryText}  ·  Necesidad {CalculatedTransferNeedText}  ·  A traer {TransferSuggestionText}  ·  Sobrante {PalletRoundUpSurplusText}  ·  Transito pendiente {PendingTransitText}  ·  Palets {SuggestedPalletCountText}  ·  Tam. {TarimaSizeText}  ·  Alistado sat. {SatellitePreparedText}  ·  Pendiente {RemainingShortageText}";

    [ObservableProperty]
    public partial bool IsOrdered { get; set; }

    [ObservableProperty]
    public partial string TransferQuantityInput { get; set; } = string.Empty;

    public string ToClipboardText() => string.Join(
        "\t",
        Article,
        Description,
        CoverageText,
        ForecastText,
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

    private ObservableCollection<PalletSelectionViewModel> BuildPalletOptions(ArticleAnalysisResult article)
    {
        var selectedKeys = article.SuggestedPallets
            .Select(pallet => PalletIdentity.Build(article.Article, pallet))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var source = article.AvailablePallets.Count > 0
            ? article.AvailablePallets
            : article.SuggestedPallets;

        return new ObservableCollection<PalletSelectionViewModel>(
            source.Select(pallet => new PalletSelectionViewModel(
                pallet,
                selectedKeys.Contains(PalletIdentity.Build(article.Article, pallet)),
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
        OnPropertyChanged(nameof(TransferSuggestionQuantity));
        OnPropertyChanged(nameof(NewTransferSuggestionQuantity));
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
        OnPropertyChanged(nameof(DetailsTooltip));
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

    private static string FormatPalletNumbers(IReadOnlyList<PalletInventoryDetail> pallets)
    {
        var numbers = pallets
            .Select(pallet => pallet.Pallet)
            .Where(pallet => !string.IsNullOrWhiteSpace(pallet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return numbers.Length == 0 ? "Sin pallets" : string.Join("; ", numbers);
    }

    private static string FormatPallets(IEnumerable<PalletInventoryDetail> pallets)
    {
        var lines = pallets
            .Select(pallet =>
                $"{FormatDate(pallet.ValidationDate)} - {pallet.Pallet} - {pallet.Quantity:N0} - {FormatLocation(pallet.Location)} - {pallet.StorageZone} - {StorageOriginClassifier.Resolve(pallet.StorageZone)}")
            .ToArray();

        return lines.Length == 0 ? "Sin pallets" : string.Join(Environment.NewLine, lines);
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd") ?? "Sin fecha";

    private static string FormatLocation(string location) =>
        string.IsNullOrWhiteSpace(location) ? "Sin ubicacion" : location.Trim();

    private static string ToStatusLabel(CoverageStatus status) =>
        status switch
        {
            CoverageStatus.Critical => "Rojo",
            CoverageStatus.Warning => "Amarillo",
            _ => "Verde",
        };

    partial void OnIsOrderedChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _onOrderChanged?.Invoke(this);
    }
}

public sealed partial class PalletSelectionViewModel : ObservableObject
{
    private readonly PalletInventoryDetail _pallet;
    private readonly Action _onSelectionChanged;

    public PalletSelectionViewModel(PalletInventoryDetail pallet, bool isSelected, Action onSelectionChanged)
    {
        _pallet = pallet;
        _onSelectionChanged = onSelectionChanged;
        Pallet = pallet.Pallet;
        Quantity = pallet.Quantity;
        QuantityText = pallet.Quantity.ToString("N0");
        StorageZone = pallet.StorageZone;
        Location = string.IsNullOrWhiteSpace(pallet.Location) ? "Sin ubicacion" : pallet.Location.Trim();
        ValidationDateText = pallet.ValidationDate?.ToString("yyyy-MM-dd") ?? "Sin fecha";
        PalletType = pallet.PalletType;
        Origin = StorageOriginClassifier.Resolve(pallet.StorageZone);
        IsSelected = isSelected;
    }

    public string Pallet { get; }

    public decimal Quantity { get; }

    public string QuantityText { get; }

    public string StorageZone { get; }

    public string Location { get; }

    public string ValidationDateText { get; }

    public string PalletType { get; }

    public string Origin { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public PalletInventoryDetail ToPalletDetail() => _pallet;

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged();
}
