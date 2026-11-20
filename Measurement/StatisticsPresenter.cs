using System.Globalization;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A próbasor jellege a táblázatban; a nézet ebből választ színt.
public enum StatisticsRowKind
{
    Practice,
    Valid,
    Invalid
}

// A táblázat egy sora, megjelenítésre kész szövegekkel.
public sealed record StatisticsTrialRow(
    string Trial,
    string EspTime,
    string LocalTime,
    string Difference,
    string Status,
    string ScanoutCorrection,
    StatisticsRowKind Kind);

// Minden, amit a statisztika nézet megjelenít (passzív nézet).
public sealed record StatisticsViewState(
    IReadOnlyList<string> SessionEntries,  // a választólista elemei, a legutóbbi méréssel az elején
    int SelectedIndex,                     // -1, ha nincs mérés
    string HeaderText,
    IReadOnlyList<StatisticsTrialRow> Rows,
    string SummaryText,
    string TotalText);

// A statisztika nézet a presenter szemszögéből. A tagokat a UI-szálról kell hívni.
public interface IStatisticsView
{
    // A kutató másik mérést választott a listából (az elem indexe).
    event EventHandler<int>? SessionSelected;

    void ApplyState(StatisticsViewState state);
}

// A statisztika nézet logikája (MVP presenter): a lemezre mentett mérések beolvasása, a kiválasztott mérés
// próbái és összesítése. A mentett méréseket egyszer, a létrehozáskor, háttérszálon olvassa be; az
// alkalmazás futása alatt befejezett mérések a memóriából kerülnek a lista elejére (AddSession).
public sealed class StatisticsPresenter
{
    private readonly IStatisticsView _view;
    private readonly IMeasurementRepository _repository;

    // A megjelenített mérések, a legutóbbival az elején.
    private List<MeasurementSessionResult> _sessions = [];
    private int _selectedIndex = -1;
    private bool _isLoading = true;

    // A beolvasás problémája (beolvashatatlan fájlok, vagy a beolvasás hibája); null, ha nem volt.
    private string? _loadNotice;

    public StatisticsPresenter(IStatisticsView view, IMeasurementRepository repository)
    {
        _view = view;
        _repository = repository;
        _view.SessionSelected += (_, index) => Select(index);
        Refresh();
    }

    // A lemezen lévő előzmény beolvasása háttérszálon, hogy a sok mentett mérés feldolgozása ne akassza
    // meg a felületet. UI-szálról hívandó; a folytatás is ott fut. A beolvasás alatt hozzáadott
    // (AddSession) mérések a lista elején maradnak.
    public async Task LoadAsync()
    {
        MeasurementLoadResult loaded;
        try
        {
            loaded = await Task.Run(_repository.LoadAll);
        }
        catch (Exception ex)
        {
            Logger.Error("A mentett mérések beolvasása sikertelen.", ex);
            loaded = new MeasurementLoadResult([], []);
            _loadNotice = "A mentett mérések beolvasása sikertelen (részletek a naplóban).";
        }

        if (loaded.UnreadableFiles.Count > 0)
        {
            _loadNotice = $"{loaded.UnreadableFiles.Count} mérési fájl nem olvasható be (részletek a naplóban).";
        }

        var addedMeanwhile = _sessions.Where(added => !loaded.Sessions.Any(existing => IsSameSession(existing, added)));
        _sessions = [.. addedMeanwhile, .. loaded.Sessions];
        _isLoading = false;
        _selectedIndex = _sessions.Count > 0 ? 0 : -1;
        Refresh();
    }

    // Az imént befejezett mérés a lista elejére kerül, és ez jelenik meg. A mérés a memóriából jön, így
    // akkor is látszik, ha a lemezre írása meghiúsult.
    public void AddSession(MeasurementSessionResult session)
    {
        if (_sessions.Any(existing => IsSameSession(existing, session)))
        {
            return;
        }

        _sessions.Insert(0, session);
        _selectedIndex = 0;
        Refresh();
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _sessions.Count || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        Refresh();
    }

    // Ugyanaz a mérés a memóriából és a lemezről: a kezdési idő a fájlba veszteség nélkül ("o"
    // formátumban) kerül, így a beolvasott érték pontosan egyezik a memóriabelivel.
    private static bool IsSameSession(MeasurementSessionResult first, MeasurementSessionResult second)
    {
        return ReferenceEquals(first, second)
            || (first.ModeName == second.ModeName && first.StartedAt.ToUniversalTime() == second.StartedAt.ToUniversalTime());
    }

    private void Refresh()
    {
        var entries = _sessions.Select(FormatEntry).ToList();
        var session = _selectedIndex >= 0 ? _sessions[_selectedIndex] : null;
        _view.ApplyState(session is null ? BuildEmptyState(entries) : BuildState(entries, session));
    }

