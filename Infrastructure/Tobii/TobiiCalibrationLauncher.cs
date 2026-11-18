using System.Diagnostics;
using ReactionTimeMeasurementApp.Diagnostics;
using Tobii.Research;

namespace ReactionTimeMeasurementApp.Infrastructure.Tobii;

// A felhasználói kalibrációt a Tobii Pro Eye Tracker Manager parancssori módja végzi. Hiba vagy
// megszakítás esetén InvalidOperationException-t dob (a kalibrációs nézet ezt "sikertelen
// kalibrációként" jeleníti meg), hiányzó telepítésnél FileNotFoundException-t.
internal static class TobiiCalibrationLauncher
{
    private const string ExecutableName = "TobiiProEyeTrackerManager.exe";
    private const int ExitCodeNotFound = 44;

    // A kalibráció interaktív (a résztvevő tempójában halad), ezért bőkezű a korlát; csak arra
    // szolgál, hogy egy lefagyott Eye Tracker Manager ne tartsa örökre a háttérszálat.
    private static readonly TimeSpan CalibrationTimeout = TimeSpan.FromMinutes(10);

    // A kilépés után a kimenet maradékára ennyit várunk: ha egy gyerekfolyamat örökölte a csöveket,
    // azok a fő folyamat kilépése után is nyitva maradhatnak.
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);

    internal static int RunUserCalibration(IEyeTracker eyeTracker)
    {
        ArgumentNullException.ThrowIfNull(eyeTracker);

        var executablePath = FindEyeTrackerManagerExecutable();
        var deviceArgs = new[]
            {
                (Name: "device-sn", Value: eyeTracker.SerialNumber),
                (Name: "device-address", Value: eyeTracker.Address?.ToString())
            }
            .Where(arg => !string.IsNullOrWhiteSpace(arg.Value))
            .Select(arg => $"--mode=usercalibration --{arg.Name}=\"{arg.Value}\"")
            .ToList();

        if (deviceArgs.Count == 0)
        {
            throw new InvalidOperationException("A Tobii eszközhöz nem tartozik használható sorozatszám vagy cím.");
        }

        // Ha az Eye Tracker Manager sorozatszám alapján nem találja az eszközt (44), a címmel is
        // megpróbáljuk; minden más hiba azonnal végleges.
        InvalidOperationException? lastError = null;
        foreach (var args in deviceArgs)
        {
            var (exitCode, stdOut, stdErr) = RunProcess(executablePath, args);
            if (exitCode == 0)
            {
                Logger.Info($"Tobii kalibráció sikeres. Args: {args}");
                return exitCode;
            }

            var description = $"Tobii Pro Eye Tracker Manager hiba. ExitCode={exitCode} ({GetExitCodeDescription(exitCode)})";
            Logger.Warning($"{description} Args: {args}");

            lastError = new InvalidOperationException(
                $"{description}{Environment.NewLine}Args: {args}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdOut}{Environment.NewLine}STDERR:{Environment.NewLine}{stdErr}");

            if (exitCode != ExitCodeNotFound)
            {
                break;
            }
        }

        throw lastError!;
    }

    private static (int ExitCode, string StdOut, string StdErr) RunProcess(string executablePath, string args)
    {
        using var process = new Process();
        process.StartInfo.FileName = executablePath;
        process.StartInfo.Arguments = args;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();

        // A két kimenetet párhuzamosan kell olvasni: ha egymás után olvasnánk őket, és a folyamat
        // közben megtöltené a másik (még nem olvasott) cső pufferét, mindkét fél örökre várna.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(CalibrationTimeout))
        {
            Logger.Warning($"A Tobii Pro Eye Tracker Manager {CalibrationTimeout.TotalMinutes:0} perc alatt sem lépett ki, leállítjuk. Args: {args}");
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is NotSupportedException)
            {
                Logger.Warning($"Az Eye Tracker Manager leállítása sikertelen: {e.Message}");
            }

            throw new InvalidOperationException(
                $"A Tobii Pro Eye Tracker Manager {CalibrationTimeout.TotalMinutes:0} perc alatt sem fejezte be a kalibrációt, ezért leállítottuk.{Environment.NewLine}Args: {args}");
        }

        try
        {
            Task.WaitAll([stdoutTask, stderrTask], OutputDrainTimeout);
        }
        catch (AggregateException)
        {
            // A kimenet csak a naplóhoz kell; olvasási hibánál üresen marad.
        }

        return (process.ExitCode, ResultOrEmpty(stdoutTask), ResultOrEmpty(stderrTask));
    }

    private static string ResultOrEmpty(Task<string> task) => task.IsCompletedSuccessfully ? task.Result : string.Empty;

    private static string GetExitCodeDescription(int exitCode)
    {
        return exitCode switch
        {
            0 => "EXIT_OK",
            30 => "EXIT_ERROR_CALIBRATION_CANCELLED",
            31 => "EXIT_ERROR_CALIBRATION_FAILED",
            32 => "EXIT_ERROR_DISPLAYAREA_FAILED",
            33 => "EXIT_ERROR_DISPLAY_AREA_NOT_SET",
            ExitCodeNotFound => "EXIT_ERROR_NOT_FOUND",
            51 => "EXIT_ERROR_INVALID_ARGUMENT",
            52 => "EXIT_ERROR_MISSING_ARGUMENT",
            53 => "EXIT_ERROR_UNKNOWN_MODE",
            _ => "ISMERETLEN_HIBA"
        };
    }

    // Keresési sorrend: az újabb telepítő helye (Programs\...\app-<verzió>, majd közvetlenül a
    // mappában), végül a régi telepítőé (...\app<verzió>). Mindkét helyen a legújabb verzió az első.
    private static string FindEyeTrackerManagerExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programsBase = Path.Combine(localAppData, "Programs", "TobiiProEyeTrackerManager");
        var legacyBase = Path.Combine(localAppData, "TobiiProEyeTrackerManager");

        var directories = AppDirectoriesNewestFirst(programsBase)
            .Append(programsBase)
            .Concat(AppDirectoriesNewestFirst(legacyBase));

        return directories.Select(directory => Path.Combine(directory, ExecutableName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "Nem található a Tobii Pro Eye Tracker Manager executable. Ellenőrizd, hogy telepítve van-e.");
    }

    // A verziók szám szerint rendezve (az "app-2.10.0" újabb, mint az "app-2.9.0"); az értelmezhetetlen
    // nevűek a végére kerülnek, egymás közt név szerint csökkenő sorrendben.
    private static IEnumerable<string> AppDirectoriesNewestFirst(string baseDirectory)
    {
        return Directory.Exists(baseDirectory)
            ? Directory.EnumerateDirectories(baseDirectory, "app*")
                .OrderByDescending(ParseAppVersion)
                .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            : [];
    }

    private static Version ParseAppVersion(string appDirectory)
    {
        var name = Path.GetFileName(appDirectory);
        var versionText = name.StartsWith("app", StringComparison.OrdinalIgnoreCase) ? name[3..].TrimStart('-') : name;
        return Version.TryParse(versionText, out var version) ? version : new Version(0, 0);
    }
}
