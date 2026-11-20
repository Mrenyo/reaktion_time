namespace ReactionTimeMeasurementApp.Domain;

// Miért érvénytelen egy próba. Az Unspecified az 1-es formátumverziójú fájlokhoz tartozik, amelyekben
// csak a Valid oszlop szerepel, az ok nem.
public enum TrialInvalidReason
{
    None,
    Timeout,

    // Elsietett válasz: a résztvevő már az inger megjelenése előtt (az előperiódus alatt) kattintott,
    // vagy a reakcióidő rövidebb a ReactionTimeLimits.AnticipationThresholdMs értéknél - ilyen gyorsan
    // az ingerre nem lehet reagálni, a válasz találgatás volt.
    Anticipation,
    TrackerUnavailable,
    InsufficientGazeData,
    GazeViolation,

    // Ingerre kattintós módban (ResponseMode.ClickOnStimulus) a kattintás nem az ingerre esett.
    MissedStimulus,
    Unspecified
}

public enum SessionStatus
{
    // Minden tervezett próba lefutott.
    Completed,

    // A mérést megszakították (Esc, visszalépés, mentési hiba). Az ESP kiesése nem szakítja meg: a
    // mérés nélküle, ESP-reakcióidő nélkül fejeződik be.
    Aborted,

    // A fájlnak nincs lezáró sora: a program a mérés közben leállt (pl. összeomlás, áramszünet).
    Interrupted
}

// A reakcióidő elfogadhatóságának határai.
public static class ReactionTimeLimits
{
    // Egyszerű vizuális reakcióidő ennél rövidebb nem lehet: az ennél gyorsabb válasz az inger
    // megjelenésére való találgatás (anticipáció), nem reakció. A szakirodalomban szokásos 100 ms-os határ.
    public const double AnticipationThresholdMs = 100;
}

public sealed class MeasurementResult
{
    public required TrialPhase Phase { get; init; }

    // A próba sorszáma a fázisán (gyakorló / teszt) belül.
    public required int TrialNumber { get; init; }

    // A mérés tervezett mért (teszt) próbáinak száma (Trials.MeasuredTrials).
    public required int PlannedTestTrials { get; init; }

    // Az ESP által (fotodióda + válaszgomb) mért reakcióidő; null időtúllépésnél, vagy ha az ESP nem
    // válaszolt az RTIME kérésre.
    public double? EspReactionTimeMs { get; init; }

    // A PC-n mért idő az inger kirajzolásától az operációs rendszer által érzékelt kattintásig; null
    // időtúllépésnél.
    public double? LocalReactionTimeMs { get; init; }

    // A PC-n és az ESP által mért idő különbsége (PC - ESP), vagyis a rendszer késleltetése (a kirajzolás
    // és a megjelenés, illetve a gombnyomás és az egérüzenet feldolgozása közti idő); null, ha bármelyik
    // hiányzik.
    public double? LocalMinusEspMs => LocalReactionTimeMs - EspReactionTimeMs;

    public required DateTime RecordedAt { get; init; }

    public TrialInvalidReason InvalidReason { get; init; }

    public bool Valid => InvalidReason == TrialInvalidReason.None;

    // Az inger előtti TERVEZETT várakozási idő (előperiódus), ms.
    public int? ForeperiodMs { get; init; }

    // A ténylegesen eltelt előperiódus (a várakozás kezdetétől az inger kirajzolásáig), ms. Az
    // időzítő felbontása és a UI-szál terhelése miatt valamivel hosszabb a tervezettnél.
    public double? ActualForeperiodMs { get; init; }

    // Az előperiódus alatti (az inger megjelenése előtti) kattintások száma ebben a próbában.
    public int? PrematureClicks { get; init; }

    // Az ESP-nek elküldött scanout-korrekció (µs): az inger és a fotodióda-jelző sora közti kirajzolási idő.
    public double? ScanoutCorrectionUs { get; init; }

    // Az inger helye a mérőfelület koordinátáiban - a tekintetminták (StageX/StageY) is ebben
    // a koordináta-rendszerben kerülnek mentésre, így a tekintet alapú döntés utólag ellenőrizhető.
    public RectangleF? StimulusBounds { get; init; }

    // Az inger középpontjának távolsága a fixációs ponttól, képpontban (a mérőfelület koordinátáiban).
    public double? StimulusEccentricityPx { get; init; }

