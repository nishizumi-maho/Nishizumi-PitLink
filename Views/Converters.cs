using System.Globalization;
using System.Windows.Data;

namespace NishizumiPitLink.Views;

public class BoolToConnectedTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Connected" : "Disconnected";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Shows a UTC timestamp in the user's own time zone. Without this the grid renders the raw UTC
/// value in local-looking format, so a car seen a minute ago reads as hours off.
/// </summary>
public class UtcToLocalTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime value_)
            return value;

        var utc = value_.Kind switch
        {
            DateTimeKind.Utc => value_,
            DateTimeKind.Local => value_.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value_, DateTimeKind.Utc), // stored as UTC, just unlabelled
        };

        return utc.ToLocalTime().ToString("g", culture);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
