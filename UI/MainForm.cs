using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A főablak: a nézetek közti navigáció, és a nézetek (valamint a mérési presenter) létrehozása a
// composition roottól (Program) kapott függőségekkel.
internal sealed partial class MainForm : Form
{
    private enum ViewKey
    {
        Menu,
        Calibration,
        Measure,
        Statistics
    }

    private const int GazeCircleDiameterPx = 60;
    private const int GazeRefreshIntervalMs = 16;
    private const float GazeSmoothingFactor = 0.6f;

    // Az áttűnés hossza nézetváltáskor; a teljes képernyőre (és onnan vissza) váltáskor és induláskor,
    // amikor az egész képernyő változik, valamivel hosszabb.
    private static readonly TimeSpan ViewFadeDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ScreenFadeDuration = TimeSpan.FromMilliseconds(260);

    private readonly MenuView _menuView;
    private readonly Dictionary<ViewKey, Control> _viewCache = new();

    private readonly AppDependencies _dependencies;

    // A gaze-overlay (a szemkövető aktuális nézési pontját mutató kör) fejlesztői segédeszköz;
    // futásidőben a --dev kapcsolóval vagy a REACTIONTIME_DEV_MODE környezeti változóval
    // kapcsolható be, lásd AppOptions.
    private readonly bool _devMode;
    // Az időzítő és a kör csak fejlesztői módban jön létre (InitializeGazeOverlay).
    private System.Windows.Forms.Timer? _gazeTimer;
    private CircleIndicator? _gazeCircle;
    // A menün kívüli nézetek az indulás után, a háttérben jönnek létre, hogy az első megnyitásuk is gyors legyen.
    private readonly System.Windows.Forms.Timer _viewPreloadTimer = new() { Interval = 80 };
    private readonly Queue<ViewKey> _viewPreloadQueue = new([ViewKey.Calibration, ViewKey.Measure, ViewKey.Statistics]);
    private readonly ViewTransition _viewTransition;
    private Control? _activeView;
    private MeasurementPresenter? _measurementPresenter;
    private StatisticsPresenter? _statisticsPresenter;

    // Volt-e ebben a programfutásban megerősített kalibráció (a kalibrációs nézetben kiválasztott vagy
    // elvégzett). Csak a tekintet-ellenőrzéses méréshez kötelező.
    private bool _isCalibrated;
    private bool _preloadStarted;
    private PointF _smoothedGazePoint;
    private bool _hasSmoothedGazePoint;
    private MeasurementSessionResult? _latestSession;
    private bool _isFullScreen;
    private FormWindowState _windowStateBeforeFullScreen;
    private Rectangle _boundsBeforeFullScreen;

    internal MainForm(AppDependencies dependencies)
    {
        _dependencies = dependencies;
        _devMode = dependencies.DeveloperMode;
        _viewTransition = new ViewTransition(this);
        InitializeComponent();
        WindowState = FormWindowState.Maximized;

        _menuView = new MenuView();

        _menuView.OpenCalibrationRequested += (_, _) => NavigateTo(ViewKey.Calibration);
        _menuView.OpenMeasureRequested += (_, _) => OpenMeasure();
        _menuView.OpenSettingsRequested += (_, _) => OpenSettingsDialog();
        _menuView.OpenStatisticsRequested += (_, _) => NavigateTo(ViewKey.Statistics);

        contentHostPanel.BackColor = UiTheme.CardBackground;
        if (_devMode)
        {
            InitializeGazeOverlay();
        }

        NavigateTo(ViewKey.Menu);
        _viewPreloadTimer.Tick += (_, _) => PreloadNextView();
    }

    private void NavigateTo(ViewKey key) => ShowView(GetOrCreateView(key));

    // A Mérés gomb. A presenter újratölti a beállításokat (így az előtér a legfrissebbeket mutatja), és
    // megmondja, kell-e hozzájuk kalibráció (csak a tekintet-ellenőrzéses méréshez kell). Hibás
    // beállításoknál is a mérési nézet nyílik meg: az előtér megmutatja a hibát, és a mérés nem indul el.
    private void OpenMeasure()
    {
        var view = GetOrCreateView(ViewKey.Measure);
        if (_measurementPresenter is { } presenter)
        {
            presenter.ReloadSettings();
            if (presenter.RequiresCalibration && !_isCalibrated)
            {
                ErrorPopup.ShowCalibrationRequired(this);
                return;
            }

            presenter.CalibrationConfirmed = _isCalibrated;
        }

        ShowView(view);
    }

