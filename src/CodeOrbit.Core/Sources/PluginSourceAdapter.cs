namespace CodeOrbit.Core.Sources;

/// <summary>
/// Plugin-defined source adapter that implements IPluginSourceAdapter.
/// Loaded from JSON files in the plugin directory at runtime.
/// </summary>
internal sealed class PluginSourceAdapter : IPluginSourceAdapter
{
    private readonly IReadOnlyDictionary<string, string> _eventAliases;
    private readonly DetectionRule? _detectionRule;
    private readonly HookInstallationSpec? _hookInstallationSpec;

    public PluginSourceAdapter(
        string sourceKey,
        string displayName,
        string iconName,
        CodeOrbitPermissionResponseStyle permissionResponseStyle,
        IReadOnlyDictionary<string, string>? eventAliases = null,
        DetectionRule? detectionRule = null,
        HookInstallationSpec? hookInstallationSpec = null)
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
        _detectionRule = detectionRule;
        _hookInstallationSpec = hookInstallationSpec;
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

    public DetectionRule? GetDetectionRule() => _detectionRule;

    public HookInstallationSpec? GetHookInstallationSpec() => _hookInstallationSpec;
}
