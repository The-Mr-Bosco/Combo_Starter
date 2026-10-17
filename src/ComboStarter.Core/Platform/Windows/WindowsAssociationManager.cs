using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using ComboStarter.Core.Rules;
using Microsoft.Win32;

namespace ComboStarter.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsAssociationManager : IAssociationManager
{
    private const string ClassesRoot = @"Software\Classes";
    private const string MetadataRoot = @"Software\ComboStarter\Associations";
    private const string FileExtsRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";

    public bool IsSupported => true;

    public string? QueryDefaultExecutable(string association)
    {
        if (string.IsNullOrWhiteSpace(association))
        {
            return null;
        }

        try
        {
            uint length = 0;
            AssocQueryString(0, AssocStr.Executable, association, null, null, ref length);
            if (length == 0)
            {
                return null;
            }

            var builder = new StringBuilder((int)length);
            var result = AssocQueryString(0, AssocStr.Executable, association, null, builder, ref length);
            if (result != 0)
            {
                return null;
            }

            var value = builder.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public string? GetSavedOpener(string extension)
    {
        try
        {
            var normalized = ExtensionUtil.Normalize(extension);
            using var key = Registry.CurrentUser.OpenSubKey($@"{MetadataRoot}\{normalized}");
            return key?.GetValue("Opener") as string;
        }
        catch (Exception ex) when (ex is ArgumentException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException)
        {
            return null;
        }
    }

    public RegisteredAssociation? Get(string extension)
    {
        try
        {
            var normalized = ExtensionUtil.Normalize(extension);
            using var metadata = Registry.CurrentUser.OpenSubKey($@"{MetadataRoot}\{normalized}");
            if (metadata is null)
            {
                return null;
            }

            var progId = metadata.GetValue("ProgId") as string ?? BuildProgId(normalized);
            var command = metadata.GetValue("Command") as string ?? string.Empty;
            var savedOpener = metadata.GetValue("Opener") as string;
            var userChoiceProgId = GetUserChoiceProgId(normalized);
            var extensionDefault = GetUserDefaultProgId(normalized);
            var effectiveProgId = userChoiceProgId ?? extensionDefault;
            var setAsDefault = string.Equals(effectiveProgId, progId, StringComparison.OrdinalIgnoreCase);
            var userChoiceExecutable = userChoiceProgId is null
                ? null
                : QueryDefaultExecutable(userChoiceProgId);

            return new RegisteredAssociation(
                normalized,
                progId,
                command,
                savedOpener,
                userChoiceProgId,
                userChoiceExecutable,
                setAsDefault);
        }
        catch (Exception ex) when (ex is ArgumentException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException)
        {
            return null;
        }
    }

    public IReadOnlyList<RegisteredAssociation> List()
    {
        var results = new List<RegisteredAssociation>();

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(MetadataRoot);
            if (root is null)
            {
                return results;
            }

            foreach (var name in root.GetSubKeyNames())
            {
                var association = Get(name);
                if (association is not null)
                {
                    results.Add(association);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return results;
        }

        return results.OrderBy(item => item.Extension, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public RegisterOutcome Register(string extension, string executablePath, string? opener)
    {
        string normalized;
        try
        {
            normalized = ExtensionUtil.Normalize(extension);
        }
        catch (ArgumentException ex)
        {
            return new RegisterOutcome(false, ex.Message, null, false, null);
        }

        if (!File.Exists(executablePath))
        {
            return new RegisterOutcome(false, $"Executable not found: {executablePath}", null, false, null);
        }

        var executable = Path.GetFullPath(executablePath);
        var progId = BuildProgId(normalized);
        var command = $"\"{executable}\" open \"%1\"";

        try
        {
            var (hadPreviousDefault, previousDefault) = ReadBackup(normalized);
            var userChoiceProgId = GetUserChoiceProgId(normalized);
            var machineDefault = GetMachineDefaultProgId(normalized);
            var existingDefault = GetUserDefaultProgId(normalized);

            var savedOpener = string.IsNullOrWhiteSpace(opener) ? null : opener.Trim();
            savedOpener ??= GetSavedOpener(normalized);

            if (savedOpener is null)
            {
                var detected = QueryDefaultExecutable(normalized);
                if (detected is not null && !PathUtil.AreSame(detected, executable))
                {
                    savedOpener = detected;
                }
            }

            var openerIsSelf = savedOpener is not null && PathUtil.AreSame(savedOpener, executable);
            if (openerIsSelf)
            {
                savedOpener = null;
            }
            else if (savedOpener is not null && File.Exists(savedOpener))
            {
                savedOpener = Path.GetFullPath(savedOpener);
            }

            string? backupValue;
            bool backupHadValue;

            if (existingDefault is not null && string.Equals(existingDefault, progId, StringComparison.OrdinalIgnoreCase))
            {
                backupHadValue = hadPreviousDefault;
                backupValue = previousDefault;
            }
            else if (existingDefault is not null)
            {
                backupHadValue = true;
                backupValue = existingDefault;
            }
            else
            {
                backupHadValue = false;
                backupValue = null;
            }

            using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{normalized}"))
            {
                using (var progIdsKey = extensionKey.CreateSubKey("OpenWithProgids"))
                {
                    progIdsKey.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
                }

                using (var contextMenu = extensionKey.CreateSubKey(@"shell\ComboStarter"))
                {
                    contextMenu.SetValue("MUIVerb", "Open with ComboStarter");
                    contextMenu.SetValue("Icon", $"\"{executable}\"");
                    using var contextCommand = contextMenu.CreateSubKey("command");
                    contextCommand.SetValue(null, command);
                }

                if (userChoiceProgId is null && machineDefault is null)
                {
                    extensionKey.SetValue(null, progId);
                }
            }

            using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{progId}"))
            {
                progIdKey.SetValue(null, $"ComboStarter {normalized} file");
                using var openKey = progIdKey.CreateSubKey(@"shell\open");
                openKey.SetValue("FriendlyAppName", "ComboStarter");
                using var commandKey = openKey.CreateSubKey("command");
                commandKey.SetValue(null, command);
            }

            var setAsDefault = string.Equals(userChoiceProgId, progId, StringComparison.OrdinalIgnoreCase)
                || (userChoiceProgId is null
                    && machineDefault is null
                    && string.Equals(GetUserDefaultProgId(normalized), progId, StringComparison.OrdinalIgnoreCase));

            using (var metadata = Registry.CurrentUser.CreateSubKey($@"{MetadataRoot}\{normalized}"))
            {
                metadata.SetValue("ProgId", progId);
                metadata.SetValue("Command", command);
                metadata.SetValue("SetAsDefault", setAsDefault ? 1 : 0, RegistryValueKind.DWord);
                metadata.SetValue("RegisteredAtUtc", DateTime.UtcNow.ToString("O"));
                metadata.SetValue("HadPreviousDefault", backupHadValue ? 1 : 0, RegistryValueKind.DWord);
                if (backupValue is not null)
                {
                    metadata.SetValue("PreviousDefault", backupValue);
                }

                if (savedOpener is not null)
                {
                    metadata.SetValue("Opener", savedOpener);
                }
            }

            var warning = savedOpener is null
                ? "No opener could be detected. Add an \"openers\" entry for this extension, otherwise opening it would start ComboStarter itself."
                : null;

            var message = setAsDefault
                ? "ComboStarter is now the default handler for this extension."
                : "Association registered, but Windows keeps the current default app. Choose ComboStarter in \"Open with\", or use the right-click menu.";

            return new RegisterOutcome(true, message, savedOpener, setAsDefault, warning);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or IOException)
        {
            return new RegisterOutcome(false, $"Failed to write registry: {ex.Message}", null, false, null);
        }
    }

    public void Unregister(string extension)
    {
        var normalized = ExtensionUtil.Normalize(extension);
        var progId = BuildProgId(normalized);

        var hadPreviousDefault = false;
        string? previousDefault = null;
        var setAsDefault = false;

        using (var metadata = Registry.CurrentUser.OpenSubKey($@"{MetadataRoot}\{normalized}"))
        {
            if (metadata is not null)
            {
                hadPreviousDefault = metadata.GetValue("HadPreviousDefault") is int had && had != 0;
                previousDefault = metadata.GetValue("PreviousDefault") as string;
                setAsDefault = metadata.GetValue("SetAsDefault") is int value && value != 0;
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{progId}", throwOnMissingSubKey: false);

        using (var extensionKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{normalized}", writable: true))
        {
            if (extensionKey is not null)
            {
                using (var progIdsKey = extensionKey.OpenSubKey("OpenWithProgids", writable: true))
                {
                    progIdsKey?.DeleteValue(progId, throwOnMissingValue: false);
                }

                extensionKey.DeleteSubKeyTree(@"shell\ComboStarter", throwOnMissingSubKey: false);

                var currentDefault = extensionKey.GetValue(null) as string;
                if (setAsDefault && string.Equals(currentDefault, progId, StringComparison.OrdinalIgnoreCase))
                {
                    if (hadPreviousDefault && previousDefault is not null)
                    {
                        extensionKey.SetValue(null, previousDefault);
                    }
                    else
                    {
                        extensionKey.DeleteValue(string.Empty, throwOnMissingValue: false);
                    }
                }
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree($@"{MetadataRoot}\{normalized}", throwOnMissingSubKey: false);

        TryDeleteEmptyKey($@"{ClassesRoot}\{normalized}\OpenWithProgids");
        TryDeleteEmptyKey($@"{ClassesRoot}\{normalized}\shell");
        TryDeleteEmptyKey($@"{ClassesRoot}\{normalized}");
    }

    private static void TryDeleteEmptyKey(string path)
    {
        var isEmpty = false;
        using (var key = Registry.CurrentUser.OpenSubKey(path))
        {
            isEmpty = key is not null && key.SubKeyCount == 0 && key.ValueCount == 0;
        }

        if (isEmpty)
        {
            Registry.CurrentUser.DeleteSubKey(path, throwOnMissingSubKey: false);
        }
    }

    private static string BuildProgId(string extension) => $"ComboStarter{extension}";

    private static (bool HadPreviousDefault, string? PreviousDefault) ReadBackup(string extension)
    {
        using var metadata = Registry.CurrentUser.OpenSubKey($@"{MetadataRoot}\{extension}");
        if (metadata is null)
        {
            return (false, null);
        }

        var hadPrevious = metadata.GetValue("HadPreviousDefault") is int had && had != 0;
        var previous = metadata.GetValue("PreviousDefault") as string;
        return (hadPrevious, previous);
    }

    private static string? GetUserChoiceProgId(string extension)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{FileExtsRoot}\{extension}\UserChoice");
        return key?.GetValue("ProgId") as string;
    }

    private static string? GetUserDefaultProgId(string extension)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{extension}");
        return key?.GetValue(null) as string;
    }

    private static string? GetMachineDefaultProgId(string extension)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"{ClassesRoot}\{extension}");
        var value = key?.GetValue(null) as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private enum AssocStr
    {
        Command = 1,
        Executable = 2,
        FriendlyDocName = 3,
        FriendlyAppName = 4,
        NoOpen = 5,
        ShellNewValue = 6,
        DdeCommand = 7,
        DdeIfExec = 8,
        DdeApplication = 9,
        DdeTopic = 10,
    }

    [DllImport("shlwapi.dll", EntryPoint = "AssocQueryStringW", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int AssocQueryString(
        uint flags,
        AssocStr str,
        string pszAssoc,
        string? pszExtra,
        StringBuilder? pszOut,
        ref uint pcchOut);
}
