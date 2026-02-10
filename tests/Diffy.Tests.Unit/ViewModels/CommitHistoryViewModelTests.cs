using Diffy.App.ViewModels;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diffy.Tests.Unit.ViewModels;

public class CommitHistoryViewModelTests
{
    private readonly Mock<IGitService> _gitServiceMock;
    private readonly Mock<IGitRepository> _repoMock;
    private readonly CommitHistoryViewModel _sut;

    public CommitHistoryViewModelTests()
    {
        _gitServiceMock = new Mock<IGitService>();
        _repoMock = new Mock<IGitRepository>();
        _sut = new CommitHistoryViewModel("/test/repo", _gitServiceMock.Object, _repoMock.Object);
    }

    [Fact]
    public void ViewCommitFilesCommand_ShowsCommitFilesOverlay()
    {
        // Arrange
        var commit = new CommitInfo { Hash = "abc123", FullHash = "abc123456789" };
        _gitServiceMock.Setup(g => g.GetFilesInCommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IGitRepository>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChangedFile>());

        // Act
        _sut.ViewCommitFilesCommand.Execute(commit).Subscribe();

        // Assert
        _sut.IsCommitFilesVisible.Should().BeTrue();
        _sut.SelectedCommit.Should().Be(commit);
    }

    [Fact]
    public void CloseCommitFilesCommand_HidesCommitFilesOverlay()
    {
        // Arrange
        _sut.IsCommitFilesVisible = true;

        // Act
        _sut.CloseCommitFilesCommand.Execute().Subscribe();

        // Assert
        _sut.IsCommitFilesVisible.Should().BeFalse();
    }
}
