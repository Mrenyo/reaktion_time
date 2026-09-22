using System.Globalization;

namespace ReactionTimeMeasurementApp.Domain;

// A kijelző képjelének időzítése a scanout-korrekcióhoz.
//   RefreshRateHz        - pontos (tört) képfrissítési frekvencia, pl. 59,94.
//   VerticalActiveLines  - a látható sorok száma a képjelben.
//   VerticalTotalLines   - az összes sor, a képkockák közti holtidővel (vertical blanking) együtt.
//                          Egy sor kirajzolása frameTime / VerticalTotalLines ideig tart.
//   DesktopHeightPx      - az asztal magassága képpontban (eltérhet az aktív sorok számától, ha a
//                          videokártya skáláz).
// Ha a pontos időzítés nem kérdezhető le, a sorszámok 0-k (a korrekció a holtidő nélküli
// közelítéssel számol), és az IsExact hamis - ezt a mérés a metaadatokban rögzíti.
public sealed record DisplayTiming(
    double RefreshRateHz,
    int VerticalActiveLines,
    int VerticalTotalLines,
    int DesktopHeightPx,
    bool IsExact,
    string Source,
    string? Warning);

// A kijelző fizikai (látható) mérete milliméterben.
public sealed record PhysicalDisplaySize(double WidthMm, double HeightMm);

// Egy próba scanout-korrekciójának bemenetei. A kijelző a képet soronként, fentről lefelé,
// állandó sorfrekvenciával rajzolja ki; egy sor frameTime / VerticalTotalLines ideig tart, ahol a
// teljes sorszámba a képkockák közti holtidő (vertical blanking) sorai is beletartoznak.
//
// Ha a VerticalActiveLines / VerticalTotalLines 0 (a pontos képidőzítés nem ismert), a számítás a
// holtidő nélküli közelítésre esik vissza: sor = képpontsor, teljes sorszám = DisplayHeight.
public readonly record struct ScanoutTrialParameters(
    double StimulusY,
    double PhotodiodeY,
    int DisplayHeight,
    double RefreshRateHz,
    int VerticalActiveLines = 0,
    int VerticalTotalLines = 0)
{
    public void Validate()
    {
        if (!double.IsFinite(StimulusY))
        {
            throw new ArgumentException("Hiányzik vagy érvénytelen az inger Y koordinátája.", nameof(StimulusY));
        }

        if (!double.IsFinite(PhotodiodeY))
        {
            throw new ArgumentException("Hiányzik vagy érvénytelen a fotodióda Y koordinátája.", nameof(PhotodiodeY));
        }

        if (DisplayHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayHeight), "A kijelző magasságának pozitívnak kell lennie.");
        }

        if (!double.IsFinite(RefreshRateHz) || RefreshRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RefreshRateHz), "A képfrissítési frekvenciának pozitívnak kell lennie.");
        }

        if (VerticalActiveLines < 0 || VerticalTotalLines < 0
            || (VerticalActiveLines == 0) != (VerticalTotalLines == 0)
            || VerticalTotalLines < VerticalActiveLines)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VerticalTotalLines),
                "Az aktív és a teljes sorszámot együtt kell megadni, és a teljes sorszám nem lehet kisebb az aktívnál.");
        }

        if (StimulusY < 0 || StimulusY >= DisplayHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(StimulusY), "Az inger Y koordinátája a kijelzőn kívülre esik.");
        }

        if (PhotodiodeY < 0 || PhotodiodeY >= DisplayHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(PhotodiodeY), "A fotodióda Y koordinátája a kijelzőn kívülre esik.");
        }
    }

    public string CreateCorrectionMessage()
    {
        return CalculateScanoutOffsetMicroseconds().ToString(
            "0.###",
            CultureInfo.InvariantCulture);
    }

    public double CalculateScanoutOffsetMicroseconds()
    {
        Validate();
        var frameTimeMicroseconds = 1_000_000d / RefreshRateHz;
        var activeLines = VerticalActiveLines > 0 ? VerticalActiveLines : DisplayHeight;
        var totalLines = VerticalTotalLines > 0 ? VerticalTotalLines : activeLines;
        var lineTimeMicroseconds = frameTimeMicroseconds / totalLines;

        // Ha a videokártya skáláz (asztal magassága != aktív sorok), egy képpontsor ennyi jelsor.
        var linesPerRow = activeLines / (double)DisplayHeight;

        return (StimulusY - PhotodiodeY) * linesPerRow * lineTimeMicroseconds;
    }
}

public static class ScanoutGeometry
{
    // Egy próba scanout-paraméterei. Az inger és a fotodióda-jelző a mérőfelület (stage)
    // kliens-koordinátáiban értendő; a mérőfelület bal felső sarka a képernyőn stageScreenOrigin,
    // a mérést futtató kijelző területe screenBounds.
    public static ScanoutTrialParameters CreateTrialParameters(
        RectangleF stimulusBounds,
        RectangleF photodiodeBounds,
        Point stageScreenOrigin,
        Rectangle screenBounds,
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

        var stimulusCenter = ToScreenCenter(stimulusBounds, stageScreenOrigin);
        var photodiodeCenter = ToScreenCenter(photodiodeBounds, stageScreenOrigin);
        if (!screenBounds.Contains(stimulusCenter) || !screenBounds.Contains(photodiodeCenter))
        {
            throw new InvalidOperationException("Az ingernek és a fotodiódának a mérést futtató kijelzőn kell lennie.");
        }

        var parameters = new ScanoutTrialParameters(
            stimulusCenter.Y - screenBounds.Top,
            photodiodeCenter.Y - screenBounds.Top,
            screenBounds.Height,
            timing.RefreshRateHz,
            timing.VerticalActiveLines,
            timing.VerticalTotalLines);

        parameters.Validate();
        return parameters;
    }

    private static Point ToScreenCenter(RectangleF bounds, Point surfaceOrigin)
    {
        return new Point(
            (int)Math.Round(surfaceOrigin.X + bounds.Left + (bounds.Width / 2d)),
            (int)Math.Round(surfaceOrigin.Y + bounds.Top + (bounds.Height / 2d)));
    }
}

public static class ScanoutProtocol
{
    // Az ESP válasza: egyetlen nemnegatív, véges, invariáns formátumú szám mikroszekundumban.
    public static bool TryParseReactionTime(
        string? message,
        out double reactionTimeMs)
    {
        reactionTimeMs = 0;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (!double.TryParse(
                message.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var reactionTimeMicroseconds)
            || !double.IsFinite(reactionTimeMicroseconds)
            || reactionTimeMicroseconds < 0)
        {
            return false;
        }

        reactionTimeMs = reactionTimeMicroseconds / 1000d;
        return true;
    }
}

// A mérés előkészítése során felmerülő, a felhasználónak szóló (nem programhiba jellegű) probléma,
// pl. nincs beállítva a Tobii display area, vagy a beállított excentricitás nem fér el a képernyőn.
public sealed class MeasurementSetupException : Exception
{
    public MeasurementSetupException(string message)
        : base(message)
    {
    }
}
