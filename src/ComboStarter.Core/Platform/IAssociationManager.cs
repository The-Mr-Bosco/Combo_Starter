namespace ComboStarter.Core.Platform;

public sealed record RegisteredAssociation(
    string Extension,
    string ProgId,
    string Command,
    string? SavedOpener,
    string? UserChoiceProgId,
    string? UserChoiceExecutable,
    bool SetAsDefault);

public sealed record RegisterOutcome(
    bool Success,
    string Message,
    string? SavedOpener,
    bool SetAsDefault,
    string? Warning);

public interface IAssociationManager
{
    bool IsSupported { get; }

    string? QueryDefaultExecutable(string association);

    string? GetSavedOpener(string extension);

    RegisteredAssociation? Get(string extension);

    IReadOnlyList<RegisteredAssociation> List();

    RegisterOutcome Register(string extension, string executablePath, string? opener);

    void Unregister(string extension);
}
