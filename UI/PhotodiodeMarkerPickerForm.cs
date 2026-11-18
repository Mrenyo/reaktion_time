using System.Drawing.Drawing2D;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A fotodióda-jelző áthelyezése a mérőfelületen (lásd StagePickerFormBase), a beállított mérési
// háttérrel és a fixációs kereszttel. A jelző itt világít (fehér), így a kutató a fizikai fotodiódát
// pontosan fölé tudja igazítani, vagy a jelzőt húzhatja a már felszerelt szenzor alá.
//
// A jelző bárhová tehető (a szenzor helye adott); ha a mérés így nem indítható (pl. a kereszt túl közel
// esik hozzá), a szalag pirosan jelzi az okát, de a hely kijelölhető, és a beállítási ablakban
// javítható a többi beállítás. Az eredmény a jelző középpontja a mérőfelület méretéhez viszonyítva
// (egész képpontból számolva), vagy null, ha az alapértelmezett hely (jobb alsó sarok) maradt.
//
// A szenzor felhelyezését célzóvonalak segítik: egy-egy vízszintes és függőleges vonal a jelző
// közepén át, a teljes képernyőn, mindkét vonalon keresztjellel a középtől AlignmentTickOffsetMm
// távolságra. A maszkos fotodióda így a jelöléshez igazítható. A milliméter átváltásához a kijelző
// fizikai mérete kell (EDID); ha nem ismert, csak a vonalak látszanak.
internal sealed class PhotodiodeMarkerPickerForm : StagePickerFormBase
{
    private const string InstructionText =
        "Húzd a világító négyzetet a fotodióda alá (vagy kattints oda, ahová kerüljön). "
        + "Nyilak: finomhangolás (Shift: 10 px) · R: alaphelyzet (jobb alsó sarok) · Enter: kész · Esc: mégse";

    // A keresztjelek távolsága a jelző közepétől, és a keresztjelek hossza (mindkét oldalra).
    private const double AlignmentTickOffsetMm = 25;
    private const double AlignmentTickHalfLengthMm = 1.5;

    // A célzóvonal: vörös mag fekete szegéllyel, így a világító jelzőn és a sötét háttéren is jól látszik.
    private static readonly Color GuideCoreColor = Color.FromArgb(255, 0, 0);
    private static readonly Color GuideHaloColor = Color.FromArgb(170, 0, 0, 0);

    // Képpont / mm vízszintesen és függőlegesen; null, ha a kijelző fizikai mérete nem ismert.
    private readonly SizeF? _pixelsPerMm;

    // A jelző jelenlegi helye; null, ha az alapértelmezett helyen van (jobb alsó sarok).
    private NormalizedPoint? _center;
    private Rectangle _bounds;
    private StageFeasibility _feasibility;

    // Húzás közben az egérmutató és a jelző középpontja közötti eltolás.
    private Size? _dragOffset;

    // settings: a jelenlegi (még nem mentett) beállítások; a jelző kiinduló helye is innen jön.
    // displaySize: a kijelző fizikai mérete a milliméteres jelöléshez; null, ha nem ismert.
    public PhotodiodeMarkerPickerForm(Rectangle screenBounds, ExperimentSettings settings, PhysicalDisplaySize? displaySize)
        : base(screenBounds, settings, "Fotodióda-jelző áthelyezése")
    {
        if (displaySize is { WidthMm: > 0, HeightMm: > 0 })
        {
            _pixelsPerMm = new SizeF(
                (float)(screenBounds.Width / displaySize.WidthMm),
                (float)(screenBounds.Height / displaySize.HeightMm));
        }

        _feasibility = SetCenter(StageSettings.Hardware.PhotodiodeMarkerCenter);
    }

    // A kijelölt hely (a jelző középpontja arányként), vagy null az alapértelmezett helyhez; csak
    // DialogResult.OK esetén érvényes.
    public NormalizedPoint? SelectedCenter { get; private set; }

