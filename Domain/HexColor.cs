using System.Globalization;

namespace ReactionTimeMeasurementApp.Domain;

// A beállításfájl színei "#RRGGBB" alakú szövegek. Csak teljesen átlátszatlan színek adhatók meg:
// az inger és a háttér a valódi képernyőn mindig átlátszatlan, egy alfa érték csak félrevezetne.
public static class HexColor
{
    // Elfogadja a "#RRGGBB" alakot (kis- és nagybetűvel is, a szöveg elején és végén szóközzel).
    public static bool TryParse(string? text, out Color color)
    {
        color = Color.Empty;
        if (text is null)
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < trimmed.Length; i++)
        {
            if (!char.IsAsciiHexDigit(trimmed[i]))
            {
                return false;
            }
        }

        var rgb = int.Parse(trimmed.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    // Egységes, nagybetűs "#RRGGBB" alak (az alfa csatorna elhagyásával).
    public static string Format(Color color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    // A két szín kontrasztaránya a WCAG 2 definíciója szerint: (L1 + 0,05) / (L2 + 0,05), ahol L a
    // relatív fénysűrűség (L1 a világosabb). Értéke 1 (azonos színek) és 21 (fekete-fehér) közé esik.
    public static double ContrastRatio(Color a, Color b)
    {
        var luminanceA = RelativeLuminance(a);
        var luminanceB = RelativeLuminance(b);
        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);
        return (lighter + 0.05) / (darker + 0.05);
    }

    // Az sRGB csatornák linearizálása után súlyozott összeg (WCAG 2 relatív fénysűrűség).
    private static double RelativeLuminance(Color color)
    {
        return (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));
    }

    private static double Linearize(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
