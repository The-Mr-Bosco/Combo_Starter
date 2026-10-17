using ComboStarter.Core.Rules;

namespace ComboStarter.Core.Launch;

public enum ItemKind
{
    Document,
    Executable,
    Directory,
    Program,
}

public sealed class PlannedItem
{
    public required string Display { get; init; }

    public required ItemKind Kind { get; init; }

    public string? TargetPath { get; init; }

    public string? Program { get; init; }

    public string? OpenWith { get; init; }

    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();

    public string Source { get; init; } = "trigger";

    public bool Required { get; init; }

    public string? Error { get; init; }
}

public sealed class OpenPlan
{
    public required string TriggerPath { get; init; }

    public List<RuleMatch> MatchedRules { get; } = new();

    public List<PlannedItem> Items { get; } = new();

    public List<string> Warnings { get; } = new();
}
