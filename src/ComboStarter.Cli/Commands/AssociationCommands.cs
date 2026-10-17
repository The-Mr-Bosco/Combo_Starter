using ComboStarter.Core.Config;
using ComboStarter.Core.Detection;
using ComboStarter.Core.Platform;
using ComboStarter.Core.Rules;

namespace ComboStarter.Cli;

internal static class AssociationCommands
{
    private static readonly HashSet<string> StatusFlags = new(StringComparer.Ordinal) { "--json" };
    private static readonly HashSet<string> NoFlags = new(StringComparer.Ordinal);

    public static int Register(IReadOnlyList<string> args)
    {
        string? opener = null;
        var positionals = new List<string>();

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--opener")
            {
                if (index + 1 >= args.Count)
                {
                    return CommandLine.UsageError("--opener requires a program.");
                }

                opener = args[++index];
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                return CommandLine.UsageError($"Unknown option '{argument}'.");
            }

            if (argument.StartsWith('-') && argument.Length > 1)
            {
                return CommandLine.UsageError($"Unknown option '{argument}'.");
            }

            positionals.Add(argument);
        }

        if (positionals.Count != 1)
        {
            return CommandLine.UsageError("'register' requires exactly one extension, for example: combo register .md");
        }

        string extension;
        try
        {
            extension = ExtensionUtil.Normalize(positionals[0]);
        }
        catch (ArgumentException ex)
        {
            return CommandLine.UsageError(ex.Message);
        }

        var associations = AssociationManagerFactory.Create();
        if (!associations.IsSupported)
        {
            ConsoleOutput.Error("File association registration is currently implemented for Windows only.");
            return ExitCodes.Failure;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)
            || string.Equals(Path.GetFileNameWithoutExtension(executablePath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleOutput.Error(
                "Cannot register ComboStarter while it is hosted by 'dotnet'. Run the built combo.exe or a published build instead.");
            return ExitCodes.Failure;
        }

        try
        {
            var outcome = associations.Register(extension, executablePath, opener);
            if (!outcome.Success)
            {
                ConsoleOutput.Error(outcome.Message);
                return ExitCodes.Failure;
            }

            ConsoleOutput.Success($"Registered {extension} -> {executablePath}");
            ConsoleOutput.Info($"  {outcome.Message}");
            if (outcome.SavedOpener is not null)
            {
                ConsoleOutput.Info($"  Saved opener: {outcome.SavedOpener}");
            }

            if (outcome.Warning is not null)
            {
                ConsoleOutput.Warning(outcome.Warning);
            }

            ConsoleOutput.Detail($"  Undo with: combo unregister {extension}");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or IOException)
        {
            ConsoleOutput.Error($"Failed to write registry: {ex.Message}");
            return ExitCodes.Failure;
        }
    }

    public static int Unregister(IReadOnlyList<string> args)
    {
        if (!FlagParser.TryParse(args, NoFlags, out var positionals, out _, out var error))
        {
            return CommandLine.UsageError(error!);
        }

        if (positionals.Count != 1)
        {
            return CommandLine.UsageError("'unregister' requires exactly one extension.");
        }

        string extension;
        try
        {
            extension = ExtensionUtil.Normalize(positionals[0]);
        }
        catch (ArgumentException ex)
        {
            return CommandLine.UsageError(ex.Message);
        }

        var associations = AssociationManagerFactory.Create();
        if (!associations.IsSupported)
        {
            ConsoleOutput.Error("File association registration is currently implemented for Windows only.");
            return ExitCodes.Failure;
        }

        try
        {
            associations.Unregister(extension);
            ConsoleOutput.Success($"Unregistered {extension}");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or IOException)
        {
            ConsoleOutput.Error($"Failed to update registry: {ex.Message}");
            return ExitCodes.Failure;
        }
    }

    public static int Status(IReadOnlyList<string> args, string? configPath)
    {
        if (!FlagParser.TryParse(args, StatusFlags, out var positionals, out var flags, out var error))
        {
            return CommandLine.UsageError(error!);
        }

        if (positionals.Count != 0)
        {
            return CommandLine.UsageError("'status' does not take positional arguments.");
        }

        var resolvedConfigPath = ConfigPaths.GetConfigPath(configPath);
        LoadedConfig? loaded = null;
        string? configError = null;

        try
        {
            loaded = ConfigLoader.Load(configPath);
        }
        catch (ConfigException ex)
        {
            configError = ex.Message;
        }

        var associations = AssociationManagerFactory.Create();
        IReadOnlyList<RegisteredAssociation> registrations;
        try
        {
            registrations = associations.List();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            registrations = Array.Empty<RegisteredAssociation>();
        }

        var stateStore = new LaunchStateStore();
        var entries = stateStore.Load();
        var probe = new ProcessProbe();
        var running = entries.Count(pair =>
            pair.Value.Pid is { } pid && probe.IsProcessAlive(pid, pair.Value.ProcessStartUtc));

        if (flags.Contains("--json"))
        {
            JsonOutput.Write(new
            {
                version = VersionInfo.Version,
                config = new
                {
                    path = resolvedConfigPath,
                    valid = loaded is not null,
                    error = configError,
                    rules = loaded?.Config.Rules.Count ?? 0,
                    openers = loaded?.Config.Openers.Count ?? 0,
                    detectionEnabled = loaded?.Config.Detection.Enabled,
                    maxAgeMinutes = loaded?.Config.Detection.MaxAgeMinutes,
                },
                state = new
                {
                    path = stateStore.Path,
                    tracked = entries.Count,
                    running,
                },
                associationsSupported = associations.IsSupported,
                associations = registrations.Select(item => new
                {
                    extension = item.Extension,
                    progId = item.ProgId,
                    command = item.Command,
                    savedOpener = item.SavedOpener,
                    userChoiceProgId = item.UserChoiceProgId,
                    userChoiceExecutable = item.UserChoiceExecutable,
                    setAsDefault = item.SetAsDefault,
                }),
            });
            return ExitCodes.Success;
        }

        ConsoleOutput.Header($"ComboStarter {VersionInfo.Version}");
        ConsoleOutput.Info($"Config: {resolvedConfigPath}");
        if (loaded is not null)
        {
            ConsoleOutput.Detail(
                $"  valid, {loaded.Config.Rules.Count} rule(s), {loaded.Config.Openers.Count} opener(s)");
        }
        else
        {
            ConsoleOutput.Warning(configError ?? "config is not available");
        }

        ConsoleOutput.Info($"State:  {stateStore.Path}");
        ConsoleOutput.Detail($"  {entries.Count} tracked, {running} running");

        ConsoleOutput.Info($"Associations: {(associations.IsSupported ? "supported" : "not supported on this platform")}");
        if (registrations.Count == 0)
        {
            ConsoleOutput.Detail("  (none)");
        }

        foreach (var item in registrations)
        {
            var parts = new List<string> { $"progid {item.ProgId}" };
            if (item.SavedOpener is not null)
            {
                parts.Add($"opener {item.SavedOpener}");
            }

            if (item.UserChoiceProgId is not null)
            {
                parts.Add($"user choice {item.UserChoiceProgId}");
            }

            if (item.SetAsDefault)
            {
                parts.Add("default");
            }

            ConsoleOutput.Status(
                "[registered]",
                ConsoleColor.Green,
                $"{item.Extension}  ({string.Join(", ", parts)})");
        }

        return ExitCodes.Success;
    }
}
