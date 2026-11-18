using System.ComponentModel;
using System.Text.RegularExpressions;
using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure.Esp;

// Kapcsolódás az ESP-hez soros porton. A port a beállításokból jön, vagy (ha ott üres) az
// USB-eszközazonosítók alapján automatikusan kerül kiválasztásra.
internal sealed class EspResponseDeviceConnector : IResponseDeviceConnector
{
    private const string UnknownPortName = "ismeretlen COM-port";

    public async Task<IResponseDevice> ConnectAsync(HardwareSettings settings, CancellationToken cancellationToken)
    {
        var portName = string.IsNullOrWhiteSpace(settings.EspPortName) ? null : settings.EspPortName.Trim();
        Serial? serial = null;

        try
        {
            // Az automatikus felismerés a rendszerleíró adatbázist járja be: háttérszálon fut.
            portName ??= await Task.Run(EspPortFinder.GetPreferredEspPortName, cancellationToken);
            serial = new Serial(portName, settings.EspBaudRate);
            serial.MessageReceived += message => Logger.Info($"[SERIAL][IN] '{message}'");
            await serial.StartAsync(cancellationToken);
            Logger.Info($"[SERIAL] Port megnyitva: {portName} @ {settings.EspBaudRate}");
            return new EspResponseDevice(serial, portName, settings.EspBaudRate);
        }
        catch (Exception ex)
        {
            // Megszakításkor is: a port a StartAsync közben már nyitva lehet.
            if (serial is not null)
            {
                await serial.DisposeAsync();
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            throw new ResponseDeviceConnectionException(portName ?? UnknownPortName, ex);
        }
    }

    // A beállítási ablak portlistájához. Ha a portok nem kérdezhetők le, üres listát ad (a kutató a
    // port nevét ilyenkor is beírhatja, vagy az automatikus felismerést választhatja).
    public IReadOnlyList<string> GetAvailablePortNames()
    {
        try
        {
            return EspPortFinder.GetAvailablePortNames();
        }
        catch (Exception ex) when (ex is Win32Exception || ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.Warning($"[SERIAL] A soros portok listája nem kérdezhető le: {ex.Message}");
            return Array.Empty<string>();
        }
    }
}

// Az ESP soros protokollja (lásd docs\SCANOUT_PROTOCOL.md): a mérés indításakor "CHECK" -> "OK"
// kapcsolatellenőrzés, majd "VERSION" -> a firmware verziója; próbánként a PC elküldi a
// scanout-korrekciót, majd a résztvevő kattintása után "RTIME"-mal lekéri a korrigált reakcióidőt, amit
// az ESP mikroszekundumban küld vissza - vagy "NA"-t, ha a próbában nem mért reakcióidőt (nem érzékelte
// a fényjelet vagy a kattintást).
//
// A válaszok formátuma egymást kizárja ("OK"; két pontot tartalmazó verziószám; szám vagy "NA"), így
// egy késve érkező válasz nem zárhatja le egy későbbi, másik kérés várakozását.
internal sealed class EspResponseDevice : IResponseDevice
{
    private const string CheckRequest = "CHECK";
    private const string CheckResponse = "OK";
    private const string ReactionTimeRequest = "RTIME";
    private const string NoReactionTimeResponse = "NA";

    // Az ESP a port megnyitásakor újraindulhat, és az indulás alatt érkező üzenet elveszhet, ezért a
    // CHECK-et néhányszor megismételjük.
    private const int CheckAttemptCount = 3;
    private static readonly TimeSpan CheckResponseTimeout = TimeSpan.FromSeconds(1);

    // A firmware verziójának lekérdezése: MAJOR.MINOR.PATCH, opcionális utótaggal (pl. "1.2.0-dev").
    // A régebbi firmware a VERSION sort figyelmen kívül hagyja, ezért a várakozás rövid.
    private const string VersionRequest = "VERSION";
    private static readonly TimeSpan VersionResponseTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$", RegexOptions.CultureInvariant);

    // Az ESP a kattintás idejét a PC-nél előbb rögzíti, így a választ azonnal küldheti; ennyi a tartalék.
    private static readonly TimeSpan ReactionTimeResponseTimeout = TimeSpan.FromSeconds(1);

