namespace ComboStarter.Core.Launch;

public sealed record LaunchRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    bool UseShellExecute,
    bool CreateNoWindow);

public sealed record LaunchOutcome(bool Started, int? Pid, DateTime? ProcessStartUtc, string? Error)
{
    public static LaunchOutcome Failure(string error) => new(false, null, null, error);

    public static LaunchOutcome Success(int? pid, DateTime? processStartUtc) => new(true, pid, processStartUtc, null);
}

public static class ProcessLauncher
{
    public static LaunchOutcome Launch(LaunchRequest request)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = request.FileName,
                UseShellExecute = request.UseShellExecute,
            };

            if (request.UseShellExecute)
            {
                startInfo.Arguments = CommandLineQuoting.Join(request.Arguments);
            }
            else
            {
                foreach (var argument in request.Arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                startInfo.CreateNoWindow = request.CreateNoWindow;
            }

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return LaunchOutcome.Success(null, null);
            }

            DateTime? startUtc = null;
            try
            {
                startUtc = process.StartTime.ToUniversalTime();
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                           or InvalidOperationException
                                           or NotSupportedException
                                           or UnauthorizedAccessException)
            {
            }

            return LaunchOutcome.Success(process.Id, startUtc);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                       or InvalidOperationException
                                       or ObjectDisposedException
                                       or FileNotFoundException
                                       or UnauthorizedAccessException)
        {
            return LaunchOutcome.Failure(ex.Message);
        }
    }
}

public static class CommandLineQuoting
{
    public static string Join(IReadOnlyList<string> arguments)
    {
        return string.Join(' ', arguments.Select(Quote));
    }

    public static string Quote(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (!argument.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            return argument;
        }

        var builder = new System.Text.StringBuilder(argument.Length + 2);
        builder.Append('"');
        var backslashes = 0;

        foreach (var character in argument)
        {
            switch (character)
            {
                case '\\':
                    backslashes++;
                    continue;
                case '"':
                    builder.Append('\\', (backslashes * 2) + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                default:
                    builder.Append('\\', backslashes);
                    backslashes = 0;
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
