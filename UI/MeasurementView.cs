using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A mérési nézet (MVP passzív nézet): a teljes nézet a mérőfelület (stage), a MainForm teljes
// képernyőn jeleníti meg. Nyugalmi állapotban (előtér) az alkalmazás rögzített hátterén egy kártya
// mutatja az instrukciót, a beállítások összefoglalóját, az előző mérés kimenetelét és a gombokat;
// mérés közben (jelenet) csak a beállított háttér, a fixációs kereszt, az inger és a fotodióda-jelző
// látszik. A felhasználói műveleteket eseményként továbbítja; a mérés menetét a MeasurementPresenter
// vezérli.
internal sealed partial class MeasurementView : UserControl, IMeasurementView
{
    // Az inger élsimított széle legfeljebb ennyivel lóghat túl a megadott téglalapján; az inger
    // megjelenésekor és eltűnésekor ennyivel nagyobb terület rajzolódik újra.
    private const int StimulusInvalidationMarginPx = 2;

    // Az előtér kártyájának méretei logikai (96 DPI-s) képpontban; az elrendezés a monitor DPI-jére
    // váltja át őket (LogicalToDeviceUnits).
    private const int LobbyCardMaxWidth = 760;
    private const int LobbyCardPaddingX = 40;
    private const int LobbyCardPaddingY = 36;
    private const int LobbyTitleGap = 14;
    private const int LobbyTextGap = 16;
    private const int LobbyButtonsGap = 26;
    private const int LobbyButtonSpacing = 16;

    // Mérés közben a mérőfelület fölött nem látszik az egérmutató.
    private static readonly Lazy<Cursor> HiddenCursor = new(CreateHiddenCursor);

    private MeasurementViewState _state = new(
        IsRunning: false,
        IsBusy: false,
        StartButtonText: "Mérés indítása",
        InstructionText: string.Empty,
        SettingsSummaryText: string.Empty,
        StatusText: string.Empty,
        Scene: null,
        PhotodiodeMarkerCenter: null);

    private RectangleF? _stimulusBounds;

    // Az inger ecsetje; csak akkor jön létre újra, ha az inger színe megváltozik.
    private SolidBrush? _stimulusBrush;

    // A háttérszálról a UI-szálra küldött, még le nem futott műveletek (lásd InvokeAsync).
    private readonly HashSet<TaskCompletionSource<object?>> _pendingInvocations = new();

    // A UI-szál, amelyen a nézet létrejött. Az InvokeRequired nem elég a szál eldöntéséhez: ha a nézetnek
    // (és a szülőláncának) még vagy már nincs ablakkezelője, akkor is hamis, ha a hívó háttérszálon fut.
    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;

    public MeasurementView()
    {
        // A mérőfelület (BufferedPanel) dupla pufferelt: a teljes jelenet (háttér, kereszt, inger,
        // fotodióda-jelző) egy memóriabeli pufferbe rajzolódik, és egyetlen másolással kerül a
        // képernyőre, így félkész állapot (pl. inger már igen, jelző még nem) nem kerülhet egy
        // képkockába. Az előtér kártyáját a rajzolás kihagyja (WS_CLIPCHILDREN), így az nem villog.
        InitializeComponent();
        DoubleBuffered = true;
        ResizeRedraw = true;

        lobbyCard.KeepRoundedCorners(UiTheme.CardCornerRadius);
        startMeasureButton.KeepRoundedCorners(UiTheme.ButtonCornerRadius);
        backButton.KeepRoundedCorners(UiTheme.ButtonCornerRadius);
        Disposed += (_, _) => _stimulusBrush?.Dispose();
        LayoutLobbyCard();
    }

    public event EventHandler? StartRequested;

    public event EventHandler? LeaveRequested;

    public event EventHandler? AbortRequested;

    public event EventHandler<ResponseClickEventArgs>? ResponseClicked;

    public event EventHandler? Closed;

