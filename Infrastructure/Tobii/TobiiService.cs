using System.Numerics;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;
using Tobii.Research;

namespace ReactionTimeMeasurementApp.Infrastructure.Tobii;

// A Tobii-ban (Eye Tracker Manager) beállított kijelző (display area) fizikai mérete és síkja a
// szemkövető koordináta-rendszerében (mm). A méret a mérés metaadataiba kerül, a síkhoz mérve pedig
// minden tekintetmintához kiszámolható a szem merőleges távolsága a kijelzőtől (GazeSample.EyeDistanceMm).
internal sealed class DisplayAreaGeometry
{
    private readonly Vector3 _topLeft;
    private readonly Vector3 _normal;

    public DisplayAreaGeometry(DisplayArea displayArea)
    {
        WidthMm = displayArea.Width;
        HeightMm = displayArea.Height;
        _topLeft = ToVector(displayArea.TopLeft);
        var right = ToVector(displayArea.TopRight) - _topLeft;
        var down = ToVector(displayArea.BottomLeft) - _topLeft;
        _normal = Vector3.Normalize(Vector3.Cross(right, down));
    }

    public double WidthMm { get; }

    public double HeightMm { get; }

    public bool IsValid => WidthMm > 0 && HeightMm > 0 && float.IsFinite(_normal.X);

    public double DistanceToPlaneMm(Point3D point)
    {
        return Math.Abs(Vector3.Dot(_normal, ToVector(point) - _topLeft));
    }

    private static Vector3 ToVector(Point3D point) => new(point.X, point.Y, point.Z);
}

// A Tobii szemkövető kezelése. Az eszköz állapotát módosító metódusokat (InitializeAsync,
// StartTracking, StopTracking, CheckExistingCalibrationAsync, Dispose) a UI-szálról kell hívni:
// az await-ek után a folytatás is ott fut, így a "_tracker" mezőt mindig ugyanaz a szál írja.
//
// A natív SDK-hívások (keresés, lekérdezések) háttérszálon futnak (RunNativeAsync); a Dispose a
// Terminate előtt megvárja őket, mert a futó hívás alól lezárt SDK natív összeomlást okozhat.
internal sealed class TobiiService : IGazeTracker, ICalibrationService, IDisposable
{
    private const int InitialGazeRecordingCapacity = 1024;
    private static readonly TimeSpan CalibrationGazeCheckDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NativeWorkShutdownTimeout = TimeSpan.FromSeconds(5);
    private const double MinValidSampleRatio = 0.5;

    private readonly object _gazeRecordingLock = new();
    private readonly object _lastGazeLock = new();
    private readonly object _nativeWorkLock = new();
    private IEyeTracker? _tracker;
    private Task? _initializeTask;
    private Task<TrackerSearchResult?>? _pendingSearch;
    private Task _nativeWork = Task.CompletedTask;
    private bool _isTracking;
    private bool _disposed;
    private PointF _lastNormalizedGaze = new(float.NaN, float.NaN);
    private List<GazeSample>? _gazeRecording;

    // A Tobii saját szálán futó GazeDataReceived is olvassa, ezért volatile.
    private volatile DisplayAreaGeometry? _displayArea;

    // A háttérszálon talált eszköz és az ott lekérdezett adatai; a UI-szálon futó folytatás veszi át.
    private sealed record TrackerSearchResult(IEyeTracker Tracker, double? GazeOutputFrequencyHz, DisplayAreaGeometry? DisplayArea);

    public event EventHandler<TrackerConnectionChangedEventArgs>? ConnectionChanged;

    public bool IsTrackerAvailable => _tracker != null;

    public string TrackerDescription => _tracker is null ? "nincs" : $"{_tracker.Model} (sorozatszám: {_tracker.SerialNumber})";

    public double? GazeOutputFrequencyHz { get; private set; }

