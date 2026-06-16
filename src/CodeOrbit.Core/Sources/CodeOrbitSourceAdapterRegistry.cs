namespace CodeOrbit.Core.Sources;

public static class CodeOrbitSourceAdapterRegistry
{
    private static readonly ICodeOrbitSourceAdapter UnknownAdapter =
        new BuiltInSourceAdapter(
            "unknown",
            "未知工具",
            "unknown",
            CodeOrbitPermissionResponseStyle.ClaudeStyle);

    // Separate built-in adapters for clarity and protection
    private static readonly Dictionary<string, ICodeOrbitSourceAdapter> BuiltInAdapters = BuildBuiltInAdapters();

    // Lazy loading of all adapters (built-in + plugins)
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

    private static Dictionary<string, ICodeOrbitSourceAdapter> BuildBuiltInAdapters()
    {
        return new Dictionary<string, ICodeOrbitSourceAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude"] = new BuiltInSourceAdapter("claude", "Claude Code", "claude", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["codex"] = new BuiltInSourceAdapter("codex", "Codex", "codex", CodeOrbitPermissionResponseStyle.Codex),
            ["gemini"] = new BuiltInSourceAdapter(
                "gemini",
                "Gemini CLI",
                "gemini",
                CodeOrbitPermissionResponseStyle.ClaudeStyle,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BeforeTool"] = "PreToolUse",
                    ["AfterTool"] = "PostToolUse",
                    ["BeforeAgent"] = "SubagentStart",
                    ["AfterAgent"] = "SubagentStop"
                }),
            ["cursor"] = new BuiltInSourceAdapter(
                "cursor",
                "Cursor",
                "cursor",
                CodeOrbitPermissionResponseStyle.ClaudeStyle,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["beforeSubmitPrompt"] = "UserPromptSubmit",
                    ["beforeShellExecution"] = "PreToolUse",
                    ["afterShellExecution"] = "PostToolUse",
                    ["beforeMcpToolExecution"] = "PreToolUse",
                    ["afterMcpToolExecution"] = "PostToolUse",
                    ["subagentStart"] = "SubagentStart",
                    ["subagentStop"] = "SubagentStop"
                }),
            ["cursor-cli"] = new BuiltInSourceAdapter("cursor-cli", "Cursor CLI", "cursor", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["trae"] = new BuiltInSourceAdapter("trae", "Trae", "trae", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["traecn"] = new BuiltInSourceAdapter("traecn", "Trae CN", "trae", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["traecli"] = new BuiltInSourceAdapter("traecli", "TraeCli", "traecli", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["copilot"] = new BuiltInSourceAdapter("copilot", "GitHub Copilot", "copilot", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["qoder"] = new BuiltInSourceAdapter("qoder", "Qoder", "qoder", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["qoder-cli"] = new BuiltInSourceAdapter("qoder-cli", "Qoder CLI", "qoder", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["droid"] = new BuiltInSourceAdapter("droid", "Factory", "factory", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["codebuddy"] = new BuiltInSourceAdapter("codebuddy", "CodeBuddy", "codebuddy", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["codybuddycn"] = new BuiltInSourceAdapter("codybuddycn", "CodyBuddy CN", "codebuddy", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["stepfun"] = new BuiltInSourceAdapter("stepfun", "StepFun", "stepfun", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["opencode"] = new BuiltInSourceAdapter("opencode", "OpenCode", "opencode", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["antigravity"] = new BuiltInSourceAdapter("antigravity", "AntiGravity", "antigravity", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["workbuddy"] = new BuiltInSourceAdapter("workbuddy", "WorkBuddy", "workbuddy", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["hermes"] = new BuiltInSourceAdapter("hermes", "Hermes", "hermes", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["qwen"] = new BuiltInSourceAdapter("qwen", "Qwen Code", "qwen", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["kimi"] = new BuiltInSourceAdapter("kimi", "Kimi Code", "kimi", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["pi"] = new BuiltInSourceAdapter("pi", "Pi", "pi", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["kiro"] = new BuiltInSourceAdapter("kiro", "Kiro", "kiro", CodeOrbitPermissionResponseStyle.ClaudeStyle),
            ["cline"] = new BuiltInSourceAdapter(
                "cline",
                "Cline",
                "cline",
                CodeOrbitPermissionResponseStyle.ClaudeStyle,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TaskStart"] = "SessionStart",
                    ["TaskResume"] = "UserPromptSubmit",
                    ["TaskComplete"] = "Stop"
                })
        };
    }

    private static Dictionary<string, ICodeOrbitSourceAdapter> BuildAllAdapters()
    {
        // Start with built-in adapters (protected from override)
        var adapters = new Dictionary<string, ICodeOrbitSourceAdapter>(
            BuiltInAdapters,
            StringComparer.OrdinalIgnoreCase);

        // Load and merge plugins
        try
        {
            var loader = new SourcePluginLoader(
                logError: msg => Console.Error.WriteLine($"[SourcePlugin] Error: {msg}"),
                logWarning: msg => Console.Error.WriteLine($"[SourcePlugin] Warning: {msg}"));

            var plugins = loader.LoadPlugins();

            foreach (var plugin in plugins)
            {
                // Skip if conflicts with built-in (protection)
                if (!adapters.ContainsKey(plugin.SourceKey))
                {
                    adapters[plugin.SourceKey] = plugin;
                }
                else
                {
                    Console.Error.WriteLine($"[SourcePlugin] Warning: Plugin '{plugin.SourceKey}' conflicts with built-in source (skipped)");
                }
            }
        }
        catch (Exception ex)
        {
            // Isolation: plugin loading errors don't break registry
            Console.Error.WriteLine($"[SourcePlugin] Failed to load plugins: {ex.Message}");
        }

        return adapters;
    }

    private sealed class BuiltInSourceAdapter : ICodeOrbitSourceAdapter
    {
        private readonly IReadOnlyDictionary<string, string> _eventAliases;

        public BuiltInSourceAdapter(
            string sourceKey,
            string displayName,
            string iconName,
            CodeOrbitPermissionResponseStyle permissionResponseStyle,
            IReadOnlyDictionary<string, string>? eventAliases = null)
        {
            SourceKey = sourceKey;
            DisplayName = displayName;
            IconName = iconName;
            PermissionResponseStyle = permissionResponseStyle;
            _eventAliases = eventAliases ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string SourceKey { get; }

        public string DisplayName { get; }

        public string IconName { get; }

        public CodeOrbitPermissionResponseStyle PermissionResponseStyle { get; }

        public bool TryNormalizeEventName(string rawEventName, out string normalizedEventName)
        {
            if (_eventAliases.TryGetValue(rawEventName.Trim(), out normalizedEventName!))
                return true;

            normalizedEventName = string.Empty;
            return false;
        }
    }
}
