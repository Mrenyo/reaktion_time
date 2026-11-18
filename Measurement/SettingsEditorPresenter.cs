using System.Globalization;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A mérési beállítások szerkesztése (MVP presenter, a beállítási ablakhoz). A betöltött beállítások egy
// másolatán dolgozik: minden módosítás után újraépíti a beállításokat a nézet értékeiből, lefuttatja az
// ExperimentSettings.Validate()-et és - ha az hibátlan - a mérést futtató képernyőre vonatkozó
// StageFeasibility-t. Az eredményből (hibák, figyelmeztetések, előnézet, a függő vezérlők állapota)
// módosításonként egyszer készül a nézet állapota; a Mentés csak hibátlan beállításoknál engedélyezett.
//
// Mentéskor előbb a még gépelés alatt álló értékek véglegesülnek, és újra lefut az ellenőrzés; az ablak
// csak sikeres mentés után zárul be. Ha a mentés nem sikerül (a fájl nem írható), a módosítások
// megmaradnak.
public sealed class SettingsEditorPresenter
{
    private readonly ISettingsEditorView _view;
    private readonly IExperimentSettingsProvider _settingsProvider;
    private readonly Size _screenSize;

    // A betöltött beállítások másolata: ebből indul a beállítások újraépítése, így a vezérlővel nem
    // rendelkező értékek változatlanul megmaradnak.
    private readonly ExperimentSettings _initialSettings;

    // A kijelölt fixációs pont és a fotodióda-jelző helye (arány); ezeknek nincs értéket tároló vezérlőjük.
    private NormalizedPoint? _fixationPoint;
    private NormalizedPoint? _photodiodeMarkerCenter;

    public SettingsEditorPresenter(ISettingsEditorView view, IExperimentSettingsProvider settingsProvider)
    {
        _view = view;
        _settingsProvider = settingsProvider;
        _screenSize = view.MeasurementScreenSize;

        // A betöltött beállítások hibásak is lehetnek (lásd SettingsLoadResult): az ablak éppen ezek
        // javítására is szolgál.
        var load = settingsProvider.Load();
        _initialSettings = load.Settings.Clone();
        _view.ShowBanner(BuildBannerText(load.Error));
        ShowSettings(load.Settings, includeHardware: true);

        _view.SettingsEdited += (_, _) => Refresh();
        _view.PickFixationPointRequested += (_, _) => PickFixationPoint();
        _view.CustomFixationPlacementSelected += (_, _) => HandleCustomFixationPlacementSelected();
        _view.MovePhotodiodeRequested += (_, _) => PickPhotodiodeMarker();
        _view.ResetPhotodiodeRequested += (_, _) => ResetPhotodiodeMarker();
        _view.DefaultsRequested += (_, _) => ShowSettings(new ExperimentSettings(), includeHardware: false);
        _view.SaveRequested += (_, _) => Save();
    }

    // A beállítások összeállítása a nézet értékeiből: a betöltött beállítások másolatába kerülnek a
    // szerkesztett értékek. A beviteli hibák (pl. nem szám az átviteli sebesség) külön listába kerülnek,
    // mert ezek az értékek a beállításokba be sem írhatók.
    private static (ExperimentSettings Settings, IReadOnlyList<SettingsIssue> InputErrors) BuildSettings(
        ExperimentSettings baseSettings,
        SettingsEditorValues values,
        NormalizedPoint? fixationPoint,
        NormalizedPoint? photodiodeMarkerCenter)
    {
        var settings = baseSettings.Clone();
        settings.SettingsVersion = ExperimentSettings.CurrentVersion;
        var inputErrors = new List<SettingsIssue>();

        foreach (var field in SettingsNumberField.All)
        {
            field.Write(settings, decimal.Round(values.Numbers[field.Id], field.DecimalPlaces, MidpointRounding.AwayFromZero));
        }

        settings.Trials.ResponseMode = values.ResponseMode;
        settings.Trials.ForeperiodDistribution = values.ForeperiodDistribution;
        settings.Stimulus.Color = values.StimulusColor;
        settings.Stimulus.BackgroundColor = values.BackgroundColor;
        settings.Gaze.Validation = values.GazeValidation;
        settings.Gaze.FixationPlacement = values.FixationPlacement;
        settings.Gaze.FixationPointX = fixationPoint?.X;
        settings.Gaze.FixationPointY = fixationPoint?.Y;
        settings.Hardware.EspPortName = values.EspPortName;
        settings.Hardware.PhotodiodeMarkerX = photodiodeMarkerCenter?.X;
        settings.Hardware.PhotodiodeMarkerY = photodiodeMarkerCenter?.Y;

        if (int.TryParse(values.BaudRateText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var baudRate))
        {
            settings.Hardware.EspBaudRate = baudRate;
        }
        else
        {
            inputErrors.Add(new SettingsIssue(
                SettingsSection.Hardware,
                "Az átviteli sebesség (Hardware.EspBaudRate) pozitív egész szám legyen, pl. 115200."));
        }

        return (settings, inputErrors);
    }

