using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diffy.App.ViewModels;

public class CommitHistoryViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly string _repositoryPath;
    private readonly IGitRepository _repository;
    private CancellationTokenSource? _loadCommitsCts;

    public CommitHistoryViewModel(string repositoryPath, IGitService gitService, IGitRepository repository)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _repository = repository;

        LoadMoreCommitsCommand = ReactiveCommand.CreateFromTask(LoadMoreCommitsAsync,
            this.WhenAnyValue(x => x.CanLoadMoreCommits, x => x.IsLoading, (can, loading) => can && !loading));
        LoadMoreCommitsCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"LoadMoreCommitsCommand Exception: {ex.Message}", nameof(CommitHistoryViewModel)));

        ViewCommitFilesCommand = ReactiveCommand.CreateFromTask<CommitInfo>(async c => await ViewCommitFilesAsync(c));
        ViewCommitFilesCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ViewCommitFilesCommand Exception: {ex.Message}", nameof(CommitHistoryViewModel)));

        CloseCommitFilesCommand = ReactiveCommand.Create(() => { IsCommitFilesVisible = false; });
        CloseCommitFilesCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"CloseCommitFilesCommand Exception: {ex.Message}", nameof(CommitHistoryViewModel)));

        this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                try
                {
                    ApplyFilter();
                }
                catch (Exception ex)
                {
                    Program.Log($"Error applying commit filter: {ex.Message}", nameof(CommitHistoryViewModel));
                }
            });
    }

    public ObservableCollection<CommitInfo> Commits { get; } = new();
    public ObservableCollection<CommitInfo> FilteredCommits { get; } = new();

    [Reactive] public string SearchQuery { get; set; } = string.Empty;
    [Reactive] public CommitInfo? SelectedCommit { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public bool CanLoadMoreCommits { get; set; } = true;
    [Reactive] public bool IsCommitFilesVisible { get; set; }
    public ObservableCollection<ChangedFile> CommitFiles { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadMoreCommitsCommand { get; }
    public ReactiveCommand<CommitInfo, Unit> ViewCommitFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommitFilesCommand { get; }

    public async Task LoadMoreCommitsAsync()
    {
        if (IsLoading) return;

        _loadCommitsCts?.Cancel();
        _loadCommitsCts = new CancellationTokenSource();
        var ct = _loadCommitsCts.Token;

        try
        {
            IsLoading = true;
            var newCommits = await _gitService.GetCommitHistoryAsync(_repositoryPath, skip: Commits.Count, take: 50, repository: _repository, ct: ct);

            ct.ThrowIfCancellationRequested();

            if (newCommits.Count < 50)
            {
                CanLoadMoreCommits = false;
            }

            foreach (var commit in newCommits)
            {
                Commits.Add(commit);
            }

            ApplyFilter();
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchQuery?.Trim();

        IEnumerable<CommitInfo> filtered = string.IsNullOrEmpty(query)
            ? Commits
            : Commits.Where(c =>
                (c.Hash?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.Message?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.Author?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

        var filteredList = filtered.ToList();

        // Update in-place to avoid Clear+Rebuild flickering
        int i = 0;
        foreach (var commit in filteredList)
        {
            if (i < FilteredCommits.Count)
            {
                if (!ReferenceEquals(FilteredCommits[i], commit))
                    FilteredCommits[i] = commit;
            }
            else
            {
                FilteredCommits.Add(commit);
            }
            i++;
        }
        while (FilteredCommits.Count > i)
            FilteredCommits.RemoveAt(FilteredCommits.Count - 1);
    }

    private async Task ViewCommitFilesAsync(CommitInfo commit)
    {
        if (commit == null) return;

        try
        {
            SelectedCommit = commit;
            var files = await _gitService.GetFilesInCommitAsync(_repositoryPath, commit.FullHash, repository: _repository);

            // Update in-place to avoid flickering
            int i = 0;
            foreach (var file in files)
            {
                if (i < CommitFiles.Count)
                {
                    if (!ReferenceEquals(CommitFiles[i], file))
                        CommitFiles[i] = file;
                }
                else
                {
                    CommitFiles.Add(file);
                }
                i++;
            }
            while (CommitFiles.Count > i)
                CommitFiles.RemoveAt(CommitFiles.Count - 1);

            IsCommitFilesVisible = true;
        }
        catch (Exception)
        {
            // Fail silently or handle error
        }
    }
}
