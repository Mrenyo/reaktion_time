using System.Drawing.Drawing2D;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A mérőfelületen kijelölő ablakok (fixációs pont, fotodióda-jelző) közös alapja: keret nélküli, teljes
// képernyős ablak azon a képernyőn, amelyen a mérés fut (a főablak képernyője), a beállított mérési
// háttérrel. Közös a billentyűkezelés (Enter: kész, Esc: mégse, nyilak: finomhangolás, Shifttel
// nagyobb lépés) és a felül (vagy ha ott takarna, alul) megjelenő tájékoztató szalag.
//
// Az ablak maga rajzol mindent, és minden mérete fizikai képpontban értendő (AutoScaleMode.None): a
// mérőfelület koordinátái így egy az egyben az ablak kliens-koordinátái.
internal abstract class StagePickerFormBase : Form
{
    protected static readonly Color BannerTextColor = Color.White;
    protected static readonly Color BannerValueColor = Color.FromArgb(255, 205, 170);
    protected static readonly Color BannerProblemColor = Color.FromArgb(255, 130, 130);

    // A tiltott terület szaggatott szegélye.
    protected static readonly Color KeepOutEdgeColor = Color.FromArgb(200, 220, 60, 60);

    private const int SmallNudgePx = 1;
    private const int LargeNudgePx = 10;

    // A szalag méretei logikai (96 DPI-s) képpontban; a rajzolás az ablak DPI-jére váltja át őket.
    private const int BannerMarginLogical = 28;
    private const int BannerPaddingXLogical = 18;
    private const int BannerPaddingYLogical = 12;
    private const int BannerLineGapLogical = 4;
    private const int BannerCornerRadiusLogical = 12;
    private const int BannerFontSizeLogical = 15;
    private const int BannerAvoidanceLogical = 12;

    private static readonly Color BannerBackColor = Color.FromArgb(232, 24, 26, 32);
    private static readonly Color BannerBorderColor = Color.FromArgb(120, 255, 255, 255);

    private readonly Rectangle _screenBounds;
    private Font? _bannerFont;
    private int _bannerFontDpi;

    // screenBounds: a mérést futtató képernyő teljes területe (Screen.Bounds), fizikai képpontban.
    // settings: a jelenlegi (még nem mentett) beállítások; az ablak a saját másolatán dolgozik.
    protected StagePickerFormBase(Rectangle screenBounds, ExperimentSettings settings, string title)
    {
        _screenBounds = screenBounds;
        StageSettings = settings.Clone();
        StageSize = screenBounds.Size;
        StageBackgroundColor = HexColor.TryParse(StageSettings.Stimulus.BackgroundColor, out var background)
            ? background
            : UiTheme.FallbackStageBackground;

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screenBounds;
        TopMost = true;
        ShowInTaskbar = false;
        ShowIcon = false;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = StageBackgroundColor;
        Cursor = Cursors.Cross;
        Text = title;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
    }

    protected ExperimentSettings StageSettings { get; }

    protected Size StageSize { get; }

    protected Color StageBackgroundColor { get; }

    // Ennyivel kell a szalagnak elkerülnie a kijelölt elemet (az ablak DPI-jére átváltva).
    protected int BannerAvoidancePx => LogicalToDeviceUnits(BannerAvoidanceLogical);

    // Enter vagy dupla kattintás: az eredmény rögzítése és az ablak bezárása (ha van mit rögzíteni).
    protected abstract void Confirm();

    // A nyilak: a kijelölt elem elmozdítása a megadott képponttal.
    protected abstract void Nudge(Size offset);

    // Igaz, ha az adott helyen a szalag eltakarná a kijelölt elemet (ilyenkor a szalag alulra kerül).
    protected abstract bool IsBannerCovering(Rectangle banner);

