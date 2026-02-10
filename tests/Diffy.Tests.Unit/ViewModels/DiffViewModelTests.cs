using Diffy.App.Services;
using Diffy.App.ViewModels;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diffy.Tests.Unit.ViewModels;

public class DiffViewModelTests
{
    private readonly Mock<IGitService> _gitServiceMock;
    private readonly Mock<IDiffService> _diffServiceMock;
    private readonly Mock<ISyntaxHighlightingService> _syntaxMock;
    private readonly Mock<ISettingsService> _settingsMock;
    private readonly Mock<IGitRepository> _repoMock;
    private readonly DiffViewModel _sut;

    public DiffViewModelTests()
    {
        _gitServiceMock = new Mock<IGitService>();
        _diffServiceMock = new Mock<IDiffService>();
        _syntaxMock = new Mock<ISyntaxHighlightingService>();
        _settingsMock = new Mock<ISettingsService>();
        _repoMock = new Mock<IGitRepository>();

        _settingsMock.Setup(s => s.GetDiffMode()).Returns(DiffMode.SideBySide);
        _settingsMock.Setup(s => s.GetIgnoreWhitespace()).Returns(false);

        _sut = new DiffViewModel(
            "/test/repo",
            _gitServiceMock.Object,
            _diffServiceMock.Object,
            _syntaxMock.Object,
            _settingsMock.Object,
            _repoMock.Object);
    }

    [Fact]
    public void ToggleModeCommand_SwitchesMode()
    {
        // Arrange
        _sut.Mode = DiffMode.SideBySide;

        // Act
        _sut.ToggleModeCommand.Execute().Subscribe();

        // Assert
        _sut.Mode.Should().Be(DiffMode.Inline);
        _settingsMock.Verify(s => s.SetDiffMode(DiffMode.Inline), Times.Once);
    }

    [Fact]
    public void ToggleIgnoreWhitespaceCommand_TogglesValue()
    {
        // Arrange
        _sut.IgnoreWhitespace = false;

        // Act
        _sut.ToggleIgnoreWhitespaceCommand.Execute().Subscribe();

        // Assert
        _sut.IgnoreWhitespace.Should().BeTrue();
        _settingsMock.Verify(s => s.SetIgnoreWhitespace(true), Times.Once);
    }

    [Fact]
    public void JumpToNextChangeCommand_TriggersScrollRequested()
    {
        // Arrange
        var diff = new FileDiff
        {
            InlineLines = new List<DiffLine>
            {
                new DiffLine { Kind = DiffLineKind.Unchanged, Content = "un" },
                new DiffLine { Kind = DiffLineKind.Added, Content = "add" }
            }
        };
        _sut.CurrentDiff = diff;
        _sut.Mode = DiffMode.Inline;

        int? scrolledIndex = null;
        _sut.ScrollRequested += (index, mode) => scrolledIndex = index;

        // Act
        _sut.JumpToNextChangeCommand.Execute().Subscribe();

        // Assert
        scrolledIndex.Should().Be(1);
    }
}
