using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A főablak: a nézetek közti navigáció, és a nézetek (valamint a mérési presenterek) létrehozása a
// composition roottól (Program) kapott függőségekkel.
public partial class MainMenu : Form
{
    private enum ViewKey
    {
        Menu,
        Calibration,
        NormalMeasure,
        PeripheralMeasure,
        Statistics
    }

    private const int GazeCircleDiameterPx = 60;
    private const int GazeRefreshIntervalMs = 16;
    private const float GazeSmoothingFactor = 0.6f;

    private readonly MenuView _menuView;
    private readonly Dictionary<ViewKey, Func<Control>> _viewFactories;
    private readonly Dictionary<ViewKey, Control> _viewCache = new();

    private readonly AppDependencies _dependencies;

    // A gaze-overlay (a szemkövető aktuális nézési pontját mutató kör) fejlesztői segédeszköz;
    // futásidőben a --dev kapcsolóval vagy a REACTIONTIME_DEV_MODE környezeti változóval
    // kapcsolható be, lásd AppOptions.
    private readonly bool _devMode;
    private readonly System.Windows.Forms.Timer _gazeTimer = new();
    private readonly System.Windows.Forms.Timer _viewPreloadTimer = new();
    private readonly Queue<ViewKey> _viewPreloadQueue = new();
    private readonly CircleIndicator _gazeCircle = new();
    private readonly TransitionCover _transitionCover = new();
    private readonly System.Windows.Forms.Timer _transitionCoverTimer = new();
    private Control? _activeView;
    private bool _preloadStarted;
    private PointF _smoothedGazePoint;
    private bool _hasSmoothedGazePoint;
    private MeasurementSessionResult? _latestSession;
    private bool _isFullScreen;
    private FormWindowState _windowStateBeforeFullScreen;
    private Rectangle _boundsBeforeFullScreen;

    internal MainMenu(AppDependencies dependencies)
    {
        _dependencies = dependencies;
        _devMode = dependencies.DeveloperMode;
        InitializeComponent();
        WindowState = FormWindowState.Maximized;

        _menuView = new MenuView();

        _menuView.OpenCalibrationRequested += (_, _) => NavigateTo(ViewKey.Calibration);
        _menuView.OpenMeasureRequested += (_, eventArgs) =>
            NavigateTo(eventArgs.Mode == MeasureMode.Normal ? ViewKey.NormalMeasure : ViewKey.PeripheralMeasure);
        _menuView.OpenStatisticsRequested += (_, _) => NavigateTo(ViewKey.Statistics);

        _viewFactories = new Dictionary<ViewKey, Func<Control>>
        {
            [ViewKey.Menu] = () => ConfigureView(_menuView),
            [ViewKey.Calibration] = CreateCalibrationView,
            [ViewKey.NormalMeasure] = () => CreateMeasurementView(new NormalMeasurementMode()),
            [ViewKey.PeripheralMeasure] = () => CreateMeasurementView(new PeripheralMeasurementMode()),
            [ViewKey.Statistics] = CreateStatisticsView
        };

        InitializeViews();
        if (_devMode)
        {
            InitializeGazeOverlay();
        }

        NavigateTo(ViewKey.Menu);
        InitializeViewPreload();
    }

    private void InitializeViews()
    {
        contentHostPanel.SuspendLayout();
        contentHostPanel.EnableDoubleBuffering();
        contentHostPanel.BackColor = Color.FromArgb(253, 250, 250);
        ConfigureTransitionCover();
        contentHostPanel.ResumeLayout(true);
    }

    private void ConfigureTransitionCover()
    {
        _transitionCover.Dock = DockStyle.Fill;
        _transitionCover.Margin = Padding.Empty;
        _transitionCover.Visible = false;
        _transitionCover.Enabled = false;
        contentHostPanel.Controls.Add(_transitionCover);

        _transitionCoverTimer.Interval = 90;
        _transitionCoverTimer.Tick += (_, _) => HideTransitionCover();
    }

    private void ShowTransitionCover()
    {
        if (_transitionCover.IsDisposed)
        {
            return;
        }

        _transitionCoverTimer.Stop();
        _transitionCover.Bounds = contentHostPanel.ClientRectangle;
        _transitionCover.Visible = true;
        _transitionCover.BringToFront();
        _transitionCover.Refresh();
    }

    private void ScheduleTransitionCoverHide()
    {
        _transitionCoverTimer.Stop();
        _transitionCoverTimer.Start();
    }

    private void HideTransitionCover()
    {
        _transitionCoverTimer.Stop();

        if (_transitionCover.IsDisposed)
        {
            return;
        }

        _transitionCover.Visible = false;
    }
    private void NavigateTo(ViewKey key)
    {
        ShowView(GetOrCreateView(key));
    }

    private Control GetOrCreateView(ViewKey key)
    {
        if (_viewCache.TryGetValue(key, out var cachedView))
        {
            return cachedView;
        }

        var factory = _viewFactories[key];
        var view = factory();
        AddViewToHost(view);
        _viewCache[key] = view;
        return view;
    }

    private void InitializeViewPreload()
    {
        foreach (var key in Enum.GetValues<ViewKey>())
        {
            if (key == ViewKey.Menu)
            {
                continue;
            }

            _viewPreloadQueue.Enqueue(key);
        }

        _viewPreloadTimer.Interval = 80;
        _viewPreloadTimer.Tick += (_, _) =>
        {
            if (_viewPreloadQueue.Count == 0)
            {
                _viewPreloadTimer.Stop();
                return;
            }

            var key = _viewPreloadQueue.Dequeue();
            _ = GetOrCreateView(key);
        };

    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_preloadStarted)
        {
            return;
        }

        _preloadStarted = true;

