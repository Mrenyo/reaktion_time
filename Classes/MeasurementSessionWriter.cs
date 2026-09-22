using System.Globalization;
using System.Text;

namespace ReactionTimeMeasurementApp.Classes;

// Egy mérési munkamenet fájljait írja, PRÓBÁNKÉNT: a mérés indításakor létrehozza a fájlokat a
// metaadatokkal, minden rögzített próba után azonnal hozzáfűzi a próba sorát (és a tekintetmintáit),
// a végén pedig egy lezáró "# Status=..." sort ír. Így egy megszakított mérés eddigi próbái is
// megmaradnak, egy összeomlás után pedig a hiányzó lezáró sorból látszik, hogy a mérés nem zárult le.
//
// Fájlok (DataLocations):
//   measurements\<kezdés>_<mód>.csv            - metaadatok ("# Kulcs=Érték" sorok) + próbánként egy sor
//   measurements\gaze\<kezdés>_<mód>_gaze.csv  - tekintetmintánként egy sor
//
// Minden írás külön megnyitja és lezárja a fájlt, így ami egyszer kiíródott, az a lemezen van.
// A metódusok bármelyik szálról hívhatók. Hiba esetén IOException / UnauthorizedAccessException
// kivételt dobnak - a hívónak jeleznie kell a felhasználónak, hogy az adatok nem mentődnek.
internal sealed class MeasurementSessionWriter
{
    internal const int FormatVersion = 2;
    internal const string CsvExtension = ".csv";
    internal const string GazeFileSuffix = "_gaze";

    internal static readonly string[] TrialColumns =
    {
        "Phase", "TrialNumber", "TargetValidTrials", "EspReactionTimeMs", "LocalReactionTimeMs", "Valid",
        "InvalidReason", "RecordedAt", "ForeperiodMs", "ScanoutCorrectionUs", "StimulusCenterX", "StimulusCenterY",
        "StimulusDiameterPx", "StimulusEccentricityDeg", "ValidGazeRatio", "GazeSamplesInWindow", "MeanEyeDistanceMm"
    };

    private static readonly string[] GazeColumns =
    {
        "Phase", "TrialNumber", "SystemTimeStampUs", "TimeFromOnsetMs", "InResponseWindow", "LeftEyeValid",
        "RightEyeValid", "NormalizedX", "NormalizedY", "StageX", "StageY", "EyeDistanceMm"
    };

    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly object _writeLock = new();
    private bool _closed;

    private MeasurementSessionWriter(string filePath, string gazeFilePath)
    {
        FilePath = filePath;
        GazeFilePath = gazeFilePath;
    }

    public string FilePath { get; }

    public string GazeFilePath { get; }

    internal static MeasurementSessionWriter Create(
        string modeName,
        DateTime startedAt,
        IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        Directory.CreateDirectory(DataLocations.MeasurementsDirectory);
        Directory.CreateDirectory(DataLocations.GazeDirectory);

        var filePath = BuildUniqueFilePath(startedAt, modeName);
        var gazeFilePath = Path.Combine(
            DataLocations.GazeDirectory,
            Path.GetFileNameWithoutExtension(filePath) + GazeFileSuffix + CsvExtension);

        var header = new StringBuilder();
        AppendMetadataLine(header, "FormatVersion", FormatVersion.ToString(CultureInfo.InvariantCulture));
        AppendMetadataLine(header, "ModeName", modeName);
        AppendMetadataLine(header, "StartedAt", FormatDate(startedAt));
        foreach (var (key, value) in metadata)
        {
            AppendMetadataLine(header, key, value);
        }

        header.Append(string.Join(',', TrialColumns)).Append('\n');

        var gazeHeader = new StringBuilder();
        AppendMetadataLine(gazeHeader, "ModeName", modeName);
        AppendMetadataLine(gazeHeader, "StartedAt", FormatDate(startedAt));
        AppendMetadataLine(gazeHeader, "SessionFile", Path.GetFileName(filePath));
        AppendMetadataLine(gazeHeader, "TimeBase", "Tobii SystemTimeStamp (µs); TimeFromOnsetMs = a minta ideje az inger kirajzolásához képest");
        gazeHeader.Append(string.Join(',', GazeColumns)).Append('\n');

        File.WriteAllText(filePath, header.ToString(), FileEncoding);
        File.WriteAllText(gazeFilePath, gazeHeader.ToString(), FileEncoding);
        Logger.Info($"Mérési fájl létrehozva: {filePath}");

        return new MeasurementSessionWriter(filePath, gazeFilePath);
    }