    // A beállítások teljes ellenőrzése: a Validate() hibái, a beviteli hibák, majd - csak hibátlan
    // beállításoknál - a képernyőhöz mért StageFeasibility.
    private static SettingsEvaluation Evaluate(ExperimentSettings settings, IReadOnlyList<SettingsIssue> inputErrors, Size screenSize)
    {
        var errors = new List<SettingsIssue>(settings.Validate());
        var settingsValid = errors.Count == 0;
        errors.AddRange(inputErrors);

        // A módszertani figyelmeztetések (kontraszt, válaszmód és tekintet-ellenőrzés együttese, gyakorlás
        // hiánya) a beállításokból jönnek, a képernyőhöz kötöttek a StageFeasibility-ből.
        var warnings = new List<SettingsIssue>(settings.GetWarnings());
        StageFeasibility? feasibility = null;
        if (settingsValid)
        {
            feasibility = StageFeasibility.Evaluate(settings, screenSize);
            errors.AddRange(feasibility.Errors);
            warnings.AddRange(feasibility.Warnings);
        }

        return new SettingsEvaluation(settings, errors, warnings, feasibility);
    }

    // A betöltés hibája a fülek fölötti szalagon; null, ha nincs hiba.
    private static string? BuildBannerText(string? loadError)
    {
        return string.IsNullOrWhiteSpace(loadError)
            ? null
            : "A beállításfájl hibás, ezért a mérés nem indul el. Javítsd az értékeket, és mentsd őket (az előző fájl "
                + ".bak kiterjesztéssel megmarad)."
                + Environment.NewLine + loadError.Trim();
    }

    // A vezérlők kitöltése a megadott beállításokból, majd ellenőrzés. includeHardware: false esetén (az
    // Alapértékek gomb) a hardverbeállítások (port, sebesség, a fotodióda-jelző helye) maradnak, mert a
    // géphez tartoznak.
    private void ShowSettings(ExperimentSettings settings, bool includeHardware)
    {
        _fixationPoint = settings.Gaze.FixationPointCenter;
        if (includeHardware)
        {
            _photodiodeMarkerCenter = settings.Hardware.PhotodiodeMarkerCenter;
        }

        _view.ShowValues(CreateValues(settings), includeHardware);
        Refresh();
    }

    private static SettingsEditorValues CreateValues(ExperimentSettings settings)
    {
        var values = new SettingsEditorValues
        {
            ResponseMode = settings.Trials.ResponseMode,
            ForeperiodDistribution = settings.Trials.ForeperiodDistribution,
            StimulusColor = settings.Stimulus.Color,
            BackgroundColor = settings.Stimulus.BackgroundColor,
            GazeValidation = settings.Gaze.Validation,
            FixationPlacement = settings.Gaze.FixationPlacement,
            EspPortName = settings.Hardware.EspPortName?.Trim() is { Length: > 0 } portName ? portName : null,
            BaudRateText = settings.Hardware.EspBaudRate.ToString(CultureInfo.InvariantCulture)
        };

        foreach (var field in SettingsNumberField.All)
        {
            values.Numbers[field.Id] = ToEditorValue(field, settings);
        }

        return values;
    }

    // A tartományon kívüli (vagy nem véges) szám a mező tartományába kerül (naplózva), mert a vezérlő
    // mást nem tud megjeleníteni.
    private static decimal ToEditorValue(SettingsNumberField field, ExperimentSettings settings)
    {
        var value = field.Read(settings);
        var clamped = double.IsFinite(value) ? Math.Clamp(value, (double)field.Min, (double)field.Max) : (double)field.Max;
        if (clamped != value)
        {
            Logger.Warning(
                $"A beállítási ablak a(z) {field.Path} értékét ({value.ToString(CultureInfo.InvariantCulture)}) a megengedett "
                + $"tartományba igazította: {clamped.ToString(CultureInfo.InvariantCulture)}.");
        }

        return decimal.Round((decimal)clamped, field.DecimalPlaces, MidpointRounding.AwayFromZero);
    }

    private SettingsEvaluation EvaluateView(bool commit)
    {
        var (settings, inputErrors) = BuildSettings(_initialSettings, _view.ReadValues(commit), _fixationPoint, _photodiodeMarkerCenter);
        return Evaluate(settings, inputErrors, _screenSize);
    }

    // Minden módosítás után: újraellenőrzés a még gépelés alatt álló értékekkel, és a nézet frissítése.
    private void Refresh() => _view.ApplyState(CreateViewState(EvaluateView(commit: false)));

