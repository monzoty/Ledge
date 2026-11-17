using System.Globalization;
using System.Windows.Data;

namespace Ledge.Converters;

/// <summary>
/// Lets a group of RadioButtons edit one property (an enum like ThemeMode, or
/// a number like the timer preset): each button is checked when the value
/// equals its ConverterParameter, and checking it sets the value to that
/// parameter. The parameter must have the same type as the property (for a
/// number, use an x:Int32 resource, not the text "25").
/// <code>
/// IsChecked="{Binding Theme, Converter={StaticResource EqualsToBool}, ConverterParameter={x:Static core:ThemeMode.Light}}"
/// </code>
/// </summary>
public sealed class EqualsToBoolConverter : IValueConverter
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
