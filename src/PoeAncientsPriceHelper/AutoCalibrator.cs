using System.Drawing;
using System.Diagnostics;

namespace PoeAncientsPriceHelper;
/// <summary>
/// Auto-detects the exchange panel region by scanning screen brightness patterns.
/// The exchange panel has a distinctive rectangular region with a parchment-colored
/// background against the dark game world.
/// </summary>
internal static class AutoCalibrator
{
    private const int SampleStep = 4;           // finer step for better accuracy
    private const int MinPanelWidth = 150;      // minimum expected panel width
    private const int MinPanelHeight = 100;     // minimum expected panel height
    private const int MaxPanelWidth = 900;
    private const int MaxPanelHeight = 800;
    // PoE2 exchange panels have a parchment/tan background — R,G,B all above ~55
    private const int ColorMin = 50;            // minimum per-channel to count as "panel pixel"
    private const int PanelPixelRatio = 30;     // % of pixels in region that must be "panel"

    /// <summary>
    /// Attempts to auto-detect the exchange panel region on any monitor.
    /// Returns null if detection fails (user should calibrate manually).
    /// </summary>
    public static Rectangle? DetectPanel()
    {
        try
        {
            var screen = SystemInformation.VirtualScreen;
            using var bmp = new Bitmap(screen.Width, screen.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screen.Location, Point.Empty, screen.Size);
            }

            // Strategy: scan for rectangular regions where a large % of pixels
            // have the parchment-like color (R,G,B all > ColorMin, warm tone).
            var candidate = FindPanelByColor(bmp, screen);
            if (candidate is { } r && r.Width >= MinPanelWidth && r.Height >= MinPanelHeight)
                return r;

            // Fallback: brightness-based detection
            var fallback = FindPanelByBrightness(bmp, screen);
            if (fallback is { } r2 && r2.Width >= MinPanelWidth && r2.Height >= MinPanelHeight)
                return r2;

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Find panel by looking for regions with high concentration of warm/parchment-colored pixels.
    /// </summary>
    private static Rectangle? FindPanelByColor(Bitmap bmp, Rectangle screen)
    {
        int w = bmp.Width, h = bmp.Height;
        int step = SampleStep;

        // Scan vertical bands: for each row, count "panel pixels" (warm parchment color)
        int[] rowCounts = new int[h];
        for (int y = 0; y < h; y += step)
        {
            int count = 0;
            for (int x = 0; x < w; x += step)
            {
                var px = bmp.GetPixel(x, y);
                // Parchment: R > 50, G > 50, B > 40, and R+B > G (warm tone)
                if (px.R > ColorMin && px.G > ColorMin && px.B > ColorMin - 10
                    && px.R + px.B >= px.G)
                    count++;
            }
            rowCounts[y] = count;
        }

        // Find contiguous vertical regions with high panel-pixel density
        int panelWidthSamples = w / step;
        int threshold = panelWidthSamples * PanelPixelRatio / 100;

        int bestTop = -1, bestBottom = -1, bestHeight = 0;
        int curTop = -1;

        for (int y = 0; y < h; y += step)
        {
            if (rowCounts[y] >= threshold)
            {
                if (curTop < 0) curTop = y;
            }
            else
            {
                if (curTop >= 0)
                {
                    int height = y - curTop;
                    if (height > bestHeight && height >= MinPanelHeight)
                    {
                        bestTop = curTop;
                        bestBottom = y;
                        bestHeight = height;
                    }
                    curTop = -1;
                }
            }
        }
        if (curTop >= 0)
        {
            int height = h - curTop;
            if (height > bestHeight && height >= MinPanelHeight)
            {
                bestTop = curTop;
                bestBottom = h;
            }
        }

        if (bestTop < 0) return null;

        // Find horizontal extent at vertical center
        int centerY = (bestTop + bestBottom) / 2;
        int left = FindColorEdge(bmp, centerY, w, step, true);
        int right = FindColorEdge(bmp, centerY, w, step, false);

        if (right - left < MinPanelWidth) return null;

        return new Rectangle(
            screen.X + left,
            screen.Y + bestTop,
            right - left,
            bestBottom - bestTop
        );
    }

    private static int FindColorEdge(Bitmap bmp, int y, int w, int step, bool fromLeft)
    {
        if (fromLeft)
        {
            for (int x = 0; x < w / 2; x += step)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R > ColorMin && px.G > ColorMin && px.B > ColorMin - 10)
                    return Math.Max(0, x - 2);
            }
            return 0;
        }
        else
        {
            for (int x = w - 1; x > w / 2; x -= step)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R > ColorMin && px.G > ColorMin && px.B > ColorMin - 10)
                    return Math.Min(w, x + 2);
            }
            return w;
        }
    }

    /// <summary>
    /// Fallback: brightness-based detection with lower threshold.
    /// </summary>
    private static Rectangle? FindPanelByBrightness(Bitmap bmp, Rectangle screen)
    {
        int w = bmp.Width, h = bmp.Height;
        int step = SampleStep;
        int threshold = 55; // lower than before (was 90)

        int[] rowBrightness = new int[h];
        for (int y = 0; y < h; y += step)
        {
            int count = 0, bright = 0;
            for (int x = w / 5; x < w * 4 / 5; x += step)
            {
                var px = bmp.GetPixel(x, y);
                int b = (px.R + px.G + px.B) / 3;
                count++;
                if (b > threshold) bright++;
            }
            rowBrightness[y] = count > 0 && (double)bright / count > 0.25 ? bright : 0;
        }

        int bestTop = -1, bestBottom = -1, bestHeight = 0;
        int curTop = -1;

        for (int y = 0; y < h; y += step)
        {
            if (rowBrightness[y] > 0)
            {
                if (curTop < 0) curTop = y;
            }
            else
            {
                if (curTop >= 0)
                {
                    int height = y - curTop;
                    if (height > bestHeight && height >= MinPanelHeight)
                    {
                        bestTop = curTop;
                        bestBottom = y;
                        bestHeight = height;
                    }
                    curTop = -1;
                }
            }
        }
        if (curTop >= 0)
        {
            int height = h - curTop;
            if (height > bestHeight && height >= MinPanelHeight)
            {
                bestTop = curTop;
                bestBottom = h;
            }
        }

        if (bestTop < 0) return null;

        int centerY = (bestTop + bestBottom) / 2;
        int left = FindBrightEdge(bmp, centerY, w, step, threshold, true);
        int right = FindBrightEdge(bmp, centerY, w, step, threshold, false);

        if (right - left < MinPanelWidth) return null;

        return new Rectangle(
            screen.X + left,
            screen.Y + bestTop,
            right - left,
            bestBottom - bestTop
        );
    }

    private static int FindBrightEdge(Bitmap bmp, int y, int w, int step, int threshold, bool fromLeft)
    {
        if (fromLeft)
        {
            for (int x = 0; x < w / 2; x += step)
            {
                var px = bmp.GetPixel(x, y);
                if ((px.R + px.G + px.B) / 3 > threshold)
                    return Math.Max(0, x - 3);
            }
            return 0;
        }
        else
        {
            for (int x = w - 1; x > w / 2; x -= step)
            {
                var px = bmp.GetPixel(x, y);
                if ((px.R + px.G + px.B) / 3 > threshold)
                    return Math.Min(w, x + 3);
            }
            return w;
        }
    }
}
