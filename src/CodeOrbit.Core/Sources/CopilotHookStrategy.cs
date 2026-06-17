using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeOrbit.Core.Sources;

/// <summary>
/// Hook installation strategy for GitHub Copilot hook arrays.
/// Format: {version, hooks: [{event, command, timeout}]}
/// </summary>
internal sealed class CopilotHookStrategy : IHookInstallationStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool Install(string sourceKey, HookInstallationSpec spec)
    {
        try
        {
            var configPath = HookInstallationUtils.ExpandPath(spec.ConfigPath);
            var root = ReadObject(configPath);
            root["version"] ??= 1;

            var hooks = root["hooks"] as JsonArray ?? new JsonArray();
            var mergedHooks = new JsonArray();

            foreach (var hook in hooks)
            {
                if (!IsCodeOrbitHook(hook, sourceKey))
                    mergedHooks.Add(hook?.DeepClone());
            }

            var command = HookInstallationUtils.GetHookCommand(sourceKey);
            foreach (var eventName in spec.Events)
            {
                mergedHooks.Add(new JsonObject
                {
                    ["event"] = eventName,
                    ["command"] = command,
                    ["timeout"] = spec.TimeoutSeconds
                });
            }

            root["hooks"] = mergedHooks;
            WriteObject(configPath, root);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Uninstall(string sourceKey, HookInstallationSpec spec)
    {
        try
        {
            var configPath = HookInstallationUtils.ExpandPath(spec.ConfigPath);
            if (!File.Exists(configPath))
                return true;

            var root = ReadObject(configPath);
            var hooks = root["hooks"] as JsonArray;
            if (hooks == null)
                return true;

            var remainingHooks = new JsonArray();
            foreach (var hook in hooks)
            {
                if (!IsCodeOrbitHook(hook, sourceKey))
                    remainingHooks.Add(hook?.DeepClone());
            }

            root["hooks"] = remainingHooks;
            WriteObject(configPath, root);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsInstalled(string sourceKey, HookInstallationSpec spec)
    {
        try
        {
            var configPath = HookInstallationUtils.ExpandPath(spec.ConfigPath);
            if (!File.Exists(configPath))
                return false;

            var root = ReadObject(configPath);
            if (root["hooks"] is not JsonArray hooks)
                return false;

            return hooks.Any(hook => IsCodeOrbitHook(hook, sourceKey));
        }
        catch
        {
            return false;
        }
    }

    private static JsonObject ReadObject(string configPath)
    {
        if (!File.Exists(configPath))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static void WriteObject(string configPath, JsonObject root)
    {
        HookInstallationUtils.EnsureDirectoryExists(configPath);
        File.WriteAllText(configPath, root.ToJsonString(JsonOptions));
    }

    private static bool IsCodeOrbitHook(JsonNode? hook, string sourceKey)
    {
        var command = hook?["command"]?.GetValue<string>() ?? string.Empty;
        return command.Contains("CodeOrbit.Bridge", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("CodeOrbit-bridge", StringComparison.OrdinalIgnoreCase) ||
               command.Contains($"--source {sourceKey}", StringComparison.OrdinalIgnoreCase);
    }
}
