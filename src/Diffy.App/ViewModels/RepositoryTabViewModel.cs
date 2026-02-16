using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Diffy.App.Caching;
using Diffy.App.Services;
using Diffy.App.Utilities;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diffy.App.ViewModels;

/// <summary>
/// ViewModel for a single repository tab.
/// </summary>
public class RepositoryTabViewModel : ViewModelBase, IDisposable
{
    private readonly IGitService _gitService;
    private readonly IDiffService _diffService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly ISettingsService _settingsService;
    private readonly IFileOperationService _fileOperationService;
    private readonly ITrashService _trashService;
    private readonly ISyntaxHighlightingService _syntaxHighlightingService;
    private readonly IGitRepositoryFactory _repoFactory;

    [Reactive] public string CurrentBranch { get; set; } = string.Empty;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public FileStatus? SelectedFile { get; set; }
    [Reactive] public int SelectedFileIndex { get; set; } = -1;
    [Reactive] public string SearchQuery { get; set; } = string.Empty;
    [Reactive] public bool IsHistoryMode { get; set; }
    [Reactive] public bool IsConfirmationVisible { get; set; }
    [Reactive] public string ConfirmationTitle { get; set; } = string.Empty;
    [Reactive] public string ConfirmationMessage { get; set; } = string.Empty;
    [Reactive] public bool AutoSelectLatestFile { get; set; }

    private Func<Task>? _pendingAction;
    private bool _disposed;
    private bool _isRefreshing;  // Prevent re-entrant refresh calls

    // Event used by RepositoryTabView
    public event Action<int, DiffMode>? ScrollRequested;

    // Decomposed ViewModels
    public DiffViewModel Diff { get; }
    public CommitHistoryViewModel History { get; }

    // Long-lived repository instance to keep it cached in the factory
    private readonly IGitRepository _repository;

    public RepositoryTabViewModel(
        string repositoryPath,
        IGitService gitService,
        IDiffService diffService,
        IFileWatcherService fileWatcherService,
        ISettingsService settingsService,
        IFileOperationService fileOperationService,
        ITrashService trashService,
        ISyntaxHighlightingService syntaxHighlightingService,
        IGitRepositoryFactory repoFactory)
    {
        RepositoryPath = repositoryPath;
        RepositoryName = new DirectoryInfo(repositoryPath).Name;

        _gitService = gitService;
        _diffService = diffService;
        _fileWatcherService = fileWatcherService;
        _settingsService = settingsService;
        _fileOperationService = fileOperationService;
        _trashService = trashService;
        _syntaxHighlightingService = syntaxHighlightingService;
        _repoFactory = repoFactory;

        try
        {
            // Hold repository instance to keep it cached in LibGit2SharpRepositoryFactory
            _repository = _repoFactory.Create(repositoryPath);

            Diff = new DiffViewModel(repositoryPath, gitService, diffService, syntaxHighlightingService, settingsService, _repository);
            History = new CommitHistoryViewModel(repositoryPath, gitService, _repository);

            Files = new ObservableCollection<FileStatus>();
            FilteredFiles = new ObservableCollection<FileStatus>();

            // Load settings
            AutoSelectLatestFile = _settingsService.GetAutoSelectLatestFile();

            InitializeCaches();
            InitializeCommands();
            InitializeSubscriptions();

            // Set up file watcher
            _fileWatcherService.FileChanged += OnFileChanged;
            _fileWatcherService.StartWatching(repositoryPath);

            // React to theme changes
            _settingsService.ThemeChanged += OnThemeChanged;
        }
        catch
        {
            // Dispose repository if construction fails
            _repository?.Dispose();
            throw;
        }
    }

    private void InitializeCaches()
    {
        // Use a fixed default (64MB) to avoid the slow Environment.WorkingSet OS query
        const long defaultMemoryLimit = 64 * 1024 * 1024;
        _diffContentCache = new StringLRUCache(defaultMemoryLimit, StringLRUCache.CalculateContentWeight);
        _fullContentCache = new StringLRUCache(defaultMemoryLimit, StringLRUCache.CalculateContentWeight);
    }

