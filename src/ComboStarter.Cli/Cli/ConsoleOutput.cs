namespace ComboStarter.Cli;

internal static class ConsoleOutput
{
    private static readonly bool ColorEnabled =
        !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    public static void Header(string text) => WriteLine(text, ConsoleColor.Cyan);

    public static void Info(string text) => Console.WriteLine(text);

    public static void Detail(string text) => WriteLine(text, ConsoleColor.DarkGray);

    public static void Success(string text) => WriteLine(text, ConsoleColor.Green);

    public static void Warning(string text) => WriteLine($"warning: {text}", ConsoleColor.Yellow);

    public static void Error(string text) => WriteLine($"error: {text}", ConsoleColor.Red);

    public static void Status(string label, ConsoleColor color, string text)
    {
        if (ColorEnabled)
        {
            Console.Write("  ");
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.Write(label.PadRight(10) + " ");
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }
        else
        {
            Console.Write("  " + label.PadRight(10) + " ");
        }

        Console.WriteLine(text);
    }

    private static void WriteLine(string text, ConsoleColor color)
    {
        if (!ColorEnabled)
        {
            Console.WriteLine(text);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }
}
