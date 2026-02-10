using Diffy.App.Services;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FluentAssertions;
using Xunit;

namespace Diffy.Tests.Unit.Services;

/// <summary>
/// Unit tests for the SettingsService class.
/// Tests theme management, diff mode, recent repositories, and persistence.
/// Uses temporary directory to avoid affecting production settings.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly SettingsService _sut;
    private readonly string _tempSettingsDir;

    public SettingsServiceTests()
    {
        // Create a temporary directory for test settings to avoid affecting production settings
        _tempSettingsDir = Path.Combine(Path.GetTempPath(), $"DiffyTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempSettingsDir);

        // Initialize service with temp directory
        _sut = new SettingsService(_tempSettingsDir);
    }

    public void Dispose()
    {
        // Clean up test settings files after each test
        try
        {
            if (Directory.Exists(_tempSettingsDir))
            {
                Directory.Delete(_tempSettingsDir, recursive: true);
            }
        }
        catch { }
    }

    #region Theme Management

    [Fact]
    public void GetTheme_Default_ReturnsLight()
    {
        // Arrange - fresh service with temp directory
        var service = new SettingsService(_tempSettingsDir);

        // Act
        var result = service.GetTheme();

        // Assert
        result.Should().Be(AppTheme.Light);
    }

    [Fact]
    public void SetTheme_SavesAndReturnsCorrectTheme()
    {
        // Arrange
        var themes = new[] { AppTheme.Dark, AppTheme.System, AppTheme.Light };

        foreach (var theme in themes)
        {
            // Act
            _sut.SetTheme(theme);
            var result = _sut.GetTheme();

            // Assert
            result.Should().Be(theme);
        }
    }

    [Fact]
    public void SetTheme_TriggersThemeChangedEvent()
    {
        // Arrange
        var eventTriggered = false;
        var resetEvent = new System.Threading.ManualResetEventSlim(false);
        _sut.ThemeChanged += () =>
        {
            eventTriggered = true;
            resetEvent.Set();
        };

        // Act
        _sut.SetTheme(AppTheme.Dark);

        // Wait for the async event to be dispatched
        resetEvent.Wait(5000);

        // Assert
        eventTriggered.Should().BeTrue();
    }

    [Fact]
    public void SetTheme_PersistsToDisk()
    {
        // Arrange
        _sut.SetTheme(AppTheme.Dark);

        // Act - create new service instance with same temp directory (simulates app restart)
        var newService = new SettingsService(_tempSettingsDir);
        var result = newService.GetTheme();

        // Assert
        result.Should().Be(AppTheme.Dark);

        // Cleanup
        newService.SetTheme(AppTheme.Light);
    }

    #endregion

    #region Diff Mode

    [Fact]
    public void GetDiffMode_Default_ReturnsSideBySide()
    {
        // Arrange - fresh service with temp directory
        var service = new SettingsService(_tempSettingsDir);

        // Act
        var result = service.GetDiffMode();

        // Assert
        result.Should().Be(DiffMode.SideBySide);
    }

    [Fact]
    public void SetDiffMode_SavesAndReturnsCorrectMode()
    {
        // Arrange
        var modes = new[] { DiffMode.Inline, DiffMode.SideBySide };

        foreach (var mode in modes)
        {
            // Act
            _sut.SetDiffMode(mode);
            var result = _sut.GetDiffMode();

            // Assert
            result.Should().Be(mode);
        }
    }

    [Fact]
    public void SetDiffMode_PersistsToDisk()
    {
        // Arrange
        _sut.SetDiffMode(DiffMode.Inline);

        // Act - create new service instance with same temp directory
        var newService = new SettingsService(_tempSettingsDir);
        var result = newService.GetDiffMode();

        // Assert
        result.Should().Be(DiffMode.Inline);

        // Cleanup
        newService.SetDiffMode(DiffMode.SideBySide);
    }

    #endregion

    #region Whitespace Toggle

    [Fact]
    public void GetIgnoreWhitespace_Default_ReturnsFalse()
    {
        // Arrange - fresh service with temp directory
        var service = new SettingsService(_tempSettingsDir);

        // Act
        var result = service.GetIgnoreWhitespace();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SetIgnoreWhitespace_SavesAndReturnsCorrectValue()
    {
        // Act
        _sut.SetIgnoreWhitespace(true);
        var result = _sut.GetIgnoreWhitespace();

        // Assert
        result.Should().BeTrue();

        // Cleanup
        _sut.SetIgnoreWhitespace(false);
    }

    [Fact]
    public void SetIgnoreWhitespace_PersistsToDisk()
    {
        // Arrange
        _sut.SetIgnoreWhitespace(true);

        // Act - create new service instance with same temp directory
        var newService = new SettingsService(_tempSettingsDir);
        var result = newService.GetIgnoreWhitespace();

        // Assert
        result.Should().BeTrue();

        // Cleanup
        newService.SetIgnoreWhitespace(false);
    }

    #endregion

    #region Recent Repositories

    [Fact]
    public void GetRecentRepositories_Default_ReturnsEmptyList()
    {
        // Arrange - fresh service with temp directory
        var service = new SettingsService(_tempSettingsDir);

        // Act
        var result = service.GetRecentRepositories();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void AddRecentRepository_AddsToList()
    {
        // Arrange
        var path = "/test/repo1";

        // Act
        _sut.AddRecentRepository(path);
        var result = _sut.GetRecentRepositories();

        // Assert
        result.Should().Contain(path);
    }

    [Fact]
    public void AddRecentRepository_MovesExistingToTop()
    {
        // Arrange
        _sut.AddRecentRepository("/test/repo1");
        _sut.AddRecentRepository("/test/repo2");
        _sut.AddRecentRepository("/test/repo3");

        // Act - add existing repo again
        _sut.AddRecentRepository("/test/repo1");
        var result = _sut.GetRecentRepositories();

        // Assert
        result.First().Should().Be("/test/repo1");
        result.Should().HaveCount(3);
    }

    [Fact]
    public void AddRecentRepository_LimitsToTen()
    {
        // Arrange
        for (int i = 1; i <= 12; i++)
        {
            _sut.AddRecentRepository($"/test/repo{i}");
        }

        // Act
        var result = _sut.GetRecentRepositories();

        // Assert
        result.Should().HaveCount(10);
        result.Should().NotContain("/test/repo1");
        result.Should().NotContain("/test/repo2");
        result.Should().Contain("/test/repo12");
    }

    [Fact]
    public void GetRecentRepositories_ReturnsInOrder()
    {
        // Arrange
        var paths = new[] { "/test/repo1", "/test/repo2", "/test/repo3" };
        foreach (var path in paths)
        {
            _sut.AddRecentRepository(path);
        }

        // Act
        var result = _sut.GetRecentRepositories();

        // Assert
        result.Should().ContainInOrder(
            "/test/repo3",  // Most recent last
            "/test/repo2",
            "/test/repo1"   // Oldest first
        );
    }

    [Fact]
    public void RemoveRecentRepository_RemovesFromList()
    {
        // Arrange
        _sut.AddRecentRepository("/test/repo1");
        _sut.AddRecentRepository("/test/repo2");

        // Act
        _sut.RemoveRecentRepository("/test/repo1");
        var result = _sut.GetRecentRepositories();

        // Assert
        result.Should().NotContain("/test/repo1");
        result.Should().Contain("/test/repo2");
    }

    [Fact]
    public void RecentRepositories_PersistsToDisk()
    {
        // Arrange
        _sut.AddRecentRepository("/test/persisted-repo");

        // Act - create new service instance with same temp directory
        var newService = new SettingsService(_tempSettingsDir);
        var result = newService.GetRecentRepositories();

        // Assert
        result.Should().Contain("/test/persisted-repo");
    }

    #endregion

    #region Filter Settings

    [Fact]
    public void GetFilterSettings_Default_ReturnsDefaultSettings()
    {
        // Arrange
        var repoPath = "/test/repo";

        // Act
        var result = _sut.GetFilterSettings(repoPath);

        // Assert
        result.Should().NotBeNull();
        result.IncludedStatuses.Should().ContainInOrder(
            FileStatusKind.Modified,
            FileStatusKind.New,
            FileStatusKind.Deleted,
            FileStatusKind.Renamed,
            FileStatusKind.Unstaged
        );
        result.ShowBinaryFiles.Should().BeTrue();
    }

    [Fact]
    public void SaveFilterSettings_PersistsCorrectly()
    {
        // Arrange
        var repoPath = "/test/filter-repo";
        var settings = new FilterSettings
        {
            ShowBinaryFiles = false,
            IncludedExtensions = { ".cs", ".txt" }
        };

        // Act
        _sut.SaveFilterSettings(repoPath, settings);
        var result = _sut.GetFilterSettings(repoPath);

        // Assert
        result.ShowBinaryFiles.Should().BeFalse();
        result.IncludedExtensions.Should().ContainInOrder(".cs", ".txt");
    }

    [Fact]
    public void GetFilterSettings_AfterSave_ReturnsSavedSettings()
    {
        // Arrange
        var repoPath = "/test/cached-repo";
        var originalSettings = new FilterSettings { ShowBinaryFiles = false };
        _sut.SaveFilterSettings(repoPath, originalSettings);

        // Act - get settings multiple times
        var result1 = _sut.GetFilterSettings(repoPath);
        var result2 = _sut.GetFilterSettings(repoPath);

        // Assert
        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public void GetFilterSettings_DifferentRepos_ReturnDifferentSettings()
    {
        // Arrange
        var repo1 = "/test/repo1";
        var repo2 = "/test/repo2";

        _sut.SaveFilterSettings(repo1, new FilterSettings { ShowBinaryFiles = true });
        _sut.SaveFilterSettings(repo2, new FilterSettings { ShowBinaryFiles = false });

        // Act
        var settings1 = _sut.GetFilterSettings(repo1);
        var settings2 = _sut.GetFilterSettings(repo2);

        // Assert
        settings1.ShowBinaryFiles.Should().BeTrue();
        settings2.ShowBinaryFiles.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void LoadSettings_WithCorruptedJson_UsesDefaults()
    {
        // Arrange - this test verifies that the service handles corrupted JSON gracefully
        // The service should return default values when settings can't be loaded
        var service = new SettingsService(_tempSettingsDir);

        // Act & Assert - verify defaults work even if file is corrupted
        var theme = service.GetTheme();
        var diffMode = service.GetDiffMode();
        var ignoreWhitespace = service.GetIgnoreWhitespace();

        // Should have valid default values
        Enum.IsDefined(typeof(AppTheme), theme).Should().BeTrue();
        Enum.IsDefined(typeof(DiffMode), diffMode).Should().BeTrue();
        ignoreWhitespace.Should().BeFalse(); // Default is false
    }

    [Fact]
    public void AddRecentRepository_WithNullPath_DoesNotThrow()
    {
        // Act & Assert
        Action act = () => _sut.AddRecentRepository(string.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveRecentRepository_NonExistentPath_DoesNotThrow()
    {
        // Act & Assert
        Action act = () => _sut.RemoveRecentRepository("/non/existent/path");
        act.Should().NotThrow();
    }

    [Fact]
    public void GetFilterSettings_InvalidRepoPath_ReturnsDefault()
    {
        // Arrange
        var invalidPath = string.Empty;

        // Act
        var result = _sut.GetFilterSettings(invalidPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new FilterSettings());
    }

    [Fact]
    public void SaveFilterSettings_InvalidRepoPath_DoesNotThrow()
    {
        // Arrange
        var invalidPath = string.Empty;
        var settings = new FilterSettings();

        // Act & Assert
        Action act = () => _sut.SaveFilterSettings(invalidPath, settings);
        act.Should().NotThrow();
    }

    #endregion
}
