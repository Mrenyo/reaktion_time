using System.Drawing.Drawing2D;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A mérőfelület elemeinek (fixációs kereszt, fotodióda-jelző a védősávval, segédkörök) rajzolása egy
// helyen: ugyanezt használja a mérési jelenet (MeasurementView), a beállítási ablak előnézete és a két
// kijelölő ablak, így a megjelenésük biztosan azonos. Csak a UI-szálról hívható (a tollak gyorsítótára
// nem szálbiztos).
internal static class StagePainter
{
    internal static readonly Color AcceptanceRingColor = Color.FromArgb(255, 140, 0);
    internal static readonly Color CrossOutlineColor = Color.Black;
    internal static readonly Color CrossColor = Color.White;

    // A jelzőnégyzet körüli 1 képpontos keret színe (a fizikai szenzor felhelyezését segíti).
    internal static readonly Color PhotodiodeOutlineColor = Color.FromArgb(90, 90, 90);

    private static readonly Color DarkGuideColor = Color.FromArgb(45, 45, 45);
    private static readonly Color LightGuideColor = Color.FromArgb(225, 225, 225);

    // A mérési méretű kereszt két tolla; a mérési jelenet minden rajzolásakor kellenek, ezért nem
    // jönnek létre újra (az alkalmazás futása alatt élnek).
    private static Pen? _crossOutlinePen;
    private static Pen? _crossPen;

    // A fixációs kereszt a mérés méretében és színeivel (lásd FixationCross).
    internal static void DrawFixationCross(Graphics graphics, PointF center)
    {
        _crossOutlinePen ??= CreateFlatPen(CrossOutlineColor, FixationCross.ThicknessPx + (2 * FixationCross.OutlinePx));
        _crossPen ??= CreateFlatPen(CrossColor, FixationCross.ThicknessPx);

        var halfArm = FixationCross.ArmLengthPx / 2f;
        DrawCrossArms(graphics, _crossOutlinePen, center, halfArm + FixationCross.OutlinePx);
        DrawCrossArms(graphics, _crossPen, center, halfArm);
    }

    // A fixációs kereszt tetszőleges méretben és színnel, két lapos végű menetben: előbb a vastagabb,
    // a szárak végén outline-nal hosszabb körvonal, majd fölé a belső vonal, így a kereszt bármilyen
    // háttéren látszik.
    internal static void DrawFixationCross(
        Graphics graphics, PointF center, float armLength, float thickness, float outline, Color outlineColor, Color color)
    {
        var halfArm = armLength / 2f;
        using (var outlinePen = CreateFlatPen(outlineColor, thickness + (2 * outline)))
        {
            DrawCrossArms(graphics, outlinePen, center, halfArm + outline);
        }

        using var pen = CreateFlatPen(color, thickness);
        DrawCrossArms(graphics, pen, center, halfArm);
    }

    // A jelzőnégyzet körüli fekete védősáv.
    internal static void FillPhotodiodeGuard(Graphics graphics, RectangleF guardBounds) =>
        FillSharpRectangle(graphics, Brushes.Black, guardBounds);

    // A jelzőnégyzet: világítva (lit) fehér, különben fekete.
    internal static void FillPhotodiodeSquare(Graphics graphics, RectangleF markerBounds, bool lit) =>
        FillSharpRectangle(graphics, lit ? Brushes.White : Brushes.Black, markerBounds);

    // A teljes jelző a mérésen kívüli nézetekhez (előtér, előnézet, kijelölők): a védősáv, a négyzet és
    // körülötte az 1 képpontos szürke keret. A mérési jelenetben keret nincs, ott a védősáv és a
    // négyzet külön rajzolódik (FillPhotodiodeGuard, FillPhotodiodeSquare).
    internal static void DrawPhotodiodeMarker(Graphics graphics, RectangleF guardBounds, RectangleF markerBounds, bool lit)
    {
        FillPhotodiodeGuard(graphics, guardBounds);
        FillPhotodiodeSquare(graphics, markerBounds, lit);

        var previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        using (var outlinePen = new Pen(PhotodiodeOutlineColor))
        {
            graphics.DrawRectangle(outlinePen, markerBounds.X - 1, markerBounds.Y - 1, markerBounds.Width + 1, markerBounds.Height + 1);
        }

        graphics.SmoothingMode = previousSmoothingMode;
    }

    internal static void DrawDashedCircle(Graphics graphics, PointF center, float radius, Color color, float width)
    {
        if (radius <= 0 || !float.IsFinite(radius))
        {
            return;
        }

        using var pen = new Pen(color, width) { DashStyle = DashStyle.Dash };
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
    }

    // A segédvonalak (távolságkörök) színe: amelyik jobban elüt a háttértől, a sötét vagy a világos
    // (WCAG-kontrasztarány alapján), így bármilyen háttéren látszanak.
    internal static Color GetGuideColor(Color background)
    {
        return HexColor.ContrastRatio(background, Color.Black) >= HexColor.ContrastRatio(background, Color.White)
            ? DarkGuideColor
            : LightGuideColor;
    }

    internal static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(2 * radius, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Kitöltés élsimítás nélkül (a hívó élsimítási módja megmarad).
    private static void FillSharpRectangle(Graphics graphics, Brush brush, RectangleF bounds)
    {
        var previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.FillRectangle(brush, bounds);
        graphics.SmoothingMode = previousSmoothingMode;
    }

    private static Pen CreateFlatPen(Color color, float width) => new(color, width) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };

    private static void DrawCrossArms(Graphics graphics, Pen pen, PointF center, float halfLength)
    {
        graphics.DrawLine(pen, center.X - halfLength, center.Y, center.X + halfLength, center.Y);
        graphics.DrawLine(pen, center.X, center.Y - halfLength, center.X, center.Y + halfLength);
    }
}
