namespace ReactionTimeMeasurementApp.Classes;

// Az alkalmazás által írt fájlok (mérési eredmények, tekintetminták, beállítások, napló) helye.
// Korábban minden az exe mellé került, ami Program Files alá telepítve nem írható - ott a mentés
// csendben meghiúsult volna. A felhasználó Dokumentumok mappája mindig írható, és a kutató is
// könnyen megtalálja. A REACTIONTIME_DATA_DIR környezeti változóval más mappa is megadható
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

    // A korábbi verziók az exe mellé mentettek; ezeket a statisztika továbbra is beolvassa.
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
