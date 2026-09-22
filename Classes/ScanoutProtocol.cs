using System.Globalization;

namespace ReactionTimeMeasurementApp.Classes;

// Egy próba scanout-korrekciójának bemenetei. A kijelző a képet soronként, fentről lefelé,
// állandó sorfrekvenciával rajzolja ki; egy sor frameTime / VerticalTotalLines ideig tart, ahol a
// teljes sorszámba a képkockák közti holtidő (vertical blanking) sorai is beletartoznak.
//
// Ha a VerticalActiveLines / VerticalTotalLines 0 (a pontos képidőzítés nem ismert), a számítás a
// korábbi, holtidő nélküli közelítésre esik vissza: sor = képpontsor, teljes sorszám = DisplayHeight.
internal readonly record struct ScanoutTrialParameters(
    double StimulusY,
    double PhotodiodeY,
    int DisplayHeight,
    double RefreshRateHz,
    int VerticalActiveLines = 0,
    int VerticalTotalLines = 0)
{
    internal void Validate()
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

    internal string CreateCorrectionMessage()
    {
        return CalculateScanoutOffsetMicroseconds().ToString(
            "0.###",
            CultureInfo.InvariantCulture);
    }

    internal double CalculateScanoutOffsetMicroseconds()
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

internal static class ScanoutProtocol
{
    internal static bool TryParseReactionTime(
        string message,
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
