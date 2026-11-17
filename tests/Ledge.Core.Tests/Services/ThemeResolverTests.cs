using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class ThemeResolverTests
{
    [Theory]
    [InlineData(ThemeMode.System, true, false)]  // Windows light → light
    [InlineData(ThemeMode.System, false, true)]  // Windows dark → dark
    [InlineData(ThemeMode.Dark, true, true)]     // explicit choice ignores Windows
    [InlineData(ThemeMode.Dark, false, true)]
    [InlineData(ThemeMode.Light, true, false)]
    [InlineData(ThemeMode.Light, false, false)]
    public void IsDark(ThemeMode setting, bool windowsLight, bool expectedDark)
    {
        Assert.Equal(expectedDark, ThemeResolver.IsDark(setting, windowsLight));
    }
}