    // Ugyanaz az óra, amin a GazeDataEventArgs.SystemTimeStamp is értendő (µs), ezért az így
    // lekért időpontok (inger megjelenése, válasz beérkezése) közvetlenül összevethetők a
    // tekintetminták időbélyegeivel.
    public long GetSystemTimeStampUs() => EyeTrackingOperations.GetSystemTimeStamp();

    // Elindít egy új felvételt: innentől minden beérkező minta (az érvénytelenek is) bekerül,
    // amíg az EndGazeRecording vagy a CancelGazeRecording le nem zárja. Egy korábbi, le nem zárt
    // felvételt eldob.
    public void BeginGazeRecording()
    {
        lock (_gazeRecordingLock)
        {
            _gazeRecording = new List<GazeSample>(InitialGazeRecordingCapacity);
        }
    }

    public IReadOnlyList<GazeSample> EndGazeRecording()
    {
        lock (_gazeRecordingLock)
        {
            var recording = _gazeRecording;
            _gazeRecording = null;
            return recording ?? (IReadOnlyList<GazeSample>)[];
        }
    }

    public void CancelGazeRecording()
    {
        lock (_gazeRecordingLock)
        {
            _gazeRecording = null;
        }
    }

    // Az eszközkeresés (FindAllEyeTrackers) akár másodpercekig is eltarthat, ezért aszinkron fut,
    // hogy ne fagyassza le a felületet. Az egyszerre érkező kéréseket egyetlen keresés szolgálja ki.
    public Task InitializeAsync(bool forceRefresh = false)
    {
        if (_disposed || _isTracking || (_tracker != null && !forceRefresh))
        {
            return Task.CompletedTask;
        }

        if (_initializeTask is { IsCompleted: false })
        {
            return _initializeTask;
        }

        _initializeTask = InitializeCoreAsync();
        return _initializeTask;
    }

    private async Task InitializeCoreAsync()
    {
        var search = RunNativeAsync(SearchTracker);
        _pendingSearch = search;

        TrackerSearchResult? result;
        try
        {
            result = await search;
        }
        catch (Exception e)
        {
            Logger.Error("Tobii inicializálási hiba.", e);
            return;
        }

        // Leállítás után a talált példányhoz már nem nyúlunk: a Dispose a Terminate előtt felszabadítja.
        if (_disposed)
        {
            return;
        }

        _pendingSearch = null;

        // Ha a keresés alatt elindult a követés, a meglévő eszközpéldány marad érvényben, az újonnan
        // talált felesleges.
        if (_isTracking)
        {
            result?.Tracker.Dispose();
            return;
        }

        ReplaceTracker(result?.Tracker);
        GazeOutputFrequencyHz = result?.GazeOutputFrequencyHz;
        _displayArea = result?.DisplayArea;
    }

    // Háttérszálon fut: csak a talált eszközt kérdezi le, a szolgáltatás mezőihez nem nyúl.
    private static TrackerSearchResult? SearchTracker()
    {
        var foundTrackers = EyeTrackingOperations.FindAllEyeTrackers().ToList();
        foreach (var unused in foundTrackers.Skip(1))
        {
            unused.Dispose();
        }

        var found = foundTrackers.FirstOrDefault();
        if (found == null)
        {
            Logger.Warning("Tobii eszköz nem csatlakozik.");
            return null;
        }

        Logger.Info($"Tobii eszköz megtalálva: {found.Model}");
        return new TrackerSearchResult(found, TryReadGazeOutputFrequency(found), TryReadDisplayArea(found));
    }

    // A display area az Eye Tracker Managerben az alkalmazás futása közben is módosulhat, ezért a
    // mérés minden indításkor újra lekérdezi.
    public async Task<PhysicalDisplaySize?> RefreshDisplayAreaAsync()
    {
        var eyeTracker = _tracker;
        var displayArea = eyeTracker is null ? null : await RunNativeAsync(() => TryReadDisplayArea(eyeTracker));
        _displayArea = displayArea;
        return displayArea is null ? null : new PhysicalDisplaySize(displayArea.WidthMm, displayArea.HeightMm);
    }