    // A válaszkattintás helye a mérőfelület koordinátáiban (mindkét válaszmódban); null időtúllépésnél.
    public PointF? ClickPoint { get; init; }

    // A válaszablak (inger megjelenése -> válasz) érvényes tekintetmintáinak aránya (0..1). Ez és a
    // további tekintetadatok null-ok, ha a mérés szemkövető nélkül futott.
    public double? ValidGazeRatio { get; init; }

    public int? GazeSamplesInWindow { get; init; }

    // A szem átlagos távolsága a kijelző síkjától a válaszablakban (mm).
    public double? MeanEyeDistanceMm { get; init; }

    // A próba teljes tekintetfelvétele. Csak a frissen mért próbáknál van kitöltve; a lemezről
    // visszaolvasott eredményeknél null (a minták a "measurements\gaze" mappában vannak).
    public TrialGazeRecording? GazeRecording { get; init; }
}

public sealed class MeasurementSessionResult
{
    public MeasurementSessionResult(
        string modeName,
        IReadOnlyList<MeasurementResult> trials,
        DateTime startedAt,
        DateTime? endedAt,
        SessionStatus status,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ModeName = modeName;
        Trials = trials;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Status = status;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    public string ModeName { get; }

    // Minden rögzített próba, a gyakorlókkal együtt, időrendben.
    public IReadOnlyList<MeasurementResult> Trials { get; }

    public DateTime StartedAt { get; }

    public DateTime? EndedAt { get; }

    public SessionStatus Status { get; }

    // A mérés körülményei (kijelző, képidőzítés, beállítások, programverzió ...), lásd
    // MeasurementSessionWriter.
    public IReadOnlyDictionary<string, string> Metadata { get; }

    // A beolvasáskor kihagyott (hibás vagy csonka) próbasorok száma; frissen mért mérésnél 0.
    public int SkippedRowCount { get; init; }

    public IEnumerable<MeasurementResult> TestTrials => Trials.Where(trial => trial.Phase == TrialPhase.Test);

    public int TestTrialCount => TestTrials.Count();

    public int ValidTrialCount => TestTrials.Count(trial => trial.Valid);

    // Az összesítésbe csak az érvényes TESZTpróbák számítanak: a gyakorló próbák és az érvénytelenek
    // (tekintethiba, elsietett válasz, időtúllépés) nem torzíthatják az eredményt.
    // Null, ha nincs egyetlen érvényes, mért értékkel rendelkező próba sem.
    public ReactionTimeSummary? EspSummary => Summarize(trial => trial.EspReactionTimeMs);

    public ReactionTimeSummary? LocalSummary => Summarize(trial => trial.LocalReactionTimeMs);

    // A PC - ESP különbség összesítése: csak azokból az érvényes tesztpróbákból, amelyekben mindkét idő megvan.
    public ReactionTimeSummary? DifferenceSummary => Summarize(trial => trial.LocalMinusEspMs);

    private ReactionTimeSummary? Summarize(Func<MeasurementResult, double?> selector)
    {
        return ReactionTimeSummary.Create(TestTrials
            .Where(trial => trial.Valid)
            .Select(selector)
            .Where(value => value.HasValue)
            .Select(value => value!.Value));
    }
}

// Egy reakcióidő-sorozat leíró statisztikái. A reakcióidők eloszlása jellemzően jobbra ferde
// (ritka, nagyon lassú válaszok), ezért az átlag mellett a medián is szerepel. A szórás a
// korrigált (n - 1 nevezőjű) tapasztalati szórás; egyetlen értékből nem számolható, ilyenkor null.
public sealed record ReactionTimeSummary(int Count, double MeanMs, double MedianMs, double? StandardDeviationMs)
{
    public static ReactionTimeSummary? Create(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        var mean = sorted.Average();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2d;
        double? standardDeviation = sorted.Length < 2
            ? null
            : Math.Sqrt(sorted.Sum(value => (value - mean) * (value - mean)) / (sorted.Length - 1));

        return new ReactionTimeSummary(sorted.Length, mean, median, standardDeviation);
    }
}

public sealed class MeasurementSessionResultEventArgs : EventArgs
{
    public MeasurementSessionResultEventArgs(MeasurementSessionResult session)
    {
        Session = session;
    }

    public MeasurementSessionResult Session { get; }
}
