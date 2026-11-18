using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure;

// A mérési beállítások tárolása egy JSON-fájlban (beallitasok.json). Ha a fájl nem létezik, a betöltés
// létrehozza az alapértékekkel; a kutató a Testreszabás ablakban vagy kézzel szerkesztheti.
//
// A beolvasás szigorú: ismeretlen (pl. elgépelt) mező, rossz típusú érték vagy számmal megadott
// felsorolási érték hiba, mert különben a beállítás csendben alapértékre esne vissza, és a mérés nem
// azzal futna, amit a kutató beállított. A megjegyzések (//, /* */) és a záró vesszők megengedettek; a
// programból mentett fájlban a megjegyzések nem maradnak meg, de az előző változat .bak másolatként igen.
//
// Az 1-es formátumú (látószög alapú, NormalMode/PeripheralMode szakaszos) fájlt a program már nem
// alakítja át: a betöltés hibát jelez, és a mérést a Testreszabásban újra be kell állítani.
internal sealed class ExperimentSettingsStore : IExperimentSettingsProvider
{
    // Az 1-es formátum gyökérszintű mezői; a mostani formátumban egyik sem fordulhat elő.
    private static readonly string[] LegacyRootMarkers =
    {
        "NormalMode", "PeripheralMode", "DefaultViewingDistanceCm", "MinValidGazeSampleRatio"
    };

    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ExperimentSettingsStore(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
    }

    public string SettingsFilePath { get; }

    // Soha nem dob kivételt. Ha az Error ki van töltve, a mérés nem indulhat el csendben alapértékekkel,
    // mert az eltérne attól, amit a kutató beállított.
    public SettingsLoadResult Load()
    {
        try
        {
            return LoadCore();
        }
        catch (JsonException ex)
        {
            return Failed(FormatJsonError(ex));
        }
        catch (Exception ex) when (ex is ArgumentException
            || ex is InvalidOperationException
            || ex is FormatException
            || ex is NotSupportedException
            || ex is IOException
            || ex is UnauthorizedAccessException)
        {
            return Failed($"A beállításfájl nem olvasható: {ex.Message}");
        }
    }

