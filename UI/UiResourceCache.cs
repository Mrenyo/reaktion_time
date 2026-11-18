using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.UI;

// A képfájlok memóriában tartott példányai (az alkalmazás futása alatt élnek). Csak a UI-szálról
// használjuk, ezért nincs zárolás.
internal static class UiResourceCache
{
    // A hiányzó vagy beolvashatatlan kép null-ként kerül ide, így a fájlt nem próbáljuk újra megnyitni
    // minden rajzoláskor.
    private static readonly Dictionary<string, Image?> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    // Null, ha a kép hiányzik vagy nem olvasható be (a nézetek ilyenkor kép nélkül jelennek meg).
    internal static Image? TryGetImage(string absolutePath)
    {
        if (ImageCache.TryGetValue(absolutePath, out var cached))
        {
            return cached;
        }

        Image? image = null;
        try
        {
            using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var decoded = Image.FromStream(stream);
            image = new Bitmap(decoded);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            // Az Image.FromStream a hibás képfájlt ArgumentException-nel (a GDI+ egyes hibáit
            // OutOfMemoryException-nel) jelzi.
            Logger.Warning($"A kép nem tölthető be: {absolutePath} ({ex.Message})");
        }

        ImageCache[absolutePath] = image;
        return image;
    }
}