    private StatisticsViewState BuildEmptyState(IReadOnlyList<string> entries)
    {
        var header = _isLoading
            ? "A mentett mérések betöltése..."
            : _loadNotice ?? "Mérés típusa: még nincs adat";
        return new StatisticsViewState(entries, -1, header, [], "-", "Érvényes: 0 / 0");
    }

    private StatisticsViewState BuildState(IReadOnlyList<string> entries, MeasurementSessionResult session)
    {
        var statusSuffix = session.Status == SessionStatus.Completed ? string.Empty : $" – {session.Status.ToDisplayText()}";
        var skippedSuffix = session.SkippedRowCount > 0 ? $" – {session.SkippedRowCount} hibás sor kihagyva" : string.Empty;
        var header = $"Mérés típusa: {session.ModeName} ({session.StartedAt:yyyy-MM-dd HH:mm}){statusSuffix}{skippedSuffix}";

        // A gyakorló próbák is látszanak ("Gy" előtaggal), de az összesítésbe nem számítanak.
        var rows = session.Trials.Select(trial =>
        {
            var isPractice = trial.Phase == TrialPhase.Practice;
            return new StatisticsTrialRow(
                isPractice ? $"Gy{trial.TrialNumber}" : trial.TrialNumber.ToString(CultureInfo.InvariantCulture),
                FormatMs(trial.EspReactionTimeMs),
                FormatMs(trial.LocalReactionTimeMs),
                FormatMs(trial.LocalMinusEspMs),
                isPractice ? "Gyakorló" : trial.InvalidReason.ToDisplayText(),
                trial.ScanoutCorrectionUs is { } correctionUs ? correctionUs.ToString("0.###", CultureInfo.CurrentCulture) : "-",
                isPractice ? StatisticsRowKind.Practice : trial.Valid ? StatisticsRowKind.Valid : StatisticsRowKind.Invalid);
        }).ToList();

        var summaryLines = new List<string>
        {
            $"ESP: {FormatSummary(session.EspSummary)}",
            $"PC: {FormatSummary(session.LocalSummary)}",
            $"PC − ESP: {FormatSummary(session.DifferenceSummary)}"
        };
        if (_loadNotice is not null)
        {
            summaryLines.Add(_loadNotice);
        }

        return new StatisticsViewState(
            entries,
            _selectedIndex,
            header,
            rows,
            string.Join(Environment.NewLine, summaryLines),
            $"Érvényes: {session.ValidTrialCount} / {session.TestTrialCount}");
    }

    private static string FormatEntry(MeasurementSessionResult session) =>
        $"{session.StartedAt:yyyy-MM-dd HH:mm} – {session.ModeName} ({session.TestTrialCount} próba"
        + (session.Status == SessionStatus.Completed ? ")" : $", {session.Status.ToDisplayText()})");

    private static string FormatMs(double? value) =>
        value.HasValue ? value.Value.ToString("0.0", CultureInfo.CurrentCulture) : "-";

    // Az összesítés csak az érvényes próbák meglévő értékeiből készül (lásd MeasurementSessionResult). Az
    // ESP-reakcióidő érvényes próbában is hiányozhat (az ESP nem válaszolt), ezért az elemszám is látszik.
    private static string FormatSummary(ReactionTimeSummary? summary)
    {
        if (summary is null)
        {
            return "nincs adat";
        }

        var culture = CultureInfo.CurrentCulture;
        var standardDeviation = summary.StandardDeviationMs?.ToString("0.0", culture) ?? "-";
        return string.Format(
            culture,
            "átlag {0:0.0} · medián {1:0.0} · SD {2} ms (n = {3})",
            summary.MeanMs,
            summary.MedianMs,
            standardDeviation,
            summary.Count);
    }
}

// A mérési eredmények felületen megjelenő szövegei.
internal static class ResultDisplayText
{
    internal static string ToDisplayText(this TrialInvalidReason reason) => reason switch
    {
        TrialInvalidReason.None => "Érvényes",
        TrialInvalidReason.Timeout => "Időtúllépés",
        TrialInvalidReason.Anticipation => "Elsietett válasz",
        TrialInvalidReason.TrackerUnavailable => "Nincs szemkövető",
        TrialInvalidReason.InsufficientGazeData => "Kevés tekintetadat",
        TrialInvalidReason.GazeViolation => "Tekintethiba",
        TrialInvalidReason.MissedStimulus => "Mellékattintás",
        _ => "Érvénytelen"
    };

    internal static string ToDisplayText(this SessionStatus status) => status switch
    {
        SessionStatus.Completed => "befejezett",
        SessionStatus.Aborted => "megszakítva",
        _ => "nem zárult le"
    };
}
