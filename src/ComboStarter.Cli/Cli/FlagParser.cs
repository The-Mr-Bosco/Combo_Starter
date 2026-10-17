namespace ComboStarter.Cli;

internal static class FlagParser
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        IReadOnlyCollection<string> allowedFlags,
        out List<string> positionals,
        out HashSet<string> flags,
        out string? error)
    {
        positionals = new List<string>();
        flags = new HashSet<string>(StringComparer.Ordinal);
        error = null;

        foreach (var argument in args)
        {
            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (!allowedFlags.Contains(argument))
                {
                    error = $"Unknown option '{argument}'.";
                    return false;
                }

                flags.Add(argument);
                continue;
            }

            if (argument.StartsWith('-') && argument.Length > 1)
            {
                error = $"Unknown option '{argument}'.";
                return false;
            }

            positionals.Add(argument);
        }

        return true;
    }
}
