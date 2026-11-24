using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScanoutValidacio;

// Egy vizsgált képernyőpont: a mérő négyzet középpontja a képernyő szélességének és magasságának
// arányában (0-1). Ha az Y null, a négyzet a referencia-jelző sorába kerül (várt különbség: 0).
internal sealed class MeasurementPoint
{
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double? Y { get; set; }
}

// beallitasok.json az exe mellett; ha nincs, az első indításkor az alapértékekkel jön létre.
internal sealed class Settings
{
    public const string FileName = "beallitasok.json";

    // Üres: az összes soros port közül az, amelyiken a validációs firmware válaszol.
    public string Port { get; set; } = "";
    public int BaudRate { get; set; } = 115200;

    // Üres: az elsődleges kijelző; egyébként a GDI-név, pl. \\.\DISPLAY2.
    public string Display { get; set; } = "";

    public int TrialsPerPoint { get; set; } = 50;

    // Nyers ADC-küszöbök (a reakcióidő-mérőben 475). A mérés előtt, a felhelyező nézetben a [K]
    // billentyűvel a mért sötét és világos szint felére állíthatók.
    public int ThresholdReference { get; set; } = 475;
    public int ThresholdMeasurement { get; set; } = 475;

    // A referencia-jelző a reakcióidő-mérő alapértelmezett jelzőjével azonos: 56 x 56 képpont,
    // a jobb és alsó széltől 20 képpontra, 16 képpontos fekete védősávval.
    public int MarkerSizePx { get; set; } = 56;
    public int MarkerMarginPx { get; set; } = 20;
    public int MarkerGuardPx { get; set; } = 16;

    // A mérő négyzet a reakcióidő-mérő ingerének felel meg: háttérszínről vált a négyzet színére.
    public int PointSizePx { get; set; } = 56;
    public string PointColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#000000";

    public int LitDurationMs { get; set; } = 150;
    public int DarkMinMs { get; set; } = 300;
    public int DarkMaxMs { get; set; } = 700;

    // Üres: az exe melletti "meresek" mappa.
    public string OutputFolder { get; set; } = "";

    public List<MeasurementPoint> Points { get; set; } =
    [
        new() { Name = "P1", X = 0.50, Y = 0.08 },
        new() { Name = "P2", X = 0.25, Y = 0.30 },
        new() { Name = "P3", X = 0.50, Y = 0.50 },
        new() { Name = "P4", X = 0.75, Y = 0.72 },
        new() { Name = "P5", X = 0.10, Y = null },
    ];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static Settings LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var defaults = new Settings();
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }

        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Üres beállításfájl: {path}");
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        if (TrialsPerPoint < 1) throw new InvalidDataException("A TrialsPerPoint legalább 1.");
        if (Points.Count == 0) throw new InvalidDataException("Legalább egy mérési pont kell.");
        if (DarkMinMs < 200 || DarkMaxMs < DarkMinMs)
        {
            throw new InvalidDataException("A DarkMinMs legalább 200 (a fotodióda-jel lecsengése), a DarkMaxMs nem kisebb nála.");
        }

        if (LitDurationMs < 50) throw new InvalidDataException("A LitDurationMs legalább 50.");
        foreach (var p in Points)
        {
            if (p.X is < 0 or > 1 || p.Y is < 0 or > 1)
            {
                throw new InvalidDataException($"A(z) {p.Name} pont koordinátája 0 és 1 közé kell essen.");
            }
        }

        _ = ColorTranslator.FromHtml(PointColor);
        _ = ColorTranslator.FromHtml(BackgroundColor);
    }
}
