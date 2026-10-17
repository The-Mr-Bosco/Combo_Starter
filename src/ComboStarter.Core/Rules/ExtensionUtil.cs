namespace ComboStarter.Core.Rules;

public static class ExtensionUtil
{
    private static readonly char[] InvalidCharacters =
    {
        '\\', '/', ':', '*', '?', '"', '<', '>', '|',
    };

    public static string Normalize(string extension)
    {
        var value = extension.Trim();
        if (value.Length == 0)
        {
            throw new ArgumentException("Extension cannot be empty.", nameof(extension));
        }

        if (value[0] != '.')
        {
            value = "." + value;
        }

        if (value.IndexOfAny(InvalidCharacters) >= 0 || value.Any(char.IsControl))
        {
            throw new ArgumentException($"Invalid extension: '{extension}'.", nameof(extension));
        }

        return value.ToLowerInvariant();
    }
}
