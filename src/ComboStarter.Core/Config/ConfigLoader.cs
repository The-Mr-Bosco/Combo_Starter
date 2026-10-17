using System.Text.Json;
using System.Text.Json.Serialization;
using ComboStarter.Core.Models;
using ComboStarter.Core.Rules;

namespace ComboStarter.Core.Config;

public sealed class LoadedConfig
{
    public required AppConfig Config { get; init; }

    public required string Path { get; init; }

    public required string Directory { get; init; }
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static LoadedConfig Load(string? explicitPath = null)
    {
        var path = ConfigPaths.GetConfigPath(explicitPath);
        if (!File.Exists(path))
        {
            throw new ConfigException(
                $"Config file not found: {path}{Environment.NewLine}Run 'combo config init' to create one.");
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException($"Cannot read config file '{path}': {ex.Message}", ex);
        }

        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(text, SerializerOptions);
        }
        catch (JsonException ex)
        {
            var location = ex.LineNumber is { } line
                ? $" (line {line + 1}, position {ex.BytePositionInLine + 1})"
                : string.Empty;
            throw new ConfigException($"Invalid config file '{path}'{location}: {ex.Message}", ex);
        }

        if (config is null)
        {
            throw new ConfigException($"Config file '{path}' is empty.");
        }

        Normalize(config, path);

        var directory = Path.GetDirectoryName(path);
        return new LoadedConfig
        {
            Config = config,
            Path = path,
            Directory = string.IsNullOrEmpty(directory) ? System.IO.Directory.GetCurrentDirectory() : directory,
        };
    }

    public static void Normalize(AppConfig config, string source)
    {
        if (config.Version != 1)
        {
            throw new ConfigException(
                $"Unsupported config version {config.Version} in '{source}'. This build supports version 1.");
        }

        config.Detection ??= new DetectionOptions();
        if (config.Detection.MaxAgeMinutes < 0)
        {
            throw new ConfigException($"detection.maxAgeMinutes must be 0 or greater in '{source}'.");
        }

        var openers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (extension, program) in config.Openers ?? new Dictionary<string, string>())
        {
            string normalizedExtension;
            try
            {
                normalizedExtension = ExtensionUtil.Normalize(extension);
            }
            catch (ArgumentException ex)
            {
                throw new ConfigException($"openers: {ex.Message}", ex);
            }

            if (string.IsNullOrWhiteSpace(program))
            {
                throw new ConfigException($"openers[\"{extension}\"]: program cannot be empty.");
            }

            openers[normalizedExtension] = program.Trim();
        }

        config.Openers = openers;

        if (config.Rules is null)
        {
            config.Rules = new List<RuleDefinition>();
        }

        for (var index = 0; index < config.Rules.Count; index++)
        {
            var rule = config.Rules[index]
                ?? throw new ConfigException($"rules[{index}] cannot be null.");
            var location = $"rules[{index}]";

            rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? $"Rule {index + 1}" : rule.Name.Trim();
            rule.Match ??= new MatchSpec();
            rule.Open ??= new List<TargetDefinition>();
            rule.Match.Extensions ??= new List<string>();
            rule.Match.FileNames ??= new List<string>();
            rule.Match.PathGlobs ??= new List<string>();
            rule.Opener = string.IsNullOrWhiteSpace(rule.Opener) ? null : rule.Opener.Trim();

            var extensions = new List<string>();
            foreach (var value in rule.Match.Extensions)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ConfigException($"{location}.match.extensions: extension cannot be empty.");
                }

                try
                {
                    extensions.Add(ExtensionUtil.Normalize(value));
                }
                catch (ArgumentException ex)
                {
                    throw new ConfigException($"{location}.match.extensions: {ex.Message}", ex);
                }
            }

            rule.Match.Extensions = extensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var fileNames = new List<string>();
            foreach (var value in rule.Match.FileNames)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ConfigException($"{location}.match.fileNames: pattern cannot be empty.");
                }

                fileNames.Add(value.Trim());
            }

            rule.Match.FileNames = fileNames;

            var pathGlobs = new List<string>();
            foreach (var value in rule.Match.PathGlobs)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ConfigException($"{location}.match.pathGlobs: pattern cannot be empty.");
                }

                pathGlobs.Add(value.Trim().Replace('\\', '/'));
            }

            rule.Match.PathGlobs = pathGlobs;

            if (rule.Match.Extensions.Count == 0
                && rule.Match.FileNames.Count == 0
                && rule.Match.PathGlobs.Count == 0)
            {
                throw new ConfigException(
                    $"{location} ('{rule.Name}'): at least one match criterion (extensions, fileNames, pathGlobs) is required.");
            }

            for (var targetIndex = 0; targetIndex < rule.Open.Count; targetIndex++)
            {
                var target = rule.Open[targetIndex]
                    ?? throw new ConfigException($"{location}.open[{targetIndex}] cannot be null.");
                var targetLocation = $"{location}.open[{targetIndex}]";

                var hasPath = !string.IsNullOrWhiteSpace(target.Path);
                var hasProgram = !string.IsNullOrWhiteSpace(target.Program);
                if (hasPath == hasProgram)
                {
                    throw new ConfigException(
                        $"{targetLocation}: specify exactly one of \"path\" or \"program\".");
                }

                target.Path = hasPath ? target.Path!.Trim() : null;
                target.Program = hasProgram ? target.Program!.Trim() : null;
                target.OpenWith = string.IsNullOrWhiteSpace(target.OpenWith) ? null : target.OpenWith.Trim();

                if (target.OpenWith is not null && target.Path is null)
                {
                    throw new ConfigException($"{targetLocation}: \"openWith\" requires \"path\".");
                }

                target.Args ??= new List<string>();
                target.Args = target.Args.Select(argument => argument ?? string.Empty).ToList();
            }
        }
    }
}