    internal void AppendTrial(MeasurementResult trial)
    {
        var row = BuildTrialRow(trial);
        var gazeRows = BuildGazeRows(trial);

        lock (_writeLock)
        {
            if (_closed)
            {
                throw new InvalidOperationException("A mérési fájl már le van zárva.");
            }

            File.AppendAllText(FilePath, row, FileEncoding);
            if (gazeRows.Length > 0)
            {
                File.AppendAllText(GazeFilePath, gazeRows, FileEncoding);
            }
        }
    }

    // Többszöri hívás esetén csak az első számít.
    internal void Close(SessionStatus status, DateTime endedAt)
    {
        lock (_writeLock)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            var footer = new StringBuilder();
            AppendMetadataLine(footer, "EndedAt", FormatDate(endedAt));
            AppendMetadataLine(footer, "Status", status.ToString());
            File.AppendAllText(FilePath, footer.ToString(), FileEncoding);
        }

        Logger.Info($"Mérési fájl lezárva ({status}): {FilePath}");
    }

    private static string BuildTrialRow(MeasurementResult trial)
    {
        var stimulus = trial.StimulusBounds;
        var fields = new[]
        {
            trial.Phase.ToString(),
            trial.TrialNumber.ToString(CultureInfo.InvariantCulture),
            trial.TargetValidTrials.ToString(CultureInfo.InvariantCulture),
            FormatNumber(trial.EspReactionTimeMs),
            FormatNumber(trial.LocalReactionTimeMs),
            trial.Valid.ToString(),
            trial.InvalidReason.ToString(),
            FormatDate(trial.RecordedAt),
            FormatNumber(trial.ForeperiodMs),
            MeasurementCsv.EscapeField(trial.ScanoutCorrectionUs ?? string.Empty),
            FormatNumber(stimulus.HasValue ? stimulus.Value.X + (stimulus.Value.Width / 2d) : null, "0.#"),
            FormatNumber(stimulus.HasValue ? stimulus.Value.Y + (stimulus.Value.Height / 2d) : null, "0.#"),
            FormatNumber(stimulus?.Width, "0.#"),
            FormatNumber(trial.StimulusEccentricityDeg, "0.###"),
            FormatNumber(trial.ValidGazeRatio, "0.####"),
            FormatNumber(trial.GazeSamplesInWindow),
            FormatNumber(trial.MeanEyeDistanceMm, "0.#")
        };

        return string.Join(',', fields) + "\n";
    }

    // Soronként egy tekintetminta. A TimeFromOnsetMs az inger kirajzolásához mért idő (az
    // előperiódus mintáinál negatív), az InResponseWindow pedig jelzi, hogy a minta beleszámított-e
    // a próba tekintet alapú érvényességébe. Érvénytelen mintánál a koordináták üresek.
    private static string BuildGazeRows(MeasurementResult trial)
    {
        var recording = trial.GazeRecording;
        if (recording is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var sample in recording.Samples)
        {
            var stagePoint = sample.IsValid ? recording.ToStagePoint(sample) : (PointF?)null;
            builder
                .Append(trial.Phase.ToString()).Append(',')
                .Append(trial.TrialNumber.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.SystemTimeStampUs.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(recording.GetTimeFromOnsetMs(sample).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(recording.IsInResponseWindow(sample).ToString()).Append(',')
                .Append(sample.LeftEyeValid.ToString()).Append(',')
                .Append(sample.RightEyeValid.ToString()).Append(',')
                .Append(sample.IsValid ? sample.NormalizedX.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(sample.IsValid ? sample.NormalizedY.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(stagePoint.HasValue ? stagePoint.Value.X.ToString("0.#", CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(stagePoint.HasValue ? stagePoint.Value.Y.ToString("0.#", CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(float.IsFinite(sample.EyeDistanceMm) ? sample.EyeDistanceMm.ToString("0.#", CultureInfo.InvariantCulture) : string.Empty)
                .Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendMetadataLine(StringBuilder builder, string key, string value)
    {
        var singleLineValue = value.Replace('\r', ' ').Replace('\n', ' ');
        builder.Append("# ").Append(key).Append('=').Append(singleLineValue).Append('\n');
    }

    private static string BuildUniqueFilePath(DateTime startedAt, string modeName)
    {
        var baseName = $"{startedAt:yyyyMMdd_HHmmss}_{MeasurementCsv.SanitizeForFileName(modeName)}";
        var candidate = Path.Combine(DataLocations.MeasurementsDirectory, baseName + CsvExtension);
        var suffix = 2;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(DataLocations.MeasurementsDirectory, $"{baseName}_{suffix}{CsvExtension}");
            suffix++;
        }

        return candidate;
    }

    private static string FormatDate(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static string FormatNumber(double? value, string format = "R")
    {
        return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatNumber(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }
}
