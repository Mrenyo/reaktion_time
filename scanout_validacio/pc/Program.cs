using System.Globalization;
using System.Text;

namespace ScanoutValidacio;

// Használat:
//   ScanoutValidacio.exe                     mérés (a beallitasok.json a munkakönyvtárban)
//   ScanoutValidacio.exe --elemzes <mappa> [--csendes]
//                                            egy korábbi mérés újraelemzése (nyers.csv + meta.json);
//                                            --csendes: összefoglaló ablak nélkül
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            if (args.Length >= 2 && args[0] == "--elemzes")
            {
                var (points, overall) = Analysis.Run(args[1]);
                if (args.Contains("--csendes"))
                {
                    return;
                }

                var sb = new StringBuilder();
                foreach (var p in points)
                {
                    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                        $"{p.Name}: várt {p.ExpectedUs / 1000:0.000} ms, mért {p.MeanUs / 1000:0.000} ± {p.SdUs / 1000:0.000} ms, eltérés {p.ResidualMeanUs:0.0} µs (n={p.ValidCount})"));
                }

                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Regresszió: meredekség {overall.Slope:0.0000}, tengelymetszet {overall.InterceptUs:0.0} µs, R² {overall.RSquared:0.00000}"));
                sb.AppendLine().Append($"Kimenet: {Analysis.TableFile}, {Analysis.SummaryFile}, {Analysis.ChartFile}");
                MessageBox.Show(sb.ToString(), "Scanout-validáció: elemzés");
                return;
            }

            var settings = Settings.LoadOrCreate(Path.Combine(Environment.CurrentDirectory, Settings.FileName));
            var screen = string.IsNullOrWhiteSpace(settings.Display)
                ? Screen.PrimaryScreen ?? Screen.AllScreens[0]
                : Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, settings.Display, StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException(
                      $"Nincs {settings.Display} kijelző. Elérhetők: {string.Join(", ", Screen.AllScreens.Select(s => s.DeviceName))}");
            Application.Run(new MeasurementForm(settings, screen));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Scanout-validáció", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
