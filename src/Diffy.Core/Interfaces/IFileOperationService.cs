namespace Diffy.Core.Interfaces;

/// <summary>
/// Service for file operations like opening, reverting, and deleting.
/// </summary>
public interface IFileOperationService
{
    /// <summary>
    /// Opens a file in the default application.
    /// </summary>
    Task OpenFileAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Opens a file in the default text editor.
    /// </summary>
    Task OpenInEditorAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Opens the containing folder of a file.
    /// </summary>
    Task OpenContainingFolderAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// Service for moving files to the system trash/recycle bin.
/// </summary>
public interface ITrashService
{
    /// <summary>
    /// Moves a file to the trash/recycle bin.
    /// </summary>
    Task<bool> MoveToTrashAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Whether trash functionality is supported on this platform.
    /// </summary>
    bool IsSupported { get; }
}
