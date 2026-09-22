using System.Numerics;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;
using Tobii.Research;

namespace ReactionTimeMeasurementApp.Infrastructure.Tobii;

// A Tobii-ban (Eye Tracker Manager) beállított kijelző (display area) fizikai mérete és síkja a
// szemkövető koordináta-rendszerében (mm). Ebből adódik a kijelző valódi pixelsűrűsége, és a szem
// távolsága a kijelző síkjától (a nézési távolság).
internal sealed class DisplayAreaGeometry
{
    public DisplayAreaGeometry(DisplayArea displayArea)
    {
        WidthMm = displayArea.Width;
        HeightMm = displayArea.Height;
        _topLeft = ToVector(displayArea.TopLeft);
        var right = ToVector(displayArea.TopRight) - _topLeft;
        var down = ToVector(displayArea.BottomLeft) - _topLeft;
        _normal = Vector3.Normalize(Vector3.Cross(right, down));
    }

    private readonly Vector3 _topLeft;
    private readonly Vector3 _normal;

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
// az await-ek után a folytatás is ott fut, így a "tracker" mezőt mindig ugyanaz a szál írja.
internal sealed class TobiiService : IGazeTracker, ICalibrationService, IDisposable
{
    private const int InitialGazeRecordingCapacity = 1024;
    private static readonly TimeSpan CalibrationGazeCheckDuration = TimeSpan.FromSeconds(1);
    private const double MinValidSampleRatio = 0.5;

    private readonly object _gazeRecordingLock = new();
    private readonly object _lastGazeLock = new();
    private IEyeTracker? tracker;
    private Task? _initializeTask;
    private bool _isTracking;
    private bool _disposed;
    private PointF _lastNormalizedGaze = new(float.NaN, float.NaN);
    private List<GazeSample>? _gazeRecording;

    // A Tobii saját szálán futó GazeDataReceived is olvassa, ezért volatile.
    private volatile DisplayAreaGeometry? _displayArea;

    public bool IsTrackerAvailable => tracker != null;

    public string TrackerDescription => tracker is null ? "nincs" : $"{tracker.Model} (sorozatszám: {tracker.SerialNumber})";

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
            return recording is null ? Array.Empty<GazeSample>() : recording;
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
        if (_disposed || _isTracking || (tracker != null && !forceRefresh))
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
        try
        {
            var foundTrackers = (await EyeTrackingOperations.FindAllEyeTrackersAsync()).ToList();
            var found = foundTrackers.FirstOrDefault();

            foreach (var unused in foundTrackers.Skip(1))
            {
                unused.Dispose();
            }

            // Ha a keresés alatt elindult a követés (vagy a szolgáltatás leállt), a meglévő
            // eszközpéldány marad érvényben, az újonnan talált felesleges.
            if (_isTracking || _disposed)
            {
                if (found != null && !ReferenceEquals(found, tracker))
                {
                    found.Dispose();
                }

                return;
            }

            ReplaceTracker(found);

            if (found != null)
            {
                Logger.Info($"Tobii eszköz megtalálva: {found.Model}");
                GazeOutputFrequencyHz = await TryReadGazeOutputFrequencyAsync(found);
                await RefreshDisplayAreaAsync();
            }
            else
            {
                Logger.Warning("Tobii eszköz nem csatlakozik.");
            }
        }
        catch (Exception e)
        {
            Logger.Error("Tobii inicializálási hiba.", e);
        }
    }

    // A display area az Eye Tracker Managerben az alkalmazás futása közben is módosulhat, ezért a
    // mérés minden indításkor újra lekérdezi.
    public async Task<PhysicalDisplaySize?> RefreshDisplayAreaAsync()
    {
        var eyeTracker = tracker;
        if (eyeTracker == null)
        {
            _displayArea = null;
            return null;
        }

        try
        {
            var geometry = new DisplayAreaGeometry(await Task.Run(eyeTracker.GetDisplayArea));
            _displayArea = geometry.IsValid ? geometry : null;
            if (_displayArea != null)
            {
                Logger.Info($"Tobii display area: {geometry.WidthMm:0.0} x {geometry.HeightMm:0.0} mm.");
            }
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii display area nem kérdezhető le: {e.Message}");
            _displayArea = null;
        }

        var displayArea = _displayArea;
        return displayArea is null ? null : new PhysicalDisplaySize(displayArea.WidthMm, displayArea.HeightMm);
    }

    private static async Task<double?> TryReadGazeOutputFrequencyAsync(IEyeTracker eyeTracker)
    {
        try
        {
            return await Task.Run(eyeTracker.GetGazeOutputFrequency);
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii mintavételi frekvenciája nem kérdezhető le: {e.Message}");
            return null;
        }
    }

