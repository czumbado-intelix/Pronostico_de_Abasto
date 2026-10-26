using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PronosticosAbasto.Converters;

/// <summary>True -> solid primary tab background, false -> transparent.</summary>
public sealed class BoolToSelectedTabBackgroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(ColorHelper.FromArgb(255, 0x00, 0xA8, 0x8F));
    private static readonly SolidColorBrush Transparent = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>True -> white selected tab text, false -> black.</summary>
public sealed class BoolToSelectedTabForegroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(Colors.White);
    private static readonly SolidColorBrush Normal = new(Colors.Black);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>True -> white icon on active rail, false -> muted icon on light rail.</summary>
public sealed class BoolToCollapsedSidebarIconForegroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(Colors.White);
    private static readonly SolidColorBrush Normal = new(ColorHelper.FromArgb(255, 0x4B, 0x55, 0x63));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>True -> primary tab border, false -> neutral border.</summary>
public sealed class BoolToSelectedTabBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(ColorHelper.FromArgb(255, 0x00, 0xA8, 0x8F));
    private static readonly SolidColorBrush Normal = new(ColorHelper.FromArgb(255, 0xD9, 0xE2, 0xE0));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>True -> soft green sidebar item background, false -> transparent.</summary>
public sealed class BoolToSidebarItemBackgroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(ColorHelper.FromArgb(255, 0xE5, 0xF7, 0xF4));
    private static readonly SolidColorBrush Transparent = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>True -> primary sidebar text, false -> muted sidebar text.</summary>
public sealed class BoolToSidebarItemForegroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(ColorHelper.FromArgb(255, 0x00, 0xA8, 0x8F));
    private static readonly SolidColorBrush Normal = new(ColorHelper.FromArgb(255, 0x4B, 0x55, 0x63));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>True -> primary sidebar selection rail, false -> transparent.</summary>
public sealed class BoolToSidebarItemRailBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(ColorHelper.FromArgb(255, 0x00, 0xA8, 0x8F));
    private static readonly SolidColorBrush Transparent = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Stitch segmented tabs use a white selected surface over a neutral rail.</summary>
public sealed class BoolToSegmentTabBackgroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(Colors.White);
    private static readonly SolidColorBrush Normal = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Stitch segmented tabs use green selected text and muted unselected text.</summary>
public sealed class BoolToSegmentTabForegroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = new(ColorHelper.FromArgb(255, 0x00, 0xA8, 0x8F));
    private static readonly SolidColorBrush Normal = new(ColorHelper.FromArgb(255, 0x6B, 0x72, 0x80));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Selected : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
