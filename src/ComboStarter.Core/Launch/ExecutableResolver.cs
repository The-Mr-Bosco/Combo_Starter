using ComboStarter.Core.Rules;

namespace ComboStarter.Core.Launch;

public static class ExecutableResolver
{
    public static string? Resolve(string? program)
    {
        if (string.IsNullOrWhiteSpace(program))
        {
            return null;
        }

        try
        {
            if (program.Contains('/') || program.Contains('\\'))
            {
                var fullPath = PathUtil.Normalize(program);
                return File.Exists(fullPath) ? fullPath : null;
            }

            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var isWindows = OperatingSystem.IsWindows();
            var hasExtension = Path.GetExtension(program).Length > 0;
            var extensions = isWindows
                ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : new[] { string.Empty };

            foreach (var directory in pathVariable.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var cleanDirectory = directory.Trim('"');
                if (cleanDirectory.Length == 0)
                {
                    continue;
                }

                if (!isWindows || hasExtension)
                {
                    var directCandidate = Path.Combine(cleanDirectory, program);
                    if (File.Exists(directCandidate))
                    {
                        return Path.GetFullPath(directCandidate);
                    }

                    if (isWindows)
                    {
                        continue;
                    }
                }

                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(cleanDirectory, program + extension);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
        }

        return null;
    }
}
