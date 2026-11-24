using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ScanoutValidacio;

// A reakcióidő-mérő UI/BufferedPanel.cs másolata: a tartalom memóriabeli pufferbe rajzolódik, és
// egyetlen másolással kerül a képernyőre.
internal sealed class BufferedPanel : Panel
{
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;

    public BufferedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        UpdateStyles();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.Style |= WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
            return createParams;
        }
    }
}

// Keret nélküli, teljes képernyős mérőablak. Pontonként:
//   1. felhelyező nézet: a mérő négyzet és a referencia-jelző keretezve, élő ADC-szintekkel;
//   2. próbák: sötét szakasz -> ARM -> a két négyzet EGY rajzolásban fehérre vált (mint a
//      reakcióidő-mérőben az inger és a jelző) -> sötét -> RESULT, a próba a nyers.csv-be kerül.
// A végén az elemzés lefut, és a táblázat, az ábra a mérés mappájába kerül.
internal sealed class MeasurementForm : Form
{
    private enum Mode { Message, Placement, Trials }

    private enum PlacementResult { Start, Skip, Quit }

    private readonly Settings _settings;
    private readonly Screen _screen;
    private readonly BufferedPanel _panel = new() { Dock = DockStyle.Fill };
    private readonly Random _random = new();
    private readonly Color _background;
    private readonly Color _pointColor;
    private readonly Font _infoFont = new("Segoe UI", 15f);
    private readonly Font _progressFont = new("Segoe UI", 11f);

    private EspConnection? _esp;
    private DisplayTiming? _timing;
    private Rectangle _reference;
    private Rectangle _point;
    private Mode _mode = Mode.Message;
    private bool _lit;
    private string _text = "Indítás...";
    private Rectangle _textBounds;
    private TaskCompletionSource<PlacementResult>? _placement;
    private volatile bool _applyThresholds;
    private volatile bool _levelsReady;
    private volatile bool _intro;
    private int _recorded;
    private long _litToggledAt;
    private volatile bool _abort;
    private string? _sessionFolder;
    private bool _finished;
    private int _thresholdRef;
    private int _thresholdMeas;

    public MeasurementForm(Settings settings, Screen screen)
    {
        _settings = settings;
        _screen = screen;
        _background = ColorTranslator.FromHtml(settings.BackgroundColor);
        _pointColor = ColorTranslator.FromHtml(settings.PointColor);
        _thresholdRef = settings.ThresholdReference;
        _thresholdMeas = settings.ThresholdMeasurement;

        Text = "Scanout-validáció";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        BackColor = Color.Black;
        KeyPreview = true;
        TopMost = true;
        Cursor.Hide();

        Controls.Add(_panel);
        _panel.Paint += OnPaintPanel;
        KeyDown += OnKey;
        Shown += async (_, _) => await RunAsync();

        var w = screen.Bounds.Width;
        var h = screen.Bounds.Height;
        var size = settings.MarkerSizePx;
        _reference = new Rectangle(w - settings.MarkerMarginPx - size, h - settings.MarkerMarginPx - size, size, size);
    }

    private static int CenterY(Rectangle r) => (int)Math.Round(r.Top + (r.Height / 2d));

    private static int CenterX(Rectangle r) => (int)Math.Round(r.Left + (r.Width / 2d));

    private Rectangle ReferenceGuard => Rectangle.Inflate(_reference, _settings.MarkerGuardPx, _settings.MarkerGuardPx);

    private Rectangle PointBounds(MeasurementPoint p)
    {
        var w = _screen.Bounds.Width;
        var h = _screen.Bounds.Height;
        var size = _settings.PointSizePx;
        var cx = (int)Math.Round(p.X * w);
        var cy = p.Y is { } y ? (int)Math.Round(y * h) : CenterY(_reference);
        var left = Math.Clamp(cx - (size / 2), 0, w - size);
        var top = Math.Clamp(cy - (size / 2), 0, h - size);
        var rect = new Rectangle(left, top, size, size);
        if (rect.IntersectsWith(ReferenceGuard))
        {
            throw new InvalidOperationException($"A(z) {p.Name} pont mérő négyzete a referencia-jelzőre vagy annak védősávjára esik.");
        }

        return rect;
    }

