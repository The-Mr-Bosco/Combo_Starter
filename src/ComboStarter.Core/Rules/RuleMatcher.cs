using System.IO.Enumeration;
using ComboStarter.Core.Models;

namespace ComboStarter.Core.Rules;

public sealed record RuleMatch(RuleDefinition Rule, string Reason);

public static class RuleMatcher
{
    public static IReadOnlyList<RuleMatch> Match(AppConfig config, string fullPath)
    {
        var matches = new List<RuleMatch>();

        foreach (var rule in config.Rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (TryMatch(rule.Match, fullPath, out var reason))
            {
                matches.Add(new RuleMatch(rule, reason));
            }
        }

        return matches;
    }

    private static bool TryMatch(MatchSpec spec, string fullPath, out string reason)
    {
        reason = string.Empty;
        var ignoreCase = OperatingSystem.IsWindows();
        var extension = Path.GetExtension(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (spec.Extensions.Count > 0
            && !spec.Extensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (spec.FileNames.Count > 0
            && !spec.FileNames.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase)))
        {
            return false;
        }

        if (spec.PathGlobs.Count > 0)
        {
            var normalizedPath = PathUtil.ToForwardSlashes(fullPath);
            if (!spec.PathGlobs.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, normalizedPath, ignoreCase)))
            {
                return false;
            }
        }

        reason = spec.Extensions.Count > 0
            ? $"extension {extension}"
            : spec.FileNames.Count > 0
                ? $"file name {fileName}"
                : "path";

        return true;
    }
}
