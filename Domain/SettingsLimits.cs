namespace ReactionTimeMeasurementApp.Domain;

// A mérési beállítások megengedett tartományai (a határok is megengedettek). Ugyanezekből dolgozik az
// ExperimentSettings.Validate() és a beállítási ablak vezérlőinek tartománya, így a kettő nem térhet el
// egymástól. A távolságok és méretek képpontban (a mérőfelület fizikai képpontjaiban) értendők.
public static class SettingsLimits
{
    // Trials.MeasuredTrials: a mért (eredménybe számító) ingerek száma.
    public static class MeasuredTrials
    {
        public const int Min = 1;
        public const int Max = 1000;
    }

    // Trials.PracticeTrials: a mért ingerek előtti gyakorló ingerek száma.
    public static class PracticeTrials
    {
        public const int Min = 0;
        public const int Max = 100;
    }

    // Trials.ReactionTimeoutMs: ennyi ideig vár a mérés a válaszra az inger megjelenése után.
    public static class ReactionTimeoutMs
    {
        public const int Min = 100;
        public const int Max = 60_000;
    }

    // Trials.SceneSettleDelayMs: várakozás a mérési jelenet megjelenése és az első próba között.
    public static class SceneSettleDelayMs
    {
        public const int Min = 0;
        public const int Max = 10_000;
    }

    // Trials.ForeperiodMinMs és Trials.ForeperiodMaxMs.
    public static class ForeperiodMs
    {
        public const int Min = 0;
        public const int Max = 60_000;
    }

    // Trials.ForeperiodExponentialMeanMs. Egyenletes eloszlásnál nincs hatása, de akkor is ebben a
    // tartományban kell maradnia, hogy az eloszlás átváltásakor ne keletkezzen érvénytelen beállítás.
    public static class ForeperiodExponentialMeanMs
    {
        public const int Min = 1;
        public const int Max = 60_000;
    }

    // Stimulus.DiameterPx: az inger (kör) átmérője.
    public static class DiameterPx
    {
        public const int Min = 4;
        public const int Max = 500;
    }

    // Stimulus.MinEccentricityPx és Stimulus.MaxEccentricityPx: az inger középpontjának távolsága a
    // fixációs ponttól.
    public static class EccentricityPx
    {
        public const int Min = 0;
        public const int Max = 10_000;
    }

    // Gaze.ValidityRadiusPx: a tekintet-ellenőrzés elfogadási rádiusza.
    public static class ValidityRadiusPx
    {
        public const int Min = 1;
        public const int Max = 2000;
    }

    // Gaze.MinValidGazeSampleRatio: a válaszablak érvényes tekintetmintáinak minimális aránya (0..1).
    public static class MinValidGazeSampleRatio
    {
        public const double Min = 0.01;
        public const double Max = 1.0;
    }

    // Gaze.FixationPointX/Y és Hardware.PhotodiodeMarkerX/Y: a mérőfelület szélességéhez, illetve
    // magasságához viszonyított arány.
    public static class FixationPoint
    {
        public const double Min = 0;
        public const double Max = 1;
    }

    // Hardware.EspBaudRate: a soros port átviteli sebessége.
    public static class EspBaudRate
    {
        public const int Min = 1200;
        public const int Max = 4_000_000;
    }
}
