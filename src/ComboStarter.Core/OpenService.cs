using ComboStarter.Core.Config;
using ComboStarter.Core.Detection;
using ComboStarter.Core.Launch;
using ComboStarter.Core.Platform;
using ComboStarter.Core.Rules;

namespace ComboStarter.Core;

public enum ItemStatus
{
    Open,
    Skip,
    Error,
    Preview,
}

public sealed class OpenItemResult
{
    public required PlannedItem Item { get; init; }

    public required ItemStatus Status { get; init; }

    public string? Opener { get; init; }

    public string? Reason { get; init; }

    public string? Error { get; init; }
}

public sealed class OpenReport
{
    public required OpenPlan Plan { get; init; }

    public required bool DryRun { get; init; }

    public List<OpenItemResult> Results { get; } = new();

    public List<string> Warnings { get; } = new();

    public bool HasFailure => Results.Any(result => result.Status == ItemStatus.Error);
}

public sealed class OpenService
{
    private readonly LoadedConfig _loaded;
    private readonly IAssociationManager _associations;
    private readonly ProcessProbe _probe;
    private readonly LaunchStateStore _stateStore;
    private readonly string? _selfPath;

    public OpenService(
        LoadedConfig loaded,
        IAssociationManager associations,
        ProcessProbe probe,
        LaunchStateStore stateStore,
        string? selfPath)
    {
        _loaded = loaded;
        _associations = associations;
        _probe = probe;
        _stateStore = stateStore;
        _selfPath = selfPath;
    }

