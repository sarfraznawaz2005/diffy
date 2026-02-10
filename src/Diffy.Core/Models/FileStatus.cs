namespace Diffy.Core.Models;

/// <summary>
/// Represents the status of a file in a Git repository.
/// </summary>
public class FileStatus
{
    /// <summary>
    /// Relative path of the file within the repository.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The kind of change this file has undergone.
    /// </summary>
    public FileStatusKind Status { get; set; }

    /// <summary>
    /// Last modification time of the file.
    /// </summary>
    public DateTime ModifiedTime { get; set; }

    /// <summary>
    /// Whether this is a binary file.
    /// </summary>
    public bool IsBinary { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets the file name without the path.
    /// </summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// Gets the file extension.
    /// </summary>
    public string Extension => System.IO.Path.GetExtension(Path);
}

/// <summary>
/// The type of change a file has undergone.
/// </summary>
public enum FileStatusKind
{
    /// <summary>File has been modified.</summary>
    Modified,

    /// <summary>File is new (untracked or added).</summary>
    New,

    /// <summary>File has been deleted.</summary>
    Deleted,

    /// <summary>File has been renamed.</summary>
    Renamed,

    /// <summary>File has unstaged changes.</summary>
    Unstaged,

    /// <summary>File has merge conflicts.</summary>
    Unmerged,

    /// <summary>File is ignored by .gitignore.</summary>
    Ignored,

    /// <summary>Unknown status.</summary>
    Unknown
}
