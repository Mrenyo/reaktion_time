using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A beállítási ablak számmezői (a Szemmozgás fül aránya százalékban, a többi egész szám).
public enum SettingsNumber
{
    MeasuredTrials,
    PracticeTrials,
    ReactionTimeoutMs,
    ForeperiodMinMs,
    ForeperiodMaxMs,
    ForeperiodExponentialMeanMs,
    SceneSettleDelayMs,
    DiameterPx,
    MinEccentricityPx,
    MaxEccentricityPx,
    ValidityRadiusPx,
    MinValidGazeSampleRatioPercent
}

// Egy számmező és a beállítás, amelyhez tartozik: a tartomány (a SettingsLimits-ből, a mező
// mértékegységében), a tizedesjegyek száma, a beállításfájlbeli név (a naplóhoz), valamint a beállítás
// olvasása és írása. Az írás a tizedesjegyekre kerekített értéket kapja (lásd SettingsEditorPresenter).
// Ugyanebből a táblából állítja be a nézet a vezérlők tartományát, így a kettő nem térhet el.
public sealed record SettingsNumberField(
    SettingsNumber Id,
    string Path,
    decimal Min,
    decimal Max,
    int DecimalPlaces,
    Func<ExperimentSettings, double> Read,
    Action<ExperimentSettings, decimal> Write)
{
    public static IReadOnlyList<SettingsNumberField> All { get; } =
    [
        Integer(SettingsNumber.MeasuredTrials, "Trials.MeasuredTrials", SettingsLimits.MeasuredTrials.Min, SettingsLimits.MeasuredTrials.Max,
            s => s.Trials.MeasuredTrials, (s, v) => s.Trials.MeasuredTrials = v),
        Integer(SettingsNumber.PracticeTrials, "Trials.PracticeTrials", SettingsLimits.PracticeTrials.Min, SettingsLimits.PracticeTrials.Max,
            s => s.Trials.PracticeTrials, (s, v) => s.Trials.PracticeTrials = v),
        Integer(SettingsNumber.ReactionTimeoutMs, "Trials.ReactionTimeoutMs", SettingsLimits.ReactionTimeoutMs.Min, SettingsLimits.ReactionTimeoutMs.Max,
            s => s.Trials.ReactionTimeoutMs, (s, v) => s.Trials.ReactionTimeoutMs = v),
        Integer(SettingsNumber.ForeperiodMinMs, "Trials.ForeperiodMinMs", SettingsLimits.ForeperiodMs.Min, SettingsLimits.ForeperiodMs.Max,
            s => s.Trials.ForeperiodMinMs, (s, v) => s.Trials.ForeperiodMinMs = v),
        Integer(SettingsNumber.ForeperiodMaxMs, "Trials.ForeperiodMaxMs", SettingsLimits.ForeperiodMs.Min, SettingsLimits.ForeperiodMs.Max,
            s => s.Trials.ForeperiodMaxMs, (s, v) => s.Trials.ForeperiodMaxMs = v),
        Integer(SettingsNumber.ForeperiodExponentialMeanMs, "Trials.ForeperiodExponentialMeanMs",
            SettingsLimits.ForeperiodExponentialMeanMs.Min, SettingsLimits.ForeperiodExponentialMeanMs.Max,
            s => s.Trials.ForeperiodExponentialMeanMs, (s, v) => s.Trials.ForeperiodExponentialMeanMs = v),
        Integer(SettingsNumber.SceneSettleDelayMs, "Trials.SceneSettleDelayMs", SettingsLimits.SceneSettleDelayMs.Min, SettingsLimits.SceneSettleDelayMs.Max,
            s => s.Trials.SceneSettleDelayMs, (s, v) => s.Trials.SceneSettleDelayMs = v),
        Integer(SettingsNumber.DiameterPx, "Stimulus.DiameterPx", SettingsLimits.DiameterPx.Min, SettingsLimits.DiameterPx.Max,
            s => s.Stimulus.DiameterPx, (s, v) => s.Stimulus.DiameterPx = v),
        Integer(SettingsNumber.MinEccentricityPx, "Stimulus.MinEccentricityPx", SettingsLimits.EccentricityPx.Min, SettingsLimits.EccentricityPx.Max,
            s => s.Stimulus.MinEccentricityPx, (s, v) => s.Stimulus.MinEccentricityPx = v),
        Integer(SettingsNumber.MaxEccentricityPx, "Stimulus.MaxEccentricityPx", SettingsLimits.EccentricityPx.Min, SettingsLimits.EccentricityPx.Max,
            s => s.Stimulus.MaxEccentricityPx, (s, v) => s.Stimulus.MaxEccentricityPx = v),
        Integer(SettingsNumber.ValidityRadiusPx, "Gaze.ValidityRadiusPx", SettingsLimits.ValidityRadiusPx.Min, SettingsLimits.ValidityRadiusPx.Max,
            s => s.Gaze.ValidityRadiusPx, (s, v) => s.Gaze.ValidityRadiusPx = v),

        // Az arány százalékban, egy tizedesjegyig (a beállításfájlban 0..1 közötti szám).
        new(SettingsNumber.MinValidGazeSampleRatioPercent, "Gaze.MinValidGazeSampleRatio (%)",
            (decimal)SettingsLimits.MinValidGazeSampleRatio.Min * 100, (decimal)SettingsLimits.MinValidGazeSampleRatio.Max * 100, 1,
            s => s.Gaze.MinValidGazeSampleRatio * 100, (s, v) => s.Gaze.MinValidGazeSampleRatio = (double)(v / 100m))
    ];

    private static readonly Dictionary<SettingsNumber, SettingsNumberField> ById = All.ToDictionary(field => field.Id);

    public static SettingsNumberField For(SettingsNumber id) => ById[id];

    private static SettingsNumberField Integer(
        SettingsNumber id,
        string path,
        int min,
        int max,
        Func<ExperimentSettings, int> read,
        Action<ExperimentSettings, int> write)
    {
        return new SettingsNumberField(id, path, min, max, 0, s => read(s), (s, v) => write(s, decimal.ToInt32(v)));
    }
}
