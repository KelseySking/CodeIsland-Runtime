using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeIsland.Core.Sources;

/// <summary>
/// Parses and validates plugin JSON files.
/// </summary>
internal static class SourcePluginJsonParser
{
    private static readonly Regex SourceKeyPattern = new(@"^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$", RegexOptions.Compiled);

    // Standard event names that plugins can map to
    private static readonly HashSet<string> ValidEventNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PreToolUse",
        "PostToolUse",
        "UserPromptSubmit",
        "SessionStart",
        "SessionEnd",
        "Stop",
        "SubagentStart",
        "SubagentStop",
        "Notification",
        "PermissionRequest",
        "PostToolUseFailure",
        "PreCompact"
    };

    public static (bool Success, PluginMetadata? Metadata, string? Error, PluginValidationError? ValidationError)
        Parse(string jsonContent, IReadOnlyCollection<string> existingSourceKeys)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Validate schema_version (support 1.0 and 2.0)
            if (!root.TryGetProperty("schema_version", out var schemaVersion))
            {
                return (false, null, "Missing 'schema_version'.",
                    PluginValidationError.InvalidSchemaVersion);
            }

            var version = schemaVersion.GetString();
            if (version != "1.0" && version != "2.0")
            {
                return (false, null, $"Invalid 'schema_version'. Expected '1.0' or '2.0', got: '{version}'",
                    PluginValidationError.InvalidSchemaVersion);
            }

            // Get source object
            if (!root.TryGetProperty("source", out var source))
            {
                return (false, null, "Missing required 'source' object.",
                    PluginValidationError.MissingRequiredField);
            }

            // Extract and validate source.key
            if (!source.TryGetProperty("key", out var keyElement))
            {
                return (false, null, "Missing required 'source.key'.",
                    PluginValidationError.MissingRequiredField);
            }

            var sourceKey = keyElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return (false, null, "'source.key' cannot be empty.",
                    PluginValidationError.InvalidSourceKey);
            }

            if (!ValidateSourceKey(sourceKey))
            {
                return (false, null,
                    $"'source.key' must match pattern: lowercase alphanumeric with hyphens, 2-64 chars. Got: '{sourceKey}'",
                    PluginValidationError.InvalidSourceKey);
            }

            if (existingSourceKeys.Contains(sourceKey, StringComparer.OrdinalIgnoreCase))
            {
                return (false, null, $"Source key '{sourceKey}' already exists (duplicate or conflicts with built-in).",
                    PluginValidationError.DuplicateSourceKey);
            }

            // Extract and validate source.display_name
            if (!source.TryGetProperty("display_name", out var displayNameElement))
            {
                return (false, null, "Missing required 'source.display_name'.",
                    PluginValidationError.MissingRequiredField);
            }

            var displayName = displayNameElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
            {
                return (false, null, "'source.display_name' must be 1-100 characters.",
                    PluginValidationError.MissingRequiredField);
            }

            // Extract and validate source.icon_name
            if (!source.TryGetProperty("icon_name", out var iconNameElement))
            {
                return (false, null, "Missing required 'source.icon_name'.",
                    PluginValidationError.MissingRequiredField);
            }

            var iconName = iconNameElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(iconName) || iconName.Length > 64)
            {
                return (false, null, "'source.icon_name' must be 1-64 characters.",
                    PluginValidationError.MissingRequiredField);
            }

            // Extract and validate source.permission_response_style
            if (!source.TryGetProperty("permission_response_style", out var styleElement))
            {
                return (false, null, "Missing required 'source.permission_response_style'.",
                    PluginValidationError.MissingRequiredField);
            }

            var styleString = styleElement.GetString();
            if (!TryParsePermissionStyle(styleString, out var permissionStyle))
            {
                return (false, null,
                    $"Invalid 'source.permission_response_style'. Expected 'claude-style' or 'codex', got: '{styleString}'",
                    PluginValidationError.InvalidPermissionStyle);
            }

            // Parse event_mappings (optional)
            var eventMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("event_mappings", out var mappingsElement) &&
                mappingsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var mapping in mappingsElement.EnumerateObject())
                {
                    var targetEvent = mapping.Value.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(targetEvent))
                        continue;

                    if (!IsValidEventName(targetEvent))
                    {
                        return (false, null,
                            $"Invalid event mapping target: '{targetEvent}'. Must be a standard event name.",
                            PluginValidationError.InvalidEventMapping);
                    }

                    eventMappings[mapping.Name] = targetEvent;
                }
            }

            // Parse detection section (optional, schema 2.0)
            DetectionRule? detectionRule = null;
            if (root.TryGetProperty("detection", out var detectionElement))
            {
                var parseResult = ParseDetection(sourceKey, detectionElement);
                if (!parseResult.Success)
                {
                    return (false, null, parseResult.Error, PluginValidationError.InvalidJson);
                }
                detectionRule = parseResult.DetectionRule;
            }

            // Parse hook_installation section (optional, schema 2.0)
            HookInstallationSpec? hookSpec = null;
            if (root.TryGetProperty("hook_installation", out var hookElement))
            {
                var parseResult = ParseHookInstallation(hookElement);
                if (!parseResult.Success)
                {
                    return (false, null, parseResult.Error, PluginValidationError.InvalidJson);
                }
                hookSpec = parseResult.HookSpec;
            }

            var metadata = new PluginMetadata(
                sourceKey,
                displayName,
                iconName,
                permissionStyle,
                eventMappings,
                Detection: detectionRule,
                HookInstallation: hookSpec);

            return (true, metadata, null, null);
        }
        catch (JsonException ex)
        {
            return (false, null, $"Invalid JSON: {ex.Message}", PluginValidationError.InvalidJson);
        }
        catch (Exception ex)
        {
            return (false, null, $"Unexpected error: {ex.Message}", PluginValidationError.InvalidJson);
        }
    }

    private static bool ValidateSourceKey(string sourceKey)
    {
        return SourceKeyPattern.IsMatch(sourceKey);
    }

    private static bool TryParsePermissionStyle(string? style, out CodeIslandPermissionResponseStyle result)
    {
        result = CodeIslandPermissionResponseStyle.ClaudeStyle;

        if (string.IsNullOrWhiteSpace(style))
            return false;

        switch (style.ToLowerInvariant())
        {
            case "claude-style":
                result = CodeIslandPermissionResponseStyle.ClaudeStyle;
                return true;
            case "codex":
                result = CodeIslandPermissionResponseStyle.Codex;
                return true;
            default:
                return false;
        }
    }

    private static bool IsValidEventName(string eventName)
    {
        return ValidEventNames.Contains(eventName);
    }

    private static (bool Success, DetectionRule? DetectionRule, string? Error) ParseDetection(
        string sourceKey,
        JsonElement detectionElement)
    {
        if (detectionElement.ValueKind != JsonValueKind.Object)
            return (false, null, "'detection' must be an object");

        // Parse process_names
        var processNames = new List<string>();
        if (detectionElement.TryGetProperty("process_names", out var processNamesElement) &&
            processNamesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in processNamesElement.EnumerateArray())
            {
                var name = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    processNames.Add(name);
            }
        }

        // Parse env_var_hints
        var envVarHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (detectionElement.TryGetProperty("env_var_hints", out var envElement) &&
            envElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in envElement.EnumerateObject())
            {
                var pattern = prop.Value.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(pattern))
                    envVarHints[prop.Name] = pattern;
            }
        }

        // Parse path_patterns
        var pathPatterns = new List<string>();
        if (detectionElement.TryGetProperty("path_patterns", out var pathElement) &&
            pathElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pathElement.EnumerateArray())
            {
                var pattern = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(pattern))
                    pathPatterns.Add(pattern);
            }
        }

        // Parse priority (default 100)
        var priority = 100;
        if (detectionElement.TryGetProperty("priority", out var priorityElement))
        {
            if (priorityElement.ValueKind == JsonValueKind.Number)
                priority = priorityElement.GetInt32();
        }

        // Validate using PluginValidator
        var (isValid, error) = PluginValidator.ValidateDetection(
            processNames,
            envVarHints,
            pathPatterns,
            priority);

        if (!isValid)
            return (false, null, $"Detection validation failed: {error}");

        var rule = new DetectionRule(
            sourceKey,
            processNames,
            envVarHints,
            pathPatterns,
            priority);

        return (true, rule, null);
    }

    private static (bool Success, HookInstallationSpec? HookSpec, string? Error) ParseHookInstallation(
        JsonElement hookElement)
    {
        if (hookElement.ValueKind != JsonValueKind.Object)
            return (false, null, "'hook_installation' must be an object");

        // Parse format (required)
        if (!hookElement.TryGetProperty("format", out var formatElement))
            return (false, null, "Missing required 'hook_installation.format'");

        var format = formatElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(format))
            return (false, null, "'hook_installation.format' cannot be empty");

        // Parse config_path (required)
        if (!hookElement.TryGetProperty("config_path", out var pathElement))
            return (false, null, "Missing required 'hook_installation.config_path'");

        var configPath = pathElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(configPath))
            return (false, null, "'hook_installation.config_path' cannot be empty");

        // Parse events (required)
        if (!hookElement.TryGetProperty("events", out var eventsElement) ||
            eventsElement.ValueKind != JsonValueKind.Array)
            return (false, null, "Missing or invalid 'hook_installation.events' (must be array)");

        var events = new List<string>();
        foreach (var item in eventsElement.EnumerateArray())
        {
            var eventName = item.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(eventName))
                events.Add(eventName);
        }

        // Parse timeout_seconds (default 10)
        var timeoutSeconds = 10;
        if (hookElement.TryGetProperty("timeout_seconds", out var timeoutElement))
        {
            if (timeoutElement.ValueKind == JsonValueKind.Number)
                timeoutSeconds = timeoutElement.GetInt32();
        }

        // Parse extra_config (optional)
        ExtraConfigSpec? extraConfig = null;
        if (hookElement.TryGetProperty("extra_config", out var extraElement) &&
            extraElement.ValueKind == JsonValueKind.Object)
        {
            var parseResult = ParseExtraConfig(extraElement);
            if (!parseResult.Success)
                return (false, null, parseResult.Error);
            extraConfig = parseResult.ExtraConfig;
        }

        // Validate using PluginValidator
        var (isValid, error) = PluginValidator.ValidateHookInstallation(
            format,
            configPath,
            events,
            timeoutSeconds);

        if (!isValid)
            return (false, null, $"Hook installation validation failed: {error}");

        var spec = new HookInstallationSpec(
            format,
            configPath,
            events,
            timeoutSeconds,
            extraConfig);

        return (true, spec, null);
    }

    private static (bool Success, ExtraConfigSpec? ExtraConfig, string? Error) ParseExtraConfig(
        JsonElement extraElement)
    {
        // Parse file (required)
        if (!extraElement.TryGetProperty("file", out var fileElement))
            return (false, null, "Missing required 'extra_config.file'");

        var filePath = fileElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
            return (false, null, "'extra_config.file' cannot be empty");

        // Parse key (required)
        if (!extraElement.TryGetProperty("key", out var keyElement))
            return (false, null, "Missing required 'extra_config.key'");

        var key = keyElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(key))
            return (false, null, "'extra_config.key' cannot be empty");

        // Parse value (required)
        if (!extraElement.TryGetProperty("value", out var valueElement))
            return (false, null, "Missing required 'extra_config.value'");

        var value = valueElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return (false, null, "'extra_config.value' cannot be empty");

        // Parse section (optional)
        string? section = null;
        if (extraElement.TryGetProperty("section", out var sectionElement))
        {
            section = sectionElement.GetString()?.Trim();
        }

        var spec = new ExtraConfigSpec(filePath, section, key, value);
        return (true, spec, null);
    }
}
