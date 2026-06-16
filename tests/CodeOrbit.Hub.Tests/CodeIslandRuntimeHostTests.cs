using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using CodeOrbit.Bridge;
using CodeOrbit.Contracts;
using CodeOrbit.Core.IPC;
using CodeOrbit.Core.Services;
using CodeOrbit.Hub;
using Xunit;

namespace CodeOrbit.Hub.Tests;

public class CodeOrbitRuntimeHostTests : IDisposable
{
    private readonly string? _originalPipeName;

    public CodeOrbitRuntimeHostTests()
    {
        _originalPipeName = Environment.GetEnvironmentVariable(NamedPipePath.OverrideEnvironmentVariable);
    }

    [Fact]
    public async Task StartAsync_ExposesHealthEndpoint()
    {
        using var runtime = CreateHost(out _);

        await runtime.StartAsync();

        using var client = new HttpClient();
        var health = await client.GetFromJsonAsync<ApiHealthDto>($"{runtime.ApiBaseUrl}/api/health");

        Assert.NotNull(health);
        Assert.Equal("ok", health!.Status);
    }

    [Fact]
    public async Task RuntimeHost_AcceptsBridgeEventsWithoutWpfProcess()
    {
        using var runtime = CreateHost(out var pipeName);
        Environment.SetEnvironmentVariable(NamedPipePath.OverrideEnvironmentVariable, pipeName);

        await runtime.StartAsync();

        var response = await BridgeClient.SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["hook_event_name"] = "SessionStart",
            ["session_id"] = "runtime-only-session",
            ["cwd"] = @"D:\Work\runtime-only",
            ["_source"] = "claude"
        }), blocking: false);

        Assert.Equal("{}", response);
        await WaitForAsync(() => runtime.HubState.GetSessions().Any(session => session.SessionId == "runtime-only-session"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(NamedPipePath.OverrideEnvironmentVariable, _originalPipeName);
    }

    private static CodeOrbitRuntimeHost CreateHost(out string pipeName)
    {
        pipeName = $"CodeOrbit-runtime-host-test-{Guid.NewGuid():N}";
        var settingsDir = Path.Combine(Path.GetTempPath(), $"CodeOrbit-runtime-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDir);

        return new CodeOrbitRuntimeHost(new CodeOrbitRuntimeHostOptions
        {
            Settings = new SettingsManager(settingsDir),
            ApiPort = GetFreeTcpPort(),
            PipeName = pipeName,
            RepairSourcesOnStart = false
        });
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
            await Task.Delay(25, cts.Token);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
