namespace ReactionTimeMeasurementApp.Infrastructure;

// Futásidejű kapcsolók. Fejlesztői (dev) módban jelenik meg a főablak gaze-overlay-je (a szemkövető
// aktuális nézési pontját mutató kör) és a statisztika nézet scanout-korrekció debug oszlopa. A
// fejlesztői mód parancssori kapcsolóval (--dev) vagy környezeti változóval kapcsolható be;
// alapértelmezetten ki van kapcsolva.
internal sealed record AppOptions(bool DeveloperMode)
{
    private const string DeveloperModeSwitch = "--dev";
    private const string DeveloperModeEnvironmentVariable = "REACTIONTIME_DEV_MODE";

    public static AppOptions FromCommandLine(string[] args)
    {
        var developerMode = args.Any(arg => string.Equals(arg, DeveloperModeSwitch, StringComparison.OrdinalIgnoreCase))
            || IsTruthy(Environment.GetEnvironmentVariable(DeveloperModeEnvironmentVariable));
        return new AppOptions(developerMode);
    }

    private static bool IsTruthy(string? value)
    {
        return value is not null
            && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }
}
