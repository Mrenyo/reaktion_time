namespace ReactionTimeMeasurementApp.Measurement;

// A mérőfelület (stage) helyzete a képernyőn.
public sealed record StageInfo(Size StageSize, Point StageScreenOrigin, Rectangle ScreenBounds, string ScreenDeviceName)
{
    public Rectangle PhotodiodeBounds => PhotodiodeMarker.GetBounds(StageSize);

    public PointF FixationPoint => new(StageSize.Width / 2f, StageSize.Height / 2f);
}

// A fotodióda-jelző egy kis négyzet a mérőfelület jobb alsó sarkában. A nézet az ingerrel EGYÜTT,
// ugyanabban a rajzolásban teszi ki: pontosan addig fehér, amíg az inger látszik. Egy erre szerelt
// fizikai fotodióda (az ESP-n keresztül) ebből méri, mikor jelent meg ténylegesen a kép.
public static class PhotodiodeMarker
{
    public const int SizePx = 56;
    public const int MarginPx = 20;

    // Üres, ha a mérőfelület túl kicsi hozzá.
    public static Rectangle GetBounds(Size stageSize)
    {
        var x = stageSize.Width - SizePx - MarginPx;
        var y = stageSize.Height - SizePx - MarginPx;
        return x < 0 || y < 0 ? Rectangle.Empty : new Rectangle(x, y, SizePx, SizePx);
    }
}

// Minden, amit a nézet megjelenít; a presenter számolja ki, a nézet csak alkalmazza (passzív nézet).
public sealed record MeasurementViewState(
    bool IsRunning,
    bool IsBusy,
    string StartButtonText,
    string InstructionText,
    string ProgressText,
    string LastResultText,
    float FixationCrossSizePx,
    float FixationCrossThicknessPx);

// A mérési nézet a presenter szemszögéből (MVP, passzív nézet). A nézet csak megjelenít és a
// felhasználói műveleteket eseményként továbbítja; minden döntést a MeasurementPresenter hoz.
// Az InvokeAsync kivételével a tagokat a UI-szálról kell hívni.
public interface IMeasurementView
{
    event EventHandler? StartRequested;

    event EventHandler? LeaveRequested;

    event EventHandler? AbortRequested;

    // A nézet megszűnt (pl. az alkalmazás bezárása): a futó mérést le kell állítani.
    event EventHandler? Closed;

    // A művelet végrehajtása a UI-szálon (háttérszálról hívható).
    Task InvokeAsync(Action action);

    StageInfo GetStageInfo();

    void ApplyState(MeasurementViewState state);

    // Az inger (null = nincs inger) és vele együtt a fotodióda-jelző megjelenítése. Ha a
    // paintImmediately igaz, a kirajzolás szinkron módon, még a hívás visszatérése előtt megtörténik.
    void ShowStimulus(RectangleF? stimulusBounds, bool paintImmediately);

    void ShowTrackerNotConnected();

    void ShowSettingsError(string settingsPath, string details);

    void ShowSetupError(string details);

    void ShowEspNotConnected(string portName, Exception exception);

    void ShowEspCommunicationError(string portName, int baudRate, Exception exception);

    void ShowSaveFailed(string path, Exception exception);

    void ShowUnexpectedError(Exception exception);
}
