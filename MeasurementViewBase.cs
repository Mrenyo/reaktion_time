using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

// Egy mérési mód futásidejű profilja: a mód saját (szöveges, megjelenítési) jellemzői és a
// beállításfájlból (ExperimentSettings) betöltött kísérleti paraméterei (Mode).
public sealed record MeasurementProfile(
    string SessionName,
    string IdleInstructionText,
    float FixationCrossSizePx,
    float FixationCrossThicknessPx,
    float StimulusSafeMarginPx,
    ModeSettings Mode);

// Közös alapja a különböző reakcióidő-mérési módoknak (pl. Measure_1 = periférikus látást,
// NormalMeasureView = közvetlen ránézést vizsgáló mérés). Ami korábban a két nézetben szó
// szerint duplikálva volt - a soros port kezelése, az inger megjelenítésének időzítése,
// a próbák számolása, a hibakezelés és a UI-szálra való átváltás -, az mind ide került.
//
// Egy ÚJ mérési mód hozzáadásához (pl. hangra vagy billentyűre reagáló reakcióidő-teszt)
// nincs szükség a fenti logika másolására. Elég egy új, Designer-rel rajzolt UserControl,
// ami ebből az osztályból származik, és megadja:
//   1. a saját MeasurementProfile-ját (a CreateProfile-ban, a beállításfájl alapján),
//   2. a 4 vezérlő-hivatkozást (StagePanel, InstructionLabel, StartMeasureButton, BackButton),
//   3. az IsGazeValidForTrial-ban, hogy az adott mérésnél mikor számít "érvényesnek" egy
//      reakció a szemkövető adata alapján - ez a fő pont, ahol egy új mérés más logikát adhat.
public abstract class MeasurementViewBase : UserControl
{
    private const int SerialBaudRate = 115200;
    private static readonly TimeSpan ViewingDistanceMeasurementDuration = TimeSpan.FromSeconds(1);

    // A fotodióda-jelző egy kis négyzet a mérőfelület (StagePanel) jobb alsó sarkában. Az ingerrel
    // EGYÜTT, ugyanabban a Paint hívásban, ugyanarra a dupla pufferelt felületre rajzoljuk (lásd
    // DrawScene), így a kettő egyetlen képkockában kerül a kijelzőre: a jelző pontosan addig fehér,
    // amíg az inger látszik. Egy erre szerelt fizikai fotodióda (az ESP-n keresztül) ebből méri
    // hardveresen, mikor jelent meg ténylegesen a kép a monitoron - ez a scanout-korrekció alapja,
    // részletesen lásd SCANOUT_PROTOCOL.md.
    private const int PhotodiodeMarkerSizePx = 56;
    private const int PhotodiodeMarkerMarginPx = 20;
    private static readonly Color PhotodiodeIdleColor = Color.Black;
    private static readonly Color PhotodiodeActiveColor = Color.White;
    private static readonly Color PhotodiodeOutlineColor = Color.FromArgb(90, 90, 90);

    private const int MaxStimulusPlacementAttempts = 1000;
    private const float MinStimulusDiameterPx = 4f;

    // Ha a válaszablak (inger megjelenése -> válasz) mintáinak ennél kisebb hányada érvényes
    // (pislogás, elveszett szem, nincs adat), a tekintet alapján nem dönthető el, hogy a résztvevő
    // betartotta-e az utasítást, ezért a próba érvénytelennek számít.
    private const double MinValidGazeSampleRatio = 0.5;

    // Ha a Tobii display area méretaránya ennél jobban eltér a kijelzőétől, a display area
    // valószínűleg egy másik monitorra van beállítva - ilyenkor a fizikai méret sem hiteles.
    private const double MaxDisplayAspectMismatch = 0.05;

    // Mérés közben a mérőfelület fölött nem látszik az egérmutató.
    private static readonly Lazy<Cursor> HiddenCursor = new(CreateHiddenCursor);

    private static readonly string AppVersion =
        typeof(MeasurementViewBase).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "ismeretlen";

    private readonly TobiiService _tobiiService;
    private readonly bool _manageTrackingLifecycle;
    private readonly Random _random = new();
    private readonly Stopwatch _reactionStopwatch = new();
    private readonly List<MeasurementResult> _currentResults = new();

    private ExperimentSettings _settings = new();
    private SessionGeometry? _geometry;
    private TrialSchedule? _schedule;
    private MeasurementSessionWriter? _writer;
    private IReadOnlyDictionary<string, string> _sessionMetadata = new Dictionary<string, string>();
    private DateTime _sessionStartedAt;

    private RectangleF _stimulusRect = RectangleF.Empty;
    private bool _isMeasurementRunning;
    private bool _stimulusVisible;
    private long _stimulusOnsetUs;
    private bool _tobiiInitialized;

    private Serial? _serial;
    private string _serialPortName = "ismeretlen COM-port";
    private CancellationTokenSource? _measurementCts;
    private Task? _measurementTask;
    private bool _isTransitioning;
    private string _transitionText = string.Empty;

