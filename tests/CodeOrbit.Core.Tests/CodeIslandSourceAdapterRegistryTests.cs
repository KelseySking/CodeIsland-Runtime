using CodeOrbit.Core.Sources;
using Xunit;

namespace CodeOrbit.Core.Tests;

public class CodeOrbitSourceAdapterRegistryTests
{
    [Fact]
    public void Get_Codex_ReturnsCodexPermissionStyle()
    {
        var adapter = CodeOrbitSourceAdapterRegistry.Get("codex");

        Assert.Equal("codex", adapter.SourceKey);
        Assert.Equal(CodeOrbitPermissionResponseStyle.Codex, adapter.PermissionResponseStyle);
        Assert.Equal("Codex", adapter.DisplayName);
    }

    [Fact]
    public void Get_Unknown_ReturnsFallbackAdapter()
    {
        var adapter = CodeOrbitSourceAdapterRegistry.Get("not-a-real-source");

        Assert.Equal("unknown", adapter.SourceKey);
        Assert.Equal("未知工具", adapter.DisplayName);
        Assert.False(CodeOrbitSourceAdapterRegistry.IsKnownSource("not-a-real-source"));
    }

    [Fact]
    public void CursorAdapter_NormalizesCursorSpecificEvents()
    {
        var adapter = CodeOrbitSourceAdapterRegistry.Get("cursor");

        Assert.True(adapter.TryNormalizeEventName("beforeMcpToolExecution", out var normalized));
        Assert.Equal("PreToolUse", normalized);
    }
}
