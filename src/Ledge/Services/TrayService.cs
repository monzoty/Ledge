using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace Ledge.Services;

/// <summary>
/// The notification-area ("tray") icon (PRD F5.1, DESIGN.md §5 "Tray icon"):
/// left-click toggles the panel, right-click shows Open / Settings / Quit.
/// </summary>
/// <remarks>
/// Windows draws tray icons on Explorer's behalf and sends the owning app
/// mouse messages (Shell_NotifyIcon). When explorer.exe restarts, every tray
/// icon vanishes and Windows broadcasts "TaskbarCreated" so apps can add
/// theirs back; H.NotifyIcon handles both for us.
/// https://learn.microsoft.com/windows/win32/shell/notification-area
/// This class only raises events; App decides what they mean.
/// </remarks>
public sealed class TrayService : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/Resources/Ledge.ico");

    private TaskbarIcon? _icon;

    /// <summary>Left-click on the icon.</summary>
    public event EventHandler? ToggleRequested;

    /// <summary>Menu → Open.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Menu → Settings.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Menu → Quit.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>The user clicked a notification we showed.</summary>
    public event EventHandler? NotificationClicked;

    /// <summary>
    /// A Windows notification from the tray icon. Windows decides how it looks
    /// (on Windows 10/11 it appears like a toast) and holds it back during Do
    /// Not Disturb or a full-screen app (respectQuietTime). It can't have buttons.
    /// https://learn.microsoft.com/windows/win32/shell/notification-area#notifications-and-the-notification-area
    /// </summary>
    public void ShowNotification(string title, string message) =>
        _icon?.ShowNotification(title, message, respectQuietTime: true);

    /// <summary>Adds the icon to the tray. Call once, on the UI thread, after resources are loaded.</summary>
    public void Show()
    {
        var resources = Application.Current;

        _icon = new TaskbarIcon
        {
            ToolTipText = (string)resources.FindResource("AppName"),
            IconSource = new BitmapImage(IconUri),

            // Without this the library waits for the double-click time before
            // reporting a single click, which makes the toggle feel laggy.
            NoLeftClickDelay = true,
            ContextMenu = BuildMenu(resources),
        };

        _icon.TrayLeftMouseUp += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        _icon.TrayBalloonTipClicked += (_, _) => NotificationClicked?.Invoke(this, EventArgs.Empty);

        // The icon isn't part of any window, so tell it to create itself now.
        // false = DON'T put the app into Windows "Efficiency mode" (low priority),
        // which would make the hotkey and animations feel sluggish.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void Dispose()
    {
        // Removes the icon at once; otherwise it lingers until the mouse passes over it.
        _icon?.Dispose();
        _icon = null;
    }

    private ContextMenu BuildMenu(Application resources)
    {
        var menu = new ContextMenu { Style = (Style)resources.FindResource("TrayMenu") };
        menu.Items.Add(MenuItem(resources, "TrayOpen", () => OpenRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(MenuItem(resources, "TraySettings", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(MenuItem(resources, "TrayQuit", () => QuitRequested?.Invoke(this, EventArgs.Empty)));
        return menu;
    }

    private static MenuItem MenuItem(Application resources, string headerKey, Action onClick)
    {
        var item = new MenuItem
        {
            Header = (string)resources.FindResource(headerKey),
            Style = (Style)resources.FindResource("TrayMenuItem"),
        };
        item.Click += (_, _) => onClick();
        return item;
    }
}
