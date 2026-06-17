using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace PoeAncientsPriceHelper;

/// <summary>
/// Auto-detects the exchange panel region by scanning screen pixel patterns.
/// Uses LockBits for fast bulk pixel access (~100x faster than GetPixel).
/// The exchange panel has a parchment/tan background against the dark game world.
/// </summary>
internal static class AutoCalibrator
{
    private const int SampleStep = 2;           // finer step for accuracy (LockBits is fast)
    private const int MinPanelWidth = 120;      // minimum expected panel width
    private const int MinPanelHeight = 80;      // minimum expected panel height
    private const int MaxPanelWidth = 1000;
    private const int MaxPanelHeight = 900;
    private const int PanelPixelRatio = 20;     // % of pixels in row that must be "panel"

    // PoE2 exchange panels have parchment/tan backgrounds — warm, mid-brightness colors.
    // Game UI uses dark grays/blacks; panels are distinctly lighter with warm tones.
    private const int ColorR_Min = 60;
    private const int ColorG_Min = 50;
    private const int ColorB_Min = 40;

    /// <summary>
    /// Attempts to auto-detect the exchange panel region on any monitor.
    /// Returns null if detection fails (user should calibrate manually).
    /// </summary>
    public static Rectangle? DetectPanel()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var screen = SystemInformation.VirtualScreen;
            using var bmp = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screen.Location, Point.Empty, screen.Size);
            }

            // Lock bits for fast pixel access
            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppRgb);

            try
            {
                // Strategy 1: Color-based detection (parchment pixels)
                var candidate = FindPanelByColor(data, screen);
                if (candidate is { } r && r.Width >= MinPanelWidth && r.Height >= MinPanelHeight)
                {
                    Debug.WriteLine($"[AutoCalibrator] Color detection: {r.Width}x{r.Height} at ({r.X},{r.Y}) in {sw.ElapsedMilliseconds}ms");
                    return r;
                }

                // Strategy 2: Brightness-based fallback
                var fallback = FindPanelByBrightness(data, screen);
                if (fallback is { } r2 && r2.Width >= MinPanelWidth && r2.Height >= MinPanelHeight)
                {
                    Debug.WriteLine($"[AutoCalibrator] Brightness fallback: {r2.Width}x{r2.Height} at ({r2.X},{r2.Y}) in {sw.ElapsedMilliseconds}ms");
                    return r2;
                }

                // Strategy 3: Edge contrast detection (find sharp brightness transitions)
                var edge = FindPanelByContrast(data, screen);
                if (edge is { } r3 && r3.Width >= MinPanelWidth && r3.Height >= MinPanelHeight)
                {
                    Debug.WriteLine($"[AutoCalibrator] Contrast detection: {r3.Width}x{r3.Height} at ({r3.X},{r3.Y}) in {sw.ElapsedMilliseconds}ms");
                    return r3;
                }

                Debug.WriteLine($"[AutoCalibrator] All strategies failed in {sw.ElapsedMilliseconds}ms");
                return null;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoCalibrator] Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Find panel by looking for regions with high concentration of warm/parchment-colored pixels.
    /// </summary>
    private static Rectangle? FindPanelByColor(BitmapData data, Rectangle screen)
    {
        int w = data.Width, h = data.Height;
        int step = SampleStep;
        int stride = data.Stride;
        unsafe
        {
            var ptr = (byte*)data.Scan0;
            int[] rowCounts = new int[h];

            for (int y = 0; y < h; y += step)
            {
                int count = 0;
                byte* row = ptr + y * stride;
                for (int x = 0; x < w; x += step)
                {
                    int offset = x * 4;
                    byte b = row[offset];
                    byte g = row[offset + 1];
                    byte r = row[offset + 2];

                    // Panel pixels: warm tone, not too dark, not pure white
                    if (r > ColorR_Min && g > ColorG_Min && b > ColorB_Min
                        && (r + b) >= g  // warm bias
                        && r < 240 && g < 240 && b < 240) // not blown-out white
                    {
                        count++;
                    }
                }
                rowCounts[y] = count;
            }

            return FindContiguousRegion(rowCounts, w, h, step, stride: 1, fromData: false);
        }
    }

    /// <summary>
    /// Fallback: brightness-based detection. Panels are brighter than the dark game world.
    /// </summary>
    private static Rectangle? FindPanelByBrightness(BitmapData data, Rectangle screen)
    {
        int w = data.Width, h = data.Height;
        int step = SampleStep;
        int stride = data.Stride;
        int threshold = 50; // panels are visibly brighter than dark game background

        unsafe
        {
            var ptr = (byte*)data.Scan0;
            int[] rowBrightness = new int[h];

            for (int y = 0; y < h; y += step)
            {
                int count = 0, bright = 0;
                byte* row = ptr + y * stride;
                // Scan middle 60% of screen (avoid taskbar/game edges)
                int xStart = w / 5;
                int xEnd = w * 4 / 5;
                for (int x = xStart; x < xEnd; x += step)
                {
                    int offset = x * 4;
                    byte b = row[offset];
                    byte g = row[offset + 1];
                    byte r = row[offset + 2];
                    int avg = (r + g + b) / 3;
                    count++;
                    if (avg > threshold) bright++;
                }
                rowBrightness[y] = count > 0 && (double)bright / count > 0.20 ? bright : 0;
            }

            return FindContiguousRegion(rowBrightness, w, h, step, stride: 1, fromData: false);
        }
    }

    /// <summary>
    /// Strategy 3: Find panel by sharp brightness contrast edges.
    /// The exchange panel has a distinctive border where brightness jumps.
    /// </summary>
    private static Rectangle? FindPanelByContrast(BitmapData data, Rectangle screen)
    {
        int w = data.Width, h = data.Height;
        int step = SampleStep;
        int stride = data.Stride;

        // For each row, count how many sharp brightness transitions exist
        // Panel regions have many transitions (border, text rows, item slots)
        int[] rowTransitions = new int[h];
        int transitionThreshold = 25; // brightness jump that counts as a transition

        unsafe
        {
            var ptr = (byte*)data.Scan0;
            for (int y = 0; y < h; y += step)
            {
                int transitions = 0;
                byte* row = ptr + y * stride;
                int prevBright = 0;

                for (int x = 0; x < w; x += step)
                {
                    int offset = x * 4;
                    int avg = (row[offset] + row[offset + 1] + row[offset + 2]) / 3;
                    if (Math.Abs(avg - prevBright) > transitionThreshold)
                        transitions++;
                    prevBright = avg;
                }
                rowTransitions[y] = transitions;
            }
        }

        // Find the region with the most transitions (likely the exchange panel)
        int bestTop = -1, bestBottom = -1, bestHeight = 0;
        int curTop = -1;
        int transThreshold = w / (step * 4); // at least 25% of the row has transitions

        for (int y = 0; y < h; y += step)
        {
            if (rowTransitions[y] >= transThreshold)
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

        // Find horizontal extent
        int centerY = (bestTop + bestBottom) / 2;
        int left = FindContrastEdge(data, centerY, w, step, true);
        int right = FindContrastEdge(data, centerY, w, step, false);

        if (right - left < MinPanelWidth) return null;

        return new Rectangle(
            screen.X + left,
            screen.Y + bestTop,
            right - left,
            bestBottom - bestTop
        );
    }

    private static unsafe int FindContrastEdge(BitmapData data, int y, int w, int step, bool fromLeft)
    {
        byte* row = (byte*)data.Scan0 + y * data.Stride;
        int prevBright = (row[2] + row[1] + row[0]) / 3;

        if (fromLeft)
        {
            for (int x = step; x < w / 2; x += step)
            {
                int offset = x * 4;
                int avg = (row[offset + 2] + row[offset + 1] + row[offset]) / 3;
                if (Math.Abs(avg - prevBright) > 30)
                    return Math.Max(0, x - 4);
                prevBright = avg;
            }
            return 0;
        }
        else
        {
            for (int x = w - 1 - step; x > w / 2; x -= step)
            {
                int offset = x * 4;
                int avg = (row[offset + 2] + row[offset + 1] + row[offset]) / 3;
                if (Math.Abs(avg - prevBright) > 30)
                    return Math.Min(w, x + 4);
                prevBright = avg;
            }
            return w;
        }
    }

    /// <summary>
    /// Find the largest contiguous vertical region where rows have enough "signal".
    /// Then find horizontal extent at the vertical center.
    /// </summary>
    private static Rectangle? FindContiguousRegion(int[] rowSignal, int w, int h, int step, int stride, bool fromData)
    {
        int panelWidthSamples = w / step;
        int threshold = panelWidthSamples * PanelPixelRatio / 100;

        int bestTop = -1, bestBottom = -1, bestHeight = 0;
        int curTop = -1;

        for (int y = 0; y < h; y += step)
        {
            if (rowSignal[y] >= threshold)
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

        // For brightness/contrast strategies, estimate horizontal extent
        // by checking the same rows for brightness above threshold
        int centerY = (bestTop + bestBottom) / 2;
        int left = 0, right = w;

        // Simple: assume panel spans the detected vertical region's typical width
        // Use 30%–80% of screen as conservative estimate for exchange panel
        left = w / 5;
        right = w * 4 / 5;

        return new Rectangle(left, bestTop, right - left, bestBottom - bestTop);
    }
}
