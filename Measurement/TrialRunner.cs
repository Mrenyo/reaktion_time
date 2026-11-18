using System.Diagnostics;
using System.Globalization;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// Egy elindított mérés próbáinak lefuttatása: a próbák ciklusa, az inger megjelenítése, a válasz
// fogadása, az ESP lekérdezése, a próba kiértékelése és mentése. A mérés indítását és lezárását a
// MeasurementPresenter végzi; egy TrialRunner pontosan egy mérést futtat le.
//
// Szálkezelés: a RunAsync ciklusa háttérszálon fut, és minden nézet- vagy szemkövető-állapotot érintő
// lépést az IMeasurementView.InvokeAsync-kal a UI-szálra küld. A próba állapotát (inger, előperiódus,
// válaszablak) csak a UI-szál írja és olvassa; a HandleResponseClick és a NotifyTrackerConnectionLost is
// ott hívandó. A véletlenszám-generátort a ciklus és a UI-szál felváltva, egymást megvárva használja.
internal sealed class TrialRunner
{
    // Az előző próba vége (a fotodióda-jelző visszavált feketére) és a következő próba scanout-korrekciójának
    // elküldése (az ESP-nél az új próba kezdete) között legalább ennyi idő telik el. Így a fotodióda jele a
    // fehér -> fekete váltás után biztosan lecseng, mielőtt az ESP az új próbára élesíti magát; enélkül az
    // ESP a még lecsengő jelet az új próba fényjelének vehette, és a reakcióidőt a próba elejétől mérte.
    private static readonly TimeSpan MarkerSettleBeforeTrial = TimeSpan.FromMilliseconds(200);

    private readonly SessionContext _context;
    private readonly IMeasurementView _view;
    private readonly IGazeTracker _gazeTracker;
    private readonly IMeasurementSessionWriter _writer;
    private readonly Random _random;
    private readonly TrialSchedule _schedule;
    private readonly List<MeasurementResult> _results = new();
    private readonly Stopwatch _reactionStopwatch = new();
    private readonly Stopwatch _foreperiodStopwatch = new();

    // Null, ha az ESP a mérés közben kiesett (a mérés nélküle folytatódik).
    private IResponseDevice? _device;

    // A próba állapota (UI-szál). A válaszablak az inger kirajzolásától az első kattintásig vagy az
    // időkorlátig tart (null, ha éppen nem várunk válaszra); az előperiódus alatt érkező kattintás
    // elsietett válasz.
    private string _trialLabel = string.Empty;
    private RectangleF _stimulusRect = RectangleF.Empty;
    private long _stimulusOnsetUs;
    private TaskCompletionSource<ParticipantResponse>? _pendingResponse;
    private bool _inForeperiod;
    private int _prematureClicks;
    private double _actualForeperiodMs;
    private Exception? _saveError;
    private string? _trackerLostAtTrial;

    public TrialRunner(
        SessionContext context,
        IMeasurementView view,
        IGazeTracker gazeTracker,
        IResponseDevice device,
        IMeasurementSessionWriter writer,
        DateTime startedAt,
        IReadOnlyDictionary<string, string> metadata)
    {
        _context = context;
        _view = view;
        _gazeTracker = gazeTracker;
        _device = device;
        _writer = writer;
        StartedAt = startedAt;
        Metadata = metadata;
        _random = new Random(context.RandomSeed);
        _schedule = new TrialSchedule(context.Settings.Trials.PracticeTrials, context.Settings.Trials.MeasuredTrials);
    }

    public SessionContext Context => _context;