    private static DisplayAreaGeometry? TryReadDisplayArea(IEyeTracker eyeTracker)
    {
        try
        {
            var geometry = new DisplayAreaGeometry(eyeTracker.GetDisplayArea());
            if (!geometry.IsValid)
            {
                return null;
            }

            Logger.Info($"Tobii display area: {geometry.WidthMm:0.0} x {geometry.HeightMm:0.0} mm.");
            return geometry;
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii display area nem kérdezhető le: {e.Message}");
            return null;
        }
    }

    private static double? TryReadGazeOutputFrequency(IEyeTracker eyeTracker)
    {
        try
        {
            return eyeTracker.GetGazeOutputFrequency();
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii mintavételi frekvenciája nem kérdezhető le: {e.Message}");
            return null;
        }
    }

    // A natív hívást háttérszálon futtatja, és nyilvántartja, hogy a Dispose megvárhassa.
    private Task<T> RunNativeAsync<T>(Func<T> work)
    {
        var task = Task.Run(work);
        lock (_nativeWorkLock)
        {
            _nativeWork = _nativeWork.IsCompleted ? task : Task.WhenAll(_nativeWork, task);
        }

        return task;
    }

    // A lecserélt eszközpéldányt fel kell szabadítani, különben a natív kapcsolata nyitva marad.
    private void ReplaceTracker(IEyeTracker? newTracker)
    {
        var previous = _tracker;
        if (ReferenceEquals(previous, newTracker))
        {
            return;
        }

        _tracker = newTracker;
        if (previous != null)
        {
            SetConnectionEvents(previous, subscribe: false);
            previous.Dispose();
        }

        if (newTracker != null)
        {
            SetConnectionEvents(newTracker, subscribe: true);
        }
    }

    private void SetConnectionEvents(IEyeTracker eyeTracker, bool subscribe)
    {
        try
        {
            if (subscribe)
            {
                eyeTracker.ConnectionLost += Tracker_ConnectionLost;
                eyeTracker.ConnectionRestored += Tracker_ConnectionRestored;
                eyeTracker.EventErrorOccurred += Tracker_EventErrorOccurred;
            }
            else
            {
                eyeTracker.ConnectionLost -= Tracker_ConnectionLost;
                eyeTracker.ConnectionRestored -= Tracker_ConnectionRestored;
                eyeTracker.EventErrorOccurred -= Tracker_EventErrorOccurred;
            }
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii kapcsolati eseményeire nem sikerült {(subscribe ? "feliratkozni" : "leiratkozni")}: {e.Message}");
        }
    }

    // A kapcsolati események a Tobii SDK saját szálán érkeznek; a UI-szálra a ConnectionChanged
    // feliratkozója továbbítja őket.
    private void Tracker_ConnectionLost(object? sender, ConnectionLostEventArgs e)
    {
        Logger.Warning("A Tobii szemkövető kapcsolata megszakadt.");

        // Az utolsó tekintetpont innentől elavult, nem szabad tovább érvényesnek mutatni.
        lock (_lastGazeLock)
        {
            _lastNormalizedGaze = new PointF(float.NaN, float.NaN);
        }

        ConnectionChanged?.Invoke(this, new TrackerConnectionChangedEventArgs(false));
    }

    private void Tracker_ConnectionRestored(object? sender, ConnectionRestoredEventArgs e)
    {
        Logger.Info("A Tobii szemkövető kapcsolata helyreállt.");
        ConnectionChanged?.Invoke(this, new TrackerConnectionChangedEventArgs(true));
    }

    private void Tracker_EventErrorOccurred(object? sender, EventErrorEventArgs e)
    {
        Logger.Warning($"Tobii eseményhiba ({e.Source}, {e.ErrorType}): {e.Message}");
    }

    public void StartTracking()
    {
        if (_isTracking || _tracker == null)
        {
            return;
        }

        _tracker.GazeDataReceived += Tracker_GazeDataReceived;
        _isTracking = true;
    }

