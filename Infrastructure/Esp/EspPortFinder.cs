using System.IO.Ports;
using System.Security;
using Microsoft.Win32;

namespace ReactionTimeMeasurementApp.Infrastructure.Esp;

// Az ESP soros portjának automatikus felismerése. A rendszerleíró adatbázis USB-eszközei közül azt a
// COM-portot választja, amelynek a neve vagy az USB-azonosítója (VID/PID) ESP-re vagy a hozzá használt
// USB-soros átalakítókra (CP210x, CH340, FTDI) utal.
internal static class EspPortFinder
{
    // Ettől a pontszámtól tekintünk egy portot ESP-nek.
    private const int EspCandidateScore = 50;
    private const int MaxRegistryDepth = 5;

    // IOException-t dob, ha nincs soros port, vagy több közül egyik sem azonosítható ESP-ként.
    public static string GetPreferredEspPortName()
    {
        var candidates = GetPortCandidates();
        if (candidates.Length == 0)
        {
            throw new IOException("Nem található elérhető COM-port.");
        }

        var preferred = candidates.FirstOrDefault(candidate => candidate.Score >= EspCandidateScore);
        if (preferred is not null)
        {
            return preferred.PortName;
        }

        if (candidates.Length == 1)
        {
            return candidates[0].PortName;
        }

        var availablePorts = string.Join(", ", candidates.Select(candidate => candidate.DisplayText));
        throw new IOException(
            $"Nem sikerült egyértelműen azonosítani az ESP COM-portját. Elérhető portok: {availablePorts}");
    }

    // A rendszerben elérhető soros portok neve, ismétlődés nélkül, sorszám szerint rendezve
    // (COM2 a COM10 előtt). Win32Exception-t dob, ha a portok listája nem kérdezhető le.
    public static string[] GetAvailablePortNames()
    {
        return SerialPort.GetPortNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetPortNumber)
            .ThenBy(portName => portName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PortCandidate[] GetPortCandidates()
    {
        var portNames = GetAvailablePortNames();
        var availablePorts = new HashSet<string>(portNames, StringComparer.OrdinalIgnoreCase);
        var candidatesByPort = portNames.ToDictionary(
            portName => portName,
            portName => new PortCandidate(portName, string.Empty, 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in ReadRegistryCandidates(availablePorts))
        {
            if (candidate.Score > candidatesByPort[candidate.PortName].Score)
            {
                candidatesByPort[candidate.PortName] = candidate;
            }
        }

        return candidatesByPort.Values
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => GetPortNumber(candidate.PortName))
            .ThenBy(candidate => candidate.PortName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<PortCandidate> ReadRegistryCandidates(HashSet<string> availablePorts)
    {
        using var enumKey = OpenSubKeySafely(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Enum");
        if (enumKey is null)
        {
            yield break;
        }

        foreach (var rootName in new[] { "USB", "FTDIBUS" })
        {
            using var rootKey = OpenSubKeySafely(enumKey, rootName);
            if (rootKey is null)
            {
                continue;
            }

            foreach (var candidate in ReadRegistryCandidates(rootKey, rootName, availablePorts, depth: 0))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<PortCandidate> ReadRegistryCandidates(
        RegistryKey registryKey,
        string hardwareId,
        HashSet<string> availablePorts,
        int depth)
    {
        if (depth > MaxRegistryDepth)
        {
            yield break;
        }

        foreach (var subKeyName in ReadSafely(registryKey.GetSubKeyNames) ?? Array.Empty<string>())
        {
            using var subKey = OpenSubKeySafely(registryKey, subKeyName);
            if (subKey is null)
            {
                continue;
            }

            var childHardwareId = $"{hardwareId}\\{subKeyName}";
            using var deviceParametersKey = OpenSubKeySafely(subKey, "Device Parameters");
            var portName = deviceParametersKey is null ? null : GetStringValue(deviceParametersKey, "PortName");

            if (!string.IsNullOrWhiteSpace(portName) && availablePorts.Contains(portName))
            {
                var friendlyName = GetStringValue(subKey, "FriendlyName") ?? GetStringValue(subKey, "DeviceDesc") ?? string.Empty;
                yield return new PortCandidate(portName, friendlyName, ScoreEspCandidate(friendlyName, childHardwareId));
            }

            foreach (var candidate in ReadRegistryCandidates(subKey, childHardwareId, availablePorts, depth + 1))
            {
                yield return candidate;
            }
        }
    }

    private static int ScoreEspCandidate(string friendlyName, string hardwareId)
    {
        var text = $"{friendlyName} {hardwareId}";
        var score = 0;

        // Az Espressif saját USB-azonosítója (pl. az ESP32-S3 beépített USB-soros portja).
        if (ContainsAny(text, "VID_303A", "ESPRESSIF", "ESP32", "ESP8266", "USB JTAG/SERIAL DEBUG UNIT"))
        {
            score += 100;
        }

        // A fejlesztőlapokon szokásos USB-soros átalakítók: Silicon Labs CP210x, WCH CH34x/CH9102.
        if (ContainsAny(text, "VID_10C4&PID_EA60", "VID_10C4&PID_EA70", "CP210", "SILICON LABS"))
        {
            score += 80;
        }

        if (ContainsAny(text, "VID_1A86", "CH340", "CH343", "CH344", "CH9102", "WCH"))
        {
            score += 80;
        }

        if (ContainsAny(text, "VID_0403", "FTDI"))
        {
            score += 50;
        }

        if (ContainsAny(text, "USB-SERIAL", "USB SERIAL", "USB-ENHANCED-SERIAL"))
        {
            score += 20;
        }

        if (ContainsAny(text, "BLUETOOTH"))
        {
            score -= 100;
        }

        return Math.Max(0, score);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetPortNumber(string portName)
    {
        return portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(portName[3..], out var portNumber)
            ? portNumber
            : int.MaxValue;
    }

    private static RegistryKey? OpenSubKeySafely(RegistryKey registryKey, string subKeyName)
    {
        return ReadSafely(() => registryKey.OpenSubKey(subKeyName));
    }

    private static string? GetStringValue(RegistryKey registryKey, string valueName)
    {
        return ReadSafely(() => registryKey.GetValue(valueName) as string);
    }

    // A rendszerleíró adatbázis egyes kulcsai nem olvashatók (jogosultság, közben eltávolított eszköz):
    // ezeket a keresés egyszerűen kihagyja.
    private static T? ReadSafely<T>(Func<T?> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            return null;
        }
    }

    private sealed record PortCandidate(string PortName, string FriendlyName, int Score)
    {
        public string DisplayText => string.IsNullOrWhiteSpace(FriendlyName) ? PortName : $"{PortName} ({FriendlyName})";
    }
}
