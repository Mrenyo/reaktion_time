using System.Globalization;

namespace ReactionTimeMeasurementApp.Domain;

// A beállítások és egy adott méretű mérőfelület összevetése: megengedett-e a fixációs pont helye, és
// hol fér el az inger. Ugyanez az egy kiértékelés szolgálja ki a mérés indítását, a beállítási ablak
// ellenőrzését és előnézetét, valamint a fixációs pont kijelölőjét, így ezek nem térhetnek el.
//
// Csak érvényes beállításokra értelmes: a hívó előbb az ExperimentSettings.Validate()-et futtatja.
//   Errors         - a mérés ezen a mérőfelületen nem indítható (a hiba szakasza: ahol javítható).
//   Warnings       - a mérés indítható, de nem pontosan a beállítások szerint fut.
//   SampleStimulus - mintainger a legkisebb távolságban (az előnézethez); null, ha ott nem fér el.
public sealed record StageFeasibility(
    PointF FixationPoint,
    Rectangle PhotodiodeBounds,
    bool FixationAllowed,
    double? EffectiveMaxEccentricityPx,
    RectangleF? SampleStimulus,
    IReadOnlyList<SettingsIssue> Errors,
    IReadOnlyList<SettingsIssue> Warnings)
{
    public bool IsFeasible => Errors.Count == 0;

    // A tekintet-ellenőrzés elfogadási köre az előnézethez: LookAtStimulus módban a mintainger körül,
    // KeepFixation módban a fixációs pont körül; null, ha nincs tekintet-ellenőrzés (vagy nincs mintainger).
    public GazeAcceptanceCircle? GetAcceptanceCircle(ExperimentSettings settings) =>
        GazeAcceptance.GetCircle(settings, FixationPoint, SampleStimulus);

    // A beállítások szerinti fixációs ponttal (GazeSettings.ResolveFixationPoint).
    public static StageFeasibility Evaluate(ExperimentSettings settings, Size stageSize)
    {
        return Evaluate(settings, stageSize, settings.Gaze.ResolveFixationPoint(stageSize));
    }

    // Egy megadott fixációs ponttal (pl. a kijelölő egérmutatója alatti jelölt pont). A fotodióda-jelző
    // helye a beállításokból jön (HardwareSettings.ResolvePhotodiodeBounds).
    public static StageFeasibility Evaluate(ExperimentSettings settings, Size stageSize, PointF fixationPoint)
    {
        var errors = new List<SettingsIssue>();
        var warnings = new List<SettingsIssue>();
        var photodiode = settings.Hardware.ResolvePhotodiodeBounds(stageSize);

        var fixationAllowed = StimulusPlacement.IsFixationPointAllowed(stageSize, photodiode, fixationPoint);
        if (!fixationAllowed)
        {
            if (!StimulusPlacement.IsFixationPointAllowed(stageSize, Rectangle.Empty, fixationPoint))
            {
                errors.Add(new SettingsIssue(SettingsSection.Gaze, "A fixációs pont túl közel esik a képernyő széléhez. Jelölj ki új helyet."));
            }
            else if (settings.Gaze.EffectivePlacement == FixationPlacement.Custom)
            {
                errors.Add(new SettingsIssue(
                    SettingsSection.Gaze,
                    "A fixációs pont túl közel esik a fotodióda-jelzőhöz. Jelölj ki új helyet, vagy helyezd át a jelzőt (Hardver fül)."));
            }
            else
            {
                errors.Add(new SettingsIssue(
                    SettingsSection.Hardware,
                    "A képernyő közepén lévő fixációs kereszt túl közel esik a fotodióda-jelzőhöz. Helyezd át a jelzőt, vagy "
                    + "jelölj ki másik fixációs pontot (Szemmozgás fül)."));
            }
        }

        var stimulus = settings.Stimulus;
        var placement = new StimulusPlacement(stageSize, photodiode, fixationPoint, stimulus.DiameterPx);
        if (placement.IsCenterAreaSplit())
        {
            errors.Add(new SettingsIssue(
                SettingsSection.Hardware,
                "A fotodióda-jelző ezzel az ingermérettel kettévágja a képernyőn az inger lehetséges helyeit. Helyezd a jelzőt "
                + "közelebb a képernyő széléhez."));
        }

        var effectiveMax = placement.FindEffectiveMaxEccentricity(stimulus.MinEccentricityPx, stimulus.MaxEccentricityPx);
        if (effectiveMax is null)
        {
            errors.Add(new SettingsIssue(
                SettingsSection.Appearance,
                $"A legkisebb távolság ({Format(stimulus.MinEccentricityPx)} px) ezen a {stageSize.Width} × {stageSize.Height} képpontos "
                + "képernyőn egyik irányban sem fér el."));
        }
        else if (effectiveMax.Value < stimulus.MaxEccentricityPx)
        {
            warnings.Add(new SettingsIssue(
                SettingsSection.Appearance,
                $"A legnagyobb távolságból csak {Format(effectiveMax.Value)} px fér el ezen a képernyőn."));
        }

        var sampleStimulus = effectiveMax is null ? null : placement.FindFirstPlacement(stimulus.MinEccentricityPx);
        return new StageFeasibility(fixationPoint, photodiode, fixationAllowed, effectiveMax, sampleStimulus, errors, warnings);
    }

    private static string Format(double value) => value.ToString("0", CultureInfo.CurrentCulture);
}
