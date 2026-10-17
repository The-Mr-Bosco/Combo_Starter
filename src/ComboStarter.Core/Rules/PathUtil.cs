namespace ComboStarter.Core.Rules;

public static class PathUtil
{
    public static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public static string Key(string path)
    {
        var normalized = Normalize(path);
        return OperatingSystem.IsWindows() ? normalized.ToLowerInvariant() : normalized;
    }

    public static bool AreSame(string? first, string? second)
    {
        if (first is null || second is null)
        {
            return false;
        }

        try
        {
            return string.Equals(Key(first), Key(second), StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static string ToForwardSlashes(string path)
    {
        return path.Replace('\\', '/');
    }
}
