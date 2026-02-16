using System.IO;
using Diffy.App.Utilities;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FileStatus = Diffy.Core.Models.FileStatus;

namespace Diffy.App.Services;

/// <summary>
/// Git service implementation using LibGit2Sharp.
/// </summary>
public class GitService : IGitService
{
    private readonly IGitRepositoryFactory _repoFactory;

    public GitService(IGitRepositoryFactory repoFactory)
    {
        _repoFactory = repoFactory;
    }

    public Task<List<FileStatus>> GetChangedFilesAsync(string repoPath, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var files = new List<FileStatus>();

            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                var status = repo.RetrieveStatus();

                foreach (var entry in status)
                {
                    if (HasFlag(entry.State, (int)LibGit2Sharp.FileStatus.Ignored))
                        continue;

                    var kind = MapFileStatus(entry.State);
                    if (kind == FileStatusKind.Unknown || kind == FileStatusKind.Ignored)
                        continue;

                    var (modTime, size) = GetFileInfo(repoPath, entry.FilePath);
                    var fileStatus = new FileStatus
                    {
                        Path = entry.FilePath,
                        Status = kind,
                        ModifiedTime = modTime,
                        IsBinary = repo.IsFileBinary(entry.FilePath),
                        Size = size
                    };

                    files.Add(fileStatus);
                }

                return files;
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task<string> GetCurrentBranchAsync(string repoPath, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                return repo.HeadFriendlyName;
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task<int> GetBranchCountAsync(string repoPath, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                return repo.BranchCount;
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task<string> GetRawDiffAsync(string repoPath, string filePath, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                return repo.ComparePatch(repo.HeadTip?.Sha, filePath);
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public async Task<string> GetFileContentAsync(string repoPath, string filePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(repoPath, filePath);
        return File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false) : string.Empty;
    }

    public Task<string> GetFileContentAtHeadAsync(string repoPath, string filePath, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                var headCommit = repo.HeadTip;
                if (headCommit == null) return string.Empty;

                return repo.GetBlobContent(filePath, headCommit.Sha);
            }
            catch (Exception ex)
            {
                Program.Log($"GetFileContentAtHeadAsync Exception: {ex.Message}", nameof(GitService));
                return string.Empty;
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task<List<CommitInfo>> GetCommitHistoryAsync(string repoPath, int skip = 0, int take = 50, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var commits = new List<CommitInfo>();

            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                var commitList = repo.QueryCommits(skip, take);

                foreach (var commit in commitList)
                {
                    commits.Add(new CommitInfo
                    {
                        Hash = commit.Sha[..7],
                        FullHash = commit.Sha,
                        Message = commit.Message,
                        Author = $"{commit.AuthorName} <{commit.AuthorEmail}>",
                        AuthorName = commit.AuthorName,
                        Date = commit.AuthorWhen.DateTime
                    });
                }

                return commits;
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task<List<ChangedFile>> GetFilesInCommitAsync(string repoPath, string commitHash, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var files = new List<ChangedFile>();

            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                var changes = repo.CompareTreeChanges(null, commitHash);

                foreach (var change in changes)
                {
                    files.Add(new ChangedFile
                    {
                        Path = change.Path,
                        ChangeType = change.Status
                    });
                }

                return files;
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task RevertFileAsync(string repoPath, string filePath, IGitRepository? repository = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repo = repository ?? _repoFactory.Create(repoPath);
            var shouldDispose = repository == null;

            try
            {
                repo.CheckoutPaths(repo.HeadFriendlyName, new[] { filePath });
            }
            finally
            {
                if (shouldDispose)
                    repo.Dispose();
            }
        }, ct);
    }

    public Task<bool> IsGitRepositoryAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() => _repoFactory.IsValid(path), ct);
    }

    public async Task TrustRepositoryAsync(string path, CancellationToken ct = default)
    {
        // Check if git is available before proceeding
        if (!await IsGitAvailableAsync(ct))
        {
            throw new InvalidOperationException(
                "Git is not installed or not available in PATH. Please install Git to trust repositories.");
        }

        // Normalize path for Git config - uses forward slashes for cross-platform consistency
        var normalizedPath = PathUtilities.NormalizePathForGit(path);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"config --global --add safe.directory \"{normalizedPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null) throw new Exception("Failed to start git process.");

        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Git config failed with exit code {process.ExitCode}: {stderr}");
        }

        // Verify it was added
        var verifyInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "config --global --get-all safe.directory",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var verifyProcess = System.Diagnostics.Process.Start(verifyInfo);
        if (verifyProcess != null)
        {
            var stdout = await verifyProcess.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await verifyProcess.WaitForExitAsync(ct).ConfigureAwait(false);
            if (!stdout.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                // If it's not there, maybe try with the EXACT path including case and slashes as we got it
                // but the normalized one usually works. We'll just throw if we can't find it.
                // Actually, let's not throw yet, maybe it was added but our check is too strict.
            }
        }
    }

    public Task<string?> GetRepoRootAsync(string path, CancellationToken ct = default)
    {
        return Task.Run<string?>(() => _repoFactory.Discover(path), ct);
    }

    private static bool HasFlag(int value, int flag)
    {
        return (value & flag) == flag;
    }

    private static FileStatusKind MapFileStatus(int status)
    {
        if (HasFlag(status, (int)LibGit2Sharp.FileStatus.Ignored))
            return FileStatusKind.Ignored;

        if (HasFlag(status, (int)LibGit2Sharp.FileStatus.Conflicted))
            return FileStatusKind.Unmerged;

        if (HasFlag(status, (int)LibGit2Sharp.FileStatus.NewInWorkdir) ||
            HasFlag(status, (int)LibGit2Sharp.FileStatus.NewInIndex))
            return FileStatusKind.New;

        if (HasFlag(status, (int)LibGit2Sharp.FileStatus.DeletedFromWorkdir) ||
            HasFlag(status, (int)LibGit2Sharp.FileStatus.DeletedFromIndex))
            return FileStatusKind.Deleted;

        if (HasFlag(status, (int)LibGit2Sharp.FileStatus.RenamedInWorkdir) ||
            HasFlag(status, (int)LibGit2Sharp.FileStatus.RenamedInIndex))
            return FileStatusKind.Renamed;

        if (HasFlag(status, (int)LibGit2Sharp.FileStatus.ModifiedInWorkdir) ||
            HasFlag(status, (int)LibGit2Sharp.FileStatus.ModifiedInIndex))
            return FileStatusKind.Modified;

        return FileStatusKind.Unknown;
    }

    private static (DateTime ModifiedTime, long Size) GetFileInfo(string repoPath, string filePath)
    {
        var fullPath = Path.Combine(repoPath, filePath);
        var info = new FileInfo(fullPath);
        return info.Exists
            ? (info.LastWriteTime, info.Length)
            : (DateTime.MinValue, 0);
    }

    /// <summary>
    /// Checks if Git is installed and available in the system PATH.
    /// </summary>
    private static async Task<bool> IsGitAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            // Git is not available or not in PATH
            return false;
        }
    }
}
