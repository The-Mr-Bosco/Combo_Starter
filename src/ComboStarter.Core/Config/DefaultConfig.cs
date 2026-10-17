namespace ComboStarter.Core.Config;

public static class DefaultConfig
{
    private const string ResourceName = "ComboStarter.Core.config.example.json";

    public static string Json { get; } = ReadResource();

    private static string ReadResource()
    {
        using var stream = typeof(DefaultConfig).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return content.EndsWith('\n') ? content : content + Environment.NewLine;
    }
}
