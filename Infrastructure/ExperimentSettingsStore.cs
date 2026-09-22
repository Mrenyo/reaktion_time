using System.Text.Json;
using System.Text.Json.Serialization;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure;

// A kísérleti beállításokat egy JSON-fájlból olvassa. Ha a fájl nem létezik, létrehozza az
// alapértékekkel, így a kutató azt szerkesztheti - újrafordítás nélkül.
internal sealed class ExperimentSettingsStore : IExperimentSettingsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ExperimentSettingsStore(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
    }

    public string SettingsFilePath { get; }

    // Hiba esetén (olvashatatlan / érvénytelen fájl) az Error ki van töltve: ilyenkor a mérés NEM
    // indulhat el csendben alapértékekkel, mert az eltérne attól, amit a kutató beállított.
    public (ExperimentSettings Settings, string? Error) Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var defaults = new ExperimentSettings();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(defaults, JsonOptions));
                Logger.Info($"Alapértelmezett mérési beállítások létrehozva: {SettingsFilePath}");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warning($"A beállításfájl nem hozható létre ({SettingsFilePath}): {ex.Message}");
            }

            return (defaults, null);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<ExperimentSettings>(File.ReadAllText(SettingsFilePath), JsonOptions)
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
