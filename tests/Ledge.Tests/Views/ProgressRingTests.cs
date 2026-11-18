using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ledge.Views;

namespace Ledge.Tests.Views;

/// <summary>
/// Renders the ring into an in-memory bitmap and checks pixel colours, so the
/// arc's direction and length are tested without opening a window.
/// </summary>
public sealed class ProgressRingTests
{
    private const int Size = 48;
    private const double Stroke = 8;

    // Points in the middle of the stroke at 12, 3, 6 and 9 o'clock.
    private static readonly (int X, int Y) Top = (Size / 2, 4);
    private static readonly (int X, int Y) Right = (Size - 5, Size / 2);
    private static readonly (int X, int Y) Bottom = (Size / 2, Size - 5);
    private static readonly (int X, int Y) Left = (4, Size / 2);

    // On the stroke's centre line (radius 20 around 24,24) at 45° angles.
    private static readonly (int X, int Y) OneThirty = (38, 10);
    private static readonly (int X, int Y) SevenThirty = (10, 38);
    private static readonly (int X, int Y) TenThirty = (10, 10);

    [Fact]
    public void Full_IsAmberAllRound()
    {
        var pixels = Render(1.0);

        Assert.All([Top, Right, Bottom, Left], p => Assert.True(IsAmber(pixels, p), $"not amber at {p}"));
    }

    [Fact]
    public void Quarter_RunsClockwiseFromTwelve()
    {
        // 12 → 3 o'clock. Checked in the MIDDLE of the arc (1:30), not at its
        // ends, where the flat-cut stroke only half-covers a pixel.
        var pixels = Render(0.25);

        Assert.True(IsAmber(pixels, OneThirty));
        Assert.False(IsAmber(pixels, Bottom));
        Assert.False(IsAmber(pixels, SevenThirty));
        Assert.False(IsAmber(pixels, Left));
    }

    [Fact]
    public void ThreeQuarters_StopsBeforeNineToTwelve()
    {
        // 12 → 9 o'clock clockwise: everything but the last quarter, so the
        // point halfway between 9 and 12 is NOT amber.
        var pixels = Render(0.75);

        Assert.True(IsAmber(pixels, Right));
        Assert.True(IsAmber(pixels, Bottom));
        Assert.True(IsAmber(pixels, SevenThirty));
        Assert.False(IsAmber(pixels, TenThirty));
    }

    [Fact]
    public void Empty_HasNoAmber_ButKeepsTheTrack()
    {
        var pixels = Render(0.0);

        Assert.All([Top, Right, Bottom, Left], p => Assert.False(IsAmber(pixels, p)));
        Assert.True(Alpha(pixels, Top) > 0, "the faint track should still be drawn");
    }

    private static bool IsAmber(byte[] pixels, (int X, int Y) p)
    {
        // Pbgra32: blue, green, red, alpha. Amber #F5A524 = strong red, medium green, little blue.
        var i = ((p.Y * Size) + p.X) * 4;
        return pixels[i + 2] > 200 && pixels[i + 1] is > 120 and < 200 && pixels[i] < 100 && pixels[i + 3] > 200;
    }

    private static byte Alpha(byte[] pixels, (int X, int Y) p) => pixels[(((p.Y * Size) + p.X) * 4) + 3];

    /// <summary>
    /// WPF objects must be created on an STA thread (the threading model WPF's
    /// UI thread uses), so the rendering runs on one made just for this.
    /// </summary>
    private static byte[] Render(double value)
    {
        byte[]? pixels = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                var ring = new ProgressRing
                {
                    Value = value,
                    StrokeThickness = Stroke,
                    TrackBrush = Brushes.White,
                    TrackOpacity = 0.3,
                    ArcBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x24)),
                };
                ring.Measure(new Size(Size, Size));
                ring.Arrange(new Rect(0, 0, Size, Size));

                var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(ring);

                pixels = new byte[Size * Size * 4];
                bitmap.CopyPixels(pixels, Size * 4, 0);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return error is null ? pixels! : throw new InvalidOperationException("Rendering failed", error);
    }
}
