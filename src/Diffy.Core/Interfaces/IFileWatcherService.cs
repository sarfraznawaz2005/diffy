namespace Diffy.Core.Interfaces;

/// <summary>
/// Service for watching file system changes in a repository.
/// </summary>
public interface IFileWatcherService : IDisposable
{
    /// <summary>
    /// Starts watching a repository for file changes.
    /// </summary>
    void StartWatching(string repoPath);

    /// <summary>
    /// Stops watching a specific repository.
    /// </summary>
    void StopWatching(string repoPath);

    /// <summary>
    /// Stops watching all repositories.
    /// </summary>
    void StopAll();

    /// <summary>
    /// Checks if a repository is currently being watched.
    /// </summary>
    bool IsWatching(string repoPath);

    /// <summary>
    /// Event raised when a file changes in a watched repository.
    /// </summary>
    event EventHandler<FileChangedEventArgs>? FileChanged;
}

/// <summary>
/// Event arguments for file change events.
/// </summary>
public class FileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Path to the repository containing the changed file.
    /// </summary>
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Relative path to the changed file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Type of change that occurred.
    /// </summary>
    public FileChangeType ChangeType { get; set; }
}

/// <summary>
/// Types of file changes.
/// </summary>
public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}
