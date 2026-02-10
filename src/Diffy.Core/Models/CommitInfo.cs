namespace Diffy.Core.Models;

/// <summary>
/// Represents information about a Git commit.
/// </summary>
public class CommitInfo
{
    /// <summary>
    /// Short hash (7 characters) of the commit.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Full SHA hash of the commit.
    /// </summary>
    public string FullHash { get; set; } = string.Empty;

    /// <summary>
    /// Commit message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Author name and email.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Author name only (without email).
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Date and time of the commit.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// List of files changed in this commit.
    /// </summary>
    public List<ChangedFile> Files { get; set; } = new();
}

/// <summary>
/// Represents a file that was changed in a commit.
/// </summary>
public class ChangedFile
{
    /// <summary>
    /// Path of the file.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Type of change (added, deleted, modified, renamed).
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;
}
