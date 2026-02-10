using Avalonia;
using Avalonia.Styling;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;

namespace Diffy.App.Services;

/// <summary>
/// Diff service implementation using DiffPlex library.
/// </summary>
public class DiffService : IDiffService
{
    private readonly SideBySideDiffBuilder _sideBySideDiffBuilder;
    private readonly InlineDiffBuilder _inlineDiffBuilder;
    private readonly ISettingsService _settingsService;

    public DiffService(ISettingsService settingsService)
    {
        var differ = new Differ();
        _sideBySideDiffBuilder = new SideBySideDiffBuilder(differ);
        _inlineDiffBuilder = new InlineDiffBuilder(differ);
        _settingsService = settingsService;
    }

    public FileDiff ParseDiff(string rawDiff, string filePath)
    {
        var fileDiff = new FileDiff
        {
            FilePath = filePath,
            IsBinary = IsBinaryDiff(rawDiff)
        };

        if (fileDiff.IsBinary)
            return fileDiff;

        ProcessUnifiedDiffLines(rawDiff, fileDiff);
        return fileDiff;
    }

    private void ProcessUnifiedDiffLines(string rawDiff, FileDiff fileDiff)
    {
        var lines = rawDiff.Split('\n');
        var currentBlock = new DiffBlock { FilePath = fileDiff.FilePath };
        int oldLineNum = 0, newLineNum = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("@@"))
            {
                FlushCurrentBlock(fileDiff, ref currentBlock);
                (oldLineNum, newLineNum) = ParseHunkHeader(line);
            }
            else if (line.StartsWith("-") && !line.StartsWith("---"))
            {
                var diffLine = CreateDiffLine(ref oldLineNum, -1, line[1..], DiffLineKind.Removed);
                currentBlock.OldLines.Add(diffLine);
                fileDiff.InlineLines.Add(diffLine);
                fileDiff.Deletions++;
            }
            else if (line.StartsWith("+") && !line.StartsWith("+++"))
            {
                var diffLine = CreateDiffLine(-1, ref newLineNum, line[1..], DiffLineKind.Added);
                currentBlock.NewLines.Add(diffLine);
                fileDiff.InlineLines.Add(diffLine);
                fileDiff.Additions++;
            }
            else if (line.StartsWith(" "))
            {
                AlignBlock(currentBlock);
                var contextLine = CreateDiffLine(ref oldLineNum, ref newLineNum, line[1..], DiffLineKind.Unchanged);
                currentBlock.OldLines.Add(contextLine);
                currentBlock.NewLines.Add(contextLine);
                fileDiff.InlineLines.Add(contextLine);
            }
        }

        FlushCurrentBlock(fileDiff, ref currentBlock);
    }

    private void FlushCurrentBlock(FileDiff fileDiff, ref DiffBlock currentBlock)
    {
        if (currentBlock.OldLines.Count > 0 || currentBlock.NewLines.Count > 0)
        {
            AlignBlock(currentBlock);
            fileDiff.Blocks.Add(currentBlock);

            foreach (var row in currentBlock.OldLines.Zip(currentBlock.NewLines, (old, @new) => new AlignedDiffRow { OldLine = old, NewLine = @new }))
            {
                fileDiff.AlignedRows.Add(row);
            }

            currentBlock = new DiffBlock { FilePath = fileDiff.FilePath };
        }
    }

    private (int oldStart, int newStart) ParseHunkHeader(string header)
    {
        var parts = header.Split(' ');
        if (parts.Length < 3) return (1, 1);

        var oldPart = parts[1].TrimStart('-').Split(',');
        var newPart = parts[2].TrimStart('+').Split(',');

        int oldStart = int.TryParse(oldPart[0], out var o) ? o : 1;
        int newStart = int.TryParse(newPart[0], out var n) ? n : 1;

        return (oldStart, newStart);
    }

    private DiffLine CreateDiffLine(ref int oldNum, int newNum, string content, DiffLineKind kind)
    {
        return new DiffLine
        {
            OldLineNumber = oldNum == -1 ? -1 : oldNum++,
            NewLineNumber = newNum,
            Content = content,
            Kind = kind
        };
    }

    private DiffLine CreateDiffLine(int oldNum, ref int newNum, string content, DiffLineKind kind)
    {
        return new DiffLine
        {
            OldLineNumber = oldNum,
            NewLineNumber = newNum == -1 ? -1 : newNum++,
            Content = content,
            Kind = kind
        };
    }

    private DiffLine CreateDiffLine(ref int oldNum, ref int newNum, string content, DiffLineKind kind)
    {
        return new DiffLine
        {
            OldLineNumber = oldNum++,
            NewLineNumber = newNum++,
            Content = content,
            Kind = kind
        };
    }

    private void AlignBlock(DiffBlock block)
    {
        // Very basic alignment: Equalize length of Old and New lines in the block
        // This is a naive heuristic because we don't have the full diff algorithm state here.
        // Ideally we'd separate "hunks" of changes within the block but that's complex.

        // However, for diff display correctness, we should at least ensure the lists are same length
        // by padding the end of the shorter list with placeholders. 
        // NOTE: This assumes the block contains one contiguous change or aligned context. 

        // A better approach for purely visual alignment without re-diffing:
        // iterate and insert placeholders to match context lines.
        // But since we built OldLines and NewLines with Context lines sync'd, 
        // mismatches only happen in the change groups.

        // Let's refine: The blocks constructed above intermix Context and Changes.
        // We need to pass through and ensure that whenever we have a run of Changes,
        // we balance them? No, that's hard post-hoc.

        // Alternative: Re-build the generic lists into aligned lists.
        // But for now, let's just pad the END of the block if it's not equal, 
        // assuming the block ends with changes. (If it ends with context, they are strictly equal).

        int oldDataCount = block.OldLines.Count;
        int newDataCount = block.NewLines.Count;

        while (oldDataCount < newDataCount)
        {
            block.OldLines.Add(new DiffLine { Kind = DiffLineKind.Placeholder, Content = "" });
            oldDataCount++;
        }
        while (newDataCount < oldDataCount)
        {
            block.NewLines.Add(new DiffLine { Kind = DiffLineKind.Placeholder, Content = "" });
            newDataCount++;
        }
    }

    public FileDiff GenerateDiff(string oldText, string newText, string filePath, bool ignoreWhitespace = false)
    {
        var fileDiff = new FileDiff { FilePath = filePath };

        // Use DiffPlex to get the full side-by-side diff model
        var sideBySide = _sideBySideDiffBuilder.BuildDiffModel(oldText, newText, ignoreWhitespace);

        var allOldLines = new List<DiffLine>();
        var allNewLines = new List<DiffLine>();

        for (int i = 0; i < sideBySide.OldText.Lines.Count; i++)
        {
            var oldLine = sideBySide.OldText.Lines[i];
            allOldLines.Add(new DiffLine
            {
                OldLineNumber = oldLine.Position ?? -1,
                NewLineNumber = -1,
                Content = oldLine.Text ?? string.Empty,
                Kind = MapChangeType(oldLine.Type, isNew: false)
            });

            if (oldLine.Type == ChangeType.Deleted || oldLine.Type == ChangeType.Modified)
            {
                fileDiff.Deletions++;
                if (oldLine.Type == ChangeType.Modified)
                {
                    allOldLines[i].Highlights = GetSubLineHighlights(oldLine.SubPieces, isNew: false);
                }
            }
        }

        for (int i = 0; i < sideBySide.NewText.Lines.Count; i++)
        {
            var newLine = sideBySide.NewText.Lines[i];
            allNewLines.Add(new DiffLine
            {
                OldLineNumber = -1,
                NewLineNumber = newLine.Position ?? -1,
                Content = newLine.Text ?? string.Empty,
                Kind = MapChangeType(newLine.Type, isNew: true)
            });

            if (newLine.Type == ChangeType.Inserted || newLine.Type == ChangeType.Modified)
            {
                fileDiff.Additions++;
                if (newLine.Type == ChangeType.Modified)
                {
                    allNewLines[i].Highlights = GetSubLineHighlights(newLine.SubPieces, isNew: true);
                }
            }
        }

        // Create a single block with full content
        var block = new DiffBlock { FilePath = filePath };

        for (int i = 0; i < allOldLines.Count; i++)
        {
            block.OldLines.Add(allOldLines[i]);
            block.NewLines.Add(allNewLines[i]);

            // Add to inline: if it's a change, add both (Removed then Added)
            // if it's context, add once
            if (allOldLines[i].Kind == DiffLineKind.Unchanged)
            {
                fileDiff.InlineLines.Add(allOldLines[i]);
            }
            else
            {
                if (allOldLines[i].Kind == DiffLineKind.Removed || allOldLines[i].Kind == DiffLineKind.Added)
                {
                    // Basic Inline logic: Removed first, then Added
                    if (allOldLines[i].Kind == DiffLineKind.Removed)
                        fileDiff.InlineLines.Add(allOldLines[i]);
                    if (allNewLines[i].Kind == DiffLineKind.Added)
                        fileDiff.InlineLines.Add(allNewLines[i]);
                }
                else if (allOldLines[i].Kind == DiffLineKind.Placeholder)
                {
                    if (allNewLines[i].Kind == DiffLineKind.Added)
                        fileDiff.InlineLines.Add(allNewLines[i]);
                }
                else if (allNewLines[i].Kind == DiffLineKind.Placeholder)
                {
                    if (allOldLines[i].Kind == DiffLineKind.Removed)
                        fileDiff.InlineLines.Add(allOldLines[i]);
                }
            }

            fileDiff.AlignedRows.Add(new AlignedDiffRow
            {
                OldLine = block.OldLines[i],
                NewLine = block.NewLines[i]
            });
        }

        fileDiff.Blocks.Add(block);

        return fileDiff;
    }

    public string GenerateInlineDiff(FileDiff diff)
    {
        var lines = new List<string>();

        foreach (var block in diff.Blocks)
        {
            foreach (var line in block.OldLines.Concat(block.NewLines).OrderBy(l =>
                l.OldLineNumber > 0 ? l.OldLineNumber : l.NewLineNumber))
            {
                // Skip placeholders (used for alignment only, not part of actual diff)
                if (line.Kind == DiffLineKind.Placeholder)
                    continue;

                var prefix = line.Kind switch
                {
                    DiffLineKind.Added => "+",
                    DiffLineKind.Removed => "-",
                    DiffLineKind.Unchanged => " ",
                    DiffLineKind.Context => " ",
                    DiffLineKind.Header => "",
                    _ => " "
                };

                // For header lines, include content as-is (no prefix space)
                // For other lines, add prefix and content
                if (line.Kind == DiffLineKind.Header)
                {
                    lines.Add(line.Content);
                }
                else
                {
                    lines.Add($"{prefix} {line.Content}");
                }
            }
        }

        return string.Join("\n", lines);
    }

    public (List<DiffLine> OldLines, List<DiffLine> NewLines) GenerateSideBySideDiff(FileDiff diff)
    {
        var oldLines = new List<DiffLine>();
        var newLines = new List<DiffLine>();

        foreach (var block in diff.Blocks)
        {
            oldLines.AddRange(block.OldLines);
            newLines.AddRange(block.NewLines);
        }

        return (oldLines, newLines);
    }

    public bool IsBinaryDiff(string rawDiff)
    {
        return rawDiff.Contains("Binary files") ||
               rawDiff.Contains("GIT binary patch");
    }

    private static DiffLineKind MapChangeType(ChangeType changeType, bool isNew)
    {
        return changeType switch
        {
            ChangeType.Inserted => DiffLineKind.Added,
            ChangeType.Deleted => DiffLineKind.Removed,
            ChangeType.Modified => isNew ? DiffLineKind.Added : DiffLineKind.Removed,
            ChangeType.Imaginary => DiffLineKind.Placeholder,
            _ => DiffLineKind.Unchanged
        };
    }

    private List<HighlightedSegment> GetSubLineHighlights(List<DiffPlex.DiffBuilder.Model.DiffPiece> subPieces, bool isNew)
    {
        var highlights = new List<HighlightedSegment>();
        int offset = 0;

        // Define theme-aware background colors for intra-line changes
        var theme = _settingsService.GetTheme();
        var isDark = theme == AppTheme.Dark || (theme == AppTheme.System && IsDarkMode());

        // For light theme: slightly darker red/green (more saturated) with black text
        // For dark theme: subtle dark red/green (slightly brighter than very dark) with white text for contrast
        string removedBg = isDark ? "#663333" : "#FFCCCC"; // Subtle dark red for dark theme, darker red for light theme
        string addedBg = isDark ? "#336633" : "#CCFFCC";   // Subtle dark green for dark theme, darker green for light theme
        string textColor = isDark ? "#FFFFFF" : "#000000"; // White text for dark theme, black text for light theme

        foreach (var piece in subPieces)
        {
            if (string.IsNullOrEmpty(piece.Text)) continue;

            if (piece.Type == ChangeType.Deleted || piece.Type == ChangeType.Inserted || piece.Type == ChangeType.Modified)
            {
                highlights.Add(new HighlightedSegment
                {
                    Offset = offset,
                    Length = piece.Text.Length,
                    BackgroundHex = isNew ? addedBg : removedBg,
                    ColorHex = textColor
                });
            }

            offset += piece.Text.Length;
        }

        return highlights;
    }

    private bool IsDarkMode()
    {
        // Cross-platform dark mode detection
        // First try to get from settings service (works on any thread)
        var theme = _settingsService.GetTheme();
        if (theme == AppTheme.Dark)
            return true;
        if (theme == AppTheme.Light)
            return false;

        // System theme - try to get actual value from Avalonia (must be on UI thread)
        try
        {
            var themeVariant = Application.Current?.ActualThemeVariant;
            return themeVariant == ThemeVariant.Dark;
        }
        catch (InvalidOperationException)
        {
            // Not on UI thread - assume light mode as conservative default
            return false;
        }
    }
}
