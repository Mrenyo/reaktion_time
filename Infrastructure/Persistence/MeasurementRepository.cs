using System.Globalization;
using System.Text;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure.Persistence;

// A mérési munkamenetek tárolása CSV-fájlokban. Az írást a MeasurementSessionWriter végzi,
// próbánként; a beolvasás a StatisticsView-t szolgálja ki.
//
// A beolvasás az oszlopfejléc NEVEI alapján történik (nem pozíció szerint), így minden
// formátumverzió olvasható:
//   1-es: nincs "# FormatVersion=" sor, 7 vagy 11 oszlop van, az időpont a "# CompletedAt=" sorban
//         áll, és nincs lezáró sor. Csak a teljesen befejezett méréseket tartalmazta, ezért itt a lezáró
//         sor hiánya "befejezett" állapotot jelent. A TotalTrials oszlop a mérés összes próbájának száma.
//   2-es: próbánként írt fájl lezáró "# Status=" sorral; ennek hiánya megszakadt mérést jelent. A
//         TargetValidTrials oszlop a kívánt ÉRVÉNYES tesztpróbák száma (az érvénytelenek helyett újabb
//         próba jött), a StimulusEccentricityDeg látószögben (fokban) adja az inger távolságát.
//   3-as: mint a 2-es, de a PlannedTestTrials a pontosan lefutó mért próbák száma (nincs pótlás), és
//         a StimulusEccentricityPx képpontban adja az inger távolságát a fixációs ponttól. Az
//         ActualForeperiodMs és a PrematureClicks oszlop a korábbi 3-as fájlokból hiányozhat (ilyenkor null).
// A MeasurementResult.PlannedTestTrials a régebbi fájlokban a TargetValidTrials / TotalTrials
// oszlopból jön (a fenti, eltérő jelentéssel); a StimulusEccentricityPx csak a saját oszlopából, mert a
// fokban mért érték a kijelző és a nézési távolság ismerete nélkül nem váltható át képpontra.
//
// A beolvasás hibatűrő: egy hibás vagy csonka próbasor (áramszünet, kézi szerkesztés) csak azt a sort
// hagyja ki (SkippedRowCount, figyelmeztetés a naplóban a fájllal és a sorszámmal). Az egész fájl csak
// akkor kerül az UnreadableFiles listába, ha a fejléce (metaadatok, oszlopfejléc) nem értelmezhető.
internal sealed class MeasurementRepository : IMeasurementRepository
{
    private static readonly string[] RequiredColumns = { "TrialNumber", "RecordedAt" };

    private readonly AppPaths _paths;

    public MeasurementRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public string MeasurementsDirectory => _paths.MeasurementsDirectory;

    public IMeasurementSessionWriter CreateSession(string modeName, DateTime startedAt, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        return MeasurementSessionWriter.Create(_paths.MeasurementsDirectory, _paths.GazeDirectory, modeName, startedAt, metadata);
    }

    public MeasurementLoadResult LoadAll()
    {
        var sessions = new List<MeasurementSessionResult>();
        var unreadable = new List<string>();
        var directories = new[] { _paths.MeasurementsDirectory, AppPaths.LegacyMeasurementsDirectory }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);

        foreach (var directory in directories)
        {
            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(directory, "*" + MeasurementSessionWriter.CsvExtension);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warning($"A mérések mappája nem olvasható, kihagyva: {directory} ({ex.Message})");
                unreadable.Add(directory);
                continue;
            }

