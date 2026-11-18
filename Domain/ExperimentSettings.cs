using System.Globalization;
using System.Text.Json.Serialization;

namespace ReactionTimeMeasurementApp.Domain;

public enum ForeperiodDistribution
{
    // Egyenletes eloszlás [ForeperiodMinMs, ForeperiodMaxMs] között.
    Uniform,

    // Csonkolt exponenciális eloszlás: az inger megjelenésének feltételes valószínűsége a várakozás
    // során (közel) állandó, így a résztvevő nem tudja kiszámítani, mikor jön az inger.
    Exponential
}

// Mi számít a résztvevő válaszának.
public enum ResponseMode
{
    // Kattintás bárhol a mérőfelületen, amint megjelenik az inger. Az egérmutató mérés közben nem látszik.
    ClickAnywhere,

    // Kattintás az ingerre. A mellékattintás is válasz (az inger eltűnik), de a próba érvénytelen
    // (TrialInvalidReason.MissedStimulus). Az egérmutató mérés közben látszik; a program nem mozgatja,
    // így minden próba onnan indul, ahol az előző véget ért.
    ClickOnStimulus
}

// A próba tekintet alapú érvényesség-ellenőrzése. A távolságok a mérőfelület képpontjaiban értendők.
public enum GazeValidationMode
{
    // Nincs tekintet-ellenőrzés: minden, az időkorláton belül érkező válasz érvényes. Ehhez nem kell
    // szemkövető és kalibráció.
    None,

    // "Normál reakcióidő": a résztvevő ránéz az ingerre. A próba érvényes, ha a válaszablak legalább egy
    // érvényes tekintetmintájára Distance(tekintet, ingerközéppont) <= DiameterPx / 2 + ValidityRadiusPx.
    // A rádiusz így az inger szélén túl elfogadott tekintethiba, és nem függ az inger méretétől.
    LookAtStimulus,

    // "Periférikus reakcióidő": a résztvevő végig a fixációs pontot nézi. A próba érvényes, ha a
    // válaszablak MINDEN érvényes tekintetmintájára Distance(tekintet, fixációs pont) <= ValidityRadiusPx.
    KeepFixation
}

public enum FixationPlacement
{
    // A mérőfelület közepe.
    Center,

    // A mérőfelületen kijelölt pont (GazeSettings.FixationPointX/Y); csak KeepFixation módban
    // érvényesül, lásd GazeSettings.EffectivePlacement.
    Custom
}

// A próbák száma és időzítése.
public sealed class TrialSettings
{
    // A mért (eredménybe számító) ingerek pontos száma. Az érvénytelen próbák nem pótlódnak, csak az
    // érvénytelenség okával együtt rögzülnek.
    public int MeasuredTrials { get; set; } = 10;

    // A mért ingerek ELŐTT megjelenő gyakorló ingerek száma. Ezek is mentésre kerülnek
    // (Phase=Practice), de az eredménybe nem számítanak.
    public int PracticeTrials { get; set; } = 3;

    public ResponseMode ResponseMode { get; set; } = ResponseMode.ClickAnywhere;

    // Ennyi ideig vár a mérés a válaszra; utána az inger eltűnik, és a próba érvénytelen (Timeout).
    public int ReactionTimeoutMs { get; set; } = 3000;

    // A mérési jelenet (beállított háttér, kereszt, fekete fotodióda-jelző) megjelenése után ennyit
    // vár a mérés az első próba előtt: a szem alkalmazkodik az új háttérhez, a szemkövető beáll.
    public int SceneSettleDelayMs { get; set; } = 2000;

    public ForeperiodDistribution ForeperiodDistribution { get; set; } = ForeperiodDistribution.Exponential;

    public int ForeperiodMinMs { get; set; } = 1000;

    public int ForeperiodMaxMs { get; set; } = 3000;

    // Exponenciális eloszlásnál a minimum feletti rész várható értéke (a csonkolás előtt).
    public int ForeperiodExponentialMeanMs { get; set; } = 700;

