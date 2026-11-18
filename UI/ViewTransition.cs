using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.UI;

// Sima nézetváltás (áttűnés) a főablakban. A WinForms a vezérlőket egyenként rajzolja ki, ezért egy
// nézet megjelenésekor látszana, ahogy az elemei sorban felépülnek. Ehelyett:
//   1. a képernyő érintett részéről pillanatkép készül, és egy külön takaró ablak pontosan a helyén
//      mutatja (a képernyőn ettől semmi nem változik);
//   2. a takaró alatt megtörténik a váltás, és az új nézet egyetlen szinkron újrarajzolással teljesen
//      elkészül (ezt a hívó végzi, a Cover által visszaadott objektum Dispose-a előtt);
//   3. a takaró rövid idő alatt elhalványul, így a régi kép simán átúszik az újba.
//
// A takaró rétegzett (layered) ablak: a tartalmát és az átlátszóságát az UpdateLayeredWindow adja,
// így az elhalványítást a DWM végzi, közben sem a takarót, sem a főablakot nem kell újrarajzolni. A
// takaró nem aktiválható (a fókusz és a billentyűzet a főablakon marad), és amíg látszik, elnyeli a
// kattintásokat, hogy egy dupla kattintás második fele ne az épp megjelenő nézeten működjön. Amíg a
// takaró látszik, a főablak saját DWM-animációi (pl. a teljes méretre váltásé) ki vannak kapcsolva,
// mert azok a takaró alatt még futnának, és az áttűnés közben előbukkannának.
//
// Ugyanez a takaró indításkor az asztal képéből tünteti át a főablakot (HideOwnerUntilRevealed,
// RevealOwner). Ha a takaró nem hozható létre (pl. nem olvasható a képernyő), a nézet áttűnés nélkül
// vált: a váltás a nézetek működésén semmit nem változtat.
internal sealed class ViewTransition : IDisposable
{
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_SETCURSOR = 0x0020;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int WM_DPICHANGED = 0x02E0;
    private static readonly IntPtr MA_NOACTIVATEANDEAT = new(4);

    private const int SW_HIDE = 0;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int RDW_INVALIDATE = 0x0001;
    private const int RDW_ERASE = 0x0004;
    private const int RDW_ALLCHILDREN = 0x0080;
    private const int RDW_UPDATENOW = 0x0100;
    private const int RDW_FRAME = 0x0400;

    private const int SRCCOPY = 0x00CC0020;
    private const int ULW_ALPHA = 0x00000002;
    private const byte AC_SRC_OVER = 0x00;

    private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
    private const int DWMWA_CLOAK = 13;

    // A WinForms-időzítő felbontása ~15,6 ms, így ez a gyakorlatban képernyőfrissítésenként egy lépés.
    private const int FrameIntervalMs = 10;

    // Egy lépés legfeljebb ennyivel viheti előre az áttűnést. Ha a UI-szál közben foglalt volt (pl. egy
    // késve érkező újrarajzolás miatt), az áttűnés onnan folytatódik, ahol tartott, és nem ugrik a végére.
    private static readonly TimeSpan MaxFrameStep = TimeSpan.FromMilliseconds(34);

    // Ha indításkor az OnShown valamiért nem jelenítené meg az ablakot, ennyi idő után biztosan látható lesz.
    private const int RevealFallbackMs = 3000;

