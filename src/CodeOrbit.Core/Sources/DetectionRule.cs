using System.Text.RegularExpressions;

namespace CodeOrbit.Core.Sources;

/// <summary>
/// Detection rule for a plugin-defined CLI source.
/// </summary>
public sealed record DetectionRule(
    string SourceKey,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyDictionary<string, string> EnvVarHints,
    IReadOnlyList<string> PathPatterns,
    int Priority)
{
    /// <summary>
    /// Tests if this detection rule matches the given process ancestry.
    /// </summary>
    public bool Matches(IReadOnlyList<ProcessInfo> ancestry)
    {
        foreach (var process in ancestry)
        {
            // Check process name match
            var processName = Path.GetFileNameWithoutExtension(process.Name);
            if (ProcessNames.Any(pn => string.Equals(pn, processName, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Check path pattern match
            if (PathPatterns.Any(pattern => MatchesGlobPattern(process.ExecutablePath, pattern)))
                return true;
        }

        // Check environment variables (if any hints provided)
        if (EnvVarHints.Count > 0 && MatchesEnvVars())
            return true;

        return false;
    }

    private bool MatchesEnvVars()
    {
        foreach (var (varName, pattern) in EnvVarHints)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (value == null)
                continue;

            if (MatchesPattern(value, pattern))
                return true;
        }
        return false;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        // Simple glob to regex conversion
        if (pattern == "*")
            return !string.IsNullOrEmpty(value);

        // Try as regex first (if contains regex special chars)
        if (pattern.Contains("^") || pattern.Contains("$") || pattern.Contains("\\"))
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                return regex.IsMatch(value);
            }
            catch
            {
                return false;
            }
        }

        // Fall back to simple glob
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        try
        {
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            return regex.IsMatch(value);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesGlobPattern(string? path, string pattern)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Normalize path separators
        path = path.Replace('\\', '/').ToLowerInvariant();
        pattern = pattern.Replace('\\', '/').ToLowerInvariant();

        // Convert glob to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*/", "(.*/)?")  // ** matches any directory depth
            .Replace("\\*", "[^/]*")       // * matches within directory
            .Replace("\\?", ".")           // ? matches single char
            + "$";

        try
        {
            var regex = new Regex(regexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return regex.IsMatch(path);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Process information for detection matching.
/// Used by DetectionRule to check process ancestry.
/// </summary>
public sealed record ProcessInfo(
    int Pid,
    int ParentPid,
    string Name,
    string? ExecutablePath,
    DateTime StartedAtUtc);