    // Csak érvényes beállítás menthető. A színek egységes "#RRGGBB" alakban, a formátumverzió a mostani
    // értékkel kerül a fájlba; a hívó példánya nem változik.
    public void Save(ExperimentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "A beállítások érvénytelenek, ezért nem menthetők:" + Environment.NewLine + string.Join(Environment.NewLine, errors),
                nameof(settings));
        }

        var normalized = settings.Clone();
        normalized.SettingsVersion = ExperimentSettings.CurrentVersion;
        normalized.Stimulus.Color = NormalizeColor(normalized.Stimulus.Color);
        normalized.Stimulus.BackgroundColor = NormalizeColor(normalized.Stimulus.BackgroundColor);
        normalized.Hardware.EspPortName = string.IsNullOrWhiteSpace(normalized.Hardware.EspPortName)
            ? null
            : normalized.Hardware.EspPortName.Trim();

        WriteSettingsFile(normalized);
    }

    private SettingsLoadResult LoadCore()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var defaults = new ExperimentSettings();
            try
            {
                WriteSettingsFile(defaults);
                Logger.Info($"Alapértelmezett mérési beállítások létrehozva: {SettingsFilePath}");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warning($"A beállításfájl nem hozható létre ({SettingsFilePath}): {ex.Message}");
            }

            return new SettingsLoadResult(defaults, null);
        }

        var text = DecodeText(DurableFile.Retry("A beállításfájl olvasása", () => File.ReadAllBytes(SettingsFilePath)));

        // Először csak a szerkezet: a formátumverzió és a régi formátum jelei a teljes deszerializálás
        // előtt kiderülnek, így egy régi vagy újabb fájl nem "ismeretlen mező" hibaként jelenik meg.
        if (JsonNode.Parse(text, NodeOptions, DocumentOptions) is not JsonObject root)
        {
            return Failed("A beállításfájl tartalma nem JSON-objektum: a teljes tartalomnak { és } között kell állnia.");
        }

        try
        {
            EnsureNoDuplicateProperties(root);
        }
        catch (ArgumentException ex)
        {
            return Failed(
                "A beállításfájlban egy mező kétszer szerepel (a mezőnevekben a kis- és nagybetű nem számít), így nem dönthető el, "
                + $"melyik érték érvényes. Részletek: {ex.Message}");
        }

        int? version = null;
        if (root.TryGetPropertyValue(nameof(ExperimentSettings.SettingsVersion), out var versionNode))
        {
            if (versionNode is not JsonValue versionValue || !versionValue.TryGetValue<int>(out var parsedVersion))
            {
                return Failed(
                    $"A beállításfájl SettingsVersion mezője egész szám legyen (a mostani formátumé {ExperimentSettings.CurrentVersion}).");
            }

            version = parsedVersion;
        }

        if (version > ExperimentSettings.CurrentVersion)
        {
            return Failed(
                $"A beállításfájl egy újabb programverzióhoz készült (SettingsVersion = {version}), ez a programverzió legfeljebb a "
                + $"{ExperimentSettings.CurrentVersion}-es formátumot ismeri. Használd az újabb programot, vagy állítsd be újra a mérést a "
                + "Testreszabásban (mentéskor a mostani fájl .bak kiterjesztéssel megmarad).");
        }

        if ((version is null || version <= 1) && LegacyRootMarkers.Any(root.ContainsKey))
        {
            return Failed(
                "A beállításfájl régi (1-es) formátumú, amelyet ez a programverzió már nem tud beolvasni. Állítsd be újra a mérést a "
                + "Testreszabásban (mentéskor a mostani fájl .bak kiterjesztéssel megmarad).");
        }

        // Az eredeti szövegből (nem a JsonNode-ból) deszerializál, hogy a hibaüzenet sorszáma a fájl
        // sorára mutasson.
        var settings = JsonSerializer.Deserialize<ExperimentSettings>(text, JsonOptions);
        if (settings is null)
        {
            return Failed("A beállításfájl üres.");
        }

        settings.SettingsVersion = ExperimentSettings.CurrentVersion;
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            ReplaceNullsWithDefaults(settings);
            var message = string.Join(Environment.NewLine, errors);
            Logger.Warning($"A mérési beállítások hibásak ({SettingsFilePath}):{Environment.NewLine}{message}");
            return new SettingsLoadResult(settings, message);
        }

        return new SettingsLoadResult(settings, null);
    }

    // A JsonObject a mezőit az első hozzáféréskor tölti be, és egy ismétlődő (a kis- és nagybetűtől
    // eltekintve azonos) mezőnévnél ekkor ArgumentException-t dob. A teljes bejárás ezt egy helyen, a
    // tényleges feldolgozás előtt jelzi.
    private static void EnsureNoDuplicateProperties(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject)
                {
                    EnsureNoDuplicateProperties(property.Value);
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    EnsureNoDuplicateProperties(item);
                }

                break;
        }
    }

    private SettingsLoadResult Failed(string message)
    {
        Logger.Warning($"A mérési beállítások nem tölthetők be ({SettingsFilePath}): {message}");
        return new SettingsLoadResult(new ExperimentSettings(), message);
    }

    private static string FormatJsonError(JsonException exception)
    {
        var location = new List<string>();
        if (exception.LineNumber is { } lineNumber)
        {
            location.Add($"{lineNumber + 1}. sor");
        }

        if (!string.IsNullOrEmpty(exception.Path))
        {
            location.Add(exception.Path);
        }

        var where = location.Count == 0 ? string.Empty : $" ({string.Join(", ", location)})";
        return $"A beállításfájl nem olvasható{where}: hibás JSON-szintaxis, ismeretlen mező vagy rossz típusú érték. "
            + $"Részletek: {exception.Message}";
    }

    // A Validate a null szakaszokat és színeket hibaként jelzi; a visszaadott példányban ezek helyén
    // alapérték áll, hogy a beállítási ablak a többi, beolvasott értéket megjeleníthesse.
    private static void ReplaceNullsWithDefaults(ExperimentSettings settings)
    {
        var defaults = new ExperimentSettings();
        settings.Trials ??= defaults.Trials;
        settings.Stimulus ??= defaults.Stimulus;
        settings.Gaze ??= defaults.Gaze;
        settings.Hardware ??= defaults.Hardware;
        settings.Stimulus.Color ??= defaults.Stimulus.Color;
        settings.Stimulus.BackgroundColor ??= defaults.Stimulus.BackgroundColor;
    }

    private static string NormalizeColor(string text)
    {
        return HexColor.TryParse(text, out var color) ? HexColor.Format(color) : text;
    }

    // UTF-8 az alapértelmezés; a bájtsorrend-jelzőt (BOM) felismeri, így a pl. a Jegyzettömbbel
    // UTF-16 kódolással mentett fájl is olvasható.
    private static string DecodeText(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    // Atomikus írás (DurableFile.ReplaceAtomically: lemezre ürített .tmp fájl cseréje a célfájlra,
    // átmeneti zárolásnál újrapróbálva), így egy írás közbeni hiba sosem hagy félig írt beállításfájlt. A
    // felülírt változat <fájl>.bak néven megmarad, így a kézi szerkesztés (megjegyzésekkel) vagy egy újabb
    // programverzió fájlja egy mentés után sem vész el.
    private void WriteSettingsFile(ExperimentSettings settings)
    {
        var fullPath = Path.GetFullPath(SettingsFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var target = new FileInfo(fullPath);
        if (target.Exists && target.IsReadOnly)
        {
            throw new UnauthorizedAccessException(
                $"A beállításfájl írásvédett, ezért nem írható felül: {fullPath}. Kapcsold ki a fájl „Csak olvasható” tulajdonságát, "
                + "majd próbáld újra.");
        }

        DurableFile.ReplaceAtomically(fullPath, FileEncoding.GetBytes(JsonSerializer.Serialize(settings, JsonOptions)), fullPath + ".bak", "A beállításfájl cseréje");
        Logger.Info($"Beállításfájl kiírva: {fullPath}");
    }
}
