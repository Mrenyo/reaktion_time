using System.Diagnostics;
using ReactionTimeMeasurementApp.Diagnostics;
using Tobii.Research;

namespace ReactionTimeMeasurementApp.Infrastructure.Tobii;

internal static class TobiiCalibrationLauncher
{
    internal static int RunUserCalibration(IEyeTracker eyeTracker, int? screenNumber = null)
    {
        ArgumentNullException.ThrowIfNull(eyeTracker);

        var executablePath = FindEyeTrackerManagerExecutable();
        var screenArg = screenNumber.HasValue ? $" --screen={screenNumber.Value}" : string.Empty;
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(eyeTracker.SerialNumber))
        {
            candidates.Add($"--mode=usercalibration --device-sn=\"{eyeTracker.SerialNumber}\"{screenArg}");
        }

        var deviceAddress = eyeTracker.Address?.ToString();
        if (!string.IsNullOrWhiteSpace(deviceAddress))
        {
            candidates.Add($"--mode=usercalibration --device-address=\"{deviceAddress}\"{screenArg}");
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("A Tobii eszközhöz nem tartozik használható sorozatszám vagy cím.");
        }

        InvalidOperationException? lastError = null;

        foreach (var args in candidates.Distinct())
        {
            var result = RunProcess(executablePath, args);
            if (result.ExitCode == 0)
            {
                Logger.Info($"Tobii kalibráció sikeres. Args: {args}");
                return result.ExitCode;
            }

            Logger.Warning(
                $"Tobii Pro Eye Tracker Manager hiba. ExitCode={result.ExitCode} ({GetExitCodeDescription(result.ExitCode)}) Args: {args}");

            lastError = new InvalidOperationException(
                $"Tobii Pro Eye Tracker Manager hiba. ExitCode={result.ExitCode} ({GetExitCodeDescription(result.ExitCode)}){Environment.NewLine}Args: {args}{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StdOut}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StdErr}");

            if (result.ExitCode != 44)
            {
                throw lastError;
            }
        }

        throw lastError ?? new InvalidOperationException("A Tobii kalibráció indítása sikertelen volt.");
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

        process.WaitForExit();

        return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    private static string GetExitCodeDescription(int exitCode)
    {
        return exitCode switch
        {
            0 => "EXIT_OK",
            30 => "EXIT_ERROR_CALIBRATION_CANCELLED",
            31 => "EXIT_ERROR_CALIBRATION_FAILED",
            32 => "EXIT_ERROR_DISPLAYAREA_FAILED",
            33 => "EXIT_ERROR_DISPLAY_AREA_NOT_SET",
            44 => "EXIT_ERROR_NOT_FOUND",
            51 => "EXIT_ERROR_INVALID_ARGUMENT",
            52 => "EXIT_ERROR_MISSING_ARGUMENT",
            53 => "EXIT_ERROR_UNKNOWN_MODE",
            _ => "ISMERETLEN_HIBA"
        };
    }

    private static string FindEyeTrackerManagerExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var programsBase = Path.Combine(localAppData, "Programs", "TobiiProEyeTrackerManager");
        if (Directory.Exists(programsBase))
        {
            var appDir = Directory.EnumerateDirectories(programsBase, "app-*")
                .OrderByDescending(path => path)
                .FirstOrDefault();

            if (appDir != null)
            {
                var exe = Path.Combine(appDir, "TobiiProEyeTrackerManager.exe");
                if (File.Exists(exe))
                {
                    return exe;
                }
            }

            var directExe = Path.Combine(programsBase, "TobiiProEyeTrackerManager.exe");
            if (File.Exists(directExe))
            {
                return directExe;
            }
        }

        var legacyBase = Path.Combine(localAppData, "TobiiProEyeTrackerManager");
        if (Directory.Exists(legacyBase))
        {
            var appDir = Directory.EnumerateDirectories(legacyBase, "app*")
                .FirstOrDefault();

            if (appDir != null)
            {
                var exe = Path.Combine(appDir, "TobiiProEyeTrackerManager.exe");
                if (File.Exists(exe))
                {
                    return exe;
                }
            }
        }

        throw new FileNotFoundException(
            "Nem található a Tobii Pro Eye Tracker Manager executable. Ellenőrizd, hogy telepítve van-e.");
    }
}
