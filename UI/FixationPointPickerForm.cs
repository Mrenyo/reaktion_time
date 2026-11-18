using System.Drawing.Drawing2D;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A fixációs pont kijelölése a mérőfelületen (lásd StagePickerFormBase): a kutató pontosan látja, hová
// kerül a kereszt. A tiltott sávok (a képernyő széle és a fotodióda-jelző környéke) sraffozva
// látszanak. Egy pont akkor jelölhető ki, ha a StageFeasibility nem talál hibát: a kereszt a
// biztonsági margón belül marad, és a legkisebb távolságban az inger is elfér legalább egy irányban.
// Az eredmény a mérőfelület méretéhez viszonyított arány, egész képpontból számolva
// (NormalizedPoint.FromPixel), így a GazeSettings.ResolveFixationPoint pontosan ugyanazt a képpontot
// adja vissza.
internal sealed class FixationPointPickerForm : StagePickerFormBase
{
    private const string InstructionText =
        "Kattints oda, ahová a fixációs keresztet szeretnéd. Enter: kész · Esc: mégse · Nyilak: finomhangolás (Shift: 10 px)";

    // Ennyi ideig látszik a szalagon, miért nem jelölhető ki egy pont.
    private const int RejectionMessageDurationMs = 3000;

    // Az egérmutató alatti, még ki nem jelölt kereszt áttetszősége.
    private const int HoverAlpha = 140;

    private static readonly Color ForbiddenHatchColor = Color.FromArgb(170, 220, 60, 60);
    private static readonly Color ForbiddenTintColor = Color.FromArgb(60, 0, 0, 0);
    private static readonly Color InvalidOutlineColor = Color.FromArgb(110, 0, 0);
    private static readonly Color InvalidColor = Color.FromArgb(255, 80, 80);

    private readonly Color _guideColor;
    private readonly System.Windows.Forms.Timer _rejectionTimer = new();

    private Point? _selectedPoint;
    private Point? _hoverPoint;
    private bool _hoverAllowed;
    private string? _rejectionMessage;

    // initialPoint: a korábban kijelölt pont arányként, vagy null, ha még nem volt kijelölve.
    public FixationPointPickerForm(Rectangle screenBounds, ExperimentSettings settings, NormalizedPoint? initialPoint)
        : base(screenBounds, settings, "Fixációs pont kijelölése")
    {
        _guideColor = StagePainter.GetGuideColor(StageBackgroundColor);

        _rejectionTimer.Interval = RejectionMessageDurationMs;
        _rejectionTimer.Tick += (_, _) => ClearRejectionMessage();

        if (initialPoint is { IsFinite: true } initial)
        {
            var pixel = initial.ToPixel(StageSize);
            var feasibility = Evaluate(pixel);
            if (feasibility.IsFeasible)
            {
                _selectedPoint = pixel;
            }
            else
            {
                _rejectionMessage = "A korábban kijelölt pont a jelenlegi beállításokkal nem használható: "
                    + feasibility.Errors[0].Message;
            }
        }
    }

