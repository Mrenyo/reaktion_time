namespace ReactionTimeMeasurementApp.Classes;

// Központosítja, hogy az alkalmazás fejlesztői (dev) módban fut-e. Dev módban jelenik meg
// a MainMenu gaze-overlay-je (a szemkövető aktuális nézési pontját mutató kör) és a statisztika
// nézet scanout-korrekció debug oszlopa. Korábban ez két külön, egymástól független, kódba égetett
// bool konstans volt, amit csak forráskód-módosítással és újrafordítással lehetett bekapcsolni -
// emiatt tudott a kettő értéke el is térni egymástól. Most egyetlen, futásidőben (parancssori
// kapcsolóval vagy környezeti változóval) vezérelhető jelző dönti el mindkettőt, alapértelmezetten
// kikapcsolt (production-biztos) állapotban.
internal static class DeveloperMode
{
    private const string CommandLineSwitch = "--dev";
    private const string EnvironmentVariableName = "REACTIONTIME_DEV_MODE";

    internal static bool IsEnabled { get; private set; }

    internal static void Initialize(string[] args)
    {
        IsEnabled = args.Any(arg => string.Equals(arg, CommandLineSwitch, StringComparison.OrdinalIgnoreCase))
            || IsTruthy(Environment.GetEnvironmentVariable(EnvironmentVariableName));

        Logger.Info($"Fejlesztői mód: {(IsEnabled ? "bekapcsolva" : "kikapcsolva")}.");
    }

    private static bool IsTruthy(string? value)
    {
        return value is not null
            && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }
}
