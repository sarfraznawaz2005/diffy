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

        try
        {
            IsLoading = true;
            var newCommits = await _gitService.GetCommitHistoryAsync(_repositoryPath, skip: Commits.Count, take: 50, repository: _repository);

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
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchQuery?.Trim().ToLowerInvariant();
        FilteredCommits.Clear();

        var filtered = string.IsNullOrEmpty(query)
            ? Commits
            : Commits.Where(c =>
                (c.Hash?.ToLowerInvariant().Contains(query) ?? false) ||
                (c.Message?.ToLowerInvariant().Contains(query) ?? false) ||
                (c.Author?.ToLowerInvariant().Contains(query) ?? false));

        foreach (var commit in filtered)
        {
            FilteredCommits.Add(commit);
        }
    }

    private async Task ViewCommitFilesAsync(CommitInfo commit)
    {
        if (commit == null) return;

        try
        {
            SelectedCommit = commit;
            var files = await _gitService.GetFilesInCommitAsync(_repositoryPath, commit.FullHash, repository: _repository);

            CommitFiles.Clear();
            foreach (var file in files)
            {
                CommitFiles.Add(file);
            }

            IsCommitFilesVisible = true;
        }
        catch (Exception)
        {
            // Fail silently or handle error
        }
    }
}
