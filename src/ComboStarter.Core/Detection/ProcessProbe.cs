using System.ComponentModel;
using System.Diagnostics;
using ComboStarter.Core.Rules;

namespace ComboStarter.Core.Detection;

public sealed class ProcessProbe
{
    private readonly Lazy<IReadOnlyList<ProcessSnapshot>> _processes;

    public ProcessProbe()
    {
        _processes = new Lazy<IReadOnlyList<ProcessSnapshot>>(Capture);
    }

    public bool IsExecutableRunning(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        string key;
        try
        {
            key = PathUtil.Key(executablePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return _processes.Value.Any(process =>
            process.Key is not null && string.Equals(process.Key, key, StringComparison.Ordinal));
    }

    public bool IsProcessAlive(int pid, DateTime? expectedStartUtc)
    {
        var snapshot = _processes.Value.FirstOrDefault(process => process.Id == pid);
        if (snapshot is null)
        {
            return false;
        }

        if (expectedStartUtc is null || snapshot.StartUtc is null)
        {
            return true;
        }

        return Math.Abs((snapshot.StartUtc.Value - expectedStartUtc.Value).TotalSeconds) < 2;
    }

    private static IReadOnlyList<ProcessSnapshot> Capture()
    {
        var snapshots = new List<ProcessSnapshot>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string? executablePath = null;
                DateTime? startUtc = null;

                try
                {
                    executablePath = process.MainModule?.FileName;
                }
                catch (Exception ex) when (IsProbeFailure(ex))
                {
                }

                try
                {
                    startUtc = process.StartTime.ToUniversalTime();
                }
                catch (Exception ex) when (IsProbeFailure(ex))
                {
                }

                string? key = null;
                if (executablePath is not null)
                {
                    try
                    {
                        key = PathUtil.Key(executablePath);
                    }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                    }
                }

                snapshots.Add(new ProcessSnapshot(process.Id, process.ProcessName, executablePath, startUtc, key));
            }
            catch (Exception ex) when (IsProbeFailure(ex))
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return snapshots;
    }

    private static bool IsProbeFailure(Exception exception)
    {
        return exception is Win32Exception
            or InvalidOperationException
            or NotSupportedException
            or UnauthorizedAccessException
            or IOException;
    }

    private sealed record ProcessSnapshot(
        int Id,
        string Name,
        string? ExecutablePath,
        DateTime? StartUtc,
        string? Key);
}
