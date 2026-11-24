using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ScanoutValidacio;

// A mérés körülményei (meta.json): ebből és a nyers.csv-ből az elemzés bármikor megismételhető.
internal sealed class SessionMeta
{
    public string StartedAt { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public string FirmwareVersion { get; set; } = "";
    public string Port { get; set; } = "";
    public string DisplayDevice { get; set; } = "";
    public string? MonitorName { get; set; }
    public int ScreenWidthPx { get; set; }
    public int ScreenHeightPx { get; set; }
    public double RefreshRateHz { get; set; }
    public int VerticalActiveLines { get; set; }
    public int VerticalTotalLines { get; set; }
    public bool TimingExact { get; set; }
    public string TimingSource { get; set; } = "";
    public string? TimingWarning { get; set; }
    public double FrameTimeUs { get; set; }
    public double MicrosecondsPerRow { get; set; }
    public int ReferenceCenterX { get; set; }
    public int ReferenceCenterY { get; set; }
    public int MarkerSizePx { get; set; }
    public int PointSizePx { get; set; }
    public string PointColor { get; set; } = "";
    public string BackgroundColor { get; set; } = "";
    public int TrialsPerPoint { get; set; }
    public int LitDurationMs { get; set; }

    public const string FileName = "meta.json";

    public void Save(string folder) =>
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, Settings.JsonOptions));

    public static SessionMeta Load(string folder) =>
        JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(Path.Combine(folder, FileName)))
        ?? throw new InvalidDataException("Üres meta.json.");
}

// Egy próba a nyers.csv-ben. Az időbélyegek az ESP esp_timer órájából, µs-ban.
internal sealed class TrialRecord
{
    public string Point { get; set; } = "";
    public int PointX { get; set; }
    public int PointY { get; set; }
    public int ReferenceY { get; set; }
    public int Trial { get; set; }
    public double ExpectedDtUs { get; set; }
    public bool Valid { get; set; }
    public string Error { get; set; } = "";
    public long? DtUs { get; set; }
    public double? DtInterpolatedUs { get; set; }
    public long? TRefUs { get; set; }
    public long? TRefPrevUs { get; set; }
    public int? VRef { get; set; }
    public int? VRefPrev { get; set; }
    public long? TMeasUs { get; set; }
    public long? TMeasPrevUs { get; set; }
    public int? VMeas { get; set; }
    public int? VMeasPrev { get; set; }
    public int? ThresholdRef { get; set; }
    public int? ThresholdMeas { get; set; }
    public double? LoopMeanUs { get; set; }
    public int? LoopMaxUs { get; set; }
    public int? RefAdcMin { get; set; }
    public int? RefAdcMax { get; set; }
    public int? MeasAdcMin { get; set; }
    public int? MeasAdcMax { get; set; }
    public string PcTime { get; set; } = "";

    public static readonly string[] Columns =
    [
        "Pont", "PontX", "PontY", "RefY", "Proba", "VartDtUs", "Ervenyes", "Hiba", "DtUs", "DtInterpUs",
        "TRefUs", "TRefElozoUs", "VRef", "VRefElozo", "TMeroUs", "TMeroElozoUs", "VMero", "VMeroElozo",
        "KuszobRef", "KuszobMero", "CiklusAtlagUs", "CiklusMaxUs",
        "RefAdcMin", "RefAdcMax", "MeroAdcMin", "MeroAdcMax", "PcIdo",
    ];

    public string ToCsv()
    {
        static string N(object? v) => v switch
        {
            null => "",
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            bool b => b ? "1" : "0",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Escape(v.ToString() ?? ""),
        };

        object?[] fields =
        [
            Point, PointX, PointY, ReferenceY, Trial, ExpectedDtUs, Valid, Error, DtUs, DtInterpolatedUs,
            TRefUs, TRefPrevUs, VRef, VRefPrev, TMeasUs, TMeasPrevUs, VMeas, VMeasPrev,
            ThresholdRef, ThresholdMeas, LoopMeanUs, LoopMaxUs,
            RefAdcMin, RefAdcMax, MeasAdcMin, MeasAdcMax, PcTime,
        ];
        return string.Join(',', fields.Select(N));
    }

