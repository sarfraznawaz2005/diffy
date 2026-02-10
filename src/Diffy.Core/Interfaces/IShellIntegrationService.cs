namespace Diffy.Core.Interfaces;

/// <summary>
/// Service for managing OS-specific shell integration.
/// </summary>
public interface IShellIntegrationService
{
    /// <summary>
    /// Registers the application in the OS context menu for folders.
    /// </summary>
    void RegisterContextMenuItem();

    /// <summary>
    /// Unregisters the application from the OS context menu.
    /// </summary>
    void UnregisterContextMenuItem();

    /// <summary>
    /// Checks if the application is currently registered in the OS context menu.
    /// </summary>
    bool IsRegistered();
}
