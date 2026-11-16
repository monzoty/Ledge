using System.Globalization;
using System.Windows.Data;

namespace Ledge.Converters;

/// <summary>
/// Shows text in UPPERCASE (the Caption style, DESIGN.md §3). WPF has no
/// CSS-like "text-transform", so a binding converter does it. The copy
/// itself stays in normal case in Strings.xaml.
/// https://learn.microsoft.com/dotnet/api/system.windows.data.ivalueconverter
/// </summary>
public sealed class UpperCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string text ? culture.TextInfo.ToUpper(text) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Uppercase text can't be turned back into the original.");
}
