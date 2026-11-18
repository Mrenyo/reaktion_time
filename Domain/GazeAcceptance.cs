namespace ReactionTimeMeasurementApp.Domain;

// A tekintet-ellenőrzés elfogadási köre a mérőfelület koordinátáiban.
public readonly record struct GazeAcceptanceCircle(PointF Center, float RadiusPx);

// A tekintet-ellenőrzés elfogadási távolságai. Ugyanezt használják a tekintetszabályok
// (Measurement.GazeValidationRules) és a beállítási ablak előnézete, így a kettő nem térhet el.
public static class GazeAcceptance
{
    // LookAtStimulus: a tekintet az inger KÖZÉPPONTJÁTÓL legfeljebb ennyire lehet, vagyis az inger
    // szélén túl validityRadiusPx a megengedett tekintethiba.
    public static double GetStimulusAcceptanceDistance(float stimulusDiameterPx, double validityRadiusPx) =>
        (stimulusDiameterPx / 2.0) + validityRadiusPx;

    // Az elfogadási kör egy adott ingerhelyhez: LookAtStimulus módban az inger körül, KeepFixation
    // módban a fixációs pont körül; null, ha nincs tekintet-ellenőrzés, vagy LookAtStimulus módban nincs inger.
    public static GazeAcceptanceCircle? GetCircle(ExperimentSettings settings, PointF fixationPoint, RectangleF? stimulusBounds)
    {
        var gaze = settings.Gaze;
        switch (gaze.Validation)
        {
            case GazeValidationMode.LookAtStimulus when stimulusBounds is { } bounds:
                return new GazeAcceptanceCircle(StimulusPlacement.GetCenter(bounds), (float)GetStimulusAcceptanceDistance(bounds.Width, gaze.ValidityRadiusPx));

            case GazeValidationMode.KeepFixation:
                return new GazeAcceptanceCircle(fixationPoint, gaze.ValidityRadiusPx);

            default:
                return null;
        }
    }
}
