using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PronosticosAbasto.Converters;

/// <summary>
/// True → primary accent brush (active filter/tab), false → muted brush. Fixed
/// hex (light palette) so it resolves regardless of theme dictionaries.
/// </summary>
public sealed class BoolToAccentBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Accent = new(ColorHelper.FromArgb(255, 0x00, 0xA8, 0x8F));
    private static readonly SolidColorBrush Muted = new(ColorHelper.FromArgb(255, 0x00, 0x00, 0x00));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Accent : Muted;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
