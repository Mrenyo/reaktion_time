using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

internal static class DisplayScanoutContext
{
    // Az inger és a fotodióda-jelző ugyanarra a felületre (surface) rajzolódik, ezért mindkét
    // téglalap ennek a felületnek a kliens-koordinátáiban értendő. A képidőzítést a mérés elején
    // egyszer kérdezzük le (DisplayTimingReader), és minden próbánál azt használjuk.
    internal static ScanoutTrialParameters Capture(
        Control surface,
        RectangleF stimulusBounds,
        RectangleF photodiodeBounds,
        DisplayTiming timing)
    {
        if (stimulusBounds.Width <= 0 || stimulusBounds.Height <= 0)
        {
            throw new InvalidOperationException("Az inger pozíciója még nincs előkészítve.");
        }

        if (photodiodeBounds.Width <= 0 || photodiodeBounds.Height <= 0)
        {
            throw new InvalidOperationException("A fotodióda-jelző nem fér el a mérőfelületen.");
        }

        var surfaceOrigin = surface.PointToScreen(Point.Empty);
        var stimulusCenter = new Point(
            (int)Math.Round(surfaceOrigin.X + stimulusBounds.Left + (stimulusBounds.Width / 2d)),
            (int)Math.Round(surfaceOrigin.Y + stimulusBounds.Top + (stimulusBounds.Height / 2d)));

        var photodiodeCenter = new Point(
            (int)Math.Round(surfaceOrigin.X + photodiodeBounds.Left + (photodiodeBounds.Width / 2d)),
            (int)Math.Round(surfaceOrigin.Y + photodiodeBounds.Top + (photodiodeBounds.Height / 2d)));

        var stimulusScreen = Screen.FromPoint(stimulusCenter);
        var photodiodeScreen = Screen.FromPoint(photodiodeCenter);
        if (!string.Equals(stimulusScreen.DeviceName, photodiodeScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Az ingernek és a fotodiódának ugyanazon a kijelzőn kell lennie.");
        }

        var parameters = new ScanoutTrialParameters(
            stimulusCenter.Y - stimulusScreen.Bounds.Top,
            photodiodeCenter.Y - stimulusScreen.Bounds.Top,
            stimulusScreen.Bounds.Height,
            timing.RefreshRateHz,
            timing.VerticalActiveLines,
            timing.VerticalTotalLines);

        parameters.Validate();
        return parameters;
    }
}
