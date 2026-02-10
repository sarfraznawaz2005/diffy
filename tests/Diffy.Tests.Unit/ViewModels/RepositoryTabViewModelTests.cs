using System.Collections.ObjectModel;
using Diffy.App.Services;
using Diffy.App.ViewModels;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diffy.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for the RepositoryTabViewModel class.
/// Tests search filtering, history search, property changes, and commands.
/// </summary>
public class RepositoryTabViewModelTests : IDisposable
{
    private readonly Mock<IGitService> _gitServiceMock;
    private readonly Mock<IDiffService> _diffServiceMock;
    private readonly Mock<IFileWatcherService> _fileWatcherMock;
    private readonly Mock<ISettingsService> _settingsMock;
    private readonly Mock<IFileOperationService> _fileOpMock;
    private readonly Mock<ITrashService> _trashMock;
    private readonly Mock<ISyntaxHighlightingService> _syntaxMock;
    private readonly Mock<IGitRepositoryFactory> _repoFactoryMock;
    private readonly RepositoryTabViewModel _sut;

    public RepositoryTabViewModelTests()
    {
        _gitServiceMock = new Mock<IGitService>();
        _diffServiceMock = new Mock<IDiffService>();
        _fileWatcherMock = new Mock<IFileWatcherService>();
        _settingsMock = new Mock<ISettingsService>();
        _fileOpMock = new Mock<IFileOperationService>();
        _trashMock = new Mock<ITrashService>();
        _syntaxMock = new Mock<ISyntaxHighlightingService>();
        _repoFactoryMock = new Mock<IGitRepositoryFactory>();

        // Setup factory to return a mock repository
        var repoMock = new Mock<IGitRepository>();
        _repoFactoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(repoMock.Object);

        // Setup default returns
        _settingsMock.Setup(s => s.GetDiffMode()).Returns(DiffMode.SideBySide);
        _settingsMock.Setup(s => s.GetIgnoreWhitespace()).Returns(false);

        _sut = new RepositoryTabViewModel(
            "/test/repo",
            _gitServiceMock.Object,
            _diffServiceMock.Object,
            _fileWatcherMock.Object,
            _settingsMock.Object,
            _fileOpMock.Object,
            _trashMock.Object,
            _syntaxMock.Object,
            _repoFactoryMock.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    #region Constructor & Properties

    [Fact]
    public void Constructor_SetsRepositoryPathAndName()
    {
        // Assert
        _sut.RepositoryPath.Should().Be("/test/repo");
        _sut.RepositoryName.Should().Be("repo");
    }

    [Fact]
    public void Constructor_InitializesCollections()
    {
        // Assert
        _sut.Files.Should().NotBeNull();
        _sut.FilteredFiles.Should().NotBeNull();
        _sut.History.Commits.Should().NotBeNull();
        _sut.History.FilteredCommits.Should().NotBeNull();
        _sut.History.CommitFiles.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InitializesSubViewModels()
    {
        // Assert
        _sut.Diff.Should().NotBeNull();
        _sut.History.Should().NotBeNull();
    }

    #endregion

    #region Search Query

    [Fact]
    public void SearchQuery_Set_RaisesPropertyChanged()
    {
        // Arrange
        var propertyChanged = false;
        _sut.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RepositoryTabViewModel.SearchQuery))
                propertyChanged = true;
        };

        // Act
        _sut.SearchQuery = "test";

        // Assert
        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void ClearSearchCommand_ClearsSearchQuery()
    {
        // Arrange
        _sut.SearchQuery = "test query";

        // Act
        _sut.ClearSearchCommand.Execute().Subscribe();

        // Assert
        _sut.SearchQuery.Should().BeEmpty();
    }

    [Fact]
    public void ClearSearchCommand_ClearsHistorySearchQuery()
    {
        // Arrange
        _sut.History.SearchQuery = "test commit";

        // Act
        _sut.ClearSearchCommand.Execute().Subscribe();

        // Assert
        _sut.History.SearchQuery.Should().BeEmpty();
    }

    #endregion

    #region Empty State

    [Fact]
    public void EmptyStateMessage_WhenFilesEmpty_ReturnsNoChangesYet()
    {
        // Arrange - Files collection is empty by default

        // Act
        var result = _sut.EmptyStateMessage;

        // Assert
        result.Should().Be("No Changes Yet");
    }

    #endregion

    #region Property Notifications

    [Fact]
    public void SelectedFile_Set_RaisesPropertyChanged()
    {
        // Arrange
        var propertyChanged = false;
        _sut.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RepositoryTabViewModel.SelectedFile))
                propertyChanged = true;
        };

        _gitServiceMock.Setup(s => s.GetFileContentAtHeadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IGitRepository>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("old content");
        _gitServiceMock.Setup(s => s.GetFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new content");
        _diffServiceMock.Setup(s => s.GenerateDiff(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new Core.Models.FileDiff { InlineLines = new List<Core.Models.DiffLine>() });

        // Act
        _sut.SelectedFile = new FileStatus { Path = "test.cs" };

        // Assert
        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void CurrentBranch_Set_RaisesPropertyChanged()
    {
        // Arrange
        var propertyChanged = false;
        _sut.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RepositoryTabViewModel.CurrentBranch))
                propertyChanged = true;
        };

        // Act
        _sut.CurrentBranch = "main";

        // Assert
        propertyChanged.Should().BeTrue();
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_StopsFileWatcher()
    {
        // Act
        _sut.Dispose();

        // Assert
        _fileWatcherMock.Verify(f => f.StopWatching("/test/repo"), Times.Once);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act
        _sut.Dispose();
        _sut.Dispose(); // Should not throw

        // Assert
        _fileWatcherMock.Verify(f => f.StopWatching("/test/repo"), Times.Once);
    }

    #endregion
}
