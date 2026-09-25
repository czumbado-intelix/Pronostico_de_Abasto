using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace PronosticosAbasto.ViewModels;

public sealed class ZoneDetailRowViewModel
{
    public ZoneDetailRowViewModel(string storageZone, decimal quantity, bool isSatellite)
    {
        StorageZone = storageZone;
        Quantity = quantity;
        IsSatellite = isSatellite;
        ZoneTypeLabel = isSatellite ? "SATELITAL" : "PRINCIPAL";
        ZoneTypeAccentBrush = new SolidColorBrush(isSatellite
            ? ColorHelper.FromArgb(255, 22, 163, 74)
            : ColorHelper.FromArgb(255, 34, 197, 94));
        ZoneTypeBorderBrush = new SolidColorBrush(isSatellite
            ? ColorHelper.FromArgb(120, 22, 163, 74)
            : ColorHelper.FromArgb(96, 14, 116, 38));
        ZoneTypeBackgroundBrush = new SolidColorBrush(isSatellite
            ? ColorHelper.FromArgb(38, 22, 163, 74)
            : ColorHelper.FromArgb(22, 34, 197, 94));
    }

    public string StorageZone { get; }

    public decimal Quantity { get; }

    public bool IsSatellite { get; }

    public string ZoneTypeLabel { get; }

    public Brush ZoneTypeAccentBrush { get; }

    public Brush ZoneTypeBorderBrush { get; }

    public Brush ZoneTypeBackgroundBrush { get; }

    public string QuantityText => Quantity.ToString("N0");
}
