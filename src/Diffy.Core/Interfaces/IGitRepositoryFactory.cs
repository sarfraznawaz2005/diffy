using Diffy.Core.Models;

namespace Diffy.Core.Interfaces;

/// <summary>
/// Factory for creating Git repository instances.
/// Abstracts LibGit2Sharp Repository creation for testability.
/// </summary>
public interface IGitRepositoryFactory
{
    /// <summary>
    /// Creates a repository instance for the given path.
    /// </summary>
    IGitRepository Create(string path);

    /// <summary>
    /// Checks if the path is a valid Git repository.
    /// </summary>
    bool IsValid(string path);

    /// <summary>
    /// Discovers the repository root from a given path.
    /// </summary>
    string? Discover(string path);
}

/// <summary>
/// Wrapper interface for LibGit2Sharp Repository operations.
/// </summary>
public interface IGitRepository : IDisposable
{
    /// <summary>
    /// Gets the current branch name.
    /// </summary>
    string HeadFriendlyName { get; }

    /// <summary>
    /// Gets the total number of branches.
    /// </summary>
    int BranchCount { get; }

    /// <summary>
    /// Gets the HEAD commit.
    /// </summary>
    IGitCommit? HeadTip { get; }

    /// <summary>
    /// Retrieves the repository status.
    /// </summary>
    IEnumerable<IGitStatusEntry> RetrieveStatus();

    /// <summary>
    /// Compares trees and returns a patch.
    /// </summary>
    string ComparePatch(string? treeSha, string filePath);

    /// <summary>
    /// Gets the content of a file at a specific commit.
    /// </summary>
    string GetBlobContent(string filePath, string commitSha);

    /// <summary>
    /// Queries commit history.
    /// </summary>
    IEnumerable<IGitCommit> QueryCommits(int skip, int take);

    /// <summary>
    /// Looks up a commit by SHA.
    /// </summary>
    IGitCommit? LookupCommit(string sha);

    /// <summary>
    /// Compares two trees and returns changes.
    /// </summary>
    IEnumerable<IGitTreeChange> CompareTreeChanges(string? parentSha, string commitSha);

    /// <summary>
    /// Checks out file paths.
    /// </summary>
    void CheckoutPaths(string branchName, string[] paths);

    /// <summary>
    /// Checks if a file is binary by examining the blob in HEAD or working directory.
    /// </summary>
    bool IsFileBinary(string filePath);
}

/// <summary>
/// Wrapper for LibGit2Sharp Commit.
/// </summary>
public interface IGitCommit
{
    string Sha { get; }
    string Message { get; }
    string AuthorName { get; }
    string AuthorEmail { get; }
    DateTimeOffset AuthorWhen { get; }
    IGitCommit? FirstParent { get; }
    IGitTree? Tree { get; }
    IGitTreeEntry? this[string path] { get; }
}

/// <summary>
/// Wrapper for LibGit2Sharp StatusEntry.
/// </summary>
public interface IGitStatusEntry
{
    string FilePath { get; }
    int State { get; }
}

/// <summary>
/// Wrapper for LibGit2Sharp TreeChanges.
/// </summary>
public interface IGitTreeChange
{
    string Path { get; }
    string Status { get; }
}

/// <summary>
/// Wrapper for LibGit2Sharp Tree.
/// </summary>
public interface IGitTree
{
}

/// <summary>
/// Wrapper for LibGit2Sharp TreeEntry.
/// </summary>
public interface IGitTreeEntry
{
    int TargetType { get; }
    object Target { get; }
}