    public void StopTracking()
    {
        if (!_isTracking || _tracker == null)
        {
            return;
        }

        _tracker.GazeDataReceived -= Tracker_GazeDataReceived;
        _isTracking = false;
    }

    // Háttérszálról (Task.Run) is hívható: csak olvassa a "_tracker" mezőt, nem cseréli le. Előtte
    // az InitializeAsync-kal kell gondoskodni róla, hogy a legfrissebb eszközpéldány legyen meg.
    public int RunUserCalibration()
    {
        var eyeTracker = _tracker;
        if (eyeTracker == null)
        {
            Logger.Warning("Kalibráció kihagyva: Tobii eszköz nem csatlakozik.");
            return -1;
        }

        return TobiiCalibrationLauncher.RunUserCalibration(eyeTracker);
    }

    // A "meglévő kalibráció" használata előtt ellenőrzi, hogy (1) csatlakozik-e szemkövető,
    // (2) van-e rajta alkalmazott kalibráció, és (3) érkezik-e tőle ténylegesen érvényes
    // tekintetadat, vagyis a résztvevő a szemkövető előtt ül, és az eszköz követi a szemét.
    public async Task<(CalibrationCheckResult Result, double ValidGazeRatio)> CheckExistingCalibrationAsync()
    {
        await InitializeAsync(forceRefresh: true);

        var eyeTracker = _tracker;
        if (eyeTracker == null)
        {
            return (CalibrationCheckResult.TrackerNotFound, 0);
        }

        if (!await HasCalibrationDataAsync(eyeTracker))
        {
            return (CalibrationCheckResult.NoCalibrationData, 0);
        }

        var startedTracking = !_isTracking;
        StartTracking();
        BeginGazeRecording();

        IReadOnlyList<GazeSample> samples;
        try
        {
            await Task.Delay(CalibrationGazeCheckDuration);
        }
        finally
        {
            samples = EndGazeRecording();
            if (startedTracking)
            {
                StopTracking();
            }
        }

        var validRatio = samples.Count == 0 ? 0 : samples.Count(sample => sample.IsValid) / (double)samples.Count;
        Logger.Info($"Kalibráció-ellenőrzés: {samples.Count} tekintetminta, érvényes arány {validRatio:P0}.");

        return validRatio >= MinValidSampleRatio
            ? (CalibrationCheckResult.Ok, validRatio)
            : (CalibrationCheckResult.NoValidGaze, validRatio);
    }

    private async Task<bool> HasCalibrationDataAsync(IEyeTracker eyeTracker)
    {
        try
        {
            var calibrationData = await RunNativeAsync(eyeTracker.RetrieveCalibrationData);
            return calibrationData?.Data is { Length: > 0 };
        }
        catch (Exception e)
        {
            // Nem minden eszköz vagy licenc engedi a kalibrációs adat kiolvasását; ilyenkor
            // egyedül a tekintetadat ellenőrzése dönt.
            Logger.Warning($"A kalibrációs adat nem olvasható ki, csak a tekintetadat ellenőrzése dönt: {e.Message}");
            return true;
        }
    }

    private void Tracker_GazeDataReceived(object? sender, GazeDataEventArgs e)
    {
        var leftValid = e.LeftEye.GazePoint.Validity == Validity.Valid;
        var rightValid = e.RightEye.GazePoint.Validity == Validity.Valid;
        var eyeDistanceMm = ComputeEyeDistanceMm(e);

        if (!leftValid && !rightValid)
        {
            RecordGazeSample(new GazeSample(e.SystemTimeStamp, float.NaN, float.NaN, false, false, eyeDistanceMm));
            return;
        }

        var left = e.LeftEye.GazePoint.PositionOnDisplayArea;
        var right = e.RightEye.GazePoint.PositionOnDisplayArea;
        var (x, y) = (leftValid, rightValid) switch
        {
            (true, true) => ((left.X + right.X) / 2f, (left.Y + right.Y) / 2f),
            (true, false) => (left.X, left.Y),
            _ => (right.X, right.Y)
        };

        RecordGazeSample(new GazeSample(e.SystemTimeStamp, x, y, leftValid, rightValid, eyeDistanceMm));

        lock (_lastGazeLock)
        {
            _lastNormalizedGaze = new PointF(x, y);
        }
    }

