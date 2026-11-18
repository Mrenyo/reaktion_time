using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A reakcióidő-mérés indítása és lezárása (MVP presenter). Egy mérési nézethez (IMeasurementView)
// tartozik; a szemkövetőt, az ESP-t, a tárolást, a beállításokat és a képidőzítést interfészeken
// keresztül kapja (konstruktoros függőséginjektálás), ezért WinForms és valódi hardver nélkül is
// futtatható. Magukat a próbákat a TrialRunner futtatja.
//
// Előtér és mérés: nyugalmi állapotban a nézet az előteret mutatja, a legutóbb betöltött beállítások
// alapján (_lobby* mezők). A mérés indításakor a presenter újra betölti és ellenőrzi a beállításokat,
// és egy változatlan pillanatképet (SessionContext) készít; a futó mérés minden lépése kizárólag ebből
// dolgozik, így a beállítások közbeni újratöltése nem hat rá.
//
// Szálkezelés: a nézet eseményei a UI-szálon érkeznek, és a presenter minden mezője csak ott íródik és
// olvasódik (az await-ek a UI-szálra térnek vissza). A próbák ciklusa háttérszálon fut (TrialRunner).
public sealed class MeasurementPresenter
{
    // A mérés vége után ennyi ideig tiltottak az előtér gombjai: egy késve érkező kattintás a
    // válaszegéren ne indítson rögtön új mérést az újra megjelenő "Új mérés indítása" gombbal.
    private static readonly TimeSpan PostSessionLockDuration = TimeSpan.FromSeconds(1.5);

