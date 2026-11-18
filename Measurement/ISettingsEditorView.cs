using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A beállítási ablak szerkeszthető értékei, a vezérlők nyelvén: a nézet ebbe olvassa ki őket
// (ISettingsEditorView.ReadValues), és ebből tölti ki a vezérlőket (ShowValues). A beállítások
// összeállítása és ellenőrzése a SettingsEditorPresenter dolga. A fixációs pontnak és a fotodióda-jelző
// helyének nincs értéket tároló vezérlője, ezeket a presenter tárolja.
public sealed class SettingsEditorValues
{
    // Mezőnként a vezérlő értéke (a mező tartományában, lásd SettingsNumberField).
    public Dictionary<SettingsNumber, decimal> Numbers { get; } = new();

    public ResponseMode ResponseMode { get; set; }

    public ForeperiodDistribution ForeperiodDistribution { get; set; }

    // A színmezők véglegesített szövege (gépelés közben a mező szövege még nem érvényes); kitöltéskor a
    // beállításfájl szövege, amelyet a nézet egységes alakra hoz.
    public string StimulusColor { get; set; } = string.Empty;

    public string BackgroundColor { get; set; } = string.Empty;

    // A tekintet-ellenőrzés jelölőnégyzete és a "mit kell néznie" választás együtt.
    public GazeValidationMode GazeValidation { get; set; }

    public FixationPlacement FixationPlacement { get; set; }

    // null: automatikus felismerés.
    public string? EspPortName { get; set; }

    // A beírt szöveg; hogy érvényes szám-e, a presenter ellenőrzi.
    public string BaudRateText { get; set; } = string.Empty;
}

// Minden, amit a beállítási ablak megjelenít; a presenter számolja ki módosításonként egyszer, a nézet
// csak alkalmazza (passzív nézet).
public sealed record SettingsEditorViewState(
    Size ScreenSize,                                  // a mérést futtató képernyő mérete
    ExperimentSettings Settings,                      // az előnézethez
    StageFeasibility? Feasibility,                    // null, ha a beállítások hibásak
    IReadOnlyList<SettingsIssue> Errors,              // a szakaszuk (Section) füle hibajelet kap
    IReadOnlyList<SettingsIssue> Warnings,
    bool CanSave,
    string TrialCountNote,
    string ResponseModeHint,
    bool ForeperiodMeanEnabled,                       // csak exponenciális eloszlásnál
    bool GazeOptionsEnabled,                          // csak tekintet-ellenőrzéssel
    bool FixationPlacementEnabled,                    // csak fixációs módban
    bool PickFixationPointEnabled,
    string ValidityRadiusHint,
    Point? FixationPointPixel,                        // null: még nincs kijelölve
    Rectangle PhotodiodeBounds,
    bool PhotodiodeIsDefault);

// A beállítási ablak a presenter szemszögéből (MVP, passzív nézet). A nézet a vezérlők értékeit adja és
// mutatja, a felhasználói műveleteket eseményként továbbítja; minden döntést a SettingsEditorPresenter
// hoz. A tagokat a UI-szálról kell hívni.
public interface ISettingsEditorView
{
    // Egy érték módosult. Egy felhasználói művelet több vezérlőeseményt is kivált; a nézet ezeket
    // összevonja, és az üzenetsor következő körében egyszer jelez.
    event EventHandler? SettingsEdited;

    event EventHandler? PickFixationPointRequested;

    // A kutató maga választotta az egyéni fixációs helyet (a rádiógomb eseménykezelése után jelez, ha a
    // választás még érvényes).
    event EventHandler? CustomFixationPlacementSelected;

    event EventHandler? MovePhotodiodeRequested;

    event EventHandler? ResetPhotodiodeRequested;

    event EventHandler? DefaultsRequested;

    event EventHandler? SaveRequested;

    // A mérést futtató képernyő mérete: ehhez igazodik az ellenőrzés és az előnézet.
    Size MeasurementScreenSize { get; }

    // A betöltés hibája és tájékoztatása a fülek fölött; null: nincs szalag.
    void ShowBanner(string? text);

    // A vezérlők kitöltése; includeHardware: false esetén a hardverbeállítások vezérlői változatlanok.
    // Nem számít módosításnak (nem vált ki SettingsEdited-et).
    void ShowValues(SettingsEditorValues values, bool includeHardware);

    // commit: false esetén a még gépelés alatt álló számot is figyelembe veszi (úgy, ahogy a vezérlő
    // véglegesítené); true esetén a véglegesített értékeket adja.
    SettingsEditorValues ReadValues(bool commit);

    // A gépelés alatt álló értékek véglegesítése (számmezők, színmezők).
    void CommitPendingEdits();

    void ApplyState(SettingsEditorViewState state);

    void SelectSection(SettingsSection section);

    // A fixációs pont kijelölője a mérést futtató képernyőn, a megadott (még nem mentett) beállításokkal;
    // null, ha a kutató megszakította.
    NormalizedPoint? PickFixationPoint(ExperimentSettings settings, NormalizedPoint? currentPoint);

    // A fotodióda-jelző áthelyezője; hamis, ha a kutató megszakította. A center null: alapértelmezett hely.
    bool TryPickPhotodiodeMarker(ExperimentSettings settings, out NormalizedPoint? center);

    // A fixációs pont helye vissza középre (nem számít módosításnak).
    void SelectCenterFixationPlacement();

    void ShowSaveFailed(string path, Exception exception);

    void ShowUnexpectedError(Exception exception);

    // Sikeres mentés után: az ablak bezárul.
    void CloseAsSaved();
}
