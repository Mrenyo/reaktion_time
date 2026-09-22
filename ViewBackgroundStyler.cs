using System.Drawing.Drawing2D;
using System.IO;

namespace ReactionTimeMeasurementApp;

internal static class ViewBackgroundStyler
{
    // A háttérképet használó nézetek (menü, kalibráció, átmeneti takaró) mind ugyanakkorák, ezért
    // elég a legutóbb használt néhány méretet megtartani. Korábban minden valaha előfordult méret
    // (pl. ablakátméretezéskor minden köztes méret, 1080p-n ~8 MB-onként) örökre a memóriában maradt.
    private const int MaxCachedSizes = 2;

    private static readonly object SyncRoot = new();

    // A legutóbb használt méret áll elöl.
    private static readonly LinkedList<(Size Size, Bitmap Bitmap)> ScaledBackgroundCache = new();

    internal static Image? TryLoadBackground()
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Images", "background.png");
        return UiResourceCache.TryGetImage(assetPath);
    }

    // A visszaadott képet a hívónak azonnal fel kell használnia (pl. kirajzolnia), és nem szabad
    // eltárolnia: egy későbbi hívás kiszoríthatja a gyorsítótárból, ami felszabadítja.
    internal static Image? TryGetScaledBackground(Size targetSize)
    {
        if (targetSize.Width <= 0 || targetSize.Height <= 0)
        {
            return null;
        }

        lock (SyncRoot)
        {
            for (var node = ScaledBackgroundCache.First; node is not null; node = node.Next)
            {
                if (node.Value.Size == targetSize)
                {
                    ScaledBackgroundCache.Remove(node);
                    ScaledBackgroundCache.AddFirst(node);
                    return node.Value.Bitmap;
                }
            }

            var source = TryLoadBackground();
            if (source is null)
            {
                return null;
            }

            var scaled = new Bitmap(targetSize.Width, targetSize.Height);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.Low;
                graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.DrawImage(source, new Rectangle(Point.Empty, targetSize));
            }

            ScaledBackgroundCache.AddFirst((targetSize, scaled));
            while (ScaledBackgroundCache.Count > MaxCachedSizes)
            {
                var evicted = ScaledBackgroundCache.Last!;
                ScaledBackgroundCache.RemoveLast();
                evicted.Value.Bitmap.Dispose();
            }

            return scaled;
        }
    }
}
