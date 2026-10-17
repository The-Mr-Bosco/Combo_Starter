using System.Text.Json;
using ComboStarter.Core.Config;
using ComboStarter.Core.Rules;

namespace ComboStarter.Core.Detection;

public sealed class LaunchRecord
{
    public string Target { get; set; } = string.Empty;

    public string? Opener { get; set; }

    public int? Pid { get; set; }

    public DateTime? ProcessStartUtc { get; set; }

    public DateTime RecordedAtUtc { get; set; }
}

public sealed class LaunchStateStore
{
    private static readonly TimeSpan MaxRecordAge = TimeSpan.FromDays(30);
    private const int MaxRecords = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly string _lockPath;

    public LaunchStateStore(string? path = null)
    {
        _path = path ?? ConfigPaths.GetStatePath();
        _lockPath = _path + ".lock";
    }

    public string Path => _path;

    public IReadOnlyDictionary<string, LaunchRecord> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, LaunchRecord>(StringComparer.Ordinal);
        }

        try
        {
            var json = File.ReadAllText(_path);
            var file = JsonSerializer.Deserialize<LaunchStateFile>(json, SerializerOptions);
            var entries = file?.Entries ?? new Dictionary<string, LaunchRecord>(StringComparer.Ordinal);
            return new Dictionary<string, LaunchRecord>(entries, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, LaunchRecord>(StringComparer.Ordinal);
        }
    }

    public void Save(IReadOnlyCollection<LaunchRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        using var lockStream = AcquireLock();
        var entries = new Dictionary<string, LaunchRecord>(Load(), StringComparer.Ordinal);

        foreach (var record in records)
        {
            var key = PathUtil.Key(record.Target);
            entries[key] = record;
        }

        Prune(entries);
        WriteAtomically(new LaunchStateFile { Version = 1, Entries = entries });
    }

    private static void Prune(Dictionary<string, LaunchRecord> entries)
    {
        var cutoff = DateTime.UtcNow - MaxRecordAge;
        foreach (var key in entries
                     .Where(pair => pair.Value.RecordedAtUtc < cutoff)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            entries.Remove(key);
        }

        if (entries.Count <= MaxRecords)
        {
            return;
        }

        foreach (var key in entries
                     .OrderByDescending(pair => pair.Value.RecordedAtUtc)
                     .Skip(MaxRecords)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            entries.Remove(key);
        }
    }

    private FileStream AcquireLock()
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }
    }

    private void WriteAtomically(LaunchStateFile file)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _path + ".tmp";
        var json = JsonSerializer.Serialize(file, SerializerOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private sealed class LaunchStateFile
    {
        public int Version { get; set; } = 1;

        public Dictionary<string, LaunchRecord> Entries { get; set; } = new(StringComparer.Ordinal);
    }
}
