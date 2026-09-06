using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsTaskViewFSE.Helpers;

/// <summary>
/// Converts a null value to <see cref="Visibility.Collapsed"/> and any non-null value to
/// <see cref="Visibility.Visible"/>. Used to hide the left/right carousel slots when fewer
/// than 3 windows are open (e.g. no previous/next window exists yet).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