    // A Testreszabás gomb: a mérési beállítások párbeszédablaka. A mérés ennek az ablaknak a képernyőjén,
    // teljes képernyőn fut, ezért az ellenőrzés, az előnézet és a fixációs pont kijelölése ehhez a
    // képernyőmérethez igazodik. Mentés után a mérési nézet előtere is az új beállításokat mutatja.
    private void OpenSettingsDialog()
    {
        // A presenter betölti a beállításokat, kitölti velük az ablakot, és ő is ment; a nézet eseményeire
        // iratkozik fel, így az ablak életciklusa tartja életben.
        var screen = Screen.FromControl(this);
        using var dialog = new MeasurementSettingsDialog(
            screen.Bounds,
            _dependencies.ResponseDeviceConnector.GetAvailablePortNames(),
            _dependencies.DisplayTimingProvider.ReadPhysicalSize(screen.DeviceName));
        _ = new SettingsEditorPresenter(dialog, _dependencies.SettingsProvider);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _measurementPresenter?.ReloadSettings();
        }
    }

    private Control GetOrCreateView(ViewKey key)
    {
        if (_viewCache.TryGetValue(key, out var cachedView))
        {
            return cachedView;
        }

        // A nézetet a létrehozója már beállította (ConfigureView: kitöltés, rejtve); itt csak a felületre kerül.
        Control view = key switch
        {
            ViewKey.Calibration => CreateCalibrationView(),
            ViewKey.Measure => CreateMeasurementView(),
            ViewKey.Statistics => CreateStatisticsView(),
            _ => ConfigureView(_menuView)
        };
        contentHostPanel.Controls.Add(view);
        _viewCache[key] = view;
        return view;
    }

    private void PreloadNextView()
    {
        // Áttűnés közben nem: egy nézet létrehozása a UI-szálon megakasztaná az animációt.
        if (_viewTransition.IsActive)
        {
            return;
        }

        if (_viewPreloadQueue.TryDequeue(out var key))
        {
            _ = GetOrCreateView(key);
        }
        else
        {
            _viewPreloadTimer.Stop();
        }
    }

    // Indításkor az ablak láthatatlan, amíg minden eleme ki nem rajzolódott (lásd OnShown).
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _viewTransition.HideOwnerUntilRevealed();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_preloadStarted)
        {
            return;
        }

        _preloadStarted = true;

        // Az ablak teljesen kirajzolva, áttűnéssel jelenik meg (nem látszik, ahogy az elemei felépülnek).
        _viewTransition.RevealOwner(ScreenFadeDuration);

        _viewPreloadTimer.Start();
    }

    private CalibrationView CreateCalibrationView()
    {
        var view = ConfigureView(new CalibrationView());
        var presenter = new CalibrationPresenter(view, _dependencies.CalibrationService);

        view.BackRequested += (_, _) => NavigateTo(ViewKey.Menu);
        presenter.CalibrationConfirmed += (_, _) =>
        {
            _isCalibrated = true;
            _menuView.SetCalibrationCompleted(true);
            NavigateTo(ViewKey.Menu);
        };

        return view;
    }

    // A mérési nézet a presenterével. A presenter a nézet eseményeire iratkozik fel, így a nézet
    // életciklusa tartja életben. A presenter itt még nem kérdezi le a nézet helyzetét: a nézet csak
    // ezután kerül a felületre.
    private MeasurementView CreateMeasurementView()
    {
        var view = ConfigureView(new MeasurementView());
        var presenter = new MeasurementPresenter(
            view,
            _dependencies.GazeTracker,
            _dependencies.ResponseDeviceConnector,
            _dependencies.MeasurementRepository,
            _dependencies.SettingsProvider,
            _dependencies.DisplayTimingProvider,
            manageTrackingLifecycle: !_devMode);

        view.LeaveRequested += (_, _) => NavigateTo(ViewKey.Menu);
        presenter.SessionEnded += (_, eventArgs) => HandleMeasurementCompleted(eventArgs.Session);
        _measurementPresenter = presenter;

        return view;
    }

    private StatisticsView CreateStatisticsView()
    {
        var view = ConfigureView(new StatisticsView(showScanoutDebugColumn: _devMode));
        var presenter = new StatisticsPresenter(view, _dependencies.MeasurementRepository);
        view.BackRequested += (_, _) => NavigateTo(ViewKey.Menu);

        // Ha a nézet létrehozása előtt már volt mérés ebben a programfutásban, az a memóriából is bekerül
        // (a lemezről beolvasott előzményben a presenter nem veszi fel másodszor).
        if (_latestSession is not null)
        {
            presenter.AddSession(_latestSession);
        }

        _statisticsPresenter = presenter;
        _ = presenter.LoadAsync();
        return view;
    }

    private static TView ConfigureView<TView>(TView view)
        where TView : Control
    {
        view.Dock = DockStyle.Fill;
        view.Margin = Padding.Empty;
        view.Visible = false;

        // A nézetek maguk kettősen puffereltek (a konstruktorukban állítják be); itt a belső paneljeik is
        // azok lesznek. A mérési nézet saját, kettősen pufferelt mérőfelületre rajzol (az inger és a
        // fotodióda-jelző kirajzolása ehhez igazodik), ezért annak a belső vezérlőihez nem nyúlunk.
        if (view is not MeasurementView)
        {
            view.EnableDoubleBufferingForPanels();
        }

        return view;
    }

    private void HandleMeasurementCompleted(MeasurementSessionResult session)
    {
        _latestSession = session;
        _statisticsPresenter?.AddSession(session);
    }

    // Nézetváltás áttűnéssel: a takaró a jelenlegi képet mutatja, amíg alatta az új nézet láthatatlanul
    // elkészül (a redrawScope végén, egyetlen szinkron újrarajzolással), majd a régi kép elhalványul (a
    // transition végén). Így nem látszik, ahogy a nézet elemei egyenként kirajzolódnak. A mérési nézet
    // teljes képernyős, ezért oda és onnan vissza az egész képernyő változik (keret, tálca); a többi
    // váltás csak a tartalmat érinti.
    private void ShowView(Control view)
    {
        if (ReferenceEquals(_activeView, view))
        {
            return;
        }

        var fullScreen = view is MeasurementView;
        var screenChanges = fullScreen != _isFullScreen;
        var transitionArea = Rectangle.Empty;
        if (IsHandleCreated && Visible)
        {
            transitionArea = screenChanges
                ? Screen.FromControl(this).Bounds
                : contentHostPanel.RectangleToScreen(contentHostPanel.ClientRectangle);
        }

        using var transition = _viewTransition.Cover(
            transitionArea,
            aboveTaskbar: screenChanges,
            screenChanges ? ScreenFadeDuration : ViewFadeDuration);
        using var redrawScope = UiRedrawScope.Suspend(contentHostPanel);

        // A teljes képernyőre váltás (és vissza) közben mindig a mérési nézet látszik: kilépéskor még,
        // belépéskor már az a látható nézet. Így a többi nézetet nem kell a köztes ablakméretekre is
        // átrendezni; a mérési nézet a végleges méretét a teljes képernyőre váltáskor kapja meg.
        if (!fullScreen)
        {
            SetFullScreen(false);
        }

        contentHostPanel.SuspendLayout();
        SuspendLayout();

        var previousView = _activeView;
        view.Bounds = contentHostPanel.ClientRectangle;
        view.Visible = true;
        view.BringToFront();
        _activeView = view;
        if (previousView is not null)
        {
            previousView.Visible = false;
        }

        // A fejlesztői tekintetkör a mérési nézeten nem látszik (a résztvevő ott csak a mérési jelenetet
        // láthatja); a követés ettől tovább fut.
        if (_gazeCircle is { } gazeCircle)
        {
            gazeCircle.Visible = _hasSmoothedGazePoint && view is not MeasurementView;
            gazeCircle.BringToFront();
        }

        ResumeLayout(false);
        contentHostPanel.ResumeLayout(false);

        if (fullScreen)
        {
            SetFullScreen(true);
        }
    }

    // A mérési nézet keret nélküli teljes képernyőn fut (címsor és tálca nélkül), hogy semmi ne vonja
    // el a résztvevő figyelmét. A fotodióda-jelző helye ehhez a teljes képernyős elrendezéshez
    // igazodik, ezért a mérési nézet már megnyitáskor (nem csak a mérés indításakor) teljes képernyős.
    private void SetFullScreen(bool fullScreen)
    {
        if (_isFullScreen == fullScreen)
        {
            return;
        }

        _isFullScreen = fullScreen;
        if (fullScreen)
        {
            _windowStateBeforeFullScreen = WindowState;
            _boundsBeforeFullScreen = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var screen = Screen.FromControl(this);
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = screen.Bounds;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _boundsBeforeFullScreen;
            WindowState = _windowStateBeforeFullScreen == FormWindowState.Minimized
                ? FormWindowState.Maximized
                : _windowStateBeforeFullScreen;
        }
    }

    // Esc: a futó mérés megszakítása (az eddigi próbák mentve maradnak).
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _activeView is MeasurementView measurementView && measurementView.TryRequestAbort())
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void InitializeGazeOverlay()
    {
        _gazeCircle = new CircleIndicator
        {
            Size = new Size(GazeCircleDiameterPx, GazeCircleDiameterPx),
            Visible = false
        };
        contentHostPanel.Controls.Add(_gazeCircle);
        _gazeCircle.BringToFront();

        _ = StartGazeOverlayTrackingAsync();

        _gazeTimer = new System.Windows.Forms.Timer { Interval = GazeRefreshIntervalMs };
        _gazeTimer.Tick += (_, _) => UpdateGazeCirclePosition();
        _gazeTimer.Start();
    }

    // Az eszközkeresés aszinkron, így nem késlelteti az ablak megjelenését.
    private async Task StartGazeOverlayTrackingAsync()
    {
        try
        {
            await _dependencies.GazeTracker.InitializeAsync();
            _dependencies.GazeTracker.StartTracking();
        }
        catch (Exception ex)
        {
            Logger.Error("A fejlesztői tekintet-overlay követésének indítása sikertelen.", ex);
        }
    }

    // A mérési nézeten a kör rejtve marad, és a helyét sem frissítjük, hogy mérés közben ne fusson
    // fölösleges munka a UI-szálon.
    private void UpdateGazeCirclePosition()
    {
        if (_gazeCircle is not { } gazeCircle || _activeView is MeasurementView)
        {
            return;
        }

        var screenPoint = _dependencies.GazeTracker.GetGazePoint(Screen.FromControl(this).Bounds);
        var clientPoint = contentHostPanel.PointToClient(screenPoint);

        if (!_hasSmoothedGazePoint)
        {
            _smoothedGazePoint = clientPoint;
            _hasSmoothedGazePoint = true;
            gazeCircle.Visible = true;
        }
        else
        {
            _smoothedGazePoint = new PointF(
                _smoothedGazePoint.X + ((clientPoint.X - _smoothedGazePoint.X) * GazeSmoothingFactor),
                _smoothedGazePoint.Y + ((clientPoint.Y - _smoothedGazePoint.Y) * GazeSmoothingFactor));
        }

        var nextLocation = new Point(
            (int)MathF.Round(_smoothedGazePoint.X - (gazeCircle.Width / 2f)),
            (int)MathF.Round(_smoothedGazePoint.Y - (gazeCircle.Height / 2f)));

        if (gazeCircle.Location != nextLocation)
        {
            gazeCircle.Location = nextLocation;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _viewPreloadTimer.Stop();
        _gazeTimer?.Stop();

        if (_devMode)
        {
            _dependencies.GazeTracker.StopTracking();
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _viewTransition.Dispose();
        _viewPreloadTimer.Dispose();
        _gazeTimer?.Dispose();
        base.OnFormClosed(e);
    }
}