    // Az utolsó mérés kimenetele: nyugalmi állapotban ennek megfelelő szöveg jelenik meg az
    // indítási instrukció helyett. Null, ha ebben a nézetben még nem volt mérés.
    private SessionStatus? _lastSessionStatus;
    private int _lastSessionValidTrials;

    public event EventHandler? BackRequested;
    public event EventHandler<MeasurementSessionResultEventArgs>? MeasurementSessionCompleted;

    internal MeasurementViewBase(TobiiService tobiiService, bool manageTrackingLifecycle)
    {
        _tobiiService = tobiiService;
        _manageTrackingLifecycle = manageTrackingLifecycle;
    }

    protected MeasurementProfile Profile { get; private set; } = null!;

    // A konkrét, Designer által generált vezérlők, amikre a lenti közös logikának szüksége van.
    protected abstract Panel StagePanel { get; }
    protected abstract Label InstructionLabel { get; }
    protected abstract Button StartMeasureButton { get; }
    protected abstract Button BackButton { get; }

    // A mód profilja a (minden mérésindításkor újraolvasott) beállításfájl alapján.
    protected abstract MeasurementProfile CreateProfile(ExperimentSettings settings);

    // Mód-specifikus ellenőrzés a beállításokra; null, ha rendben vannak, különben a hibaüzenet.
    protected virtual string? ValidateProfile(MeasurementProfile profile) => null;

    // A fő bővítési pont, ha valamilyen más logika alapján szeretnénk eldönteni, mi számít
    // "sikeres" reakciónak. A bázisosztály a próba teljes idejére rögzíti a tekintetet; ide az
    // inger megjelenése és a válasz beérkezése közötti ÉRVÉNYES minták kerülnek, időrendben, a
    // mérőfelület koordinátáiban (az adatminőség ellenőrzését a bázisosztály már elvégezte). A
    // visualSpace-szel a képpontok látószögre válthatók. Pl. egy periférikus látást vizsgáló
    // mérésnél az érvényes, ha a tekintet végig a középső fixációs kereszten marad, egy normál
    // mérésnél pedig az, ha legalább egyszer az ingerre esik.
    protected abstract bool IsGazeValidForTrial(IReadOnlyList<PointF> validGazePointsOnStage, RectangleF stimulusRect, StageVisualSpace visualSpace);

    // További, nem kötelező bővítési pontok. Alapesetben nem csinálnak semmit; egy adott mérési
    // mód felülírhatja őket, ha extra UI-elemet (pl. egy állapotsávot) akar frissíteni.
    protected virtual void OnUiStateRefreshing()
    {
    }

    protected virtual void OnIdleStateShown()
    {
    }

    protected virtual void OnTrialRecorded(MeasurementResult result)
    {
    }

    // A haladás rövid szöveges formában (pl. "Gyakorlás 2/3", "Érvényes 5/10").
    protected string ProgressText => _schedule?.ProgressText ?? $"Érvényes 0/{Profile.Mode.TestTrials}";

    public bool IsMeasurementRunning => _isMeasurementRunning;

    // Az InitializeComponent() (Designer-generált metódus) UTÁN kell meghívni: a benne lévő
    // hívások a leszármazott osztály StagePanel / InstructionLabel stb. tulajdonságain keresztül
    // a Designer által létrehozott vezérlőket használják. Ha ez a bázisosztály konstruktorából,
    // InitializeComponent() előtt futna le, ezek a vezérlők még null értékűek lennének.
    protected void InitializeMeasurementView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;

        // A mérőfelület dupla pufferelt: a teljes jelenet (háttér, kereszt, inger, fotodióda-jelző)
        // egy memóriabeli pufferbe rajzolódik, és egyetlen másolással kerül a képernyőre, így
        // félkész állapot (pl. inger már igen, jelző még nem) nem kerülhet egy képkockába.
        StagePanel.EnableDoubleBuffering();

