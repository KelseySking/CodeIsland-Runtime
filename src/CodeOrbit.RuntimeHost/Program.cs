using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using CodeOrbit.Core.Services;
using CodeOrbit.Hub;

var manifest = RuntimeManifest.TryLoad();
var arguments = RuntimeHostArguments.Parse(args);
var settingsDirectory = arguments.SettingsDirectory ?? manifest?.DefaultSettingsDir;
var settings = new SettingsManager(settingsDirectory);
var logger = new EventLogger();
var apiPort = Math.Clamp(arguments.PortOverride ?? settings.Get("api_port", manifest?.DefaultPort ?? 32145), 1024, 65535);

using var singleInstance = RuntimeHostSingleInstance.TryAcquire(apiPort);
if (singleInstance == null)
{
    Console.Error.WriteLine("CodeOrbit RuntimeHost is already running for this API port.");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    logger.Write("CodeOrbit.RuntimeHost", "cancel-key");
    cts.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    logger.Write("CodeOrbit.RuntimeHost", "process-exit");
    cts.Cancel();
};

var ownerMonitorTask = arguments is { ShutdownWhenOwnerExits: true, OwnerPid: { } ownerPid }
    ? RuntimeHostOwnerMonitor.StartAsync(ownerPid, cts, logger)
    : Task.CompletedTask;

var runtimeHost = new CodeOrbitRuntimeHost(new CodeOrbitRuntimeHostOptions
{
    Settings = settings,
    Logger = logger,
    ApiPort = apiPort,
    ApiToken = arguments.TokenOverride,
    PipeName = arguments.PipeNameOverride,
    ApiHost = arguments.HostOverride ?? settings.Get("api_bind_host", manifest?.DefaultHost ?? "127.0.0.1"),
    RepairSourcesOnStart = !arguments.SkipRepair
});

try
{
    logger.Write("CodeOrbit.RuntimeHost", "start-begin", new Dictionary<string, string?>
    {
        ["port"] = apiPort.ToString(),
        ["host"] = arguments.HostOverride ?? settings.Get("api_bind_host", manifest?.DefaultHost ?? "127.0.0.1"),
        ["pipeName"] = arguments.PipeNameOverride,
        ["ownerPid"] = arguments.OwnerPid?.ToString(),
        ["shutdownWhenOwnerExits"] = arguments.ShutdownWhenOwnerExits.ToString()
    });
    await runtimeHost.StartAsync(cts.Token);
    logger.Write("CodeOrbit.RuntimeHost", "start-complete", new Dictionary<string, string?>
    {
        ["baseUrl"] = runtimeHost.ApiBaseUrl,
        ["pipeName"] = runtimeHost.PipeName
    });
    Console.WriteLine($"CodeOrbit RuntimeHost started at {runtimeHost.ApiBaseUrl}");
    Console.WriteLine($"Named pipe: {runtimeHost.PipeName}");

    await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
    return 0;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    logger.Write("CodeOrbit.RuntimeHost", "stopped");
    return 0;
}
catch (Exception ex)
{
    logger.Write("CodeOrbit.RuntimeHost", "failed", new Dictionary<string, string?>
    {
        ["message"] = ex.Message,
        ["exception"] = ex.GetType().Name
    });
    Console.Error.WriteLine($"CodeOrbit RuntimeHost failed: {ex.Message}");
    return 1;
}
finally
{
    cts.Cancel();
    try
    {
        await ownerMonitorTask;
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
    }
    await runtimeHost.DisposeAsync();
}

