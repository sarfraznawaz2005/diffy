using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Diffy.App.Services;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diffy.App.ViewModels;

public class DiffViewModel : ViewModelBase, IDisposable
{
    private readonly IGitService _gitService;
    private readonly IDiffService _diffService;
    private readonly ISyntaxHighlightingService _syntaxHighlightingService;
    private readonly ISettingsService _settingsService;
    private readonly string _repositoryPath;
    private readonly IGitRepository _repository;
    private CancellationTokenSource? _loadDiffCts;
    private Task? _progressiveHighlightingTask;

    private const int LargeFileThreshold = 500;

    public DiffViewModel(
        string repositoryPath,
        IGitService gitService,
        IDiffService diffService,
        ISyntaxHighlightingService syntaxHighlightingService,
        ISettingsService settingsService,
        IGitRepository repository)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _diffService = diffService;
        _syntaxHighlightingService = syntaxHighlightingService;
        _settingsService = settingsService;
        _repository = repository;

        Mode = _settingsService.GetDiffMode();
        IgnoreWhitespace = _settingsService.GetIgnoreWhitespace();
        ShowFullContent = _settingsService.GetShowFullContent();

        ToggleModeCommand = ReactiveCommand.Create(ToggleMode);
        ToggleIgnoreWhitespaceCommand = ReactiveCommand.Create(ToggleIgnoreWhitespace);
        ToggleFullContentCommand = ReactiveCommand.Create(ToggleFullContent);
        JumpToNextChangeCommand = ReactiveCommand.Create(() => JumpToChange(1));
        JumpToPreviousChangeCommand = ReactiveCommand.Create(() => JumpToChange(-1));
        CopyFullDiffCommand = ReactiveCommand.CreateFromTask(CopyFullDiffAsync);
    }

    [Reactive] public FileDiff? CurrentDiff { get; set; }
    [Reactive] public DiffMode Mode { get; set; }
    [Reactive] public bool IgnoreWhitespace { get; set; }
    [Reactive] public bool ShowFullContent { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string? HighlightingSearchQuery { get; set; }

    public ReactiveCommand<Unit, Unit> ToggleModeCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleIgnoreWhitespaceCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleFullContentCommand { get; }
    public ReactiveCommand<Unit, Unit> JumpToNextChangeCommand { get; }
    public ReactiveCommand<Unit, Unit> JumpToPreviousChangeCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyFullDiffCommand { get; }

    public event Action<int, DiffMode>? ScrollRequested;

    public async Task LoadDiffAsync(FileStatus file)
    {
        var cts = new CancellationTokenSource();
        _loadDiffCts?.Cancel();
        _loadDiffCts = cts;

        try
        {
            IsLoading = true;

            // Reset jump index when loading new file to prevent incorrect navigation
            _lastJumpIndex = -1;

            if (file.IsBinary)
            {
                CurrentDiff = new FileDiff
                {
                    FilePath = file.Path,
                    IsBinary = true
                };
                return;
            }

            var ct = cts.Token;

            var oldTask = _gitService.GetFileContentAtHeadAsync(_repositoryPath, file.Path, _repository, ct);
            var newTask = _gitService.GetFileContentAsync(_repositoryPath, file.Path, ct);

            await Task.WhenAll(oldTask, newTask);

            var oldContent = await oldTask;
            var newContent = await newTask;

            ct.ThrowIfCancellationRequested();

            var diffResult = await Task.Run(() =>
                ShowFullContent 
                    ? _diffService.GenerateDiff(oldContent, newContent, file.Path, IgnoreWhitespace)
                    : _diffService.GenerateDiffWithContext(oldContent, newContent, file.Path, IgnoreWhitespace, 5),
                ct);

            ct.ThrowIfCancellationRequested();

            // Use progressive highlighting for large files
            var totalLines = diffResult.InlineLines.Count;
            var isLargeFile = totalLines > LargeFileThreshold;

            if (isLargeFile)
            {
                // For large files, use viewport-based progressive highlighting
                // Track the task so we can ensure it completes or is cancelled properly
                _progressiveHighlightingTask = Task.Run(async () =>
                {
                    try
                    {
                        await _syntaxHighlightingService.HighlightFileDiffProgressiveAsync(
                            diffResult,
                            oldContent,
                            newContent,
                            HighlightingSearchQuery,
                            0,
                            Math.Min(200, totalLines - 1),
                            OnHighlightChunkComplete,
                            ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when user switches files quickly - silently ignore
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash - user can still see unhighlighted diff
                        System.Diagnostics.Debug.WriteLine($"Progressive highlighting failed: {ex.Message}");
                    }
                }, ct);
                
                // Set CurrentDiff immediately so user sees content while highlighting progresses
                if (!ct.IsCancellationRequested)
                    CurrentDiff = diffResult;
            }
            else
            {
                // For smaller files, await highlighting completion before showing diff
                try
                {
                    await _syntaxHighlightingService.HighlightFileDiffAsync(diffResult, oldContent, newContent, HighlightingSearchQuery);
                }
                catch (Exception ex)
                {
                    // Log but don't crash - user can still see unhighlighted diff
                    System.Diagnostics.Debug.WriteLine($"Syntax highlighting failed: {ex.Message}");
                }

                if (!ct.IsCancellationRequested)
                    CurrentDiff = diffResult;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user switches files quickly
        }
        catch (Exception ex)
        {
            // Log unexpected errors
            System.Diagnostics.Debug.WriteLine($"LoadDiffAsync failed: {ex.Message}");
            // Could set an error state here if needed
        }
        finally
        {
            IsLoading = false;
            if (cts == _loadDiffCts)
                _loadDiffCts = null;
        }
    }

    private void OnHighlightChunkComplete(int chunkStart, int chunkEnd)
    {
        // Notify that a chunk of lines has been highlighted
        // This triggers UI update for the affected lines
        try
        {
            if (CurrentDiff == null)
                return;
                
            if (chunkStart < 0 || chunkStart >= CurrentDiff.InlineLines.Count)
                return;
                
            // Clamp chunkEnd to valid range
            var safeChunkEnd = Math.Min(chunkEnd, CurrentDiff.InlineLines.Count - 1);
            
            for (int i = chunkStart; i <= safeChunkEnd; i++)
            {
                var line = CurrentDiff.InlineLines[i];
                if (line != null)
                {
                    line.RaisePropertyChanged("Highlights");
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash - highlighting update failure is non-critical
            System.Diagnostics.Debug.WriteLine($"OnHighlightChunkComplete failed: {ex.Message}");
        }
    }

    private void ToggleMode()
    {
        Mode = Mode == DiffMode.SideBySide ? DiffMode.Inline : DiffMode.SideBySide;
        _settingsService.SetDiffMode(Mode);
    }

    private void ToggleIgnoreWhitespace()
    {
        IgnoreWhitespace = !IgnoreWhitespace;
        _settingsService.SetIgnoreWhitespace(IgnoreWhitespace);
    }

    private void ToggleFullContent()
    {
        ShowFullContent = !ShowFullContent;
        _settingsService.SetShowFullContent(ShowFullContent);
    }

    private int _lastJumpIndex = -1;

    private void JumpToChange(int direction)
    {
        if (CurrentDiff == null) return;

        var items = Mode == DiffMode.SideBySide
            ? (IEnumerable<object>)CurrentDiff.AlignedRows
            : CurrentDiff.InlineLines;

        var list = items.ToList();
        if (list.Count == 0) return;

        int startIndex = _lastJumpIndex + direction;
        if (startIndex < 0) startIndex = list.Count - 1;
        if (startIndex >= list.Count) startIndex = 0;

        int i = startIndex;
        bool wrapped = false;

        while (true)
        {
            if (IsChange(list[i]))
            {
                _lastJumpIndex = i;
                ScrollRequested?.Invoke(i, Mode);
                return;
            }

            i += direction;
            if (i < 0)
            {
                if (wrapped) break;
                i = list.Count - 1;
                wrapped = true;
            }
            else if (i >= list.Count)
            {
                if (wrapped) break;
                i = 0;
                wrapped = true;
            }

            if (i == startIndex) break;
        }
    }

    private bool IsChange(object item)
    {
        if (item is AlignedDiffRow row)
        {
            return row.OldLine.Kind == DiffLineKind.Added ||
                   row.OldLine.Kind == DiffLineKind.Removed ||
                   row.NewLine.Kind == DiffLineKind.Added ||
                   row.NewLine.Kind == DiffLineKind.Removed;
        }
        else if (item is DiffLine line)
        {
            return line.Kind == DiffLineKind.Added || line.Kind == DiffLineKind.Removed;
        }
        return false;
    }

    private async Task CopyFullDiffAsync()
    {
        if (CurrentDiff != null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.Clipboard != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var line in CurrentDiff.InlineLines)
                {
                    if (line.Kind != DiffLineKind.Placeholder)
                        sb.AppendLine(line.Content);
                }
                await topLevel.Clipboard.SetTextAsync(sb.ToString());
            }
        }
    }

    public void Dispose()
    {
        _loadDiffCts?.Cancel();
        _loadDiffCts?.Dispose();
        _loadDiffCts = null;
    }
}