    // A jelzőre kattintva a jelző az egérrel együtt mozog (húzás); máshová kattintva oda ugrik, és onnan
    // húzható tovább.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _bounds.IsEmpty)
        {
            return;
        }

        var center = PhotodiodeMarker.GetCenter(_bounds);
        if (PhotodiodeMarker.GetGuardBounds(_bounds).Contains(e.Location))
        {
            _dragOffset = new Size(center.X - e.X, center.Y - e.Y);
        }
        else
        {
            _dragOffset = Size.Empty;
            MoveCenterTo(e.Location);
        }

        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragOffset is { } offset)
        {
            MoveCenterTo(Point.Add(e.Location, offset));
            return;
        }

        Cursor = !_bounds.IsEmpty && PhotodiodeMarker.GetGuardBounds(_bounds).Contains(e.Location) ? Cursors.SizeAll : Cursors.Cross;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragOffset = null;
            Capture = false;
        }
    }

    // Ha az ablak a húzás közben elveszíti az egeret (pl. Alt+Tab), a húzás véget ér; különben a jelző
    // lenyomott gomb nélkül is követné az egérmutatót.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            _dragOffset = null;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left)
        {
            Confirm();
        }
    }

    // R: vissza az alapértelmezett helyre (jobb alsó sarok).
    protected override bool ProcessPickerKey(Keys key)
    {
        if (key != Keys.R)
        {
            return false;
        }

        _feasibility = SetCenter(null);
        Invalidate();
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(StageBackgroundColor);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        StagePainter.DrawFixationCross(graphics, _feasibility.FixationPoint);

        PaintMarker(graphics);
        PaintAlignmentGuides(graphics);

        var guideText = _pixelsPerMm is null
            ? "Piros vonalak: a jelző közepe. A kijelző fizikai mérete nem ismert, ezért a "
                + $"{AlignmentTickOffsetMm / 10:0.0} cm-es keresztjelek nem jeleníthetők meg."
            : $"Piros vonalak: a jelző közepe; a keresztjelek a középtől {AlignmentTickOffsetMm / 10:0.0} cm-re vannak "
                + "(ezekhez igazítsd a maszkos fotodiódát).";
        var lines = new List<(string Text, Color Color)>
        {
            (InstructionText, BannerTextColor),
            (guideText, BannerTextColor),
            ("A jelző helye: " + MeasurementDisplayText.FormatPhotodiodePosition(_bounds, StageSize, _center is null)
                + " · Enter vagy dupla kattintás: kész", BannerValueColor)
        };
        foreach (var error in _feasibility.Errors)
        {
            lines.Add(("Így a mérés nem indítható: " + error.Message, BannerProblemColor));
        }

        PaintBanner(graphics, lines);
    }

    protected override void Confirm()
    {
        SelectedCenter = _center;
        DialogResult = DialogResult.OK;
    }

    protected override void Nudge(Size offset)
    {
        if (!_bounds.IsEmpty)
        {
            MoveCenterTo(Point.Add(PhotodiodeMarker.GetCenter(_bounds), offset));
        }
    }

    // A szalag nem kerülhet a jelző (és a védősávja) fölé.
    protected override bool IsBannerCovering(Rectangle banner)
    {
        if (_bounds.IsEmpty)
        {
            return false;
        }

        var avoidance = BannerAvoidancePx;
        var area = Rectangle.Inflate(banner, avoidance, avoidance);
        return area.IntersectsWith(PhotodiodeMarker.GetGuardBounds(_bounds));
    }

    // A jelző a képernyő legszéléig tolható, de nem lóghat ki róla (a PhotodiodeMarker.GetBounds a
    // szélre igazítja), ezért a középpontot a már igazított négyzetből számoljuk vissza.
    private void MoveCenterTo(Point requestedCenter)
    {
        if (StageSize.Width <= 0 || StageSize.Height <= 0)
        {
            return;
        }

        var requested = NormalizedPoint.FromPixel(requestedCenter, StageSize);
        var bounds = PhotodiodeMarker.GetBounds(StageSize, requested);
        if (bounds.IsEmpty || (bounds == _bounds && _center is not null))
        {
            return;
        }

        _feasibility = SetCenter(NormalizedPoint.FromPixel(PhotodiodeMarker.GetCenter(bounds), StageSize));
        Invalidate();
    }

    private StageFeasibility SetCenter(NormalizedPoint? center)
    {
        _center = center;
        StageSettings.Hardware.PhotodiodeMarkerX = center?.X;
        StageSettings.Hardware.PhotodiodeMarkerY = center?.Y;
        _bounds = StageSettings.Hardware.ResolvePhotodiodeBounds(StageSize);
        return StageFeasibility.Evaluate(StageSettings, StageSize);
    }

    // A fekete védősáv, a világító (fehér) négyzet szürke kerettel, és szaggatottan az a terület, ahová
    // az inger és a kereszt nem kerülhet (a jelző StageLayout.SafeMarginPx-szel megnövelve).
    private void PaintMarker(Graphics graphics)
    {
        if (_bounds.IsEmpty)
        {
            return;
        }

        StagePainter.DrawPhotodiodeMarker(graphics, PhotodiodeMarker.GetGuardBounds(_bounds), _bounds, lit: true);

        graphics.SmoothingMode = SmoothingMode.None;
        var keepOut = (RectangleF)_bounds;
        keepOut.Inflate(StageLayout.SafeMarginPx, StageLayout.SafeMarginPx);
        using var edgePen = new Pen(KeepOutEdgeColor, 1f) { DashStyle = DashStyle.Dash };
        graphics.DrawRectangle(edgePen, keepOut.X, keepOut.Y, keepOut.Width, keepOut.Height);
    }

    // A jelző közepén átmenő vízszintes és függőleges célzóvonal a teljes képernyőn, keresztjelekkel a
    // középtől AlignmentTickOffsetMm-re. A jelző oldala páros számú képpont, így a közepe két képpontsor
    // (-oszlop) határára esik: a vonal ezért páros vastagságú, és pontosan erre a határra szimmetrikus.
    private void PaintAlignmentGuides(Graphics graphics)
    {
        if (_bounds.IsEmpty)
        {
            return;
        }

        // A GDI+ képpont-középpontos koordinátáiban a jelző közepe X + SizePx/2 - 0,5.
        var centerX = _bounds.X + (PhotodiodeMarker.SizePx / 2f) - 0.5f;
        var centerY = _bounds.Y + (PhotodiodeMarker.SizePx / 2f) - 0.5f;
        var coreWidth = 2 * Math.Max(1, LogicalToDeviceUnits(1));

        graphics.SmoothingMode = SmoothingMode.None;
        using var halo = new Pen(GuideHaloColor, coreWidth + 2);
        using var core = new Pen(GuideCoreColor, coreWidth);
        foreach (var pen in new[] { halo, core })
        {
            graphics.DrawLine(pen, 0, centerY, StageSize.Width, centerY);
            graphics.DrawLine(pen, centerX, 0, centerX, StageSize.Height);

            if (_pixelsPerMm is not { } perMm)
            {
                continue;
            }

            var offsetX = (float)(AlignmentTickOffsetMm * perMm.Width);
            var offsetY = (float)(AlignmentTickOffsetMm * perMm.Height);
            var tickX = (float)(AlignmentTickHalfLengthMm * perMm.Width);
            var tickY = (float)(AlignmentTickHalfLengthMm * perMm.Height);
            foreach (var sign in new[] { -1f, 1f })
            {
                // Függőleges keresztjel a vízszintes vonalon, vízszintes a függőlegesen.
                var x = centerX + (sign * offsetX);
                var y = centerY + (sign * offsetY);
                graphics.DrawLine(pen, x, centerY - tickY, x, centerY + tickY);
                graphics.DrawLine(pen, centerX - tickX, y, centerX + tickX, y);
            }
        }
    }
}
