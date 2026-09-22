namespace ReactionTimeMeasurementApp.Infrastructure;

// Az alkalmazás által írt fájlok (mérési eredmények, tekintetminták, beállítások, napló) helye.
// Az exe mappája (pl. Program Files alatt) nem mindig írható, a felhasználó Dokumentumok mappája
// viszont igen, és a kutató is könnyen megtalálja. A REACTIONTIME_DATA_DIR környezeti változóval más
// mappa is megadható (pl. hordozható telepítéshez vagy hálózati mappához).
internal sealed class AppPaths
{
    private const string AppFolderName = "ReakcioidoMeres";
    private const string DataDirectoryEnvironmentVariable = "REACTIONTIME_DATA_DIR";

    public AppPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public string MeasurementsDirectory => Path.Combine(RootDirectory, "measurements");

    public string GazeDirectory => Path.Combine(MeasurementsDirectory, "gaze");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string SettingsFilePath => Path.Combine(RootDirectory, "beallitasok.json");

    // Az 1-es formátumverziójú mérések helye (az exe mellett); a statisztika ezeket is beolvassa.
    public string LegacyMeasurementsDirectory => Path.Combine(AppContext.BaseDirectory, "measurements");

    public static AppPaths FromEnvironment()
    {
        var configured = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new AppPaths(Path.GetFullPath(configured));
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var baseDirectory = string.IsNullOrEmpty(documents)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : documents;

        return new AppPaths(Path.Combine(baseDirectory, AppFolderName));
    }
}
