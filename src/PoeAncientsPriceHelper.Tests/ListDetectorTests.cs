using System.Drawing;
using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class ListDetectorTests
{
    private static Bitmap SolidBitmap(int w, int h, Color c)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(c);
        return bmp;
    }

    [Fact]
    public void IsOpen_True_WhenStripIsBright()
    {
        var detector = new ListDetector();
        using var bmp = SolidBitmap(100, 100, Color.FromArgb(187, 179, 162)); // #BBB3A2 — alloy panel
        Assert.True(detector.IsOpen(bmp));
    }

    [Fact]
    public void IsOpen_True_WhenStripIsMediumBright()
    {
        var detector = new ListDetector();
        using var bmp = SolidBitmap(100, 100, Color.FromArgb(116, 103, 84)); // #746754 — rune panel, brightness 101
        Assert.True(detector.IsOpen(bmp));
    }

    [Fact]
    public void IsOpen_False_WhenStripIsDark()
    {
        var detector = new ListDetector();
        using var bmp = SolidBitmap(100, 100, Color.FromArgb(6, 6, 6)); // game world terrain
        Assert.False(detector.IsOpen(bmp));
    }

    [Fact]
    public void IsOpen_False_WhenBrightnessJustBelowThreshold()
    {
        var detector = new ListDetector();
        using var bmp = SolidBitmap(100, 100, Color.FromArgb(87, 87, 87)); // brightness 87, below 88
        Assert.False(detector.IsOpen(bmp));
    }

    [Fact]
    public void IsOpen_False_WhenStripIsBlack()
    {
        var detector = new ListDetector();
        using var bmp = SolidBitmap(100, 100, Color.Black);
        Assert.False(detector.IsOpen(bmp));
    }

    [Fact]
    public void IsOpen_ReturnsSampledAvg()
    {
        var detector = new ListDetector();
        using var bmp = SolidBitmap(100, 100, Color.FromArgb(120, 120, 120));
        Assert.True(detector.IsOpen(bmp, out var avg));
        Assert.Equal(120, avg.R);
        Assert.Equal(120, avg.G);
        Assert.Equal(120, avg.B);
    }
}
