using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace NEREA.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Collapsed;
}

public sealed class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TimeSpan t && t > TimeSpan.Zero)
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes}:{t.Seconds:00}";
        return "--:--";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        TimeSpan.Zero;
}

/// <summary>Visible when the bound collection is non-empty.</summary>
public sealed class CollectionCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        int count = 0;
        if (value is System.Collections.ICollection c) count = c.Count;
        else if (value is System.Collections.IEnumerable e) count = e.Cast<object>().Count();
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value;
}

/// <summary>Converts a "#RRGGBB" hex string into a SolidColorBrush.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var s = (value as string)?.TrimStart('#') ?? "3E7E8E";
        try
        {
            byte r = System.Convert.ToByte(s[..2], 16);
            byte g = System.Convert.ToByte(s[2..4], 16);
            byte b = System.Convert.ToByte(s[4..6], 16);
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, r, g, b));
        }
        catch
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Teal);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => "";
}
