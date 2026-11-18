using Microsoft.Win32;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Infrastructure.Display;

// A monitor EDID-blokkjának beolvasása a registryből és a fizikai (látható) képméret kiolvasása belőle.
internal static class EdidReader
{
    private const int BaseBlockLength = 128;
    private const int FirstDetailedTimingOffset = 54;

    // A részletes és az alapadatokban (cm) megadott méret ennyinél jobban nem térhet el egymástól.
    private const double MaxSizeDisagreementMm = 20;
    private const double MinPlausibleSizeMm = 10;

    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    // A QueryDisplayConfig monitor-eszközútvonalából ("\\?\DISPLAY#<hardverazonosító>#<példány>#{GUID}")
    // a HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\<hardverazonosító>\<példány>\Device Parameters\EDID
    // értéket olvassa ki; null, ha az útvonal más formájú vagy az érték hiányzik.
    internal static byte[]? ReadEdid(string monitorDevicePath)
    {
        var parts = monitorDevicePath.Split('#');
        if (parts.Length < 3
            || !parts[0].EndsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)
            || !IsValidKeyName(parts[1])
            || !IsValidKeyName(parts[2]))
        {
            return null;
        }

        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters");
        return key?.GetValue("EDID") as byte[];
    }

    private static bool IsValidKeyName(string name) => name.Length > 0 && !name.Contains('\\');

    // A méret az első részletes időzítés-leíróból (DTD, 54. bájttól) jön milliméterben; ha az nem ad
    // értéket, az alapadatok (21-22. bájt) centiméteres mérete. A "problem" a null eredmény oka.
    internal static PhysicalDisplaySize? ParsePhysicalSize(byte[] edid, out string? problem)
    {
        problem = null;
        if (edid.Length < BaseBlockLength || !edid.AsSpan(0, Header.Length).SequenceEqual(Header))
        {
            problem = "érvénytelen EDID-fejléc.";
            return null;
        }

        byte checksum = 0;
        foreach (var value in edid.AsSpan(0, BaseBlockLength))
        {
            checksum += value;
        }

        if (checksum != 0)
        {
            problem = "hibás EDID-ellenőrzőösszeg.";
            return null;
        }

        double basicWidth = edid[21] * 10;
        double basicHeight = edid[22] * 10;

        // A leíró csak akkor időzítés, ha az első két bájtja (pixelórajel) nem nulla.
        var dtd = FirstDetailedTimingOffset;
        var isTimingDescriptor = edid[dtd] != 0 || edid[dtd + 1] != 0;
        double width = isTimingDescriptor ? edid[dtd + 12] | ((edid[dtd + 14] & 0xF0) << 4) : 0;
        double height = isTimingDescriptor ? edid[dtd + 13] | ((edid[dtd + 14] & 0x0F) << 8) : 0;

        if (width < MinPlausibleSizeMm || height < MinPlausibleSizeMm)
        {
            (width, height) = (basicWidth, basicHeight);
        }
        else if (basicWidth > 0 && basicHeight > 0
            && (Math.Abs(width - basicWidth) > MaxSizeDisagreementMm || Math.Abs(height - basicHeight) > MaxSizeDisagreementMm))
        {
            // Hibás EDID: nem tudni, melyik érték a helyes, ezért egyiket sem használjuk.
            problem = $"a részletes ({width:0} x {height:0} mm) és az alapadatok ({basicWidth:0} x {basicHeight:0} mm) mérete ellentmond egymásnak.";
            return null;
        }

        if (width < MinPlausibleSizeMm || height < MinPlausibleSizeMm)
        {
            // Az alapadatokban a 0 "ismeretlen/változó méretet" (pl. projektor), vagy a másik értékkel
            // együtt csak képarányt jelent.
            problem = "az EDID nem tartalmaz fizikai méretet.";
            return null;
        }

        return new PhysicalDisplaySize(width, height);
    }
}
