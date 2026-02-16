using System.Collections.Concurrent;
using Diffy.Core.Interfaces;

namespace Diffy.App.Services;

/// <summary>
/// File watcher service using FileSystemWatcher with debouncing.
/// </summary>
public class FileWatcherService : IFileWatcherService
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastEventTimes = new();
    private readonly ConcurrentDictionary<string, int> _retryCount = new();
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(300);
    private readonly object _debounceLock = new();
    private const int MaxRetries = 5;
    private bool _disposed;

    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public void StartWatching(string repoPath)
    {
        if (_watchers.ContainsKey(repoPath))
            return;

        var watcher = new FileSystemWatcher(repoPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                          NotifyFilters.DirectoryName |
                          NotifyFilters.LastWrite |
                          NotifyFilters.Size
        };

        watcher.Changed += (s, e) => OnFileChanged(repoPath, e.FullPath, FileChangeType.Modified);
        watcher.Created += (s, e) => OnFileChanged(repoPath, e.FullPath, FileChangeType.Created);
        watcher.Deleted += (s, e) => OnFileChanged(repoPath, e.FullPath, FileChangeType.Deleted);
        watcher.Renamed += (s, e) => OnFileChanged(repoPath, e.FullPath, FileChangeType.Renamed);
        watcher.Error += (s, e) => HandleError(repoPath, e.GetException());

        watcher.EnableRaisingEvents = true;
        _watchers[repoPath] = watcher;
        _retryCount[repoPath] = 0;
    }

    public void StopWatching(string repoPath)
    {
        if (_watchers.TryRemove(repoPath, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }

    public void StopAll()
    {
        foreach (var path in _watchers.Keys.ToList())
        {
            StopWatching(path);
        }
    }

    public bool IsWatching(string repoPath)
    {
        return _watchers.ContainsKey(repoPath);
    }

    private void OnFileChanged(string repoPath, string fullPath, FileChangeType changeType)
    {
        // Monitor critical git files for status changes, but ignore other internal git files
        bool isGitInternal = fullPath.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar) ||
                             fullPath.EndsWith(Path.DirectorySeparatorChar + ".git");

        if (isGitInternal)
        {
            // Only allow specific git files that indicate status changes
            var fileName = Path.GetFileName(fullPath);
            var parentDir = Path.GetFileName(Path.GetDirectoryName(fullPath));

            bool isRelevantGitFile = fileName == "index" ||
                                     fileName == "HEAD" ||
                                     parentDir == "refs" ||
                                     parentDir == "heads"; // e.g. .git/refs/heads/main

            if (!isRelevantGitFile)
                return;
        }

        // Debounce rapid events for the same file
        var key = $"{repoPath}:{fullPath}";
        var now = DateTime.UtcNow;

        lock (_debounceLock)
        {
            if (_lastEventTimes.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < _debounceInterval)
                    return;
            }

            _lastEventTimes[key] = now;
        }

        var relativePath = Path.GetRelativePath(repoPath, fullPath);

        FileChanged?.Invoke(this, new FileChangedEventArgs
        {
            RepositoryPath = repoPath,
            FilePath = relativePath,
            ChangeType = changeType
        });
    }

    private void HandleError(string repoPath, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"FileWatcher error for {repoPath}: {ex.Message}");

        if (_watchers.TryRemove(repoPath, out var watcher))
        {
            watcher.Dispose();
        }

        // Retry with exponential backoff and a maximum retry count
        var retries = _retryCount.GetOrAdd(repoPath, 0);
        if (retries >= MaxRetries)
        {
            System.Diagnostics.Debug.WriteLine($"FileWatcher: max retries ({MaxRetries}) reached for {repoPath}, giving up");
            return;
        }

        _retryCount[repoPath] = retries + 1;
        var delay = Math.Min(1000 * (1 << retries), 30000); // 1s, 2s, 4s, 8s, 16s cap at 30s

        Task.Delay(delay).ContinueWith(_ =>
        {
            if (!_disposed && Directory.Exists(repoPath))
            {
                StartWatching(repoPath);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAll();
        GC.SuppressFinalize(this);
    }
}