    // A nézési távolság (a szem merőleges távolsága a kijelző síkjától) mediánja egy rövid felvétel
    // alapján. Null, ha a display area nem ismert, vagy a minták kevesebb mint felében nem volt
    // érvényes szempozíció (pl. a résztvevő nem ül a szemkövető előtt). A követésnek futnia kell.
    public async Task<double?> MeasureViewingDistanceAsync(TimeSpan duration)
    {
        if (_displayArea == null)
        {
            return null;
        }

        BeginGazeRecording();
        IReadOnlyList<GazeSample> samples;
        try
        {
            await Task.Delay(duration);
        }
        finally
        {
            samples = EndGazeRecording();
        }

        var distances = samples
            .Select(sample => sample.EyeDistanceMm)
            .Where(float.IsFinite)
            .OrderBy(distance => distance)
            .ToArray();

        if (samples.Count == 0 || distances.Length < samples.Count * MinValidSampleRatio)
        {
            return null;
        }

        var middle = distances.Length / 2;
        return distances.Length % 2 == 1
            ? distances[middle]
            : (distances[middle - 1] + distances[middle]) / 2d;
    }

    // A lecserélt eszközpéldányt fel kell szabadítani, különben a natív kapcsolata nyitva marad.
    private void ReplaceTracker(IEyeTracker? newTracker)
    {
        var previous = tracker;
        tracker = newTracker;

        if (previous != null && !ReferenceEquals(previous, newTracker))
        {
            _displayArea = null;
            GazeOutputFrequencyHz = null;
            previous.Dispose();
        }
    }

    public void StartTracking()
    {
        if (_isTracking || tracker == null)
        {
            return;
        }

        tracker.GazeDataReceived += Tracker_GazeDataReceived;
        _isTracking = true;
    }

    public void StopTracking()
    {
        if (!_isTracking || tracker == null)
        {
            return;
        }

        tracker.GazeDataReceived -= Tracker_GazeDataReceived;
        _isTracking = false;
    }

    // Háttérszálról (Task.Run) is hívható: csak olvassa a "tracker" mezőt, nem cseréli le. Előtte
    // az InitializeAsync-kal kell gondoskodni róla, hogy a legfrissebb eszközpéldány legyen meg.
    public int RunUserCalibration(int? screenNumber = null)
    {
        var eyeTracker = tracker;
        if (eyeTracker == null)
        {
            Logger.Warning("Kalibráció kihagyva: Tobii eszköz nem csatlakozik.");
            return -1;
        }

        return TobiiCalibrationLauncher.RunUserCalibration(eyeTracker, screenNumber);
    }

    // A "meglévő kalibráció" használata előtt ellenőrzi, hogy (1) csatlakozik-e szemkövető,
    // (2) van-e rajta alkalmazott kalibráció, és (3) érkezik-e tőle ténylegesen érvényes
    // tekintetadat, vagyis a résztvevő a szemkövető előtt ül, és az eszköz követi a szemét.
    public async Task<(CalibrationCheckResult Result, double ValidGazeRatio)> CheckExistingCalibrationAsync()
    {
        await InitializeAsync(forceRefresh: true);

        var eyeTracker = tracker;
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

    private static async Task<bool> HasCalibrationDataAsync(IEyeTracker eyeTracker)
    {
        try
        {
            var calibrationData = await Task.Run(eyeTracker.RetrieveCalibrationData);
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

        float x;
        float y;

        if (leftValid && rightValid)
        {
            var left = e.LeftEye.GazePoint.PositionOnDisplayArea;
            var right = e.RightEye.GazePoint.PositionOnDisplayArea;
            x = (left.X + right.X) / 2f;
            y = (left.Y + right.Y) / 2f;
        }
        else if (leftValid)
        {
            x = e.LeftEye.GazePoint.PositionOnDisplayArea.X;
            y = e.LeftEye.GazePoint.PositionOnDisplayArea.Y;
        }
        else
        {
            x = e.RightEye.GazePoint.PositionOnDisplayArea.X;
            y = e.RightEye.GazePoint.PositionOnDisplayArea.Y;
        }

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
        foreach (var eye in new[] { e.LeftEye, e.RightEye })
        {
            if (eye.GazeOrigin.Validity == Validity.Valid)
            {
                sum += displayArea.DistanceToPlaneMm(eye.GazeOrigin.PositionInUserCoordinates);
                count++;
            }
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

        StopTracking();
        CancelGazeRecording();
        ReplaceTracker(null);
        _disposed = true;

        try
        {
            EyeTrackingOperations.Terminate();
        }
        catch (Exception e)
        {
            Logger.Warning($"A Tobii SDK lezárása sikertelen: {e.Message}");
        }
    }
}
