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
        Assert.Equal("Codex CLI", adapter.DisplayName);
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
    public void ClineAdapter_NormalizesClineSpecificEvents()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var loader = new SourcePluginLoader(
            logError: msg => errors.Add(msg),
            logWarning: msg => warnings.Add(msg));
        var plugins = loader.LoadPlugins();
        var cline = plugins.FirstOrDefault(p => p.SourceKey == "cline");

        Assert.NotNull(cline);
        Assert.True(cline.TryNormalizeEventName("TaskStart", out var n1), $"TaskStart should normalize, errors: {string.Join("; ", errors)}");
        Assert.Equal("SessionStart", n1);
        Assert.True(cline.TryNormalizeEventName("TaskResume", out var n2), "TaskResume should normalize");
        Assert.Equal("UserPromptSubmit", n2);
    }

    [Fact]
    public void CursorAdapter_NormalizesCursorSpecificEvents()
    {
        var adapter = CodeOrbitSourceAdapterRegistry.Get("cursor");

        Assert.True(adapter.TryNormalizeEventName("beforeMcpToolExecution", out var normalized));
        Assert.Equal("PreToolUse", normalized);
    }
}
