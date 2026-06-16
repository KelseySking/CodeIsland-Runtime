namespace CodeIsland.Core.Sources;

/// <summary>
/// Discovers and loads CLI source plugins from JSON files.
/// </summary>
public sealed class SourcePluginLoader
{
    private readonly string _pluginDirectory;
    private readonly Action<string>? _logError;
    private readonly Action<string>? _logWarning;

    public SourcePluginLoader(
        string? pluginDirectory = null,
        Action<string>? logError = null,
        Action<string>? logWarning = null)
    {
        _pluginDirectory = pluginDirectory ?? GetDefaultPluginDirectory();
        _logError = logError;
        _logWarning = logWarning;
    }

    public static string GetDefaultPluginDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeIsland",
            "sources");
    }

    /// <summary>
    /// Loads all valid plugins from the plugin directory.
    /// Invalid plugins are skipped with logging.
    /// </summary>
    public IReadOnlyList<ICodeIslandSourceAdapter> LoadPlugins()
    {
        var adapters = new List<ICodeIslandSourceAdapter>();
        var loadedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ensure directory exists
        try
        {
            if (!Directory.Exists(_pluginDirectory))
            {
                Directory.CreateDirectory(_pluginDirectory);
                return adapters; // Empty directory, no plugins
            }
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"Failed to create plugin directory '{_pluginDirectory}': {ex.Message}");
            return adapters;
        }

        // Discover *.json files
        string[] pluginFiles;
        try
        {
            pluginFiles = Directory.GetFiles(_pluginDirectory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"Failed to enumerate plugin files in '{_pluginDirectory}': {ex.Message}");
            return adapters;
        }

        // Load each file
        foreach (var filePath in pluginFiles)
        {
            var result = TryLoadPluginFromFile(filePath, loadedKeys);

            if (result.Success && result.Adapter != null)
            {
                adapters.Add(result.Adapter);
                loadedKeys.Add(result.Adapter.SourceKey);
            }
            else if (result.ErrorMessage != null)
            {
                var fileName = Path.GetFileName(filePath);

                // Log errors vs warnings based on severity
                if (result.ValidationError == PluginValidationError.DuplicateSourceKey)
                {
                    _logWarning?.Invoke($"Plugin '{fileName}': {result.ErrorMessage} (skipped)");
                }
                else
                {
                    _logError?.Invoke($"Plugin '{fileName}': {result.ErrorMessage} (skipped)");
                }
            }
        }

        return adapters;
    }

    /// <summary>
    /// Attempts to load a single plugin from a file.
    /// </summary>
    public PluginLoadResult TryLoadPluginFromFile(string filePath, IReadOnlyCollection<string> existingKeys)
    {
        try
        {
            // Read file content
            string jsonContent;
            try
            {
                jsonContent = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                return new PluginLoadResult(
                    false,
                    null,
                    $"Failed to read file: {ex.Message}",
                    null);
            }

            // Parse JSON
            var parseResult = SourcePluginJsonParser.Parse(jsonContent, existingKeys);

            if (!parseResult.Success || parseResult.Metadata == null)
            {
                return new PluginLoadResult(
                    false,
                    null,
                    parseResult.Error,
                    parseResult.ValidationError);
            }

            // Create adapter
            var metadata = parseResult.Metadata;
            var adapter = new PluginSourceAdapter(
                metadata.SourceKey,
                metadata.DisplayName,
                metadata.IconName,
                metadata.PermissionResponseStyle,
                metadata.EventMappings);

            return new PluginLoadResult(true, adapter, null, null);
        }
        catch (Exception ex)
        {
            return new PluginLoadResult(
                false,
                null,
                $"Unexpected error loading plugin: {ex.Message}",
                null);
        }
    }
}
