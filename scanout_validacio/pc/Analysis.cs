using System.Globalization;
using System.Text;

namespace ScanoutValidacio;

internal sealed record PointSummary(
    string Name,
    int X,
    int Y,
    int DeltaRows,
    double ExpectedUs,
    int Trials,
    int ValidCount,
    int MissingCount,
    int OtherFrameCount,
    double MeanUs,
    double SdUs,
    double MinUs,
    double MaxUs,
    double MedianUs,
    double ResidualMeanUs,
    double ResidualCi95Us,
    double? InterpolatedResidualMeanUs,
    double? InterpolatedSdUs);

internal sealed record OverallSummary(
    int Trials,
    int ValidCount,
    int OtherFrameCount,
    double Slope,
    double InterceptUs,
    double RSquared,
    double ResidualRmsUs,
    double MaxAbsPointResidualUs,
    double LoopMeanUs,
    int LoopMaxUs);

// A nyers.csv kiértékelése: pontonkénti statisztika, összesítő regresszió (mért ~ várt), táblázatok
// (CSV, Markdown, a szakdolgozat "!table" formátuma) és ábra.
internal static class Analysis
{
    public const string RawFile = "nyers.csv";
    public const string SummaryFile = "osszesites.csv";
    public const string TableFile = "tablazat.md";
    public const string ChartFile = "abra.png";

    private static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

    public static (List<PointSummary> Points, OverallSummary Overall) Run(string folder)
    {
        var meta = SessionMeta.Load(folder);
        var records = TrialRecord.ReadCsv(Path.Combine(folder, RawFile));
        var (points, overall) = Summarize(records, meta);

        WriteSummaryCsv(Path.Combine(folder, SummaryFile), points);
        File.WriteAllText(Path.Combine(folder, TableFile), BuildMarkdown(meta, points, overall), new UTF8Encoding(false));
        Chart.Draw(Path.Combine(folder, ChartFile), meta, records, points);
        return (points, overall);
    }

    // Egy próba "másik képkockába esik", ha a mért különbség a vártól több mint fél képidővel tér el:
    // ilyenkor a mérő négyzet és a jelző nem ugyanabban a képkockában jelent meg. Ezek külön
    // számolva szerepelnek, a statisztikából kimaradnak.
    private static bool IsOtherFrame(TrialRecord r, SessionMeta meta) =>
        Math.Abs(r.DtUs!.Value - r.ExpectedDtUs) > meta.FrameTimeUs / 2;

    public static (List<PointSummary>, OverallSummary) Summarize(List<TrialRecord> records, SessionMeta meta)
    {
        var points = new List<PointSummary>();
        foreach (var group in records.GroupBy(r => r.Point))
        {
            var all = group.ToList();
            var first = all[0];
            var valid = all.Where(r => r.Valid && r.DtUs is not null).ToList();
            var otherFrame = valid.Where(r => IsOtherFrame(r, meta)).ToList();
            var used = valid.Except(otherFrame).ToList();
            var dts = used.Select(r => (double)r.DtUs!.Value).ToList();
            var expected = first.ExpectedDtUs;

            var interp = used.Where(r => r.DtInterpolatedUs is not null).Select(r => r.DtInterpolatedUs!.Value).ToList();
            var sd = Sd(dts);
            points.Add(new PointSummary(
                group.Key,
                first.PointX,
                first.PointY,
                first.PointY - first.ReferenceY,
                expected,
                all.Count,
                used.Count,
                all.Count - valid.Count,
                otherFrame.Count,
                Mean(dts),
                sd,
                dts.Count > 0 ? dts.Min() : double.NaN,
                dts.Count > 0 ? dts.Max() : double.NaN,
                Median(dts),
                Mean(dts) - expected,
                dts.Count > 1 ? TQuantile975(dts.Count - 1) * sd / Math.Sqrt(dts.Count) : double.NaN,
                interp.Count > 0 ? Mean(interp) - expected : null,
                interp.Count > 1 ? Sd(interp) : null));
        }

        var usedAll = records
            .Where(r => r.Valid && r.DtUs is not null && !IsOtherFrame(r, meta))
            .ToList();
        var xs = usedAll.Select(r => r.ExpectedDtUs).ToList();
        var ys = usedAll.Select(r => (double)r.DtUs!.Value).ToList();
        var (slope, intercept, r2) = Regression(xs, ys);
        var rms = usedAll.Count > 0
            ? Math.Sqrt(usedAll.Average(r => Math.Pow(r.DtUs!.Value - r.ExpectedDtUs, 2)))
            : double.NaN;
        var loops = records.Where(r => r.LoopMeanUs is not null).ToList();

        var overall = new OverallSummary(
            records.Count,
            usedAll.Count,
            points.Sum(p => p.OtherFrameCount),
            slope,
            intercept,
            r2,
            rms,
            points.Where(p => p.ValidCount > 0).Select(p => Math.Abs(p.ResidualMeanUs)).DefaultIfEmpty(double.NaN).Max(),
            loops.Count > 0 ? loops.Average(r => r.LoopMeanUs!.Value) : double.NaN,
            records.Select(r => r.LoopMaxUs ?? 0).DefaultIfEmpty(0).Max());
        return (points, overall);
    }