internal sealed record RuntimeHostArguments(
    string? SettingsDirectory,
    int? PortOverride,
    string? HostOverride,
    string? TokenOverride,
    string? PipeNameOverride,
    int? OwnerPid,
    bool ShutdownWhenOwnerExits,
    bool SkipRepair)
{
    public static RuntimeHostArguments Parse(string[] args)
    {
        string? settingsDirectory = null;
        int? portOverride = null;
        string? hostOverride = null;
        string? tokenOverride = null;
        string? pipeNameOverride = null;
        int? ownerPid = null;
        var shutdownWhenOwnerExits = false;
        var skipRepair = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--settings-dir" when i + 1 < args.Length:
                    settingsDirectory = args[++i];
                    break;
                case "--port" when i + 1 < args.Length && int.TryParse(args[i + 1], out var port):
                    portOverride = port;
                    i++;
                    break;
                case "--host" when i + 1 < args.Length:
                    hostOverride = args[++i];
                    break;
                case "--token" when i + 1 < args.Length:
                    tokenOverride = args[++i];
                    break;
                case "--pipe-name" when i + 1 < args.Length:
                    pipeNameOverride = args[++i];
                    break;
                case "--owner-pid" when i + 1 < args.Length && int.TryParse(args[i + 1], out var pid):
                    ownerPid = pid;
                    i++;
                    break;
                case "--shutdown-when-owner-exits":
                    shutdownWhenOwnerExits = true;
                    break;
                case "--no-repair":
                    skipRepair = true;
                    break;
            }
        }

        return new RuntimeHostArguments(settingsDirectory, portOverride, hostOverride, tokenOverride, pipeNameOverride, ownerPid, shutdownWhenOwnerExits, skipRepair);
    }
}

internal static class RuntimeHostOwnerMonitor
{
    public static async Task StartAsync(int ownerPid, CancellationTokenSource cts, EventLogger logger)
    {
        try
        {
            using var owner = Process.GetProcessById(ownerPid);
            logger.Write("CodeOrbit.RuntimeHost", "owner-monitor-started", new Dictionary<string, string?>
            {
                ["ownerPid"] = ownerPid.ToString()
            });

            while (!cts.IsCancellationRequested)
            {
                owner.Refresh();
                if (owner.HasExited)
                {
                    logger.Write("CodeOrbit.RuntimeHost", "owner-exited", new Dictionary<string, string?>
                    {
                        ["ownerPid"] = ownerPid.ToString()
                    });
                    cts.Cancel();
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
            }
        }
        catch (ArgumentException)
        {
            logger.Write("CodeOrbit.RuntimeHost", "owner-not-found", new Dictionary<string, string?>
            {
                ["ownerPid"] = ownerPid.ToString()
            });
            cts.Cancel();
        }
    }
}

internal sealed class RuntimeHostSingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    private RuntimeHostSingleInstance(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static RuntimeHostSingleInstance? TryAcquire(int port)
    {
        var mutex = new Mutex(initiallyOwned: true, GetName(port), out var createdNew);
        if (createdNew)
            return new RuntimeHostSingleInstance(mutex);

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static string GetName(int port)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"CodeOrbit.RuntimeHost:{port}")));
        return $@"Local\CodeOrbit.RuntimeHost.{hash[..16]}";
    }
}

internal sealed record RuntimeManifest(
    string RuntimeVersion,
    string ContractVersion,
    string HostExe,
    string BridgeExe,
    int DefaultPort,
    string DefaultHost,
    string? DefaultPipeName,
    string? DefaultSettingsDir)
{
    public static RuntimeManifest? TryLoad()
    {
        try
        {
            var manifestPath = Path.Combine(AppContext.BaseDirectory, "runtime-manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            var json = File.ReadAllText(manifestPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new RuntimeManifest(
                RuntimeVersion: root.GetProperty("runtimeVersion").GetString() ?? "0.0.0",
                ContractVersion: root.GetProperty("contractVersion").GetString() ?? "1",
                HostExe: root.GetProperty("hostExe").GetString() ?? "CodeOrbit.RuntimeHost.exe",
                BridgeExe: root.GetProperty("bridgeExe").GetString() ?? "CodeOrbit.Bridge.exe",
                DefaultPort: root.TryGetProperty("defaultPort", out var port) ? port.GetInt32() : 32145,
                DefaultHost: root.TryGetProperty("defaultHost", out var host) ? host.GetString() ?? "127.0.0.1" : "127.0.0.1",
                DefaultPipeName: root.TryGetProperty("defaultPipeName", out var pipe) && pipe.ValueKind != JsonValueKind.Null ? pipe.GetString() : null,
                DefaultSettingsDir: root.TryGetProperty("defaultSettingsDir", out var settingsDir) && settingsDir.ValueKind != JsonValueKind.Null ? settingsDir.GetString() : null
            );
        }
        catch
        {
            return null;
        }
    }
}
