namespace CodeIsland.Core.Sources;

/// <summary>
/// Metadata extracted from a plugin JSON file.
/// </summary>
internal sealed record PluginMetadata(
    string SourceKey,
    string DisplayName,
    string IconName,
    CodeIslandPermissionResponseStyle PermissionResponseStyle,
    IReadOnlyDictionary<string, string> EventMappings,
    DetectionRule? Detection,
    HookInstallationSpec? HookInstallation);

/// <summary>
/// Result of attempting to load a plugin from a file.
/// </summary>
public sealed record PluginLoadResult(
    bool Success,
    ICodeIslandSourceAdapter? Adapter,
    string? ErrorMessage,
    PluginValidationError? ValidationError);

/// <summary>
/// Specific validation errors that can occur when loading a plugin.
/// </summary>
public enum PluginValidationError
{
    None,
    InvalidJson,
    MissingRequiredField,
    InvalidSchemaVersion,
    InvalidSourceKey,
    DuplicateSourceKey,
    ConflictWithBuiltInSource,
    InvalidPermissionStyle,
    InvalidEventMapping
}
