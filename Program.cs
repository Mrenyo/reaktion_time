namespace ReactionTimeMeasurementApp;

static class Program
{
    [STAThread]
    static void Main()
    {
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
    }
}
