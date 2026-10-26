using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using PronosticosAbasto.Core.Storage;
using PronosticosAbasto.Services;

namespace PronosticosAbasto.ViewModels;

public sealed partial class TransitRowViewModel : ObservableObject, IClipboardRow
{
    private readonly Action<TransitRowViewModel>? _onSelectionChanged;
    private bool _initializing = true;

    public TransitRowViewModel(TransferTransitItem item, Action<TransitRowViewModel>? onSelectionChanged)
    {
        _onSelectionChanged = onSelectionChanged;
        Id = item.Id;
        Company = item.Company;
        PeriodKey = item.PeriodKey;
        Article = item.Article;
        Description = item.Description;
        Pallet = item.Pallet;
        Quantity = item.Quantity;
        QuantityText = item.Quantity.ToString("N0");
        StorageZone = item.StorageZone;
        Location = string.IsNullOrWhiteSpace(item.Location) ? "Sin ubicacion" : item.Location;
        ValidationDateText = item.ValidationDate?.ToString("yyyy-MM-dd") ?? "Sin fecha";
        TarimaSize = string.IsNullOrWhiteSpace(item.TarimaSize) ? "—" : item.TarimaSize;
        Origin = item.Origin;
        OriginLocationText = string.IsNullOrWhiteSpace(StorageZone)
            ? Location
            : $"{StorageZone} - {Location}";
        CreatedAtText = item.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        ReceivedAtText = item.ReceivedAt?.ToString("dd/MM/yy HH:mm") ?? "Pendiente";
        ReconciledAtText = item.ReconciledAt?.ToString("dd/MM/yy HH:mm") ?? "Pendiente";
        IsReconciled = item.IsReconciled;
        IsReceivedPendingInventory = item.IsReceived && !item.IsReconciled;
        IsPending = !item.IsReceived && !item.IsReconciled;
        CanConfirmReceived = IsPending;
        StatusLabel = item.IsReconciled
            ? "Historial"
            : item.IsReceived
                ? "Historial"
                : "En tránsito";
        StatusDateText = item.IsReconciled
            ? ReconciledAtText
            : item.IsReceived
                ? ReceivedAtText
                : CreatedAtText;
        StatusBackgroundBrush = ResolveStatusBackgroundBrush();
        StatusForegroundBrush = ResolveStatusForegroundBrush();
        StatusBorderBrush = ResolveStatusBorderBrush();
        DetailsTooltip =
            $"{Article} · {Pallet} · {QuantityText} uds · {Location} · {StorageZone} · {Origin} · {StatusLabel} · pedido {CreatedAtText}";
        _initializing = false;
    }

    public string Id { get; }

    public string Company { get; }

    public string PeriodKey { get; }

    public string Article { get; }

    public string Description { get; }

    public string Pallet { get; }

    public decimal Quantity { get; }

    public string QuantityText { get; }

    public string StorageZone { get; }

    public string Location { get; }

    public string ValidationDateText { get; }

    public string TarimaSize { get; }

    public string Origin { get; }

    public string OriginLocationText { get; }

    public string CreatedAtText { get; }

    public string ReceivedAtText { get; }

    public string ReconciledAtText { get; }

    public string StatusDateText { get; }

    public bool IsPending { get; }

    public bool IsReceivedPendingInventory { get; }

    public bool IsReconciled { get; }

    public bool CanConfirmReceived { get; }

    public string StatusLabel { get; }

    public Brush StatusBackgroundBrush { get; }

    public Brush StatusForegroundBrush { get; }

    public Brush StatusBorderBrush { get; }

    public string DetailsTooltip { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string ToClipboardText() => string.Join(
        "\t",
        Article,
        Description,
        Pallet,
        QuantityText,
        Location,
        StorageZone,
        ValidationDateText,
        TarimaSize,
        Origin,
        CreatedAtText,
        ReceivedAtText,
        ReconciledAtText,
        StatusLabel);

    private SolidColorBrush ResolveStatusBackgroundBrush() =>
        StatusLabel switch
        {
            "Historial" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xE6, 0xF7, 0xF4)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0xF3, 0xF4, 0xF6)),
        };

    private SolidColorBrush ResolveStatusForegroundBrush() =>
        StatusLabel switch
        {
            "Historial" => new SolidColorBrush(ColorHelper.FromArgb(255, 0x00, 0x8F, 0x7A)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0x37, 0x41, 0x51)),
        };

    private SolidColorBrush ResolveStatusBorderBrush() =>
        StatusLabel switch
        {
            "Historial" => new SolidColorBrush(ColorHelper.FromArgb(255, 0xC2, 0xEF, 0xEB)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0xE5, 0xE7, 0xEB)),
        };

    partial void OnIsSelectedChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _onSelectionChanged?.Invoke(this);
    }
}
