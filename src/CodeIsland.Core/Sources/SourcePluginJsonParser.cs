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

            // Validate schema_version
            if (!root.TryGetProperty("schema_version", out var schemaVersion) ||
                schemaVersion.GetString() != "1.0")
            {
                return (false, null, "Missing or invalid 'schema_version'. Expected '1.0'.",
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

            var metadata = new PluginMetadata(
                sourceKey,
                displayName,
                iconName,
                permissionStyle,
                eventMappings,
                Detection: null,  // TODO: Parse detection section in Phase 2A
                HookInstallation: null);  // TODO: Parse hook_installation section in Phase 2A

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
}