        if (_viewPreloadQueue.Count > 0)
        {
            _viewPreloadTimer.Start();
        }
    }

    private CalibrationView CreateCalibrationView()
    {
        var view = ConfigureView(new CalibrationView(_dependencies.CalibrationService));

        view.BackRequested += (_, _) => NavigateTo(ViewKey.Menu);
        view.CalibrationCompleted += (_, _) => _menuView.SetCalibrationCompleted(true);

        return view;
    }

    // Egy mérési nézet a hozzá tartozó presenterrel. A presenter a nézet eseményeire iratkozik fel,
    // így a nézet életciklusa tartja életben.
    private MeasurementView CreateMeasurementView(IMeasurementMode mode)
    {
        var view = ConfigureView(new MeasurementView());
        var presenter = new MeasurementPresenter(
            view,
            mode,
            _dependencies.GazeTracker,
            _dependencies.ResponseDeviceConnector,
            _dependencies.MeasurementRepository,
            _dependencies.SettingsProvider,
            _dependencies.DisplayTimingProvider,
            manageTrackingLifecycle: !_devMode);

        view.LeaveRequested += (_, _) => NavigateTo(ViewKey.Menu);
        presenter.SessionEnded += (_, eventArgs) => HandleMeasurementCompleted(eventArgs.Session);

        return view;
    }

    private StatisticsView CreateStatisticsView()
    {
        var view = ConfigureView(new StatisticsView(_dependencies.MeasurementRepository, showScanoutDebugColumn: _devMode));
        view.BackRequested += (_, _) => NavigateTo(ViewKey.Menu);

        if (_latestSession is not null)
        {
            view.SetLatestSessionResult(_latestSession);
        }

        return view;
    }

    private static TView ConfigureView<TView>(TView view)
        where TView : Control
    {
        view.Dock = DockStyle.Fill;
        view.Margin = Padding.Empty;
        view.Visible = false;
        view.EnableDoubleBuffering();
        return view;
    }

    private void HandleMeasurementCompleted(MeasurementSessionResult session)
    {
        _latestSession = session;
        if (_viewCache.TryGetValue(ViewKey.Statistics, out var view) && view is StatisticsView statisticsView)
        {
            statisticsView.SetLatestSessionResult(session);
        }
    }

    private void ShowView(Control view)
    {
        if (ReferenceEquals(_activeView, view))
        {
            return;
        }

        // Még a nézetváltás (és az elrendezés felfüggesztése) előtt, hogy a nézet már a végleges
        // méretet kapja.
        SetFullScreen(view is MeasurementView);
        ShowTransitionCover();
        using var redrawScope = UiRedrawScope.Suspend(contentHostPanel);
        contentHostPanel.SuspendLayout();
        SuspendLayout();

        var previousView = _activeView;

        if (view.Bounds != contentHostPanel.ClientRectangle)
        {
            view.Bounds = contentHostPanel.ClientRectangle;
        }

        if (!view.Visible)
        {
            view.Visible = true;
        }

        view.BringToFront();
        _activeView = view;

        if (previousView is not null && !ReferenceEquals(previousView, view))
        {
            previousView.Visible = false;
        }

        if (_devMode)
        {
            _gazeCircle.Visible = true;
            _gazeCircle.BringToFront();
        }

        _transitionCover.BringToFront();
        ResumeLayout(false);
        contentHostPanel.ResumeLayout(false);
        ScheduleTransitionCoverHide();
    }

    // A mérési nézetek keret nélküli teljes képernyőn futnak (címsor és tálca nélkül), hogy semmi ne
    // vonja el a résztvevő figyelmét. A fotodióda-jelző helye ehhez a teljes képernyős elrendezéshez
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
        contentHostPanel.Controls.Add(_gazeCircle);
        _gazeCircle.Size = new Size(GazeCircleDiameterPx, GazeCircleDiameterPx);
        _gazeCircle.ForeColor = Color.FromArgb(138, 138, 138);
        _gazeCircle.BackColor = Color.Transparent;
        _gazeCircle.Visible = false;
        _gazeCircle.BringToFront();

        _ = StartGazeOverlayTrackingAsync();

        _gazeTimer.Interval = GazeRefreshIntervalMs;
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

    private void UpdateGazeCirclePosition()
    {
        var screenPoint = _dependencies.GazeTracker.GetGazePoint(Screen.FromControl(this).Bounds);
        var clientPoint = contentHostPanel.PointToClient(screenPoint);

        if (!_hasSmoothedGazePoint)
        {
            _smoothedGazePoint = clientPoint;
            _hasSmoothedGazePoint = true;
            _gazeCircle.Visible = true;
        }
        else
        {
            _smoothedGazePoint = new PointF(
                _smoothedGazePoint.X + ((clientPoint.X - _smoothedGazePoint.X) * GazeSmoothingFactor),
                _smoothedGazePoint.Y + ((clientPoint.Y - _smoothedGazePoint.Y) * GazeSmoothingFactor));
        }

        var nextLocation = new Point(
            (int)MathF.Round(_smoothedGazePoint.X - (_gazeCircle.Width / 2f)),
            (int)MathF.Round(_smoothedGazePoint.Y - (_gazeCircle.Height / 2f)));

        if (_gazeCircle.Location != nextLocation)
        {
            _gazeCircle.Location = nextLocation;
        }
    }

    private void AddViewToHost(Control view)
    {
        contentHostPanel.SuspendLayout();
        view.Dock = DockStyle.Fill;
        view.Margin = Padding.Empty;
        contentHostPanel.Controls.Add(view);
        contentHostPanel.ResumeLayout(true);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _viewPreloadTimer.Stop();
        _gazeTimer.Stop();

        if (_devMode)
        {
            _dependencies.GazeTracker.StopTracking();
        }

        base.OnFormClosing(e);
    }
}

