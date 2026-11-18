using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Ledge.ViewModels;

namespace Ledge.Windows;

/// <summary>
/// The pill ⇄ panel morph (DESIGN.md §4). Purely visual, so it lives in
/// code-behind; it's in its own file to keep the focus logic readable.
/// </summary>
/// <remarks>
/// WPF animation basics:
/// <list type="bullet">
/// <item>An animation (DoubleAnimation, ColorAnimation) changes ONE property over time.
/// https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/animation-overview</item>
/// <item>BeginTime delays it, which gives us "width first, then height".</item>
/// <item>Easing (CubicEase) makes motion start fast and settle, or the reverse.
/// https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/easing-functions</item>
/// <item>We never set From: each animation starts at the property's CURRENT value,
/// so reversing mid-way (close while opening) continues smoothly without a jump.</item>
/// </list>
/// </remarks>
public sealed partial class PillWindow
{
    private static readonly IEasingFunction ExpandEasing = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction CollapseEasing = new CubicEase { EasingMode = EasingMode.EaseIn };
    private static readonly IEasingFunction ResizeEasing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// True while the open/close morph runs. The content's size changes during
    /// it (it's being laid out), and those changes must not start a separate
    /// resize that would fight the morph.
    /// </summary>
    private bool _isMorphing;

    // Our own brush instance for the shell: brushes from resources are frozen
    // (shared, read-only) and can't be animated.
    private readonly SolidColorBrush _shellBrush = new();

    /// <summary>Height differences smaller than this (in DIPs) aren't worth animating.</summary>
    private const double HeightTolerance = 0.5;

    private void InitializeAnimation()
    {
        _shellBrush.Color = BrushColor("PillBackgroundBrush");
        Shell.Background = _shellBrush;
    }

    /// <summary>
    /// The theme changed (ThemeService). Everything in XAML follows by itself
    /// ({DynamicResource}), but the shell's colour is our own animated brush:
    /// stop any running colour animation and set the right colour directly.
    /// </summary>
    private void ApplyThemeToShell()
    {
        _shellBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _shellBrush.Color = BrushColor(IsExpanded ? "BackgroundBrush" : "PillBackgroundBrush");
    }

    private void AnimateExpand()
    {
        var total = Duration("ExpandDuration");
        var fadeDelay = Duration("ExpandContentFadeDelay");
        var half = total / 2;

        // Make the panel content measurable and switch to the panel's corner
        // radius (at pill height both radii look identical, so no visible jump).
        PanelContent.Visibility = Visibility.Visible;
        Shell.CornerRadius = (CornerRadius)FindResource("PanelCornerRadius");
        _isMorphing = true;

        Animate(Shell, WidthProperty, Double("PanelWidth"), TimeSpan.Zero, half, ExpandEasing);
        var heightDone = Animate(Shell, HeightProperty, PanelHeight(), half, half, ExpandEasing);
        AnimateColor(BrushColor("BackgroundBrush"), total, ExpandEasing);
        Animate(ShellShadow, DropShadowEffect.OpacityProperty, Double("PanelShadowOpacity"), TimeSpan.Zero, total, ExpandEasing);

        Animate(PillContent, OpacityProperty, 0, TimeSpan.Zero, fadeDelay, ExpandEasing);
        Animate(PanelContent, OpacityProperty, 1, fadeDelay, total - fadeDelay, ExpandEasing);

        heightDone.Completed += (_, _) =>
        {
            // Closed again before we got here? Then the collapse owns the shape now.
            if (!IsExpanded)
            {
                return;
            }

            _isMorphing = false;

            // The content may have changed while opening; catch up if so.
            AnimateToContentHeight();
        };
    }

