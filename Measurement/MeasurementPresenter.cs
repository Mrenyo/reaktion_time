using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A reakcióidő-mérés menete (MVP presenter). Egy mérési nézethez (IMeasurementView) és egy mérési
// módhoz (IMeasurementMode) tartozik; a szemkövetőt, az ESP-t, a tárolást, a beállításokat és a
// képidőzítést interfészeken keresztül kapja (konstruktoros függőséginjektálás), ezért WinForms és
// valódi hardver nélkül is futtatható.
//
// Szálkezelés: a nézet eseményei a UI-szálon érkeznek, a mérés előkészítése is ott fut. A próbák
// ciklusa (RunMeasurementLoopAsync) háttérszálon fut, és minden nézet- vagy szemkövető-állapotot
// érintő műveletet az IMeasurementView.InvokeAsync-kal a UI-szálra küld.
public sealed class MeasurementPresenter
{
    private static readonly TimeSpan ViewingDistanceMeasurementDuration = TimeSpan.FromSeconds(1);
    private const int MaxStimulusPlacementAttempts = 1000;
    private const float MinStimulusDiameterPx = 4f;

    // Ha a Tobii display area méretaránya ennél jobban eltér a kijelzőétől, a display area
    // valószínűleg egy másik monitorra van beállítva - ilyenkor a fizikai méret sem hiteles.
    private const double MaxDisplayAspectMismatch = 0.05;

    private static readonly string AppVersion =
        typeof(MeasurementPresenter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "ismeretlen";

    private readonly IMeasurementView _view;
    private readonly IMeasurementMode _mode;
    private readonly IGazeTracker _gazeTracker;
    private readonly IResponseDeviceConnector _deviceConnector;
    private readonly IMeasurementRepository _repository;
    private readonly IExperimentSettingsProvider _settingsProvider;
    private readonly IDisplayTimingProvider _displayTimingProvider;
    private readonly bool _manageTrackingLifecycle;
    private readonly Random _random = new();
    private readonly Stopwatch _reactionStopwatch = new();
    private readonly List<MeasurementResult> _currentResults = new();

    private ExperimentSettings _settings;
    private SessionGeometry? _geometry;
    private TrialSchedule? _schedule;
    private IMeasurementSessionWriter? _writer;
    private IResponseDevice? _device;
    private IReadOnlyDictionary<string, string> _sessionMetadata = new Dictionary<string, string>();
    private DateTime _sessionStartedAt;

    private RectangleF _stimulusRect = RectangleF.Empty;
    private bool _isMeasurementRunning;
    private long _stimulusOnsetUs;
    private bool _trackerInitialized;
    private CancellationTokenSource? _measurementCts;
    private Task? _measurementTask;
    private bool _isTransitioning;
    private string _transitionText = string.Empty;
    private string _lastResultText = "-";

    // Az utolsó mérés kimenetele: nyugalmi állapotban ennek megfelelő szöveg jelenik meg az
    // indítási instrukció helyett. Null, ha ebben a nézetben még nem volt mérés.
    private SessionStatus? _lastSessionStatus;
    private int _lastSessionValidTrials;

    public MeasurementPresenter(
        IMeasurementView view,
        IMeasurementMode mode,
        IGazeTracker gazeTracker,
        IResponseDeviceConnector deviceConnector,
        IMeasurementRepository repository,
        IExperimentSettingsProvider settingsProvider,
        IDisplayTimingProvider displayTimingProvider,
        bool manageTrackingLifecycle)
    {
        _view = view;
        _mode = mode;
        _gazeTracker = gazeTracker;
        _deviceConnector = deviceConnector;
        _repository = repository;
        _settingsProvider = settingsProvider;
        _displayTimingProvider = displayTimingProvider;
        _manageTrackingLifecycle = manageTrackingLifecycle;

        // A nyugalmi szövegekhez; egy esetleges hibát a mérés indításakor jelzünk.
        (_settings, _) = settingsProvider.Load();
        Profile = mode.CreateProfile(_settings);

        _view.StartRequested += async (_, _) => await RunSafelyAsync(StartAsync);
        _view.LeaveRequested += async (_, _) => await RunSafelyAsync(LeaveAsync);
        _view.AbortRequested += async (_, _) => await RunSafelyAsync(AbortAsync);
        _view.Closed += (_, _) => _measurementCts?.Cancel();
        RefreshView();
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

    // Minden mérés végén (befejezés, korlát, megszakítás) jelez, a rögzített próbákkal.
    public event EventHandler<MeasurementSessionResultEventArgs>? SessionEnded;

    public MeasurementProfile Profile { get; private set; }

    public bool IsMeasurementRunning => _isMeasurementRunning;

    public async Task StartAsync()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        _transitionText = "Mérés indítása...";
        RefreshView();

        try
        {
            await StartMeasurementCoreAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error($"Mérés indítása közben hiba történt ({Profile.SessionName}).", ex);
            _view.ShowUnexpectedError(ex);
            _isMeasurementRunning = false;
        }
        finally
        {
            _isTransitioning = false;
            RefreshView();
        }
    }

    // A nézet elhagyása: a futó mérést leállítja (az eddigi próbák mentve maradnak).
    public async Task LeaveAsync()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        _transitionText = "Leállítás...";
        if (!_isMeasurementRunning)
        {
            _lastSessionStatus = null;
        }

        RefreshView();
        await StopMeasurementWithErrorHandlingAsync();
    }