    public OpenReport Run(string triggerPath, bool force, bool dryRun)
    {
        var plan = PlanBuilder.Build(_loaded, triggerPath);
        var report = new OpenReport { Plan = plan, DryRun = dryRun };
        report.Warnings.AddRange(plan.Warnings);

        var deduplicate = _loaded.Config.Detection.Enabled && !force;
        var state = deduplicate
            ? _stateStore.Load()
            : new Dictionary<string, LaunchRecord>(StringComparer.Ordinal);
        var records = new List<LaunchRecord>();

        foreach (var item in plan.Items)
        {
            if (item.Error is not null)
            {
                report.Results.Add(new OpenItemResult
                {
                    Item = item,
                    Status = ItemStatus.Error,
                    Error = item.Error,
                });
                continue;
            }

            ResolvedTarget resolved;
            try
            {
                resolved = Resolve(item, plan.TriggerPath);
            }
            catch (Exception ex) when (ex is FormatException
                                           or ArgumentException
                                           or NotSupportedException
                                           or PathTooLongException)
            {
                report.Results.Add(new OpenItemResult
                {
                    Item = item,
                    Status = ItemStatus.Error,
                    Error = ex.Message,
                });
                continue;
            }

            if (resolved.Error is not null)
            {
                report.Results.Add(new OpenItemResult
                {
                    Item = item,
                    Status = ItemStatus.Error,
                    Opener = resolved.DisplayOpener,
                    Error = resolved.Error,
                });
                continue;
            }

            if (deduplicate && ShouldSkip(item, state, out var reason))
            {
                report.Results.Add(new OpenItemResult
                {
                    Item = item,
                    Status = ItemStatus.Skip,
                    Opener = resolved.DisplayOpener,
                    Reason = reason,
                });
                continue;
            }

            if (dryRun)
            {
                report.Results.Add(new OpenItemResult
                {
                    Item = item,
                    Status = ItemStatus.Preview,
                    Opener = resolved.DisplayOpener,
                });
                continue;
            }

            var outcome = ProcessLauncher.Launch(resolved.Request);
            if (!outcome.Started)
            {
                report.Results.Add(new OpenItemResult
                {
                    Item = item,
                    Status = ItemStatus.Error,
                    Opener = resolved.DisplayOpener,
                    Error = outcome.Error,
                });
                continue;
            }

            report.Results.Add(new OpenItemResult
            {
                Item = item,
                Status = ItemStatus.Open,
                Opener = resolved.DisplayOpener,
            });

            if (item.Kind == ItemKind.Document && item.TargetPath is not null)
            {
                records.Add(new LaunchRecord
                {
                    Target = item.TargetPath,
                    Opener = resolved.RecordOpener,
                    Pid = outcome.Pid,
                    ProcessStartUtc = outcome.ProcessStartUtc,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        if (!dryRun && records.Count > 0)
        {
            try
            {
                _stateStore.Save(records);
            }
            catch (Exception ex) when (ex is IOException
                                           or UnauthorizedAccessException
                                           or System.Security.SecurityException)
            {
                report.Warnings.Add($"Could not save launch state: {ex.Message}");
            }
        }

        return report;
    }

    private ResolvedTarget Resolve(PlannedItem item, string triggerPath)
    {
        if (item.Kind == ItemKind.Program)
        {
            return ResolveProgram(item, triggerPath, item.Program!, item.Args, appendTarget: false);
        }

        if (item.OpenWith is not null)
        {
            return ResolveProgram(item, triggerPath, item.OpenWith, item.Args, appendTarget: true);
        }

        if (item.Kind == ItemKind.Executable)
        {
            if (IsSelf(item.TargetPath))
            {
                return SelfLaunchError();
            }

            return new ResolvedTarget(
                new LaunchRequest(item.TargetPath!, item.Args, UseShellExecute: false, CreateNoWindow: true),
                item.TargetPath,
                item.TargetPath,
                null);
        }

        if (item.Kind == ItemKind.Directory)
        {
            return new ResolvedTarget(
                new LaunchRequest(item.TargetPath!, Array.Empty<string>(), UseShellExecute: true, CreateNoWindow: false),
                null,
                null,
                null);
        }

        var extension = item.TargetPath is null ? string.Empty : Path.GetExtension(item.TargetPath);
        var configuredOpener = extension.Length > 0
            && _loaded.Config.Openers.TryGetValue(extension, out var openerFromConfig)
                ? openerFromConfig
                : null;
        configuredOpener ??= extension.Length > 0 ? _associations.GetSavedOpener(extension) : null;

        if (configuredOpener is not null)
        {
            return ResolveProgram(item, triggerPath, configuredOpener, item.Args, appendTarget: true);
        }

        string? defaultExecutable = null;
        if (OperatingSystem.IsWindows() && extension.Length > 0)
        {
            defaultExecutable = _associations.QueryDefaultExecutable(extension);
            if (IsSelf(defaultExecutable))
            {
                return new ResolvedTarget(
                    new LaunchRequest(item.TargetPath!, Array.Empty<string>(), UseShellExecute: true, CreateNoWindow: false),
                    defaultExecutable,
                    defaultExecutable,
                    $"ComboStarter is the default handler for '{extension}', but no opener is configured. " +
                    $"Add \"openers\": {{ \"{extension}\": \"<program>\" }} to the config.");
            }
        }

        return new ResolvedTarget(
            new LaunchRequest(item.TargetPath!, Array.Empty<string>(), UseShellExecute: true, CreateNoWindow: false),
            defaultExecutable,
            defaultExecutable,
            null);
    }

    private ResolvedTarget ResolveProgram(
        PlannedItem item,
        string triggerPath,
        string programExpression,
        IReadOnlyList<string> arguments,
        bool appendTarget)
    {
        var expanded = PathExpander.ExpandProgram(programExpression, triggerPath, _loaded.Directory);
        var resolved = ExecutableResolver.Resolve(expanded);

        if (IsSelf(resolved) || IsSelf(expanded))
        {
            return SelfLaunchError();
        }

        var launchArguments = appendTarget && item.TargetPath is not null
            ? arguments.Concat(new[] { item.TargetPath }).ToList()
            : arguments.ToList();

        var useShell = resolved is null || !IsDirectExecutable(resolved);

        return new ResolvedTarget(
            new LaunchRequest(
                resolved ?? expanded,
                launchArguments,
                UseShellExecute: useShell,
                CreateNoWindow: !useShell),
            resolved ?? expanded,
            resolved,
            null);
    }

    private bool ShouldSkip(
        PlannedItem item,
        IReadOnlyDictionary<string, LaunchRecord> state,
        out string reason)
    {
        reason = string.Empty;

        if (item.Kind is ItemKind.Program or ItemKind.Directory || item.TargetPath is null)
        {
            return false;
        }

        if (item.Kind == ItemKind.Executable)
        {
            if (_probe.IsExecutableRunning(item.TargetPath))
            {
                reason = "process already running";
                return true;
            }

            return false;
        }

        var key = PathUtil.Key(item.TargetPath);
        if (!state.TryGetValue(key, out var record))
        {
            return false;
        }

        if (record.Pid is { } pid && _probe.IsProcessAlive(pid, record.ProcessStartUtc))
        {
            reason = $"already open (pid {pid})";
            return true;
        }

        var maxAgeMinutes = _loaded.Config.Detection.MaxAgeMinutes;
        if (maxAgeMinutes > 0
            && record.Opener is not null
            && _probe.IsExecutableRunning(record.Opener)
            && record.RecordedAtUtc >= DateTime.UtcNow.AddMinutes(-maxAgeMinutes))
        {
            reason = "already open (opener still running)";
            return true;
        }

        return false;
    }

    private bool IsSelf(string? path)
    {
        return path is not null && PathUtil.AreSame(path, _selfPath);
    }

    private static ResolvedTarget SelfLaunchError()
    {
        return new ResolvedTarget(
            new LaunchRequest(string.Empty, Array.Empty<string>(), UseShellExecute: false, CreateNoWindow: true),
            null,
            null,
            "Refusing to launch ComboStarter itself. Configure a different opener for this file type.");
    }

    private static bool IsDirectExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".com", StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var mode = File.GetUnixFileMode(path);
            const UnixFileMode executeBits =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (mode & executeBits) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private sealed record ResolvedTarget(
        LaunchRequest Request,
        string? DisplayOpener,
        string? RecordOpener,
        string? Error);
}
