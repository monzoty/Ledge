using System.Windows;
using System.Windows.Interop;
using Ledge.Interop;
using Ledge.Services;

namespace Ledge.Windows;

/// <summary>
/// The overlay window. Code-behind holds only visual concerns
/// (placement now; animations and focus later).
/// </summary>
public sealed partial class PillWindow : Window
{
    // Created here for now; injected through the Generic Host from M3.
    private readonly ScreenService _screen = new();

    public PillWindow()
    {
        InitializeComponent();

        _screen.LayoutChanged += (_, _) => PositionAtTopCentre();

        // Scaling changed (or the window moved to a monitor with another DPI):
        // WPF resizes the window first, so re-centre after that has happened.
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(PositionAtTopCentre);

        Closed += (_, _) => _screen.Dispose();
    }

    private nint Handle => new WindowInteropHelper(this).Handle;

    /// <summary>
    /// Runs once the native window (HWND) exists but before it is shown:
    /// the right moment to change its extended styles and place it.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.ApplyOverlayStyles(Handle);
        PositionAtTopCentre();
    }

    /// <summary>
    /// Centres the window horizontally on the primary monitor's work area and
    /// puts it at the top. Everything is in physical pixels (see WindowPlacement).
    /// </summary>
    private void PositionAtTopCentre()
    {
        var workArea = ScreenService.GetPrimaryWorkArea();
        var (width, _) = WindowPlacement.GetSize(Handle);

        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = workArea.Y;

        WindowPlacement.MoveTo(Handle, x, y);
    }
}
