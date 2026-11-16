using System.ComponentModel;
using System.Windows;
using Ledge.Interop;
using Microsoft.Win32;

namespace Ledge.Services;

/// <summary>
/// Knows about the primary monitor and raises <see cref="LayoutChanged"/> when
/// anything that affects the pill's position changes: resolution, monitors,
/// or the taskbar (work area). The event is always raised on the UI thread.
/// </summary>
public sealed class ScreenService : IDisposable
{
    public ScreenService()
    {
        // Resolution, orientation, monitor added/removed.
        // https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.displaysettingschanged
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // WPF raises this when a SystemParameters value changes; "WorkArea"
        // covers the taskbar being moved, resized or set to auto-hide.
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    /// <summary>Raised on the UI thread when the pill may need to move.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>The primary monitor's work area in physical pixels.</summary>
    public static Int32Rect GetPrimaryWorkArea() => WindowPlacement.GetPrimaryWorkArea();

    public void Dispose()
    {
        // SystemEvents are static: without unsubscribing, they would keep this object alive forever.
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => RaiseOnUiThread();

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea))
        {
            RaiseOnUiThread();
        }
    }

    private void RaiseOnUiThread()
    {
        // SystemEvents may fire on a background thread, and WPF objects may only be
        // touched on the UI thread, so hop over to it before notifying anyone.
        Application.Current?.Dispatcher.BeginInvoke(() => LayoutChanged?.Invoke(this, EventArgs.Empty));
    }
}
