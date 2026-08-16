using System.Globalization;

namespace ReactionTimeMeasurementApp.Classes;

internal readonly record struct ScanoutTrialParameters(
    double StimulusY,
    double PhotodiodeY,
    int DisplayHeight,
    double RefreshRateHz)
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
        return ((StimulusY - PhotodiodeY) / DisplayHeight) * frameTimeMicroseconds;
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