    // A közös billentyűkön (Enter, Esc, nyilak) túli, módosító nélküli billentyűk; igaz, ha kezelte.
    protected virtual bool ProcessPickerKey(Keys key) => false;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // A kezelő (handle) létrehozásakor a Windows a DPI miatt átméretezheti az ablakot; a
        // kijelölőnek pontosan a mérést futtató képernyőt kell lefednie.
        Bounds = _screenBounds;
    }

    // Ha az ablak DPI-je megváltozik (pl. a képernyő méretezése módosul), a Windows által javasolt
    // méret helyett újra a teljes képernyőt fedi le.
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Bounds = _screenBounds;
        Invalidate();
    }

    // A billentyűket itt kezeljük, mert az ablaknak nincs gyermekvezérlője, amely megkapná őket.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var modifiers = keyData & Keys.Modifiers;
        var key = keyData & Keys.KeyCode;
        if (modifiers == Keys.None)
        {
            if (key == Keys.Enter)
            {
                Confirm();
                return true;
            }

            if (key == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }

            if (ProcessPickerKey(key))
            {
                return true;
            }
        }

        if (modifiers is Keys.None or Keys.Shift)
        {
            var step = modifiers == Keys.Shift ? LargeNudgePx : SmallNudgePx;
            var offset = key switch
            {
                Keys.Left => new Size(-step, 0),
                Keys.Right => new Size(step, 0),
                Keys.Up => new Size(0, -step),
                Keys.Down => new Size(0, step),
                _ => Size.Empty
            };

            if (offset != Size.Empty)
            {
                Nudge(offset);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bannerFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    // A szalag a megadott sorokkal: felül középen van; ha ott eltakarná a kijelölt elemet
    // (IsBannerCovering), alulra kerül.
    protected void PaintBanner(Graphics graphics, IReadOnlyList<(string Text, Color Color)> lines)
    {
        var font = GetBannerFont();
        var margin = LogicalToDeviceUnits(BannerMarginLogical);
        var paddingX = LogicalToDeviceUnits(BannerPaddingXLogical);
        var paddingY = LogicalToDeviceUnits(BannerPaddingYLogical);
        var lineGap = LogicalToDeviceUnits(BannerLineGapLogical);
        var maxTextWidth = Math.Max(1, StageSize.Width - (2 * margin) - (2 * paddingX));

        const TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter;
        var sizes = lines
            .Select(line => TextRenderer.MeasureText(graphics, line.Text, font, new Size(maxTextWidth, int.MaxValue), flags))
            .ToList();
        var textWidth = Math.Min(maxTextWidth, sizes.Max(size => size.Width));
        var textHeight = sizes.Sum(size => size.Height) + (lineGap * (sizes.Count - 1));
        var bannerSize = new Size(textWidth + (2 * paddingX), textHeight + (2 * paddingY));
        var left = (StageSize.Width - bannerSize.Width) / 2;
        var top = new Rectangle(new Point(left, margin), bannerSize);
        var bottom = new Rectangle(new Point(left, StageSize.Height - margin - bannerSize.Height), bannerSize);
        var banner = IsBannerCovering(top) && !IsBannerCovering(bottom) ? bottom : top;

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = StagePainter.CreateRoundedRectangle(banner, LogicalToDeviceUnits(BannerCornerRadiusLogical)))
        using (var brush = new SolidBrush(BannerBackColor))
        using (var pen = new Pen(BannerBorderColor, 1f))
        {
            graphics.FillPath(brush, path);
            graphics.DrawPath(pen, path);
        }

        var y = banner.Y + paddingY;
        for (var i = 0; i < lines.Count; i++)
        {
            var bounds = new Rectangle(banner.X + paddingX, y, textWidth, sizes[i].Height);
            TextRenderer.DrawText(graphics, lines[i].Text, font, bounds, lines[i].Color, flags);
            y += sizes[i].Height + lineGap;
        }
    }

    // A betűméret képpontban van megadva, így független a Graphics DPI-értékétől, és a monitor DPI-jével
    // együtt nő.
    private Font GetBannerFont()
    {
        if (_bannerFont is null || _bannerFontDpi != DeviceDpi)
        {
            _bannerFont?.Dispose();
            _bannerFont = new Font("Segoe UI", LogicalToDeviceUnits(BannerFontSizeLogical), FontStyle.Regular, GraphicsUnit.Pixel);
            _bannerFontDpi = DeviceDpi;
        }

        return _bannerFont;
    }
}
