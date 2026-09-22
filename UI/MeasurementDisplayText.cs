using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A mérési eredmények felületen megjelenő szövegei.
internal static class MeasurementDisplayText
{
    internal static string ToDisplayText(this TrialInvalidReason reason) => reason switch
    {
        TrialInvalidReason.None => "Érvényes",
        TrialInvalidReason.Timeout => "Időtúllépés",
        TrialInvalidReason.TrackerUnavailable => "Nincs szemkövető",
        TrialInvalidReason.InsufficientGazeData => "Kevés tekintetadat",
        TrialInvalidReason.GazeViolation => "Tekintethiba",
        _ => "Érvénytelen"
    };

    internal static string ToDisplayText(this SessionStatus status) => status switch
    {
        SessionStatus.Completed => "befejezett",
        SessionStatus.AttemptLimitReached => "próbálkozási korlát",
        SessionStatus.Aborted => "megszakítva",
        _ => "nem zárult le"
    };
}
