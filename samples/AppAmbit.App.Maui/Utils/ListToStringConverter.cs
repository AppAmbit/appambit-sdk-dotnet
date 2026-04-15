using System.Collections.Generic;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace AppAmbitTestingApp.Utils;

public class ListToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<string> list)
            return string.Join(", ", list);
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