    // Megszakítás (Esc): a futó mérést leállítja, a nézet marad.
    public async Task AbortAsync()
    {
        if (!_isMeasurementRunning || _isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        _transitionText = "Leállítás...";
        RefreshView();
        Logger.Info($"Mérés megszakítása a felhasználó kérésére ({Profile.SessionName}).");
        await StopMeasurementWithErrorHandlingAsync();
    }

    private async Task StopMeasurementWithErrorHandlingAsync()
    {
        try
        {
            await StopMeasurementAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"Mérés leállítása közben hiba történt ({Profile.SessionName}).", ex);
            _view.ShowUnexpectedError(ex);
        }
        finally
        {
            _isTransitioning = false;
            RefreshView();
        }
    }

    // A UI-szálon fut: előbb minden feltételt ellenőriz (beállítások, szemkövető, kijelző, ESP,
    // mentési hely), és csak ha mind rendben van, indítja el a próbákat.
    private async Task StartMeasurementCoreAsync()
    {
        await StopMeasurementAsync();

        if (!TryApplySettings() || !await EnsureTrackerReadyAsync())
        {
            return;
        }

        // A követést mindig elindítjuk (ha már fut, pl. dev módban az overlay miatt, ez nem csinál
        // semmit), különben dev módban, ha az overlay indulásakor még nem volt eszköz, egyetlen
        // tekintetminta sem érkezne. Leállítani csak akkor állítjuk le, ha a nézet kezeli az életciklust.
        _gazeTracker.StartTracking();

        _currentResults.Clear();
        _schedule = null;
        _geometry = null;
        _lastResultText = "-";
        _isMeasurementRunning = true;
        _stimulusRect = RectangleF.Empty;
        _stimulusOnsetUs = 0;
        _reactionStopwatch.Reset();
        _view.ShowStimulus(null, paintImmediately: false);
        RefreshView();

        var started = false;
        try
        {
            _geometry = await CaptureSessionGeometryAsync();

            // A mérés ESP nélkül nem indul el: a reakcióidőt a fotodiódás hardveres mérés adja, egy
            // pusztán szoftveres (kattintásos) mérés eredménye ezzel nem lenne összevethető.
            if (!await TryConnectDeviceAsync())
            {
                return;
            }

            _sessionStartedAt = DateTime.Now;
            var metadata = BuildSessionMetadata(_geometry, _device!);
            _sessionMetadata = metadata.ToDictionary(pair => pair.Key, pair => pair.Value);
            try
            {
                _writer = _repository.CreateSession(Profile.SessionName, _sessionStartedAt, metadata);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Error("A mérési fájl nem hozható létre.", ex);
                _view.ShowSaveFailed(_repository.MeasurementsDirectory, ex);
                return;
            }

            _schedule = new TrialSchedule(Profile.Mode);
            _measurementCts = new CancellationTokenSource();
            Logger.Info(
                $"Mérés indul: {Profile.SessionName} ({Profile.Mode.PracticeTrials} gyakorló, {Profile.Mode.TestTrials} érvényes tesztpróba a cél).");
            _measurementTask = RunMeasurementLoopAsync(_measurementCts.Token);
            started = true;
        }
        catch (MeasurementSetupException ex)
        {
            Logger.Warning($"A mérés nem indítható: {ex.Message}");
            _view.ShowSetupError(ex.Message);
        }
        finally
        {
            if (!started)
            {
                await DisposeDeviceAsync();
                if (_manageTrackingLifecycle)
                {
                    _gazeTracker.StopTracking();
                }

                _isMeasurementRunning = false;
                RefreshView();
            }
        }
    }

    private bool TryApplySettings()
    {
        var (settings, error) = _settingsProvider.Load();
        if (error is null)
        {
            var profile = _mode.CreateProfile(settings);
            error = _mode.ValidateProfile(profile);
            if (error is null)
            {
                _settings = settings;
                Profile = profile;
                return true;
            }
        }

        Logger.Warning($"Hibás mérési beállítások: {error}");
        _view.ShowSettingsError(_settingsProvider.SettingsFilePath, error);
        return false;
    }

    private async Task<bool> EnsureTrackerReadyAsync()
    {
        if (!_trackerInitialized || !_gazeTracker.IsTrackerAvailable)
        {
            await _gazeTracker.InitializeAsync(forceRefresh: true);
            _trackerInitialized = true;
        }

        if (_gazeTracker.IsTrackerAvailable)
        {
            return true;
        }

        _view.ShowTrackerNotConnected();
        return false;
    }

    private async Task<bool> TryConnectDeviceAsync()
    {
        try
        {
            _device = await _deviceConnector.ConnectAsync(_settings.Hardware, CancellationToken.None);
            return true;
        }
        catch (ResponseDeviceConnectionException ex)
        {
            Logger.Warning($"[ESP] Kapcsolódás sikertelen ({ex.PortName}): {ex.Message}");
            _view.ShowEspNotConnected(ex.PortName, ex.InnerException ?? ex);
            return false;
        }
    }