    // A kijelölt pont a mérőfelület szélességéhez, illetve magasságához viszonyítva (egész képpontból
    // számolva); csak DialogResult.OK esetén érvényes.
    public NormalizedPoint SelectedPoint { get; private set; }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_rejectionMessage is not null)
        {
            _rejectionTimer.Start();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.Location;
        if (_hoverPoint == point || !ClientRectangle.Contains(point))
        {
            return;
        }

        _hoverPoint = point;
        _hoverAllowed = Evaluate(point).IsFeasible;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverPoint is null)
        {
            return;
        }

        _hoverPoint = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
        {
            TrySelect(e.Location);
        }
    }

    // Dupla kattintás egy megengedett ponton: kijelölés és befejezés egy lépésben.
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && TrySelect(e.Location))
        {
            Confirm();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(StageBackgroundColor);

        var marker = StageSettings.Hardware.ResolvePhotodiodeBounds(StageSize);
        PaintForbiddenZones(graphics, marker);
        if (!marker.IsEmpty)
        {
            StagePainter.DrawPhotodiodeMarker(graphics, PhotodiodeMarker.GetGuardBounds(marker), marker, lit: false);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_selectedPoint is { } selected)
        {
            PaintSelection(graphics, selected);
        }

        if (_hoverPoint is { } hover && hover != _selectedPoint)
        {
            PaintHover(graphics, hover, _hoverAllowed);
        }

        var selectionText = _selectedPoint is { } point
            ? "Kijelölt pont: " + MeasurementDisplayText.FormatStagePoint(point, StageSize) + " · Enter vagy dupla kattintás: kész"
            : "Még nincs kijelölt pont.";
        var lines = new List<(string Text, Color Color)>
        {
            (InstructionText, BannerTextColor),
            (selectionText, BannerValueColor)
        };
        if (_rejectionMessage is not null)
        {
            lines.Add((_rejectionMessage, BannerProblemColor));
        }

        PaintBanner(graphics, lines);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rejectionTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void Confirm()
    {
        if (_selectedPoint is not { } selected || StageSize.Width <= 0 || StageSize.Height <= 0)
        {
            return;
        }

        SelectedPoint = NormalizedPoint.FromPixel(selected, StageSize);
        DialogResult = DialogResult.OK;
    }

    // A nyilak a kijelölt pontot mozgatják; ha még nincs kijelölés, az egérmutató alatti pontból, annak
    // hiányában a képernyő közepéből indulnak.
    protected override void Nudge(Size offset)
    {
        var origin = _selectedPoint
            ?? _hoverPoint
            ?? new Point(StageSize.Width / 2, StageSize.Height / 2);
        var target = Point.Add(origin, offset);
        target = new Point(
            Math.Clamp(target.X, 0, Math.Max(0, StageSize.Width - 1)),
            Math.Clamp(target.Y, 0, Math.Max(0, StageSize.Height - 1)));
        TrySelect(target);
    }

    // A szalag nem kerülhet az egérmutató alatti vagy a kijelölt pont (és a keresztje) fölé.
    protected override bool IsBannerCovering(Rectangle banner)
    {
        var avoidance = (int)Math.Ceiling(FixationCross.BoundingRadiusPx) + BannerAvoidancePx;
        var area = Rectangle.Inflate(banner, avoidance, avoidance);
        return (_hoverPoint is { } hover && area.Contains(hover))
            || (_selectedPoint is { } selected && area.Contains(selected));
    }

    private StageFeasibility Evaluate(Point point) => StageFeasibility.Evaluate(StageSettings, StageSize, point);

    // Megengedett pontnál kijelöl; tiltottnál a korábbi kijelölés marad, és a szalag megmutatja az okát.
    private bool TrySelect(Point point)
    {
        var feasibility = Evaluate(point);
        if (!feasibility.IsFeasible)
        {
            ShowRejection(feasibility);
            return false;
        }

        _selectedPoint = point;
        ClearRejectionMessage();
        Invalidate();
        return true;
    }

    private void ShowRejection(StageFeasibility feasibility)
    {
        _rejectionMessage = "Ide nem tehető a kereszt: " + feasibility.Errors[0].Message;
        _rejectionTimer.Stop();
        _rejectionTimer.Start();
        Invalidate();
    }

    private void ClearRejectionMessage()
    {
        _rejectionTimer.Stop();
        if (_rejectionMessage is null)
        {
            return;
        }

        _rejectionMessage = null;
        Invalidate();
    }

    // A tiltott sávok pontosan azok a területek, ahová a kereszt középpontja nem kerülhet
    // (StimulusPlacement.IsFixationPointAllowed): a képernyő szélén SafeMarginPx + BoundingRadiusPx
    // széles sáv, és a fotodióda-jelző ugyanennyivel megnövelve.
    private void PaintForbiddenZones(Graphics graphics, Rectangle marker)
    {
        var width = StageSize.Width;
        var height = StageSize.Height;
        var strip = StageLayout.SafeMarginPx + FixationCross.BoundingRadiusPx;
        var allowed = new RectangleF(strip, strip, width - (2 * strip), height - (2 * strip));

        using var zone = new Region(new RectangleF(0, 0, width, height));
        if (allowed.Width > 0 && allowed.Height > 0)
        {
            zone.Exclude(allowed);
        }

        var markerZone = RectangleF.Empty;
        if (!marker.IsEmpty)
        {
            markerZone = marker;
            markerZone.Inflate(strip, strip);
            zone.Union(markerZone);
        }

        graphics.SmoothingMode = SmoothingMode.None;
        using (var tint = new SolidBrush(ForbiddenTintColor))
        {
            graphics.FillRegion(tint, zone);
        }

        using (var hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, ForbiddenHatchColor, Color.Transparent))
        {
            graphics.FillRegion(hatch, zone);
        }

        using var edgePen = new Pen(KeepOutEdgeColor, 1f) { DashStyle = DashStyle.Dash };
        if (allowed.Width > 0 && allowed.Height > 0)
        {
            graphics.DrawRectangle(edgePen, allowed.X, allowed.Y, allowed.Width, allowed.Height);
        }

        if (!markerZone.IsEmpty)
        {
            graphics.DrawRectangle(edgePen, markerZone.X, markerZone.Y, markerZone.Width, markerZone.Height);
        }
    }

    // A kijelölt kereszt a mérés méretében, a legkisebb és a legnagyobb ingertávolság szaggatott körével,
    // periférikus módban az elfogadási körrel is.
    private void PaintSelection(Graphics graphics, Point selected)
    {
        var center = (PointF)selected;
        var stimulus = StageSettings.Stimulus;
        StagePainter.DrawDashedCircle(graphics, center, stimulus.MinEccentricityPx, _guideColor, 1.5f);
        StagePainter.DrawDashedCircle(graphics, center, stimulus.MaxEccentricityPx, _guideColor, 1.5f);
        if (StageSettings.Gaze.Validation == GazeValidationMode.KeepFixation)
        {
            StagePainter.DrawDashedCircle(
                graphics, center, StageSettings.Gaze.ValidityRadiusPx, StagePainter.AcceptanceRingColor, 2f);
        }

        StagePainter.DrawFixationCross(graphics, center);
    }

    // Az egérmutató alatti jelölt pont: áttetsző kereszt a legkisebb távolság körével; tiltott helyen piros.
    private void PaintHover(Graphics graphics, Point hover, bool allowed)
    {
        var center = (PointF)hover;
        if (allowed)
        {
            StagePainter.DrawDashedCircle(
                graphics, center, StageSettings.Stimulus.MinEccentricityPx, Color.FromArgb(HoverAlpha, _guideColor), 1.5f);
            DrawCross(graphics, center, Color.FromArgb(HoverAlpha, StagePainter.CrossOutlineColor), Color.FromArgb(HoverAlpha, StagePainter.CrossColor));
            return;
        }

        using (var ringPen = new Pen(InvalidColor, 2f))
        {
            var radius = FixationCross.BoundingRadiusPx;
            graphics.DrawEllipse(ringPen, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        }

        DrawCross(graphics, center, InvalidOutlineColor, InvalidColor);
    }

    // A kereszt a mérés méretében, a megadott színekkel.
    private static void DrawCross(Graphics graphics, PointF center, Color outlineColor, Color color)
    {
        StagePainter.DrawFixationCross(
            graphics, center, FixationCross.ArmLengthPx, FixationCross.ThicknessPx, FixationCross.OutlinePx, outlineColor, color);
    }
}
