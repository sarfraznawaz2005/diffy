using Diffy.App.Services;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diffy.Tests.Unit.Services;

/// <summary>
/// Unit tests for the GitService class.
/// Tests Git operations with mocked repository factory.
/// </summary>
public class GitServiceTests
{
    private readonly Mock<IGitRepositoryFactory> _factoryMock;
    private readonly Mock<IGitRepository> _repoMock;
    private readonly GitService _sut;

    public GitServiceTests()
    {
        _factoryMock = new Mock<IGitRepositoryFactory>();
        _repoMock = new Mock<IGitRepository>();
        _sut = new GitService(_factoryMock.Object);
    }

    private void SetupRepository()
    {
        _factoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(_repoMock.Object);
    }

    #region Repository Validation

    [Fact]
    public async Task IsGitRepositoryAsync_WithValidRepo_ReturnsTrue()
    {
        // Arrange
        _factoryMock.Setup(f => f.IsValid("/valid/repo")).Returns(true);

        // Act
        var result = await _sut.IsGitRepositoryAsync("/valid/repo");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsGitRepositoryAsync_WithInvalidRepo_ReturnsFalse()
    {
        // Arrange
        _factoryMock.Setup(f => f.IsValid("/invalid/repo")).Returns(false);

        // Act
        var result = await _sut.IsGitRepositoryAsync("/invalid/repo");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetRepoRootAsync_WithGitSubfolder_ReturnsRoot()
    {
        // Arrange
        _factoryMock.Setup(f => f.Discover("/repo/subfolder")).Returns("/repo/.git/");

        // Act
        var result = await _sut.GetRepoRootAsync("/repo/subfolder");

        // Assert
        result.Should().Be("/repo/.git/");
    }

    [Fact]
    public async Task GetRepoRootAsync_WithNonGitPath_ReturnsNull()
    {
        // Arrange
        _factoryMock.Setup(f => f.Discover("/not/a/repo")).Returns((string?)null);

        // Act
        var result = await _sut.GetRepoRootAsync("/not/a/repo");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Branch Operations

    [Fact]
    public async Task GetCurrentBranchAsync_WithValidRepo_ReturnsBranchName()
    {
        // Arrange
        SetupRepository();
        _repoMock.Setup(r => r.HeadFriendlyName).Returns("main");

        // Act
        var result = await _sut.GetCurrentBranchAsync("/repo");

        // Assert
        result.Should().Be("main");
    }

    [Fact]
    public async Task GetBranchCountAsync_WithValidRepo_ReturnsCount()
    {
        // Arrange
        SetupRepository();
        _repoMock.Setup(r => r.BranchCount).Returns(5);

        // Act
        var result = await _sut.GetBranchCountAsync("/repo");

        // Assert
        result.Should().Be(5);
    }

    #endregion

    #region File Status

    [Fact]
    public async Task GetChangedFilesAsync_WithModifiedFile_ReturnsModifiedStatus()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("modified.cs");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.ModifiedInWorkdir);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result.Should().ContainSingle();
        result[0].Path.Should().Be("modified.cs");
        result[0].Status.Should().Be(FileStatusKind.Modified);
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithNewFile_ReturnsNewStatus()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("newfile.cs");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.NewInWorkdir);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result.Should().ContainSingle();
        result[0].Status.Should().Be(FileStatusKind.New);
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithDeletedFile_ReturnsDeletedStatus()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("deleted.cs");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.DeletedFromWorkdir);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result.Should().ContainSingle();
        result[0].Status.Should().Be(FileStatusKind.Deleted);
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithRenamedFile_ReturnsRenamedStatus()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("renamed.cs");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.RenamedInWorkdir);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result.Should().ContainSingle();
        result[0].Status.Should().Be(FileStatusKind.Renamed);
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithIgnoredFile_SkipsFile()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("ignored.dll");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.Ignored);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithUnstagedFile_ReturnsUnstagedStatus()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("unstaged.cs");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.ModifiedInWorkdir);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result[0].Status.Should().Be(FileStatusKind.Modified);
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithConflictingFile_ReturnsUnmergedStatus()
    {
        // Arrange
        SetupRepository();
        var statusEntry = new Mock<IGitStatusEntry>();
        statusEntry.Setup(s => s.FilePath).Returns("conflict.cs");
        statusEntry.Setup(s => s.State).Returns((int)LibGit2Sharp.FileStatus.Conflicted);

        _repoMock.Setup(r => r.RetrieveStatus()).Returns(new[] { statusEntry.Object });

        // Act
        var result = await _sut.GetChangedFilesAsync("/repo");

        // Assert
        result.Should().ContainSingle();
        result[0].Status.Should().Be(FileStatusKind.Unmerged);
    }

    #endregion

    #region File Operations

    [Fact]
    public async Task GetRawDiffAsync_ReturnsPatchContent()
    {
        // Arrange
        SetupRepository();
        var commitMock = new Mock<IGitCommit>();
        commitMock.Setup(c => c.Sha).Returns("abc123");

        _repoMock.Setup(r => r.HeadTip).Returns(commitMock.Object);
        _repoMock.Setup(r => r.ComparePatch("abc123", "file.cs")).Returns("diff content");

        // Act
        var result = await _sut.GetRawDiffAsync("/repo", "file.cs");

        // Assert
        result.Should().Be("diff content");
    }

    [Fact]
    public async Task GetFileContentAtHeadAsync_WithValidFile_ReturnsContent()
    {
        // Arrange
        SetupRepository();
        var commitMock = new Mock<IGitCommit>();
        commitMock.Setup(c => c.Sha).Returns("abc123");

        _repoMock.Setup(r => r.HeadTip).Returns(commitMock.Object);
        _repoMock.Setup(r => r.GetBlobContent("file.cs", "abc123")).Returns("file content");

        // Act
        var result = await _sut.GetFileContentAtHeadAsync("/repo", "file.cs");

        // Assert
        result.Should().Be("file content");
    }

    [Fact]
    public async Task GetFileContentAtHeadAsync_WithNoHead_ReturnsEmpty()
    {
        // Arrange
        SetupRepository();
        _repoMock.Setup(r => r.HeadTip).Returns((IGitCommit?)null);

        // Act
        var result = await _sut.GetFileContentAtHeadAsync("/repo", "file.cs");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RevertFileAsync_CallsCheckout()
    {
        // Arrange
        SetupRepository();
        _repoMock.Setup(r => r.HeadFriendlyName).Returns("main");

        // Act
        await _sut.RevertFileAsync("/repo", "file.cs");

        // Assert
        _repoMock.Verify(r => r.CheckoutPaths("main", new[] { "file.cs" }), Times.Once);
    }

    #endregion

    #region Commit History

    [Fact]
    public async Task GetCommitHistoryAsync_ReturnsCommits()
    {
        // Arrange
        SetupRepository();
        var commitMock = new Mock<IGitCommit>();
        commitMock.Setup(c => c.Sha).Returns("abc1234567890");
        commitMock.Setup(c => c.Message).Returns("Test commit");
        commitMock.Setup(c => c.AuthorName).Returns("John Doe");
        commitMock.Setup(c => c.AuthorEmail).Returns("john@example.com");
        commitMock.Setup(c => c.AuthorWhen).Returns(DateTimeOffset.Now);

        _repoMock.Setup(r => r.QueryCommits(0, 50)).Returns(new[] { commitMock.Object });

        // Act
        var result = await _sut.GetCommitHistoryAsync("/repo");

        // Assert
        result.Should().ContainSingle();
        result[0].Hash.Should().Be("abc1234");
        result[0].FullHash.Should().Be("abc1234567890");
        result[0].Message.Should().Be("Test commit");
        result[0].Author.Should().Be("John Doe <john@example.com>");
    }

    [Fact]
    public async Task GetCommitHistoryAsync_WithPagination_SkipsAndTakes()
    {
        // Arrange
        SetupRepository();
        var commits = Enumerable.Range(1, 10).Select(i =>
        {
            var mock = new Mock<IGitCommit>();
            mock.Setup(c => c.Sha).Returns($"abc{i}");
            mock.Setup(c => c.Message).Returns($"Commit {i}");
            mock.Setup(c => c.AuthorName).Returns("Author");
            mock.Setup(c => c.AuthorEmail).Returns("email@test.com");
            mock.Setup(c => c.AuthorWhen).Returns(DateTimeOffset.Now);
            return mock.Object;
        });

        _repoMock.Setup(r => r.QueryCommits(10, 5)).Returns(commits.Skip(10).Take(5));

        // Act
        var result = await _sut.GetCommitHistoryAsync("/repo", 10, 5);

        // Assert
        _repoMock.Verify(r => r.QueryCommits(10, 5), Times.Once);
    }

    #endregion

    #region Commit Files

    [Fact]
    public async Task GetFilesInCommitAsync_ReturnsChangedFiles()
    {
        // Arrange
        SetupRepository();
        var changeMock = new Mock<IGitTreeChange>();
        changeMock.Setup(c => c.Path).Returns("modified.cs");
        changeMock.Setup(c => c.Status).Returns("Modified");

        _repoMock.Setup(r => r.CompareTreeChanges(null, "abc123"))
            .Returns(new[] { changeMock.Object });

        // Act
        var result = await _sut.GetFilesInCommitAsync("/repo", "abc123");

        // Assert
        result.Should().ContainSingle();
        result[0].Path.Should().Be("modified.cs");
        result[0].ChangeType.Should().Be("Modified");
    }

    [Fact]
    public async Task GetFilesInCommitAsync_WithNoChanges_ReturnsEmpty()
    {
        // Arrange
        SetupRepository();
        _repoMock.Setup(r => r.CompareTreeChanges(null, "abc123"))
            .Returns(Enumerable.Empty<IGitTreeChange>());

        // Act
        var result = await _sut.GetFilesInCommitAsync("/repo", "abc123");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Dispose

    [Fact]
    public async Task AllMethods_DisposeRepository()
    {
        // Arrange
        SetupRepository();
        _repoMock.Setup(r => r.HeadFriendlyName).Returns("main");

        // Act
        await _sut.GetCurrentBranchAsync("/repo");

        // Assert
        _repoMock.Verify(r => r.Dispose(), Times.Once);
    }

    #endregion
}
