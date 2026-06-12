using System.Globalization;
using System.Windows.Data;

namespace StickyNotes.Converters;

/// <summary>
/// 比较两个字符串是否相等，用于 DataTrigger
/// </summary>
public class StringEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        return string.Equals(values[0]?.ToString(), values[1]?.ToString(), StringComparison.Ordinal);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
