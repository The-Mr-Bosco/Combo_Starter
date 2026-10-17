namespace ComboStarter.Core.Config;

public static class ConfigPaths
{
    public const string ConfigEnvironmentVariable = "COMBOSTARTER_CONFIG";

    public const string StateEnvironmentVariable = "COMBOSTARTER_STATE";

    public static string GetConfigPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(ConfigEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var baseDirectory = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : GetXdgDirectory("XDG_CONFIG_HOME", ".config");

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(baseDirectory, "ComboStarter", "config.json");
    }

    public static string GetStatePath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(StateEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Combine(localAppData, "ComboStarter", "state.json");
        }

        var stateDirectory = GetXdgDirectory("XDG_STATE_HOME", ".local/state");
        return Path.Combine(stateDirectory, "ComboStarter", "state.json");
    }

    private static string GetXdgDirectory(string variable, string fallbackRelativePath)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        {
            return configured;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallback = fallbackRelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(home, fallback);
    }
}