    // A tagonkénti másolás minden tulajdonságot átvesz, így egy új beállítás sem maradhat ki belőle.
    public TrialSettings Clone() => (TrialSettings)MemberwiseClone();
}

// Az inger megjelenése és helye. Minden méret és távolság a mérőfelület képpontjaiban értendő; a
// fizikai méret a monitortól függ (a mérés metaadatai a kijelző fizikai méretét is rögzítik, ha ismert).
public sealed class StimulusSettings
{
    // "#RRGGBB" alakban, lásd HexColor.
    public string Color { get; set; } = "#FFFFFF";

    // A mérési jelenet háttérszíne, "#RRGGBB" alakban.
    public string BackgroundColor { get; set; } = "#1E1E1E";

    // Az inger (kitöltött kör) átmérője.
    public int DiameterPx { get; set; } = 60;

    // Az inger középpontjának távolsága a fixációs ponttól. Próbánként egyenletes eloszlásból sorsolt
    // érték a legkisebb és a képernyőn még elférő legnagyobb távolság között (lásd StimulusPlacement).
    public int MinEccentricityPx { get; set; } = 150;

    public int MaxEccentricityPx { get; set; } = 500;

    // A tagonkénti másolás minden tulajdonságot átvesz, így egy új beállítás sem maradhat ki belőle.
    public StimulusSettings Clone() => (StimulusSettings)MemberwiseClone();
}

// A tekintet-ellenőrzés és a fixációs kereszt helye.
public sealed class GazeSettings
{
    public GazeValidationMode Validation { get; set; } = GazeValidationMode.LookAtStimulus;

    // A fixációs kereszt helyének beállítása; a mérésben érvényes helyet az EffectivePlacement adja.
    public FixationPlacement FixationPlacement { get; set; } = FixationPlacement.Center;

    // A kijelölt fixációs pont a mérőfelület szélességéhez, illetve magasságához viszonyítva (0..1). A két
    // érték együtt null (még nem jelöltek ki pontot) vagy együtt kitöltött. A pont akkor is megmarad, ha
    // éppen nem érvényesül, így a mód visszaváltásakor nem kell újra kijelölni.
    public double? FixationPointX { get; set; }

    public double? FixationPointY { get; set; }

    // LookAtStimulus módban az inger szélén túl elfogadott tekintethiba, KeepFixation módban a fixációs
    // pont körüli megengedett eltérés.
    public int ValidityRadiusPx { get; set; } = 80;

    // Ha a válaszablak (inger megjelenése -> válasz) tekintetmintáinak ennél kisebb hányada érvényes
    // (pislogás, elveszett szem, nincs adat), a tekintet alapján nem dönthető el, hogy a résztvevő
    // betartotta-e az utasítást, ezért a próba érvénytelen.
    public double MinValidGazeSampleRatio { get; set; } = 0.5;

    // A kijelölt pont, vagy null, ha még nincs kijelölve. Számított érték, ezért nem kerül a beállításfájlba.
    [JsonIgnore]
    public NormalizedPoint? FixationPointCenter =>
        FixationPointX is { } x && FixationPointY is { } y ? new NormalizedPoint(x, y) : null;

    // A kijelölt fixációs pont csak akkor érvényesül, ha a résztvevőnek a fixációs pontot kell néznie,
    // az egyéni hely van kiválasztva, és már ki is jelöltek egy pontot; minden más esetben a kereszt
    // középen van. Számított érték, ezért nem kerül a beállításfájlba.
    [JsonIgnore]
    public FixationPlacement EffectivePlacement =>
        Validation == GazeValidationMode.KeepFixation
        && FixationPlacement == FixationPlacement.Custom
        && FixationPointX.HasValue
        && FixationPointY.HasValue
            ? FixationPlacement.Custom
            : FixationPlacement.Center;

    // A fixációs kereszt helye a mérőfelület koordinátáiban, egész képpontra kerekítve. Ezt használja a
    // mérés, az előnézet és a kijelölő is, így mindenhol pontosan ugyanoda kerül a kereszt.
    public PointF ResolveFixationPoint(Size stageSize)
    {
        if (EffectivePlacement == FixationPlacement.Custom && FixationPointX is { } x && FixationPointY is { } y)
        {
            return new PointF(
                MathF.Round((float)(stageSize.Width * x)),
                MathF.Round((float)(stageSize.Height * y)));
        }

        return new PointF(MathF.Round(stageSize.Width * 0.5f), MathF.Round(stageSize.Height * 0.5f));
    }

