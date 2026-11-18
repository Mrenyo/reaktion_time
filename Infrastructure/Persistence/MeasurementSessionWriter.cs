using System.Globalization;
using System.Text;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure.Persistence;

// Egy mérési munkamenet fájljait írja, PRÓBÁNKÉNT: a mérés indításakor létrehozza a fájlokat a
// metaadatokkal, minden rögzített próba után azonnal hozzáfűzi a próba sorát (és a tekintetmintáit),
// a végén pedig egy lezáró "# Status=..." sort ír. Így egy megszakított mérés eddigi próbái is
// megmaradnak, egy összeomlás után pedig a hiányzó lezáró sorból látszik, hogy a mérés nem zárult le.
//
// Fájlok (AppPaths):
//   measurements\<kezdés>_<mód>.csv            - metaadatok ("# Kulcs=Érték" sorok) + próbánként egy sor
//   measurements\gaze\<kezdés>_<mód>_gaze.csv  - tekintetmintánként egy sor
//
// A formátum verzióját a "# FormatVersion=" sor adja (az oszlopok jelentése verziónként: lásd
// MeasurementRepository). A PlannedTestTrials a tervezett mért próbák száma, a StimulusEccentricityPx
// az inger középpontjának távolsága a fixációs ponttól, képpontban. A metaadat-érték a sor végéig tart,
// idézőjelezés nélkül (a sor nem CSV-mező, így vessző is lehet benne); a sortörés szóközzé válik.
//
// A két fájl a mérés teljes ideje alatt nyitva marad (FileShare.Read: más program, pl. az Excel
// olvashatja, de az írást nem zárhatja ki). Minden próba után a kiírt rész a lemezre ürül
// (Flush(flushToDisk: true)), így ami egyszer kiíródott, az egy áramszünet után is megvan; átmeneti
// hibánál (OneDrive, víruskereső) az írás néhányszor újrapróbálkozik (DurableFile.WriteDurably).
// A metódusok bármelyik szálról hívhatók. Hiba esetén IOException / UnauthorizedAccessException
// kivételt dobnak - a hívónak jeleznie kell a felhasználónak, hogy az adatok nem mentődnek.
internal sealed class MeasurementSessionWriter : IMeasurementSessionWriter
{
    private const int FormatVersion = 3;
    internal const string CsvExtension = ".csv";
    private const string GazeFileSuffix = "_gaze";

    private static readonly string[] TrialColumns =
    {
        "Phase", "TrialNumber", "PlannedTestTrials", "EspReactionTimeMs", "LocalReactionTimeMs", "Valid",
        "InvalidReason", "RecordedAt", "ForeperiodMs", "ActualForeperiodMs", "PrematureClicks", "ScanoutCorrectionUs", "StimulusCenterX", "StimulusCenterY",
        "StimulusDiameterPx", "StimulusEccentricityPx", "ValidGazeRatio", "GazeSamplesInWindow", "MeanEyeDistanceMm",
        "ClickX", "ClickY"
    };

    private static readonly string[] GazeColumns =
    {
        "Phase", "TrialNumber", "SystemTimeStampUs", "TimeFromOnsetMs", "InResponseWindow", "LeftEyeValid",
        "RightEyeValid", "NormalizedX", "NormalizedY", "StageX", "StageY", "EyeDistanceMm"
    };

    // A fájl eleji bájtsorrend-jelzőt (BOM) a Create írja ki; a GetBytes nem teszi a szöveg elé.
    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly object _writeLock = new();
    private readonly FileStream _stream;
    private readonly FileStream _gazeStream;
    private bool _closed;

    private MeasurementSessionWriter(SessionFiles files)
    {
        FilePath = files.FilePath;
        _stream = files.Stream;
        _gazeStream = files.GazeStream;
    }

    public string FilePath { get; }

    internal static MeasurementSessionWriter Create(
        string measurementsDirectory,
        string gazeDirectory,
        string modeName,
        DateTime startedAt,
        IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        Directory.CreateDirectory(measurementsDirectory);
        Directory.CreateDirectory(gazeDirectory);

        var files = CreateUniqueFiles(measurementsDirectory, gazeDirectory, startedAt, modeName);

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
        AppendMetadataLine(gazeHeader, "SessionFile", Path.GetFileName(files.FilePath));
        AppendMetadataLine(gazeHeader, "TimeBase", "Tobii SystemTimeStamp (µs); TimeFromOnsetMs = a minta ideje az inger kirajzolásához képest");
        gazeHeader.Append(string.Join(',', GazeColumns)).Append('\n');

        try
        {
            DurableFile.WriteDurably(files.Stream, [.. FileEncoding.GetPreamble(), .. FileEncoding.GetBytes(header.ToString())]);
            DurableFile.WriteDurably(files.GazeStream, [.. FileEncoding.GetPreamble(), .. FileEncoding.GetBytes(gazeHeader.ToString())]);
        }
        catch
        {
            // Fejléc nélküli fájl nem maradhat meg, mert a statisztika olvashatatlannak látná.
            files.Discard();
            throw;
        }

        Logger.Info($"Mérési fájl létrehozva: {files.FilePath}");
        return new MeasurementSessionWriter(files);
    }

