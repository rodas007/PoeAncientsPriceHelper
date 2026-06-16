using System.Drawing;
using System.Diagnostics;

namespace PoeAncientsPriceHelper;

/// <summary>
/// Auto-detects the exchange panel region by scanning screen brightness patterns.
/// The exchange panel has a distinctive bright rectangular region against the dark game world.
/// </summary>
internal static class AutoCalibrator
{
    // The exchange panel is typically: bright background, rows of items, ~300-600px wide, ~200-500px tall.
    // We scan the screen for a contiguous bright rectangle that matches these dimensions.

    private const int SampleStep = 8;           // pixel step for scanning (speed vs accuracy)
    private const int MinPanelWidth = 200;      // minimum expected panel width
    private const int MinPanelHeight = 150;     // minimum expected panel height
    private const int MaxPanelWidth = 800;      // maximum expected panel width
    private const int MaxPanelHeight = 700;     // maximum expected panel height
    private const int BrightnessThreshold = 90; // average brightness to consider "panel"
    private const int EdgeTolerance = 5;        // brightness drop tolerance at edges

    /// <summary>
    /// Attempts to auto-detect the exchange panel region on any monitor.
    /// Returns null if detection fails (user should calibrate manually).
    /// </summary>
    public static Rectangle? DetectPanel()
    {
        try
        {
            // Capture the full virtual screen
            var screen = SystemInformation.VirtualScreen;
            using var bmp = new Bitmap(screen.Width, screen.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screen.Location, Point.Empty, screen.Size);
            }

            // Scan for bright horizontal bands
            var bands = FindBrightBands(bmp, screen);
            if (bands.Count == 0) return null;

            // Find the tallest contiguous bright region
            var region = FindContiguousRegion(bmp, bands, screen);
            if (region is null || region.Value.Width < MinPanelWidth || region.Value.Height < MinPanelHeight)
                return null;

            // Trim edges to remove non-panel borders
            var trimmed = TrimToContent(bmp, region.Value);
            return trimmed;
        }
        catch
        {
            return null;
        }
    }

    private static List<int> FindBrightBands(Bitmap bmp, Rectangle screen)
    {
        var bands = new List<int>();
        int h = bmp.Height;
        int w = bmp.Width;

        for (int y = 0; y < h; y += SampleStep)
        {
            long sum = 0;
            int count = 0;
            // Sample middle 60% of the screen width (skip edges)
            int xStart = w / 5;
            int xEnd = w * 4 / 5;

            for (int x = xStart; x < xEnd; x += SampleStep)
            {
                var px = bmp.GetPixel(x, y);
                sum += (px.R + px.G + px.B) / 3;
                count++;
            }

            if (count > 0 && sum / count > BrightnessThreshold)
                bands.Add(y);
        }

        return bands;
    }

    private static Rectangle? FindContiguousRegion(Bitmap bmp, List<int> bands, Rectangle screen)
    {
        if (bands.Count == 0) return null;

        // Group consecutive bands into regions
        int regionStart = bands[0];
        int prevBand = bands[0];
        int bestStart = regionStart;
        int bestEnd = bands[0];
        int bestHeight = 0;

        foreach (var band in bands)
        {
            if (band - prevBand <= SampleStep * 2) // contiguous (allow small gaps)
            {
                prevBand = band;
            }
            else
            {
                // End of contiguous region
                int height = prevBand - regionStart;
                if (height > bestHeight && height >= MinPanelHeight)
                {
                    bestStart = regionStart;
                    bestEnd = prevBand;
                    bestHeight = height;
                }
                regionStart = band;
                prevBand = band;
            }
        }

        // Check final region
        int finalHeight = prevBand - regionStart;
        if (finalHeight > bestHeight && finalHeight >= MinPanelHeight)
        {
            bestStart = regionStart;
            bestEnd = prevBand;
        }

        if (bestEnd - bestStart < MinPanelHeight) return null;

        // Now find horizontal extent at the vertical center
        int centerY = (bestStart + bestEnd) / 2;
        int left = FindLeftEdge(bmp, centerY, screen);
        int right = FindRightEdge(bmp, centerY, screen);

        if (right - left < MinPanelWidth) return null;

        return new Rectangle(
            screen.X + left,
            screen.Y + bestStart,
            right - left,
            bestEnd - bestStart
        );
    }

    private static int FindLeftEdge(Bitmap bmp, int y, Rectangle screen)
    {
        int w = bmp.Width;
        for (int x = 0; x < w / 2; x += SampleStep)
        {
            var px = bmp.GetPixel(x, y);
            int brightness = (px.R + px.G + px.B) / 3;
            if (brightness > BrightnessThreshold)
                return Math.Max(0, x - EdgeTolerance);
        }
        return 0;
    }

    private static int FindRightEdge(Bitmap bmp, int y, Rectangle screen)
    {
        int w = bmp.Width;
        for (int x = w - 1; x > w / 2; x -= SampleStep)
        {
            var px = bmp.GetPixel(x, y);
            int brightness = (px.R + px.G + px.B) / 3;
            if (brightness > BrightnessThreshold)
                return Math.Min(w, x + EdgeTolerance);
        }
        return w;
    }

    private static Rectangle TrimToContent(Bitmap bmp, Rectangle region)
    {
        // Fine-tune: find actual content bounds within the detected region
        int left = region.X, right = region.X + region.Width;
        int top = region.Y, bottom = region.Y + region.Height;
        int screenW = bmp.Width, screenH = bmp.Height;

        // Trim top
        for (int y = region.Y; y < region.Y + region.Height / 2; y += 2)
        {
            if (IsBrightRow(bmp, y, region.X, region.X + region.Width))
            { top = y; break; }
        }

        // Trim bottom
        for (int y = region.Y + region.Height - 1; y > region.Y + region.Height / 2; y -= 2)
        {
            if (IsBrightRow(bmp, y, region.X, region.X + region.Width))
            { bottom = y; break; }
        }

        // Trim left
        for (int x = region.X; x < region.X + region.Width / 2; x += 2)
        {
            if (IsBrightCol(bmp, x, top, bottom))
            { left = x; break; }
        }

        // Trim right
        for (int x = region.X + region.Width - 1; x > region.X + region.Width / 2; x -= 2)
        {
            if (IsBrightCol(bmp, x, top, bottom))
            { right = x; break; }
        }

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static bool IsBrightRow(Bitmap bmp, int y, int xStart, int xEnd)
    {
        int count = 0, bright = 0;
        for (int x = xStart; x < xEnd; x += SampleStep)
        {
            var px = bmp.GetPixel(Math.Min(x, bmp.Width - 1), Math.Min(y, bmp.Height - 1));
            count++;
            if ((px.R + px.G + px.B) / 3 > BrightnessThreshold) bright++;
        }
        return count > 0 && (double)bright / count > 0.5;
    }

    private static bool IsBrightCol(Bitmap bmp, int x, int yStart, int yEnd)
    {
        int count = 0, bright = 0;
        for (int y = yStart; y < yEnd; y += SampleStep)
        {
            var px = bmp.GetPixel(Math.Min(x, bmp.Width - 1), Math.Min(y, bmp.Height - 1));
            count++;
            if ((px.R + px.G + px.B) / 3 > BrightnessThreshold) bright++;
        }
        return count > 0 && (double)bright / count > 0.5;
    }
}
