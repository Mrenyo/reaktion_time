using System.Globalization;
using System.Text;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure.Persistence;

// A mérési munkamenetek tárolása CSV-fájlokban. Az írást a MeasurementSessionWriter végzi,
// próbánként; a beolvasás a StatisticsView-t szolgálja ki.
//
// Csak a 3-as formátumú ("# FormatVersion=3") fájlok olvashatók; a régebbi (1.x programmal készült)
// fájlok az UnreadableFiles listába kerülnek. A beolvasás az oszlopfejléc NEVEI alapján történik (nem
// pozíció szerint); a 2.0.0 fejlesztése közben készült 3-as fájlokból hiányzó oszlop (pl.
// ActualForeperiodMs, PrematureClicks, ClickX/ClickY) értéke null. A lezáró "# Status=" sor hiánya
// megszakadt mérést jelent.
//
// A beolvasás hibatűrő: egy hibás vagy csonka próbasor (áramszünet, kézi szerkesztés) csak azt a sort
// hagyja ki (SkippedRowCount, figyelmeztetés a naplóban a fájllal és a sorszámmal). Az egész fájl csak
// akkor kerül az UnreadableFiles listába, ha a fejléce (metaadatok, oszlopfejléc) nem értelmezhető.
internal sealed class MeasurementRepository : IMeasurementRepository
{
    private const string SupportedFormatVersion = "3";

    private static readonly string[] RequiredColumns = { "Phase", "TrialNumber", "RecordedAt" };

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
        var directory = _paths.MeasurementsDirectory;
        if (!Directory.Exists(directory))
        {
            return new MeasurementLoadResult(sessions, unreadable);
        }

        string[] filePaths;
        try
        {
            filePaths = Directory.GetFiles(directory, "*" + MeasurementSessionWriter.CsvExtension);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.Warning($"A mérések mappája nem olvasható, kihagyva: {directory} ({ex.Message})");
            unreadable.Add(directory);
            return new MeasurementLoadResult(sessions, unreadable);
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
                // A formátumverzió az oszlopfejléc előtti metaadatok között áll; a régebbi fájlok
                // oszlopait a program már nem értelmezi.
                if (!metadata.TryGetValue("FormatVersion", out var version) || version != SupportedFormatVersion)
                {
                    throw new FormatException($"Nem támogatott (régi) mérési formátum: FormatVersion={version ?? "nincs"}.");
                }

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

        var endedAt = TryParseDate(metadata, "EndedAt");
        var startedAt = TryParseDate(metadata, "StartedAt")
            ?? throw new FormatException("Hiányzó időpont (StartedAt).");

        var status = metadata.TryGetValue("Status", out var statusText) && Enum.TryParse(statusText, out SessionStatus parsedStatus)
            ? parsedStatus
            : SessionStatus.Interrupted;

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

        string? Get(string name) => columns.TryGetValue(name, out var index) ? fields[index] : null;

        var trialNumberText = Get("TrialNumber")!;
        var recordedAtText = Get("RecordedAt")!;

        var reasonText = Get("InvalidReason");
        var invalidReason = string.IsNullOrEmpty(reasonText)
            ? TrialInvalidReason.None
            : Enum.TryParse(reasonText, out TrialInvalidReason parsedReason) ? parsedReason : TrialInvalidReason.Unspecified;

        var phaseText = Get("Phase")!;
        if (!Enum.TryParse(phaseText, out TrialPhase phase))
        {
            throw new FormatException($"Ismeretlen Phase érték: {phaseText}.");
        }

        return new MeasurementResult
        {
            Phase = phase,
            TrialNumber = int.Parse(trialNumberText, CultureInfo.InvariantCulture),
            PlannedTestTrials = ParseInt(Get("PlannedTestTrials")) ?? 0,
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

    // Hiányzó ClickX/ClickY oszlopnál vagy üres mezőnél null.
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
}

// A mérési CSV-fájlok közös segédfüggvényei (írás és olvasás).
internal static class MeasurementCsv
{
    // Egy próba- vagy fejlécsor mezőkre bontása. Az idézőjeles mezőt (benne vesszővel, "" jelöli az
    // idézőjelet) is kezeli: a program nem ír ilyet, de egy kézzel vagy Excellel szerkesztett fájlban
    // előfordulhat.
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