    private static void WriteSummaryCsv(string path, List<PointSummary> points)
    {
        var sb = new StringBuilder();
        sb.Append("Pont,PontX,PontY,DeltaY,VartDtUs,Probak,Ervenyes,NincsFenyjel,MasikKepkocka,")
          .Append("AtlagDtUs,SzorasUs,MinUs,MaxUs,MedianUs,ElteresAtlagUs,Elteres95KIUs,InterpElteresAtlagUs,InterpSzorasUs\n");
        foreach (var p in points)
        {
            sb.AppendJoin(',',
                p.Name, p.X, p.Y, p.DeltaRows, Inv.F(p.ExpectedUs), p.Trials, p.ValidCount, p.MissingCount, p.OtherFrameCount,
                Inv.F(p.MeanUs), Inv.F(p.SdUs), Inv.F(p.MinUs), Inv.F(p.MaxUs), Inv.F(p.MedianUs),
                Inv.F(p.ResidualMeanUs), Inv.F(p.ResidualCi95Us),
                p.InterpolatedResidualMeanUs is { } ir ? Inv.F(ir) : "",
                p.InterpolatedSdUs is { } isd ? Inv.F(isd) : "");
            sb.Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Ms(double us, string format = "0.000") =>
        double.IsFinite(us) ? (us / 1000).ToString(format, Hu) : "–";

    private static string Num(double value, string format) =>
        double.IsFinite(value) ? value.ToString(format, Hu) : "–";

    public static string BuildMarkdown(SessionMeta meta, List<PointSummary> points, OverallSummary o)
    {
        var date = DateTime.TryParse(meta.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy. MMMM d.", Hu)
            : meta.StartedAt;
        var monitor = meta.MonitorName ?? meta.DisplayDevice;
        var lineTimeUs = meta.VerticalTotalLines > 0 ? meta.FrameTimeUs / meta.VerticalTotalLines : double.NaN;

        var caption = $"A scanout-korrekció validálása két fotodiódával ({date}; {monitor}, "
            + $"{meta.ScreenWidthPx} × {meta.ScreenHeightPx}, {Num(meta.RefreshRateHz, "0.###")} Hz)";

        var rows = points.Select(p => new[]
        {
            p.Name,
            $"{p.X}; {p.Y}",
            p.DeltaRows.ToString(Hu),
            Ms(p.ExpectedUs),
            $"{Ms(p.MeanUs)} ± {Ms(p.SdUs)}",
            $"{Ms(p.ResidualMeanUs)} ± {Ms(p.ResidualCi95Us)}",
            p.OtherFrameCount > 0 ? $"{p.ValidCount} (+{p.OtherFrameCount})" : p.ValidCount.ToString(Hu),
        }).ToList();
        string[] header =
        [
            "Pont", "Középpont x; y (px)", "Δy (px)", "Várt Δt (ms)", "Mért Δt átlag ± szórás (ms)",
            "Eltérés átlaga ± 95% KI (ms)", "n",
        ];

        var sb = new StringBuilder();
        sb.Append("# Scanout-korrekció validálása\n\n");
        sb.Append("## Mérési körülmények\n\n");
        sb.Append("| | |\n|---|---|\n");
        sb.Append($"| Dátum | {meta.StartedAt} |\n");
        sb.Append($"| Kijelző | {monitor} ({meta.DisplayDevice}), {meta.ScreenWidthPx} × {meta.ScreenHeightPx} px |\n");
        sb.Append($"| Képfrissítés | {Num(meta.RefreshRateHz, "0.0000")} Hz, képidő {Ms(meta.FrameTimeUs, "0.0000")} ms |\n");
        sb.Append($"| Sorok (aktív / összes) | {meta.VerticalActiveLines} / {meta.VerticalTotalLines} ({meta.TimingSource}{(meta.TimingExact ? "" : ", NEM pontos")}) |\n");
        sb.Append($"| Soridő | {Num(lineTimeUs, "0.000")} µs; egy képpontsor: {Num(meta.MicrosecondsPerRow, "0.000")} µs |\n");
        if (meta.TimingWarning is not null)
        {
            sb.Append($"| Figyelmeztetés | {meta.TimingWarning} |\n");
        }

        sb.Append($"| Referencia-jelző | középpont ({meta.ReferenceCenterX}; {meta.ReferenceCenterY}), {meta.MarkerSizePx} × {meta.MarkerSizePx} px |\n");
        sb.Append($"| Mérő négyzet | {meta.PointSizePx} × {meta.PointSizePx} px, {meta.PointColor} a {meta.BackgroundColor} háttéren |\n");
        sb.Append($"| Próbák pontonként | {meta.TrialsPerPoint} (világos {meta.LitDurationMs} ms) |\n");
        sb.Append($"| Firmware | {meta.FirmwareVersion} ({meta.Port}); mérő ciklus átlag {Num(o.LoopMeanUs, "0.0")} µs, max {o.LoopMaxUs} µs |\n\n");

        sb.Append("## Eredmények\n\n");
        sb.Append("Δt = t(mérő fotodióda) − t(referencia-jelző). A várt érték a reakcióidő-mérő ");
        sb.Append("scanout-képlete: Δy · linesPerRow · soridő. Az eltérés = mért átlag − várt; ");
        sb.Append("a 95%-os konfidenciaintervallum (KI) az átlag bizonytalansága. ");
        sb.Append("Az n melletti zárójeles szám a más képkockába esett (|eltérés| > fél képidő), ");
        sb.Append("a statisztikából kihagyott próbák száma.\n\n");
        sb.Append("| ").AppendJoin(" | ", header).Append(" |\n");
        sb.Append("|").Append(string.Concat(Enumerable.Repeat("---|", header.Length))).Append('\n');
        foreach (var row in rows)
        {
            sb.Append("| ").AppendJoin(" | ", row).Append(" |\n");
        }

        sb.Append('\n');
        sb.Append($"- Érvényes próbák: {o.ValidCount} / {o.Trials}; más képkockába esett: {o.OtherFrameCount}.\n");
        sb.Append($"- Regresszió (mért Δt a várt függvényében, minden érvényes próba): meredekség {Num(o.Slope, "0.0000")}, ");
        sb.Append($"tengelymetszet {Ms(o.InterceptUs)} ms, R² = {Num(o.RSquared, "0.00000")}.\n");
        sb.Append($"- Az eltérés négyzetes középértéke (RMS) próbánként: {Ms(o.ResidualRmsUs)} ms; ");
        sb.Append($"a pontátlagok legnagyobb abszolút eltérése: {Ms(o.MaxAbsPointResidualUs)} ms.\n");
        // A Δy = 0 pont (a referencia sorában) eltérése a két érzékelőlánc állandó eltolása (eltérő
        // erősítés, küszöb, panelfelfutás), nem a scanout-modell hibája; ezzel csökkentve a többi pont
        // eltérése a modell pozíciófüggő hibáját mutatja.
        if (points.FirstOrDefault(p => p.DeltaRows == 0 && p.ValidCount > 0) is { } zero)
        {
            sb.Append($"- Nullpont ({zero.Name}, Δy = 0): eltérés {Ms(zero.ResidualMeanUs)} ms; ez a két érzékelőlánc állandó eltolása. ");
            sb.Append("Ezzel csökkentve az eltérés: ");
            sb.AppendJoin(", ", points.Where(p => p != zero && p.ValidCount > 0)
                .Select(p => $"{p.Name}: {Ms(p.ResidualMeanUs - zero.ResidualMeanUs)} ms"));
            sb.Append(".\n");
        }

        if (points.Any(p => p.InterpolatedResidualMeanUs is not null))
        {
            sb.Append("- Kiegészítés (a küszöbátlépés lineáris interpolációja a két szomszédos minta között): eltérés átlaga pontonként ");
            sb.AppendJoin(", ", points.Select(p => $"{p.Name}: {(p.InterpolatedResidualMeanUs is { } v ? Ms(v) : "–")} ms"));
            sb.Append(".\n");
        }

        sb.Append("\n## A szakdolgozat formátumában\n\n");
        sb.Append("```text\n");
        sb.Append($"!table {caption} || 1.4,2.6,1.6,2.2,3.2,3.6,1.4\n");
        sb.AppendJoin(" | ", header).Append('\n');
        foreach (var row in rows)
        {
            sb.AppendJoin(" | ", row).Append('\n');
        }

        sb.Append("```\n\n");
        sb.Append($"Ábra: `{ChartFile}` (felül a mért Δt a képernyő-pozíció függvényében a várt egyenessel, alul az eltérés).\n");
        return sb.ToString();
    }

    private static double Mean(List<double> v) => v.Count > 0 ? v.Average() : double.NaN;

    private static double Sd(List<double> v)
    {
        if (v.Count < 2)
        {
            return double.NaN;
        }

        var m = v.Average();
        return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1));
    }

    private static double Median(List<double> v)
    {
        if (v.Count == 0)
        {
            return double.NaN;
        }

        var s = v.OrderBy(x => x).ToList();
        return s.Count % 2 == 1 ? s[s.Count / 2] : (s[(s.Count / 2) - 1] + s[s.Count / 2]) / 2;
    }

    private static (double Slope, double Intercept, double R2) Regression(List<double> x, List<double> y)
    {
        if (x.Count < 2)
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        var mx = x.Average();
        var my = y.Average();
        double sxx = 0, sxy = 0, syy = 0;
        for (var i = 0; i < x.Count; i++)
        {
            sxx += (x[i] - mx) * (x[i] - mx);
            sxy += (x[i] - mx) * (y[i] - my);
            syy += (y[i] - my) * (y[i] - my);
        }

        if (sxx == 0)
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        var slope = sxy / sxx;
        return (slope, my - (slope * mx), syy == 0 ? double.NaN : sxy * sxy / (sxx * syy));
    }

    // A Student-féle t-eloszlás 97,5%-os kvantilise (Cornish-Fisher-közelítés; df >= 5-re 0,1%-on belül).
    private static double TQuantile975(int df)
    {
        const double z = 1.959963985;
        double n = df;
        var z3 = z * z * z;
        var z5 = z3 * z * z;
        var z7 = z5 * z * z;
        return z + ((z3 + z) / (4 * n)) + (((5 * z5) + (16 * z3) + (3 * z)) / (96 * n * n))
               + (((3 * z7) + (19 * z5) + (17 * z3) - (15 * z)) / (384 * n * n * n));
    }
}
