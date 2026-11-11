using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace MyDrawVector.Helpers;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is not Visibility.Visible;
    }
}
