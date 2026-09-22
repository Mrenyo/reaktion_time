namespace ReactionTimeMeasurementApp.Classes;

// Az alkalmazás által írt fájlok (mérési eredmények, tekintetminták, beállítások, napló) helye.
// Az exe mappája (pl. Program Files alatt) nem mindig írható, a felhasználó Dokumentumok mappája
// viszont igen, és a kutató is könnyen megtalálja. A REACTIONTIME_DATA_DIR környezeti változóval más mappa is megadható
// (pl. hordozható telepítéshez vagy hálózati mappához).
internal static class DataLocations
{
    private const string AppFolderName = "ReakcioidoMeres";
    private const string DataDirectoryEnvironmentVariable = "REACTIONTIME_DATA_DIR";

    internal static string RootDirectory { get; } = BuildRootDirectory();

    internal static string MeasurementsDirectory => Path.Combine(RootDirectory, "measurements");

    internal static string GazeDirectory => Path.Combine(MeasurementsDirectory, "gaze");

    internal static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    internal static string SettingsFilePath => Path.Combine(RootDirectory, "beallitasok.json");

    // Az 1-es formátumverziójú mérések helye (az exe mellett); a statisztika ezeket is beolvassa.
    internal static string LegacyMeasurementsDirectory => Path.Combine(AppContext.BaseDirectory, "measurements");

    private static string BuildRootDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var baseDirectory = string.IsNullOrEmpty(documents)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : documents;

        return Path.Combine(baseDirectory, AppFolderName);
    }
}