    // Az Esc billentyűre (lásd MainForm): csak futó mérés közben kér megszakítást.
    public bool TryRequestAbort()
    {
        if (!_state.IsRunning || _state.IsBusy)
        {
            return false;
        }

        AbortRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // A művelet végrehajtása a UI-szálon. A megszűnt (vagy épp megszűnő) nézeten a művelet nem fut le, de
    // a visszaadott Task ilyenkor is befejeződik, így a háttérszálon várakozó hívó sosem akad el.
    public Task InvokeAsync(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return Task.CompletedTask;
        }

        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            action();
            return Task.CompletedTask;
        }

        // Háttérszálról ablakkezelő nélkül nincs hová küldeni a műveletet (a nézet megszűnőben van).
        if (!IsHandleCreated)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingInvocations)
        {
            _pendingInvocations.Add(completion);
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                lock (_pendingInvocations)
                {
                    _pendingInvocations.Remove(completion);
                }

                try
                {
                    action();
                    completion.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // A nézet (az ablakkezelője) a fenti ellenőrzés és a hívás között szűnt meg.
            lock (_pendingInvocations)
            {
                _pendingInvocations.Remove(completion);
            }

            completion.TrySetResult(null);
        }

        return completion.Task;
    }

    public StageInfo GetStageInfo()
    {
        var screen = Screen.FromControl(stagePanel);
        return new StageInfo(stagePanel.ClientSize, stagePanel.PointToScreen(Point.Empty), screen.Bounds, screen.DeviceName);
    }

    public void ApplyState(MeasurementViewState state)
    {
        _state = state;
        if (IsDisposed)
        {
            return;
        }

        lobbyCard.Visible = !state.IsRunning;
        startMeasureButton.Enabled = !state.IsRunning && !state.IsBusy;
        startMeasureButton.Text = state.StartButtonText;
        backButton.Enabled = !state.IsBusy;
        SetLabelText(instructionLabel, state.InstructionText);
        SetLabelText(statusLabel, state.StatusText);
        SetLabelText(summaryLabel, state.SettingsSummaryText);
        stagePanel.Cursor = state.IsRunning && state.Scene?.ShowCursor != true ? HiddenCursor.Value : Cursors.Default;
        LayoutLobbyCard();
        stagePanel.Invalidate();
    }

    // Az ingerrel együtt csak az inger (régi és új) helye és a fotodióda-jelző változhat, ezért csak ez
    // a két terület rajzolódik újra, nem a teljes mérőfelület; így az inger megjelenítése nagy
    // felbontáson is rövid. (A rajzolás ilyenkor is a teljes jelenetet rajzolja, csak a rendszer az
    // érvénytelenített területre vágja.)
    public void ShowStimulus(RectangleF? stimulusBounds, bool paintImmediately)
    {
        var previousBounds = _stimulusBounds;
        _stimulusBounds = stimulusBounds;
        if (IsDisposed)
        {
            return;
        }

        InvalidateStimulusArea(previousBounds);
        InvalidateStimulusArea(stimulusBounds);
        var photodiodeBounds = GetPhotodiodeBounds(stagePanel.ClientSize);
        if (!photodiodeBounds.IsEmpty)
        {
            stagePanel.Invalidate(photodiodeBounds);
        }

        if (paintImmediately)
        {
            // Az Update() azonnal lefuttatja a rajzolást (nem várja meg, hogy a WM_PAINT sorra
            // kerüljön az üzenetsorban).
            stagePanel.Update();
        }
    }

    private void InvalidateStimulusArea(RectangleF? stimulusBounds)
    {
        if (stimulusBounds is not { } bounds)
        {
            return;
        }

        bounds.Inflate(StimulusInvalidationMarginPx, StimulusInvalidationMarginPx);
        stagePanel.Invalidate(Rectangle.Ceiling(bounds));
    }

    public void ShowTrackerNotConnected() => ErrorPopup.ShowTobiiNotConnected(this);

    public void ShowSettingsError(string settingsPath, string details) => ErrorPopup.ShowSettingsError(this, settingsPath, details);

    public void ShowSetupError(string details) => ErrorPopup.ShowMeasurementSetupError(this, details);

    public void ShowEspNotConnected(string portName, Exception exception) => ErrorPopup.ShowEspNotConnected(this, portName, exception);

    public void ShowEspCommunicationError(string portName, int baudRate, Exception? exception) =>
        ErrorPopup.ShowEspCommunicationError(this, portName, baudRate, exception);

    public void ShowEspConnectionLost(string portName, string trialLabel, Exception exception) =>
        ErrorPopup.ShowEspConnectionLost(this, portName, trialLabel, exception);

    public void ShowTrackerConnectionLost(string trialLabel, bool measurementAborted) =>
        ErrorPopup.ShowTrackerConnectionLost(this, trialLabel, measurementAborted);

    public void ShowSaveFailed(string path, Exception exception) => ErrorPopup.ShowSaveFailed(this, path, exception);

    public void ShowUnexpectedError(Exception exception) => ErrorPopup.ShowGeneral(this, exception);

    // Az ablakkezelő megszűnése a nézet végét jelenti - kivéve, ha a rendszer csak újra létrehozza
    // (RecreatingHandle), olyankor a nézet tovább él. A még le nem futott UI-szálas műveletek már nem
    // futnak le; a rájuk váró hívók Taskja befejeződik.
    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (!RecreatingHandle)
        {
            Closed?.Invoke(this, EventArgs.Empty);

            TaskCompletionSource<object?>[] pending;
            lock (_pendingInvocations)
            {
                pending = [.. _pendingInvocations];
                _pendingInvocations.Clear();
            }

            foreach (var completion in pending)
            {
                completion.TrySetResult(null);
            }
        }

        base.OnHandleDestroyed(e);
    }

    // Az üres szövegű címke nem látszik (pl. az állapotsor, amíg nem volt mérés), így helyet sem foglal.
    private static void SetLabelText(Label label, string text)
    {
        label.Text = text;
        label.Visible = text.Length > 0;
    }

    // Az előtér kártyája: a szélessége legfeljebb LobbyCardMaxWidth, és két oldalon annyival keskenyebb
    // a mérőfelületnél, amennyi egy sarokba tett fotodióda-jelzőnek, a margójának, a védősávjának és a
    // biztonsági margónak kell. A címkék a tartalomszélességnél tördelnek (AutoSize + MaximumSize), a
    // kártya magassága a tartalomhoz igazodik. A kártya középre kerül; ha így a jelző védősávjához
    // SafeMarginPx-nél közelebb lenne, a jelző fölé, alá, bal vagy jobb oldalára csúszik (a középhez
    // legközelebbi elférő helyre), hogy a jelző a szenzor felhelyezésekor is látsszon.
    private void LayoutLobbyCard()
    {
        var stageSize = stagePanel.ClientSize;
        if (_state.IsRunning || stageSize.Width <= 0 || stageSize.Height <= 0)
        {
            return;
        }

        var safeMargin = (int)Math.Ceiling(StageLayout.SafeMarginPx);
        var markerReserve = PhotodiodeMarker.SizePx + PhotodiodeMarker.MarginPx + PhotodiodeMarker.GuardPx + safeMargin;
        var cardWidth = Math.Max(1, Math.Min(LogicalToDeviceUnits(LobbyCardMaxWidth), stageSize.Width - (2 * markerReserve)));
        var contentWidth = Math.Max(1, cardWidth - (2 * LogicalToDeviceUnits(LobbyCardPaddingX)));

        lobbyCard.SuspendLayout();

        var top = LogicalToDeviceUnits(LobbyCardPaddingY);
        top = PlaceLabel(titleLabel, top, cardWidth, contentWidth);
        var gap = LogicalToDeviceUnits(LobbyTitleGap);
        foreach (var label in new[] { instructionLabel, statusLabel, summaryLabel })
        {
            // Az üres címke rejtett (SetLabelText); a Visible itt nem használható, mert a nézet
            // előtöltéskor még rejtett, és ilyenkor minden gyermeke rejtettnek látszik.
            if (label.Text.Length == 0)
            {
                continue;
            }

            top = PlaceLabel(label, top + gap, cardWidth, contentWidth);
            gap = LogicalToDeviceUnits(LobbyTextGap);
        }

        top += LogicalToDeviceUnits(LobbyButtonsGap);
        var startSize = startMeasureButton.Size;
        var backSize = backButton.Size;
        var spacing = LogicalToDeviceUnits(LobbyButtonSpacing);
        if (startSize.Width + spacing + backSize.Width <= contentWidth)
        {
            var rowHeight = Math.Max(startSize.Height, backSize.Height);
            var left = (cardWidth - (startSize.Width + spacing + backSize.Width)) / 2;
            startMeasureButton.Location = new Point(left, top + ((rowHeight - startSize.Height) / 2));
            backButton.Location = new Point(left + startSize.Width + spacing, top + ((rowHeight - backSize.Height) / 2));
            top += rowHeight;
        }
        else
        {
            // Keskeny mérőfelületen a gombok egymás alá kerülnek.
            startMeasureButton.Location = new Point((cardWidth - startSize.Width) / 2, top);
            top += startSize.Height + spacing;
            backButton.Location = new Point((cardWidth - backSize.Width) / 2, top);
            top += backSize.Height;
        }

        var cardHeight = top + LogicalToDeviceUnits(LobbyCardPaddingY);
        var cardSize = new Size(cardWidth, cardHeight);
        lobbyCard.Size = cardSize;
        lobbyCard.ResumeLayout(false);
        lobbyCard.Location = PlaceLobbyCard(stageSize, cardSize, safeMargin);
    }

    private Point PlaceLobbyCard(Size stageSize, Size cardSize, int safeMargin)
    {
        var centered = new Point((stageSize.Width - cardSize.Width) / 2, Math.Max(0, (stageSize.Height - cardSize.Height) / 2));
        var guardBounds = PhotodiodeMarker.GetGuardBounds(GetPhotodiodeBounds(stageSize));
        if (guardBounds.IsEmpty)
        {
            return centered;
        }

        var keepOut = Rectangle.Inflate(guardBounds, safeMargin, safeMargin);
        if (!keepOut.IntersectsWith(new Rectangle(centered, cardSize)))
        {
            return centered;
        }

        var candidates = new[]
        {
            new Point(centered.X, keepOut.Top - cardSize.Height),
            new Point(centered.X, keepOut.Bottom),
            new Point(keepOut.Left - cardSize.Width, centered.Y),
            new Point(keepOut.Right, centered.Y)
        };
        var stage = new Rectangle(Point.Empty, stageSize);
        var best = candidates
            .Where(candidate => stage.Contains(new Rectangle(candidate, cardSize)))
            .OrderBy(candidate => Math.Abs(candidate.X - centered.X) + Math.Abs(candidate.Y - centered.Y))
            .Cast<Point?>()
            .FirstOrDefault();

        // Ha sehol nem fér el a jelző mellett, a kártya felülre kerül (a jelző fölé is lóghat).
        return best ?? new Point(centered.X, Math.Max(0, Math.Min(centered.Y, keepOut.Top - cardSize.Height)));
    }

    private Rectangle GetPhotodiodeBounds(Size stageSize) => PhotodiodeMarker.GetBounds(stageSize, _state.PhotodiodeMarkerCenter);

    // A címke a tartalomszélességnél tördel, vízszintesen középre kerül; a visszaadott érték az alja.
    private static int PlaceLabel(Label label, int top, int cardWidth, int contentWidth)
    {
        label.MaximumSize = new Size(contentWidth, 0);
        var size = label.GetPreferredSize(new Size(contentWidth, 0));
        label.SetBounds((cardWidth - size.Width) / 2, top, size.Width, size.Height);
        return top + size.Height;
    }

    private void stagePanel_Resize(object? sender, EventArgs e) => LayoutLobbyCard();

    // A válasz a bal gomb lenyomása (nem a felengedése), így a kattintás ideje a lehető legkorábbi. Csak a
    // bal gomb számít: az ESP is csak ennek a kapcsolóját figyeli, így a PC és az ESP ugyanazt méri.
    private void stagePanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_state.IsRunning && e.Button == MouseButtons.Left)
        {
            ResponseClicked?.Invoke(this, new ResponseClickEventArgs(e.Location));
        }
    }

    private void stagePanel_Paint(object? sender, PaintEventArgs e)
    {
        var stageSize = stagePanel.ClientSize;
        var photodiodeBounds = GetPhotodiodeBounds(stageSize);
        if (_state.IsRunning && _state.Scene is { } scene)
        {
            PaintScene(e.Graphics, scene, photodiodeBounds);
        }
        else
        {
            PaintLobby(e.Graphics, stageSize, photodiodeBounds);
        }
    }

    // Az előtér: az alkalmazás rögzített háttere (nem a beállított mérési háttér), a fekete védősáv a
    // fekete jelzőnégyzettel, és a négyzet körül egy 1 képpontos szürke keret, amely a fizikai szenzor
    // felhelyezését segíti. A keret csak itt látszik, a mérési jelenetben nem.
    private static void PaintLobby(Graphics graphics, Size stageSize, Rectangle bounds)
    {
        ViewBackgroundStyler.DrawBackground(graphics, stageSize);
        if (bounds.IsEmpty)
        {
            return;
        }

        StagePainter.DrawPhotodiodeMarker(graphics, PhotodiodeMarker.GetGuardBounds(bounds), bounds, lit: false);
    }

    // A mérési jelenet: a beállított háttér, a fekete védősáv, a fixációs kereszt, az inger és a
    // jelzőnégyzet (keret nélkül) - semmi más. Az inger és a jelző ugyanabban a rajzolásban készül, és
    // a jelző színét ugyanaz az állapot (_stimulusBounds) vezérli, mint az inger kirajzolását, így a
    // kettő nem kerülhet külön képkockába.
    private void PaintScene(Graphics graphics, MeasurementScene scene, Rectangle photodiodeBounds)
    {
        graphics.Clear(scene.BackgroundColor);

        var guardBounds = PhotodiodeMarker.GetGuardBounds(photodiodeBounds);
        if (!guardBounds.IsEmpty)
        {
            StagePainter.FillPhotodiodeGuard(graphics, guardBounds);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        StagePainter.DrawFixationCross(graphics, scene.FixationPoint);

        if (_stimulusBounds is { } stimulus)
        {
            graphics.FillEllipse(GetStimulusBrush(scene.StimulusColor), stimulus);
        }

        if (!photodiodeBounds.IsEmpty)
        {
            StagePainter.FillPhotodiodeSquare(graphics, photodiodeBounds, lit: _stimulusBounds.HasValue);
        }
    }

    private SolidBrush GetStimulusBrush(Color color)
    {
        if (_stimulusBrush is null || _stimulusBrush.Color != color)
        {
            _stimulusBrush?.Dispose();
            _stimulusBrush = new SolidBrush(color);
        }

        return _stimulusBrush;
    }

    private void startMeasureButton_Click(object? sender, EventArgs e) => StartRequested?.Invoke(this, EventArgs.Empty);

    private void backButton_Click(object? sender, EventArgs e) => LeaveRequested?.Invoke(this, EventArgs.Empty);

    // Teljesen átlátszó egérmutató. A kezelőből (HICON) létrehozott Cursor nem lesz a kezelő
    // tulajdonosa, ezért az alkalmazás kilépésekor itt szabadul fel.
    private static Cursor CreateHiddenCursor()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        var iconHandle = bitmap.GetHicon();
        Application.ApplicationExit += (_, _) => DestroyIcon(iconHandle);
        return new Cursor(iconHandle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);
}
