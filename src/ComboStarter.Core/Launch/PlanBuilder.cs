using ComboStarter.Core.Config;
using ComboStarter.Core.Models;
using ComboStarter.Core.Rules;

namespace ComboStarter.Core.Launch;

public static class PlanBuilder
{
    public static OpenPlan Build(LoadedConfig loaded, string triggerPath)
    {
        var fullTrigger = PathUtil.Normalize(triggerPath);
        if (!File.Exists(fullTrigger) && !Directory.Exists(fullTrigger))
        {
            throw new FileNotFoundException($"Path not found: {fullTrigger}", fullTrigger);
        }

        var plan = new OpenPlan { TriggerPath = fullTrigger };
        var matches = RuleMatcher.Match(loaded.Config, fullTrigger);
        plan.MatchedRules.AddRange(matches);

        var seen = new Dictionary<string, PlannedItem>(StringComparer.Ordinal);
        var openSelf = matches.Count == 0 || matches.Any(match => match.Rule.OpenSelf);

        if (openSelf)
        {
            var opener = matches
                .Select(match => match.Rule.Opener)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var self = CreatePathItem(fullTrigger, opener, Array.Empty<string>(), "trigger", required: true);
            if (self is not null)
            {
                AddUnique(plan, seen, self);
            }
        }

        foreach (var match in matches)
        {
            foreach (var target in match.Rule.Open)
            {
                AddTarget(plan, seen, loaded, fullTrigger, target, match.Rule.Name ?? "rule");
            }
        }

        if (plan.Items.Count == 0)
        {
            plan.Warnings.Add("Nothing to open: no rule matched and openSelf is false.");
        }

        return plan;
    }

    private static void AddTarget(
        OpenPlan plan,
        Dictionary<string, PlannedItem> seen,
        LoadedConfig loaded,
        string triggerPath,
        TargetDefinition target,
        string source)
    {
        try
        {
            var args = target.Args
                .Select(argument => PathExpander.Expand(argument, triggerPath))
                .ToList();

            if (target.Program is not null)
            {
                var program = PathExpander.ExpandProgram(target.Program, triggerPath, loaded.Directory);
                plan.Items.Add(new PlannedItem
                {
                    Display = program,
                    Kind = ItemKind.Program,
                    Program = program,
                    Args = args,
                    Source = source,
                });
                return;
            }

            var path = PathExpander.ExpandPath(target.Path!, triggerPath, loaded.Directory);
            var openWith = target.OpenWith is null
                ? null
                : PathExpander.ExpandProgram(target.OpenWith, triggerPath, loaded.Directory);

            var item = CreatePathItem(path, openWith, args, source, target.Required);
            if (item is not null)
            {
                AddUnique(plan, seen, item);
                return;
            }

            var message = $"Target not found: {path}";
            plan.Warnings.Add(target.Required ? $"Required target not found: {path}" : $"Target not found, skipped: {path}");
            if (target.Required)
            {
                plan.Items.Add(new PlannedItem
                {
                    Display = path,
                    Kind = ItemKind.Document,
                    TargetPath = path,
                    OpenWith = openWith,
                    Args = args,
                    Source = source,
                    Required = true,
                    Error = message,
                });
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            var message = $"Invalid target in rule '{source}': {ex.Message}";
            plan.Warnings.Add(message);
            if (target.Required)
            {
                plan.Items.Add(new PlannedItem
                {
                    Display = target.Path ?? target.Program ?? "?",
                    Kind = ItemKind.Document,
                    Error = message,
                    Source = source,
                    Required = true,
                });
            }
        }
    }

    private static PlannedItem? CreatePathItem(
        string path,
        string? openWith,
        IReadOnlyList<string> args,
        string source,
        bool required)
    {
        if (Directory.Exists(path))
        {
            return new PlannedItem
            {
                Display = path,
                Kind = ItemKind.Directory,
                TargetPath = path,
                OpenWith = openWith,
                Args = args,
                Source = source,
                Required = required,
            };
        }

        if (!File.Exists(path))
        {
            return null;
        }

        return new PlannedItem
        {
            Display = path,
            Kind = IsExecutableFile(path) ? ItemKind.Executable : ItemKind.Document,
            TargetPath = path,
            OpenWith = openWith,
            Args = args,
            Source = source,
            Required = required,
        };
    }

    private static bool IsExecutableFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".com", StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode executeBits =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (mode & executeBits) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static void AddUnique(OpenPlan plan, Dictionary<string, PlannedItem> seen, PlannedItem item)
    {
        if (item.TargetPath is null)
        {
            plan.Items.Add(item);
            return;
        }

        var key = PathUtil.Key(item.TargetPath);
        if (seen.TryAdd(key, item))
        {
            plan.Items.Add(item);
        }
    }
}
