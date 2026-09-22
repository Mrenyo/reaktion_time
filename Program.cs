using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Logger.Info("Alkalmazás indul.");
        Logger.Info($"Adatmappa (mérések, beállítások, napló): {DataLocations.RootDirectory}");
        DeveloperMode.Initialize(args);

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

        try
        {
            Application.Run(new MainMenu());
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
