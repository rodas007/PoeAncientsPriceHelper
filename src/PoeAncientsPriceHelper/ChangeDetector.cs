using System.Drawing;
using System.Drawing.Imaging;

namespace PoeAncientsPriceHelper;

internal sealed class ChangeDetector
{
    private readonly Dictionary<string, ulong> _lastHashes = new();

    public bool HasChanged(string key, Bitmap bmp)
    {
        var h = Fnv1a64(bmp);
        if (_lastHashes.TryGetValue(key, out var prev) && prev == h) return false;
        _lastHashes[key] = h;
        return true;
    }

    private static ulong Fnv1a64(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int len = data.Stride * bmp.Height;
            var buf = new byte[len];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, len);
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < buf.Length; i++) { h ^= buf[i]; h *= 1099511628211UL; }
            return h;
        }
        finally { bmp.UnlockBits(data); }
    }
}