            foreach (var filePath in filePaths)
            {
                try
                {
                    sessions.Add(ParseCsvFile(filePath));
                }
                catch (Exception ex) when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is FormatException
                    || ex is ArgumentException)
                {
                    Logger.Warning($"Mérési fájl beolvasása sikertelen, kihagyva: {filePath} ({ex.Message})");
                    unreadable.Add(filePath);
                }
            }
        }

        return new MeasurementLoadResult(sessions.OrderByDescending(session => session.StartedAt).ToArray(), unreadable);
    }

    // Az írás alatt álló (a MeasurementSessionWriter által nyitva tartott) fájl is olvasható, ezért a
    // megosztás az írást is engedi; átmeneti zárolásnál (OneDrive, víruskereső) újrapróbálkozik.
    private static List<string> ReadAllLines(string filePath)
    {
        return DurableFile.Retry($"A mérési fájl olvasása ({filePath})", () =>
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines;
        });
    }

    private static MeasurementSessionResult ParseCsvFile(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trials = new List<MeasurementResult>();
        var skippedRows = 0;
        Dictionary<string, int>? columns = null;

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                var separator = line.IndexOf('=');
                if (separator > 1)
                {
                    metadata[line[1..separator].Trim()] = line[(separator + 1)..].Trim();
                }

                continue;
            }

            if (columns is null)
            {
                columns = ParseColumnHeader(line);
                continue;
            }

            // A sorszám a fájl 1-től számozott sora, hogy a kutató a szerkesztőben megtalálja.
            try
            {
                trials.Add(ParseTrialLine(MeasurementCsv.SplitLine(line), columns));
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                skippedRows++;
                Logger.Warning($"Hibás vagy csonka próbasor kihagyva: {filePath}, {lineIndex + 1}. sor ({ex.Message})");
            }
        }

        if (columns is null)
        {
            throw new FormatException("Hiányzik a próbák oszlopfejléce.");
        }

        if (!metadata.TryGetValue("ModeName", out var modeName))
        {
            throw new FormatException("Hiányzó ModeName fejléc.");
        }

        // A metaadat-érték a sor végéig tart, idézőjelezés nélkül (MeasurementSessionWriter); csak az 1-es
        // formátum tehette idézőjelbe a mérés nevét, ezért ott az idézőjelek lekerülnek.
        var isNewFormat = metadata.ContainsKey("FormatVersion");
        if (!isNewFormat)
        {
            modeName = Unquote(modeName);
        }

        var endedAt = TryParseDate(metadata, "EndedAt") ?? TryParseDate(metadata, "CompletedAt");
        var startedAt = TryParseDate(metadata, "StartedAt")
            ?? endedAt
            ?? throw new FormatException("Hiányzó időpont (StartedAt).");

        var status = metadata.TryGetValue("Status", out var statusText) && Enum.TryParse(statusText, out SessionStatus parsedStatus)
            ? parsedStatus
            : isNewFormat ? SessionStatus.Interrupted : SessionStatus.Completed;

        return new MeasurementSessionResult(modeName, trials, startedAt, endedAt, status, metadata) { SkippedRowCount = skippedRows };
    }

    // Az oszlopnév -> index megfeleltetés. Ismétlődő oszlopnévnél (ArgumentException) vagy a kötelező
    // oszlopok hiányában a fájl nem értelmezhető.
    private static Dictionary<string, int> ParseColumnHeader(string line)
    {
        var columns = MeasurementCsv.SplitLine(line)
            .Select((name, index) => (name: name.Trim(), index))
            .ToDictionary(column => column.name, column => column.index, StringComparer.OrdinalIgnoreCase);

        foreach (var required in RequiredColumns)
        {
            if (!columns.ContainsKey(required))
            {
                throw new FormatException($"Hiányzó {required} oszlop a fejlécben.");
            }
        }

        return columns;
    }

    // A mezők száma a fejlécével egyezzen meg: a csonka (pl. áramszünetkor félbemaradt) sor kevesebb
    // mezőből áll, és az utolsó, félig kiírt értéke egyébként észrevétlenül hibás számként kerülne be.
    private static MeasurementResult ParseTrialLine(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> columns)
    {
        if (fields.Count != columns.Count)
        {
            throw new FormatException($"{fields.Count} mező a fejléc {columns.Count} oszlopa helyett.");
        }

        string? Get(params string[] names)
        {
            foreach (var name in names)
            {
                if (columns.TryGetValue(name, out var index))
                {
                    return fields[index];
                }
            }

            return null;
        }

        var trialNumberText = Get("TrialNumber")!;
        var recordedAtText = Get("RecordedAt")!;

        var invalidReason = TrialInvalidReason.None;
        var reasonText = Get("InvalidReason");
        if (!string.IsNullOrEmpty(reasonText))
        {
            invalidReason = Enum.TryParse(reasonText, out TrialInvalidReason parsedReason) ? parsedReason : TrialInvalidReason.Unspecified;
        }
        else if (Get("Valid") is { Length: > 0 } validText && !bool.Parse(validText))
        {
            invalidReason = TrialInvalidReason.Unspecified;
        }

        return new MeasurementResult
        {
            Phase = Enum.TryParse(Get("Phase"), out TrialPhase phase) ? phase : TrialPhase.Test,
            TrialNumber = int.Parse(trialNumberText, CultureInfo.InvariantCulture),
            PlannedTestTrials = ParseInt(Get("PlannedTestTrials", "TargetValidTrials", "TotalTrials")) ?? 0,
            EspReactionTimeMs = ParseDouble(Get("EspReactionTimeMs")),
            LocalReactionTimeMs = ParseDouble(Get("LocalReactionTimeMs")),
            RecordedAt = DateTime.Parse(recordedAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            InvalidReason = invalidReason,
            ForeperiodMs = ParseInt(Get("ForeperiodMs")),
            ActualForeperiodMs = ParseDouble(Get("ActualForeperiodMs")),
            PrematureClicks = ParseInt(Get("PrematureClicks")),
            ScanoutCorrectionUs = ParseDouble(Get("ScanoutCorrectionUs")),
            StimulusBounds = ParseStimulusBounds(Get("StimulusCenterX"), Get("StimulusCenterY"), Get("StimulusDiameterPx")),
            StimulusEccentricityPx = ParseDouble(Get("StimulusEccentricityPx")),
            ValidGazeRatio = ParseDouble(Get("ValidGazeRatio")),
            GazeSamplesInWindow = ParseInt(Get("GazeSamplesInWindow")),
            MeanEyeDistanceMm = ParseDouble(Get("MeanEyeDistanceMm")),
            ClickPoint = ParsePoint(Get("ClickX"), Get("ClickY"))
        };
    }

    // A régebbi fájlokban nincs ClickX/ClickY oszlop: ilyenkor null.
    private static PointF? ParsePoint(string? xText, string? yText)
    {
        var x = ParseDouble(xText);
        var y = ParseDouble(yText);
        return x.HasValue && y.HasValue ? new PointF((float)x.Value, (float)y.Value) : null;
    }

    private static RectangleF? ParseStimulusBounds(string? centerXText, string? centerYText, string? diameterText)
    {
        var centerX = ParseDouble(centerXText);
        var centerY = ParseDouble(centerYText);
        var diameter = ParseDouble(diameterText);
        if (!centerX.HasValue || !centerY.HasValue || !diameter.HasValue)
        {
            return null;
        }

        return new RectangleF(
            (float)(centerX.Value - (diameter.Value / 2d)),
            (float)(centerY.Value - (diameter.Value / 2d)),
            (float)diameter.Value,
            (float)diameter.Value);
    }

    private static DateTime? TryParseDate(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var text)
            && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
                ? value
                : null;
    }

    private static double? ParseDouble(string? field)
    {
        return string.IsNullOrEmpty(field) ? null : double.Parse(field, CultureInfo.InvariantCulture);
    }

    private static int? ParseInt(string? field)
    {
        return string.IsNullOrEmpty(field) ? null : int.Parse(field, CultureInfo.InvariantCulture);
    }

    // CSV-szerű idézőjelezés eltávolítása ("a ""b"" c" -> a "b" c); csak az 1-es formátumú fájlokhoz.
    private static string Unquote(string value)
    {
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1].Replace("\"\"", "\"")
            : value;
    }
}

// A mérési CSV-fájlok közös segédfüggvényei (írás és olvasás).
internal static class MeasurementCsv
{
    // Egy próba- vagy fejlécsor mezőkre bontása. Az idézőjeles mezőt (benne vesszővel, "" jelöli az
    // idézőjelet) is kezeli: a 3-as formátum nem ír ilyet, de a 2-es fájlokban és egy kézzel vagy
    // Excellel szerkesztett fájlban előfordulhat.
    internal static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    internal static string SanitizeForFileName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? c : '-');
        }

        // Itt már csak betű, számjegy és kötőjel van, ezek mind megengedettek fájlnévben.
        var sanitized = builder.ToString();
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "meres" : sanitized;
    }
}