    private void InitializeCommands()
    {
        var hasFileSelected = this.WhenAnyValue(x => x.SelectedFile).Select(file => file != null);

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        RefreshCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"RefreshCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        OpenFileCommand = ReactiveCommand.CreateFromTask<FileStatus>(async f => await OpenFileAsync(f), hasFileSelected);
        OpenFileCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"OpenFileCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        RevertFileCommand = ReactiveCommand.CreateFromTask<FileStatus>(async f => await RevertFileAsync(f), hasFileSelected);
        RevertFileCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"RevertFileCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        DeleteFileCommand = ReactiveCommand.CreateFromTask<FileStatus>(async f => await DeleteFileAsync(f), hasFileSelected);
        DeleteFileCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"DeleteFileCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        CopyFileContentCommand = ReactiveCommand.CreateFromTask(CopyFileContentAsync, hasFileSelected);
        CopyFileContentCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"CopyFileContentCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);
        ClearSearchCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ClearSearchCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        ToggleHistoryCommand = ReactiveCommand.CreateFromTask(ToggleHistoryAsync);
        ToggleHistoryCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ToggleHistoryCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        CopyPathCommand = ReactiveCommand.CreateFromTask(CopyPathAsync, hasFileSelected);
        CopyPathCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"CopyPathCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        ConfirmActionCommand = ReactiveCommand.CreateFromTask(async () => await ConfirmActionAsync());
        ConfirmActionCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ConfirmActionCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));

        CancelConfirmationCommand = ReactiveCommand.Create(() => { IsConfirmationVisible = false; });
        CancelConfirmationCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"CancelConfirmationCommand Exception: {ex.Message}", nameof(RepositoryTabViewModel)));
    }

    private void InitializeSubscriptions()
    {
        // React to selected file changes
        this.WhenAnyValue(x => x.SelectedFile)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(file =>
            {
                try
                {
                    if (file == null)
                    {
                        // Don't clear diff during refresh - FilteredFiles.Clear() causes
                        // a transient null that would flash the empty state. The selection
                        // will be restored immediately after the file list is rebuilt.
                        if (!_isRefreshing)
                        {
                            Diff.CurrentDiff = null;
                            SelectedFileIndex = -1;
                        }
                    }
                    else
                    {
                        _ = Diff.LoadDiffAsync(file);
                        // Update index when selection changes from UI
                        var index = FilteredFiles.IndexOf(file);
                        if (index >= 0)
                        {
                            SelectedFileIndex = index;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading file diff: {ex.Message}");
                }
            });

        this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(query =>
            {
                try
                {
                    Diff.HighlightingSearchQuery = query;
                    ApplyFilterInternal();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error applying search filter: {ex.Message}");
                }
            });

        Diff.WhenAnyValue(x => x.IgnoreWhitespace)
            .Skip(1) // Skip initial value to prevent reload on startup
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ignore =>
            {
                if (SelectedFile != null)
                {
                    // LoadDiffAsync handles its own cancellation and error logging
                    _ = Diff.LoadDiffAsync(SelectedFile);
                }
            });

        // React to ShowFullContent changes
        Diff.WhenAnyValue(x => x.ShowFullContent)
            .Skip(1) // Skip initial value
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(showFull =>
            {
                if (SelectedFile != null)
                {
                    _ = Diff.LoadDiffAsync(SelectedFile);
                }
            });

        // Forward scroll requests to the View
        Diff.ScrollRequested += (index, mode) => ScrollRequested?.Invoke(index, mode);

        // Save AutoSelectLatestFile setting when changed and auto-select if enabled
        this.WhenAnyValue(x => x.AutoSelectLatestFile)
            .Skip(1) // Skip initial value
            .Subscribe(autoSelect =>
            {
                try
                {
                    _settingsService.SetAutoSelectLatestFile(autoSelect);

                    // Auto-select first file when checkbox is turned on
                    if (autoSelect && FilteredFiles.Count > 0)
                    {
                        SelectedFile = FilteredFiles[0];
                        SelectedFileIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling auto-select: {ex.Message}");
                }
            });
    }

    private void OnThemeChanged()
    {
        if (SelectedFile != null)
        {
            _ = Diff.LoadDiffAsync(SelectedFile);
        }
    }

    public string RepositoryPath { get; }
    public string RepositoryName { get; }

    public async Task OpenFileAsync(FileStatus? file)
    {
        if (file == null) return;
        await _fileOperationService.OpenFileAsync(System.IO.Path.Combine(RepositoryPath, file.Path));
    }

    public Task RevertFileAsync(FileStatus? file)
    {
        if (file == null)
        {
            return Task.CompletedTask;
        }

        _pendingAction = async () =>
        {
            await _gitService.RevertFileAsync(RepositoryPath, file.Path, _repository);
            await RefreshFilesAsync();
        };

        ConfirmationTitle = "Revert Changes";
        ConfirmationMessage = $"Are you sure you want to revert changes in {file.Path}?";
        IsConfirmationVisible = true;
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(FileStatus? file)
    {
        if (file == null)
        {
            return Task.CompletedTask;
        }

        _pendingAction = async () =>
        {
            await _trashService.MoveToTrashAsync(System.IO.Path.Combine(RepositoryPath, file.Path));
            await RefreshFilesAsync();
        };

        ConfirmationTitle = "Delete File";
        ConfirmationMessage = $"Are you sure you want to delete {file.Path}?";
        IsConfirmationVisible = true;
        return Task.CompletedTask;
    }

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        // FileWatcherService calls this on a background thread
        // Use Dispatcher to ensure UI updates happen on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshFilesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshFilesAsync failed: {ex.Message}");
            }
        });
    }



    public string EmptyStateMessage => Files.Any() ? "Select a file to view changes" : "No Changes Yet";

    public ObservableCollection<FileStatus> Files { get; }
    public ObservableCollection<FileStatus> FilteredFiles { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; private set; } = null!;
    public ReactiveCommand<FileStatus, Unit> OpenFileCommand { get; private set; } = null!;
    public ReactiveCommand<FileStatus, Unit> RevertFileCommand { get; private set; } = null!;
    public ReactiveCommand<FileStatus, Unit> DeleteFileCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CopyFileContentCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearSearchCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleHistoryCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CopyPathCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ConfirmActionCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelConfirmationCommand { get; private set; } = null!;

    private async Task CopyFileContentAsync()
    {
        if (SelectedFile == null) return;

        try
        {
            var content = await _gitService.GetFileContentAsync(RepositoryPath, SelectedFile.Path);
            await TextCopy.ClipboardService.SetTextAsync(content);
        }
        catch (Exception ex)
        {
            Program.Log($"CopyFileContent Exception: {ex}", nameof(RepositoryTabViewModel));
        }
    }

    private async Task CopyPathAsync()
    {
        if (SelectedFile == null) return;

        try
        {
            await TextCopy.ClipboardService.SetTextAsync(SelectedFile.Path);
        }
        catch (Exception ex)
        {
            Program.Log($"CopyPath Exception: {ex}", nameof(RepositoryTabViewModel));
        }
    }

    private async Task ConfirmActionAsync()
    {
        if (_pendingAction != null)
        {
            await _pendingAction();
            _pendingAction = null;
        }
        IsConfirmationVisible = false;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            CurrentBranch = await _gitService.GetCurrentBranchAsync(RepositoryPath, _repository);
            await RefreshFilesAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        await RefreshFilesAsync();
    }

    private StringLRUCache _diffContentCache = null!;
    private StringLRUCache _fullContentCache = null!;
    private CancellationTokenSource? _filterCts;

    private async Task RefreshFilesAsync()
    {
        // Prevent re-entrant calls that could cause infinite loops
        if (_isRefreshing)
        {
            return;
        }

        try
        {
            _isRefreshing = true;

            // Preserve selection
            var selectedPath = SelectedFile?.Path;

            var files = await _gitService.GetChangedFilesAsync(RepositoryPath, _repository);

            var sorted = files.OrderByDescending(f => f.ModifiedTime).ToList();

            // Update Files in-place to avoid Clear+Rebuild flickering
            SyncCollection(Files, sorted);

            this.RaisePropertyChanged(nameof(EmptyStateMessage));

            // Apply filter and await it to avoid race condition with selection
            await ApplyFilterInternalAsync();

            // Now set selection after filter is complete
            // Auto-select latest file if enabled and files exist
            if (AutoSelectLatestFile && FilteredFiles.Count > 0)
            {
                SelectedFile = FilteredFiles[0];
                SelectedFileIndex = 0;
            }
            // Otherwise try to restore previous selection
            else if (!string.IsNullOrEmpty(selectedPath))
            {
                var newSelection = FilteredFiles.FirstOrDefault(f => f.Path == selectedPath);
                if (newSelection != null)
                {
                    SelectedFile = newSelection;
                    SelectedFileIndex = FilteredFiles.IndexOf(newSelection);
                }
            }

            // If no file is selected after refresh (e.g., all files were committed),
            // clear stale diff content that was preserved during the refresh cycle.
            if (SelectedFile == null)
            {
                Diff.CurrentDiff = null;
                SelectedFileIndex = -1;
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// Updates target collection to match source without Clear+Rebuild,
    /// reducing CollectionChanged events and preventing selection flickering.
    /// </summary>
    private static void SyncCollection<T>(ObservableCollection<T> target, IList<T> source)
    {
        int i = 0;
        foreach (var item in source)
        {
            if (i < target.Count)
            {
                if (!ReferenceEquals(target[i], item))
                    target[i] = item;
            }
            else
            {
                target.Add(item);
            }
            i++;
        }
        while (target.Count > i)
            target.RemoveAt(target.Count - 1);
    }

    private async Task ApplyFilterInternalAsync()
    {
        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();

        var query = SearchQuery?.Trim();

        if (string.IsNullOrEmpty(query))
        {
            SyncCollection(FilteredFiles, Files);
            return;
        }

        // For search queries, run search on background thread then update UI
        var token = _filterCts.Token;
        var filtered = await Task.Run(() => PerformSearch(query, token), token);

        if (token.IsCancellationRequested) return;

        SyncCollection(FilteredFiles, filtered);
    }

    /// <summary>
    /// Synchronous filter apply for use outside of RefreshFilesAsync (e.g., search query changes).
    /// </summary>
    private void ApplyFilterInternal()
    {
        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();

        var query = SearchQuery?.Trim();

        if (string.IsNullOrEmpty(query))
        {
            SyncCollection(FilteredFiles, Files);
            return;
        }

        // For search queries, run async in background
        var token = _filterCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var filtered = await Task.Run(() => PerformSearch(query, token), token);

                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    SyncCollection(FilteredFiles, filtered);
                });
            }
            catch (OperationCanceledException) { }
        }, token);
    }



    private async Task<List<FileStatus>> PerformSearch(string query, CancellationToken token)
    {
        var filtered = new List<FileStatus>();
        foreach (var file in Files)
        {
            if (token.IsCancellationRequested) break;

            if (await IsFileMatch(file, query, token))
            {
                filtered.Add(file);
            }
        }
        return filtered;
    }

    private async Task<bool> IsFileMatch(FileStatus file, string query, CancellationToken token)
    {
        // 1. Path match (use platform-appropriate comparison)
        if (StringComparisonHelper.PathContains(file.Path, query))
            return true;

        // 2. Content match
        try
        {
            if (await IsContentMatch(file.Path, query, token))
                return true;

            // 3. Diff match
            if (await IsDiffMatch(file.Path, query, token))
                return true;
        }
        catch (OperationCanceledException) { }
        catch { /* Ignore fetch errors for search */ }

        return false;
    }

    private async Task<bool> IsContentMatch(string path, string query, CancellationToken token)
    {
        var content = _fullContentCache.Get(path);
        if (string.IsNullOrEmpty(content))
        {
            content = await _gitService.GetFileContentAsync(RepositoryPath, path);
            var weight = StringLRUCache.CalculateContentWeight(content);
            _fullContentCache.Set(path, content, weight);
        }
        return content.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsDiffMatch(string path, string query, CancellationToken token)
    {
        var diff = _diffContentCache.Get(path);
        if (string.IsNullOrEmpty(diff))
        {
            diff = await _gitService.GetRawDiffAsync(RepositoryPath, path, _repository, token);
            var weight = StringLRUCache.CalculateContentWeight(diff);
            _diffContentCache.Set(path, diff, weight);
        }
        return diff.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        History.SearchQuery = string.Empty;
    }

    private async Task ToggleHistoryAsync()
    {
        IsHistoryMode = !IsHistoryMode;

        // Always reload commits when entering history mode to show latest changes
        if (IsHistoryMode)
        {
            // Clear existing commits and reload fresh from git
            History.Commits.Clear();
            await History.LoadMoreCommitsAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _fileWatcherService.FileChanged -= OnFileChanged;
        _fileWatcherService.StopWatching(RepositoryPath);
        _settingsService.ThemeChanged -= OnThemeChanged;

        _repository.Dispose();
        Diff?.Dispose();

        _diffContentCache?.RemoveAll();
        _fullContentCache?.RemoveAll();

        GC.SuppressFinalize(this);
    }
}
