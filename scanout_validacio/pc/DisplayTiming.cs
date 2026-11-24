using System.Runtime.InteropServices;

namespace ScanoutValidacio;

// A kijelző képjelének időzítése. A mezők és a lekérdezés a reakcióidő-mérő
// Domain/Scanout.cs (DisplayTiming) és Infrastructure/Display/DisplayTimingReader.cs fájljával
// azonosak, hogy a validáció pontosan azt a számítást ellenőrizze, amelyet a mérőprogram használ.
internal sealed record DisplayTiming(
    double RefreshRateHz,
    int VerticalActiveLines,
    int VerticalTotalLines,
    int DesktopHeightPx,
    bool IsExact,
    string Source,
    string? Warning,
    string? MonitorName)
{
    public double FrameTimeUs => 1_000_000d / RefreshRateHz;

    // Egy képpontsorra jutó scanout-idő (µs): lineTime * linesPerRow.
    public double MicrosecondsPerRow(int displayHeight)
    {
        var activeLines = VerticalActiveLines > 0 ? VerticalActiveLines : displayHeight;
        var totalLines = VerticalTotalLines > 0 ? VerticalTotalLines : activeLines;
        var lineTimeUs = FrameTimeUs / totalLines;
        var linesPerRow = activeLines / (double)displayHeight;
        return lineTimeUs * linesPerRow;
    }

    // A reakcióidő-mérő ScanoutTrialParameters.CalculateScanoutOffsetMicroseconds képlete:
    //   (stimulusY - photodiodeY) * linesPerRow * lineTimeUs
    // Itt a "stimulus" a mérő négyzet, a "photodiode" a referencia-jelző; az eredmény a mérő
    // fotodióda fényjelének várt késése a referenciáéhoz képest (t_mero - t_ref).
    public double ScanoutOffsetUs(double pointY, double referenceY, int displayHeight)
    {
        return (pointY - referenceY) * MicrosecondsPerRow(displayHeight);
    }
}

internal static class DisplayTimingReader
{
    private const uint QdcOnlyActivePaths = 0x2;
    private const uint ModeInfoTypeSource = 1;
    private const uint ModeInfoTypeTarget = 2;
    private const uint DeviceInfoGetSourceName = 1;
    private const uint DeviceInfoGetTargetName = 2;
    private const uint RotationIdentity = 1;
    private const uint ScanLineOrderingProgressive = 1;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int EnumCurrentSettings = -1;

    public static DisplayTiming Read(string screenDeviceName, Rectangle screenBounds)
    {
        try
        {
            var exact = TryReadFromDisplayConfig(screenDeviceName, screenBounds);
            if (exact is not null)
            {
                return exact;
            }
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException || ex is ExternalException)
        {
        }

        var mode = DevMode.Create();
        if (!EnumDisplaySettings(screenDeviceName, EnumCurrentSettings, ref mode) || mode.DisplayFrequency <= 1)
        {
            throw new InvalidOperationException($"Nem határozható meg a(z) {screenDeviceName} kijelző képfrissítési frekvenciája.");
        }

        return new DisplayTiming(
            mode.DisplayFrequency, 0, 0, screenBounds.Height, IsExact: false, "EnumDisplaySettings",
            "A pontos képidőzítés nem kérdezhető le: a frekvencia egész Hz-re kerekített, a korrekció a holtidő nélkül közelít.",
            MonitorName: null);
    }

    private static DisplayTiming? TryReadFromDisplayConfig(string screenDeviceName, Rectangle screenBounds)
    {
        if (!TryQueryActivePaths(out var paths, out var modes))
        {
            return null;
        }

        foreach (var path in paths)
        {
            if (!string.Equals(GetSourceGdiName(path.SourceInfo), screenDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.TargetInfo.ModeInfoIdx >= modes.Length || modes[path.TargetInfo.ModeInfoIdx].InfoType != ModeInfoTypeTarget)
            {
                return null;
            }

            var signal = modes[path.TargetInfo.ModeInfoIdx].TargetMode.TargetVideoSignalInfo;
            var refreshRate = ToDouble(signal.VSyncFreq);
            if (!(refreshRate > 1))
            {
                refreshRate = ToDouble(path.TargetInfo.RefreshRate);
            }

            var desktopHeight = screenBounds.Height;
            if (path.SourceInfo.ModeInfoIdx < modes.Length && modes[path.SourceInfo.ModeInfoIdx].InfoType == ModeInfoTypeSource)
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
                warning = "A kijelző el van forgatva: a scanout-korrekció a fentről lefelé haladó sorkirajzolást feltételezi.";
            }
            else if (signal.ScanLineOrdering != ScanLineOrderingProgressive)
            {
                warning = "A kijelző nem progresszív (váltottsoros) képjelet kap.";
            }

            return new DisplayTiming(
                refreshRate,
                (int)signal.ActiveSize.Cy,
                (int)signal.TotalSize.Cy,
                desktopHeight,
                IsExact: warning is null,
                "QueryDisplayConfig",
                warning,
                GetMonitorName(path.TargetInfo));
        }

        return null;
    }

    private static bool TryQueryActivePaths(out DisplayConfigPathInfo[] paths, out DisplayConfigModeInfo[] modes)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != ErrorSuccess)
            {
                break;
            }

            paths = new DisplayConfigPathInfo[pathCount];
            modes = new DisplayConfigModeInfo[modeCount];
            var result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result == ErrorSuccess)
            {
                Array.Resize(ref paths, (int)pathCount);
                Array.Resize(ref modes, (int)modeCount);
                return true;
            }

            if (result != ErrorInsufficientBuffer)
            {
                break;
            }
        }

        paths = [];
        modes = [];
        return false;
    }

    private static string? GetSourceGdiName(DisplayConfigPathSourceInfo source)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = CreateHeader(DeviceInfoGetSourceName, Marshal.SizeOf<DisplayConfigSourceDeviceName>(), source.AdapterId, source.Id)
        };

        return DisplayConfigGetDeviceInfo(ref request) == ErrorSuccess ? request.ViewGdiDeviceName : null;
    }

    private static string? GetMonitorName(DisplayConfigPathTargetInfo target)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Header = CreateHeader(DeviceInfoGetTargetName, Marshal.SizeOf<DisplayConfigTargetDeviceName>(), target.AdapterId, target.Id)
        };

        return DisplayConfigGetDeviceInfo(ref request) == ErrorSuccess && !string.IsNullOrWhiteSpace(request.MonitorFriendlyDeviceName)
            ? request.MonitorFriendlyDeviceName
            : null;
    }

    private static DisplayConfigDeviceInfoHeader CreateHeader(uint type, int size, Luid adapterId, uint id) =>
        new() { Type = type, Size = (uint)size, AdapterId = adapterId, Id = id };

    private static double ToDouble(DisplayConfigRational rational) =>
        rational.Denominator == 0 ? 0 : rational.Numerator / (double)rational.Denominator;

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

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

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
    private struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
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

        public static DevMode Create() => new()
        {
            DeviceName = string.Empty,
            FormName = string.Empty,
            Size = (ushort)Marshal.SizeOf<DevMode>()
        };
    }
}
