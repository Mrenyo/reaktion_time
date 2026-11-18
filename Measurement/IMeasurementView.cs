using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A mérőfelület (stage) helyzete a képernyőn. A fixációs pont és a fotodióda-jelző helye a
// beállításokból jön (GazeSettings.ResolveFixationPoint, HardwareSettings.ResolvePhotodiodeBounds), nem
// a nézetből.
public sealed record StageInfo(Size StageSize, Point StageScreenOrigin, Rectangle ScreenBounds, string ScreenDeviceName);

// A mérési jelenet: a beállított háttér, az inger színe és a fixációs kereszt helye (a mérés elején
// rögzítve), valamint az, hogy látszik-e az egérmutató (csak ingerre kattintós módban).
public sealed record MeasurementScene(Color BackgroundColor, Color StimulusColor, PointF FixationPoint, bool ShowCursor);

// A válaszkattintás helye a mérőfelület koordinátáiban.
public sealed class ResponseClickEventArgs : EventArgs
{
    public ResponseClickEventArgs(PointF stagePoint)
    {
        StagePoint = stagePoint;
    }

    public PointF StagePoint { get; }
}

// Minden, amit a nézet megjelenít; a presenter számolja ki, a nézet csak alkalmazza (passzív nézet).
public sealed record MeasurementViewState(
    bool IsRunning,              // true: mérési jelenet; false: előtér
    bool IsBusy,                 // indítás/leállítás folyamatban, vagy rövid zárolás a mérés vége után: gombok tiltva
    string StartButtonText,
    string InstructionText,      // a résztvevőnek
    string SettingsSummaryText,  // a kutatónak: rövid összefoglaló, vagy a beállítási hiba
    string StatusText,           // az előző mérés kimenetele; üres, ha még nem volt
    MeasurementScene? Scene,     // csak futó mérésnél van kitöltve
    NormalizedPoint? PhotodiodeMarkerCenter); // a fotodióda-jelző helye (null: alapértelmezett), lásd PhotodiodeMarker.GetBounds

// A mérési nézet a presenter szemszögéből (MVP, passzív nézet). A nézet csak megjelenít és a
// felhasználói műveleteket eseményként továbbítja; minden döntést a MeasurementPresenter hoz.
// Az InvokeAsync kivételével a tagokat a UI-szálról kell hívni.
public interface IMeasurementView
{
    event EventHandler? StartRequested;

    event EventHandler? LeaveRequested;

    event EventHandler? AbortRequested;

    // A bal egérgomb lenyomása a mérőfelületen mérés közben (a résztvevő válasza), a helyével; UI-szálon,
    // a lenyomás üzenetének feldolgozásakor érkezik. Csak a bal gomb számít, mert az ESP is csak azt
    // érzékeli. Hogy válasznak számít-e, a presenter dönti el.
    event EventHandler<ResponseClickEventArgs>? ResponseClicked;

    // A nézet megszűnt (pl. az alkalmazás bezárása): a futó mérést le kell állítani.
    event EventHandler? Closed;

    // A művelet végrehajtása a UI-szálon (háttérszálról hívható).
    Task InvokeAsync(Action action);

    StageInfo GetStageInfo();

    void ApplyState(MeasurementViewState state);

    // Az inger (null = nincs inger) és vele együtt a fotodióda-jelző megjelenítése. Ha a
    // paintImmediately igaz, az ablak felületére rajzolás még a hívás visszatérése előtt megtörténik; a
    // kijelzőn a kép csak a következő képkockák egyikében jelenik meg (ezt méri a fotodióda).
    void ShowStimulus(RectangleF? stimulusBounds, bool paintImmediately);

    void ShowTrackerNotConnected();

    void ShowSettingsError(string settingsPath, string details);

    void ShowSetupError(string details);

    void ShowEspNotConnected(string portName, Exception exception);

    // Az ESP portja megnyílt, de a kapcsolatellenőrzés nem sikerült (nem jött "OK", vagy kommunikációs hiba).
    void ShowEspCommunicationError(string portName, int baudRate, Exception? exception);

    // Az ESP a mérés közben kiesett (a mérés nélküle folytatódott); a mérés végén jelezzük.
    void ShowEspConnectionLost(string portName, string trialLabel, Exception exception);

    // A szemkövető a mérés közben kiesett; measurementAborted: a mérés emiatt leállt (tekintet-ellenőrzésnél).
    void ShowTrackerConnectionLost(string trialLabel, bool measurementAborted);

    void ShowSaveFailed(string path, Exception exception);

    void ShowUnexpectedError(Exception exception);
}
