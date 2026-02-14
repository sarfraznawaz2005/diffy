using System.Collections.Concurrent;
using System.IO;
using Diffy.Core.Interfaces;
using LibGit2Sharp;

namespace Diffy.App.Services;

/// <summary>
/// Factory for creating LibGit2Sharp repository wrappers with reference counting.
/// </summary>
public class LibGit2SharpRepositoryFactory : IGitRepositoryFactory
{
    private static readonly ConcurrentDictionary<string, RepositoryEntry> _repoCache = new();

    private class RepositoryEntry
    {
        public Repository Repository { get; }
        public int RefCount { get; set; }
        public object Lock { get; } = new object();

        public RepositoryEntry(string path)
        {
            Repository = new Repository(path);
            RefCount = 1;
        }
    }

    public IGitRepository Create(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var cacheKey = fullPath.ToLowerInvariant();

        RepositoryEntry entry;
        lock (_repoCache)
        {
            if (_repoCache.TryGetValue(cacheKey, out entry!))
            {
                entry.RefCount++;
            }
            else
            {
                entry = new RepositoryEntry(fullPath);
                _repoCache[cacheKey] = entry;
            }
        }

        return new GitRepositoryWrapper(cacheKey, entry.Repository, entry.Lock, () => Release(cacheKey));
    }

    private void Release(string normalizedPath)
    {
        lock (_repoCache)
        {
            if (_repoCache.TryGetValue(normalizedPath, out var entry))
            {
                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    entry.Repository.Dispose();
                    _repoCache.TryRemove(normalizedPath, out _);
                }
            }
        }
    }

    public bool IsValid(string path) => Repository.IsValid(path);
    public string? Discover(string path) => Repository.Discover(path);
}

/// <summary>
/// Wrapper around LibGit2Sharp Repository. 
/// Shares a Repository instance via reference counting from the factory.
/// </summary>
public class GitRepositoryWrapper : IGitRepository
{
    private readonly Repository _repo;
    private readonly object _lock;
    private readonly Action _onDispose;
    private bool _disposed;

    public GitRepositoryWrapper(string path, Repository repo, object repoLock, Action onDispose)
    {
        _repo = repo;
        _lock = repoLock;
        _onDispose = onDispose;
    }

    private T SafeRun<T>(Func<Repository, T> action, T defaultValue = default!)
    {
        lock (_lock)
        {
            try
            {
                return action(_repo);
            }
            catch (LibGit2Sharp.LibGit2SharpException ex)
            {
                Program.Log($"LibGit2Sharp Exception in SafeRun: {ex.Message}", nameof(GitRepositoryWrapper));
                return defaultValue;
            }
            catch (Exception ex)
            {
                Program.Log($"Exception in SafeRun: {ex.Message}", nameof(GitRepositoryWrapper));
                return defaultValue;
            }
        }
    }

    private void SafeRun(Action<Repository> action)
    {
        lock (_lock)
        {
            try
            {
                action(_repo);
            }
            catch (LibGit2Sharp.LibGit2SharpException ex)
            {
                Program.Log($"LibGit2Sharp Exception in SafeRun: {ex.Message}", nameof(GitRepositoryWrapper));
            }
            catch (Exception ex)
            {
                Program.Log($"Exception in SafeRun: {ex.Message}", nameof(GitRepositoryWrapper));
            }
        }
    }

    public string HeadFriendlyName => SafeRun(r => r.Head.FriendlyName);

    public int BranchCount => SafeRun(r => r.Branches.Count());

    public IGitCommit? HeadTip => SafeRun(r => r.Head.Tip != null ? new GitCommitWrapper(r.Head.Tip) : null);

    public IEnumerable<IGitStatusEntry> RetrieveStatus()
    {
        return SafeRun(r =>
        {
            var status = r.RetrieveStatus(new StatusOptions());
            return status.Select(s => new GitStatusEntryWrapper(s)).ToList(); // Materialize under lock
        });
    }

    public string ComparePatch(string? treeSha, string filePath)
    {
        return SafeRun(r =>
        {
            Tree? tree = null;
            if (treeSha != null)
            {
                var commit = r.Lookup<Commit>(treeSha);
                tree = commit?.Tree;
            }

            var patch = r.Diff.Compare<Patch>(
                tree,
                DiffTargets.WorkingDirectory,
                new[] { filePath });

            return patch.Content;
        });
    }

    public string GetBlobContent(string filePath, string commitSha)
    {
        return SafeRun(r =>
        {
            var commit = r.Lookup<Commit>(commitSha);
            if (commit == null) return string.Empty;

            var treeEntry = commit[filePath];
            if (treeEntry?.TargetType != TreeEntryTargetType.Blob)
                return string.Empty;

            var blob = (Blob)treeEntry.Target;
            return blob.GetContentText();
        });
    }

