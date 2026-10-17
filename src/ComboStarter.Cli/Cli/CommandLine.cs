namespace ComboStarter.Cli;

internal static class CommandLine
{
    public static int Run(string[] args)
    {
        var arguments = args.ToList();
        if (arguments.Count == 0)
        {
            HelpText.Print();
            return ExitCodes.Success;
        }

        if (arguments.Any(argument => argument is "--help" or "-h") || arguments[0] == "help")
        {
            HelpText.Print();
            return ExitCodes.Success;
        }

        if (arguments.Any(argument => argument is "--version" or "-V") || arguments[0] == "version")
        {
            ConsoleOutput.Info($"ComboStarter {VersionInfo.Version}");
            return ExitCodes.Success;
        }

        string? configPath = null;
        var filtered = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == "--config")
            {
                if (index + 1 >= arguments.Count)
                {
                    return UsageError("--config requires a file path.");
                }

                configPath = arguments[index + 1];
                index++;
                continue;
            }

            filtered.Add(arguments[index]);
        }

        if (filtered.Count == 0)
        {
            HelpText.Print();
            return ExitCodes.Success;
        }

        var command = filtered[0];
        var rest = filtered.Skip(1).ToList();

        return command switch
        {
            "open" => OpenCommands.Open(rest, configPath),
            "resolve" => OpenCommands.Resolve(rest, configPath),
            "config" => ConfigCommands.Run(rest, configPath),
            "register" => AssociationCommands.Register(rest),
            "unregister" => AssociationCommands.Unregister(rest),
            "status" => AssociationCommands.Status(rest, configPath),
            _ => OpenCommands.Open(filtered, configPath),
        };
    }

    public static int UsageError(string message)
    {
        ConsoleOutput.Error(message);
        ConsoleOutput.Detail("Run 'combo --help' for usage.");
        return ExitCodes.Usage;
    }
}
