using System.Text.RegularExpressions;

namespace CodeOrbit.Core.Sources;

/// <summary>
/// Validates plugin configurations for security and correctness.
/// </summary>
public static class PluginValidator
{
    // Limits
    private const int MaxProcessNames = 20;
    private const int MaxEnvVarHints = 10;
    private const int MaxPathPatterns = 10;
    private const int MaxEvents = 50;
    private const int MinTimeoutSeconds = 1;
    private const int MaxTimeoutSeconds = 86400; // 24 hours

    /// <summary>
    /// Validates detection rules.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateDetection(
        IReadOnlyList<string> processNames,
        IReadOnlyDictionary<string, string> envVarHints,
        IReadOnlyList<string> pathPatterns,
        int priority)
    {
        // Check limits
        if (processNames.Count > MaxProcessNames)
            return (false, $"Too many process names (max {MaxProcessNames})");

        if (envVarHints.Count > MaxEnvVarHints)
            return (false, $"Too many environment variable hints (max {MaxEnvVarHints})");

        if (pathPatterns.Count > MaxPathPatterns)
            return (false, $"Too many path patterns (max {MaxPathPatterns})");

        // Validate process names
        foreach (var name in processNames)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64)
                return (false, $"Invalid process name: '{name}'");

            if (!Regex.IsMatch(name, @"^[a-zA-Z0-9_\-]+$"))
                return (false, $"Process name contains invalid characters: '{name}'");
        }

        // Validate env var patterns
        foreach (var (varName, pattern) in envVarHints)
        {
            if (string.IsNullOrWhiteSpace(varName))
                return (false, "Environment variable name cannot be empty");

            if (!IsValidEnvVarName(varName))
                return (false, $"Invalid environment variable name: '{varName}'");

            var (patternValid, patternError) = ValidatePattern(pattern);
            if (!patternValid)
                return (false, $"Invalid env var pattern for '{varName}': {patternError}");
        }

        // Validate path patterns
        foreach (var pattern in pathPatterns)
        {
            var (patternValid, patternError) = ValidatePattern(pattern);
            if (!patternValid)
                return (false, $"Invalid path pattern: {patternError}");
        }

        // Validate priority
        if (priority < 1 || priority > 1000)
            return (false, $"Priority must be between 1 and 1000 (got {priority})");

        return (true, null);
    }

    /// <summary>
    /// Validates hook installation spec.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateHookInstallation(
        string format,
        string configPath,
        IReadOnlyList<string> events,
        int timeoutSeconds)
    {
        // Validate format
        if (!HookFormats.IsSupported(format))
            return (false, $"Unsupported hook format: '{format}'. Supported: {string.Join(", ", HookFormats.Supported)}");

        // Validate config path
        var (pathValid, pathError) = ValidateConfigPath(configPath);
        if (!pathValid)
            return (false, pathError);

        // Validate events
        if (events.Count == 0)
            return (false, "At least one event must be specified");

        if (events.Count > MaxEvents)
            return (false, $"Too many events (max {MaxEvents})");

        foreach (var eventName in events)
        {
            if (!IsValidStandardEvent(eventName))
                return (false, $"Invalid event name: '{eventName}'");
        }

        // Validate timeout
        if (timeoutSeconds < MinTimeoutSeconds || timeoutSeconds > MaxTimeoutSeconds)
            return (false, $"Timeout must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds} seconds");

        return (true, null);
    }

    /// <summary>
    /// Validates a config file path for security.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateConfigPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Config path cannot be empty");

        // Must start with user home directory markers
        if (!path.StartsWith("~/") &&
            !path.StartsWith("$HOME/") &&
            !path.StartsWith("%APPDATA%/") &&
            !path.StartsWith("%USERPROFILE%/"))
        {
            return (false, "Config path must start with ~/,  $HOME/, %APPDATA%/, or %USERPROFILE%/");
        }

        // No directory traversal
        if (path.Contains(".."))
            return (false, "Config path cannot contain '..' (directory traversal)");

        // Expand and check actual path (if possible)
        try
        {
            var expanded = ExpandPath(path).ToLowerInvariant().Replace('\\', '/');

            // Blacklist system directories
            var forbidden = new[]
            {
                "c:/windows/", "/windows/",
                "c:/system/", "/system/",
                "/etc/", "/usr/", "/var/",
                "c:/program files/", "c:/program files (x86)/"
            };

            if (forbidden.Any(f => expanded.StartsWith(f)))
                return (false, $"Config path cannot be in system directory: {path}");
        }
        catch
        {
            // Path expansion failed, but the format checks passed
        }

        return (true, null);
    }

    /// <summary>
    /// Validates a pattern (glob or regex) for safety (ReDoS prevention).
    /// </summary>
    private static (bool IsValid, string? Error) ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return (false, "Pattern cannot be empty");

        // Check for dangerous regex patterns (ReDoS)
        if (pattern.Contains("(.*)*") ||
            pattern.Contains("(.+)+") ||
            pattern.Contains("(a|a)*"))
        {
            return (false, "Pattern contains dangerous nested quantifiers");
        }

        // Count nested groups (too many = potential ReDoS)
        var nestedGroups = Regex.Matches(pattern, @"\(.*\)\*").Count;
        if (nestedGroups > 2)
            return (false, "Pattern has too many nested quantified groups");

        // Try to compile and test with timeout
        if (pattern.Contains("^") || pattern.Contains("$") || pattern.Contains("\\"))
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));

                // Test against worst-case input
                regex.IsMatch(new string('a', 1000));
                return (true, null);
            }
            catch (RegexMatchTimeoutException)
            {
                return (false, "Pattern is too slow (potential ReDoS)");
            }
            catch (ArgumentException ex)
            {
                return (false, $"Invalid regex pattern: {ex.Message}");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Checks if a string is a valid environment variable name.
    /// </summary>
    private static bool IsValidEnvVarName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
            return false;

        // Windows/Unix env var naming rules
        return Regex.IsMatch(name, @"^[A-Z_][A-Z0-9_]*$", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Checks if an event name is a valid standard event.
    /// </summary>
    private static bool IsValidStandardEvent(string eventName)
    {
        var validEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PreToolUse", "PostToolUse", "UserPromptSubmit",
            "SessionStart", "SessionEnd", "Stop",
            "SubagentStart", "SubagentStop", "Notification",
            "PermissionRequest", "PostToolUseFailure", "PreCompact"
        };

        return validEvents.Contains(eventName);
    }

    /// <summary>
    /// Expands path markers (~/,  $HOME, %APPDATA%, etc.) to actual paths.
    /// </summary>
    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/") || path.StartsWith("$HOME/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return path.Replace("~/", home + "/").Replace("$HOME/", home + "/");
        }

        if (path.StartsWith("%APPDATA%/"))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return path.Replace("%APPDATA%/", appData + "/");
        }

        if (path.StartsWith("%USERPROFILE%/"))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return path.Replace("%USERPROFILE%/", userProfile + "/");
        }

        return path;
    }
}
