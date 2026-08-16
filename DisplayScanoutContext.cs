using System.Runtime.InteropServices;
using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

internal static class DisplayScanoutContext
{
    internal static ScanoutTrialParameters Capture(
        Control stimulusSurface,
        RectangleF stimulusBounds,
        Control photodiodeIndicator)
    {
        if (stimulusBounds.Width <= 0 || stimulusBounds.Height <= 0)
        {
            throw new InvalidOperationException("Az inger pozíciója még nincs előkészítve.");
        }

        var stimulusSurfaceOrigin = stimulusSurface.PointToScreen(Point.Empty);
        var stimulusCenter = new Point(
            (int)Math.Round(stimulusSurfaceOrigin.X + stimulusBounds.Left + (stimulusBounds.Width / 2d)),
            (int)Math.Round(stimulusSurfaceOrigin.Y + stimulusBounds.Top + (stimulusBounds.Height / 2d)));

        var photodiodeOrigin = photodiodeIndicator.PointToScreen(Point.Empty);
        var photodiodeCenter = new Point(
            photodiodeOrigin.X + (photodiodeIndicator.Width / 2),
            photodiodeOrigin.Y + (photodiodeIndicator.Height / 2));

        var stimulusScreen = Screen.FromPoint(stimulusCenter);
        var photodiodeScreen = Screen.FromPoint(photodiodeCenter);
        if (!string.Equals(stimulusScreen.DeviceName, photodiodeScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Az ingernek és a fotodiódának ugyanazon a kijelzőn kell lennie.");
        }

        var parameters = new ScanoutTrialParameters(
            stimulusCenter.Y - stimulusScreen.Bounds.Top,
            photodiodeCenter.Y - stimulusScreen.Bounds.Top,
            stimulusScreen.Bounds.Height,
            DisplayRefreshRate.GetHertz(stimulusScreen));

        parameters.Validate();
        return parameters;
    }
}

internal static class DisplayRefreshRate
{
    private const int EnumCurrentSettings = -1;

    internal static double GetHertz(Screen screen)
    {
        var mode = DevMode.Create();
        if (!EnumDisplaySettings(screen.DeviceName, EnumCurrentSettings, ref mode)
            || mode.DisplayFrequency <= 1)
        {
            throw new InvalidOperationException(
                $"Nem határozható meg a(z) {screen.DeviceName} kijelző képfrissítési frekvenciája.");
        }

        return mode.DisplayFrequency;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string deviceName,
        int modeNumber,
        ref DevMode deviceMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        private const int DeviceNameLength = 32;
        private const int FormNameLength = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DeviceNameLength)]
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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = FormNameLength)]
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