    private readonly Form _owner;
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = FrameIntervalMs };
    private readonly System.Windows.Forms.Timer _revealFallbackTimer = new() { Interval = RevealFallbackMs };
    private readonly Stopwatch _fadeClock = new();

    private OverlayWindow? _overlay;
    private TimeSpan _fadeDuration;
    private TimeSpan _fadeElapsed;
    private TimeSpan _lastFrameTime;
    private int _generation;
    private bool _isCovering;
    private bool _isFading;
    private bool _ownerCloaked;
    private bool _ownerRevealed;
    private bool _disposed;

    public ViewTransition(Form owner)
    {
        _owner = owner;
        _frameTimer.Tick += (_, _) => AdvanceFade();
        _revealFallbackTimer.Tick += (_, _) => UncloakOwner();
    }

    // Igaz, amíg a takaró látszik (a váltás vagy az áttűnés tart).
    public bool IsActive => _isCovering;

    // A megadott képernyőterület (fizikai képpontban) befedése a jelenlegi képével. A visszaadott objektum
    // Dispose-a indítja az áttűnést, ezért a hívó addigra rajzolja ki teljesen az új állapotot. Ha a
    // befedés nem lehetséges (pl. a főablak nem látszik, vagy kis méretre van csukva), a Dispose nem
    // csinál semmit, és a váltás áttűnés nélkül látszik. aboveTaskbar: a takaró a tálca fölé is kerül
    // (a teljes képernyőre és onnan vissza váltáskor a tálca is megjelenik vagy eltűnik).
    public IDisposable Cover(Rectangle screenArea, bool aboveTaskbar, TimeSpan fadeDuration)
    {
        if (_isCovering)
        {
            // Egy még tartó áttűnés azonnal véget ér. A DwmFlush megvárja, hogy a takaró a képernyőről is
            // eltűnjön, különben az új pillanatképre is rákerülne.
            Finish();
            _ = DwmFlush();
        }

        if (_disposed
            || !_owner.IsHandleCreated
            || !_owner.Visible
            || _owner.WindowState == FormWindowState.Minimized
            || screenArea.Width <= 0
            || screenArea.Height <= 0)
        {
            return NoTransition.Instance;
        }

        try
        {
            if (_overlay is null || _overlay.Handle == IntPtr.Zero)
            {
                _overlay = new OverlayWindow(_owner.Handle);
            }

            // Ha a terület a főablak tartalmán belül van, a kép a főablak saját (a DWM által tárolt)
            // felületéről másolható, ami sokkal gyorsabb a képernyőről másolásnál. Indításkor (amíg a
            // főablak láthatatlan) és ha a tálca is a képen van, a képernyőről kell másolni.
            var clientArea = _owner.RectangleToScreen(_owner.ClientRectangle);
            var copyFromOwner = !_ownerCloaked && clientArea.Contains(screenArea);
            _overlay.ShowSnapshot(
                screenArea,
                aboveTaskbar,
                copyFromOwner ? _owner.Handle : IntPtr.Zero,
                copyFromOwner ? clientArea.Location : Point.Empty);
        }
        catch (Win32Exception ex)
        {
            Logger.Warning($"A nézetváltás áttűnése nem indítható, a nézet áttűnés nélkül vált: {ex.Message}");
            return NoTransition.Instance;
        }

        _isCovering = true;
        _isFading = false;
        _fadeDuration = fadeDuration;
        _generation++;
        SetTransitionsDisabled(_owner.Handle, true);

        // Megvárjuk, hogy a takaró ténylegesen a képernyőre kerüljön, mielőtt alatta bármi változna.
        _ = DwmFlush();
        return new FadeOnDispose(this, _generation);
    }

    // Indításkor, a főablak OnHandleCreated-jében: az ablak láthatatlan marad (DWM-cloak), amíg a
    // RevealOwner meg nem jeleníti. Ha a DWM ezt nem támogatja, az ablak a szokásos módon jelenik meg.
    public void HideOwnerUntilRevealed()
    {
        if (_ownerRevealed || _ownerCloaked)
        {
            return;
        }

        _ownerCloaked = SetCloaked(_owner.Handle, true);
        if (_ownerCloaked)
        {
            _revealFallbackTimer.Start();
        }
    }

    // Indításkor, a főablak OnShown-jában: az ablak minden eleme kirajzolódik, amíg még láthatatlan, majd az
    // ablak az asztal képéből áttűnéssel jelenik meg.
    public void RevealOwner(TimeSpan fadeDuration)
    {
        _ownerRevealed = true;
        if (!_ownerCloaked)
        {
            return;
        }

        // A láthatatlan főablak nem kerül a pillanatképre, így a takaró az asztalt (a mögötte lévő ablakokat) mutatja.
        var area = Rectangle.Intersect(_owner.Bounds, Screen.FromControl(_owner).Bounds);
        using (Cover(area, aboveTaskbar: false, fadeDuration))
        {
            try
            {
                RedrawNow(_owner.Handle);
            }
            finally
            {
                UncloakOwner();
            }
        }
    }

    // A takaró azonnali eltüntetése: az áttűnés végén, vagy egy újabb váltás előtt.
    private void Finish()
    {
        _frameTimer.Stop();
        _isFading = false;
        if (!_isCovering)
        {
            return;
        }

        _isCovering = false;
        _overlay?.Hide();
        if (_owner.IsHandleCreated)
        {
            SetTransitionsDisabled(_owner.Handle, false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Finish();
        UncloakOwner();
        _frameTimer.Dispose();
        _revealFallbackTimer.Dispose();
        if (_overlay is not null && _overlay.Handle != IntPtr.Zero)
        {
            _overlay.DestroyHandle();
        }
    }

    // Az egész ablak (keret, háttér, minden gyermek) azonnali, szinkron újrarajzolása.
    internal static void RedrawNow(IntPtr handle)
    {
        _ = RedrawWindow(
            handle,
            IntPtr.Zero,
            IntPtr.Zero,
            RDW_INVALIDATE | RDW_ERASE | RDW_FRAME | RDW_ALLCHILDREN | RDW_UPDATENOW);
    }

    private void StartFade(int generation)
    {
        // Csak a saját takarójához tartozó áttűnés indulhat (egy újabb Cover addigra lezárhatta).
        if (!_isCovering || _isFading || generation != _generation)
        {
            return;
        }

        // Az első lépésig (a WM_TIMER a legkisebb prioritású üzenet) a még függő rajzolások is lefutnak,
        // így az áttűnés már a teljesen kész új képet fedi fel.
        _isFading = true;
        _fadeElapsed = TimeSpan.Zero;
        _lastFrameTime = TimeSpan.Zero;
        _fadeClock.Restart();
        _frameTimer.Start();
    }

    private void AdvanceFade()
    {
        var overlay = _overlay;
        var now = _fadeClock.Elapsed;
        var step = now - _lastFrameTime;
        _lastFrameTime = now;
        _fadeElapsed += step < MaxFrameStep ? step : MaxFrameStep;
        var progress = _fadeDuration > TimeSpan.Zero ? _fadeElapsed / _fadeDuration : 1;
        if (overlay is null || progress >= 1)
        {
            Finish();
            return;
        }

        // Lassuló (ease-out, köbös) görbe: az új kép gyorsan előtűnik, a vége finoman simul el.
        var remaining = 1 - progress;
        var opacity = remaining * remaining * remaining;
        if (!overlay.TrySetOpacity((byte)Math.Round(opacity * byte.MaxValue)))
        {
            Finish();
        }
    }

    private void UncloakOwner()
    {
        _revealFallbackTimer.Stop();
        if (!_ownerCloaked)
        {
            return;
        }

        _ownerCloaked = false;
        if (_owner.IsHandleCreated)
        {
            _ = SetCloaked(_owner.Handle, false);
        }
    }

    private static bool SetCloaked(IntPtr handle, bool cloaked)
    {
        var value = cloaked ? 1 : 0;
        return DwmSetWindowAttribute(handle, DWMWA_CLOAK, ref value, sizeof(int)) >= 0;
    }

    private static void SetTransitionsDisabled(IntPtr handle, bool disabled)
    {
        var value = disabled ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));
    }

    private static BlendFunction CreateBlend(byte opacity) => new()
    {
        BlendOp = AC_SRC_OVER,
        BlendFlags = 0,
        SourceConstantAlpha = opacity,
        AlphaFormat = 0
    };

    // A takaró ablak: kattintásokat elnyelő, nem aktiválható, tálcán nem látszó rétegzett felugró ablak,
    // a főablak tulajdonában (így mindig fölötte marad, és vele együtt szűnik meg).
    private sealed class OverlayWindow : NativeWindow
    {
        public OverlayWindow(IntPtr ownerHandle)
        {
            CreateHandle(new CreateParams
            {
                Caption = string.Empty,
                Style = WS_POPUP,
                ExStyle = WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
                Parent = ownerHandle
            });

            // A takaró azonnal, teljes fedéssel jelenjen meg és tűnjön el, a DWM ne animálja.
            SetTransitionsDisabled(Handle, true);
        }

        // A terület jelenlegi képe a takaróra kerül, és a takaró aktiválás nélkül megjelenik a helyén. A kép
        // a sourceWindow ablak kliensterületéről (amelynek bal felső sarka a képernyőn sourceOrigin), vagy
        // ha az IntPtr.Zero, a képernyőről származik.
        public void ShowSnapshot(Rectangle area, bool aboveTaskbar, IntPtr sourceWindow, Point sourceOrigin)
        {
            var screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception("A képernyő tartalma nem olvasható.");
            }

            var sourceDc = sourceWindow == IntPtr.Zero ? screenDc : GetDC(sourceWindow);
            var memoryDc = IntPtr.Zero;
            var bitmap = IntPtr.Zero;
            var previousBitmap = IntPtr.Zero;
            try
            {
                if (sourceDc == IntPtr.Zero)
                {
                    throw new Win32Exception("A főablak tartalma nem olvasható.");
                }

                memoryDc = CreateCompatibleDC(screenDc);
                bitmap = CreateCompatibleBitmap(screenDc, area.Width, area.Height);
                if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
                {
                    throw new Win32Exception("Nem hozható létre a pillanatkép.");
                }

                previousBitmap = SelectObject(memoryDc, bitmap);
                if (!BitBlt(memoryDc, 0, 0, area.Width, area.Height, sourceDc, area.X - sourceOrigin.X, area.Y - sourceOrigin.Y, SRCCOPY))
                {
                    throw new Win32Exception("A képernyő tartalma nem másolható.");
                }

                // A rendszer a képet a takaró saját felületére másolja, így a bitkép utána felszabadítható.
                var position = new NativePoint { X = area.X, Y = area.Y };
                var size = new NativeSize { Width = area.Width, Height = area.Height };
                var source = new NativePoint();
                var blend = CreateBlend(byte.MaxValue);
                if (!UpdateLayeredWindow(Handle, screenDc, ref position, ref size, memoryDc, ref source, 0, ref blend, ULW_ALPHA))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                if (previousBitmap != IntPtr.Zero)
                {
                    _ = SelectObject(memoryDc, previousBitmap);
                }

                if (bitmap != IntPtr.Zero)
                {
                    _ = DeleteObject(bitmap);
                }

                if (memoryDc != IntPtr.Zero)
                {
                    _ = DeleteDC(memoryDc);
                }

                if (sourceWindow != IntPtr.Zero && sourceDc != IntPtr.Zero)
                {
                    _ = ReleaseDC(sourceWindow, sourceDc);
                }

                _ = ReleaseDC(IntPtr.Zero, screenDc);
            }

            if (!SetWindowPos(
                    Handle,
                    aboveTaskbar ? HWND_TOPMOST : HWND_NOTOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW))
            {
                throw new Win32Exception();
            }
        }

        // Csak az átlátszóság változik, a tartalom marad (ezért nem kell újra átadni a képet).
        public bool TrySetOpacity(byte opacity)
        {
            var blend = CreateBlend(opacity);
            return UpdateLayeredWindowOpacity(Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref blend, ULW_ALPHA);
        }

        public void Hide()
        {
            if (Handle != IntPtr.Zero)
            {
                _ = ShowWindow(Handle, SW_HIDE);
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                // Kattintásra sem aktiválódik, és a kattintást elnyeli.
                case WM_MOUSEACTIVATE:
                    m.Result = MA_NOACTIVATEANDEAT;
                    return;

                // Az egérmutató alakja a takaró fölött sem változik.
                case WM_SETCURSOR:
                    m.Result = 1;
                    return;

                // A helyét és a méretét mindig fizikai képpontban adjuk meg; a DPI-váltás ne méretezze át.
                case WM_DPICHANGED:
                    m.Result = IntPtr.Zero;
                    return;
            }

            base.WndProc(ref m);
        }
    }

    // A Dispose-a indítja a takaró áttűnését (a StartFade az ismételt hívást figyelmen kívül hagyja).
    private sealed class FadeOnDispose : IDisposable
    {
        private readonly ViewTransition _transition;
        private readonly int _generation;

        public FadeOnDispose(ViewTransition transition, int generation)
        {
            _transition = transition;
            _generation = generation;
        }

        public void Dispose() => _transition.StartFade(_generation);
    }

    private sealed class NoTransition : IDisposable
    {
        public static readonly NoTransition Instance = new();

        public void Dispose()
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref NativePoint pptDst,
        ref NativeSize psize,
        IntPtr hdcSrc,
        ref NativePoint pptSrc,
        int crKey,
        ref BlendFunction pblend,
        int dwFlags);

    [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindowOpacity(
        IntPtr hwnd,
        IntPtr hdcDst,
        IntPtr pptDst,
        IntPtr psize,
        IntPtr hdcSrc,
        IntPtr pptSrc,
        int crKey,
        ref BlendFunction pblend,
        int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, int flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