    public IEnumerable<IGitCommit> QueryCommits(int skip, int take)
    {
        return SafeRun(r =>
        {
            return r.Commits
                .QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Time })
                .Skip(skip)
                .Take(take)
                .Select(c => new GitCommitWrapper(c))
                .ToList(); // Materialize under lock
        });
    }

    public IGitCommit? LookupCommit(string sha)
    {
        return SafeRun(r =>
        {
            var commit = r.Lookup<Commit>(sha);
            return commit != null ? new GitCommitWrapper(commit) : null;
        });
    }

    public IEnumerable<IGitTreeChange> CompareTreeChanges(string? parentSha, string commitSha)
    {
        return SafeRun(r =>
        {
            var commit = r.Lookup<Commit>(commitSha);
            if (commit == null) return Enumerable.Empty<IGitTreeChange>();

            var parent = parentSha != null
                ? r.Lookup<Commit>(parentSha)
                : commit.Parents.FirstOrDefault();

            if (parent == null) return Enumerable.Empty<IGitTreeChange>();

            var changes = r.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
            return changes.Select(c => new GitTreeChangeWrapper(c)).ToList(); // Materialize under lock
        });
    }

    public void CheckoutPaths(string branchName, string[] paths)
    {
        SafeRun(r =>
        {
            r.CheckoutPaths(branchName, paths,
                new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        });
    }

    public bool IsFileBinary(string filePath)
    {
        return SafeRun(r =>
        {
            var headCommit = r.Head.Tip;
            if (headCommit != null)
            {
                var treeEntry = headCommit[filePath];
                if (treeEntry?.TargetType == TreeEntryTargetType.Blob)
                {
                    var blob = (Blob)treeEntry.Target;
                    return blob.IsBinary;
                }
            }

            var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe", ".dll", ".pdb", ".obj", ".bin", ".png", ".jpg", ".jpeg", ".gif",
                ".ico", ".bmp", ".pdf", ".zip", ".rar", ".7z", ".tar", ".gz", ".mp3",
                ".mp4", ".avi", ".mov", ".wav", ".ogg", ".ttf", ".otf", ".woff", ".woff2"
            };

            var extension = Path.GetExtension(filePath);
            return binaryExtensions.Contains(extension);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _onDispose();
    }
}

/// <summary>
/// Wrapper for LibGit2Sharp Commit.
/// </summary>
public class GitCommitWrapper : IGitCommit
{
    private readonly Commit _commit;

    public GitCommitWrapper(Commit commit)
    {
        _commit = commit;
    }

    public string Sha => _commit.Sha;
    public string Message => _commit.Message;
    public string AuthorName => _commit.Author.Name;
    public string AuthorEmail => _commit.Author.Email;
    public DateTimeOffset AuthorWhen => _commit.Author.When;
    public IGitCommit? FirstParent => _commit.Parents.FirstOrDefault() != null
        ? new GitCommitWrapper(_commit.Parents.First())
        : null;
    public IGitTree? Tree => _commit.Tree != null ? new GitTreeWrapper(_commit.Tree) : null;
    public IGitTreeEntry? this[string path]
    {
        get
        {
            var entry = _commit[path];
            return entry != null ? new GitTreeEntryWrapper(entry) : null;
        }
    }
}

/// <summary>
/// Wrapper for LibGit2Sharp StatusEntry.
/// </summary>
public class GitStatusEntryWrapper : IGitStatusEntry
{
    private readonly StatusEntry _entry;

    public GitStatusEntryWrapper(StatusEntry entry)
    {
        _entry = entry;
    }

    public string FilePath => _entry.FilePath;
    public int State => (int)_entry.State;
}

/// <summary>
/// Wrapper for LibGit2Sharp TreeChanges.
/// </summary>
public class GitTreeChangeWrapper : IGitTreeChange
{
    private readonly TreeEntryChanges _change;

    public GitTreeChangeWrapper(TreeEntryChanges change)
    {
        _change = change;
    }

    public string Path => _change.Path;
    public string Status => _change.Status.ToString();
}

/// <summary>
/// Wrapper for LibGit2Sharp TreeEntry.
/// </summary>
public class GitTreeEntryWrapper : IGitTreeEntry
{
    private readonly TreeEntry _entry;

    public GitTreeEntryWrapper(TreeEntry entry)
    {
        _entry = entry;
    }

    public int TargetType => (int)_entry.TargetType;
    public object Target => _entry.Target;
}

/// <summary>
/// Wrapper for LibGit2Sharp Tree.
/// </summary>
public class GitTreeWrapper : IGitTree
{
    private readonly Tree _tree;

    public GitTreeWrapper(Tree tree)
    {
        _tree = tree;
    }
}
