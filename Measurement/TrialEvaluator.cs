using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A résztvevő válasza egy próbában a kiértékeléshez: volt-e kattintás az időkorláton belül, hová
// esett, mennyi a reakcióidő (az ESP által mért, ennek hiányában a PC-n mért; null időtúllépésnél), és
// hány kattintás érkezett még az inger megjelenése előtt.
internal readonly record struct TrialResponse(bool TimedOut, PointF? ClickPoint, double? ReactionTimeMs, int PrematureClicks);

// Egy próba érvényességének eldöntése. Tiszta függvény: csak a paramétereiből dolgozik, így a mérés
// menetétől (MeasurementPresenter) függetlenül is ellenőrizhető.
internal static class TrialEvaluator
{
    // Az érvénytelenség oka, ebben a sorrendben:
    //   1. időtúllépés (nincs kattintás);
    //   2. elsietett válasz: kattintás már az előperiódus alatt, vagy a reakcióidő rövidebb a
    //      ReactionTimeLimits.AnticipationThresholdMs értéknél;
    //   3. ingerre kattintós módban a mellékattintás - ez a válasz saját hibája, ezért a
    //      tekintet-ellenőrzés előtt dől el, és független tőle;
    //   4. a tekintet-ellenőrzés: nem érkezett tekintetminta (a szemkövető kiesett), kevés az érvényes
    //      minta, végül a tekintetszabály megsértése.
    public static TrialInvalidReason DetermineInvalidReason(
        SessionContext context,
        TrialResponse response,
        TrialGazeRecording? gazeRecording,
        RectangleF stimulusBounds)
    {
        if (response.TimedOut)
        {
            return TrialInvalidReason.Timeout;
        }

        if (response.PrematureClicks > 0 || response.ReactionTimeMs < ReactionTimeLimits.AnticipationThresholdMs)
        {
            return TrialInvalidReason.Anticipation;
        }

        var stimulusCenter = StimulusPlacement.GetCenter(stimulusBounds);
        if (context.ClickOnStimulus && !IsClickOnStimulus(response.ClickPoint, stimulusCenter, stimulusBounds.Width))
        {
            return TrialInvalidReason.MissedStimulus;
        }

        var rule = context.Rule;
        if (rule.Mode == GazeValidationMode.None)
        {
            return TrialInvalidReason.None;
        }

        // Egyetlen minta sem érkezett a próba teljes ideje alatt: a szemkövető nem küld adatot.
        if (gazeRecording is null || gazeRecording.Samples.Count == 0)
        {
            return TrialInvalidReason.TrackerUnavailable;
        }

        var gaze = context.Settings.Gaze;
        if (gazeRecording.ResponseWindowSampleCount == 0
            || gazeRecording.ValidResponseWindowRatio < gaze.MinValidGazeSampleRatio)
        {
            return TrialInvalidReason.InsufficientGazeData;
        }

        return rule.IsGazeValid(
                gazeRecording.ValidResponseWindowPoints,
                stimulusCenter,
                context.Settings.Stimulus.DiameterPx,
                context.Geometry.FixationPoint,
                gaze.ValidityRadiusPx)
            ? TrialInvalidReason.None
            : TrialInvalidReason.GazeViolation;
    }

    // Találat, ha a kattintás a kör alakú ingeren belülre (vagy a szélére) esik.
    private static bool IsClickOnStimulus(PointF? clickPoint, PointF stimulusCenter, float stimulusDiameterPx)
    {
        return clickPoint is { } point && StimulusPlacement.Distance(point, stimulusCenter) <= stimulusDiameterPx / 2.0;
    }
}
