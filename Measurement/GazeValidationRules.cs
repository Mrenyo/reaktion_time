using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A próba tekintet alapú érvényességi szabálya (stratégia). A mérés menete (MeasurementPresenter)
// minden szabálynál ugyanaz; egy új szabály egy új GazeValidationMode-értékkel, egy új
// IGazeValidationRule megvalósítással és a GazeValidationRules.For bővítésével adható hozzá.
public interface IGazeValidationRule
{
    GazeValidationMode Mode { get; }

    // A mérés neve: a mérési fájl ModeName mezője és a statisztika felirata.
    string SessionName { get; }

    // A résztvevőnek szóló instrukció az előtérben (a gyakorló ingerekről és az Esc-ről szóló
    // kiegészítést a presenter teszi hozzá).
    string InstructionText { get; }

    // Kell-e hozzá szemkövető (és így kalibráció is).
    bool RequiresTracker { get; }

    // Tekintet alapú érvényesség. A validGazePointsOnStage az inger megjelenése és a válasz közötti
    // ÉRVÉNYES tekintetminták, időrendben; az adatminőséget (van-e elég érvényes minta) a presenter
    // már ellenőrizte. A radiusPx a beállított elfogadási rádiusz (Gaze.ValidityRadiusPx).
    // Pontok a mérőfelület koordinátáiban.
    bool IsGazeValid(
        IReadOnlyList<PointF> validGazePointsOnStage,
        PointF stimulusCenter,
        float stimulusDiameterPx,
        PointF fixationPoint,
        double radiusPx);
}

public static class GazeValidationRules
{
    private static readonly IGazeValidationRule NoValidation = new NoGazeValidationRule();
    private static readonly IGazeValidationRule LookAtStimulus = new LookAtStimulusRule();
    private static readonly IGazeValidationRule KeepFixation = new KeepFixationRule();

    // A beállított módhoz tartozó szabály. Ismeretlen értékre (az ExperimentSettings.Validate() után
    // nem fordulhat elő) ArgumentOutOfRangeException-t dob.
    public static IGazeValidationRule For(GazeValidationMode mode) => mode switch
    {
        GazeValidationMode.None => NoValidation,
        GazeValidationMode.LookAtStimulus => LookAtStimulus,
        GazeValidationMode.KeepFixation => KeepFixation,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Ismeretlen tekintet-ellenőrzési mód.")
    };
}

// Tekintet-ellenőrzés nélkül: minden, az időkorláton belül érkező válasz érvényes. Szemkövető nem
// kell hozzá; ha mégis elérhető, a tekintetadat rögzül, de a próba érvényességébe nem számít bele.
public sealed class NoGazeValidationRule : IGazeValidationRule
{
    public GazeValidationMode Mode => GazeValidationMode.None;

    public string SessionName => "Mérés tekintet-ellenőrzés nélkül";

    public string InstructionText => "Nézz a keresztre. Amint megjelenik az inger, a lehető leggyorsabban kattints.";

    public bool RequiresTracker => false;

    public bool IsGazeValid(
        IReadOnlyList<PointF> validGazePointsOnStage,
        PointF stimulusCenter,
        float stimulusDiameterPx,
        PointF fixationPoint,
        double radiusPx) => true;
}

// "Normál reakcióidő": a résztvevő ránéz az ingerre, és úgy reagál. A próba akkor érvényes, ha a
// tekintet az inger megjelenése és a válasz között legalább egyszer az inger szélétől legfeljebb
// radiusPx távolságra esik (Distance(tekintet, ingerközéppont) <= átmérő / 2 + radiusPx). A rádiusz így
// az inger szélén túl elfogadott tekintethiba, és nem függ az inger méretétől.
public sealed class LookAtStimulusRule : IGazeValidationRule
{
    public GazeValidationMode Mode => GazeValidationMode.LookAtStimulus;

    public string SessionName => "Normál mérés";

    public string InstructionText =>
        "Nézz a keresztre. Amint megjelenik az inger, nézz rá, és a lehető leggyorsabban kattints. Utána nézz vissza a keresztre.";

    public bool RequiresTracker => true;

    public bool IsGazeValid(
        IReadOnlyList<PointF> validGazePointsOnStage,
        PointF stimulusCenter,
        float stimulusDiameterPx,
        PointF fixationPoint,
        double radiusPx)
    {
        var acceptedDistance = GazeAcceptance.GetStimulusAcceptanceDistance(stimulusDiameterPx, radiusPx);
        return validGazePointsOnStage.Any(point => StimulusPlacement.Distance(point, stimulusCenter) <= acceptedDistance);
    }
}

// "Periférikus reakcióidő": a résztvevő a fixációs keresztet nézi, és úgy reagál a máshol megjelenő
// ingerre, hogy nem néz oda. A próba akkor érvényes, ha a tekintet az inger megjelenésétől a válaszig
// VÉGIG a fixációs pont körüli radiusPx sugarú körön belül marad (akkor is érvénytelen, ha a
// résztvevő az ingerre nézett, de a válasz pillanatára már visszanézett).
public sealed class KeepFixationRule : IGazeValidationRule
{
    public GazeValidationMode Mode => GazeValidationMode.KeepFixation;

    public string SessionName => "Periférikus mérés";

    public string InstructionText =>
        "Nézz végig a keresztre. Amint bárhol megjelenik az inger, a lehető leggyorsabban kattints – de ne nézz oda, "
        + "a tekinteted maradjon a kereszten.";

    public bool RequiresTracker => true;

    public bool IsGazeValid(
        IReadOnlyList<PointF> validGazePointsOnStage,
        PointF stimulusCenter,
        float stimulusDiameterPx,
        PointF fixationPoint,
        double radiusPx)
    {
        return validGazePointsOnStage.All(point => StimulusPlacement.Distance(point, fixationPoint) <= radiusPx);
    }
}