    public DateTime StartedAt { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public string FilePath => _writer.FilePath;

    // Az eddig rögzített próbák (UI-szálon olvasandó).
    public IReadOnlyList<MeasurementResult> Results => _results;

    // A próba, amelyben a szemkövető kapcsolata megszakadt; null, ha nem szakadt meg (UI-szál).
    public string? TrackerLostAtTrial => _trackerLostAtTrial;

    // A szemkövető kapcsolata megszakadt (UI-szálon). A próbák ettől kezdve tekintetadat nélkül futnak.
    public void NotifyTrackerConnectionLost()
    {
        _trackerLostAtTrial ??= _trialLabel.Length > 0 ? _trialLabel : "1";
        Logger.Warning($"[GAZE] A szemkövető kapcsolata megszakadt a(z) {_trackerLostAtTrial}. próbában.");
    }

    // A próbák lefuttatása a megszakításig vagy az utolsó próbáig; a végén az ESP-t lezárja, a mérési fájlt
    // lezárja (szálfüggetlenül, hogy akkor is megkapja a lezáró sort, ha a nézet már nem frissíthető), és
    // visszaadja a kimenetelt. Kivételt nem dob.
    public async Task<SessionRunOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var status = SessionStatus.Aborted;
        EspConnectionLoss? espLoss = null;
        Exception? unexpectedError = null;
        try
        {
            espLoss = await RunTrialsAsync(cancellationToken).ConfigureAwait(false);
            if (_schedule.IsCompleted && _saveError is null)
            {
                status = SessionStatus.Completed;
            }

            Logger.Info(
                $"Mérés véget ért ({_context.SessionName}, {status}): {_schedule.ValidTestTrials}/{_context.Settings.Trials.MeasuredTrials} "
                + $"érvényes mért próba; lefutott {_schedule.TestTrialsDone} mért és {_schedule.PracticeTrialsDone} gyakorló próba.");
        }
        catch (OperationCanceledException)
        {
            Logger.Info(
                $"Mérés megszakítva ({_context.SessionName}): {_schedule.TestTrialsDone}/{_context.Settings.Trials.MeasuredTrials} "
                + $"mért próba után, ebből {_schedule.ValidTestTrials} érvényes.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Váratlan hiba a mérési ciklusban ({_context.SessionName}).", ex);
            unexpectedError = ex;
        }

        await DisposeDeviceAsync().ConfigureAwait(false);

        // Egy megszakított próba félbemaradt tekintetfelvételét el kell dobni, különben (főleg dev
        // módban, ahol a követés a mérés után is fut) tovább gyűlnének a minták. Szemkövető nélkül
        // nincs felvétel, és a Tobii-tagokat ilyenkor nem hívjuk (a natív könyvtár is hiányozhat).
        if (_context.GazeActive)
        {
            _gazeTracker.CancelGazeRecording();
        }

        var endedAt = DateTime.Now;
        Exception? closeError = null;
        try
        {
            _writer.Close(status, endedAt);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.Error("A mérési fájl lezárása sikertelen.", ex);
            closeError = ex;
        }

        return new SessionRunOutcome(status, endedAt, _saveError ?? closeError, espLoss, unexpectedError);
    }

    // UI-szálon, a nézet ResponseClicked eseményére. Csak az inger megjelenése utáni első kattintás
    // számít válasznak; az előperiódus alatti kattintás elsietett válasz (a próba érvénytelen lesz,
    // TrialInvalidReason.Anticipation), a válasz utáni csak a naplóba kerül. Az eltelt idő és az
    // időbélyeg a kezelés legelején készül. A válasz után az inger azonnal eltűnik - ingerre kattintós
    // módban akkor is, ha a kattintás mellé ment (ezt a RecordReaction értékeli).
    public void HandleResponseClick(PointF clickPoint)
    {
        var localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;
        var pending = _pendingResponse;
        _pendingResponse = null;
        if (pending is null)
        {
            if (_inForeperiod)
            {
                _prematureClicks++;
                Logger.Info("[KATTINTÁS] Elsietett kattintás az előperiódus alatt (még az inger megjelenése előtt): a próba érvénytelen lesz.");
            }
            else
            {
                Logger.Info("[KATTINTÁS] Kattintás a válasz után; nem számít válasznak.");
            }

            return;
        }

        var responseUs = _context.GazeActive ? _gazeTracker.GetSystemTimeStampUs() : 0;
        if (!pending.TrySetResult(new ParticipantResponse(localReactionTimeMs, clickPoint, responseUs)))
        {
            Logger.Info("[KATTINTÁS] Kattintás a válaszidő-korlát lejárta után; nem számít válasznak.");
            return;
        }

        _reactionStopwatch.Stop();
        _view.ShowStimulus(null, paintImmediately: false);
    }

