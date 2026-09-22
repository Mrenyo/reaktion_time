using System.Runtime.InteropServices;

namespace ReactionTimeMeasurementApp.Classes;

// A kijelző képjelének időzítése a scanout-korrekcióhoz.
//   RefreshRateHz        - pontos (tört) képfrissítési frekvencia, pl. 59,94.
//   VerticalActiveLines  - a látható sorok száma a képjelben.
//   VerticalTotalLines   - az összes sor, a képkockák közti holtidővel (vertical blanking) együtt.
//                          Egy sor kirajzolása frameTime / VerticalTotalLines ideig tart.
//   DesktopHeightPx      - az asztal magassága képpontban (eltérhet az aktív sorok számától, ha a
//                          videokártya skáláz).
// Ha a pontos időzítés nem kérdezhető le, a sorszámok 0-k (a korrekció a holtidő nélküli
// közelítéssel számol), és az IsExact hamis - ezt a mérés a metaadatokban rögzíti.
internal sealed record DisplayTiming(
    double RefreshRateHz,
    int VerticalActiveLines,
    int VerticalTotalLines,
    int DesktopHeightPx,
    bool IsExact,
    string Source,
    string? Warning);

internal static class DisplayTimingReader
{
    private const uint QdcOnlyActivePaths = 0x2;
    private const uint ModeInfoTypeSource = 1;
    private const uint ModeInfoTypeTarget = 2;
    private const uint DeviceInfoGetSourceName = 1;
    private const uint RotationIdentity = 1;
    private const uint ScanLineOrderingProgressive = 1;
    private const int ErrorSuccess = 0;
    private const int EnumCurrentSettings = -1;

    internal static DisplayTiming Read(Screen screen)
    {
        try
        {
            var exact = TryReadFromDisplayConfig(screen);
            if (exact is not null)
            {
                return exact;
            }
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException || ex is ExternalException)
        {
            Logger.Warning($"A QueryDisplayConfig nem használható: {ex.Message}");
        }

        return ReadFromEnumDisplaySettings(screen);
    }

    private static DisplayTiming? TryReadFromDisplayConfig(Screen screen)
    {
        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != ErrorSuccess)
        {
            return null;
        }

        var paths = new DisplayConfigPathInfo[pathCount];
        var modes = new DisplayConfigModeInfo[modeCount];
        if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != ErrorSuccess)
        {
            return null;
        }

        for (var i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            if (!string.Equals(GetSourceGdiName(path.SourceInfo), screen.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.TargetInfo.ModeInfoIdx >= modeCount || modes[path.TargetInfo.ModeInfoIdx].InfoType != ModeInfoTypeTarget)
            {
                return null;
            }

            var signal = modes[path.TargetInfo.ModeInfoIdx].TargetMode.TargetVideoSignalInfo;
            var refreshRate = ToDouble(signal.VSyncFreq);
            if (!(refreshRate > 1))
            {
                refreshRate = ToDouble(path.TargetInfo.RefreshRate);
            }

            var desktopHeight = screen.Bounds.Height;
            if (path.SourceInfo.ModeInfoIdx < modeCount && modes[path.SourceInfo.ModeInfoIdx].InfoType == ModeInfoTypeSource)
            {
                desktopHeight = (int)modes[path.SourceInfo.ModeInfoIdx].SourceMode.Height;
            }

            if (!(refreshRate > 1) || signal.ActiveSize.Cy == 0 || signal.TotalSize.Cy < signal.ActiveSize.Cy || desktopHeight <= 0)
            {
                return null;
            }

            string? warning = null;
            if (path.TargetInfo.Rotation != RotationIdentity)
            {
                warning = "A kijelző el van forgatva: a scanout-korrekció a fentről lefelé haladó sorkirajzolást feltételezi, így nem érvényes.";
            }
            else if (signal.ScanLineOrdering != ScanLineOrderingProgressive)
            {
                warning = "A kijelző nem progresszív (váltottsoros) képjelet kap: a scanout-korrekció nem érvényes.";
            }

            return new DisplayTiming(
                refreshRate,
                (int)signal.ActiveSize.Cy,
                (int)signal.TotalSize.Cy,
                desktopHeight,
                IsExact: warning is null,
                Source: "QueryDisplayConfig",
                warning);
        }

        return null;
    }

    private static DisplayTiming ReadFromEnumDisplaySettings(Screen screen)
    {
        var mode = DevMode.Create();
        if (!EnumDisplaySettings(screen.DeviceName, EnumCurrentSettings, ref mode) || mode.DisplayFrequency <= 1)
        {
            throw new MeasurementSetupException($"Nem határozható meg a(z) {screen.DeviceName} kijelző képfrissítési frekvenciája.");
        }

        return new DisplayTiming(
            mode.DisplayFrequency,
            VerticalActiveLines: 0,
            VerticalTotalLines: 0,
            screen.Bounds.Height,
            IsExact: false,
            Source: "EnumDisplaySettings",
            Warning: "A pontos képidőzítés nem kérdezhető le: a frekvencia egész Hz-re kerekített, a korrekció a képkockák közti holtidő nélkül közelít.");
    }

    private static string? GetSourceGdiName(DisplayConfigPathSourceInfo source)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DeviceInfoGetSourceName,
                Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                AdapterId = source.AdapterId,
                Id = source.Id
            }
        };

        return DisplayConfigGetDeviceInfo(ref request) == ErrorSuccess ? request.ViewGdiDeviceName : null;
    }

    private static double ToDouble(DisplayConfigRational rational)
    {
        return rational.Denominator == 0 ? 0 : rational.Numerator / (double)rational.Denominator;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DevMode deviceMode);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfig2DRegion
    {
        public uint Cx;
        public uint Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DisplayConfigRational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigVideoSignalInfo
    {
        public ulong PixelRate;
        public DisplayConfigRational HSyncFreq;
        public DisplayConfigRational VSyncFreq;
        public DisplayConfig2DRegion ActiveSize;
        public DisplayConfig2DRegion TotalSize;
        public uint VideoStandard;
        public uint ScanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigTargetMode
    {
        public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigSourceMode
    {
        public uint Width;
        public uint Height;
        public uint PixelFormat;
        public int PositionX;
        public int PositionY;
    }

    // A natív DISPLAYCONFIG_MODE_INFO egy unió: a 16. bájttól vagy a cél-, vagy a forrásmód áll.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct DisplayConfigModeInfo
    {
        [FieldOffset(0)]
        public uint InfoType;

        [FieldOffset(4)]
        public uint Id;

        [FieldOffset(8)]
        public Luid AdapterId;

        [FieldOffset(16)]
        public DisplayConfigTargetMode TargetMode;

        [FieldOffset(16)]
        public DisplayConfigSourceMode SourceMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint IcmMethod;
        public uint IcmIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;

        public static DevMode Create()
        {
            return new DevMode
            {
                DeviceName = string.Empty,
                FormName = string.Empty,
                Size = (ushort)Marshal.SizeOf<DevMode>()
            };
        }
    }
}

// A mérés előkészítése során felmerülő, a felhasználónak szóló (nem programhiba jellegű) probléma,
// pl. nincs beállítva a Tobii display area, vagy a beállított excentricitás nem fér el a képernyőn.
internal sealed class MeasurementSetupException : Exception
{
    public MeasurementSetupException(string message)
        : base(message)
    {
    }
}
