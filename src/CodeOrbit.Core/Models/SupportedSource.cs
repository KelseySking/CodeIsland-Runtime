using CodeOrbit.Core.Sources;

namespace CodeOrbit.Core.Models;

/// <summary>
/// 支持的 AI 编程工具来源定义
/// </summary>
public static class SupportedSource
{
    public static IReadOnlyCollection<string> All => CodeOrbitSourceAdapterRegistry.KnownSources;

    public static bool IsValid(string source) =>
        CodeOrbitSourceAdapterRegistry.IsKnownSource(source);

    public static string GetDisplayName(string source)
    {
        if (CodeOrbitSourceAdapterRegistry.TryGet(source, out var adapter))
            return adapter.DisplayName;

        return source switch
        {
            "unknown" => "未知工具",
            "CodeOrbit" => "CodeOrbit",
            _ => source
        };
    }

    public static string GetIconName(string source)
    {
        if (CodeOrbitSourceAdapterRegistry.TryGet(source, out var adapter))
            return adapter.IconName;

        return source;
    }
}
