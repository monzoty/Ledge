using System.Windows.Input;
using Ledge.Services;

namespace Ledge.Tests.Services;

public sealed class HotkeyTextTests
{
    [Theory]
    [InlineData(Key.L, ModifierKeys.Windows | ModifierKeys.Alt)]
    [InlineData(Key.Space, ModifierKeys.Windows | ModifierKeys.Alt)]               // DESIGN fallback 1
    [InlineData(Key.L, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)] // DESIGN fallback 2
    [InlineData(Key.K, ModifierKeys.Windows)]
    [InlineData(Key.F5, ModifierKeys.Control | ModifierKeys.Alt)]
    public void Allowed_WinOrCtrlAlt(Key key, ModifierKeys modifiers)
    {
        Assert.True(HotkeyText.IsAllowed(modifiers, key));
    }

    [Theory]
    [InlineData(Key.L, ModifierKeys.Control)]                       // apps use Ctrl+letter
    [InlineData(Key.N, ModifierKeys.Control | ModifierKeys.Shift)]  // and Ctrl+Shift+letter
    [InlineData(Key.F, ModifierKeys.Alt)]                           // Alt+letter = menus
    [InlineData(Key.F, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData(Key.A, ModifierKeys.Shift)]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.LeftAlt, ModifierKeys.Windows | ModifierKeys.Alt)] // only modifiers, no key yet
    [InlineData(Key.None, ModifierKeys.Windows)]
    public void NotAllowed(Key key, ModifierKeys modifiers)
    {
        Assert.False(HotkeyText.IsAllowed(modifiers, key));
    }

    [Theory]
    [InlineData("Win+Alt+L", "Win + Alt + L")]
    [InlineData("Ctrl+Alt+Shift+L", "Ctrl + Alt + Shift + L")]
    [InlineData("Alt+Win+Space", "Win + Alt + Space")] // order is normalised
    [InlineData("win+alt+l", "Win + Alt + L")]          // case doesn't matter
    public void Parse_ThenShow(string text, string shown)
    {
        var gesture = HotkeyText.Parse(text);

        Assert.NotNull(gesture);
        Assert.Equal(shown, HotkeyText.ToShown(gesture));
    }

    [Theory]
    [InlineData("Win+Alt+L")]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Win+Shift+F5")]
    [InlineData("Win+Alt+1")]
    public void Stored_RoundTrips(string text)
    {
        var gesture = HotkeyText.Parse(text)!;

        var reread = HotkeyText.Parse(HotkeyText.ToStored(gesture))!;

        Assert.Equal(gesture.Key, reread.Key);
        Assert.Equal(gesture.Modifiers, reread.Modifiers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Win+Alt+Banana")]
    [InlineData("Hyper+L")]
    [InlineData("L")] // a letter with no modifier isn't a valid gesture
    public void Parse_Invalid_ReturnsNull(string? text)
    {
        Assert.Null(HotkeyText.Parse(text));
    }
}
