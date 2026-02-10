using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diffy.Core.Models;

/// <summary>
/// Represents a single line in a diff output.
/// </summary>
public class DiffLine : INotifyPropertyChanged
{
    private List<HighlightedSegment>? _highlights;

    /// <summary>
    /// Line number in old (original) file. -1 for added lines.
    /// </summary>
    public int OldLineNumber { get; set; } = -1;

    /// <summary>
    /// Line number in new (modified) file. -1 for removed lines.
    /// </summary>
    public int NewLineNumber { get; set; } = -1;

    /// <summary>
    /// The text content of the line.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The kind of change this line represents.
    /// </summary>
    public DiffLineKind Kind { get; set; }

    /// <summary>
    /// Highlighting segments for this line.
    /// </summary>
    public List<HighlightedSegment>? Highlights
    {
        get => _highlights;
        set
        {
            if (!Equals(_highlights, value))
            {
                _highlights = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void RaisePropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }
}

/// <summary>
/// Represents a highlighted segment of text.
/// </summary>
public class HighlightedSegment
{
    public int Offset { get; set; }
    public int Length { get; set; }
    public string? ColorHex { get; set; }
    public string? BackgroundHex { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
}

/// <summary>
/// The type of change a diff line represents.
/// </summary>
public enum DiffLineKind
{
    /// <summary>Line was added.</summary>
    Added,

    /// <summary>Line was removed.</summary>
    Removed,

    /// <summary>Line is unchanged.</summary>
    Unchanged,

    /// <summary>Context line (same as unchanged but for display purposes).</summary>
    Context,

    /// <summary>Hunk header line.</summary>
    Header,

    /// <summary>Placeholder line for side-by-side alignment.</summary>
    Placeholder
}

/// <summary>
/// Represents a block of diff lines (a hunk).
/// </summary>
public class DiffBlock
{
    /// <summary>
    /// Lines from the old (original) file.
    /// </summary>
    public List<DiffLine> OldLines { get; set; } = new();

    /// <summary>
    /// Lines from the new (modified) file.
    /// </summary>
    public List<DiffLine> NewLines { get; set; } = new();

    /// <summary>
    /// Path of the file this diff belongs to.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// Complete diff result for a file.
/// </summary>
public class FileDiff
{
    /// <summary>
    /// Path of the file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file is binary.
    /// </summary>
    public bool IsBinary { get; set; }

    /// <summary>
    /// The diff blocks (hunks) for this file.
    /// </summary>
    public List<DiffBlock> Blocks { get; set; } = new();

    /// <summary>
    /// Flat list of lines for inline diff view.
    /// </summary>
    public List<DiffLine> InlineLines { get; set; } = new();

    /// <summary>
    /// Flat list of aligned rows for side-by-side diff view (supports virtualization).
    /// </summary>
    public List<AlignedDiffRow> AlignedRows { get; set; } = new();

    /// <summary>
    /// Number of lines added.
    /// </summary>
    public int Additions { get; set; }

    /// <summary>
    /// Number of lines removed.
    /// </summary>
    public int Deletions { get; set; }
}

/// <summary>
/// Represents a row in a side-by-side diff view.
/// </summary>
public class AlignedDiffRow
{
    public DiffLine OldLine { get; set; } = new();
    public DiffLine NewLine { get; set; } = new();
}

/// <summary>
/// Display mode for the diff viewer.
/// </summary>
public enum DiffMode
{
    /// <summary>Show old and new files side by side.</summary>
    SideBySide,

    /// <summary>Show changes inline in a single view.</summary>
    Inline
}
