using System.Windows;
using System.Windows.Threading;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Microsoft.Win32;

namespace Ledge.Services;

/// <summary>
/// Puts the right colours on screen (PRD F6, DESIGN.md §3): the Theme setting,
/// or, for "System", Windows' own light/dark app mode, followed live.
/// </summary>
/// <remarks>
/// How the switch works: App.xaml merges ONE colour dictionary
/// (Colors.Dark.xaml or Colors.Light.xaml). This class replaces it with the
/// other. Every element that uses a colour with {DynamicResource …} keeps a
/// live link to the resource key, so it repaints by itself; {StaticResource …}
/// would have been looked up once and kept the old brush.
/// https://learn.microsoft.com/dotnet/desktop/wpf/systems/xaml-resources-overview#static-and-dynamic-resources
/// </remarks>
public sealed class ThemeService : IDisposable
{
    // "pack://application:,,,/…" = a XAML file compiled into Ledge.exe.
    // https://learn.microsoft.com/dotnet/desktop/wpf/app-development/pack-uris-in-wpf
    private const string DarkColorsPath = "Themes/Colors.Dark.xaml";
    private const string LightColorsPath = "Themes/Colors.Light.xaml";
    private static readonly Uri DarkColors = new("pack://application:,,,/" + DarkColorsPath);
    private static readonly Uri LightColors = new("pack://application:,,,/" + LightColorsPath);

    /// <summary>Windows' app mode lives here: AppsUseLightTheme = 1 (light) or 0 (dark).</summary>
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private readonly SettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private bool? _appliedDark;

    public ThemeService(SettingsService settings)
    {
        _settings = settings;

        // Created on the UI thread (at start-up), so this is the UI dispatcher.
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>The colours were swapped. Raised on the UI thread.</summary>
    public event EventHandler? ThemeChanged;

    public bool IsDark => _appliedDark ?? true;

    /// <summary>Applies the current theme and starts following changes. Call once, before showing windows.</summary>
    public void Start()
    {
        Apply();

        _settings.Changed += OnSettingsChanged;

        // Raised when Windows settings change, including light/dark mode
        // (category General). Static event: must be removed in Dispose.
        // https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.userpreferencechanged
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => _dispatcher.BeginInvoke(Apply);

    /// <summary>SystemEvents arrive on their own thread; hop to the UI thread to touch resources.</summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            _dispatcher.BeginInvoke(Apply);
        }
    }

    private void Apply()
    {
        var dark = ThemeResolver.IsDark(_settings.Current.Theme, WindowsAppsUseLightTheme());
        if (dark == _appliedDark)
        {
            return;
        }

        SwapColorDictionary(dark ? DarkColors : LightColors);
        _appliedDark = dark;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void SwapColorDictionary(Uri source)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        // App.xaml writes the path relatively ("Themes/Colors.Dark.xaml"), so
        // match on the end of the path, whichever form it was written in.
        var current = dictionaries.FirstOrDefault(d =>
            d.Source is { } uri && (uri.OriginalString.EndsWith(DarkColorsPath, StringComparison.OrdinalIgnoreCase)
                                 || uri.OriginalString.EndsWith(LightColorsPath, StringComparison.OrdinalIgnoreCase)));

        var replacement = new ResourceDictionary { Source = source };
        if (current is null)
        {
            dictionaries.Add(replacement);
            return;
        }

        // Replacing in place keeps the same position in the merge order.
        dictionaries[dictionaries.IndexOf(current)] = replacement;
    }

    /// <summary>
    /// Windows' "Choose your default app mode". Missing value (very old
    /// Windows 10) means light, which was the only mode back then.
    /// </summary>
    private static bool WindowsAppsUseLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue(AppsUseLightThemeValue) is not int value || value != 0;
    }
}