        // A nyugalmi szövegekhez; egy esetleges hibát a mérés indításakor jelzünk.
        var (settings, _) = ExperimentSettingsStore.Load();
        _settings = settings;
        Profile = CreateProfile(settings);
        UpdateUiState();
    }

    protected async Task HandleStartMeasureButtonClickAsync()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        _transitionText = "Mérés indítása...";
        UpdateUiState();

        try
        {
            await StartMeasurementAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error($"Mérés indítása közben hiba történt ({Profile.SessionName}).", ex);
            ErrorPopup.ShowGeneral(this, ex);
            _isMeasurementRunning = false;
        }
        finally
        {
            _isTransitioning = false;
            UpdateUiState();
        }
    }

    protected void HandleBackButtonClick()
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

        UpdateUiState();
        BackRequested?.Invoke(this, EventArgs.Empty);
        BeginInvoke(new Action(StopMeasurementAfterNavigationAsync));
    }

    // Az Esc billentyűre (lásd MainMenu): a futó mérést leállítja, az eddigi próbák mentve maradnak.
    internal bool TryAbortMeasurement()
    {
        if (!_isMeasurementRunning || _isTransitioning)
        {
            return false;
        }

        _ = AbortMeasurementAsync();
        return true;
    }

    protected void DrawScene(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(30, 30, 30));

        var center = new PointF(StagePanel.ClientSize.Width / 2f, StagePanel.ClientSize.Height / 2f);
        DrawFixationCross(graphics, center);

        if (_stimulusVisible)
        {
            using var brush = new SolidBrush(Color.White);
            graphics.FillEllipse(brush, _stimulusRect);
        }

        DrawPhotodiodeMarker(graphics);
    }

    protected static string FormatReaction(double? value)
    {
        return value.HasValue ? $"{value.Value:0} ms" : "-";
    }

    private async void StopMeasurementAfterNavigationAsync()
    {
        try
        {
            await StopMeasurementAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error($"Mérés leállítása közben hiba történt ({Profile.SessionName}).", ex);
            ErrorPopup.ShowGeneral(this, ex);
        }
        finally
        {
            _isTransitioning = false;
            UpdateUiState();
        }
    }

    private async Task AbortMeasurementAsync()
    {
        _isTransitioning = true;
        _transitionText = "Leállítás...";
        UpdateUiState();

        try
        {
            Logger.Info($"Mérés megszakítása a felhasználó kérésére ({Profile.SessionName}).");
            await StopMeasurementAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"Mérés leállítása közben hiba történt ({Profile.SessionName}).", ex);
            ErrorPopup.ShowGeneral(this, ex);
        }
        finally
        {
            _isTransitioning = false;
            UpdateUiState();
        }
    }

    // A UI-szálon fut (a hívó await-je után is): előbb minden feltételt ellenőriz (beállítások,
    // szemkövető, kijelző, ESP, mentési hely), és csak ha mind rendben van, indítja el a próbákat.
    private async Task StartMeasurementAsync()
    {
        await StopMeasurementAsync();

        if (!TryApplySettings() || !await EnsureTobiiReadyAsync())
        {
            return;
        }

        // A követést mindig elindítjuk (ha már fut, pl. dev módban az overlay miatt, ez nem csinál
        // semmit), különben dev módban, ha az overlay indulásakor még nem volt eszköz, egyetlen
        // tekintetminta sem érkezne. Leállítani csak akkor állítjuk le, ha a nézet kezeli az életciklust.
        _tobiiService.StartTracking();

        _currentResults.Clear();
        _schedule = null;
        _geometry = null;
        _isMeasurementRunning = true;
        _stimulusVisible = false;
        _stimulusRect = RectangleF.Empty;
        _stimulusOnsetUs = 0;
        _reactionStopwatch.Reset();
        UpdateUiState();

        var started = false;
        try
        {
            _geometry = await CaptureSessionGeometryAsync();

            // A mérés ESP nélkül nem indul el: a reakcióidőt a fotodiódás hardveres mérés adja, egy
            // pusztán szoftveres (kattintásos) mérés eredménye ezzel nem lenne összevethető.
            if (!await TryOpenSerialAsync())
            {
                return;
            }

            _sessionStartedAt = DateTime.Now;
            var metadata = BuildSessionMetadata(_geometry);
            _sessionMetadata = metadata.ToDictionary(pair => pair.Key, pair => pair.Value);
            try
            {
                _writer = MeasurementSessionWriter.Create(Profile.SessionName, _sessionStartedAt, metadata);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Error("A mérési fájl nem hozható létre.", ex);
                ErrorPopup.ShowSaveFailed(this, DataLocations.MeasurementsDirectory, ex);
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
            ErrorPopup.ShowMeasurementSetupError(this, ex.Message);
        }
        finally
        {
            if (!started)
            {
                await DisposeSerialAsync();
                if (_manageTrackingLifecycle)
                {
                    _tobiiService.StopTracking();
                }

                _isMeasurementRunning = false;
                UpdateUiState();
            }
        }
    }

    private bool TryApplySettings()
    {
        var (settings, error) = ExperimentSettingsStore.Load();
        if (error is null)
        {
            var profile = CreateProfile(settings);
            error = ValidateProfile(profile);
            if (error is null)
            {
                _settings = settings;
                Profile = profile;
                return true;
            }
        }

        Logger.Warning($"Hibás mérési beállítások: {error}");
        ErrorPopup.ShowSettingsError(this, DataLocations.SettingsFilePath, error);
        return false;
    }

    private async Task<bool> TryOpenSerialAsync()
    {
        try
        {
            _serialPortName = Serial.GetPreferredEspPortName();
            _serial = new Serial(_serialPortName, SerialBaudRate);
            _serial.MessageReceived += message => Logger.Info($"[SERIAL][IN] '{message}'");
            await _serial.StartAsync(CancellationToken.None);
            Logger.Info($"[SERIAL] Port megnyitva: {_serialPortName} @ {SerialBaudRate}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning($"[SERIAL] Port megnyitása sikertelen: {ex.Message}");
            ErrorPopup.ShowEspNotConnected(this, _serialPortName, ex);
            return false;
        }
    }

    // A kijelző fizikai mérete a Tobii display areából jön (a Windows DPI-je nem a valódi
    // pixelsűrűség), a nézési távolságot a Tobii méri a szem helyzetéből, a képidőzítést pedig a
    // Windows adja meg. Mindez a mérés elején egyszer rögzül, és a metaadatok közé kerül.
    private async Task<SessionGeometry> CaptureSessionGeometryAsync()
    {
        var screen = Screen.FromControl(StagePanel);
        var bounds = screen.Bounds;
        var displayArea = await _tobiiService.RefreshDisplayAreaAsync()
            ?? throw new MeasurementSetupException(
                "A Tobii szemkövetőn nincs beállítva a kijelző (display area), ezért a kijelző fizikai mérete nem ismert. "
                + "Állítsd be a Tobii Pro Eye Tracker Managerben, majd próbáld újra.");

        var screenAspect = bounds.Width / (double)bounds.Height;
        var displayAreaAspect = displayArea.WidthMm / displayArea.HeightMm;
        if (Math.Abs((screenAspect / displayAreaAspect) - 1) > MaxDisplayAspectMismatch)
        {
            throw new MeasurementSetupException(
                $"A Tobii display area ({displayArea.WidthMm:0} × {displayArea.HeightMm:0} mm) méretaránya nem egyezik a mérést futtató "
                + $"kijelzőével ({bounds.Width} × {bounds.Height} képpont). Valószínűleg a display area egy másik monitorra van beállítva, "
                + "vagy az alkalmazás nem azon a monitoron fut, amelyikre a szemkövető be van állítva.");
        }

        var pixelsPerMm = ((bounds.Width / displayArea.WidthMm) + (bounds.Height / displayArea.HeightMm)) / 2;
        var timing = DisplayTimingReader.Read(screen);
        if (timing.Warning is not null)
        {
            Logger.Warning($"[SCANOUT] {timing.Warning}");
        }

        var measuredDistanceMm = await _tobiiService.MeasureViewingDistanceAsync(ViewingDistanceMeasurementDuration);
        var viewingDistanceMm = measuredDistanceMm ?? (_settings.DefaultViewingDistanceCm * 10);
        var viewingDistanceSource = measuredDistanceMm.HasValue ? "Tobii (mért)" : "Beállítás (a Tobii nem tudta mérni)";
        if (!measuredDistanceMm.HasValue)
        {
            Logger.Warning($"A nézési távolság nem mérhető, a beállított {viewingDistanceMm:0} mm-rel számolunk.");
        }

        var stageSize = StagePanel.ClientSize;
        var visualSpace = new StageVisualSpace(new PointF(stageSize.Width / 2f, stageSize.Height / 2f), pixelsPerMm, viewingDistanceMm);
        var effectiveMaxEccentricity = CheckStimulusPlacementFeasibility(visualSpace);

        Logger.Info(
            $"Kijelző: {screen.DeviceName} {bounds.Width}x{bounds.Height} px, {displayArea.WidthMm:0.0}x{displayArea.HeightMm:0.0} mm, "
            + $"{pixelsPerMm:0.###} px/mm, {timing.RefreshRateHz:0.###} Hz ({timing.Source}); nézési távolság {viewingDistanceMm:0} mm ({viewingDistanceSource}).");

        return new SessionGeometry(
            bounds,
            screen.DeviceName,
            displayArea.WidthMm,
            displayArea.HeightMm,
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
    private double CheckStimulusPlacementFeasibility(StageVisualSpace visualSpace)
    {
        var mode = Profile.Mode;
        if (!CanPlaceStimulusAt(visualSpace, mode.MinEccentricityDeg))
        {
            throw new MeasurementSetupException(
                $"A beállított legkisebb excentricitás ({mode.MinEccentricityDeg:0.#}°) ezen a kijelzőn, "
                + $"{visualSpace.ViewingDistanceMm / 10:0} cm nézési távolságból egyik irányban sem fér el. "
                + $"Csökkentsd a MinEccentricityDeg értékét a beállításfájlban ({DataLocations.SettingsFilePath}).");
        }

        var effectiveMax = mode.MaxEccentricityDeg;
        while (effectiveMax > mode.MinEccentricityDeg && !CanPlaceStimulusAt(visualSpace, effectiveMax))
        {
            effectiveMax = Math.Max(mode.MinEccentricityDeg, effectiveMax - 0.5);
        }

        if (effectiveMax < mode.MaxEccentricityDeg)
        {
            Logger.Warning($"A beállított {mode.MaxEccentricityDeg:0.#}° excentricitás nem fér el, a legnagyobb használt érték {effectiveMax:0.#}°.");
        }

        return effectiveMax;
    }

    private bool CanPlaceStimulusAt(StageVisualSpace visualSpace, double eccentricityDeg)
    {
        for (var directionDeg = 0; directionDeg < 360; directionDeg++)
        {
            if (TryCreateStimulusRect(visualSpace, eccentricityDeg, directionDeg * Math.PI / 180, out _))
            {
                return true;
            }
        }

        return false;
    }

    // Az inger a mérőfelület biztonsági margóján belül marad, és sosem fedi (a margón belül sem
    // közelíti meg) a fotodióda-jelzőt, különben a fotodióda az ingert is érzékelhetné.
    private bool TryCreateStimulusRect(StageVisualSpace visualSpace, double eccentricityDeg, double directionRad, out RectangleF rect)
    {
        var diameter = (float)Math.Max(MinStimulusDiameterPx, visualSpace.SizeDegToPx(Profile.Mode.StimulusDiameterDeg));
        var center = visualSpace.PointAtEccentricity(eccentricityDeg, directionRad);
        rect = new RectangleF(center.X - (diameter / 2f), center.Y - (diameter / 2f), diameter, diameter);

        var margin = Profile.StimulusSafeMarginPx;
        var allowedArea = new RectangleF(margin, margin, StagePanel.ClientSize.Width - (2 * margin), StagePanel.ClientSize.Height - (2 * margin));
        if (!allowedArea.Contains(rect))
        {
            return false;
        }

        var photodiodeBounds = GetPhotodiodeBounds();
        return photodiodeBounds.IsEmpty || !rect.IntersectsWith(RectangleF.Inflate(photodiodeBounds, margin, margin));
    }

    private List<KeyValuePair<string, string>> BuildSessionMetadata(SessionGeometry geometry)
    {
        var mode = Profile.Mode;
        var timing = geometry.Timing;
        var photodiode = GetPhotodiodeBounds();
        var stageOrigin = StagePanel.PointToScreen(Point.Empty);
        var metadata = new List<KeyValuePair<string, string>>();

        void Add(string key, string value) => metadata.Add(new KeyValuePair<string, string>(key, value));
        static string Number(double value, string format = "0.###") => value.ToString(format, CultureInfo.InvariantCulture);

        Add("AppVersion", AppVersion);
        Add("Tracker", _tobiiService.TrackerDescription);
        Add("GazeOutputFrequencyHz", _tobiiService.GazeOutputFrequencyHz.HasValue ? Number(_tobiiService.GazeOutputFrequencyHz.Value) : string.Empty);
        Add("SerialPort", _serialPortName);
        Add("ScreenDevice", geometry.ScreenDeviceName);
        Add("ScreenResolutionPx", $"{geometry.ScreenBounds.Width}x{geometry.ScreenBounds.Height}");
        Add("DisplaySizeMm", $"{Number(geometry.DisplayWidthMm, "0.0")}x{Number(geometry.DisplayHeightMm, "0.0")}");
        Add("DisplaySizeSource", "Tobii display area");
        Add("PixelsPerMm", Number(geometry.PixelsPerMm, "0.####"));
        Add("ViewingDistanceMm", Number(geometry.ViewingDistanceMm, "0.#"));
        Add("ViewingDistanceSource", geometry.ViewingDistanceSource);
        Add("RefreshRateHz", Number(timing.RefreshRateHz, "0.####"));
        Add("VerticalActiveLines", timing.VerticalActiveLines.ToString(CultureInfo.InvariantCulture));
        Add("VerticalTotalLines", timing.VerticalTotalLines.ToString(CultureInfo.InvariantCulture));
        Add("DesktopHeightPx", timing.DesktopHeightPx.ToString(CultureInfo.InvariantCulture));
        Add("ScanoutTimingSource", timing.Source);
        Add("ScanoutTimingExact", timing.IsExact.ToString());
        if (timing.Warning is not null)
        {
            Add("ScanoutTimingWarning", timing.Warning);
        }

        Add("StageSizePx", $"{StagePanel.ClientSize.Width}x{StagePanel.ClientSize.Height}");
        Add("StageScreenOriginPx", $"{stageOrigin.X},{stageOrigin.Y}");
        Add("PhotodiodeMarkerStagePx", $"{photodiode.X},{photodiode.Y},{photodiode.Width},{photodiode.Height}");
        Add("TestTrials", mode.TestTrials.ToString(CultureInfo.InvariantCulture));
        Add("PracticeTrials", mode.PracticeTrials.ToString(CultureInfo.InvariantCulture));
        Add("RepeatInvalidTrials", mode.RepeatInvalidTrials.ToString());
        Add("MaxTestAttempts", mode.MaxTestAttempts.ToString(CultureInfo.InvariantCulture));
        Add("ForeperiodDistribution", mode.ForeperiodDistribution.ToString());
        Add("ForeperiodMinMs", mode.ForeperiodMinMs.ToString(CultureInfo.InvariantCulture));
        Add("ForeperiodMaxMs", mode.ForeperiodMaxMs.ToString(CultureInfo.InvariantCulture));
        Add("ForeperiodExponentialMeanMs", mode.ForeperiodExponentialMeanMs.ToString(CultureInfo.InvariantCulture));
        Add("StimulusDiameterDeg", Number(mode.StimulusDiameterDeg));
        Add("StimulusDiameterPx", Number(Math.Max(MinStimulusDiameterPx, geometry.VisualSpace.SizeDegToPx(mode.StimulusDiameterDeg)), "0.#"));
        Add("MinEccentricityDeg", Number(mode.MinEccentricityDeg));
        Add("MaxEccentricityDeg", Number(mode.MaxEccentricityDeg));
        Add("EffectiveMaxEccentricityDeg", Number(geometry.EffectiveMaxEccentricityDeg));
        Add("GazeValidityRadiusDeg", Number(mode.GazeValidityRadiusDeg));
        Add("MinValidGazeSampleRatio", Number(MinValidGazeSampleRatio));
        Add("ReactionTimeoutMs", mode.ReactionTimeoutMs.ToString(CultureInfo.InvariantCulture));
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

                var serial = _serial;
                if (serial is null)
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
                    await serial.SendAsync(scanoutCorrectionMessage, cancellationToken).ConfigureAwait(false);
                    await ShowStimulusAfterForeperiodAsync(foreperiodMs, cancellationToken).ConfigureAwait(false);
                    var espOutcome = await WaitForEspReactionAsync(serial, cancellationToken).ConfigureAwait(false);
                    espReactionTimeMs = espOutcome.EspReactionTimeMs;
                    localReactionTimeMs = espOutcome.LocalReactionTimeMs;
                    espTimedOut = espOutcome.TimedOut;
                    responseWindowEndUs = espOutcome.ResponseReceivedUs;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is IOException)
                {
                    Logger.Warning($"[SERIAL] Futásidejű hiba: {ex.Message}");
                    await DisposeSerialAsync().ConfigureAwait(false);
                    await RunOnUiThreadAsync(() =>
                        ErrorPopup.ShowEspCommunicationError(this, _serialPortName, SerialBaudRate, ex)).ConfigureAwait(false);
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
                await RunOnUiThreadAsync(() => RecordReaction(outcome)).ConfigureAwait(false);
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
            await RunOnUiThreadAsync(() => ErrorPopup.ShowGeneral(this, ex)).ConfigureAwait(false);
        }
        finally
        {
            await DisposeSerialAsync().ConfigureAwait(false);

            // Egy megszakított próba félbemaradt tekintetfelvételét el kell dobni, különben
            // (főleg dev módban, ahol a követés a mérés után is fut) tovább gyűlnének a minták.
            _tobiiService.CancelGazeRecording();

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

            await RunOnUiThreadAsync(() => EndSession(status, endedAt, writer.FilePath, closeError)).ConfigureAwait(false);
        }
    }

    private void EndSession(SessionStatus status, DateTime endedAt, string filePath, Exception? closeError)
    {
        if (_manageTrackingLifecycle)
        {
            _tobiiService.StopTracking();
        }

        _isMeasurementRunning = false;
        _stimulusVisible = false;
        _stimulusRect = RectangleF.Empty;
        _reactionStopwatch.Reset();
        _writer = null;

        var session = new MeasurementSessionResult(Profile.SessionName, _currentResults.ToArray(), _sessionStartedAt, endedAt, status, _sessionMetadata);
        _lastSessionStatus = status;
        _lastSessionValidTrials = session.ValidTrialCount;
        Logger.Info(
            $"Mérés összegzés ({Profile.SessionName}): {session.ValidTrialCount}/{session.TestTrialCount} érvényes tesztpróba, "
            + $"ESP átlag={FormatReaction(session.EspSummary?.MeanMs)}, medián={FormatReaction(session.EspSummary?.MedianMs)}; "
            + $"PC átlag={FormatReaction(session.LocalSummary?.MeanMs)}, medián={FormatReaction(session.LocalSummary?.MedianMs)}.");
        MeasurementSessionCompleted?.Invoke(this, new MeasurementSessionResultEventArgs(session));

        if (closeError is not null)
        {
            ErrorPopup.ShowSaveFailed(this, filePath, closeError);
        }

        UpdateUiState();
    }

    private async Task<ScanoutTrialParameters> PrepareScanoutTrialAsync(string trialLabel)
    {
        ScanoutTrialParameters? parameters = null;
        await RunOnUiThreadAsync(() =>
        {
            PrepareStimulus();
            parameters = DisplayScanoutContext.Capture(StagePanel, _stimulusRect, GetPhotodiodeBounds(), _geometry!.Timing);
            StagePanel.Invalidate();
        }).ConfigureAwait(false);

        return parameters
            ?? throw new InvalidOperationException($"Nem sikerült előkészíteni a(z) {trialLabel}. próba scanout mérési paramétereit.");
    }

    private async Task ShowStimulusAfterForeperiodAsync(int foreperiodMs, CancellationToken cancellationToken)
    {
        await RunOnUiThreadAsync(() =>
        {
            _stimulusVisible = false;
            _reactionStopwatch.Reset();

            // A tekintet a próba teljes idejére (előperiódus + inger + válasz) rögzítésre kerül;
            // a felvételt a RecordReaction zárja le.
            _tobiiService.BeginGazeRecording();
            StagePanel.Invalidate();
        }).ConfigureAwait(false);

        await Task.Delay(foreperiodMs, cancellationToken).ConfigureAwait(false);

        await RunOnUiThreadAsync(ShowStimulus).ConfigureAwait(false);
    }

    private void ShowStimulus()
    {
        if (_stimulusRect.Width <= 0 || _stimulusRect.Height <= 0)
        {
            throw new InvalidOperationException("Az inger pozíciója nincs előkészítve.");
        }

        _stimulusVisible = true;

        // Az inger és a fotodióda-jelző ugyanabban a Paint hívásban rajzolódik ki. Az Update()
        // azonnal, szinkron módon lefuttatja ezt a rajzolást (nem várja meg, hogy a WM_PAINT
        // sorra kerüljön az üzenetsorban), így az alábbi időbélyegek már a kirajzolás után készülnek.
        StagePanel.Invalidate();
        StagePanel.Update();

        _stimulusOnsetUs = _tobiiService.GetSystemTimeStampUs();
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

            if (TryCreateStimulusRect(geometry.VisualSpace, eccentricity, direction, out var candidate))
            {
                _stimulusRect = candidate;
                _stimulusVisible = false;
                return;
            }
        }

        throw new InvalidOperationException("Nem sikerült az ingert a beállított excentricitás-tartományban elhelyezni.");
    }

    // Ha az ESP a megengedett időn (ReactionTimeoutMs) belül nem küld értelmezhető választ, az nem
    // kezelhető soros kommunikációs hibaként (nem szakíthatja meg a teljes mérést): egyszerű
    // kimaradt/túl lassú reakció is előidézheti. Ilyenkor a TimedOut jelzővel térünk vissza, a
    // próba érvénytelenként kerül rögzítésre, és a mérés a következő próbával folytatódik.
    // A ResponseReceivedUs a válasz beérkezésének ideje a Tobii rendszerórán - ez zárja le a
    // tekintet-kiértékelés válaszablakát (időtúllépésnél az időkorlát lejártának ideje).
    private async Task<(double? EspReactionTimeMs, double? LocalReactionTimeMs, bool TimedOut, long ResponseReceivedUs)> WaitForEspReactionAsync(
        Serial serial,
        CancellationToken cancellationToken)
    {
        var localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;

        try
        {
            // A WaitForResponseAsync csak értelmezhető reakcióidő-sort fogad el, időtúllépéskor
            // pedig üres szöveggel tér vissza - tehát ha az alábbi értelmezés nem sikerül, az
            // mindig időtúllépést jelent.
            var reactionResponse = await serial.WaitForResponseAsync(
                message => ScanoutProtocol.TryParseReactionTime(message, out _),
                TimeSpan.FromMilliseconds(Profile.Mode.ReactionTimeoutMs),
                cancellationToken).ConfigureAwait(false);

            localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;
            var responseReceivedUs = _tobiiService.GetSystemTimeStampUs();
            _reactionStopwatch.Stop();

            if (!ScanoutProtocol.TryParseReactionTime(reactionResponse, out var espReactionTimeMs))
            {
                Logger.Warning("[SERIAL] Az ESP nem küldött reakcióidő-adatot a megengedett időn belül, a próba érvénytelen.");
                return (null, localReactionTimeMs, true, responseReceivedUs);
            }

            Logger.Info($"[SERIAL][PARSED] ESP reakció = {espReactionTimeMs:0.###} ms (nyers: '{reactionResponse}').");
            return (espReactionTimeMs, localReactionTimeMs, false, responseReceivedUs);
        }
        catch (OperationCanceledException)
        {
            _reactionStopwatch.Stop();
            return (null, null, false, 0);
        }
    }

    private void RecordReaction(TrialOutcome outcome)
    {
        var schedule = _schedule;
        var geometry = _geometry;
        if (!_isMeasurementRunning || schedule is null || geometry is null)
        {
            return;
        }

        _reactionStopwatch.Reset();
        _stimulusVisible = false;

        var gazeRecording = new TrialGazeRecording(
            _stimulusOnsetUs,
            outcome.ResponseWindowEndUs,
            geometry.ScreenBounds,
            StagePanel.PointToScreen(Point.Empty),
            _tobiiService.EndGazeRecording());
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
            ErrorPopup.ShowSaveFailed(this, _writer?.FilePath ?? DataLocations.MeasurementsDirectory, ex);
        }

        OnTrialRecorded(result);
        UpdateUiState();
    }

    private TrialInvalidReason DetermineInvalidReason(bool timedOut, TrialGazeRecording gazeRecording, StageVisualSpace visualSpace)
    {
        if (timedOut)
        {
            return TrialInvalidReason.Timeout;
        }

        if (!_tobiiService.IsTrackerAvailable)
        {
            return TrialInvalidReason.TrackerUnavailable;
        }

        if (gazeRecording.ResponseWindowSampleCount == 0
            || gazeRecording.ValidResponseWindowRatio < MinValidGazeSampleRatio)
        {
            return TrialInvalidReason.InsufficientGazeData;
        }

        return IsGazeValidForTrial(gazeRecording.ValidResponseWindowPoints, _stimulusRect, visualSpace)
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

        await DisposeSerialAsync().ConfigureAwait(false);

        _tobiiService.CancelGazeRecording();

        await RunOnUiThreadAsync(() =>
        {
            if (_manageTrackingLifecycle)
            {
                _tobiiService.StopTracking();
            }

            _isMeasurementRunning = false;
            _stimulusVisible = false;
            _stimulusRect = RectangleF.Empty;
            _reactionStopwatch.Reset();
            UpdateUiState();
            StagePanel.Invalidate();
        }).ConfigureAwait(false);
    }

    private async Task DisposeSerialAsync()
    {
        var serial = Interlocked.Exchange(ref _serial, null);
        if (serial is null)
        {
            return;
        }

        try
        {
            await serial.StopAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        await serial.DisposeAsync().ConfigureAwait(false);
    }

    // Nyugalmi állapotban a résztvevő látja az instrukciót (vagy az előző mérés kimenetelét). Mérés
    // közben a szöveg rejtve van és az egérmutató sem látszik: a mérőfelületen csak a fixációs
    // kereszt, az inger és a fotodióda-jelző van, a haladást pedig az alsó sáv gombja mutatja - így
    // semmilyen szövegváltozás nem eshet egybe az inger megjelenésével.
    private void UpdateUiState()
    {
        OnUiStateRefreshing();
        BackButton.Enabled = !_isTransitioning;
        StagePanel.Cursor = _isMeasurementRunning ? HiddenCursor.Value : Cursors.Default;

        if (!_isMeasurementRunning)
        {
            StartMeasureButton.Enabled = !_isTransitioning;
            StartMeasureButton.Text = _lastSessionStatus is null ? "Mérés indítása" : "Új mérés indítása";
            InstructionLabel.Text = BuildIdleText();
            InstructionLabel.Visible = true;
            OnIdleStateShown();
        }
        else
        {
            InstructionLabel.Visible = false;
            StartMeasureButton.Enabled = false;
            StartMeasureButton.Text = _isTransitioning ? _transitionText : $"Mérés folyamatban – {ProgressText}";
        }

        StagePanel.Invalidate();
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

    private void DrawFixationCross(Graphics graphics, PointF center)
    {
        using var pen = new Pen(Color.White, Profile.FixationCrossThicknessPx)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var half = Profile.FixationCrossSizePx / 2f;
        graphics.DrawLine(pen, center.X - half, center.Y, center.X + half, center.Y);
        graphics.DrawLine(pen, center.X, center.Y - half, center.X, center.Y + half);
    }

    private async Task<bool> EnsureTobiiReadyAsync()
    {
        if (!_tobiiInitialized || !_tobiiService.IsTrackerAvailable)
        {
            await _tobiiService.InitializeAsync(forceRefresh: true);
            _tobiiInitialized = true;
        }

        if (_tobiiService.IsTrackerAvailable)
        {
            return true;
        }

        ErrorPopup.ShowTobiiNotConnected(this);
        return false;
    }

    // A fotodióda-jelző helye a mérőfelület koordinátáiban (jobb alsó sarok). Üres, ha a felület
    // túl kicsi hozzá.
    private Rectangle GetPhotodiodeBounds()
    {
        var x = StagePanel.ClientSize.Width - PhotodiodeMarkerSizePx - PhotodiodeMarkerMarginPx;
        var y = StagePanel.ClientSize.Height - PhotodiodeMarkerSizePx - PhotodiodeMarkerMarginPx;
        if (x < 0 || y < 0)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(x, y, PhotodiodeMarkerSizePx, PhotodiodeMarkerSizePx);
    }

    // A jelző színét ugyanaz az állapot (_stimulusVisible) vezérli, mint az inger kirajzolását,
    // így a kettő nem kerülhet el egymástól.
    private void DrawPhotodiodeMarker(Graphics graphics)
    {
        var bounds = GetPhotodiodeBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        var previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;

        using var brush = new SolidBrush(_stimulusVisible ? PhotodiodeActiveColor : PhotodiodeIdleColor);
        graphics.FillRectangle(brush, bounds);

        // A vékony keret csak a fizikai szenzor felhelyezését segíti; a jelzőn kívül esik, és
        // sosem változik, ezért a fotodióda jelét nem befolyásolja.
        using var outlinePen = new Pen(PhotodiodeOutlineColor);
        graphics.DrawRectangle(outlinePen, bounds.X - 1, bounds.Y - 1, bounds.Width + 1, bounds.Height + 1);

        graphics.SmoothingMode = previousSmoothingMode;
    }

    private static Cursor CreateHiddenCursor()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        return new Cursor(bitmap.GetHicon());
    }

    // A háttérszálon futó mérési ciklus (RunMeasurementLoopAsync) innen tudja biztonságosan
    // módosítani a UI vezérlőket: WinForms-ban vezérlőt csak a létrehozó (UI) szálról szabad
    // módosítani, ezt biztosítja itt az InvokeRequired + BeginInvoke páros.
    private Task RunOnUiThreadAsync(Action action)
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        if (!InvokeRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke(new Action(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }));

        return tcs.Task;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _measurementCts?.Cancel();
        base.OnHandleDestroyed(e);
    }

    // A mérés elején rögzített kijelző- és nézési geometria.
    private sealed record SessionGeometry(
        Rectangle ScreenBounds,
        string ScreenDeviceName,
        double DisplayWidthMm,
        double DisplayHeightMm,
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
