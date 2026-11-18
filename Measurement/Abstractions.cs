using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A mérés külső függőségeinek interfészei. A MeasurementPresenter kizárólag ezeken keresztül éri el a
// szemkövetőt, az ESP-t, a fájlrendszert és a kijelzőt, így a mérés menete ezek valódi megvalósítása
// nélkül (hamis objektumokkal) is futtatható és tesztelhető. A megvalósítások az Infrastructure
// rétegben vannak, összekötésük a composition rootban (Program) történik.

// A szemkövető a mérés szemszögéből (megvalósítás: Infrastructure.Tobii.TobiiService). Az állapotot
// módosító metódusokat (InitializeAsync, StartTracking, StopTracking) a UI-szálról kell hívni.
//
// A szemkövető csak a tekintet-ellenőrzéshez kötelező. Ha nem érhető el (IsTrackerAvailable hamis), a
// mérés a felvételhez és az időbélyegekhez tartozó tagokat (GetSystemTimeStampUs, BeginGazeRecording,
// EndGazeRecording, CancelGazeRecording) nem hívja, mert ilyenkor a Tobii natív könyvtára is hiányozhat.
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

    // A szemkövetőben beállított kijelző (display area) fizikai mérete; null, ha nincs beállítva. A
    // display area képezi le a tekintetet a képernyőre, ezért a mérés ebből ellenőrzi, hogy a szemkövető
    // a mérést futtató kijelzőre van-e beállítva; a méret a metaadatokba kerül. A tekintetminták
    // szemtávolsága (GazeSample.EyeDistanceMm) is a display area síkjához mérve értendő.
    Task<PhysicalDisplaySize?> RefreshDisplayAreaAsync();

    // A legutóbbi tekintetpont a megadott kijelzőn, képernyő-koordinátákban (fejlesztői overlay).
    Point GetGazePoint(Rectangle screenBounds);

    // A szemkövető kapcsolatának elvesztése és helyreállása (pl. kihúzott USB-kábel). Tetszőleges
    // (a Tobii SDK) szálán érkezik; a feliratkozónak kell a UI-szálra továbbítania.
    event EventHandler<TrackerConnectionChangedEventArgs>? ConnectionChanged;
}

public sealed class TrackerConnectionChangedEventArgs(bool isConnected) : EventArgs
{
    public bool IsConnected { get; } = isConnected;
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
    int RunUserCalibration();
}

// Az ESP alapú reakcióidő-mérő (megvalósítás: Infrastructure.Esp.EspResponseDevice). A kommunikációs
// hibát (pl. kihúzott eszköz) IOException, InvalidOperationException vagy UnauthorizedAccessException jelzi.
public interface IResponseDevice : IAsyncDisposable
{
    string PortName { get; }

    int BaudRate { get; }

    // Kapcsolatellenőrzés a mérés indításakor ("CHECK" -> "OK"). Hamis, ha az ESP nem
    // válaszolt "OK"-val.
    Task<bool> CheckAsync(CancellationToken cancellationToken);

    // A firmware verziója (pl. "1.1.0"), ha a RequestFirmwareVersionAsync sikeresen lekérdezte;
    // különben null (régebbi firmware, vagy még nem volt lekérdezés).
    string? FirmwareVersion { get; }

    // A sikeres kapcsolatellenőrzés után: a firmware verziójának lekérdezése ("VERSION"). Null, ha az
    // ESP nem válaszolt verzió alakú sorral az időkorláton belül.
    Task<string?> RequestFirmwareVersionAsync(CancellationToken cancellationToken);

    Task SendScanoutCorrectionAsync(string correctionMicroseconds, CancellationToken cancellationToken);

    // A résztvevő kattintása után: az ESP által mért reakcióidő lekérése ("RTIME") ms-ban, vagy null, ha
    // az ESP nem küldött értelmezhető választ az időkorláton belül.
    Task<double?> RequestReactionTimeMsAsync(CancellationToken cancellationToken);
}

public interface IResponseDeviceConnector
{
    // Sikertelen kapcsolódáskor ResponseDeviceConnectionException-t dob.
    Task<IResponseDevice> ConnectAsync(HardwareSettings settings, CancellationToken cancellationToken);

    // A gépen elérhető soros portok neve, sorszám szerint rendezve (pl. "COM3", "COM5"); üres lista,
    // ha nincs ilyen, vagy nem kérdezhető le.
    IReadOnlyList<string> GetAvailablePortNames();
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

    // Soha nem dob kivételt a fájlok hibája miatt: a beolvashatatlan fájlok neve az UnreadableFiles-ba kerül.
    MeasurementLoadResult LoadAll();

    IMeasurementSessionWriter CreateSession(string modeName, DateTime startedAt, IReadOnlyList<KeyValuePair<string, string>> metadata);
}

// Az összes mérés beolvasásának eredménye: a beolvasott mérések (a legújabbal kezdve) és a beolvashatatlan
// fájlok neve (a felület ezt jelzi, hogy egy hibás fájl ne tűnjön el csendben).
public sealed record MeasurementLoadResult(IReadOnlyList<MeasurementSessionResult> Sessions, IReadOnlyList<string> UnreadableFiles);

// Egy mérés fájljainak próbánkénti írása. Hiba esetén IOException / UnauthorizedAccessException.
public interface IMeasurementSessionWriter
{
    string FilePath { get; }

    void AppendTrial(MeasurementResult trial);

    void Close(SessionStatus status, DateTime endedAt);
}

// A mérési beállítások tárolása (megvalósítás: Infrastructure.ExperimentSettingsStore).
public interface IExperimentSettingsProvider
{
    // Soha nem dob kivételt: minden hibát az Error mezőben, magyar szöveggel ad vissza.
    SettingsLoadResult Load();

    // IOException / UnauthorizedAccessException, ha a fájl nem írható; ArgumentException, ha a beállítások érvénytelenek.
    void Save(ExperimentSettings settings);

    string SettingsFilePath { get; }
}

// A beállítások betöltésének eredménye.
//   Settings - mindig kitöltött, null szakasz nélkül. Ha a fájl beolvasható volt, de az
//              ExperimentSettings.Validate() hibát talált, a beolvasott (hibás) értékeket tartalmazza,
//              hogy a beállítási ablak megmutathassa és javíthassa őket; alapértékeket csak
//              olvashatatlan, értelmezhetetlen vagy újabb programverzióhoz készült fájlnál ad.
//   Error    - ha nem null, a mérés nem indulhat el (a beállítások nem a kutató szándéka szerintiek).
public sealed record SettingsLoadResult(ExperimentSettings Settings, string? Error);

public interface IDisplayTimingProvider
{
    // MeasurementSetupException-t dob, ha a képfrissítési frekvencia sem határozható meg.
    DisplayTiming Read(string screenDeviceName, Rectangle screenBounds);

    // A kijelző fizikai (látható) mérete a monitor EDID-adatai alapján; null, ha nem kérdezhető le. Soha
    // nem dob kivételt.
    PhysicalDisplaySize? ReadPhysicalSize(string screenDeviceName);
}
