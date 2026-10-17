using System.Text.Json.Serialization;

namespace ComboStarter.Core.Models;

public sealed class AppConfig
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public int Version { get; set; } = 1;

    public Dictionary<string, string> Openers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DetectionOptions Detection { get; set; } = new();

    public List<RuleDefinition> Rules { get; set; } = new();
}

public sealed class DetectionOptions
{
    public bool Enabled { get; set; } = true;

    public int MaxAgeMinutes { get; set; } = 720;
}

public sealed class RuleDefinition
{
    public string? Name { get; set; }

    public bool Enabled { get; set; } = true;

    public MatchSpec Match { get; set; } = new();

    public string? Opener { get; set; }

    public bool OpenSelf { get; set; } = true;

    public List<TargetDefinition> Open { get; set; } = new();
}

public sealed class MatchSpec
{
    public List<string> Extensions { get; set; } = new();

    public List<string> FileNames { get; set; } = new();

    public List<string> PathGlobs { get; set; } = new();
}

public sealed class TargetDefinition
{
    public string? Path { get; set; }

    public string? Program { get; set; }

    public string? OpenWith { get; set; }

    public List<string> Args { get; set; } = new();

    public bool Required { get; set; }
}
