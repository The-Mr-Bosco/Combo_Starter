using System.Reflection;

namespace ComboStarter.Cli;

internal static class VersionInfo
{
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(VersionInfo).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadataSeparator = informational.IndexOf('+');
            return metadataSeparator > 0 ? informational[..metadataSeparator] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