    // A próbák ciklusa; visszaadja az ESP esetleges kiesését.
    private async Task<EspConnectionLoss?> RunTrialsAsync(CancellationToken cancellationToken)
    {
        var trials = _context.Settings.Trials;
        EspConnectionLoss? espLoss = null;

        // Az előző próba vége óta eltelt idő (lásd MarkerSettleBeforeTrial); az első próba előtt a jelző
        // a SceneSettleDelayMs alatt már végig fekete volt.
        Stopwatch? sincePreviousTrial = null;

        // A mérési jelenet (beállított háttér, kereszt, fekete fotodióda-jelző) már látszik: az első
        // próba előtt a szem alkalmazkodik az új háttérhez, és a szemkövető is beáll.
        await Task.Delay(trials.SceneSettleDelayMs, cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested && _schedule.HasNextTrial && _saveError is null)
        {
            var phase = _schedule.CurrentPhase;
            var trialNumber = _schedule.NextTrialNumber;
            var trialLabel = FormatTrialLabel(phase, trialNumber);
            Logger.Info($"[PRÓBA] {trialLabel}. próba indul (eddig: {_schedule.ProgressText}).");

            var scanout = await PrepareTrialAsync(trialLabel).ConfigureAwait(false);
            var correctionMessage = scanout.CreateCorrectionMessage();
            Logger.Info($"[SCANOUT] {trialLabel}. próba: correctionUs={correctionMessage}");
            var foreperiodMs = ForeperiodSampler.Sample(_random, trials);

            // Null, ha az ESP egy korábbi próbában kiesett: a próba ilyenkor is lefut, ESP-adat nélkül.
            var device = _device;
            if (device is not null)
            {
                if (sincePreviousTrial is not null && sincePreviousTrial.Elapsed < MarkerSettleBeforeTrial)
                {
                    await Task.Delay(MarkerSettleBeforeTrial - sincePreviousTrial.Elapsed, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    await device.SendScanoutCorrectionAsync(correctionMessage, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsDeviceCommunicationError(ex))
                {
                    espLoss = await DropDeviceAsync(device, trialLabel, ex).ConfigureAwait(false);
                    device = null;
                }
            }

            var responseCompletion = await ShowStimulusAfterForeperiodAsync(foreperiodMs, cancellationToken).ConfigureAwait(false);
            var response = await WaitForResponseAsync(responseCompletion, cancellationToken).ConfigureAwait(false);

            // Az ESP reakcióidejét a kattintás után kérjük le. Ha nem érkezik, a próba ESP-adat nélkül
            // kerül rögzítésre; az érvényességét ez nem befolyásolja.
            double? espReactionTimeMs = null;
            if (!response.TimedOut && device is not null)
            {
                try
                {
                    espReactionTimeMs = await device.RequestReactionTimeMsAsync(cancellationToken).ConfigureAwait(false);
                    if (espReactionTimeMs is null)
                    {
                        Logger.Warning($"[ESP] A(z) {trialLabel}. próbában az RTIME kérésre nem érkezett értelmezhető reakcióidő.");
                    }
                }
                catch (Exception ex) when (IsDeviceCommunicationError(ex))
                {
                    espLoss = await DropDeviceAsync(device, trialLabel, ex).ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var resultNote = response.TimedOut
                ? " (nem jött kattintás az időkorláton belül, a próba érvénytelen)"
                : espReactionTimeMs is null ? " (nincs ESP-reakcióidő)" : string.Empty;
            Logger.Info(
                $"[EREDMÉNY] {trialLabel}. próba: ESP={FormatReaction(espReactionTimeMs)}, PC={FormatReaction(response.LocalReactionTimeMs)}, "
                + $"előperiódus={foreperiodMs} ms{resultNote}");

            var outcome = new TrialOutcome(phase, trialNumber, foreperiodMs, scanout.CalculateScanoutOffsetMicroseconds(), espReactionTimeMs, response);
            await _view.InvokeAsync(() => RecordReaction(outcome)).ConfigureAwait(false);

            // A RecordReaction után a jelző mindenképp fekete (időtúllépésnél is itt tűnik el az inger).
            sincePreviousTrial = Stopwatch.StartNew();
        }

        return espLoss;
    }

    // UI-szálon: a próba ingerhelyének kisorsolása és a scanout-korrekció paraméterei. Az inger helye: a
    // fixációs ponttól mért távolság egyenletes eloszlású a [MinEccentricityPx, elférő maximum]
    // tartományban, az irány pedig egyenletes az adott távolságban elférő irányok között
    // (StimulusPlacement.TrySampleStimulusRect).
    private async Task<ScanoutTrialParameters> PrepareTrialAsync(string trialLabel)
    {
        ScanoutTrialParameters? parameters = null;
        await _view.InvokeAsync(() =>
        {
            _trialLabel = trialLabel;
            var geometry = _context.Geometry;
            if (!geometry.Placement.TrySampleStimulusRect(
                    _random,
                    _context.Settings.Stimulus.MinEccentricityPx,
                    geometry.EffectiveMaxEccentricityPx,
                    out var rect))
            {
                throw new InvalidOperationException("Nem sikerült az ingert a beállított távolságtartományban elhelyezni.");
            }

            _stimulusRect = rect;
            var stage = _view.GetStageInfo();
            parameters = ScanoutGeometry.CreateTrialParameters(
                rect,
                _context.Settings.Hardware.ResolvePhotodiodeBounds(stage.StageSize),
                stage.StageScreenOrigin,
                stage.ScreenBounds,
                geometry.Timing);
        }).ConfigureAwait(false);

        return parameters
            ?? throw new OperationCanceledException($"A(z) {trialLabel}. próba nem készíthető elő: a mérési nézet már nem elérhető.");
    }

    // A visszaadott válaszablakot a próba első kattintása (HandleResponseClick) vagy az időkorlát
    // (WaitForResponseAsync) teljesíti.
    private async Task<TaskCompletionSource<ParticipantResponse>> ShowStimulusAfterForeperiodAsync(
        int foreperiodMs,
        CancellationToken cancellationToken)
    {
        await _view.InvokeAsync(() =>
        {
            _reactionStopwatch.Reset();

            // A tekintet a próba teljes idejére (előperiódus + inger + válasz) rögzítésre kerül; a
            // felvételt a RecordReaction zárja le.
            if (_context.GazeActive)
            {
                _gazeTracker.BeginGazeRecording();
            }

            _view.ShowStimulus(null, paintImmediately: false);
            _prematureClicks = 0;
            _inForeperiod = true;
            _foreperiodStopwatch.Restart();
        }).ConfigureAwait(false);

        await Task.Delay(foreperiodMs, cancellationToken).ConfigureAwait(false);

        TaskCompletionSource<ParticipantResponse>? responseCompletion = null;
        await _view.InvokeAsync(() => responseCompletion = PresentStimulus()).ConfigureAwait(false);

        // A megszűnt nézeten (pl. az alkalmazás bezárásakor) az InvokeAsync nem futtat semmit; a mérés
        // ilyenkor már le van állítva.
        cancellationToken.ThrowIfCancellationRequested();
        return responseCompletion
            ?? throw new OperationCanceledException("Az inger nem jeleníthető meg: a mérési nézet már nem elérhető.");
    }

    // UI-szálon: az inger és a fotodióda-jelző szinkron kirajzolása, majd rögtön utána az időbélyegek -
    // így azok már a kirajzolás után készülnek. Innentől az első kattintás a válasz.
    private TaskCompletionSource<ParticipantResponse> PresentStimulus()
    {
        _view.ShowStimulus(_stimulusRect, paintImmediately: true);
        _stimulusOnsetUs = _context.GazeActive ? _gazeTracker.GetSystemTimeStampUs() : 0;
        _reactionStopwatch.Restart();
        _actualForeperiodMs = _foreperiodStopwatch.Elapsed.TotalMilliseconds;
        _inForeperiod = false;

        // A RunContinuationsAsynchronously miatt a kattintás kezelése nem futtatja a próbák ciklusát a UI-szálon.
        _pendingResponse = new TaskCompletionSource<ParticipantResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pendingResponse;
    }

    // A résztvevő válasza: az első kattintás az inger megjelenése után, legfeljebb ReactionTimeoutMs-ig.
    // Ha addig nem jön kattintás, az nem hiba (kimaradt vagy túl lassú reakció): a próba időtúllépéssel
    // érvénytelen, és a mérés a következő próbával folytatódik. Ha az időkorlát és egy kattintás
    // egyszerre érkezik, az számít, amelyik előbb teljesíti a válaszablakot.
    private async Task<ParticipantResponse> WaitForResponseAsync(
        TaskCompletionSource<ParticipantResponse> responseCompletion,
        CancellationToken cancellationToken)
    {
        var timeout = Task.Delay(_context.Settings.Trials.ReactionTimeoutMs, cancellationToken);
        if (await Task.WhenAny(responseCompletion.Task, timeout).ConfigureAwait(false) != responseCompletion.Task)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeoutUs = _context.GazeActive ? _gazeTracker.GetSystemTimeStampUs() : 0;
            responseCompletion.TrySetResult(new ParticipantResponse(null, null, timeoutUs));
        }

        return await responseCompletion.Task.ConfigureAwait(false);
    }

    // UI-szálon: a próba kiértékelése, rögzítése és mentése. Mentési hibánál a ciklus a próba után leáll.
    private void RecordReaction(TrialOutcome outcome)
    {
        _pendingResponse = null;
        _reactionStopwatch.Reset();
        _view.ShowStimulus(null, paintImmediately: false);

        var geometry = _context.Geometry;
        var response = outcome.Response;

        // Szemkövető nélkül nincs felvétel: a próba tekintetadatai null-ok.
        TrialGazeRecording? gazeRecording = _context.GazeActive
            ? new TrialGazeRecording(
                _stimulusOnsetUs,
                response.ResponseWindowEndUs,
                geometry.Stage.ScreenBounds,
                _view.GetStageInfo().StageScreenOrigin,
                _gazeTracker.EndGazeRecording())
            : null;

        var invalidReason = TrialEvaluator.DetermineInvalidReason(
            _context,
            new TrialResponse(response.TimedOut, response.ClickPoint, outcome.EspReactionTimeMs ?? response.LocalReactionTimeMs, _prematureClicks),
            gazeRecording,
            _stimulusRect);

        var result = new MeasurementResult
        {
            Phase = outcome.Phase,
            TrialNumber = outcome.TrialNumber,
            PlannedTestTrials = _context.Settings.Trials.MeasuredTrials,
            EspReactionTimeMs = outcome.EspReactionTimeMs,
            LocalReactionTimeMs = response.LocalReactionTimeMs,
            RecordedAt = DateTime.Now,
            InvalidReason = invalidReason,
            ForeperiodMs = outcome.ForeperiodMs,
            ActualForeperiodMs = _actualForeperiodMs,
            PrematureClicks = _prematureClicks,
            ScanoutCorrectionUs = outcome.ScanoutCorrectionUs,
            StimulusBounds = _stimulusRect,
            StimulusEccentricityPx = StimulusPlacement.Distance(StimulusPlacement.GetCenter(_stimulusRect), geometry.FixationPoint),
            ValidGazeRatio = gazeRecording?.ValidResponseWindowRatio,
            GazeSamplesInWindow = gazeRecording?.ResponseWindowSampleCount,
            MeanEyeDistanceMm = gazeRecording?.MeanEyeDistanceMm,
            ClickPoint = response.ClickPoint,
            GazeRecording = gazeRecording
        };
        _results.Add(result);
        _schedule.Record(outcome.Phase, result.Valid);

        var trialLabel = FormatTrialLabel(outcome.Phase, outcome.TrialNumber);
        Logger.Info(
            $"[PRÓBA] {trialLabel}. próba: tényleges előperiódus {_actualForeperiodMs:0.#} ms (tervezett: {outcome.ForeperiodMs} ms), "
            + $"elsietett kattintások: {_prematureClicks}.");
        Logger.Info(gazeRecording is null
            ? $"[GAZE] {trialLabel}. próba: nincs tekintetadat, távolság {result.StimulusEccentricityPx:0} px, eredmény: {invalidReason}."
            : $"[GAZE] {trialLabel}. próba: {gazeRecording.Samples.Count} minta, ebből {gazeRecording.ResponseWindowSampleCount} a válaszablakban "
                + $"({(response.ResponseWindowEndUs - _stimulusOnsetUs) / 1000d:0} ms), érvényes arány {gazeRecording.ValidResponseWindowRatio:P0}, "
                + $"távolság {result.StimulusEccentricityPx:0} px, eredmény: {invalidReason}.");

        try
        {
            _writer.AppendTrial(result);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            Logger.Error("A próba mentése sikertelen, a mérés leáll.", ex);
            _saveError = ex;
        }
    }

    // Az ESP kommunikációs hibája (pl. kihúzott eszköz) nem állítja le a mérést: az ESP-t a mérés hátralévő
    // részére lezárjuk, a próbák a PC által érzékelt kattintással, ESP-reakcióidő nélkül folytatódnak. A
    // hibát a mérés végén jelezzük, mert mérés közben a felugró ablak a résztvevő elé kerülne.
    private async Task<EspConnectionLoss> DropDeviceAsync(IResponseDevice device, string trialLabel, Exception exception)
    {
        Logger.Warning($"[ESP] Kommunikációs hiba a(z) {trialLabel}. próbában: {exception.Message} A mérés ESP nélkül folytatódik.");
        await DisposeDeviceAsync().ConfigureAwait(false);
        return new EspConnectionLoss(device.PortName, trialLabel, exception);
    }

    private async Task DisposeDeviceAsync()
    {
        var device = Interlocked.Exchange(ref _device, null);
        if (device is null)
        {
            return;
        }

        try
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warning($"[ESP] Az ESP lezárása közben hiba történt: {ex.Message}");
        }
    }

    internal static bool IsDeviceCommunicationError(Exception ex) =>
        ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException;

    internal static string FormatTrialLabel(TrialPhase phase, int trialNumber) =>
        phase == TrialPhase.Practice ? $"Gy{trialNumber}" : trialNumber.ToString(CultureInfo.InvariantCulture);

    internal static string FormatReaction(double? value) => value.HasValue ? $"{value.Value:0} ms" : "-";

    // A próba válasza: a kirajzolástól a kattintásig eltelt idő a PC-n és a kattintás helye a mérőfelületen
    // (mindkettő null, ha az időkorláton belül nem jött kattintás), valamint a tekintet-kiértékelés
    // válaszablakának vége a szemkövető óráján (a kattintás, illetve az időkorlát lejártának ideje;
    // szemkövető nélkül 0, mert ilyenkor nincs tekintetfelvétel).
    private readonly record struct ParticipantResponse(double? LocalReactionTimeMs, PointF? ClickPoint, long ResponseWindowEndUs)
    {
        public bool TimedOut => LocalReactionTimeMs is null;
    }

    private readonly record struct TrialOutcome(
        TrialPhase Phase,
        int TrialNumber,
        int ForeperiodMs,
        double ScanoutCorrectionUs,
        double? EspReactionTimeMs,
        ParticipantResponse Response);
}

// Az ESP kiesése a mérés közben: melyik porton, melyik próbában, milyen hibával.
internal sealed record EspConnectionLoss(string PortName, string TrialLabel, Exception Error);

// Egy lefutott mérés kimenetele. SaveError: a próba mentésének vagy a fájl lezárásának hibája;
// UnexpectedError: a ciklus váratlan hibája (a mérés ilyenkor megszakadt).
internal sealed record SessionRunOutcome(
    SessionStatus Status,
    DateTime EndedAt,
    Exception? SaveError,
    EspConnectionLoss? EspLoss,
    Exception? UnexpectedError);