    // A tagonkénti másolás minden tulajdonságot átvesz, így egy új beállítás sem maradhat ki belőle.
    public GazeSettings Clone() => (GazeSettings)MemberwiseClone();
}

// A mérőhardver (ESP) beállításai.
public sealed class HardwareSettings
{
    // Az ESP soros portja (pl. "COM5"); üresen hagyva a program az USB-eszközazonosítók alapján
    // maga keresi meg.
    public string? EspPortName { get; set; }

    // Az ESP firmware-ben beállított sebességgel egyezzen.
    public int EspBaudRate { get; set; } = 115200;

    // A fotodióda-jelző középpontja a mérőfelület szélességéhez, illetve magasságához viszonyítva (0..1),
    // oda, ahová a fizikai fotodióda fel van szerelve. A két érték együtt null (alapértelmezett hely: a
    // jobb alsó sarok) vagy együtt kitöltött. Lásd PhotodiodeMarker.GetBounds.
    public double? PhotodiodeMarkerX { get; set; }

    public double? PhotodiodeMarkerY { get; set; }

    // A beállított hely, vagy null az alapértelmezett helyhez. Számított érték, ezért nem kerül a
    // beállításfájlba.
    [JsonIgnore]
    public NormalizedPoint? PhotodiodeMarkerCenter =>
        PhotodiodeMarkerX is { } x && PhotodiodeMarkerY is { } y ? new NormalizedPoint(x, y) : null;

    // A fotodióda-jelző négyzete a mérőfelület koordinátáiban. Ezt használja a mérés (a jelző
    // kirajzolása és a scanout-korrekció), az előnézet és a kijelölők is, így mindenhol ugyanoda kerül.
    public Rectangle ResolvePhotodiodeBounds(Size stageSize) => PhotodiodeMarker.GetBounds(stageSize, PhotodiodeMarkerCenter);

    // A tagonkénti másolás minden tulajdonságot átvesz, így egy új beállítás sem maradhat ki belőle.
    public HardwareSettings Clone() => (HardwareSettings)MemberwiseClone();
}

// A mérés összes beállítása (a beallitasok.json tartalma).
public sealed class ExperimentSettings
{
    public const int CurrentVersion = 2;

    // Ennél kisebb inger-háttér kontrasztaránynál (WCAG szerinti számítás, lásd HexColor.ContrastRatio)
    // a GetWarnings figyelmeztet: az inger nehezen észrevehető lehet.
    public const double MinimumContrastRatio = 3.0;

    // A beállításfájl formátumverziója: ebből ismeri fel a betöltés a régebbi és az újabb formátumot.
    public int SettingsVersion { get; set; } = CurrentVersion;

    // A Populate miatt egy részleges JSON (pl. csak a Trials.MeasuredTrials megadva) a hiányzó
    // mezőknél az alapértéket tartja meg.
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public TrialSettings Trials { get; set; } = new();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public StimulusSettings Stimulus { get; set; } = new();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public GazeSettings Gaze { get; set; } = new();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HardwareSettings Hardware { get; set; } = new();

    // Teljes (mély) másolat: a másolat módosítása az eredetit nem érinti.
    public ExperimentSettings Clone()
    {
        var copy = (ExperimentSettings)MemberwiseClone();
        copy.Trials = Trials.Clone();
        copy.Stimulus = Stimulus.Clone();
        copy.Gaze = Gaze.Clone();
        copy.Hardware = Hardware.Clone();
        return copy;
    }