    private static readonly string AppVersion =
        typeof(MeasurementPresenter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "ismeretlen";

    private readonly IMeasurementView _view;
    private readonly IGazeTracker _gazeTracker;
    private readonly IResponseDeviceConnector _deviceConnector;
    private readonly IMeasurementRepository _repository;
    private readonly IExperimentSettingsProvider _settingsProvider;
    private readonly IDisplayTimingProvider _displayTimingProvider;
    private readonly bool _manageTrackingLifecycle;

    // Az előtér adatai: a legutóbb betöltött beállítások, a hozzájuk tartozó tekintetszabály (null, ha
    // a beállítások hibásak) és a beállítási hiba szövege (null, ha nincs hiba).
    private ExperimentSettings _lobbySettings;
    private IGazeValidationRule? _lobbyRule;
    private string? _lobbyError;

    // A futó mérés; csak mérés közben van kitöltve.
    private TrialRunner? _runner;
    private CancellationTokenSource? _measurementCts;
    private Task? _measurementTask;

    private bool _trackerInitialized;
    private bool _optionalTrackerSearchDone;
    private bool _isTransitioning;
    private string _transitionText = string.Empty;
    private bool _isPostSessionLocked;

    // Az utolsó mérés kimenetele az előtér állapotsorához. Null, ha ebben a nézetben még nem volt
    // mérés, vagy a nézetet azóta elhagyták.
    private LastSessionSummary? _lastSession;

    public MeasurementPresenter(
        IMeasurementView view,
        IGazeTracker gazeTracker,
        IResponseDeviceConnector deviceConnector,
        IMeasurementRepository repository,
        IExperimentSettingsProvider settingsProvider,
        IDisplayTimingProvider displayTimingProvider,
        bool manageTrackingLifecycle)
    {
        _view = view;
        _gazeTracker = gazeTracker;
        _deviceConnector = deviceConnector;
        _repository = repository;
        _settingsProvider = settingsProvider;
        _displayTimingProvider = displayTimingProvider;
        _manageTrackingLifecycle = manageTrackingLifecycle;

        // Az előtér szövegeihez. A nézet helyzetét (GetStageInfo) itt még nem kérdezzük le: a MainForm a
        // nézetet előtöltéskor hozza létre, még mielőtt a felületre kerülne.
        ApplyLobbySettings(settingsProvider.Load());

        _view.StartRequested += async (_, _) => await RunSafelyAsync(StartAsync);
        _view.LeaveRequested += async (_, _) => await RunSafelyAsync(LeaveAsync);
        _view.AbortRequested += async (_, _) => await RunSafelyAsync(AbortAsync);
        _view.ResponseClicked += (_, eventArgs) => _runner?.HandleResponseClick(eventArgs.StagePoint);
        _view.Closed += (_, _) => _measurementCts?.Cancel();
        _gazeTracker.ConnectionChanged += (_, eventArgs) =>
            _ = RunSafelyAsync(() => _view.InvokeAsync(() => HandleTrackerConnectionChanged(eventArgs.IsConnected)));
        RefreshView();
    }

    // Minden mérés végén (befejezés, megszakítás) jelez, a rögzített próbákkal.
    public event EventHandler<MeasurementSessionResultEventArgs>? SessionEnded;

    // Kell-e kalibráció a legutóbb betöltött beállításokkal (a tekintet-ellenőrzéshez szemkövető és
    // kalibráció kell). Hibás beállításoknál hamis: a hibát az előtér mutatja. A MainForm a nézet
    // megnyitása előtt, a ReloadSettings után kérdezi le.
    public bool RequiresCalibration => _lobbyRule?.RequiresTracker == true;

    // Volt-e megerősített kalibráció ebben a programfutásban; a MainForm állítja be a nézet
    // megjelenítése előtt. Csak a metaadatokba kerül: a kalibrációt a MainForm követeli meg, és csak
    // tekintet-ellenőrzéses méréshez.
    public bool CalibrationConfirmed { get; set; }

    // A beállítások újratöltése az előtérhez (UI-szálon). A MainForm a nézet megjelenítése előtt és a
    // beállítások mentése után hívja. Futó mérés közben is biztonságos, mert a mérés a saját
    // pillanatképéből dolgozik. A tekintet-ellenőrzés nélküli méréshez újra engedélyezi a szemkövető
    // egyszeri keresését is (pl. ha közben csatlakoztatták az eszközt).
    public void ReloadSettings()
    {
        ApplyLobbySettings(_settingsProvider.Load());
        _optionalTrackerSearchDone = false;
        RefreshView();
    }

    public async Task StartAsync()
    {
        if (_isTransitioning || _isPostSessionLocked)
        {
            return;
        }

        await RunTransitionAsync("Mérés indítása...", async () =>
        {
            try
            {
                await StartMeasurementCoreAsync();
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    // A nézet elhagyása: a futó mérést leállítja (az eddigi próbák mentve maradnak). A nézet
    // elhagyásával az előző mérés kimenetele lekerül az állapotsorról.
    public async Task LeaveAsync()
    {
        if (_isTransitioning)
        {
            return;
        }

        await RunTransitionAsync("Leállítás...", StopMeasurementAsync);
        _lastSession = null;
        RefreshView();
    }

    // Megszakítás (Esc): a futó mérést leállítja, a nézet marad.
    public async Task AbortAsync()
    {
        if (_runner is not { } runner || _isTransitioning)
        {
            return;
        }

        Logger.Info($"Mérés megszakítása a felhasználó kérésére ({runner.Context.SessionName}).");
        await RunTransitionAsync("Leállítás...", StopMeasurementAsync);
    }

    // Az eseménykezelők async void-ként futnak: egy kiszökő kivétel az egész alkalmazást leállítaná
    // (pl. ha a nézet a leállítás befejezése előtt megszűnik), ezért itt naplózzuk.
    private static async Task RunSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.Error("Váratlan hiba a mérési nézet eseményének kezelése közben.", ex);
        }
    }

    // Indítás és leállítás: a művelet idejére a gombok tiltottak, és az indítógomb a folyamatot jelzi.
    private async Task RunTransitionAsync(string text, Func<Task> operation)
    {
        _isTransitioning = true;
        _transitionText = text;
        RefreshView();
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Logger.Error($"Váratlan hiba ({text}).", ex);
            _view.ShowUnexpectedError(ex);
        }
        finally
        {
            _isTransitioning = false;
            RefreshView();
        }
    }

    // A UI-szálon fut: előbb minden feltételt ellenőriz (beállítások, szemkövető, mérőfelület, kijelző,
    // ESP, mentési hely), és csak ha mind rendben van, indítja el a próbákat. Bármely hiba esetén minden
    // erőforrást felszabadít, és a nézet az előtérben marad.
    private async Task StartMeasurementCoreAsync()
    {
        await StopMeasurementAsync();

        var load = _settingsProvider.Load();
        ApplyLobbySettings(load);
        if (_lobbyError is not null)
        {
            Logger.Warning($"Hibás mérési beállítások: {_lobbyError}");
            _view.ShowSettingsError(_settingsProvider.SettingsFilePath, _lobbyError);
            return;
        }

        // Saját másolat: a pillanatkép az előtér beállításaitól is független.
        var settings = load.Settings.Clone();
        var rule = GazeValidationRules.For(settings.Gaze.Validation);
        if (!await EnsureTrackerAsync(rule))
        {
            return;
        }

        // Ha van szemkövető, a tekintetadat a tekintet-ellenőrzés nélküli mérésben is rögzül. A követést
        // ilyenkor mindig elindítjuk (ha már fut, pl. dev módban az overlay miatt, ez nem csinál semmit),
        // különben dev módban, ha az overlay indulásakor még nem volt eszköz, egyetlen tekintetminta sem
        // érkezne. Leállítani csak akkor állítjuk le, ha a presenter kezeli az életciklust.
        var gazeActive = _gazeTracker.IsTrackerAvailable;
        if (gazeActive)
        {
            _gazeTracker.StartTracking();
        }

        _view.ShowStimulus(null, paintImmediately: false);

        IResponseDevice? device = null;
        try
        {
            var geometry = await SessionGeometryBuilder.CaptureAsync(
                _view.GetStageInfo(), settings, rule, gazeActive, _displayTimingProvider, _gazeTracker);

            // A mérés csak válaszoló ESP-vel indul el: a pontos reakcióidőt a fotodiódás hardveres mérés
            // adja. Ha az ESP a mérés közben esik ki, a mérés nélküle, a PC által érzékelt kattintásokkal
            // fejeződik be.
            device = await TryConnectDeviceAsync(settings.Hardware);
            if (device is null)
            {
                return;
            }

            var seed = RandomNumberGenerator.GetInt32(int.MaxValue);
            var context = new SessionContext(
                settings,
                rule,
                geometry,
                gazeActive,
                seed,
                ParseColor(settings.Stimulus.Color),
                ParseColor(settings.Stimulus.BackgroundColor));

            var startedAt = DateTime.Now;
            var metadata = SessionMetadata.Build(context, _gazeTracker, device, CalibrationConfirmed, AppVersion);
            IMeasurementSessionWriter writer;
            try
            {
                writer = _repository.CreateSession(context.SessionName, startedAt, metadata);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Error("A mérési fájl nem hozható létre.", ex);
                _view.ShowSaveFailed(_repository.MeasurementsDirectory, ex);
                return;
            }

            var trials = settings.Trials;
            Logger.Info(
                $"Mérés indul: {context.SessionName} ({trials.PracticeTrials} gyakorló + {trials.MeasuredTrials} mért inger, "
                + $"tekintetadat: {(gazeActive ? "rögzül" : "nincs")}, véletlenmag: {seed}).");

            // Innentől az ESP a TrialRunneré: ő zárja le a mérés végén.
            var runner = new TrialRunner(
                context, _view, _gazeTracker, device, writer, startedAt, metadata.ToDictionary(pair => pair.Key, pair => pair.Value));
            device = null;
            _runner = runner;
            _measurementCts = new CancellationTokenSource();

            // A jelenet (beállított háttér, kereszt, fekete jelző) még a ciklus indulása előtt a nézetre
            // kerül, így az első inger biztosan már a mérési jeleneten jelenik meg.
            RefreshView();
            _measurementTask = RunSessionAsync(runner, _measurementCts.Token);
        }
        catch (MeasurementSetupException ex)
        {
            Logger.Warning($"A mérés nem indítható: {ex.Message}");
            _view.ShowSetupError(ex.Message);
        }
        finally
        {
            if (_runner is null)
            {
                if (device is not null)
                {
                    await device.DisposeAsync();
                }

                if (gazeActive && _manageTrackingLifecycle)
                {
                    _gazeTracker.StopTracking();
                }
            }
        }
    }

    // A próbák lefuttatása háttérszálon, majd a mérés lezárása a felületen.
    private async Task RunSessionAsync(TrialRunner runner, CancellationToken cancellationToken)
    {
        var outcome = await Task.Run(() => runner.RunAsync(cancellationToken), CancellationToken.None);
        try
        {
            await _view.InvokeAsync(() => EndSession(runner, outcome));
        }
        catch (Exception ex)
        {
            // Pl. az alkalmazás bezárásakor a nézet már megszűnt; a fájl ekkorra le van zárva.
            Logger.Error("A mérés lezárása a felületen nem sikerült.", ex);
        }
    }

    // Az előtér beállításainak frissítése egy betöltés eredményéből. A hibát itt nem jelezzük felugró
    // ablakban: az előtér összefoglalója mutatja, a mérés indítása pedig hibaüzenettel áll meg.
    [MemberNotNull(nameof(_lobbySettings))]
    private void ApplyLobbySettings(SettingsLoadResult load)
    {
        _lobbySettings = load.Settings;
        _lobbyError = load.Error;
        if (_lobbyError is null && load.Settings.Validate() is { Count: > 0 } errors)
        {
            _lobbyError = string.Join(Environment.NewLine, errors);
        }

        _lobbyRule = _lobbyError is null ? GazeValidationRules.For(load.Settings.Gaze.Validation) : null;
    }

    // Tekintet-ellenőrzéshez a szemkövető kötelező. Enélkül is használjuk, ha elérhető (a tekintetadat
    // ilyenkor is rögzül), de a hiánya nem akadályozza a mérést; az eszközkeresést, ami másodpercekig is
    // tarthat, ilyenkor a beállítások legutóbbi újratöltése (ReloadSettings) óta csak egyszer próbáljuk.
    private async Task<bool> EnsureTrackerAsync(IGazeValidationRule rule)
    {
        if (rule.RequiresTracker)
        {
            if (!_trackerInitialized || !_gazeTracker.IsTrackerAvailable)
            {
                await _gazeTracker.InitializeAsync(forceRefresh: true);
                _trackerInitialized = true;
            }

            if (!_gazeTracker.IsTrackerAvailable)
            {
                _view.ShowTrackerNotConnected();
                return false;
            }

            return true;
        }

        if (!_gazeTracker.IsTrackerAvailable && !_optionalTrackerSearchDone)
        {
            _optionalTrackerSearchDone = true;
            await _gazeTracker.InitializeAsync();
            _trackerInitialized = true;
        }

        if (!_gazeTracker.IsTrackerAvailable)
        {
            Logger.Info("A szemkövető nem érhető el: a mérés tekintet-ellenőrzés nélkül fut, ebben a mérésben nem rögzül tekintetadat.");
        }

        return true;
    }

    // A port megnyitása után kapcsolatellenőrzés ("CHECK" -> "OK"): a mérés csak akkor indul, ha az ESP
    // válaszol. Null, ha nem sikerült (a hibát a nézet már megmutatta, az eszköz le van zárva).
    private async Task<IResponseDevice?> TryConnectDeviceAsync(HardwareSettings hardware)
    {
        IResponseDevice device;
        try
        {
            device = await _deviceConnector.ConnectAsync(hardware, CancellationToken.None);
        }
        catch (ResponseDeviceConnectionException ex)
        {
            Logger.Warning($"[ESP] Kapcsolódás sikertelen ({ex.PortName}): {ex.Message}");
            _view.ShowEspNotConnected(ex.PortName, ex.InnerException ?? ex);
            return null;
        }

        Exception? error = null;
        try
        {
            if (await device.CheckAsync(CancellationToken.None))
            {
                // A firmware verziója csak a metaadatokba kerül; ha nem kérdezhető le, a mérés ettől
                // még elindul (a kommunikációs hibát viszont a lenti ág kezeli).
                var firmwareVersion = await device.RequestFirmwareVersionAsync(CancellationToken.None);
                Logger.Info($"[ESP] Kapcsolat rendben ({device.PortName}, firmware: {firmwareVersion ?? "ismeretlen"}).");
                return device;
            }

            Logger.Warning($"[ESP] A kapcsolatellenőrzés sikertelen ({device.PortName}): nem érkezett \"OK\" válasz.");
        }
        catch (Exception ex) when (TrialRunner.IsDeviceCommunicationError(ex))
        {
            Logger.Warning($"[ESP] A kapcsolatellenőrzés sikertelen ({device.PortName}): {ex.Message}");
            error = ex;
        }

        _view.ShowEspCommunicationError(device.PortName, device.BaudRate, error);
        await device.DisposeAsync();
        return null;
    }

    // UI-szálon. A szemkövető kiesése mérés közben: tekintet-ellenőrzéses mérésnél minden további próba
    // érvénytelen lenne, ezért a mérés leáll; anélkül a mérés tekintetadat nélkül folytatódik. A
    // résztvevő elé mérés közben nem kerül felugró ablak: a kiesést a mérés végén jelezzük.
    private void HandleTrackerConnectionChanged(bool isConnected)
    {
        if (_runner is not { } runner || !runner.Context.GazeActive)
        {
            return;
        }

        if (isConnected)
        {
            Logger.Info("[GAZE] A szemkövető kapcsolata mérés közben helyreállt.");
            return;
        }

        runner.NotifyTrackerConnectionLost();
        if (runner.Context.Rule.RequiresTracker)
        {
            Logger.Warning("[GAZE] A mérés leáll, mert a tekintet-ellenőrzéshez szemkövető kell.");
            _measurementCts?.Cancel();
        }
    }

    // UI-szálon, a mérés végén (befejezés, megszakítás, hiba).
    private void EndSession(TrialRunner runner, SessionRunOutcome outcome)
    {
        var context = runner.Context;
        var session = new MeasurementSessionResult(
            context.SessionName, runner.Results.ToArray(), runner.StartedAt, outcome.EndedAt, outcome.Status, runner.Metadata);
        _lastSession = new LastSessionSummary(
            outcome.Status,
            session.ValidTrialCount,
            context.Settings.Trials.MeasuredTrials,
            session.EspSummary?.MedianMs,
            MissingEspTrials: session.TestTrials.Count(trial => trial.LocalReactionTimeMs.HasValue && !trial.EspReactionTimeMs.HasValue),
            NoGazeDataTrials: session.Trials.Count(trial => trial.InvalidReason == TrialInvalidReason.TrackerUnavailable));
        Logger.Info(
            $"Mérés összegzés ({context.SessionName}): {session.ValidTrialCount}/{session.TestTrialCount} érvényes mért próba, "
            + $"ESP átlag={TrialRunner.FormatReaction(session.EspSummary?.MeanMs)}, medián={TrialRunner.FormatReaction(session.EspSummary?.MedianMs)}; "
            + $"PC átlag={TrialRunner.FormatReaction(session.LocalSummary?.MeanMs)}, medián={TrialRunner.FormatReaction(session.LocalSummary?.MedianMs)}.");

        ResetSessionState();
        SessionEnded?.Invoke(this, new MeasurementSessionResultEventArgs(session));
        StartPostSessionLock();
        RefreshView();

        if (outcome.UnexpectedError is { } unexpected)
        {
            _view.ShowUnexpectedError(unexpected);
        }

        if (outcome.SaveError is { } saveError)
        {
            _view.ShowSaveFailed(runner.FilePath, saveError);
        }

        if (outcome.EspLoss is { } espLoss)
        {
            _view.ShowEspConnectionLost(espLoss.PortName, espLoss.TrialLabel, espLoss.Error);
        }

        if (runner.TrackerLostAtTrial is { } trackerLostAt)
        {
            _view.ShowTrackerConnectionLost(trackerLostAt, measurementAborted: context.Rule.RequiresTracker);
        }
    }

    // UI-szálon: a gombok a PostSessionLockDuration leteltéig tiltottak (lásd ott).
    private void StartPostSessionLock()
    {
        _isPostSessionLocked = true;
        _ = ReleasePostSessionLockAsync();
    }

    private async Task ReleasePostSessionLockAsync()
    {
        try
        {
            await Task.Delay(PostSessionLockDuration).ConfigureAwait(false);
            await _view.InvokeAsync(() =>
            {
                _isPostSessionLocked = false;
                RefreshView();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Pl. az alkalmazás bezárásakor a nézet már megszűnt.
            Logger.Error("A mérés utáni zárolás feloldása a felületen nem sikerült.", ex);
        }
    }

    // UI-szálon: a futó mérés leállítása és megvárása (a TrialRunner lezárja az ESP-t és a fájlt, majd az
    // EndSession a felületet). A CancellationTokenSource-t csak a UI-szál éri el, így a megszakítás és a
    // felszabadítás nem versenyezhet.
    private async Task StopMeasurementAsync()
    {
        _measurementCts?.Cancel();
        if (_measurementTask is { } task)
        {
            await task;
            _measurementTask = null;
        }

        _measurementCts?.Dispose();
        _measurementCts = null;
        ResetSessionState();
        RefreshView();
    }

    // UI-szálon: a futó mérés állapotának törlése. A követést csak akkor állítja le, ha a presenter kezeli
    // az életciklusát.
    private void ResetSessionState()
    {
        if (_runner is not null && _manageTrackingLifecycle)
        {
            _gazeTracker.StopTracking();
        }

        _runner = null;
        _view.ShowStimulus(null, paintImmediately: false);
    }

    private static Color ParseColor(string text)
    {
        // Az ExperimentSettings.Validate() után mindig sikerül.
        return HexColor.TryParse(text, out var color)
            ? color
            : throw new InvalidOperationException($"Érvénytelen szín a beállításokban: {text}");
    }

    // A nézet teljes állapota; UI-szálon hívandó, és sosem kérdezi le a nézetet. Nyugalmi állapotban
    // az előtér látszik: a résztvevőnek szóló instrukció, a kutatónak szóló összefoglaló a
    // beállításokról (vagy a beállítási hiba) és az előző mérés kimenetele. Mérés közben a nézet csak a
    // mérési jelenetet mutatja (háttér, fixációs kereszt, inger, fotodióda-jelző): semmilyen szöveg nem
    // látszik, így szövegváltozás sem eshet egybe az inger megjelenésével. Az egérmutató csak ingerre
    // kattintós módban látszik, mert ott célozni kell vele.
    private void RefreshView()
    {
        var startButtonText = _isTransitioning
            ? _transitionText
            : _lastSession is null ? "Mérés indítása" : "Új mérés indítása";

        var session = _runner?.Context;
        var scene = session is null
            ? null
            : new MeasurementScene(session.BackgroundColor, session.StimulusColor, session.Geometry.FixationPoint, session.ClickOnStimulus);

        _view.ApplyState(new MeasurementViewState(
            IsRunning: session is not null,
            IsBusy: _isTransitioning || _isPostSessionLocked,
            StartButtonText: startButtonText,
            InstructionText: LobbyText.BuildInstruction(_lobbySettings, _lobbyRule),
            SettingsSummaryText: LobbyText.BuildSettingsSummary(_lobbySettings, _lobbyError),
            StatusText: LobbyText.BuildStatus(_lastSession),
            Scene: scene,
            PhotodiodeMarkerCenter: (session?.Settings ?? _lobbySettings).Hardware.PhotodiodeMarkerCenter));
    }
}