    // A kijelző fizikai mérete a Tobii display areából jön (a Windows DPI-je nem a valódi
    // pixelsűrűség), a nézési távolságot a Tobii méri a szem helyzetéből, a képidőzítést pedig a
    // Windows adja meg. Mindez a mérés elején egyszer rögzül, és a metaadatok közé kerül.
    private async Task<SessionGeometry> CaptureSessionGeometryAsync()
    {
        var stage = _view.GetStageInfo();
        var bounds = stage.ScreenBounds;
        var displaySize = await _gazeTracker.RefreshDisplayAreaAsync()
            ?? throw new MeasurementSetupException(
                "A Tobii szemkövetőn nincs beállítva a kijelző (display area), ezért a kijelző fizikai mérete nem ismert. "
                + "Állítsd be a Tobii Pro Eye Tracker Managerben, majd próbáld újra.");

        var screenAspect = bounds.Width / (double)bounds.Height;
        var displayAspect = displaySize.WidthMm / displaySize.HeightMm;
        if (Math.Abs((screenAspect / displayAspect) - 1) > MaxDisplayAspectMismatch)
        {
            throw new MeasurementSetupException(
                $"A Tobii display area ({displaySize.WidthMm:0} × {displaySize.HeightMm:0} mm) méretaránya nem egyezik a mérést futtató "
                + $"kijelzőével ({bounds.Width} × {bounds.Height} képpont). Valószínűleg a display area egy másik monitorra van beállítva, "
                + "vagy az alkalmazás nem azon a monitoron fut, amelyikre a szemkövető be van állítva.");
        }

        var pixelsPerMm = ((bounds.Width / displaySize.WidthMm) + (bounds.Height / displaySize.HeightMm)) / 2;
        var timing = _displayTimingProvider.Read(stage.ScreenDeviceName, bounds);
        if (timing.Warning is not null)
        {
            Logger.Warning($"[SCANOUT] {timing.Warning}");
        }

        var measuredDistanceMm = await _gazeTracker.MeasureViewingDistanceAsync(ViewingDistanceMeasurementDuration);
        var viewingDistanceMm = measuredDistanceMm ?? (_settings.DefaultViewingDistanceCm * 10);
        var viewingDistanceSource = measuredDistanceMm.HasValue ? "Tobii (mért)" : "Beállítás (a Tobii nem tudta mérni)";
        if (!measuredDistanceMm.HasValue)
        {
            Logger.Warning($"A nézési távolság nem mérhető, a beállított {viewingDistanceMm:0} mm-rel számolunk.");
        }

        var visualSpace = new StageVisualSpace(stage.FixationPoint, pixelsPerMm, viewingDistanceMm);
        var effectiveMaxEccentricity = CheckStimulusPlacementFeasibility(visualSpace, stage.StageSize);

        Logger.Info(
            $"Kijelző: {stage.ScreenDeviceName} {bounds.Width}x{bounds.Height} px, {displaySize.WidthMm:0.0}x{displaySize.HeightMm:0.0} mm, "
            + $"{pixelsPerMm:0.###} px/mm, {timing.RefreshRateHz:0.###} Hz ({timing.Source}); nézési távolság {viewingDistanceMm:0} mm ({viewingDistanceSource}).");

        return new SessionGeometry(
            stage,
            displaySize,
            pixelsPerMm,
            viewingDistanceMm,
            viewingDistanceSource,
            timing,
            visualSpace,
            effectiveMaxEccentricity);
    }

    // Ellenőrzi, hogy a beállított excentricitás-tartomány elfér-e a mérőfelületen. A legkisebb
    // értéknek legalább egy irányban el kell férnie; a legnagyobbat, ha sehol nem fér el, a mérés
    // a még elférő értékre csökkenti (és ezt a metaadatokban rögzíti).
    private double CheckStimulusPlacementFeasibility(StageVisualSpace visualSpace, Size stageSize)
    {
        var mode = Profile.Mode;
        if (!CanPlaceStimulusAt(visualSpace, stageSize, mode.MinEccentricityDeg))
        {
            throw new MeasurementSetupException(
                $"A beállított legkisebb excentricitás ({mode.MinEccentricityDeg:0.#}°) ezen a kijelzőn, "
                + $"{visualSpace.ViewingDistanceMm / 10:0} cm nézési távolságból egyik irányban sem fér el. "
                + $"Csökkentsd a MinEccentricityDeg értékét a beállításfájlban ({_settingsProvider.SettingsFilePath}).");
        }

        var effectiveMax = mode.MaxEccentricityDeg;
        while (effectiveMax > mode.MinEccentricityDeg && !CanPlaceStimulusAt(visualSpace, stageSize, effectiveMax))
        {
            effectiveMax = Math.Max(mode.MinEccentricityDeg, effectiveMax - 0.5);
        }

        if (effectiveMax < mode.MaxEccentricityDeg)
        {
            Logger.Warning($"A beállított {mode.MaxEccentricityDeg:0.#}° excentricitás nem fér el, a legnagyobb használt érték {effectiveMax:0.#}°.");
        }

        return effectiveMax;
    }