    public void AppendTrial(MeasurementResult trial)
    {
        var row = FileEncoding.GetBytes(BuildTrialRow(trial));
        var gazeRows = BuildGazeRows(trial);

        lock (_writeLock)
        {
            if (_closed)
            {
                throw new InvalidOperationException("A mérési fájl már le van zárva.");
            }

            DurableFile.WriteDurably(_stream, row);
            if (gazeRows.Length > 0)
            {
                DurableFile.WriteDurably(_gazeStream, FileEncoding.GetBytes(gazeRows));
            }
        }
    }

    // Többszöri hívás esetén csak az első számít. A fájlok akkor is felszabadulnak, ha a lezáró sor
    // kiírása nem sikerül (a kivétel ilyenkor is a hívóhoz jut).
    public void Close(SessionStatus status, DateTime endedAt)
    {
        lock (_writeLock)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            try
            {
                var footer = new StringBuilder();
                AppendMetadataLine(footer, "EndedAt", FormatDate(endedAt));
                AppendMetadataLine(footer, "Status", status.ToString());
                DurableFile.WriteDurably(_stream, FileEncoding.GetBytes(footer.ToString()));
            }
            finally
            {
                ReleaseStreams();
            }
        }

        Logger.Info($"Mérési fájl lezárva ({status}): {FilePath}");
    }

    private void ReleaseStreams()
    {
        try
        {
            _stream.Dispose();
        }
        finally
        {
            _gazeStream.Dispose();
        }
    }

    private static string BuildTrialRow(MeasurementResult trial)
    {
        var stimulus = trial.StimulusBounds;
        var fields = new[]
        {
            trial.Phase.ToString(),
            trial.TrialNumber.ToString(CultureInfo.InvariantCulture),
            trial.PlannedTestTrials.ToString(CultureInfo.InvariantCulture),
            FormatNumber(trial.EspReactionTimeMs),
            FormatNumber(trial.LocalReactionTimeMs),
            trial.Valid.ToString(),
            trial.InvalidReason.ToString(),
            FormatDate(trial.RecordedAt),
            FormatNumber(trial.ForeperiodMs),
            FormatNumber(trial.ActualForeperiodMs, "0.#"),
            FormatNumber(trial.PrematureClicks),
            FormatNumber(trial.ScanoutCorrectionUs, "0.###"),
            FormatNumber(stimulus.HasValue ? stimulus.Value.X + (stimulus.Value.Width / 2d) : null, "0.#"),
            FormatNumber(stimulus.HasValue ? stimulus.Value.Y + (stimulus.Value.Height / 2d) : null, "0.#"),
            FormatNumber(stimulus?.Width, "0.#"),
            FormatNumber(trial.StimulusEccentricityPx, "0.#"),
            FormatNumber(trial.ValidGazeRatio, "0.####"),
            FormatNumber(trial.GazeSamplesInWindow),
            FormatNumber(trial.MeanEyeDistanceMm, "0.#"),
            FormatNumber(trial.ClickPoint?.X, "0.#"),
            FormatNumber(trial.ClickPoint?.Y, "0.#")
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
                .Append(recording.IsInResponseWindow(sample)).Append(',')
                .Append(sample.LeftEyeValid).Append(',')
                .Append(sample.RightEyeValid).Append(',')
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

    // A mérési és a tekintetfájl létrehozása szabad néven (<kezdés>_<mód>.csv, ütközéskor _2, _3, ...).
    // Egyik sem írhat felül meglévő fájlt: ha bármelyik név foglalt (pl. egy korábbi, árván maradt
    // tekintetfájl miatt), a következő utótag jön, így a pár mindig azonos alapnevű marad.
    private static SessionFiles CreateUniqueFiles(string directory, string gazeDirectory, DateTime startedAt, string modeName)
    {
        var baseName = $"{startedAt:yyyyMMdd_HHmmss}_{MeasurementCsv.SanitizeForFileName(modeName)}";
        for (var suffix = 1; ; suffix++)
        {
            var name = suffix == 1 ? baseName : $"{baseName}_{suffix}";
            var filePath = Path.Combine(directory, name + CsvExtension);
            var gazeFilePath = Path.Combine(gazeDirectory, name + GazeFileSuffix + CsvExtension);
            if (File.Exists(gazeFilePath) || DurableFile.TryCreateNew(filePath, FileShare.Read) is not { } stream)
            {
                continue;
            }

            FileStream? gazeStream = null;
            try
            {
                gazeStream = DurableFile.TryCreateNew(gazeFilePath, FileShare.Read);
            }
            finally
            {
                if (gazeStream is null)
                {
                    // Hiba, vagy közben valaki létrehozta a tekintetfájlt: az üres mérési fájl nem marad meg.
                    stream.Dispose();
                    DurableFile.TryDelete(filePath);
                }
            }

            if (gazeStream is not null)
            {
                return new SessionFiles(filePath, stream, gazeFilePath, gazeStream);
            }
        }
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

    private sealed record SessionFiles(string FilePath, FileStream Stream, string GazeFilePath, FileStream GazeStream)
    {
        // A létrehozott fájlok bezárása és törlése (a fejléc kiírása nem sikerült).
        public void Discard()
        {
            Stream.Dispose();
            GazeStream.Dispose();
            DurableFile.TryDelete(FilePath);
            DurableFile.TryDelete(GazeFilePath);
        }
    }
}
