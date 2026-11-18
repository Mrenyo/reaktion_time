namespace ReactionTimeMeasurementApp.Domain;

// A beállítások szakaszai; a beállítási ablak fülei ugyanezek, ugyanezekkel a címekkel
// (SettingsSections.GetTitle).
public enum SettingsSection
{
    Trials,
    Appearance,
    Gaze,
    Hardware
}

public static class SettingsSections
{
    public static string GetTitle(SettingsSection section) => section switch
    {
        SettingsSection.Trials => "Ingerek és időzítés",
        SettingsSection.Appearance => "Megjelenés",
        SettingsSection.Gaze => "Szemmozgás",
        SettingsSection.Hardware => "Hardver",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Ismeretlen beállítási szakasz.")
    };
}

// A beállítások egy hibája vagy figyelmeztetése: az üzenet és az a szakasz, ahol javítható. A felület a
// szakasz alapján jelöli meg a fület; a szöveges megjelenítés (ToString) az üzenet végén zárójelben
// megnevezi a fület is, pl. "... (Megjelenés fül)".
public sealed record SettingsIssue(SettingsSection Section, string Message)
{
    public override string ToString() => $"{Message} ({SettingsSections.GetTitle(Section)} fül)";
}
