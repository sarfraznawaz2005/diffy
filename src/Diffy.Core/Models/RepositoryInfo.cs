namespace Diffy.Core.Models;

/// <summary>
/// Represents metadata about a Git repository.
/// </summary>
public class RepositoryInfo
{
    /// <summary>
    /// Absolute path to the repository root.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the repository (folder name).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Currently checked out branch.
    /// </summary>
    public string CurrentBranch { get; set; } = string.Empty;

    /// <summary>
    /// Total number of branches in the repository.
    /// </summary>
    public int BranchCount { get; set; }

    /// <summary>
    /// Whether the repository is currently being watched for changes.
    /// </summary>
    public bool IsWatching { get; set; }

    /// <summary>
    /// Last time the repository status was refreshed.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// List of files with changes in the repository.
    /// </summary>
    public List<FileStatus> Files { get; set; } = new();
}