    // Az ellenőrzés eredményéből a nézet állapota: üzenetek, a fülek hibajele, a Mentés gomb, az előnézet,
    // a nem alkalmazható vezérlők tiltása és a beállításoktól függő szövegek.
    private SettingsEditorViewState CreateViewState(SettingsEvaluation evaluation)
    {
        var settings = evaluation.Settings;
        var trials = settings.Trials;
        var validationOn = settings.Gaze.Validation != GazeValidationMode.None;
        var keepFixation = settings.Gaze.Validation == GazeValidationMode.KeepFixation;

        var trialCountNote = trials.PracticeTrials == 0
            ? $"Összesen {trials.MeasuredTrials} inger jelenik meg (gyakorlás nélkül). Az érvénytelen próbák nem pótlódnak."
            : $"Összesen {trials.PracticeTrials + trials.MeasuredTrials} inger jelenik meg: először {trials.PracticeTrials} gyakorló, "
              + $"utána {trials.MeasuredTrials} mért. A gyakorló ingerek nem számítanak bele az eredménybe; az érvénytelen próbák nem pótlódnak.";

        var responseModeHint = trials.ResponseMode == ResponseMode.ClickOnStimulus
            ? "A résztvevőnek az ingerre kell kattintania; az egérmutató látszik. A mellékattintás után is eltűnik az inger, "
              + "de a próba érvénytelen (mellékattintás)."
            : "A résztvevő bárhová kattinthat, amint megjelenik az inger; az egérmutató mérés közben nem látszik.";

        var validityRadiusHint = keepFixation
            ? "A fixációs pont körül: a próba érvényes, ha a tekintet a válaszig végig ezen a körön belül marad."
            : "Az inger szélén túl elfogadott tekintethiba: a próba érvényes, ha a tekintet a válasz előtt legalább "
              + "egyszer ennyire megközelíti az inger szélét.";

        return new SettingsEditorViewState(
            _screenSize,
            settings,
            evaluation.Feasibility,
            evaluation.Errors,
            evaluation.Warnings,
            CanSave: evaluation.Errors.Count == 0,
            trialCountNote,
            responseModeHint,
            ForeperiodMeanEnabled: trials.ForeperiodDistribution != ForeperiodDistribution.Uniform,
            GazeOptionsEnabled: validationOn,
            FixationPlacementEnabled: keepFixation,
            PickFixationPointEnabled: keepFixation && settings.Gaze.FixationPlacement == FixationPlacement.Custom,
            validityRadiusHint,
            settings.Gaze.FixationPointCenter?.ToPixel(_screenSize),
            settings.Hardware.ResolvePhotodiodeBounds(_screenSize),
            PhotodiodeIsDefault: _photodiodeMarkerCenter is null);
    }

    // A véglegesített értékekből összeállított beállítások a kijelölőknek (a mérést futtató képernyőn
    // nyílnak meg, a jelenlegi, még nem mentett beállításokkal).
    private ExperimentSettings BuildCommittedSettings()
    {
        _view.CommitPendingEdits();
        return BuildSettings(_initialSettings, _view.ReadValues(commit: true), _fixationPoint, _photodiodeMarkerCenter).Settings;
    }

    // Igazat ad, ha a kutató kijelölt egy pontot.
    private bool PickFixationPoint()
    {
        if (_view.PickFixationPoint(BuildCommittedSettings(), _fixationPoint) is not { } point)
        {
            return false;
        }

        _fixationPoint = point;
        Refresh();
        return true;
    }

    // Ha a kutató maga választja az egyéni helyet, és még nincs kijelölt pont, rögtön megnyílik a
    // kijelölő; ha megszakítja, a fixációs pont visszakerül középre.
    private void HandleCustomFixationPlacementSelected()
    {
        if (_fixationPoint.HasValue || PickFixationPoint())
        {
            return;
        }

        _view.SelectCenterFixationPlacement();
        Refresh();
    }

    // A kutató a kereszthez és a háttérhez képest látja a jelzőt.
    private void PickPhotodiodeMarker()
    {
        if (!_view.TryPickPhotodiodeMarker(BuildCommittedSettings(), out var center))
        {
            return;
        }

        _photodiodeMarkerCenter = center;
        Refresh();
    }

    private void ResetPhotodiodeMarker()
    {
        _photodiodeMarkerCenter = null;
        Refresh();
    }

    private void Save()
    {
        _view.CommitPendingEdits();
        var evaluation = EvaluateView(commit: true);
        _view.ApplyState(CreateViewState(evaluation));
        if (evaluation.Errors.Count > 0)
        {
            _view.SelectSection(evaluation.Errors[0].Section);
            return;
        }

        try
        {
            _settingsProvider.Save(evaluation.Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _view.ShowSaveFailed(_settingsProvider.SettingsFilePath, ex);
            return;
        }
        catch (ArgumentException ex)
        {
            // A tároló a saját ellenőrzése szerint érvénytelennek találta a beállításokat, pedig az itteni
            // ellenőrzés hibátlannak: ez programhiba, a módosítások az ablakban megmaradnak.
            Logger.Error("A beállítási ablak által ellenőrzött beállításokat a tároló elutasította.", ex);
            _view.ShowUnexpectedError(ex);
            return;
        }

        _view.CloseAsSaved();
    }
}

// Egy ellenőrzés eredménye; módosításonként egyszer készül, ebből frissül minden kijelzés.
public sealed record SettingsEvaluation(
    ExperimentSettings Settings,
    IReadOnlyList<SettingsIssue> Errors,
    IReadOnlyList<SettingsIssue> Warnings,
    StageFeasibility? Feasibility);
