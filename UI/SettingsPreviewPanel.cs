using System.Drawing.Drawing2D;
using System.Globalization;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A beállítási ablak előnézete. Felül a mérési képernyő kicsinyített modellje (a képernyő
// oldalarányával): a beállított háttér, a fotodióda-jelző a védősávval, a fixációs kereszt, a
// legkisebb és a ténylegesen elérhető legnagyobb ingertávolság szaggatott köre, egy mintainger a
// legkisebb távolságban és az elfogadási terület narancs szaggatott köre. Alul egy keret, amelyben az
// inger valós méretben (egy az egyben, fizikai képpontban) látszik a beállított háttéren.
//
// A geometria (a mintainger helye, az elfogadási kör, az elérhető legnagyobb távolság) a
// StageFeasibility-ből jön; az UpdatePreview (a beállítási ablak minden módosítás után egyszer hívja)
// csak összeállítja belőle a rajzolás modelljét. Hibás beállításoknál (nincs StageFeasibility) a
// mintainger és az ingerhez kötött elfogadási kör nem látszik.
internal sealed class SettingsPreviewPanel : Control
{
    private static readonly Color CaptionColor = Color.FromArgb(52, 58, 70);
    private static readonly Color LegendColor = Color.FromArgb(96, 106, 120);
    private static readonly Color FrameColor = Color.FromArgb(150, 150, 150);
    private static readonly Color DefaultStimulusColor = Color.White;

    private const string LegendText = "Szaggatott: legkisebb és legnagyobb ingertávolság · kitöltött kör: mintainger";
    private const string AcceptanceLegendText = " · narancs: elfogadási terület";

    // Logikai (96 DPI-s) képpontban; a rajzolás a vezérlő DPI-jére váltja át őket. Kevés helynél előbb
    // a valós méretű keret lesz alacsonyabb (legfeljebb a MinTrueSizeBoxHeightLogical-ig), csak utána a
    // kicsinyített modell.
    private const int TrueSizeBoxWidthLogical = 220;
    private const int TrueSizeBoxHeightLogical = 160;
    private const int MinTrueSizeBoxHeightLogical = 110;
    private const int CaptionGapLogical = 4;
    private const int SectionGapLogical = 14;
    private const int MinCrossArmLogical = 7;
    private const int MinStimulusLogical = 2;

    private PreviewModel? _model;
    private Font? _legendFont;

    public SettingsPreviewPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        TabStop = false;
    }

    // A beállítások, a mérési képernyő mérete és a hozzájuk tartozó StageFeasibility (null, ha a
    // beállítások hibásak, és ezért nem értékelhetők a képernyőhöz). Hibás beállításoknál is a lehető
    // legtöbbet mutatja: értelmezhetetlen színnél az utolsó érvényes színt.
    public void UpdatePreview(ExperimentSettings settings, Size screenSize, StageFeasibility? feasibility)
    {
        _model = PreviewModel.Create(settings, screenSize, feasibility, _model);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        _legendFont?.Dispose();
        _legendFont = null;
        base.OnFontChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        if (_model is not { } model || model.ScreenSize.Width <= 0 || model.ScreenSize.Height <= 0)
        {
            return;
        }

        var width = ClientSize.Width;
        var height = ClientSize.Height;
        var captionGap = LogicalToDeviceUnits(CaptionGapLogical);
        var sectionGap = LogicalToDeviceUnits(SectionGapLogical);
        var legendFont = GetLegendFont();
        const TextFormatFlags captionFlags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        const TextFormatFlags legendFlags = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak;

        var previewCaption = string.Format(
            CultureInfo.CurrentCulture,
            "Előnézet – {0} × {1} px képernyő, kicsinyítve",
            model.ScreenSize.Width,
            model.ScreenSize.Height);
        var legendText = model.AcceptanceCenter.HasValue ? LegendText + AcceptanceLegendText : LegendText;
        var captionHeight = TextRenderer.MeasureText(graphics, previewCaption, Font, new Size(width, int.MaxValue), captionFlags).Height;
        var legendHeight = TextRenderer.MeasureText(graphics, legendText, legendFont, new Size(width, int.MaxValue), legendFlags).Height;

        // A kicsinyített modell a teljes szélességet kapja (a képernyő oldalarányával), ha elfér a valós
        // méretű keret fölött; különben előbb a keret, majd a modell lesz kisebb.
        var boxWidth = Math.Min(width, LogicalToDeviceUnits(TrueSizeBoxWidthLogical));
        var boxHeight = LogicalToDeviceUnits(TrueSizeBoxHeightLogical);
        var available = Math.Max(1, height - ((2 * captionHeight) + (3 * captionGap) + legendHeight + sectionGap));
        var previewWidth = (float)width;
        var previewHeight = previewWidth * model.ScreenSize.Height / model.ScreenSize.Width;
        if (previewHeight + boxHeight > available)
        {
            boxHeight = (int)Math.Clamp(available - previewHeight, LogicalToDeviceUnits(MinTrueSizeBoxHeightLogical), boxHeight);
            if (previewHeight > available - boxHeight)
            {
                previewHeight = Math.Max(1, available - boxHeight);
                previewWidth = previewHeight * model.ScreenSize.Width / model.ScreenSize.Height;
            }
        }

        var y = 0;
        TextRenderer.DrawText(graphics, previewCaption, Font, new Rectangle(0, y, width, captionHeight), CaptionColor, captionFlags);
        y += captionHeight + captionGap;

        var previewRect = new RectangleF(
            MathF.Floor((width - previewWidth) / 2f),
            y,
            MathF.Floor(previewWidth),
            MathF.Floor(previewHeight));
        PaintScaledStage(graphics, model, previewRect);
        y = (int)previewRect.Bottom + captionGap;

        TextRenderer.DrawText(graphics, legendText, legendFont, new Rectangle(0, y, width, legendHeight), LegendColor, legendFlags);
        y += legendHeight + sectionGap;

        var clipped = model.DiameterPx > boxWidth || model.DiameterPx > boxHeight;
        var trueSizeCaption = string.Format(CultureInfo.CurrentCulture, "Valós méret: {0} px", model.DiameterPx)
            + (clipped ? " – nagyobb, mint a keret" : string.Empty);
        TextRenderer.DrawText(graphics, trueSizeCaption, Font, new Rectangle(0, y, width, captionHeight), CaptionColor, captionFlags);
        y += captionHeight + captionGap;

        PaintTrueSizeBox(graphics, model, new Rectangle((width - boxWidth) / 2, y, boxWidth, boxHeight));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _legendFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Font GetLegendFont()
    {
        return _legendFont ??= new Font(Font.FontFamily, Math.Max(6f, Font.SizeInPoints - 1.5f), FontStyle.Regular, GraphicsUnit.Point);
    }

    // A mérési képernyő kicsinyítve: minden elem a teljes méretű mérőfelület koordinátáiból skálázva.
    private void PaintScaledStage(Graphics graphics, PreviewModel model, RectangleF rect)
    {
        var scale = rect.Width / model.ScreenSize.Width;
        PointF Map(PointF point) => new(rect.X + (point.X * scale), rect.Y + (point.Y * scale));
        RectangleF MapRect(RectangleF source) => new(rect.X + (source.X * scale), rect.Y + (source.Y * scale), source.Width * scale, source.Height * scale);

        graphics.SmoothingMode = SmoothingMode.None;
        using (var background = new SolidBrush(model.BackgroundColor))
        {
            graphics.FillRectangle(background, rect);
        }

        var state = graphics.Save();
        graphics.SetClip(rect);

        var marker = model.PhotodiodeBounds;
        if (!marker.IsEmpty)
        {
            StagePainter.DrawPhotodiodeMarker(
                graphics, MapRect(PhotodiodeMarker.GetGuardBounds(marker)), MapRect(marker), lit: false);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fixation = Map(model.FixationPoint);
        StagePainter.DrawDashedCircle(graphics, fixation, model.MinEccentricityPx * scale, model.GuideColor, 1f);
        StagePainter.DrawDashedCircle(graphics, fixation, model.MaxEccentricityPx * scale, model.GuideColor, 1f);

        if (model.SampleStimulus is { } sample)
        {
            var mapped = MapRect(sample);
            var minSize = LogicalToDeviceUnits(MinStimulusLogical);
            if (mapped.Width < minSize)
            {
                mapped.Inflate((minSize - mapped.Width) / 2f, (minSize - mapped.Height) / 2f);
            }

            using var stimulusBrush = new SolidBrush(model.StimulusColor);
            graphics.FillEllipse(stimulusBrush, mapped);
        }

        if (model.AcceptanceCenter is { } acceptanceCenter)
        {
            StagePainter.DrawDashedCircle(
                graphics, Map(acceptanceCenter), model.AcceptanceRadiusPx * scale, StagePainter.AcceptanceRingColor, 1.5f);
        }

        // A kereszt legalább MinCrossArmLogical méretű, hogy kicsinyítve is látsszon.
        var arm = Math.Max(LogicalToDeviceUnits(MinCrossArmLogical), FixationCross.ArmLengthPx * scale);
        var thickness = Math.Max(1f, FixationCross.ThicknessPx * scale);
        var outline = Math.Max(1f, FixationCross.OutlinePx * scale);
        StagePainter.DrawFixationCross(graphics, fixation, arm, thickness, outline, StagePainter.CrossOutlineColor, StagePainter.CrossColor);

        graphics.Restore(state);

        graphics.SmoothingMode = SmoothingMode.None;
        using var framePen = new Pen(FrameColor);
        graphics.DrawRectangle(framePen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
    }

    // Az inger valós méretben: a rajzolás fizikai képpontban történik (PerMonitorV2), így az átmérő
    // pontosan annyi képpont, mint a mérésben. A keretnél nagyobb inger levágva látszik.
    private static void PaintTrueSizeBox(Graphics graphics, PreviewModel model, Rectangle box)
    {
        graphics.SmoothingMode = SmoothingMode.None;
        using (var background = new SolidBrush(model.BackgroundColor))
        {
            graphics.FillRectangle(background, box);
        }

        var state = graphics.Save();
        graphics.SetClip(box);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = (float)model.DiameterPx;
        var center = new PointF(box.X + (box.Width / 2f), box.Y + (box.Height / 2f));
        using (var stimulusBrush = new SolidBrush(model.StimulusColor))
        {
            graphics.FillEllipse(stimulusBrush, center.X - (diameter / 2f), center.Y - (diameter / 2f), diameter, diameter);
        }

        graphics.Restore(state);

        graphics.SmoothingMode = SmoothingMode.None;
        using var framePen = new Pen(FrameColor);
        graphics.DrawRectangle(framePen, box.X, box.Y, box.Width - 1, box.Height - 1);
    }

    // Az előnézet kiszámolt adatai a teljes méretű mérőfelület koordinátáiban.
    private sealed record PreviewModel(
        Size ScreenSize,
        Color BackgroundColor,
        Color StimulusColor,
        Color GuideColor,
        PointF FixationPoint,
        Rectangle PhotodiodeBounds,
        int DiameterPx,
        int MinEccentricityPx,
        float MaxEccentricityPx,
        RectangleF? SampleStimulus,
        PointF? AcceptanceCenter,
        float AcceptanceRadiusPx)
    {
        public static PreviewModel Create(ExperimentSettings settings, Size screenSize, StageFeasibility? feasibility, PreviewModel? previous)
        {
            var backgroundColor = HexColor.TryParse(settings.Stimulus.BackgroundColor, out var background)
                ? background
                : previous?.BackgroundColor ?? UiTheme.FallbackStageBackground;
            var stimulusColor = HexColor.TryParse(settings.Stimulus.Color, out var stimulus)
                ? stimulus
                : previous?.StimulusColor ?? DefaultStimulusColor;

            var fixationPoint = feasibility?.FixationPoint ?? settings.Gaze.ResolveFixationPoint(screenSize);
            var photodiodeBounds = settings.Hardware.ResolvePhotodiodeBounds(screenSize);
            var diameter = settings.Stimulus.DiameterPx;
            var minDistance = settings.Stimulus.MinEccentricityPx;

            // A külső kör a képernyőn ténylegesen elérhető legnagyobb távolság (ha a beállított nem fér el,
            // a figyelmeztetés mellett itt is látszik, meddig juthat az inger).
            var maxDistance = feasibility?.EffectiveMaxEccentricityPx is { } effectiveMax
                ? (float)Math.Min(settings.Stimulus.MaxEccentricityPx, effectiveMax)
                : settings.Stimulus.MaxEccentricityPx;
            var sample = feasibility?.SampleStimulus;
            var acceptance = feasibility is not null
                ? feasibility.GetAcceptanceCircle(settings)
                : GazeAcceptance.GetCircle(settings, fixationPoint, stimulusBounds: null);

            return new PreviewModel(
                screenSize,
                backgroundColor,
                stimulusColor,
                StagePainter.GetGuideColor(backgroundColor),
                fixationPoint,
                photodiodeBounds,
                diameter,
                minDistance,
                maxDistance,
                sample,
                acceptance?.Center,
                acceptance?.RadiusPx ?? 0f);
        }
    }
}