    // A két szem (ha érvényes) átlagos merőleges távolsága a kijelző síkjától, mm-ben.
    private float ComputeEyeDistanceMm(GazeDataEventArgs e)
    {
        var displayArea = _displayArea;
        if (displayArea == null)
        {
            return float.NaN;
        }

        var sum = 0d;
        var count = 0;
        if (e.LeftEye.GazeOrigin.Validity == Validity.Valid)
        {
            sum += displayArea.DistanceToPlaneMm(e.LeftEye.GazeOrigin.PositionInUserCoordinates);
            count++;
        }

        if (e.RightEye.GazeOrigin.Validity == Validity.Valid)
        {
            sum += displayArea.DistanceToPlaneMm(e.RightEye.GazeOrigin.PositionInUserCoordinates);
            count++;
        }

        return count == 0 ? float.NaN : (float)(sum / count);
    }

    // A Tobii SDK saját szálán fut (GazeDataReceived), ezért a felvétel zárolva bővül.
    private void RecordGazeSample(GazeSample sample)
    {
        lock (_gazeRecordingLock)
        {
            _gazeRecording?.Add(sample);
        }
    }

    // A legutóbbi érvényes tekintetpont képernyő-koordinátákban. A screenBounds annak a kijelzőnek
    // a területe, amelyre a Tobii display area be van állítva (a mérést futtató kijelző).
    public Point GetGazePoint(Rectangle screenBounds)
    {
        PointF normalized;
        lock (_lastGazeLock)
        {
            normalized = _lastNormalizedGaze;
        }

        if (!float.IsFinite(normalized.X) || !float.IsFinite(normalized.Y))
        {
            return new Point(int.MinValue / 2, int.MinValue / 2);
        }

        return new Point(
            (int)(screenBounds.Left + (normalized.X * screenBounds.Width)),
            (int)(screenBounds.Top + (normalized.Y * screenBounds.Height)));
    }

    // Az alkalmazás bezárásakor hívandó: leállítja a követést, felszabadítja az eszközt, és a
    // Tobii SDK ajánlása szerint lezárja az SDK natív erőforrásait.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopTracking();
        CancelGazeRecording();
        ReplaceTracker(null);
        _displayArea = null;
        GazeOutputFrequencyHz = null;

        // A még futó natív hívások (pl. egy tartó eszközkeresés) alól nem szabad lezárni az SDK-t. Az
        // InitializeAsync folytatására viszont nem várhatunk: az a UI-szálon futna, amelyet épp ez a
        // hívás foglal (az üzenetciklus leállta után pedig már le sem fut).
        Task pendingNativeWork;
        lock (_nativeWorkLock)
        {
            pendingNativeWork = _nativeWork;
        }

        if (!WaitQuietly(pendingNativeWork, NativeWorkShutdownTimeout))
        {
            Logger.Warning("A Tobii háttérművelete nem fejeződött be időben, az SDK lezárása kimarad (kilépéskor az operációs rendszer szabadítja fel).");
            return;
        }

        // A keresés lefutott, de a folytatása már nem vette át a talált eszközt.
        if (_pendingSearch is { IsCompletedSuccessfully: true, Result: { } unclaimed })
        {
            unclaimed.Tracker.Dispose();
        }

        _pendingSearch = null;

        try
        {
            EyeTrackingOperations.Terminate();
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii SDK lezárása sikertelen: {e.Message}");
        }
    }

    // Igaz, ha a feladat az időkorláton belül befejeződött (hibával is).
    private static bool WaitQuietly(Task task, TimeSpan timeout)
    {
        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            return true;
        }
    }
}
