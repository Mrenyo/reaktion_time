using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ReactionTimeMeasurementApp.UI;

internal static class ViewBackgroundStyler
{
    // A háttérképet használó nézetek (menü, kalibráció, a mérési nézet előtere) a főablak tartalmának,
    // illetve a teljes képernyőnek a méretében rajzolják, ezért elég a legutóbb használt néhány méretet
    // megtartani. Így az ablak átméretezésekor sem gyűlnek a memóriában a köztes méretek képei (1080p-n
    // ~8 MB-onként).
    private const int MaxCachedSizes = 2;

    // A legutóbb használt méret áll elöl. Csak a UI-szálról használjuk (rajzoláskor), ezért nincs zárolás.
    private static readonly LinkedList<(Size Size, Bitmap Bitmap)> ScaledBackgroundCache = new();

    // A háttér kirajzolása a (0, 0) ponttól, a megadott méretben; ha a háttérkép nem érhető el, a nézetek
    // egyszínű háttere (UiTheme.PageBackground) kerül a helyére. A háttérkép teljesen átlátszatlan, ezért
    // keverés nélkül, 1:1 képpontmásolással kerül ki: ez sokszor gyorsabb a szokásos, alfa-keveréses
    // rajzolásnál, és egy újrarajzolás során többször is lefut (minden átlátszó hátterű vezérlő alatt). A
    // Graphics beállításai a rajzolás után visszaállnak.
    internal static void DrawBackground(Graphics graphics, Size targetSize)
    {
        var background = TryGetScaledBackground(targetSize);
        if (background is null)
        {
            graphics.Clear(UiTheme.PageBackground);
            return;
        }

        var state = graphics.Save();
        try
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            graphics.DrawImage(
                background,
                new Rectangle(Point.Empty, targetSize),
                0,
                0,
                targetSize.Width,
                targetSize.Height,
                GraphicsUnit.Pixel);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    // A visszaadott képet azonnal fel kell használni (kirajzolni), és nem szabad eltárolni: egy későbbi
    // hívás kiszoríthatja a gyorsítótárból, ami felszabadítja.
    private static Bitmap? TryGetScaledBackground(Size targetSize)
    {
        if (targetSize.Width <= 0 || targetSize.Height <= 0)
        {
            return null;
        }

        for (var node = ScaledBackgroundCache.First; node is not null; node = node.Next)
        {
            if (node.Value.Size == targetSize)
            {
                ScaledBackgroundCache.Remove(node);
                ScaledBackgroundCache.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        var source = UiResourceCache.TryGetImage(Path.Combine(AppContext.BaseDirectory, "Images", "background.png"));
        if (source is null)
        {
            return null;
        }

        // Előszorzott alfájú formátum: ezt a GDI+ rajzolja a leggyorsabban, és a háttér egy újrarajzolás
        // során többször is kirajzolódik (minden átlátszó hátterű vezérlő alatt).
        var scaled = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppPArgb);
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
