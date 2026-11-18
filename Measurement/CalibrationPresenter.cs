using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.Measurement;

// A kalibrációs nézet két művelete.
public enum CalibrationAction
{
    ConfirmExisting,
    Recalibrate
}

// A kalibrációs nézet a presenter szemszögéből (passzív nézet). A tagokat a UI-szálról kell hívni.
public interface ICalibrationView
{
    event EventHandler<CalibrationAction>? ActionRequested;

    // A folyamatban lévő művelet (null: nincs): ennek idejére a gombok tiltottak, és a megnyomott gomb
    // felirata a folyamatot jelzi.
    void SetBusy(CalibrationAction? action);

    void ShowTrackerNotConnected();

    void ShowCalibrationMissing();

    void ShowGazeNotDetected(double validGazeRatio);

    void ShowEyeTrackerManagerMissing(Exception exception);

    void ShowCalibrationFailed(Exception exception);

    void ShowUnexpectedError(Exception exception);
}

// A kalibráció megerősítése (MVP presenter): a szemkövetőn lévő kalibráció ellenőrzése vagy új
// kalibráció a Tobii Pro Eye Tracker Managerrel. A kalibráció mindkét úton csak akkor számít
// megerősítettnek, ha a szemkövető utána ténylegesen érvényes tekintetadatot küld
// (ICalibrationService.CheckExistingCalibrationAsync).
public sealed class CalibrationPresenter
{
    private readonly ICalibrationView _view;
    private readonly ICalibrationService _calibrationService;
    private bool _isBusy;

    public CalibrationPresenter(ICalibrationView view, ICalibrationService calibrationService)
    {
        _view = view;
        _calibrationService = calibrationService;
        _view.ActionRequested += async (_, action) => await RunAsync(action);
    }

    // A kalibráció megerősítve: a szemkövető érvényes tekintetadatot küld.
    public event EventHandler? CalibrationConfirmed;

    // Az eseménykezelő async void-ként fut, ezért itt minden váratlan hibát elkapunk.
    public async Task RunAsync(CalibrationAction action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        _view.SetBusy(action);
        try
        {
            if (action == CalibrationAction.Recalibrate)
            {
                await RecalibrateAsync();
            }
            else
            {
                await ConfirmCalibrationAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Váratlan hiba a kalibráció közben.", ex);
            _view.ShowUnexpectedError(ex);
        }
        finally
        {
            _isBusy = false;
            _view.SetBusy(null);
        }
    }

    // Új kalibráció, majd ugyanaz az ellenőrzés, mint a meglévő kalibrációnál: a sikeresen lefutott
    // kalibrációs program még nem garantálja, hogy a szemkövető érvényes tekintetadatot küld.
    private async Task RecalibrateAsync()
    {
        await _calibrationService.InitializeAsync(forceRefresh: true);

        int calibrationResult;
        try
        {
            calibrationResult = await Task.Run(() => _calibrationService.RunUserCalibration());
        }
        catch (FileNotFoundException ex)
        {
            _view.ShowEyeTrackerManagerMissing(ex);
            return;
        }
        catch (InvalidOperationException ex)
        {
            // A kalibrációs program hibával, megszakítva vagy időtúllépéssel ért véget; a részletek a naplóban.
            _view.ShowCalibrationFailed(ex);
            return;
        }

        // Negatív érték: nincs csatlakoztatott szemkövető (lásd ICalibrationService.RunUserCalibration).
        if (calibrationResult < 0)
        {
            _view.ShowTrackerNotConnected();
            return;
        }

        await ConfirmCalibrationAsync();
    }

    private async Task ConfirmCalibrationAsync()
    {
        var (result, validGazeRatio) = await _calibrationService.CheckExistingCalibrationAsync();
        switch (result)
        {
            case CalibrationCheckResult.Ok:
                CalibrationConfirmed?.Invoke(this, EventArgs.Empty);
                break;
            case CalibrationCheckResult.TrackerNotFound:
                _view.ShowTrackerNotConnected();
                break;
            case CalibrationCheckResult.NoCalibrationData:
                _view.ShowCalibrationMissing();
                break;
            case CalibrationCheckResult.NoValidGaze:
                _view.ShowGazeNotDetected(validGazeRatio);
                break;
        }
    }
}
