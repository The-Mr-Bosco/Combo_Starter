namespace ComboStarter.Cli;

internal static class HelpText
{
    public static void Print()
    {
        ConsoleOutput.Header($"ComboStarter {VersionInfo.Version}");
        ConsoleOutput.Info("Open a file together with its companion files.");
        ConsoleOutput.Info(string.Empty);
        ConsoleOutput.Info("Usage:");
        ConsoleOutput.Info("  combo open <path> [--force] [--dry-run] [--verbose] [--config <file>]");
        ConsoleOutput.Info("  combo resolve <path> [--json] [--config <file>]");
        ConsoleOutput.Info("  combo config init [--force] [--config <file>]");
        ConsoleOutput.Info("  combo config path [--config <file>]");
        ConsoleOutput.Info("  combo register <ext> [--opener <program>] [--config <file>]");
        ConsoleOutput.Info("  combo unregister <ext> [--config <file>]");
        ConsoleOutput.Info("  combo status [--json] [--config <file>]");
        ConsoleOutput.Info("  combo help | version");
        ConsoleOutput.Info(string.Empty);
        ConsoleOutput.Info("Commands:");
        ConsoleOutput.Info("  open        Open the path and every target from matching rules.");
        ConsoleOutput.Info("  resolve     Show what would open without launching anything.");
        ConsoleOutput.Info("  config      Create or locate the configuration file.");
        ConsoleOutput.Info("  register    Register ComboStarter as a handler for an extension (Windows).");
        ConsoleOutput.Info("  unregister  Remove a handler registered by ComboStarter.");
        ConsoleOutput.Info("  status      Show config, rules and association state.");
        ConsoleOutput.Info(string.Empty);
        ConsoleOutput.Info("Options:");
        ConsoleOutput.Info("  --force     Ignore the already-open detection.");
        ConsoleOutput.Info("  --dry-run   Print the plan without launching anything.");
        ConsoleOutput.Info("  --json      Machine-readable output (resolve, status).");
        ConsoleOutput.Info("  --config    Use a specific configuration file.");
        ConsoleOutput.Info("  --verbose   Print extra details.");
        ConsoleOutput.Info(string.Empty);
        ConsoleOutput.Info("Examples:");
        ConsoleOutput.Info("  combo open C:\\notes\\todo.md");
        ConsoleOutput.Info("  combo notes.md --dry-run");
        ConsoleOutput.Info("  combo config init");
        ConsoleOutput.Info("  combo register .md --opener \"C:\\Program Files\\Microsoft VS Code\\Code.exe\"");
        ConsoleOutput.Info(string.Empty);
        ConsoleOutput.Detail("Config file: " + ComboStarter.Core.Config.ConfigPaths.GetConfigPath());
    }
}
