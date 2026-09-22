using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Infrastructure;
using ReactionTimeMeasurementApp.Infrastructure.Display;
using ReactionTimeMeasurementApp.Infrastructure.Esp;
using ReactionTimeMeasurementApp.Infrastructure.Persistence;
using ReactionTimeMeasurementApp.Infrastructure.Tobii;
using ReactionTimeMeasurementApp.Measurement;
using ReactionTimeMeasurementApp.UI;

namespace ReactionTimeMeasurementApp;

// A felület által használt szolgáltatások. Mindegyik interfész; a megvalósításokat a composition
// root (Program.Main) választja ki és köti össze.
internal sealed record AppDependencies(
    bool DeveloperMode,
    IGazeTracker GazeTracker,
    ICalibrationService CalibrationService,
    IResponseDeviceConnector ResponseDeviceConnector,
    IMeasurementRepository MeasurementRepository,
    IExperimentSettingsProvider SettingsProvider,
    IDisplayTimingProvider DisplayTimingProvider);

static class Program
{
    // Composition root: itt jön létre és kapcsolódik össze minden szolgáltatás (konstruktoros
    // függőséginjektálás, konténer nélkül). Máshol a kód csak interfészeket lát.
    [STAThread]
    static void Main(string[] args)
    {
        var paths = AppPaths.FromEnvironment();
        Logger.Initialize(paths.LogsDirectory);
        Logger.Info("Alkalmazás indul.");
        Logger.Info($"Adatmappa (mérések, beállítások, napló): {paths.RootDirectory}");

        var options = AppOptions.FromCommandLine(args);
        Logger.Info($"Fejlesztői mód: {(options.DeveloperMode ? "bekapcsolva" : "kikapcsolva")}.");

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
            ErrorPopup.ShowGeneral(null, eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            ErrorPopup.ShowGeneral(null, eventArgs.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            ErrorPopup.ShowGeneral(null, eventArgs.Exception);
            eventArgs.SetObserved();
        };

        // A Tobii-szolgáltatás a teljes futás alatt él; a using a végén leállítja a követést,
        // felszabadítja az eszközt és lezárja a Tobii SDK-t.
        using var tobiiService = new TobiiService();
        var dependencies = new AppDependencies(
            options.DeveloperMode,
            GazeTracker: tobiiService,
            CalibrationService: tobiiService,
            ResponseDeviceConnector: new EspResponseDeviceConnector(),
            MeasurementRepository: new MeasurementRepository(paths),
            SettingsProvider: new ExperimentSettingsStore(paths.SettingsFilePath),
            DisplayTimingProvider: new DisplayTimingReader());

        try
        {
            Application.Run(new MainMenu(dependencies));
        }
        catch (Exception ex)
        {
            ErrorPopup.ShowGeneral(null, ex);
        }
        finally
        {
            Logger.Info("Alkalmazás bezárva.");
        }
    }
}