    private bool CanPlaceStimulusAt(StageVisualSpace visualSpace, Size stageSize, double eccentricityDeg)
    {
        for (var directionDeg = 0; directionDeg < 360; directionDeg++)
        {
            if (TryCreateStimulusRect(visualSpace, stageSize, eccentricityDeg, directionDeg * Math.PI / 180, out _))
            {
                return true;
            }
        }

        return false;
    }

    // Az inger a mérőfelület biztonsági margóján belül marad, és sosem fedi (a margón belül sem
    // közelíti meg) a fotodióda-jelzőt, különben a fotodióda az ingert is érzékelhetné.
    private bool TryCreateStimulusRect(StageVisualSpace visualSpace, Size stageSize, double eccentricityDeg, double directionRad, out RectangleF rect)
    {
        var diameter = StimulusDiameterPx(visualSpace);
        var center = visualSpace.PointAtEccentricity(eccentricityDeg, directionRad);
        rect = new RectangleF(center.X - (diameter / 2f), center.Y - (diameter / 2f), diameter, diameter);

        var margin = Profile.StimulusSafeMarginPx;
        var allowedArea = new RectangleF(margin, margin, stageSize.Width - (2 * margin), stageSize.Height - (2 * margin));
        if (!allowedArea.Contains(rect))
        {
            return false;
        }

        var photodiodeBounds = PhotodiodeMarker.GetBounds(stageSize);
        return photodiodeBounds.IsEmpty || !rect.IntersectsWith(RectangleF.Inflate(photodiodeBounds, margin, margin));
    }

    private float StimulusDiameterPx(StageVisualSpace visualSpace)
    {
        return (float)Math.Max(MinStimulusDiameterPx, visualSpace.SizeDegToPx(Profile.Mode.StimulusDiameterDeg));
    }

    private List<KeyValuePair<string, string>> BuildSessionMetadata(SessionGeometry geometry, IResponseDevice device)
    {
        var mode = Profile.Mode;
        var timing = geometry.Timing;
        var stage = geometry.Stage;
        var photodiode = stage.PhotodiodeBounds;
        var metadata = new List<KeyValuePair<string, string>>();

        void Add(string key, string value) => metadata.Add(new KeyValuePair<string, string>(key, value));
        static string Number(double value, string format = "0.###") => value.ToString(format, CultureInfo.InvariantCulture);
        static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

        Add("AppVersion", AppVersion);
        Add("Tracker", _gazeTracker.TrackerDescription);
        Add("GazeOutputFrequencyHz", _gazeTracker.GazeOutputFrequencyHz.HasValue ? Number(_gazeTracker.GazeOutputFrequencyHz.Value) : string.Empty);
        Add("SerialPort", device.PortName);
        Add("SerialBaudRate", Integer(device.BaudRate));
        Add("ScreenDevice", stage.ScreenDeviceName);
        Add("ScreenResolutionPx", $"{stage.ScreenBounds.Width}x{stage.ScreenBounds.Height}");
        Add("DisplaySizeMm", $"{Number(geometry.DisplaySize.WidthMm, "0.0")}x{Number(geometry.DisplaySize.HeightMm, "0.0")}");
        Add("DisplaySizeSource", "Tobii display area");
        Add("PixelsPerMm", Number(geometry.PixelsPerMm, "0.####"));
        Add("ViewingDistanceMm", Number(geometry.ViewingDistanceMm, "0.#"));
        Add("ViewingDistanceSource", geometry.ViewingDistanceSource);
        Add("RefreshRateHz", Number(timing.RefreshRateHz, "0.####"));
        Add("VerticalActiveLines", Integer(timing.VerticalActiveLines));
        Add("VerticalTotalLines", Integer(timing.VerticalTotalLines));
        Add("DesktopHeightPx", Integer(timing.DesktopHeightPx));
        Add("ScanoutTimingSource", timing.Source);
        Add("ScanoutTimingExact", timing.IsExact.ToString());
        if (timing.Warning is not null)
        {
            Add("ScanoutTimingWarning", timing.Warning);
        }

        Add("StageSizePx", $"{stage.StageSize.Width}x{stage.StageSize.Height}");
        Add("StageScreenOriginPx", $"{stage.StageScreenOrigin.X},{stage.StageScreenOrigin.Y}");
        Add("PhotodiodeMarkerStagePx", $"{photodiode.X},{photodiode.Y},{photodiode.Width},{photodiode.Height}");
        Add("TestTrials", Integer(mode.TestTrials));
        Add("PracticeTrials", Integer(mode.PracticeTrials));
        Add("RepeatInvalidTrials", mode.RepeatInvalidTrials.ToString());
        Add("MaxTestAttempts", Integer(mode.MaxTestAttempts));
        Add("ForeperiodDistribution", mode.ForeperiodDistribution.ToString());
        Add("ForeperiodMinMs", Integer(mode.ForeperiodMinMs));
        Add("ForeperiodMaxMs", Integer(mode.ForeperiodMaxMs));
        Add("ForeperiodExponentialMeanMs", Integer(mode.ForeperiodExponentialMeanMs));
        Add("StimulusDiameterDeg", Number(mode.StimulusDiameterDeg));
        Add("StimulusDiameterPx", Number(StimulusDiameterPx(geometry.VisualSpace), "0.#"));
        Add("MinEccentricityDeg", Number(mode.MinEccentricityDeg));
        Add("MaxEccentricityDeg", Number(mode.MaxEccentricityDeg));
        Add("EffectiveMaxEccentricityDeg", Number(geometry.EffectiveMaxEccentricityDeg));
        Add("GazeValidityRadiusDeg", Number(mode.GazeValidityRadiusDeg));
        Add("MinValidGazeSampleRatio", Number(_settings.MinValidGazeSampleRatio));
        Add("ReactionTimeoutMs", Integer(mode.ReactionTimeoutMs));
        return metadata;
    }

