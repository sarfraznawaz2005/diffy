using System.Text.Json;
using Diffy.App.Utilities;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;

namespace Diffy.App.Services;

/// <summary>
/// Settings service that persists user preferences to JSON files.
/// </summary>
public class SettingsService : ISettingsService
{
    public event Action? ThemeChanged;
    private readonly string _settingsDir;
    private readonly string _globalSettingsPath;
    private GlobalSettings? _globalSettings;
    private readonly object _settingsLock = new();
    private readonly Dictionary<string, FilterSettings> _filterSettingsCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsService(string? settingsDir = null)
    {
        _settingsDir = settingsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Diffy");

        _globalSettingsPath = Path.Combine(_settingsDir, "settings.json");

        Task.Run(() => Directory.CreateDirectory(_settingsDir)).GetAwaiter().GetResult();
    }

    public FilterSettings GetFilterSettings(string repoPath)
    {
        if (_filterSettingsCache.TryGetValue(repoPath, out var cached))
            return cached;

        var settingsPath = GetRepoSettingsPath(repoPath);
        if (Task.Run(() => File.Exists(settingsPath)).GetAwaiter().GetResult())
        {
            try
            {
                var json = Task.Run(() => File.ReadAllText(settingsPath)).GetAwaiter().GetResult();
                var settings = JsonSerializer.Deserialize<FilterSettings>(json, JsonOptions);
                if (settings != null)
                {
                    _filterSettingsCache[repoPath] = settings;
                    return settings;
                }
            }
            catch
            {
                // Ignore and return default
            }
        }

        var defaultSettings = new FilterSettings();
        _filterSettingsCache[repoPath] = defaultSettings;
        return defaultSettings;
    }

    public void SaveFilterSettings(string repoPath, FilterSettings settings)
    {
        _filterSettingsCache[repoPath] = settings;
        var settingsPath = GetRepoSettingsPath(repoPath);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        Task.Run(() => File.WriteAllText(settingsPath, json)).GetAwaiter().GetResult();
    }

    public DiffMode GetDiffMode()
    {
        EnsureGlobalSettingsLoaded();
        return _globalSettings!.DiffMode;
    }

    public void SetDiffMode(DiffMode mode)
    {
        EnsureGlobalSettingsLoaded();
        _globalSettings!.DiffMode = mode;
        SaveGlobalSettings();
    }

    public bool GetIgnoreWhitespace()
    {
        EnsureGlobalSettingsLoaded();
        return _globalSettings!.IgnoreWhitespace;
    }

    public void SetIgnoreWhitespace(bool ignore)
    {
        EnsureGlobalSettingsLoaded();
        _globalSettings!.IgnoreWhitespace = ignore;
        SaveGlobalSettings();
    }

    public bool GetShowFullContent()
    {
        EnsureGlobalSettingsLoaded();
        return _globalSettings!.ShowFullContent;
    }

    public void SetShowFullContent(bool showFull)
    {
        EnsureGlobalSettingsLoaded();
        _globalSettings!.ShowFullContent = showFull;
        SaveGlobalSettings();
    }

    public AppTheme GetTheme()
    {
        EnsureGlobalSettingsLoaded();
        return _globalSettings!.Theme;
    }

    public void SetTheme(AppTheme theme)
    {
        EnsureGlobalSettingsLoaded();
        _globalSettings!.Theme = theme;
        SaveGlobalSettings();

        // Use Dispatcher to ensure UI updates happen on UI thread
        // If no UI thread is available (e.g., in unit tests), invoke directly
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            ThemeChanged?.Invoke();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ThemeChanged?.Invoke();
            });
        }
    }

    public List<string> GetRecentRepositories()
    {
        EnsureGlobalSettingsLoaded();
        return _globalSettings!.RecentRepositories.ToList();
    }

    public void AddRecentRepository(string path)
    {
        EnsureGlobalSettingsLoaded();

        var normalizedPath = PathUtilities.NormalizePathForComparison(path);

        _globalSettings!.RecentRepositories.RemoveAll(r =>
            StringComparisonHelper.PathEquals(
                PathUtilities.NormalizePathForComparison(r),
                normalizedPath));

        _globalSettings.RecentRepositories.Insert(0, normalizedPath);

        // Keep only the 10 most recent
        if (_globalSettings.RecentRepositories.Count > 10)
        {
            _globalSettings.RecentRepositories =
                _globalSettings.RecentRepositories.Take(10).ToList();
        }

        SaveGlobalSettings();
    }

    public void RemoveRecentRepository(string path)
    {
        EnsureGlobalSettingsLoaded();

        var normalizedPath = PathUtilities.NormalizePathForComparison(path);

        _globalSettings!.RecentRepositories.RemoveAll(r =>
            StringComparisonHelper.PathEquals(
                PathUtilities.NormalizePathForComparison(r),
                normalizedPath));

        SaveGlobalSettings();
    }

    public string? GetLastActiveRepository()
    {
        EnsureGlobalSettingsLoaded();
        return _globalSettings!.LastActiveRepository;
    }

    public void SetLastActiveRepository(string? path)
    {
        EnsureGlobalSettingsLoaded();

        if (!string.IsNullOrEmpty(path))
        {
            var normalizedPath = PathUtilities.NormalizePathForComparison(path);
            _globalSettings!.LastActiveRepository = normalizedPath;
            SaveGlobalSettings();
        }
        else
        {
            _globalSettings!.LastActiveRepository = null;
            SaveGlobalSettings();
        }
    }




    private void EnsureGlobalSettingsLoaded()
    {
        if (_globalSettings != null) return;

        lock (_settingsLock)
        {
            if (_globalSettings != null) return;
            LoadGlobalSettings();
        }
    }

    private void LoadGlobalSettings()
    {
        if (Task.Run(() => File.Exists(_globalSettingsPath)).GetAwaiter().GetResult())
        {
            try
            {
                var json = Task.Run(() => File.ReadAllText(_globalSettingsPath)).GetAwaiter().GetResult();
                _globalSettings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions)
                    ?? new GlobalSettings();

                if (_globalSettings.RecentRepositories == null)
                {
                    _globalSettings.RecentRepositories = new List<string>();
                }
            }
            catch
            {
                _globalSettings = new GlobalSettings();
            }
        }
        else
        {
            _globalSettings = new GlobalSettings();
        }
    }

    private void SaveGlobalSettings()
    {
        var json = JsonSerializer.Serialize(_globalSettings, JsonOptions);
        Task.Run(() => File.WriteAllText(_globalSettingsPath, json)).GetAwaiter().GetResult();
    }

    private string GetRepoSettingsPath(string repoPath)
    {
        var hash = Math.Abs(repoPath.GetHashCode()).ToString();
        return Path.Combine(_settingsDir, $"repo-{hash}.json");
    }

    private class GlobalSettings
    {
        public DiffMode DiffMode { get; set; } = DiffMode.SideBySide;
        public AppTheme Theme { get; set; } = AppTheme.Light;
        public List<string> RecentRepositories { get; set; } = new();
        public string? LastActiveRepository { get; set; }
        public bool IgnoreWhitespace { get; set; }
        public bool ShowFullContent { get; set; } = false; // Default to hunks only
    }

}
