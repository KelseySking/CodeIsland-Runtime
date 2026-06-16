namespace CodeOrbit.Core.Sources;

public static class CodeOrbitSourceAdapterRegistry
{
    private static readonly ICodeOrbitSourceAdapter UnknownAdapter =
        new BuiltInSourceAdapter(
            "unknown",
            "未知工具",
            "unknown",
            CodeOrbitPermissionResponseStyle.ClaudeStyle);

    // No more hardcoded built-in adapters — all sources come from plugins
    private static readonly Dictionary<string, ICodeOrbitSourceAdapter> BuiltInAdapters = [];

    // Lazy loading of all adapters (plugins only)
    private static readonly Lazy<Dictionary<string, ICodeOrbitSourceAdapter>> AllAdapters = new(BuildAllAdapters);

    public static IReadOnlyCollection<string> KnownSources => AllAdapters.Value.Keys.ToArray();

    public static bool IsKnownSource(string? source) =>
        TryGet(source, out _);

    public static bool TryGet(string? source, out ICodeOrbitSourceAdapter adapter)
    {
        if (!string.IsNullOrWhiteSpace(source) &&
            AllAdapters.Value.TryGetValue(source.Trim(), out adapter!))
        {
            return true;
        }

        adapter = UnknownAdapter;
        return false;
    }

    public static ICodeOrbitSourceAdapter Get(string? source) =>
        TryGet(source, out var adapter) ? adapter : UnknownAdapter;

    private static Dictionary<string, ICodeOrbitSourceAdapter> BuildAllAdapters()
    {
        var adapters = new Dictionary<string, ICodeOrbitSourceAdapter>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var loader = new SourcePluginLoader(
                logError: msg => Console.Error.WriteLine($"[SourcePlugin] Error: {msg}"),
                logWarning: msg => Console.Error.WriteLine($"[SourcePlugin] Warning: {msg}"));

            var plugins = loader.LoadPlugins();

            foreach (var plugin in plugins)
            {
                if (!adapters.ContainsKey(plugin.SourceKey))
                {
                    adapters[plugin.SourceKey] = plugin;
                }
                else
                {
                    Console.Error.WriteLine($"[SourcePlugin] Warning: Plugin '{plugin.SourceKey}' conflicts with existing source (skipped)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SourcePlugin] Failed to load plugins: {ex.Message}");
        }

        return adapters;
    }

    private sealed class BuiltInSourceAdapter : ICodeOrbitSourceAdapter
    {
        public BuiltInSourceAdapter(
            string sourceKey,
            string displayName,
            string iconName,
            CodeOrbitPermissionResponseStyle permissionResponseStyle)
        {
            SourceKey = sourceKey;
            DisplayName = displayName;
            IconName = iconName;
            PermissionResponseStyle = permissionResponseStyle;
        }

        public string SourceKey { get; }
        public string DisplayName { get; }
        public string IconName { get; }
        public CodeOrbitPermissionResponseStyle PermissionResponseStyle { get; }

        public bool TryNormalizeEventName(string rawEventName, out string normalizedEventName)
        {
            normalizedEventName = string.Empty;
            return false;
        }
    }
}
