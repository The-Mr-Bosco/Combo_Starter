using ComboStarter.Core.Config;

namespace ComboStarter.Cli;

internal static class ConfigCommands
{
    private static readonly HashSet<string> InitFlags = new(StringComparer.Ordinal) { "--force" };
    private static readonly HashSet<string> NoFlags = new(StringComparer.Ordinal);

    public static int Run(IReadOnlyList<string> args, string? configPath)
    {
        if (args.Count == 0)
        {
            return CommandLine.UsageError("'config' requires a subcommand: init or path.");
        }

        var rest = args.Skip(1).ToList();
        return args[0] switch
        {
            "init" => Init(rest, configPath),
            "path" => PrintPath(rest, configPath),
            _ => CommandLine.UsageError($"Unknown config subcommand '{args[0]}'."),
        };
    }

    private static int Init(IReadOnlyList<string> args, string? configPath)
    {
        if (!FlagParser.TryParse(args, InitFlags, out var positionals, out var flags, out var error))
        {
            return CommandLine.UsageError(error!);
        }

        if (positionals.Count != 0)
        {
            return CommandLine.UsageError("'config init' does not take positional arguments.");
        }

        var path = ConfigPaths.GetConfigPath(configPath);
        if (File.Exists(path) && !flags.Contains("--force"))
        {
            ConsoleOutput.Error($"Config already exists: {path}");
            ConsoleOutput.Detail("Use --force to overwrite it.");
            return ExitCodes.Failure;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, DefaultConfig.Json);
            ConsoleOutput.Success($"Created config: {path}");
            ConsoleOutput.Detail("Edit the rules, then run: combo open <path>");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or ArgumentException)
        {
            ConsoleOutput.Error($"Could not write config: {ex.Message}");
            return ExitCodes.Failure;
        }
    }

    private static int PrintPath(IReadOnlyList<string> args, string? configPath)
    {
        if (!FlagParser.TryParse(args, NoFlags, out var positionals, out _, out var error))
        {
            return CommandLine.UsageError(error!);
        }

        if (positionals.Count != 0)
        {
            return CommandLine.UsageError("'config path' does not take positional arguments.");
        }

        var path = ConfigPaths.GetConfigPath(configPath);
        ConsoleOutput.Info(path);
        ConsoleOutput.Detail(File.Exists(path) ? "Status: exists" : "Status: not found");
        return ExitCodes.Success;
    }
}
