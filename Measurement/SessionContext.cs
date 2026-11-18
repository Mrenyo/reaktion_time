using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A mérés elején rögzített geometria: a mérőfelület, a fixációs pont, az ingerhelyek mintavétele és
// a legnagyobb elférő távolság, a képidőzítés, valamint (ha van szemkövető) a kijelző fizikai
// mérete és a display areával kapcsolatos figyelmeztetés.
internal sealed record SessionGeometry(
    StageInfo Stage,
    PointF FixationPoint,
    StimulusPlacement Placement,
    double EffectiveMaxEccentricityPx,
    DisplayTiming Timing,
    PhysicalDisplaySize? DisplaySize,
    string? DisplayAreaWarning);

// A futó mérés változatlan pillanatképe: a mérés indításakor betöltött beállítások (saját másolat),
// a tekintetszabály, a geometria, az, hogy rögzül-e tekintetadat (GazeActive), a véletlenszám-
// generátor magja és a beállított színek. A próbák ciklusa, a kiértékelés (TrialEvaluator), a
// metaadatok (SessionMetadata) és a jelenet kizárólag ebből dolgozik.
internal sealed record SessionContext(
    ExperimentSettings Settings,
    IGazeValidationRule Rule,
    SessionGeometry Geometry,
    bool GazeActive,
    int RandomSeed,
    Color StimulusColor,
    Color BackgroundColor)
{
    public bool ClickOnStimulus => Settings.Trials.ResponseMode == ResponseMode.ClickOnStimulus;

    // A mérés neve (a fájlnévben, a statisztikában és a naplóban): a tekintetszabály neve, ingerre
    // kattintós módban ezzel jelölve.
    public string SessionName => ClickOnStimulus ? $"{Rule.SessionName} – ingerre kattintás" : Rule.SessionName;
}
