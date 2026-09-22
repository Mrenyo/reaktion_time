using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReactionTimeMeasurementApp.Classes;

public enum ForeperiodDistribution
{
    // Egyenletes eloszlás [ForeperiodMinMs, ForeperiodMaxMs] között.
    Uniform,

    // Csonkolt exponenciális eloszlás: az inger megjelenésének feltételes valószínűsége a várakozás
    // során (közel) állandó, így a résztvevő nem tudja kiszámítani, mikor jön az inger.
    Exponential
}

// Egy mérési mód (normál / periférikus) kísérleti paraméterei. Minden szögérték látószögben (fok)
// értendő, a képpontra váltást a mérés a kijelző fizikai mérete és a nézési távolság alapján végzi.
public sealed class ModeSettings
{
    // Ennyi ÉRVÉNYES tesztpróba a cél (ha az érvénytelenek ismétlése be van kapcsolva).
    public int TestTrials { get; set; } = 10;

    // Gyakorló próbák a tesztpróbák előtt; mentésre kerülnek, de az eredménybe nem számítanak.
    public int PracticeTrials { get; set; } = 3;

    // Az érvénytelen tesztpróbák helyett újabb próba jön, amíg össze nem gyűlik TestTrials érvényes.
    public bool RepeatInvalidTrials { get; set; } = true;

    // A tesztpróbák (érvényesek + érvénytelenek) legnagyobb száma, hogy a mérés ne tartson a végtelenségig.
    public int MaxTestAttempts { get; set; } = 20;

    public ForeperiodDistribution ForeperiodDistribution { get; set; } = ForeperiodDistribution.Exponential;

    public int ForeperiodMinMs { get; set; } = 1000;

    public int ForeperiodMaxMs { get; set; } = 3000;

    // Exponenciális eloszlásnál a minimum feletti rész várható értéke (a csonkolás előtt).
    public int ForeperiodExponentialMeanMs { get; set; } = 700;

    public double StimulusDiameterDeg { get; set; } = 2;

    // Az inger középpontjának távolsága a fixációs kereszttől, látószögben.
    public double MinEccentricityDeg { get; set; } = 3;

    public double MaxEccentricityDeg { get; set; } = 15;

    // A tekintet-ellenőrzés sugara (a fixációs kereszt, illetve az inger körül).
    public double GazeValidityRadiusDeg { get; set; } = 2;

    public int ReactionTimeoutMs { get; set; } = 3000;

    internal static ModeSettings CreateNormalDefaults() => new();

    internal static ModeSettings CreatePeripheralDefaults() => new()
    {
        StimulusDiameterDeg = 1,
        MinEccentricityDeg = 8,
        MaxEccentricityDeg = 20,
        GazeValidityRadiusDeg = 4
    };

    internal IEnumerable<string> Validate(string modeName)
    {
        if (TestTrials < 1)
        {
            yield return $"{modeName}.TestTrials legalább 1 legyen.";
        }

        if (PracticeTrials < 0)
        {
            yield return $"{modeName}.PracticeTrials nem lehet negatív.";
        }

        if (RepeatInvalidTrials && MaxTestAttempts < TestTrials)
        {
            yield return $"{modeName}.MaxTestAttempts legalább akkora legyen, mint a TestTrials.";
        }

        if (ForeperiodMinMs < 0 || ForeperiodMaxMs < ForeperiodMinMs)
        {
            yield return $"{modeName}: 0 <= ForeperiodMinMs <= ForeperiodMaxMs teljesüljön.";
        }

        if (ForeperiodDistribution == ForeperiodDistribution.Exponential && ForeperiodExponentialMeanMs <= 0)
        {
            yield return $"{modeName}.ForeperiodExponentialMeanMs pozitív legyen.";
        }

        if (StimulusDiameterDeg <= 0 || StimulusDiameterDeg > 20)
        {
            yield return $"{modeName}.StimulusDiameterDeg 0 és 20 fok közé essen.";
        }

        if (MinEccentricityDeg < 0 || MaxEccentricityDeg < MinEccentricityDeg || MaxEccentricityDeg >= 80)
        {
            yield return $"{modeName}: 0 <= MinEccentricityDeg <= MaxEccentricityDeg < 80 teljesüljön.";
        }

        if (GazeValidityRadiusDeg <= 0)
        {
            yield return $"{modeName}.GazeValidityRadiusDeg pozitív legyen.";
        }

        if (ReactionTimeoutMs < 100)
        {
            yield return $"{modeName}.ReactionTimeoutMs legalább 100 legyen.";
        }
    }
}

public sealed class ExperimentSettings
{
    // Ezt a nézési távolságot akkor használja a mérés, ha a Tobii nem tudja megmérni (pl. a
    // résztvevő szemét a mérés indításakor nem látja).
    public double DefaultViewingDistanceCm { get; set; } = 60;

    // A Populate miatt egy részleges JSON (pl. csak a TestTrials megadva) a hiányzó mezőknél az
    // adott mód saját alapértékét tartja meg, nem az általános alapértéket.
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ModeSettings NormalMode { get; set; } = ModeSettings.CreateNormalDefaults();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ModeSettings PeripheralMode { get; set; } = ModeSettings.CreatePeripheralDefaults();

    internal IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (DefaultViewingDistanceCm < 20 || DefaultViewingDistanceCm > 200)
        {
            errors.Add("DefaultViewingDistanceCm 20 és 200 cm közé essen.");
        }

        errors.AddRange(NormalMode.Validate(nameof(NormalMode)));
        errors.AddRange(PeripheralMode.Validate(nameof(PeripheralMode)));
        return errors;
    }
}

// A beállításokat a DataLocations.SettingsFilePath JSON-fájlból olvassa. Ha a fájl nem létezik,
// létrehozza az alapértékekkel, így a kutató azt szerkesztheti - újrafordítás nélkül.
internal static class ExperimentSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Hiba esetén (olvashatatlan / érvénytelen fájl) az Error ki van töltve: ilyenkor a mérés NEM
    // indulhat el csendben alapértékekkel, mert az eltérne attól, amit a kutató beállított.
    internal static (ExperimentSettings Settings, string? Error) Load()
    {
        var path = DataLocations.SettingsFilePath;

        if (!File.Exists(path))
        {
            var defaults = new ExperimentSettings();
            try
            {
                Directory.CreateDirectory(DataLocations.RootDirectory);
                File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
                Logger.Info($"Alapértelmezett mérési beállítások létrehozva: {path}");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warning($"A beállításfájl nem hozható létre ({path}): {ex.Message}");
            }

            return (defaults, null);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<ExperimentSettings>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("A fájl üres.");
            var errors = settings.Validate();
            return errors.Count == 0
                ? (settings, null)
                : (new ExperimentSettings(), string.Join(Environment.NewLine, errors));
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
        {
            return (new ExperimentSettings(), ex.Message);
        }
    }
}
