namespace ComboStarter.Core.Platform;

public sealed class NullAssociationManager : IAssociationManager
{
    public bool IsSupported => false;

    public string? QueryDefaultExecutable(string association) => null;

    public string? GetSavedOpener(string extension) => null;

    public RegisteredAssociation? Get(string extension) => null;

    public IReadOnlyList<RegisteredAssociation> List() => Array.Empty<RegisteredAssociation>();

    public RegisterOutcome Register(string extension, string executablePath, string? opener)
    {
        return new RegisterOutcome(
            false,
            "File association registration is currently implemented for Windows only. Linux support is planned.",
            null,
            false,
            null);
    }

    public void Unregister(string extension)
    {
        throw new PlatformNotSupportedException(
            "File association registration is currently implemented for Windows only.");
    }
}
