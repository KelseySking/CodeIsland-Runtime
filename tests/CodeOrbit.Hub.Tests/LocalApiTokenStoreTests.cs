using CodeOrbit.Core.Services;
using CodeOrbit.Hub;
using Xunit;

namespace CodeOrbit.Hub.Tests;

public class LocalApiTokenStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "CodeOrbit-hub-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureToken_WhenMissing_GeneratesAndPersistsToken()
    {
        var settings = new SettingsManager(_tempDir);

        var token = LocalApiTokenStore.EnsureToken(settings);
        var loaded = new SettingsManager(_tempDir).Get(LocalApiTokenStore.SettingsKey, "");

        Assert.True(token.Length >= 32);
        Assert.Equal(token, loaded);
    }

    [Fact]
    public void EnsureToken_WhenExisting_ReusesToken()
    {
        var settings = new SettingsManager(_tempDir);
        settings.Set(LocalApiTokenStore.SettingsKey, "existing-token-value-with-enough-length");

        var token = LocalApiTokenStore.EnsureToken(settings);

        Assert.Equal("existing-token-value-with-enough-length", token);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
