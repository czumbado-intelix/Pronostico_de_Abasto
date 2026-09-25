using CommunityToolkit.Mvvm.ComponentModel;
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
        CreatedAtText = item.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        DetailsTooltip =
            $"{Article} · {Pallet} · {QuantityText} uds · {Location} · {StorageZone} · {Origin} · pedido {CreatedAtText}";
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

    public string CreatedAtText { get; }

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
        CreatedAtText);

    partial void OnIsSelectedChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _onSelectionChanged?.Invoke(this);
    }
}