    private readonly Serial _serial;

    public EspResponseDevice(Serial serial, string portName, int baudRate)
    {
        _serial = serial;
        PortName = portName;
        BaudRate = baudRate;
    }

    public string PortName { get; }

    public int BaudRate { get; }

    public string? FirmwareVersion { get; private set; }

    // Időtúllépéskor a Serial üres szöveget ad vissza; az "OK"-tól eltérő sorokat (pl. az ESP indulási
    // üzeneteit) a várakozás figyelmen kívül hagyja.
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= CheckAttemptCount; attempt++)
        {
            var response = await _serial.RequestResponseAsync(
                CheckRequest,
                IsCheckResponse,
                CheckResponseTimeout,
                cancellationToken).ConfigureAwait(false);

            if (IsCheckResponse(response))
            {
                Logger.Info($"[SERIAL] Kapcsolatellenőrzés rendben ({PortName}, {attempt}. próbálkozás).");
                return true;
            }

            Logger.Warning(
                $"[SERIAL] Nem érkezett \"{CheckResponse}\" válasz a(z) \"{CheckRequest}\" üzenetre "
                + $"{CheckResponseTimeout.TotalMilliseconds:0} ms alatt ({attempt}/{CheckAttemptCount}).");
        }

        return false;
    }

    public Task SendScanoutCorrectionAsync(string correctionMicroseconds, CancellationToken cancellationToken)
    {
        return _serial.SendAsync(correctionMicroseconds, cancellationToken);
    }

    // Az ESP minden RTIME kérésre azonnal válaszol: a reakcióidővel, vagy "NA"-val, ha a próbában nem
    // mért. Mindkettő lezárja a várakozást (az "NA"-ból null lesz); időtúllépéskor a Serial üres szöveget
    // ad vissza, amiből szintén null lesz.
    public async Task<double?> RequestReactionTimeMsAsync(CancellationToken cancellationToken)
    {
        var response = await _serial.RequestResponseAsync(
            ReactionTimeRequest,
            message => IsNoReactionTimeResponse(message) || ScanoutProtocol.TryParseReactionTime(message, out _),
            ReactionTimeResponseTimeout,
            cancellationToken).ConfigureAwait(false);

        if (IsNoReactionTimeResponse(response))
        {
            Logger.Info("[SERIAL] Az ESP ebben a próbában nem mért reakcióidőt (\"NA\").");
            return null;
        }

        return ScanoutProtocol.TryParseReactionTime(response, out var reactionTimeMs) ? reactionTimeMs : null;
    }

    private static bool IsNoReactionTimeResponse(string? message) =>
        string.Equals(message?.Trim(), NoReactionTimeResponse, StringComparison.OrdinalIgnoreCase);

    private static bool IsCheckResponse(string? message) => string.Equals(message?.Trim(), CheckResponse, StringComparison.Ordinal);

    // A sikeres CHECK után, egyszer. Ha nem jön verzió alakú sor (pl. régebbi firmware), a verzió
    // ismeretlen marad (null); a mérés ettől még elindulhat.
    public async Task<string?> RequestFirmwareVersionAsync(CancellationToken cancellationToken)
    {
        var response = await _serial.RequestResponseAsync(
            VersionRequest,
            IsVersionResponse,
            VersionResponseTimeout,
            cancellationToken).ConfigureAwait(false);

        FirmwareVersion = IsVersionResponse(response) ? response!.Trim() : null;
        if (FirmwareVersion is null)
        {
            Logger.Warning($"[SERIAL] Az ESP nem válaszolt a(z) \"{VersionRequest}\" kérésre; a firmware verziója ismeretlen.");
        }
        else
        {
            Logger.Info($"[SERIAL] Az ESP firmware verziója: {FirmwareVersion}");
        }

        return FirmwareVersion;
    }

    private static bool IsVersionResponse(string? message) => message is not null && VersionPattern.IsMatch(message.Trim());

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _serial.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warning($"[SERIAL] A port lezárása közben hiba történt: {ex.Message}");
        }
    }
}