    private async Task RunAsync()
    {
        try
        {
            ShowMessage("Kapcsolódás az ESP-hez...");
            _esp = await Task.Run(() => EspConnection.Connect(_settings.Port, _settings.BaudRate));
            var thr = await Task.Run(() => _esp.SetThresholds(_thresholdRef, _thresholdMeas))
                ?? throw new InvalidOperationException("Az ESP nem fogadta el a küszöböket.");
            (_thresholdRef, _thresholdMeas) = thr;

            _timing = DisplayTimingReader.Read(_screen.DeviceName, _screen.Bounds);
            var points = _settings.Points.Select(p => (Point: p, Bounds: PointBounds(p))).ToList();

            var root = string.IsNullOrWhiteSpace(_settings.OutputFolder)
                ? Path.Combine(Environment.CurrentDirectory, "meresek")
                : _settings.OutputFolder;
            var started = DateTime.Now;
            _sessionFolder = Path.Combine(root, "scanout_validacio_" + started.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(_sessionFolder);

            var meta = new SessionMeta
            {
                StartedAt = started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                AppVersion = typeof(MeasurementForm).Assembly.GetName().Version?.ToString(3) ?? "",
                FirmwareVersion = _esp.FirmwareVersion,
                Port = _esp.PortName,
                DisplayDevice = _screen.DeviceName,
                MonitorName = _timing.MonitorName,
                ScreenWidthPx = _screen.Bounds.Width,
                ScreenHeightPx = _screen.Bounds.Height,
                RefreshRateHz = _timing.RefreshRateHz,
                VerticalActiveLines = _timing.VerticalActiveLines,
                VerticalTotalLines = _timing.VerticalTotalLines,
                TimingExact = _timing.IsExact,
                TimingSource = _timing.Source,
                TimingWarning = _timing.Warning,
                FrameTimeUs = _timing.FrameTimeUs,
                MicrosecondsPerRow = _timing.MicrosecondsPerRow(_screen.Bounds.Height),
                ReferenceCenterX = CenterX(_reference),
                ReferenceCenterY = CenterY(_reference),
                MarkerSizePx = _settings.MarkerSizePx,
                PointSizePx = _settings.PointSizePx,
                PointColor = _settings.PointColor,
                BackgroundColor = _settings.BackgroundColor,
                TrialsPerPoint = _settings.TrialsPerPoint,
                LitDurationMs = _settings.LitDurationMs,
            };
            meta.Save(_sessionFolder);
            File.Copy(Path.Combine(Environment.CurrentDirectory, Settings.FileName), Path.Combine(_sessionFolder, Settings.FileName), overwrite: true);

            await using var raw = new StreamWriter(Path.Combine(_sessionFolder, Analysis.RawFile), append: false, new UTF8Encoding(false)) { AutoFlush = true };
            await raw.WriteLineAsync(string.Join(',', TrialRecord.Columns));

            // Érzékelő-teszt: mindkét ADC-érték élőben, még az első pont előtt.
            _point = points[0].Bounds;
            if (await PlacementAsync(points[0].Point, 0, points.Count, intro: true) == PlacementResult.Quit)
            {
                _abort = true;
            }

            for (var i = 0; i < points.Count && !_abort; i++)
            {
                var (point, bounds) = points[i];
                _point = bounds;
                var result = await PlacementAsync(point, i + 1, points.Count);
                if (result == PlacementResult.Quit)
                {
                    break;
                }

                if (result == PlacementResult.Start)
                {
                    await TrialsAsync(point, raw);
                }
            }

            await raw.DisposeAsync();
            _esp.Stop();
            if (_recorded == 0)
            {
                _finished = true;
                ShowMessage("Nincs mért próba, az elemzés kimarad.\n\n[Esc] kilépés");
                return;
            }

            await FinishAsync();
        }
        catch (Exception ex)
        {
            _finished = true;
            ShowMessage("Hiba:\n" + ex.Message + "\n\n[Esc] kilépés");
        }
    }

    // intro = igaz: az elején lévő érzékelő-teszt (mindkét ADC-érték élőben, küszöb beállítása); a
    // [Space] ilyenkor az első pont felhelyezésére lép. Egyébként a pont felhelyező nézete: a [Space]
    // csak akkor indítja a próbákat, ha a sötét és a világos szint is ismert, és ilyenkor a
    // küszöböket a program a mért szintek felére állítja.
    private async Task<PlacementResult> PlacementAsync(MeasurementPoint point, int index, int count, bool intro = false)
    {
        _mode = Mode.Placement;
        _lit = false;
        _intro = intro;
        _levelsReady = false;
        _placement = new TaskCompletionSource<PlacementResult>();
        var dark = new double?[2];
        var light = new double?[2];
        int? suggestRef = null;
        int? suggestMeas = null;
        var expected = _timing!.ScanoutOffsetUs(CenterY(_point), CenterY(_reference), _screen.Bounds.Height);

        while (!_placement.Task.IsCompleted)
        {
            var adc = await Task.Run(() => _esp!.ReadAdc());
            var settled = Environment.TickCount64 - Interlocked.Read(ref _litToggledAt) > 250;
            if (adc is { } a && settled)
            {
                var target = _lit ? light : dark;
                target[0] = target[0] is { } p0 ? (0.7 * p0) + (0.3 * a.Reference) : a.Reference;
                target[1] = target[1] is { } p1 ? (0.7 * p1) + (0.3 * a.Measurement) : a.Measurement;
            }

            suggestRef = dark[0] is { } d0 && light[0] is { } l0 && l0 > d0 + 20 ? (int)Math.Round((d0 + l0) / 2) : null;
            suggestMeas = dark[1] is { } d1 && light[1] is { } l1 && l1 > d1 + 20 ? (int)Math.Round((d1 + l1) / 2) : null;
            _levelsReady = suggestRef is not null && suggestMeas is not null;
            if (_applyThresholds)
            {
                _applyThresholds = false;
                if (suggestRef is not null && suggestMeas is not null)
                {
                    var thr = await Task.Run(() => _esp!.SetThresholds(suggestRef.Value, suggestMeas.Value));
                    if (thr is { } t)
                    {
                        (_thresholdRef, _thresholdMeas) = t;
                    }
                }
            }

            static string V(double? v) => v is { } x ? x.ToString("0", CultureInfo.InvariantCulture) : "?";
            var sb = new StringBuilder();
            if (intro)
            {
                sb.AppendLine("ÉRZÉKELŐ-TESZT: mindkét fotodióda jele (nyers ADC, 0-4095)");
                sb.AppendLine($"Referencia-jelző: x = {CenterX(_reference)}, y = {CenterY(_reference)} px;  a mérő négyzet itt az első pont helyén van");
                sb.AppendLine();
                sb.AppendLine("Tedd mindkét fotodiódát a négyzetére, kapcsold [L]-lel világosra és sötétre, és ellenőrizd a szinteket.");
                sb.AppendLine("A küszöb a világos szint alatt, a sötét szint fölött kell legyen (alapból 475: ha a világos szint kisebb, nem lesz fényjel).");
            }
            else
            {
                sb.AppendLine($"{point.Name} ({index}/{count}): a mérő négyzet középpontja x = {CenterX(_point)}, y = {CenterY(_point)} px");
                sb.AppendLine($"Referencia-jelző: x = {CenterX(_reference)}, y = {CenterY(_reference)} px;  várt Δt = {(expected / 1000).ToString("0.000", CultureInfo.InvariantCulture)} ms");
                sb.AppendLine();
                sb.AppendLine("Helyezd a mérő fotodiódát a keretezett négyzet közepére (a referencia maradjon a jobb alsó jelzőn).");
            }

            sb.AppendLine();
            sb.AppendLine($"ADC most, referencia (GPIO4): {(adc is { } n ? n.Reference.ToString(CultureInfo.InvariantCulture) : "NA")}   ({(_lit ? "világos" : "sötét")})");
            sb.AppendLine($"ADC most, mérő       (GPIO6): {(adc is { } m ? m.Measurement.ToString(CultureInfo.InvariantCulture) : "NA")}   ({(_lit ? "világos" : "sötét")})");
            sb.AppendLine($"(az ESP nyers válasza: {_esp!.LastAdcReply})");
            sb.AppendLine($"Sötét szint:   referencia {V(dark[0])},  mérő {V(dark[1])}");
            sb.AppendLine($"Világos szint: referencia {V(light[0])},  mérő {V(light[1])}");
            sb.AppendLine($"Küszöb:        referencia {_thresholdRef},  mérő {_thresholdMeas}"
                + (suggestRef is not null && suggestMeas is not null ? $"   (javaslat: {suggestRef}, {suggestMeas})" : "   (javaslathoz: [L] világos és sötét)"));
            sb.AppendLine();
            if (intro)
            {
                sb.Append("[L] világos/sötét   [K] javasolt küszöb   [Space] tovább az 1. pontra   [Esc] kilépés");
            }
            else
            {
                sb.Append($"[L] világos/sötét   [K] javasolt küszöb   [Space] {_settings.TrialsPerPoint} próba indítása   [S] pont kihagyása   [Esc] befejezés");
                if (!_levelsReady)
                {
                    sb.Append("\nA [Space]-hez előbb mérd meg [L]-lel a sötét és a világos szintet (mindkét fotodiódán).");
                }
            }

            SetText(sb.ToString(), large: true);

            await Task.WhenAny(_placement.Task, Task.Delay(150));
        }

        _lit = false;
        var result = await _placement.Task;
        if (!intro && result == PlacementResult.Start && suggestRef is not null && suggestMeas is not null)
        {
            // A próbák a most mért szintek felével dolgoznak (a firmware csatornánként külön küszöböt használ).
            var thr = await Task.Run(() => _esp!.SetThresholds(suggestRef.Value, suggestMeas.Value));
            if (thr is { } t)
            {
                (_thresholdRef, _thresholdMeas) = t;
            }
        }

        return result;
    }

    private async Task TrialsAsync(MeasurementPoint point, StreamWriter raw)
    {
        _mode = Mode.Trials;
        _lit = false;
        var expected = _timing!.ScanoutOffsetUs(CenterY(_point), CenterY(_reference), _screen.Bounds.Height);
        SetText($"{point.Name}: mérés indul...", large: false);
        await Task.Delay(1000);

        var errors = 0;
        for (var trial = 1; trial <= _settings.TrialsPerPoint && !_abort; trial++)
        {
            await Task.Delay(_random.Next(_settings.DarkMinMs, _settings.DarkMaxMs + 1));
            var record = new TrialRecord
            {
                Point = point.Name,
                PointX = CenterX(_point),
                PointY = CenterY(_point),
                ReferenceY = CenterY(_reference),
                Trial = trial,
                ExpectedDtUs = expected,
            };

            if (!await Task.Run(() => _esp!.Arm()))
            {
                record.Error = "ARM: nincs valasz";
            }
            else
            {
                await Task.Delay(30);
                record.PcTime = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                ShowLit(true);
                await Task.Delay(_settings.LitDurationMs);
                ShowLit(false);
                record.FillFromResult(await Task.Run(() => _esp!.ReadResult()));
            }

            if (!record.Valid)
            {
                errors++;
            }

            await raw.WriteLineAsync(record.ToCsv());
            _recorded++;
            var last = record.DtUs is { } dt
                ? $"utolsó Δt = {(dt / 1000d).ToString("0.000", CultureInfo.InvariantCulture)} ms (várt {(expected / 1000).ToString("0.000", CultureInfo.InvariantCulture)})"
                : $"utolsó: {record.Error}";
            SetText($"{point.Name}: {trial}/{_settings.TrialsPerPoint}   {last}   hibás: {errors}   [Esc] befejezés", large: false);
        }
    }

    private async Task FinishAsync()
    {
        _mode = Mode.Message;
        ShowMessage("Elemzés...");
        var (points, overall) = await Task.Run(() => Analysis.Run(_sessionFolder!));
        var sb = new StringBuilder();
        sb.AppendLine("Kész. Eredmények:");
        sb.AppendLine();
        foreach (var p in points)
        {
            var other = p.OtherFrameCount > 0 ? $" (+{p.OtherFrameCount} más képkocka)" : "";
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{p.Name}  y={p.Y,5}  várt {p.ExpectedUs / 1000,8:0.000} ms   mért {p.MeanUs / 1000,8:0.000} ± {p.SdUs / 1000:0.000} ms   eltérés {p.ResidualMeanUs,7:0.0} µs   n={p.ValidCount}{other}"));
        }

        sb.AppendLine();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Regresszió: meredekség {overall.Slope:0.0000}, tengelymetszet {overall.InterceptUs:0.0} µs, R² {overall.RSquared:0.00000}"));
        sb.AppendLine();
        sb.AppendLine(_sessionFolder);
        sb.AppendLine();
        sb.Append("[Enter] mappa megnyitása   [Esc] kilépés");
        _finished = true;
        ShowMessage(sb.ToString());
    }

    private void ShowMessage(string text)
    {
        _mode = Mode.Message;
        _lit = false;
        _text = text;
        _panel.Invalidate();
    }

    // A két négyzet ugyanabban a rajzolásban vált: a két terület érvénytelenítése, majd egyetlen
    // azonnali Update() - ugyanúgy, mint a reakcióidő-mérő MeasurementView.ShowStimulus metódusa.
    private void ShowLit(bool lit)
    {
        _lit = lit;
        _panel.Invalidate(_point);
        _panel.Invalidate(_reference);
        _panel.Update();
    }

    private void SetText(string text, bool large)
    {
        _text = text;
        var old = _textBounds;
        using (var g = _panel.CreateGraphics())
        {
            var font = large ? _infoFont : _progressFont;
            var size = Size.Ceiling(g.MeasureString(text, font));
            _textBounds = PlaceText(size);
        }

        if (_mode == Mode.Placement)
        {
            _panel.Invalidate();
        }
        else
        {
            _panel.Invalidate(Rectangle.Inflate(old, 4, 4));
            _panel.Invalidate(Rectangle.Inflate(_textBounds, 4, 4));
        }
    }

    // A szöveg a két négyzettől legtávolabbi negyedbe kerül, hogy fénye ne jusson az érzékelőkre.
    private Rectangle PlaceText(Size size)
    {
        var w = _screen.Bounds.Width;
        var h = _screen.Bounds.Height;
        Point[] candidates = [new(w / 4, h / 4), new(3 * w / 4, h / 4), new(w / 4, 3 * h / 4), new(3 * w / 4, 3 * h / 4), new(w / 2, h / 2)];
        var best = candidates.OrderByDescending(c => Math.Min(Distance(c, _point), Distance(c, _reference))).First();
        var left = Math.Clamp(best.X - (size.Width / 2), 10, Math.Max(10, w - size.Width - 10));
        var top = Math.Clamp(best.Y - (size.Height / 2), 10, Math.Max(10, h - size.Height - 10));
        return new Rectangle(left, top, size.Width, size.Height);
    }

    private static double Distance(Point p, Rectangle r)
    {
        var dx = p.X - (r.Left + (r.Width / 2d));
        var dy = p.Y - (r.Top + (r.Height / 2d));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private void OnPaintPanel(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_background);

        if (_mode == Mode.Message)
        {
            using var brush = new SolidBrush(Color.FromArgb(220, 220, 220));
            g.DrawString(_text, _infoFont, brush, 80, 80);
            return;
        }

        // Mérő négyzet: a reakcióidő-mérő ingeréhez hasonlóan háttérszínről vált a négyzet színére.
        if (_lit)
        {
            using var brush = new SolidBrush(_pointColor);
            g.FillRectangle(brush, _point);
        }

        // Referencia-jelző: fekete védősáv, benne fekete / fehér négyzet (a reakcióidő-mérő jelzője).
        g.FillRectangle(Brushes.Black, ReferenceGuard);
        g.FillRectangle(_lit ? Brushes.White : Brushes.Black, _reference);

        if (_mode == Mode.Placement)
        {
            using var frame = new Pen(Color.FromArgb(128, 128, 128), 1);
            using var cross = new Pen(Color.FromArgb(70, 70, 70), 1);
            g.DrawRectangle(frame, Rectangle.Inflate(_point, 1, 1));
            g.DrawRectangle(frame, Rectangle.Inflate(_reference, 1, 1));
            var cx = CenterX(_point);
            var cy = CenterY(_point);
            var gap = (_settings.PointSizePx / 2) + 30;
            g.DrawLine(cross, 0, cy, cx - gap, cy);
            g.DrawLine(cross, cx + gap, cy, _screen.Bounds.Width, cy);
            g.DrawLine(cross, cx, 0, cx, cy - gap);
            g.DrawLine(cross, cx, cy + gap, cx, _screen.Bounds.Height);
        }

        using (var brush = new SolidBrush(_mode == Mode.Placement ? Color.FromArgb(210, 210, 210) : Color.FromArgb(90, 90, 90)))
        {
            g.DrawString(_text, _mode == Mode.Placement ? _infoFont : _progressFont, brush, _textBounds.Location);
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        switch (_mode)
        {
            case Mode.Placement when _placement is not null:
                switch (e.KeyCode)
                {
                    case Keys.Space:
                        if (_intro || _levelsReady)
                        {
                            _placement.TrySetResult(PlacementResult.Start);
                        }

                        break;
                    case Keys.S:
                        _placement.TrySetResult(PlacementResult.Skip);
                        break;
                    case Keys.Escape:
                        _placement.TrySetResult(PlacementResult.Quit);
                        break;
                    case Keys.L:
                        _lit = !_lit;
                        Interlocked.Exchange(ref _litToggledAt, Environment.TickCount64);
                        _panel.Invalidate();
                        break;
                    case Keys.K:
                        _applyThresholds = true;
                        break;
                }

                break;
            case Mode.Trials when e.KeyCode == Keys.Escape:
                _abort = true;
                break;
            case Mode.Message when _finished && e.KeyCode == Keys.Escape:
                Close();
                break;
            case Mode.Message when _finished && e.KeyCode == Keys.Enter && _sessionFolder is not null:
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_sessionFolder}\"") { UseShellExecute = true });
                break;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _esp?.Dispose();
        Cursor.Show();
        base.OnFormClosed(e);
    }
}
