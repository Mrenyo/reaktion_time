using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// Egy mérési mód futásidejű profilja: a mód saját (szöveges, megjelenítési) jellemzői és a
// beállításfájlból betöltött kísérleti paraméterei (Mode).
public sealed record MeasurementProfile(
    string SessionName,
    string IdleInstructionText,
    float FixationCrossSizePx,
    float FixationCrossThicknessPx,
    float StimulusSafeMarginPx,
    ModeSettings Mode);

// Egy mérési mód (stratégia). A mérés menete (MeasurementPresenter) minden módnál ugyanaz; egy
// új mód (pl. más tekintetszabállyal) egy új IMeasurementMode megvalósítással adható hozzá.
public interface IMeasurementMode
{
    MeasurementProfile CreateProfile(ExperimentSettings settings);

    // Mód-specifikus ellenőrzés a beállításokra; null, ha rendben vannak, különben a hibaüzenet.
    string? ValidateProfile(MeasurementProfile profile);

    // Tekintet alapú érvényesség. A validGazePointsOnStage az inger megjelenése és a válasz közötti
    // ÉRVÉNYES tekintetminták, időrendben, a mérőfelület koordinátáiban (az adatminőséget a
    // presenter már ellenőrizte); a visualSpace-szel a képpontok látószögre válthatók.
    bool IsGazeValid(IReadOnlyList<PointF> validGazePointsOnStage, RectangleF stimulusBounds, StageVisualSpace visualSpace, ModeSettings settings);
}

// "Normál reakcióidő": a résztvevő ránéz az ingerre, és úgy reagál. A próba akkor érvényes, ha a
// tekintet az inger megjelenése és a válasz között legalább egyszer az inger helyére (középpontjától
// GazeValidityRadiusDeg fokon belülre) esik.
public sealed class NormalMeasurementMode : IMeasurementMode
{
    public MeasurementProfile CreateProfile(ExperimentSettings settings) => new(
        SessionName: "Normál mérés",
        IdleInstructionText: "Nézz a középső keresztre. Amint megjelenik a fehér pont, nézz rá, és a lehető leggyorsabban kattints. "
            + "Utána nézz vissza a keresztre.",
        FixationCrossSizePx: 18f,
        FixationCrossThicknessPx: 3f,
        StimulusSafeMarginPx: 36f,
        Mode: settings.NormalMode);

    public string? ValidateProfile(MeasurementProfile profile) => null;

    public bool IsGazeValid(IReadOnlyList<PointF> validGazePointsOnStage, RectangleF stimulusBounds, StageVisualSpace visualSpace, ModeSettings settings)
    {
        var stimulusCenter = new PointF(
            stimulusBounds.X + (stimulusBounds.Width / 2f),
            stimulusBounds.Y + (stimulusBounds.Height / 2f));
        return validGazePointsOnStage.Any(point => visualSpace.AngleBetweenDeg(point, stimulusCenter) < settings.GazeValidityRadiusDeg);
    }
}

// "Periférikus reakcióidő": a résztvevő a középső keresztet nézi, és úgy reagál a periférián
// megjelenő ingerre, hogy nem néz oda. A próba akkor érvényes, ha a tekintet az inger megjelenésétől
// a válaszig VÉGIG a kereszt körüli GazeValidityRadiusDeg fokos ablakban marad (akkor is érvénytelen,
// ha a résztvevő az ingerre nézett, de a válasz pillanatára már visszanézett).
public sealed class PeripheralMeasurementMode : IMeasurementMode
{
    public MeasurementProfile CreateProfile(ExperimentSettings settings) => new(
        SessionName: "Periférikus mérés",
        IdleInstructionText: "Nézz végig a középső keresztre. Amint bárhol megjelenik a fehér pont, a lehető leggyorsabban kattints – "
            + "de ne nézz oda a pontra, a tekinteted maradjon a kereszten.",
        FixationCrossSizePx: 18f,
        FixationCrossThicknessPx: 3f,
        StimulusSafeMarginPx: 36f,
        Mode: settings.PeripheralMode);

    // Ha az inger a fixációs ablakon belül (vagy annak szélén) is megjelenhetne, az ingerre nézés
    // sem hagyná el az ablakot, így a tekintet-ellenőrzés értelmét vesztené.
    public string? ValidateProfile(MeasurementProfile profile)
    {
        var mode = profile.Mode;
        var minimumEccentricity = mode.GazeValidityRadiusDeg + (mode.StimulusDiameterDeg / 2);
        return mode.MinEccentricityDeg > minimumEccentricity
            ? null
            : $"PeripheralMode: a MinEccentricityDeg ({mode.MinEccentricityDeg:0.#}°) legyen nagyobb, mint "
                + $"GazeValidityRadiusDeg + StimulusDiameterDeg / 2 ({minimumEccentricity:0.#}°), különben az ingerre nézés "
                + "is a fixációs ablakon belül maradhat.";
    }

    public bool IsGazeValid(IReadOnlyList<PointF> validGazePointsOnStage, RectangleF stimulusBounds, StageVisualSpace visualSpace, ModeSettings settings)
    {
        return validGazePointsOnStage.All(point => visualSpace.AngleBetweenDeg(point, visualSpace.FixationPoint) < settings.GazeValidityRadiusDeg);
    }
}
