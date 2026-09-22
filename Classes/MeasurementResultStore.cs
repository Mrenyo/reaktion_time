using System.Globalization;
using System.Text;

namespace ReactionTimeMeasurementApp.Classes;

// A mentett mérési munkamenetek beolvasása a StatisticsView számára. Az írást a
// MeasurementSessionWriter végzi, próbánként.
//
// A beolvasás az oszlopfejléc NEVEI alapján történik (nem pozíció szerint), így a korábbi
// formátumú fájlok (7 vagy 11 oszlop, "# CompletedAt=" fejléc, lezáró sor nélkül) is olvashatók
// maradnak. A korábbi verziók csak a teljesen befejezett méréseket mentették, ezért a lezáró sor
// nélküli régi fájl "befejezett"; az új formátumban a hiányzó lezáró sor megszakadt mérést jelent.
internal static class MeasurementResultStore
{
    internal static IReadOnlyList<MeasurementSessionResult> LoadAll()
    {
        var sessions = new List<MeasurementSessionResult>();
        var directories = new[] { DataLocations.MeasurementsDirectory, DataLocations.LegacyMeasurementsDirectory }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);

        foreach (var directory in directories)
        {
            foreach (var filePath in Directory.EnumerateFiles(directory, "*" + MeasurementSessionWriter.CsvExtension))
            {
                try
                {
                    sessions.Add(ParseCsvFile(filePath));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
                {
                    Logger.Warning($"Mérési fájl beolvasása sikertelen, kihagyva: {filePath} ({ex.Message})");
                }
            }
        }

        return sessions
            .OrderByDescending(session => session.StartedAt)
            .ToArray();
    }

    private static MeasurementSessionResult ParseCsvFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trials = new List<MeasurementResult>();
        Dictionary<string, int>? columns = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
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
                columns = MeasurementCsv.SplitLine(line)
                    .Select((name, index) => (name: name.Trim(), index))
                    .ToDictionary(column => column.name, column => column.index, StringComparer.OrdinalIgnoreCase);
                continue;
            }

            trials.Add(ParseTrialLine(MeasurementCsv.SplitLine(line), columns, line));
        }

        if (!metadata.TryGetValue("ModeName", out var modeName))
        {
            throw new FormatException($"Hiányzó ModeName fejléc a mérési fájlban: {filePath}");
        }

        modeName = MeasurementCsv.UnescapeField(modeName);
        var isNewFormat = metadata.ContainsKey("FormatVersion");
        var endedAt = TryParseDate(metadata, "EndedAt") ?? TryParseDate(metadata, "CompletedAt");
        var startedAt = TryParseDate(metadata, "StartedAt")
            ?? endedAt
            ?? throw new FormatException($"Hiányzó időpont a mérési fájlban: {filePath}");

        SessionStatus status;
        if (metadata.TryGetValue("Status", out var statusText) && Enum.TryParse(statusText, out SessionStatus parsedStatus))
        {
            status = parsedStatus;
        }
        else
        {
            status = isNewFormat ? SessionStatus.Interrupted : SessionStatus.Completed;
        }

        metadata["SourceFile"] = filePath;
        return new MeasurementSessionResult(modeName, trials, startedAt, endedAt, status, metadata);
    }

    private static MeasurementResult ParseTrialLine(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> columns, string line)
    {
        string? Get(params string[] names)
        {
            foreach (var name in names)
            {
                if (columns.TryGetValue(name, out var index) && index < fields.Count)
                {
                    return fields[index];
                }
            }

            return null;
        }

        var trialNumberText = Get("TrialNumber") ?? throw new FormatException($"Érvénytelen sor a mérési fájlban: {line}");
        var recordedAtText = Get("RecordedAt") ?? throw new FormatException($"Érvénytelen sor a mérési fájlban: {line}");

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

        var scanout = Get("ScanoutCorrectionUs");

        return new MeasurementResult
        {
            Phase = Enum.TryParse(Get("Phase"), out TrialPhase phase) ? phase : TrialPhase.Test,
            TrialNumber = int.Parse(trialNumberText, CultureInfo.InvariantCulture),
            TargetValidTrials = ParseInt(Get("TargetValidTrials", "TotalTrials")) ?? 0,
            EspReactionTimeMs = ParseDouble(Get("EspReactionTimeMs")),
            LocalReactionTimeMs = ParseDouble(Get("LocalReactionTimeMs")),
            RecordedAt = DateTime.Parse(recordedAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            InvalidReason = invalidReason,
            ForeperiodMs = ParseInt(Get("ForeperiodMs")),
            ScanoutCorrectionUs = string.IsNullOrEmpty(scanout) ? null : MeasurementCsv.UnescapeField(scanout),
            StimulusBounds = ParseStimulusBounds(Get("StimulusCenterX"), Get("StimulusCenterY"), Get("StimulusDiameterPx")),
            StimulusEccentricityDeg = ParseDouble(Get("StimulusEccentricityDeg")),
            ValidGazeRatio = ParseDouble(Get("ValidGazeRatio")),
            GazeSamplesInWindow = ParseInt(Get("GazeSamplesInWindow")),
            MeanEyeDistanceMm = ParseDouble(Get("MeanEyeDistanceMm"))
        };
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

internal static class MeasurementCsv
{
    internal static string EscapeField(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    internal static string UnescapeField(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\"\"", "\"");
        }

        return value;
    }

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

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(builder.ToString().Where(c => !invalidChars.Contains(c)).ToArray());

        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "meres" : sanitized;
    }
}
