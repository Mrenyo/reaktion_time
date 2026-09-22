using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

// Miért érvénytelen egy próba. A korábbi, egyetlen Valid=False jelző nem különböztette meg az
// időtúllépést a tekintethibától; az Unspecified a régi (ok nélkül mentett) fájlokhoz tartozik.
public enum TrialInvalidReason
{
    None,
    Timeout,
    TrackerUnavailable,
    InsufficientGazeData,
    GazeViolation,
    Unspecified
}

public enum SessionStatus
{
    // Összegyűlt a kívánt számú érvényes tesztpróba.
    Completed,

    // Elfogyott a megengedett próbálkozás, mielőtt összegyűlt volna a kívánt számú érvényes próba.
    AttemptLimitReached,

    // A mérést megszakították (Esc, visszalépés, ESP-hiba, mentési hiba).
    Aborted,

    // A fájlnak nincs lezáró sora: a program a mérés közben leállt (pl. összeomlás, áramszünet).
    Interrupted
}

public sealed class MeasurementResult
{
    public required TrialPhase Phase { get; init; }

    // A próba sorszáma a fázisán (gyakorló / teszt) belül.
    public required int TrialNumber { get; init; }

    // A kívánt érvényes tesztpróbák száma a mérésben.
    public required int TargetValidTrials { get; init; }

    public double? EspReactionTimeMs { get; init; }

    public double? LocalReactionTimeMs { get; init; }

    public required DateTime RecordedAt { get; init; }

    public TrialInvalidReason InvalidReason { get; init; }

    public bool Valid => InvalidReason == TrialInvalidReason.None;

    // Az inger előtti várakozási idő (előperiódus), ms.
    public int? ForeperiodMs { get; init; }

    public string? ScanoutCorrectionUs { get; init; }

    // Az inger helye a mérőfelület koordinátáiban - a tekintetminták (StageX/StageY) is ebben
    // a koordináta-rendszerben kerülnek mentésre, így a tekintet alapú döntés utólag ellenőrizhető.
    public RectangleF? StimulusBounds { get; init; }

    // Az inger középpontjának távolsága a fixációs kereszttől, látószögben.
    public double? StimulusEccentricityDeg { get; init; }

    // A válaszablak (inger megjelenése -> válasz) érvényes tekintetmintáinak aránya (0..1).
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

    // A mérés körülményei (kijelző, nézési távolság, beállítások, programverzió ...), lásd
    // MeasurementSessionWriter.
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IEnumerable<MeasurementResult> TestTrials => Trials.Where(trial => trial.Phase == TrialPhase.Test);

    public int TestTrialCount => TestTrials.Count();

    public int ValidTrialCount => TestTrials.Count(trial => trial.Valid);

    // Az összesítésbe csak az érvényes TESZTpróbák számítanak: a gyakorló próbák és az érvénytelenek
    // (tekintethiba, időtúllépés, pl. a kb. 3000 ms-os időkorlát) nem torzíthatják az eredményt.
    // Null, ha nincs egyetlen érvényes, mért értékkel rendelkező próba sem.
    public ReactionTimeSummary? EspSummary => Summarize(trial => trial.EspReactionTimeMs);

    public ReactionTimeSummary? LocalSummary => Summarize(trial => trial.LocalReactionTimeMs);

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

internal static class MeasurementTextExtensions
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
