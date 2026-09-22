using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A mérés külső függőségeinek interfészei. A MeasurementPresenter kizárólag ezeken keresztül éri el a
// szemkövetőt, az ESP-t, a fájlrendszert és a kijelzőt, így a mérés menete ezek valódi megvalósítása
// nélkül (hamis objektumokkal) is futtatható és tesztelhető. A megvalósítások az Infrastructure
// rétegben vannak, összekötésük a composition rootban (Program) történik.

// A szemkövető a mérés szemszögéből (megvalósítás: Infrastructure.Tobii.TobiiService). Az állapotot
// módosító metódusokat (InitializeAsync, StartTracking, StopTracking) a UI-szálról kell hívni.
public interface IGazeTracker
{
    bool IsTrackerAvailable { get; }

    string TrackerDescription { get; }

    double? GazeOutputFrequencyHz { get; }

    Task InitializeAsync(bool forceRefresh = false);

    void StartTracking();

    void StopTracking();

    // A tekintetminták időbélyegeivel azonos órán mért pillanatnyi idő (µs).
    long GetSystemTimeStampUs();

    // Elindít egy felvételt: innentől minden minta bekerül, amíg az EndGazeRecording vagy a
    // CancelGazeRecording le nem zárja.
    void BeginGazeRecording();

    IReadOnlyList<GazeSample> EndGazeRecording();

    void CancelGazeRecording();

    // A szemkövetőben beállított kijelző fizikai mérete; null, ha nincs beállítva.
    Task<PhysicalDisplaySize?> RefreshDisplayAreaAsync();

    // A szem merőleges távolsága a kijelző síkjától (mm, medián); null, ha nem mérhető.
    Task<double?> MeasureViewingDistanceAsync(TimeSpan duration);

    // A legutóbbi tekintetpont a megadott kijelzőn, képernyő-koordinátákban (fejlesztői overlay).
    Point GetGazePoint(Rectangle screenBounds);
}

public enum CalibrationCheckResult
{
    Ok,
    TrackerNotFound,
    NoCalibrationData,
    NoValidGaze
}

// A kalibrációs nézet igényei (megvalósítás: Infrastructure.Tobii.TobiiService).
public interface ICalibrationService
{
    Task InitializeAsync(bool forceRefresh = false);

    Task<(CalibrationCheckResult Result, double ValidGazeRatio)> CheckExistingCalibrationAsync();

    // A Tobii Pro Eye Tracker Manager kalibrációja; háttérszálról hívandó. -1, ha nincs eszköz.
    int RunUserCalibration(int? screenNumber = null);
}

// Az ESP alapú reakcióidő-mérő (megvalósítás: Infrastructure.Esp.EspResponseDevice).
public interface IResponseDevice : IAsyncDisposable
{
    string PortName { get; }

    int BaudRate { get; }

    Task SendScanoutCorrectionAsync(string correctionMicroseconds, CancellationToken cancellationToken);

    // A következő érvényes reakcióidő-üzenet ms-ban, vagy null, ha az időkorlát lejárt.
    Task<double?> WaitForReactionTimeMsAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IResponseDeviceConnector
{
    // Sikertelen kapcsolódáskor ResponseDeviceConnectionException-t dob.
    Task<IResponseDevice> ConnectAsync(HardwareSettings settings, CancellationToken cancellationToken);
}

public sealed class ResponseDeviceConnectionException : Exception
{
    public ResponseDeviceConnectionException(string portName, Exception innerException)
        : base(innerException.Message, innerException)
    {
        PortName = portName;
    }

    public string PortName { get; }
}

// A mérési eredmények tárolása (megvalósítás: Infrastructure.Persistence.MeasurementRepository).
public interface IMeasurementRepository
{
    string MeasurementsDirectory { get; }

    IReadOnlyList<MeasurementSessionResult> LoadAll();

    IMeasurementSessionWriter CreateSession(string modeName, DateTime startedAt, IReadOnlyList<KeyValuePair<string, string>> metadata);
}

// Egy mérés fájljainak próbánkénti írása. Hiba esetén IOException / UnauthorizedAccessException.
public interface IMeasurementSessionWriter
{
    string FilePath { get; }

    void AppendTrial(MeasurementResult trial);

    void Close(SessionStatus status, DateTime endedAt);
}

public interface IExperimentSettingsProvider
{
    string SettingsFilePath { get; }

    // Hibás beállítások esetén az Error ki van töltve (a Settings ilyenkor az alapérték).
    (ExperimentSettings Settings, string? Error) Load();
}

public interface IDisplayTimingProvider
{
    // MeasurementSetupException-t dob, ha a képfrissítési frekvencia sem határozható meg.
    DisplayTiming Read(string screenDeviceName, Rectangle screenBounds);
}
