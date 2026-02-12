using Diffy.Core.Models;

namespace Diffy.Core.Interfaces;

/// <summary>
/// Service for persisting user settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Event fired when the application theme changes.
    /// </summary>
    event Action? ThemeChanged;

    /// <summary>
    /// Gets the filter settings for a repository.
    /// </summary>
    FilterSettings GetFilterSettings(string repoPath);

    /// <summary>
    /// Saves filter settings for a repository.
    /// </summary>
    void SaveFilterSettings(string repoPath, FilterSettings settings);

    /// <summary>
    /// Gets the current diff display mode.
    /// </summary>
    DiffMode GetDiffMode();

    /// <summary>
    /// Sets the diff display mode.
    /// </summary>
    void SetDiffMode(DiffMode mode);

    /// <summary>
    /// Gets whether whitespace should be ignored in diffs.
    /// </summary>
    bool GetIgnoreWhitespace();

    /// <summary>
    /// Sets whether whitespace should be ignored in diffs.
    /// </summary>
    void SetIgnoreWhitespace(bool ignore);

    /// <summary>
    /// Gets whether to show full file content or only hunks with context.
    /// </summary>
    bool GetShowFullContent();

    /// <summary>
    /// Sets whether to show full file content or only hunks with context.
    /// </summary>
    void SetShowFullContent(bool showFull);

    /// <summary>
    /// Gets the current application theme.
    /// </summary>
    AppTheme GetTheme();

    /// <summary>
    /// Sets the application theme.
    /// </summary>
    void SetTheme(AppTheme theme);


    /// <summary>
    /// Gets the list of recently opened repositories.
    /// </summary>
    List<string> GetRecentRepositories();

    /// <summary>
    /// Adds a repository to the recent list.
    /// </summary>
    void AddRecentRepository(string path);

    /// <summary>
    /// Removes a repository from recent list.
    /// </summary>
    void RemoveRecentRepository(string path);

    /// <summary>
    /// Gets the last active repository path.
    /// </summary>
    string? GetLastActiveRepository();

    /// <summary>
    /// Sets the last active repository path.
    /// </summary>
    void SetLastActiveRepository(string? path);

    /// <summary>
    /// Gets whether to automatically select the latest changed file.
    /// </summary>
    bool GetAutoSelectLatestFile();

    /// <summary>
    /// Sets whether to automatically select the latest changed file.
    /// </summary>
    void SetAutoSelectLatestFile(bool autoSelect);

}

/// <summary>
/// Application theme options.
/// </summary>
public enum AppTheme
{
    Light,
    Dark
}
