namespace CodeIsland.Core.Sources;

/// <summary>
/// Plugin-defined source adapter that implements ICodeIslandSourceAdapter.
/// Loaded from JSON files in the plugin directory at runtime.
/// </summary>
internal sealed class PluginSourceAdapter : ICodeIslandSourceAdapter
{
    private readonly IReadOnlyDictionary<string, string> _eventAliases;

    public PluginSourceAdapter(
        string sourceKey,
        string displayName,
        string iconName,
        CodeIslandPermissionResponseStyle permissionResponseStyle,
        IReadOnlyDictionary<string, string>? eventAliases = null)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            throw new ArgumentException("Source key cannot be null or whitespace.", nameof(sourceKey));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be null or whitespace.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(iconName))
            throw new ArgumentException("Icon name cannot be null or whitespace.", nameof(iconName));

        SourceKey = sourceKey;
        DisplayName = displayName;
        IconName = iconName;
        PermissionResponseStyle = permissionResponseStyle;
        _eventAliases = eventAliases ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string SourceKey { get; }

    public string DisplayName { get; }

    public string IconName { get; }

    public CodeIslandPermissionResponseStyle PermissionResponseStyle { get; }

    public bool TryNormalizeEventName(string rawEventName, out string normalizedEventName)
    {
        if (_eventAliases.TryGetValue(rawEventName.Trim(), out normalizedEventName!))
            return true;

        normalizedEventName = string.Empty;
        return false;
    }
}
