using System.IO;

namespace ReactionTimeMeasurementApp.UI;

internal static class UiResourceCache
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, Image> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    internal static Image? TryGetImage(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (ImageCache.TryGetValue(absolutePath, out var cached))
            {
                return cached;
            }

            using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var decoded = Image.FromStream(stream);
            var materialized = new Bitmap(decoded);
            ImageCache[absolutePath] = materialized;
            return materialized;
        }
    }
}