    /// <summary>
    /// The list's natural height changed (task added/removed, done, empty state…).
    /// While open, ease the panel to the new height (DESIGN.md §2 "List details").
    /// </summary>
    private void OnListContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
        {
            ResizeToContentIfOpen();
        }
    }

    private void ResizeToContentIfOpen()
    {
        if (IsExpanded && !_isMorphing)
        {
            AnimateToContentHeight();
        }
    }

    /// <summary>The collapsed pill's width: 136 while showing a timer, otherwise 112 (DESIGN.md §2).</summary>
    private double CollapsedWidth() =>
        Double(_pill.Mode == PillMode.Timer ? "PillTimerWidth" : "PillWidth");

    /// <summary>
    /// The pill switched between the timer and the other labels while closed:
    /// ease its width along with the label cross-fade (DESIGN.md §4, 150 ms).
    /// While open (or opening/closing) the panel owns the width; the collapse
    /// animation picks up the right width by itself.
    /// </summary>
    private void AnimatePillWidth()
    {
        if (IsExpanded || _isMorphing)
        {
            return;
        }

        var duration = ((Duration)FindResource("PillLabelDuration")).TimeSpan;
        Animate(Shell, WidthProperty, CollapsedWidth(), TimeSpan.Zero, duration, ResizeEasing);
    }

    private void AnimateToContentHeight()
    {
        var target = PanelHeight();

        // ActualHeight is what's on screen now; skip if it's already right.
        if (Math.Abs(Shell.ActualHeight - target) < HeightTolerance)
        {
            return;
        }

        Animate(Shell, HeightProperty, target, TimeSpan.Zero, Duration("PanelResizeDuration"), ResizeEasing);
    }

    private void AnimateCollapse()
    {
        var total = Duration("CollapseDuration");
        var fade = Duration("CollapseContentFadeDuration");
        var sizeStep = (total - fade) / 2;
        _isMorphing = true;

        // Content fades out first, then height shrinks, then width.
        Animate(PanelContent, OpacityProperty, 0, TimeSpan.Zero, fade, CollapseEasing);
        Animate(Shell, HeightProperty, Double("PillHeight"), fade, sizeStep, CollapseEasing);
        var widthDone = Animate(Shell, WidthProperty, CollapsedWidth(), fade + sizeStep, sizeStep, CollapseEasing);
        AnimateColor(BrushColor("PillBackgroundBrush"), total, CollapseEasing);
        Animate(ShellShadow, DropShadowEffect.OpacityProperty, 0, TimeSpan.Zero, total, CollapseEasing);
        Animate(PillContent, OpacityProperty, 1, fade + sizeStep, sizeStep, CollapseEasing);

        widthDone.Completed += (_, _) =>
        {
            // Re-opened before we finished? Then leave everything as it is.
            if (IsExpanded)
            {
                return;
            }

            // Fully collapsed: hide the panel content so its text box can't
            // keep keyboard focus or be tabbed into while invisible.
            PanelContent.Visibility = Visibility.Collapsed;
            Shell.CornerRadius = (CornerRadius)FindResource("PillCornerRadius");
        };
    }

    /// <summary>
    /// The panel's height for its current content: measured at full panel
    /// width, then clamped to the min/max from DESIGN.md §2.
    /// </summary>
    private double PanelHeight()
    {
        PanelContent.Measure(new Size(Double("PanelWidth"), double.PositiveInfinity));
        return Math.Clamp(PanelContent.DesiredSize.Height, Double("PanelMinHeight"), Double("PanelMaxHeight"));
    }

    /// <summary>
    /// Starts one animation and returns it (so callers can listen for Completed).
    /// IAnimatable covers both UI elements and effects like the shadow.
    /// With Windows' "Show animations" turned off, the change is instant.
    /// </summary>
    private static AnimationTimeline Animate(
        IAnimatable target,
        DependencyProperty property,
        double to,
        TimeSpan beginTime,
        TimeSpan duration,
        IEasingFunction easing)
    {
        var animation = new DoubleAnimation(to, new Duration(Scale(duration)))
        {
            BeginTime = Scale(beginTime),
            EasingFunction = easing,
        };

        target.BeginAnimation(property, animation);
        return animation;
    }

    private void AnimateColor(Color to, TimeSpan duration, IEasingFunction easing)
    {
        var animation = new ColorAnimation(to, new Duration(Scale(duration))) { EasingFunction = easing };
        _shellBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    /// <summary>
    /// Respects Windows Settings → Accessibility → Visual effects → Animation effects.
    /// https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.clientareaanimation
    /// </summary>
    private static TimeSpan Scale(TimeSpan time) =>
        SystemParameters.ClientAreaAnimation ? time : TimeSpan.Zero;

    private TimeSpan Duration(string key) => (TimeSpan)FindResource(key);

    private double Double(string key) => (double)FindResource(key);

    private Color BrushColor(string key) => ((SolidColorBrush)FindResource(key)).Color;
}
