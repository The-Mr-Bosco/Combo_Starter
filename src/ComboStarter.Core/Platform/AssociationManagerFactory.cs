namespace ComboStarter.Core.Platform;

public static class AssociationManagerFactory
{
    public static IAssociationManager Create()
    {
        return OperatingSystem.IsWindows()
            ? new Windows.WindowsAssociationManager()
            : new NullAssociationManager();
    }
}
