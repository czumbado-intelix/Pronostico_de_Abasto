using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PronosticosAbasto.Converters;

/// <summary>True → white card surface (active tab/segment), false → transparent.</summary>
public sealed class BoolToCardBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Card = new(ColorHelper.FromArgb(255, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush Transparent = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Card : Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
