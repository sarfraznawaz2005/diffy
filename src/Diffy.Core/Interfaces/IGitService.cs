using Diffy.Core.Models;

namespace Diffy.Core.Interfaces;

/// <summary>
/// Service for Git repository operations.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Gets a list of changed files in repository.
    /// </summary>
    Task<List<FileStatus>> GetChangedFilesAsync(string repoPath, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the current branch name.
    /// </summary>
    Task<string> GetCurrentBranchAsync(string repoPath, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the total number of branches.
    /// </summary>
    Task<int> GetBranchCountAsync(string repoPath, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the raw diff output for a specific file.
    /// </summary>
    Task<string> GetRawDiffAsync(string repoPath, string filePath, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the content of a file from the working directory.
    /// </summary>
    Task<string> GetFileContentAsync(string repoPath, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Gets the content of a file at the current HEAD (committed version).
    /// </summary>
    Task<string> GetFileContentAtHeadAsync(string repoPath, string filePath, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Gets commit history for the repository with pagination.
    /// </summary>
    Task<List<CommitInfo>> GetCommitHistoryAsync(string repoPath, int skip = 0, int take = 50, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the list of files changed in a specific commit.
    /// </summary>
    Task<List<ChangedFile>> GetFilesInCommitAsync(string repoPath, string commitHash, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Reverts a file to the last committed state.
    /// </summary>
    Task RevertFileAsync(string repoPath, string filePath, IGitRepository? repository = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if a path is a valid Git repository.
    /// </summary>
    Task<bool> IsGitRepositoryAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Gets the root path of the repository containing the given path.
    /// </summary>
    Task<string?> GetRepoRootAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Adds a directory to Git's safe.directory list.
    /// </summary>
    Task TrustRepositoryAsync(string path, CancellationToken ct = default);
}
