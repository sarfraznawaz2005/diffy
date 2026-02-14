using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Diffy.App.Services;
using Diffy.App.Utilities;
using Diffy.Core.Interfaces;
using ReactiveUI;

namespace Diffy.App.ViewModels;

/// <summary>
/// Main window ViewModel managing repository tabs.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly IDiffService _diffService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly ISettingsService _settingsService;
    private readonly IFileOperationService _fileOperationService;
    private readonly ITrashService _trashService;
    private readonly ISyntaxHighlightingService _syntaxHighlightingService;
    private readonly IShellIntegrationService _shellIntegrationService;
    private readonly IGitRepositoryFactory _repoFactory;

    private int _selectedTabIndex;
    private string _statusText = "Ready";
    private string? _errorMessage;
    private string? _pendingTrustPath;
    private bool? _isContextMenuRegistered;

    public MainWindowViewModel(
        IGitService gitService,
        IDiffService diffService,
        IFileWatcherService fileWatcherService,
        ISettingsService settingsService,
        IFileOperationService fileOperationService,
        ITrashService trashService,
        ISyntaxHighlightingService syntaxHighlightingService,
        IShellIntegrationService shellIntegrationService,
        IGitRepositoryFactory repoFactory)
    {
        _gitService = gitService;
        _diffService = diffService;
        _fileWatcherService = fileWatcherService;
        _settingsService = settingsService;
        _fileOperationService = fileOperationService;
        _trashService = trashService;
        _syntaxHighlightingService = syntaxHighlightingService;
        _shellIntegrationService = shellIntegrationService;
        _repoFactory = repoFactory;

        // Initialize collections
        Repositories = new ObservableCollection<RepositoryTabViewModel>();

        // Load recent repositories efficienty
        RecentRepositories = new ObservableCollection<string>(_settingsService.GetRecentRepositories());

        // Commands
        AddRepositoryCommand = ReactiveCommand.CreateFromTask<IStorageProvider?>(AddRepositoryAsync);
        AddRepositoryCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"AddRepositoryCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        CloseTabCommand = ReactiveCommand.Create<RepositoryTabViewModel>(CloseTab);
        CloseTabCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"CloseTabCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        OpenRecentCommand = ReactiveCommand.CreateFromTask<string>(OpenRecentRepositoryAsync);
        OpenRecentCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"OpenRecentCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        ExitCommand = ReactiveCommand.Create(Exit);
        ExitCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ExitCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        RefreshCurrentTabCommand = ReactiveCommand.CreateFromTask(RefreshCurrentTabAsync);
        RefreshCurrentTabCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"RefreshCurrentTabCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        ToggleContextMenuCommand = ReactiveCommand.Create(ToggleContextMenu);
        ToggleContextMenuCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ToggleContextMenuCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        DismissErrorCommand = ReactiveCommand.Create(() => { ErrorMessage = null; });
        DismissErrorCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"DismissErrorCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        ToggleHistoryCommand = ReactiveCommand.CreateFromTask(ToggleHistoryAsync);
        ToggleHistoryCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"ToggleHistoryCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        JumpToNextChangeCommand = ReactiveCommand.CreateFromTask(JumpToNextChangeAsync);
        JumpToNextChangeCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"JumpToNextChangeCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        JumpToPreviousChangeCommand = ReactiveCommand.CreateFromTask(JumpToPreviousChangeAsync);
        JumpToPreviousChangeCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"JumpToPreviousChangeCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        var canTrust = this.WhenAnyValue(
            x => x.PendingTrustPath,
            path => !string.IsNullOrEmpty(path));

        TrustAndRetryCommand = ReactiveCommand.CreateFromTask(TrustAndRetryAsync, canTrust);
        TrustAndRetryCommand.ThrownExceptions.Subscribe(ex =>
            Program.Log($"TrustAndRetryCommand Exception: {ex.Message}", nameof(MainWindowViewModel)));

        // Track last active repository when tab selection changes
        this.WhenAnyValue(x => x.SelectedTabIndex)
            .Subscribe(index =>
            {
                try
                {
                    if (index >= 0 && index < Repositories.Count)
                    {
                        var activeRepoPath = Repositories[index].RepositoryPath;
                        _settingsService.SetLastActiveRepository(activeRepoPath);
                    }
                }
                catch (Exception ex)
                {
                    Program.Log($"Error tracking active repository: {ex.Message}", nameof(MainWindowViewModel));
                }
            });
    }

    public ObservableCollection<RepositoryTabViewModel> Repositories { get; }
    public ObservableCollection<string> RecentRepositories { get; } = new();

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public string? PendingTrustPath
    {
        get => _pendingTrustPath;
        set => this.RaiseAndSetIfChanged(ref _pendingTrustPath, value);
    }

    public bool IsContextMenuRegistered
    {
        get
        {
            if (_isContextMenuRegistered == null)
            {
                _isContextMenuRegistered = _shellIntegrationService.IsRegistered();
            }
            return _isContextMenuRegistered.Value;
        }
        set => this.RaiseAndSetIfChanged(ref _isContextMenuRegistered, value);
    }

    public ReactiveCommand<IStorageProvider?, Unit> AddRepositoryCommand { get; }
    public ReactiveCommand<RepositoryTabViewModel, Unit> CloseTabCommand { get; }
    public ReactiveCommand<string, Unit> OpenRecentCommand { get; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    private ReactiveCommand<Unit, Unit>? _aboutCommand;
    public ReactiveCommand<Unit, Unit> AboutCommand =>
        _aboutCommand ??= ReactiveCommand.CreateFromTask(ShowAboutAsync);
    private ReactiveCommand<string, Unit>? _themeCommand;
    public ReactiveCommand<string, Unit> ThemeCommand =>
        _themeCommand ??= ReactiveCommand.Create<string>(SetTheme);
    public ReactiveCommand<Unit, Unit> RefreshCurrentTabCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleContextMenuCommand { get; }
    public ReactiveCommand<Unit, Unit> DismissErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> TrustAndRetryCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> JumpToNextChangeCommand { get; }
    public ReactiveCommand<Unit, Unit> JumpToPreviousChangeCommand { get; }

    public async Task AutoOpenLastRepositoryAsync()
    {
        var lastActiveRepo = _settingsService.GetLastActiveRepository();
        if (!string.IsNullOrEmpty(lastActiveRepo))
        {
            await OpenRepositoryAsync(lastActiveRepo);
        }
        else if (RecentRepositories.Any())
        {
            await OpenRepositoryAsync(RecentRepositories.First());
        }
    }

    private async Task RefreshCurrentTabAsync()
    {
        if (SelectedTabIndex >= 0 && SelectedTabIndex < Repositories.Count)
        {
            await Repositories[SelectedTabIndex].RefreshCommand.Execute(Unit.Default);
        }
    }

    private async Task ToggleHistoryAsync()
    {
        if (SelectedTabIndex >= 0 && SelectedTabIndex < Repositories.Count)
        {
            await Repositories[SelectedTabIndex].ToggleHistoryCommand.Execute(Unit.Default);
        }
    }

    private async Task JumpToNextChangeAsync()
    {
        if (SelectedTabIndex >= 0 && SelectedTabIndex < Repositories.Count)
        {
            await Repositories[SelectedTabIndex].Diff.JumpToNextChangeCommand.Execute(Unit.Default);
        }
    }

    private async Task JumpToPreviousChangeAsync()
    {
        if (SelectedTabIndex >= 0 && SelectedTabIndex < Repositories.Count)
        {
            await Repositories[SelectedTabIndex].Diff.JumpToPreviousChangeCommand.Execute(Unit.Default);
        }
    }

    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void SetTheme(string theme)
    {
        var appTheme = theme switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Light // Default to Light
        };

        _settingsService.SetTheme(appTheme);

        var app = Application.Current;
        if (app != null)
        {
            app.RequestedThemeVariant = appTheme switch
            {
                AppTheme.Light => Avalonia.Styling.ThemeVariant.Light,
                AppTheme.Dark => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Light // Default to Light
            };
        }
    }

    private async Task AddRepositoryAsync(IStorageProvider? storageProvider)
    {
        if (storageProvider == null)
        {
            StatusText = "Error: Storage provider not available";
            return;
        }

        try
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Git Repository",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                var path = folder.Path.LocalPath;
                await OpenRepositoryAsync(path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening repository: {ex.Message}";
        }
    }

    private async Task ShowAboutAsync()
    {
        var aboutWindow = new Views.AboutWindow();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await aboutWindow.ShowDialog(desktop.MainWindow!);
        }
    }

    public async Task OpenRepositoryAsync(string path)
    {
        ErrorMessage = null;
        PendingTrustPath = null;

        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // Normalize path (handle potential escaped quotes and mix of slashes)
            path = path.Trim('"').Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                       .TrimEnd(Path.DirectorySeparatorChar);

            if (!await _gitService.IsGitRepositoryAsync(path))
            {
                var rootPath = await _gitService.GetRepoRootAsync(path);
                if (rootPath == null)
                {
                    ErrorMessage = $"Error: '{path}' is not a valid Git repository.";
                    return;
                }
                path = rootPath.Replace('/', Path.DirectorySeparatorChar);
            }

            // Check if already open (use platform-appropriate path comparison)
            var existing = Repositories.FirstOrDefault(r =>
                StringComparisonHelper.PathEquals(r.RepositoryPath, path));

            if (existing != null)
            {
                SelectedTabIndex = Repositories.IndexOf(existing);
                StatusText = $"Switched to: {existing.RepositoryName}";
                return;
            }

            var tabViewModel = new RepositoryTabViewModel(
                path,
                _gitService,
                _diffService,
                _fileWatcherService,
                _settingsService,
                _fileOperationService,
                _trashService,
                _syntaxHighlightingService,
                _repoFactory);

            await tabViewModel.LoadAsync();
            Repositories.Add(tabViewModel);
            SelectedTabIndex = Repositories.Count - 1;

            _settingsService.AddRecentRepository(path);
            UpdateRecentRepositories();

            StatusText = $"Opened: {tabViewModel.RepositoryName}";
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (message.Contains("not owned by current user", StringComparison.OrdinalIgnoreCase))
            {
                PendingTrustPath = path;
                message += "\n\nClick the button below to trust this directory";
            }
            ErrorMessage = $"Error loading repository: {message}";
            StatusText = "Error loading repository";
        }
    }

    private async Task TrustAndRetryAsync()
    {
        if (string.IsNullOrEmpty(PendingTrustPath)) return;

        var path = PendingTrustPath;
        try
        {
            StatusText = "Trusting repository...";
            await _gitService.TrustRepositoryAsync(path);

            PendingTrustPath = null;
            ErrorMessage = null;
            StatusText = "Repository trusted. Retrying...";

            // Wait a tiny bit for Git to process the config change
            await Task.Delay(100);

            await OpenRepositoryAsync(path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to trust repository: {ex.Message}";
            StatusText = "Trust failed";
        }
    }

    private async Task OpenRecentRepositoryAsync(string path)
    {
        await OpenRepositoryAsync(path);
    }

    private void CloseTab(RepositoryTabViewModel tab)
    {
        tab.Dispose();
        Repositories.Remove(tab);

        if (SelectedTabIndex >= Repositories.Count)
        {
            SelectedTabIndex = Math.Max(0, Repositories.Count - 1);
        }
    }

    private void ToggleContextMenu()
    {
        if (IsContextMenuRegistered)
        {
            _shellIntegrationService.UnregisterContextMenuItem();
            IsContextMenuRegistered = false;
        }
        else
        {
            _shellIntegrationService.RegisterContextMenuItem();
            IsContextMenuRegistered = true;
        }
    }

    private void UpdateRecentRepositories()
    {
        RecentRepositories.Clear();
        foreach (var path in _settingsService.GetRecentRepositories())
        {
            RecentRepositories.Add(path);
        }
    }
}
