using System.Drawing.Drawing2D;
using System.IO;

namespace ReactionTimeMeasurementApp;

internal static class ViewBackgroundStyler
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<Size, Bitmap> ScaledBackgroundCache = new();

    internal static Image? TryLoadBackground()
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Images", "background.png");
        return UiResourceCache.TryGetImage(assetPath);
    }

    internal static Image? TryGetScaledBackground(Size targetSize)
    {
        if (targetSize.Width <= 0 || targetSize.Height <= 0)
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (ScaledBackgroundCache.TryGetValue(targetSize, out var cached))
            {
                return cached;
            }

            var source = TryLoadBackground();
            if (source is null)
            {
                return null;
            }

            var scaled = new Bitmap(targetSize.Width, targetSize.Height);
            using var graphics = Graphics.FromImage(scaled);
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.Low;
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.DrawImage(source, new Rectangle(Point.Empty, targetSize));

            ScaledBackgroundCache[targetSize] = scaled;
            return scaled;
        }
    }
}