    // A beállítások ellenőrzése; üres lista, ha minden rendben van. Minden hiba megnevezi a mezőt
    // (JSON-útvonal) és a beállítások szakaszát, ahol javítható (SettingsIssue.Section). A képernyőtől
    // függő feltételeket (elfér-e az inger, jó helyen van-e a fixációs pont) a StageFeasibility ellenőrzi.
    public IReadOnlyList<SettingsIssue> Validate()
    {
        var errors = new List<SettingsIssue>();

        // 1. Hiányzó (null) szakaszok és színek: ezek nélkül a többi szabály nem értelmezhető. A
        //    deszerializálás csak akkor hagy itt null-t, ha a fájlban kifejezetten null érték áll.
        AddIfNull(errors, Trials, "Trials", SettingsSection.Trials);
        AddIfNull(errors, Stimulus, "Stimulus", SettingsSection.Appearance);
        AddIfNull(errors, Gaze, "Gaze", SettingsSection.Gaze);
        AddIfNull(errors, Hardware, "Hardware", SettingsSection.Hardware);
        StimulusSettings? stimulusSection = Stimulus;
        if (stimulusSection is not null)
        {
            if (stimulusSection.Color is null)
            {
                errors.Add(Issue("Hiányzik az inger színe (Stimulus.Color).", SettingsSection.Appearance));
            }

            if (stimulusSection.BackgroundColor is null)
            {
                errors.Add(Issue("Hiányzik a háttérszín (Stimulus.BackgroundColor).", SettingsSection.Appearance));
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        // 2. Felsorolási értékek.
        AddIfUndefined(errors, Trials.ResponseMode, "A válasz módja", "Trials.ResponseMode", SettingsSection.Trials);
        AddIfUndefined(errors, Trials.ForeperiodDistribution, "Az előperiódus eloszlása", "Trials.ForeperiodDistribution", SettingsSection.Trials);
        AddIfUndefined(errors, Gaze.Validation, "A tekintet-ellenőrzés módja", "Gaze.Validation", SettingsSection.Gaze);
        AddIfUndefined(errors, Gaze.FixationPlacement, "A fixációs pont helye", "Gaze.FixationPlacement", SettingsSection.Gaze);

        // 3. Tartományok (SettingsLimits).
        AddIfOutOfRange(errors, Trials.MeasuredTrials, SettingsLimits.MeasuredTrials.Min, SettingsLimits.MeasuredTrials.Max,
            "A mért ingerek száma", "Trials.MeasuredTrials", string.Empty, SettingsSection.Trials);
        AddIfOutOfRange(errors, Trials.PracticeTrials, SettingsLimits.PracticeTrials.Min, SettingsLimits.PracticeTrials.Max,
            "A gyakorló ingerek száma", "Trials.PracticeTrials", string.Empty, SettingsSection.Trials);
        AddIfOutOfRange(errors, Trials.ReactionTimeoutMs, SettingsLimits.ReactionTimeoutMs.Min, SettingsLimits.ReactionTimeoutMs.Max,
            "A válaszra várás leghosszabb ideje", "Trials.ReactionTimeoutMs", "ms", SettingsSection.Trials);
        AddIfOutOfRange(errors, Trials.SceneSettleDelayMs, SettingsLimits.SceneSettleDelayMs.Min, SettingsLimits.SceneSettleDelayMs.Max,
            "A mérési képernyő megjelenése utáni várakozás", "Trials.SceneSettleDelayMs", "ms", SettingsSection.Trials);
        AddIfOutOfRange(errors, Trials.ForeperiodMinMs, SettingsLimits.ForeperiodMs.Min, SettingsLimits.ForeperiodMs.Max,
            "Az előperiódus legrövidebb értéke", "Trials.ForeperiodMinMs", "ms", SettingsSection.Trials);
        AddIfOutOfRange(errors, Trials.ForeperiodMaxMs, SettingsLimits.ForeperiodMs.Min, SettingsLimits.ForeperiodMs.Max,
            "Az előperiódus leghosszabb értéke", "Trials.ForeperiodMaxMs", "ms", SettingsSection.Trials);
        AddIfOutOfRange(errors, Trials.ForeperiodExponentialMeanMs, SettingsLimits.ForeperiodExponentialMeanMs.Min,
            SettingsLimits.ForeperiodExponentialMeanMs.Max, "Az előperiódus exponenciális várható értéke",
            "Trials.ForeperiodExponentialMeanMs", "ms", SettingsSection.Trials);
        if (Trials.ForeperiodMinMs > Trials.ForeperiodMaxMs)
        {
            errors.Add(Issue(
                "Az előperiódus legrövidebb értéke (Trials.ForeperiodMinMs) nem lehet nagyobb a leghosszabbnál (Trials.ForeperiodMaxMs).",
                SettingsSection.Trials));
        }

        var diameterInRange = AddIfOutOfRange(errors, Stimulus.DiameterPx, SettingsLimits.DiameterPx.Min, SettingsLimits.DiameterPx.Max,
            "Az inger átmérője", "Stimulus.DiameterPx", "képpont", SettingsSection.Appearance);
        AddIfOutOfRange(errors, Stimulus.MinEccentricityPx, SettingsLimits.EccentricityPx.Min, SettingsLimits.EccentricityPx.Max,
            "Az inger legkisebb távolsága a fixációs ponttól", "Stimulus.MinEccentricityPx", "képpont", SettingsSection.Appearance);
        AddIfOutOfRange(errors, Stimulus.MaxEccentricityPx, SettingsLimits.EccentricityPx.Min, SettingsLimits.EccentricityPx.Max,
            "Az inger legnagyobb távolsága a fixációs ponttól", "Stimulus.MaxEccentricityPx", "képpont", SettingsSection.Appearance);
        var radiusInRange = AddIfOutOfRange(errors, Gaze.ValidityRadiusPx, SettingsLimits.ValidityRadiusPx.Min, SettingsLimits.ValidityRadiusPx.Max,
            "Az elfogadási rádiusz", "Gaze.ValidityRadiusPx", "képpont", SettingsSection.Gaze);
        AddIfOutOfRange(errors, Gaze.MinValidGazeSampleRatio, SettingsLimits.MinValidGazeSampleRatio.Min, SettingsLimits.MinValidGazeSampleRatio.Max,
            "Az érvényes tekintetminták minimális aránya", "Gaze.MinValidGazeSampleRatio", string.Empty, SettingsSection.Gaze);

        // 4. Színek. Azonos színnel az inger nem látszana.
        var stimulusColorValid = HexColor.TryParse(Stimulus.Color, out var stimulusColor);
        if (!stimulusColorValid)
        {
            errors.Add(Issue("Az inger színe (Stimulus.Color) #RRGGBB alakú legyen, pl. #FFFFFF.", SettingsSection.Appearance));
        }

        var backgroundColorValid = HexColor.TryParse(Stimulus.BackgroundColor, out var backgroundColor);
        if (!backgroundColorValid)
        {
            errors.Add(Issue("A háttérszín (Stimulus.BackgroundColor) #RRGGBB alakú legyen, pl. #1E1E1E.", SettingsSection.Appearance));
        }

        if (stimulusColorValid && backgroundColorValid && stimulusColor.ToArgb() == backgroundColor.ToArgb())
        {
            errors.Add(Issue(
                "Az inger színe (Stimulus.Color) megegyezik a háttérszínnel (Stimulus.BackgroundColor), így az inger nem látszana.",
                SettingsSection.Appearance));
        }

        // 5. A távolságtartomány iránya.
        if (Stimulus.MinEccentricityPx > Stimulus.MaxEccentricityPx)
        {
            errors.Add(Issue(
                "Az inger legkisebb távolsága (Stimulus.MinEccentricityPx) nem lehet nagyobb a legnagyobbnál (Stimulus.MaxEccentricityPx).",
                SettingsSection.Appearance));
        }

        // 6. Az inger sosem takarhatja a fixációs keresztet.
        var stimulusRadius = Stimulus.DiameterPx / 2.0;
        if (diameterInRange && Stimulus.MinEccentricityPx < stimulusRadius + FixationCross.BoundingRadiusPx)
        {
            var required = (int)Math.Ceiling(stimulusRadius + FixationCross.BoundingRadiusPx);
            errors.Add(Issue(
                $"Az inger legkisebb távolsága a fixációs ponttól (Stimulus.MinEccentricityPx) legalább {FormatNumber(required)} képpont "
                + "legyen (az inger sugara + a fixációs kereszt mérete), különben az inger a keresztet is takarhatná.",
                SettingsSection.Appearance));
        }

        // 7. Tekintet-ellenőrzésnél az inger legközelebbi helye is essen kívül az elfogadási tartományon.
        //    Különben LookAtStimulus módban a kereszt nézése is az inger nézésének számíthatna, KeepFixation
        //    módban pedig az ingerre nézés is a fixációs ablakon belül maradhatna.
        if (Gaze.Validation != GazeValidationMode.None
            && Enum.IsDefined(Gaze.Validation)
            && diameterInRange
            && radiusInRange
            && Stimulus.MinEccentricityPx <= stimulusRadius + Gaze.ValidityRadiusPx)
        {
            errors.Add(Issue(
                $"Az inger legkisebb távolsága a fixációs ponttól (Stimulus.MinEccentricityPx) legyen nagyobb "
                + $"{FormatNumber(stimulusRadius + Gaze.ValidityRadiusPx)} képpontnál (az inger sugara + az elfogadási rádiusz, "
                + "Gaze.ValidityRadiusPx), különben a tekintet-ellenőrzés nem tudja elkülöníteni a kereszt és az inger nézését.",
                SettingsSection.Appearance));
        }

        // 8. A kijelölt fixációs pont.
        var hasPointX = Gaze.FixationPointX.HasValue;
        var hasPointY = Gaze.FixationPointY.HasValue;
        if (hasPointX != hasPointY)
        {
            errors.Add(Issue(
                "A fixációs pont két koordinátáját (Gaze.FixationPointX, Gaze.FixationPointY) együtt kell megadni, vagy mindkettő legyen null.",
                SettingsSection.Gaze));
        }
        else if (hasPointX && (!IsInRange(Gaze.FixationPointX!.Value) || !IsInRange(Gaze.FixationPointY!.Value)))
        {
            errors.Add(Issue(
                $"A fixációs pont koordinátái (Gaze.FixationPointX, Gaze.FixationPointY) {FormatNumber(SettingsLimits.FixationPoint.Min)} és "
                + $"{FormatNumber(SettingsLimits.FixationPoint.Max)} közötti arányszámok legyenek (a mérőfelület szélességéhez, "
                + "illetve magasságához viszonyítva).",
                SettingsSection.Gaze));
        }

        if (Gaze.Validation == GazeValidationMode.KeepFixation
            && Gaze.FixationPlacement == FixationPlacement.Custom
            && !hasPointX
            && !hasPointY)
        {
            errors.Add(Issue("Jelölj ki fixációs pontot a mérőfelületen.", SettingsSection.Gaze));
        }

        // 9. Hardver.
        if (!string.IsNullOrWhiteSpace(Hardware.EspPortName)
            && !Hardware.EspPortName.Trim().StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(Issue(
                "Az ESP soros portja (Hardware.EspPortName) egy COM-port neve legyen (pl. \"COM5\"), vagy maradjon üresen.",
                SettingsSection.Hardware));
        }

        AddIfOutOfRange(errors, Hardware.EspBaudRate, SettingsLimits.EspBaudRate.Min, SettingsLimits.EspBaudRate.Max,
            "Az átviteli sebesség", "Hardware.EspBaudRate", "baud", SettingsSection.Hardware);

        var hasMarkerX = Hardware.PhotodiodeMarkerX.HasValue;
        var hasMarkerY = Hardware.PhotodiodeMarkerY.HasValue;
        if (hasMarkerX != hasMarkerY)
        {
            errors.Add(Issue(
                "A fotodióda-jelző helyének két koordinátáját (Hardware.PhotodiodeMarkerX, Hardware.PhotodiodeMarkerY) együtt kell "
                + "megadni, vagy mindkettő legyen null (alapértelmezett hely: jobb alsó sarok).",
                SettingsSection.Hardware));
        }
        else if (hasMarkerX && (!IsInRange(Hardware.PhotodiodeMarkerX!.Value) || !IsInRange(Hardware.PhotodiodeMarkerY!.Value)))
        {
            errors.Add(Issue(
                $"A fotodióda-jelző helyének koordinátái (Hardware.PhotodiodeMarkerX, Hardware.PhotodiodeMarkerY) "
                + $"{FormatNumber(SettingsLimits.FixationPoint.Min)} és {FormatNumber(SettingsLimits.FixationPoint.Max)} közötti "
                + "arányszámok legyenek (a mérőfelület szélességéhez, illetve magasságához viszonyítva).",
                SettingsSection.Hardware));
        }

        return errors;

        // A fixációs pont és a fotodióda-jelző helye is a mérőfelülethez viszonyított arány (0..1).
        static bool IsInRange(double value)
        {
            return double.IsFinite(value)
                && value >= SettingsLimits.FixationPoint.Min
                && value <= SettingsLimits.FixationPoint.Max;
        }
    }

    // Módszertani figyelmeztetések: a mérés ezekkel is indítható, de a kutatónak érdemes tudnia róluk.
    // Csak érvényes (Validate szerint hibátlan) beállításokra értelmes.
    public IReadOnlyList<SettingsIssue> GetWarnings()
    {
        var warnings = new List<SettingsIssue>();

        if (HexColor.TryParse(Stimulus.Color, out var stimulusColor)
            && HexColor.TryParse(Stimulus.BackgroundColor, out var backgroundColor)
            && stimulusColor.ToArgb() != backgroundColor.ToArgb())
        {
            var contrast = HexColor.ContrastRatio(stimulusColor, backgroundColor);
            if (contrast < MinimumContrastRatio)
            {
                warnings.Add(Issue(
                    $"Az inger és a háttér kontrasztja alacsony ({contrast.ToString("0.0", CultureInfo.CurrentCulture)}:1).",
                    SettingsSection.Appearance));
            }
        }

        if (Trials.ResponseMode == ResponseMode.ClickOnStimulus && Gaze.Validation == GazeValidationMode.KeepFixation)
        {
            warnings.Add(Issue(
                "Ingerre kattintásnál a résztvevőnek oda kell céloznia, a periférikus módban viszont a fixációs pontról nem nézhet "
                + "oda: így sok próba lehet tekintethiba vagy mellékattintás.",
                SettingsSection.Trials));
        }

        if (Gaze.Validation != GazeValidationMode.None && Trials.PracticeTrials == 0)
        {
            warnings.Add(Issue("Gyakorló ingerek nélkül az első mért próbák nagyobb eséllyel érvénytelenek.", SettingsSection.Trials));
        }

        return warnings;
    }

    private static SettingsIssue Issue(string message, SettingsSection section) => new(section, message);

    // A felületen megjelenő számok a felhasználó területi beállítása szerint (magyarul tizedesvesszővel).
    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static void AddIfNull(List<SettingsIssue> errors, object? value, string path, SettingsSection section)
    {
        if (value is null)
        {
            errors.Add(Issue($"A beállításfájl {path} szakasza null értékű; adj meg érvényes értékeket, vagy töröld a szakaszt.", section));
        }
    }

    private static void AddIfUndefined<TEnum>(List<SettingsIssue> errors, TEnum value, string subject, string path, SettingsSection section)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            errors.Add(Issue(
                $"{subject} ({path}) ismeretlen érték ({value}). Lehetséges értékek: {string.Join(", ", Enum.GetNames<TEnum>())}.",
                section));
        }
    }

    // Igazat ad, ha az érték a tartományban van; különben hibát ad a listához.
    private static bool AddIfOutOfRange(List<SettingsIssue> errors, double value, double min, double max, string subject, string path, string unit, SettingsSection section)
    {
        if (value >= min && value <= max)
        {
            return true;
        }

        var unitSuffix = unit.Length == 0 ? string.Empty : " " + unit;
        errors.Add(Issue($"{subject} ({path}) {FormatNumber(min)} és {FormatNumber(max)}{unitSuffix} közé essen.", section));
        return false;
    }
}