    private async Task RunMeasurementLoopAsync(CancellationToken cancellationToken)
    {
        var schedule = _schedule!;
        var writer = _writer!;
        var status = SessionStatus.Aborted;

        try
        {
            while (!cancellationToken.IsCancellationRequested && schedule.HasNextTrial)
            {
                var phase = schedule.CurrentPhase;
                var trialNumber = schedule.NextTrialNumber;
                var trialLabel = FormatTrialLabel(phase, trialNumber);
                var scanoutParameters = await PrepareScanoutTrialAsync(trialLabel).ConfigureAwait(false);
                var scanoutCorrectionMessage = scanoutParameters.CreateCorrectionMessage();
                Logger.Info($"[SCANOUT] {trialLabel}. próba: correctionUs={scanoutCorrectionMessage}");

                var device = _device;
                if (device is null)
                {
                    break;
                }

                int foreperiodMs;
                lock (_random)
                {
                    foreperiodMs = ForeperiodSampler.Sample(_random, Profile.Mode);
                }

                double? espReactionTimeMs;
                double? localReactionTimeMs;
                long responseWindowEndUs;
                bool espTimedOut;
                try
                {
                    await device.SendScanoutCorrectionAsync(scanoutCorrectionMessage, cancellationToken).ConfigureAwait(false);
                    await ShowStimulusAfterForeperiodAsync(foreperiodMs, cancellationToken).ConfigureAwait(false);
                    var reaction = await WaitForReactionAsync(device, cancellationToken).ConfigureAwait(false);
                    espReactionTimeMs = reaction.EspReactionTimeMs;
                    localReactionTimeMs = reaction.LocalReactionTimeMs;
                    espTimedOut = reaction.TimedOut;
                    responseWindowEndUs = reaction.ResponseReceivedUs;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is IOException)
                {
                    Logger.Warning($"[ESP] Futásidejű hiba: {ex.Message}");
                    await DisposeDeviceAsync().ConfigureAwait(false);
                    await _view.InvokeAsync(() =>
                        _view.ShowEspCommunicationError(device.PortName, device.BaudRate, ex)).ConfigureAwait(false);
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Logger.Info(
                    $"[EREDMÉNY] {trialLabel}. próba: ESP={FormatReaction(espReactionTimeMs)}, PC={FormatReaction(localReactionTimeMs)}, előperiódus={foreperiodMs} ms"
                    + (espTimedOut ? " (ESP időtúllépés, a próba érvénytelen)" : string.Empty));

                var outcome = new TrialOutcome(
                    phase,
                    trialNumber,
                    foreperiodMs,
                    scanoutCorrectionMessage,
                    espReactionTimeMs,
                    localReactionTimeMs,
                    espTimedOut,
                    responseWindowEndUs);
                await _view.InvokeAsync(() => RecordReaction(outcome)).ConfigureAwait(false);
            }

            if (schedule.IsCompleted)
            {
                status = SessionStatus.Completed;
            }
            else if (!cancellationToken.IsCancellationRequested && !schedule.HasNextTrial)
            {
                status = SessionStatus.AttemptLimitReached;
            }

            Logger.Info(
                $"Mérés véget ért ({Profile.SessionName}, {status}): {schedule.ValidTestTrials}/{Profile.Mode.TestTrials} érvényes, "
                + $"{schedule.TestAttempts} tesztpróba, {schedule.PracticeTrialsDone} gyakorló.");
        }
        catch (OperationCanceledException)
        {
            Logger.Info($"Mérés megszakítva ({Profile.SessionName}): {schedule.ValidTestTrials}/{Profile.Mode.TestTrials} érvényes próba után.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Váratlan hiba a mérési ciklusban ({Profile.SessionName}).", ex);
            await _view.InvokeAsync(() => _view.ShowUnexpectedError(ex)).ConfigureAwait(false);
        }
        finally
        {
            await DisposeDeviceAsync().ConfigureAwait(false);

            // Egy megszakított próba félbemaradt tekintetfelvételét el kell dobni, különben
            // (főleg dev módban, ahol a követés a mérés után is fut) tovább gyűlnének a minták.
            _gazeTracker.CancelGazeRecording();

            // A fájlt itt, szálfüggetlenül zárjuk le, hogy akkor is megkapja a lezáró sort, ha a
            // nézet (pl. az alkalmazás bezárása miatt) már nem tud a UI-szálon frissülni.
            var endedAt = DateTime.Now;
            Exception? closeError = null;
            try
            {
                writer.Close(status, endedAt);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Error("A mérési fájl lezárása sikertelen.", ex);
                closeError = ex;
            }

            try
            {
                await _view.InvokeAsync(() => EndSession(status, endedAt, writer.FilePath, closeError)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Pl. az alkalmazás bezárásakor a nézet már megszűnt; a fájl ekkorra le van zárva.
                Logger.Error("A mérés lezárása a felületen nem sikerült.", ex);
            }
        }
    }

    private void EndSession(SessionStatus status, DateTime endedAt, string filePath, Exception? closeError)
    {
        if (_manageTrackingLifecycle)
        {
            _gazeTracker.StopTracking();
        }

        _isMeasurementRunning = false;
        _stimulusRect = RectangleF.Empty;
        _reactionStopwatch.Reset();
        _writer = null;
        _view.ShowStimulus(null, paintImmediately: false);

        var session = new MeasurementSessionResult(Profile.SessionName, _currentResults.ToArray(), _sessionStartedAt, endedAt, status, _sessionMetadata);
        _lastSessionStatus = status;
        _lastSessionValidTrials = session.ValidTrialCount;
        Logger.Info(
            $"Mérés összegzés ({Profile.SessionName}): {session.ValidTrialCount}/{session.TestTrialCount} érvényes tesztpróba, "
            + $"ESP átlag={FormatReaction(session.EspSummary?.MeanMs)}, medián={FormatReaction(session.EspSummary?.MedianMs)}; "
            + $"PC átlag={FormatReaction(session.LocalSummary?.MeanMs)}, medián={FormatReaction(session.LocalSummary?.MedianMs)}.");
        SessionEnded?.Invoke(this, new MeasurementSessionResultEventArgs(session));

        if (closeError is not null)
        {
            _view.ShowSaveFailed(filePath, closeError);
        }

        RefreshView();
    }

    private async Task<ScanoutTrialParameters> PrepareScanoutTrialAsync(string trialLabel)
    {
        ScanoutTrialParameters? parameters = null;
        await _view.InvokeAsync(() =>
        {
            PrepareStimulus();
            var stage = _view.GetStageInfo();
            parameters = ScanoutGeometry.CreateTrialParameters(
                _stimulusRect,
                stage.PhotodiodeBounds,
                stage.StageScreenOrigin,
                stage.ScreenBounds,
                _geometry!.Timing);
        }).ConfigureAwait(false);

        return parameters
            ?? throw new InvalidOperationException($"Nem sikerült előkészíteni a(z) {trialLabel}. próba scanout mérési paramétereit.");
    }

    private async Task ShowStimulusAfterForeperiodAsync(int foreperiodMs, CancellationToken cancellationToken)
    {
        await _view.InvokeAsync(() =>
        {
            _reactionStopwatch.Reset();

            // A tekintet a próba teljes idejére (előperiódus + inger + válasz) rögzítésre kerül;
            // a felvételt a RecordReaction zárja le.
            _gazeTracker.BeginGazeRecording();
            _view.ShowStimulus(null, paintImmediately: false);
        }).ConfigureAwait(false);

        await Task.Delay(foreperiodMs, cancellationToken).ConfigureAwait(false);

        await _view.InvokeAsync(PresentStimulus).ConfigureAwait(false);
    }

    // UI-szálon: az inger és a fotodióda-jelző szinkron kirajzolása, majd rögtön utána az
    // időbélyegek - így azok már a kirajzolás után készülnek.
    private void PresentStimulus()
    {
        if (_stimulusRect.Width <= 0 || _stimulusRect.Height <= 0)
        {
            throw new InvalidOperationException("Az inger pozíciója nincs előkészítve.");
        }

        _view.ShowStimulus(_stimulusRect, paintImmediately: true);
        _stimulusOnsetUs = _gazeTracker.GetSystemTimeStampUs();
        _reactionStopwatch.Restart();
    }

    // Az excentricitás egyenletes eloszlású a [MinEccentricityDeg, elférő maximum] tartományban,
    // az irány véletlen; ami nem fér el a mérőfelületen (vagy a fotodióda-jelzőhöz túl közel esne),
    // azt újrasorsoljuk. A ténylegesen használt excentricitás minden próbánál mentésre kerül.
    private void PrepareStimulus()
    {
        var geometry = _geometry ?? throw new InvalidOperationException("A mérés geometriája nincs előkészítve.");
        var minEccentricity = Profile.Mode.MinEccentricityDeg;

        for (var attempt = 0; attempt < MaxStimulusPlacementAttempts; attempt++)
        {
            double eccentricity;
            double direction;
            lock (_random)
            {
                eccentricity = minEccentricity + (_random.NextDouble() * (geometry.EffectiveMaxEccentricityDeg - minEccentricity));
                direction = _random.NextDouble() * 2 * Math.PI;
            }

            if (TryCreateStimulusRect(geometry.VisualSpace, geometry.Stage.StageSize, eccentricity, direction, out var candidate))
            {
                _stimulusRect = candidate;
                return;
            }
        }

        throw new InvalidOperationException("Nem sikerült az ingert a beállított excentricitás-tartományban elhelyezni.");
    }

    // Ha az ESP a megengedett időn (ReactionTimeoutMs) belül nem küld értelmezhető választ, az nem
    // kezelhető kommunikációs hibaként (nem szakíthatja meg a teljes mérést): egyszerű
    // kimaradt/túl lassú reakció is előidézheti. Ilyenkor a TimedOut jelzővel térünk vissza, a
    // próba érvénytelenként kerül rögzítésre, és a mérés a következő próbával folytatódik.
    // A ResponseReceivedUs a válasz beérkezésének ideje a szemkövető óráján - ez zárja le a
    // tekintet-kiértékelés válaszablakát (időtúllépésnél az időkorlát lejártának ideje).
    private async Task<(double? EspReactionTimeMs, double? LocalReactionTimeMs, bool TimedOut, long ResponseReceivedUs)> WaitForReactionAsync(
        IResponseDevice device,
        CancellationToken cancellationToken)
    {
        var localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;

        try
        {
            var espReactionTimeMs = await device.WaitForReactionTimeMsAsync(
                TimeSpan.FromMilliseconds(Profile.Mode.ReactionTimeoutMs),
                cancellationToken).ConfigureAwait(false);

            localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;
            var responseReceivedUs = _gazeTracker.GetSystemTimeStampUs();
            _reactionStopwatch.Stop();

            if (espReactionTimeMs is null)
            {
                Logger.Warning("[ESP] Nem érkezett reakcióidő-adat a megengedett időn belül, a próba érvénytelen.");
                return (null, localReactionTimeMs, true, responseReceivedUs);
            }

            Logger.Info($"[ESP] Reakcióidő = {espReactionTimeMs:0.###} ms.");
            return (espReactionTimeMs, localReactionTimeMs, false, responseReceivedUs);
        }
        catch (OperationCanceledException)
        {
            _reactionStopwatch.Stop();
            return (null, null, false, 0);
        }
    }

    // UI-szálon fut.
    private void RecordReaction(TrialOutcome outcome)
    {
        var schedule = _schedule;
        var geometry = _geometry;
        if (!_isMeasurementRunning || schedule is null || geometry is null)
        {
            return;
        }

        _reactionStopwatch.Reset();
        _view.ShowStimulus(null, paintImmediately: false);

        var gazeRecording = new TrialGazeRecording(
            _stimulusOnsetUs,
            outcome.ResponseWindowEndUs,
            geometry.Stage.ScreenBounds,
            _view.GetStageInfo().StageScreenOrigin,
            _gazeTracker.EndGazeRecording());
        var invalidReason = DetermineInvalidReason(outcome.TimedOut, gazeRecording, geometry.VisualSpace);
        var stimulusCenter = new PointF(_stimulusRect.X + (_stimulusRect.Width / 2f), _stimulusRect.Y + (_stimulusRect.Height / 2f));

        var result = new MeasurementResult
        {
            Phase = outcome.Phase,
            TrialNumber = outcome.TrialNumber,
            TargetValidTrials = Profile.Mode.TestTrials,
            EspReactionTimeMs = outcome.EspReactionTimeMs,
            LocalReactionTimeMs = outcome.LocalReactionTimeMs,
            RecordedAt = DateTime.Now,
            InvalidReason = invalidReason,
            ForeperiodMs = outcome.ForeperiodMs,
            ScanoutCorrectionUs = outcome.ScanoutCorrectionUs,
            StimulusBounds = _stimulusRect,
            StimulusEccentricityDeg = geometry.VisualSpace.EccentricityDeg(stimulusCenter),
            ValidGazeRatio = gazeRecording.ValidResponseWindowRatio,
            GazeSamplesInWindow = gazeRecording.ResponseWindowSampleCount,
            MeanEyeDistanceMm = gazeRecording.MeanEyeDistanceMm,
            GazeRecording = gazeRecording
        };
        _currentResults.Add(result);
        schedule.Record(outcome.Phase, result.Valid);
        _lastResultText = $"ESP {FormatReaction(result.EspReactionTimeMs)} | PC {FormatReaction(result.LocalReactionTimeMs)}";

        Logger.Info(
            $"[GAZE] {FormatTrialLabel(outcome.Phase, outcome.TrialNumber)}. próba: {gazeRecording.Samples.Count} minta, "
            + $"ebből {gazeRecording.ResponseWindowSampleCount} a válaszablakban ({(outcome.ResponseWindowEndUs - _stimulusOnsetUs) / 1000d:0} ms), "
            + $"érvényes arány {gazeRecording.ValidResponseWindowRatio:P0}, excentricitás {result.StimulusEccentricityDeg:0.0}°, eredmény: {invalidReason}.");

        try
        {
            _writer?.AppendTrial(result);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            Logger.Error("A próba mentése sikertelen, a mérés leáll.", ex);
            _measurementCts?.Cancel();
            _view.ShowSaveFailed(_writer?.FilePath ?? _repository.MeasurementsDirectory, ex);
        }

        RefreshView();
    }

    private TrialInvalidReason DetermineInvalidReason(bool timedOut, TrialGazeRecording gazeRecording, StageVisualSpace visualSpace)
    {
        if (timedOut)
        {
            return TrialInvalidReason.Timeout;
        }

        if (!_gazeTracker.IsTrackerAvailable)
        {
            return TrialInvalidReason.TrackerUnavailable;
        }

        if (gazeRecording.ResponseWindowSampleCount == 0
            || gazeRecording.ValidResponseWindowRatio < _settings.MinValidGazeSampleRatio)
        {
            return TrialInvalidReason.InsufficientGazeData;
        }

        return _mode.IsGazeValid(gazeRecording.ValidResponseWindowPoints, _stimulusRect, visualSpace, Profile.Mode)
            ? TrialInvalidReason.None
            : TrialInvalidReason.GazeViolation;
    }

    private async Task StopMeasurementAsync()
    {
        _measurementCts?.Cancel();

        if (_measurementTask is not null)
        {
            try
            {
                await _measurementTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            _measurementTask = null;
        }

        _measurementCts?.Dispose();
        _measurementCts = null;

        await DisposeDeviceAsync().ConfigureAwait(false);

        _gazeTracker.CancelGazeRecording();

        await _view.InvokeAsync(() =>
        {
            if (_manageTrackingLifecycle)
            {
                _gazeTracker.StopTracking();
            }

            _isMeasurementRunning = false;
            _stimulusRect = RectangleF.Empty;
            _reactionStopwatch.Reset();
            _view.ShowStimulus(null, paintImmediately: false);
            RefreshView();
        }).ConfigureAwait(false);
    }

    private async Task DisposeDeviceAsync()
    {
        var device = Interlocked.Exchange(ref _device, null);
        if (device is not null)
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Nyugalmi állapotban a résztvevő látja az instrukciót (vagy az előző mérés kimenetelét). Mérés
    // közben a szöveg rejtve van és az egérmutató sem látszik: a mérőfelületen csak a fixációs
    // kereszt, az inger és a fotodióda-jelző van, a haladást pedig az alsó sáv mutatja - így
    // semmilyen szövegváltozás nem eshet egybe az inger megjelenésével. UI-szálon hívandó.
    private void RefreshView()
    {
        var progressText = _schedule?.ProgressText ?? $"Érvényes 0/{Profile.Mode.TestTrials}";
        string startButtonText;
        if (_isTransitioning)
        {
            startButtonText = _transitionText;
        }
        else if (_isMeasurementRunning)
        {
            startButtonText = $"Mérés folyamatban – {progressText}";
        }
        else
        {
            startButtonText = _lastSessionStatus is null ? "Mérés indítása" : "Új mérés indítása";
        }

        _view.ApplyState(new MeasurementViewState(
            IsRunning: _isMeasurementRunning,
            IsBusy: _isTransitioning,
            StartButtonText: startButtonText,
            InstructionText: BuildIdleText(),
            ProgressText: progressText,
            LastResultText: _lastResultText,
            FixationCrossSizePx: Profile.FixationCrossSizePx,
            FixationCrossThicknessPx: Profile.FixationCrossThicknessPx));
    }

    private string BuildIdleText()
    {
        var mode = Profile.Mode;
        return _lastSessionStatus switch
        {
            SessionStatus.Completed =>
                $"A mérés elkészült ({_lastSessionValidTrials} érvényes próba). Indíthatsz új mérést, vagy visszaléphetsz a menübe.",
            SessionStatus.AttemptLimitReached =>
                $"A mérés véget ért, de a próbálkozási korlát miatt csak {_lastSessionValidTrials} érvényes próba gyűlt össze a kívánt {mode.TestTrials}-ből. Az adatok elmentve.",
            SessionStatus.Aborted or SessionStatus.Interrupted =>
                $"A mérés megszakadt; az eddigi próbák elmentve ({_lastSessionValidTrials} érvényes). Indíthatsz új mérést.",
            _ => Profile.IdleInstructionText
                + (mode.PracticeTrials > 0 ? $" Először {mode.PracticeTrials} gyakorló próba következik, ezek nem számítanak bele az eredménybe." : string.Empty)
                + " (Megszakítás: Esc)"
        };
    }

    private static string FormatTrialLabel(TrialPhase phase, int trialNumber)
    {
        return phase == TrialPhase.Practice ? $"Gy{trialNumber}" : trialNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatReaction(double? value)
    {
        return value.HasValue ? $"{value.Value:0} ms" : "-";
    }

    // A mérés elején rögzített kijelző- és nézési geometria.
    private sealed record SessionGeometry(
        StageInfo Stage,
        PhysicalDisplaySize DisplaySize,
        double PixelsPerMm,
        double ViewingDistanceMm,
        string ViewingDistanceSource,
        DisplayTiming Timing,
        StageVisualSpace VisualSpace,
        double EffectiveMaxEccentricityDeg);

    private readonly record struct TrialOutcome(
        TrialPhase Phase,
        int TrialNumber,
        int ForeperiodMs,
        string ScanoutCorrectionUs,
        double? EspReactionTimeMs,
        double? LocalReactionTimeMs,
        bool TimedOut,
        long ResponseWindowEndUs);
}
