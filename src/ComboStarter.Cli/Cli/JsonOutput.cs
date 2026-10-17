using System.Text.Encodings.Web;
using System.Text.Json;

namespace ComboStarter.Cli;

internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(object payload)
    {
        Console.WriteLine(JsonSerializer.Serialize(payload, Options));
    }
}
