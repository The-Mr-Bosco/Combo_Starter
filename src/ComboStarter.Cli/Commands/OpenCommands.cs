using ComboStarter.Core;
using ComboStarter.Core.Config;
using ComboStarter.Core.Detection;
using ComboStarter.Core.Launch;
using ComboStarter.Core.Platform;

namespace ComboStarter.Cli;

internal static class OpenCommands
{
    private static readonly HashSet<string> OpenFlags = new(StringComparer.Ordinal)
    {
        "--force",
        "--dry-run",
        "--verbose",
    };

    private static readonly HashSet<string> ResolveFlags = new(StringComparer.Ordinal)
    {
        "--json",
    };

    public static int Open(IReadOnlyList<string> args, string? configPath)
    {
        if (!FlagParser.TryParse(args, OpenFlags, out var positionals, out var flags, out var error))
        {
            return CommandLine.UsageError(error!);
        }

        if (positionals.Count != 1)
        {
            return CommandLine.UsageError("'open' requires exactly one path.");
        }

        return Execute(
            positionals[0],
            configPath,
            force: flags.Contains("--force"),
            dryRun: flags.Contains("--dry-run"),
            verbose: flags.Contains("--verbose"),
            json: false);
    }

    public static int Resolve(IReadOnlyList<string> args, string? configPath)
    {
        if (!FlagParser.TryParse(args, ResolveFlags, out var positionals, out var flags, out var error))
        {
            return CommandLine.UsageError(error!);
        }

        if (positionals.Count != 1)
        {
            return CommandLine.UsageError("'resolve' requires exactly one path.");
        }

        return Execute(
            positionals[0],
            configPath,
            force: false,
            dryRun: true,
            verbose: false,
            json: flags.Contains("--json"));
    }

    private static int Execute(
        string path,
        string? configPath,
        bool force,
        bool dryRun,
        bool verbose,
        bool json)
    {
        try
        {
            var loaded = ConfigLoader.Load(configPath);
            var associations = AssociationManagerFactory.Create();
            var stateStore = new LaunchStateStore();
            var service = new OpenService(
                loaded,
                associations,
                new ProcessProbe(),
                stateStore,
                Environment.ProcessPath);
            var report = service.Run(path, force, dryRun);

            if (json)
            {
                PrintJson(report);
            }
            else
            {
                PrintReport(report, loaded, stateStore, verbose);
            }

            return ExitCodeFor(report);
        }
        catch (ConfigException ex)
        {
            ConsoleOutput.Error(ex.Message);
            return ExitCodes.Usage;
        }
        catch (FileNotFoundException ex)
        {
            ConsoleOutput.Error(ex.Message);
            return ExitCodes.Failure;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or ArgumentException)
        {
            ConsoleOutput.Error(ex.Message);
            return ExitCodes.Failure;
        }
    }

    private static void PrintReport(
        OpenReport report,
        LoadedConfig loaded,
        LaunchStateStore stateStore,
        bool verbose)
    {
        ConsoleOutput.Header($"ComboStarter {VersionInfo.Version}");
        ConsoleOutput.Info($"Trigger: {report.Plan.TriggerPath}");

        if (verbose)
        {
            ConsoleOutput.Detail($"Config: {loaded.Path}");
            ConsoleOutput.Detail($"State:  {stateStore.Path}");
        }

        if (report.Plan.MatchedRules.Count == 0)
        {
            ConsoleOutput.Detail("Rules:   none (only the trigger is opened)");
        }
        else
        {
            ConsoleOutput.Detail("Rules:");
            foreach (var match in report.Plan.MatchedRules)
            {
                ConsoleOutput.Detail($"  - {match.Rule.Name} ({match.Reason})");
            }
        }

        ConsoleOutput.Detail(report.DryRun ? "Plan (dry run):" : "Plan:");
        if (report.Results.Count == 0)
        {
            ConsoleOutput.Detail("  (none)");
        }

        foreach (var result in report.Results)
        {
            var label = result.Status switch
            {
                ItemStatus.Open => "[open]",
                ItemStatus.Skip => "[skip]",
                ItemStatus.Preview => "[preview]",
                _ => "[error]",
            };
            var color = result.Status switch
            {
                ItemStatus.Open => ConsoleColor.Green,
                ItemStatus.Skip => ConsoleColor.DarkGray,
                ItemStatus.Preview => ConsoleColor.Cyan,
                _ => ConsoleColor.Red,
            };
            ConsoleOutput.Status(label, color, Describe(result));
        }

        foreach (var warning in report.Warnings)
        {
            ConsoleOutput.Warning(warning);
        }
    }

    private static string Describe(OpenItemResult result)
    {
        var parts = new List<string> { KindLabel(result.Item.Kind) };
        if (result.Opener is not null)
        {
            parts.Add($"opener: {result.Opener}");
        }

        if (result.Reason is not null)
        {
            parts.Add(result.Reason);
        }

        if (result.Error is not null)
        {
            parts.Add(result.Error);
        }

        return $"{result.Item.Display}  ({string.Join(", ", parts)})";
    }

    private static string KindLabel(ItemKind kind)
    {
        return kind switch
        {
            ItemKind.Document => "document",
            ItemKind.Executable => "executable",
            ItemKind.Directory => "folder",
            _ => "program",
        };
    }

    private static void PrintJson(OpenReport report)
    {
        JsonOutput.Write(new
        {
            version = VersionInfo.Version,
            trigger = report.Plan.TriggerPath,
            dryRun = report.DryRun,
            rules = report.Plan.MatchedRules.Select(match => new
            {
                name = match.Rule.Name,
                reason = match.Reason,
            }),
            items = report.Results.Select(result => new
            {
                action = result.Status switch
                {
                    ItemStatus.Open => "open",
                    ItemStatus.Skip => "skip",
                    ItemStatus.Preview => "preview",
                    _ => "error",
                },
                kind = KindLabel(result.Item.Kind),
                path = result.Item.TargetPath,
                program = result.Item.Program,
                opener = result.Opener,
                reason = result.Reason,
                error = result.Error,
                rule = result.Item.Source,
            }),
            warnings = report.Warnings,
        });
    }

    private static int ExitCodeFor(OpenReport report)
    {
        var errors = report.Results.Count(result => result.Status == ItemStatus.Error);
        if (errors == 0)
        {
            return ExitCodes.Success;
        }

        var succeeded = report.Results.Count(result =>
            result.Status is ItemStatus.Open or ItemStatus.Preview);
        return succeeded > 0 ? ExitCodes.PartialFailure : ExitCodes.Failure;
    }
}
