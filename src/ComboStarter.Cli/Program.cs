using System.Text;
using ComboStarter.Core.Config;

namespace ComboStarter.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
        }

        try
        {
            return CommandLine.Run(args);
        }
        catch (ConfigException ex)
        {
            ConsoleOutput.Error(ex.Message);
            return ExitCodes.Usage;
        }
        catch (Exception ex)
        {
            ConsoleOutput.Error(ex.Message);
            return ExitCodes.Failure;
        }
    }
}
