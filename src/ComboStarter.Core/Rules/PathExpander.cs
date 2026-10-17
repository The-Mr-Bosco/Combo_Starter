using System.Text.RegularExpressions;

namespace ComboStarter.Core.Rules;

public static class PathExpander
{
    private static readonly Regex WindowsVariablePattern = new("%([^%]+)%", RegexOptions.Compiled);
    private static readonly Regex UnixVariablePattern = new(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}|\$([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    public static string Expand(string value, string? triggerPath)
    {
        var expanded = ExpandHomeDirectory(value);
        expanded = ExpandEnvironmentVariables(expanded);
        return ExpandTokens(expanded, triggerPath);
    }

    public static string ExpandPath(string value, string? triggerPath, string baseDirectory)
    {
        var expanded = Expand(value, triggerPath);
        if (string.IsNullOrWhiteSpace(expanded))
        {
            throw new FormatException("Path expands to an empty string.");
        }

        if (!Path.IsPathRooted(expanded))
        {
            expanded = Path.Combine(baseDirectory, expanded);
        }

        return PathUtil.Normalize(expanded);
    }

    public static string ExpandProgram(string value, string? triggerPath, string baseDirectory)
    {
        var expanded = Expand(value, triggerPath);
        if (string.IsNullOrWhiteSpace(expanded))
        {
            throw new FormatException("Program expands to an empty string.");
        }

        if (expanded.Contains('/') || expanded.Contains('\\'))
        {
            if (!Path.IsPathRooted(expanded))
            {
                expanded = Path.Combine(baseDirectory, expanded);
            }

            expanded = PathUtil.Normalize(expanded);
        }

        return expanded;
    }

    private static string ExpandTokens(string value, string? triggerPath)
    {
        if (triggerPath is null || value.IndexOf('{') < 0)
        {
            return value;
        }

        var directory = Path.GetDirectoryName(triggerPath) ?? string.Empty;
        var fileName = Path.GetFileName(triggerPath);
        var stem = Path.GetFileNameWithoutExtension(triggerPath);

        return value
            .Replace("{file}", triggerPath, StringComparison.Ordinal)
            .Replace("{fileDir}", directory, StringComparison.Ordinal)
            .Replace("{fileName}", fileName, StringComparison.Ordinal)
            .Replace("{fileStem}", stem, StringComparison.Ordinal);
    }

    private static string ExpandHomeDirectory(string value)
    {
        if (value == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (value.StartsWith("~/", StringComparison.Ordinal) || value.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                value[2..]);
        }

        return value;
    }

    private static string ExpandEnvironmentVariables(string value)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsVariablePattern.Replace(
                value,
                match => Environment.GetEnvironmentVariable(match.Groups[1].Value) ?? match.Value);
        }

        return UnixVariablePattern.Replace(
            value,
            match =>
            {
                var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                return Environment.GetEnvironmentVariable(name) ?? match.Value;
            });
    }
}