    private static string Escape(string s) =>
        s.IndexOfAny([',', '"', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static List<TrialRecord> ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0)
        {
            return [];
        }

        var header = SplitCsv(lines[0]);
        var index = Columns.ToDictionary(c => c, c => Array.IndexOf(header, c));
        var records = new List<TrialRecord>();
        foreach (var line in lines.Skip(1).Where(l => l.Length > 0))
        {
            var f = SplitCsv(line);
            string S(string c) => index[c] >= 0 && index[c] < f.Length ? f[index[c]] : "";
            long? L(string c) => long.TryParse(S(c), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
            int? I(string c) => int.TryParse(S(c), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
            double? D(string c) => double.TryParse(S(c), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

            records.Add(new TrialRecord
            {
                Point = S("Pont"),
                PointX = I("PontX") ?? 0,
                PointY = I("PontY") ?? 0,
                ReferenceY = I("RefY") ?? 0,
                Trial = I("Proba") ?? 0,
                ExpectedDtUs = D("VartDtUs") ?? 0,
                Valid = S("Ervenyes") == "1",
                Error = S("Hiba"),
                DtUs = L("DtUs"),
                DtInterpolatedUs = D("DtInterpUs"),
                TRefUs = L("TRefUs"),
                TRefPrevUs = L("TRefElozoUs"),
                VRef = I("VRef"),
                VRefPrev = I("VRefElozo"),
                TMeasUs = L("TMeroUs"),
                TMeasPrevUs = L("TMeroElozoUs"),
                VMeas = I("VMero"),
                VMeasPrev = I("VMeroElozo"),
                ThresholdRef = I("KuszobRef"),
                ThresholdMeas = I("KuszobMero"),
                LoopMeanUs = D("CiklusAtlagUs"),
                LoopMaxUs = I("CiklusMaxUs"),
                RefAdcMin = I("RefAdcMin"),
                RefAdcMax = I("RefAdcMax"),
                MeasAdcMin = I("MeroAdcMin"),
                MeasAdcMax = I("MeroAdcMax"),
                PcTime = S("PcIdo"),
            });
        }

        return records;
    }

    private static string[] SplitCsv(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    // Az ESP "RES ..." válaszából. A lineáris interpoláció a küszöbátlépés időpontját a két
    // szomszédos minta között becsli; ez kiegészítő adat, a fő eredmény (DtUs) a reakcióidő-mérővel
    // azonos módszerű, az olvasás utáni időbélyegek különbsége.
    public void FillFromResult(Dictionary<string, string>? r)
    {
        if (r is null)
        {
            Error = "nincs valasz";
            return;
        }

        long? L(string k) => r.TryGetValue(k, out var s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        int? I(string k) => r.TryGetValue(k, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        double? D(string k) => r.TryGetValue(k, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

        LoopMeanUs = D("atlag_us");
        LoopMaxUs = I("max_us");
        RefAdcMin = I("min_ref");
        RefAdcMax = I("max_ref");
        MeasAdcMin = I("min_mero");
        MeasAdcMax = I("max_mero");

        if (r.ContainsKey("NA"))
        {
            var missing = new List<string>();
            if (I("ref") != 1) missing.Add("ref");
            if (I("mero") != 1) missing.Add("mero");
            Error = "nincs fenyjel: " + string.Join('+', missing);
            return;
        }

        DtUs = L("dt");
        TRefUs = L("t_ref");
        TRefPrevUs = L("tp_ref");
        VRef = I("v_ref");
        VRefPrev = I("vp_ref");
        TMeasUs = L("t_mero");
        TMeasPrevUs = L("tp_mero");
        VMeas = I("v_mero");
        VMeasPrev = I("vp_mero");
        ThresholdRef = I("thr_ref");
        ThresholdMeas = I("thr_mero");

        if (DtUs is null)
        {
            Error = "hibas valasz";
            return;
        }

        var tr = Interpolate(TRefPrevUs, TRefUs, VRefPrev, VRef, ThresholdRef);
        var tm = Interpolate(TMeasPrevUs, TMeasUs, VMeasPrev, VMeas, ThresholdMeas);
        DtInterpolatedUs = tr is not null && tm is not null ? tm - tr : null;
        Valid = true;
    }

    private static double? Interpolate(long? tPrev, long? t, int? vPrev, int? v, int? threshold)
    {
        if (tPrev is not > 0 || t is null || vPrev is not >= 0 || v is null || threshold is null || v <= vPrev)
        {
            return null;
        }

        var fraction = Math.Clamp((threshold.Value - vPrev.Value) / (double)(v.Value - vPrev.Value), 0, 1);
        return tPrev.Value + (fraction * (t.Value - tPrev.Value));
    }
}
