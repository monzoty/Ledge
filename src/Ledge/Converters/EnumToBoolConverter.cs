using System.Globalization;
using System.Windows.Data;

namespace Ledge.Converters;

/// <summary>
/// Lets a group of RadioButtons edit one enum property: each button is
/// checked when the value equals its ConverterParameter, and checking it sets
/// the value to that parameter.
/// <code>
/// IsChecked="{Binding Theme, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static core:ThemeMode.Light}}"
/// </code>
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value, parameter);

    /// <summary>
    /// Only the button being CHECKED sets the value. A button being unchecked
    /// (because another one was picked) must not change anything.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}
