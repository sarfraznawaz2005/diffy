using Diffy.Core.Models;

namespace Diffy.Core.Interfaces;

/// <summary>
/// Service for generating and parsing file diffs.
/// </summary>
public interface IDiffService
{
    /// <summary>
    /// Parses raw diff text into structured DiffBlocks.
    /// </summary>
    FileDiff ParseDiff(string rawDiff, string filePath);

    /// <summary>
    /// Generates inline diff HTML for display.
    /// </summary>
    string GenerateInlineDiff(FileDiff diff);

    /// <summary>
    /// Generates side-by-side diff data.
    /// </summary>
    (List<DiffLine> OldLines, List<DiffLine> NewLines) GenerateSideBySideDiff(FileDiff diff);

    /// <summary>
    /// Checks if the diff indicates a binary file.
    /// </summary>
    bool IsBinaryDiff(string rawDiff);

    /// <summary>
    /// Generates a diff between two strings.
    /// </summary>
    FileDiff GenerateDiff(string oldText, string newText, string filePath, bool ignoreWhitespace = false);

    /// <summary>
    /// Generates a diff with context lines (hunks only).
    /// </summary>
    FileDiff GenerateDiffWithContext(string oldText, string newText, string filePath, bool ignoreWhitespace = false, int contextLines = 5);
}
