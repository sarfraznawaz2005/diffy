# Diff Pipeline - Quick Reference Guide

> **Last Updated**: 2026-02-11  
> **Note**: This is a living document. Code is the source of truth if there are discrepancies.

---

## Overview

Diffy's diff pipeline transforms Git repository changes into syntax-highlighted, searchable diffs through 5 phases:

```
Git → Diff Generation → Syntax Highlighting → Highlight Merging → UI Rendering
```

**Typical Processing Time**: 50-200ms for standard files (with caching and throttling)

---

## The 5 Phases

### Phase 1: Git Operations
**Entry Point**: `RepositoryTabViewModel.SelectedFile` → `DiffViewModel.LoadDiffAsync()`

**Process**:
1. Check if binary file (`file.IsBinary`) → stop if true
2. Extract old content: `GitService.GetFileContentAtHeadAsync()` (from HEAD commit)
3. Extract new content: `GitService.GetFileContentAsync()` (from working directory)
4. Both operations run async via `Task.Run()` to avoid blocking UI

**Key Service**: `GitService` with LibGit2Sharp

---

### Phase 2: Diff Generation
**Service**: `DiffService.GenerateDiff()`

**Process**:
1. Use **DiffPlex** library's `SideBySideDiffBuilder`
2. Compare line-by-line with optional whitespace ignore
3. Extract character-level changes for modified lines (SubPieces)
4. Create **three parallel representations**:
   - `OldLines` / `NewLines` - Separate lists for each side
   - `InlineLines` - Flat sequential list for inline view
   - `AlignedRows` - Paired old/new lines for side-by-side view

**Output**: Structured `FileDiff` object with additions/deletions count

**Key Library**: DiffPlex 1.9.0 (Myers diff algorithm)

---

### Phase 3: Syntax Highlighting
**Service**: `SyntaxHighlightingService.HighlightFileDiffAsync()`

**Process**:
1. Select TextMate grammar based on file extension
2. Load theme (Light/Dark/System)
3. **Selective tokenization** - Only changed lines + context (±5 lines)
4. Tokenize line-by-line with parser state carried forward
5. Match token scopes against theme rules for colors
6. Store as `HighlightedSegment` list per line

**Optimization**: 
- Cache tokenization results in `_highlightCache` (LRU, max 100 entries)
- Cache key: `{contentHash}|{themeName}|{scope}`
- Only reprocess if content/theme changes

**Key Library**: TextMateSharp + AvaloniaEdit.TextMate

---

### Phase 4: Highlight Merging
**Method**: `MergeHighlights()` using **Interval Tree**

**Challenge**: Merge three highlight types without overlap:
1. **Diff highlights** (intra-line change backgrounds) - highest priority
2. **Search highlights** (yellow backgrounds)
3. **Syntax highlights** (code token colors)

**Algorithm**:
1. Build `IntervalTree` for diff and syntax highlights
2. Collect all segment boundaries (split points)
3. For each tile between boundaries:
   - Query IntervalTree for overlapping segments
   - Apply priority rules (diff > search > syntax)
   - Create merged segment

**Complexity**: O(n log n + m log n) where n=segments, m=tiles

**Priority Rules**:
- Background: Diff > Search > Syntax
- Foreground: Use syntax color unless diff background present
- Styles: Bold/Italic from syntax highlighting

---

### Phase 5: UI Rendering
**Views**: `RepositoryTabView.axaml`

#### Inline View
- **Data Source**: `CurrentDiff.InlineLines`
- **Structure**: ListBox with virtualization
- **Styling**: Green (added), Red (removed), Transparent (unchanged)
- **Control**: `HighlightedTextBlock` renders each line with colored segments

#### Side-by-Side View
- **Data Source**: `CurrentDiff.AlignedRows`
- **Structure**: ListBox with two-column grid
- **Synchronized Scrolling**: Both sides in same ListBox item
- **Alignment**: Placeholder lines ensure equal lengths

#### DiffMinimapControl
- Bird's-eye view overlay on right edge
- Colored bars show change locations (green/red)
- Click to jump to position
- Tooltip shows line number and nearby changes

---

## Key Components

### DiffService (`Diffy.App/Services/DiffService.cs`)
```csharp
FileDiff GenerateDiff(string oldContent, string newContent, string filePath, bool ignoreWhitespace);
List<AlignedDiffRow> AlignDiffForSideBySide(FileDiff diff);
```
**Responsibilities**: Diff generation, alignment for side-by-side, intra-line highlighting

---

### SyntaxHighlightingService (`Diffy.App/Services/SyntaxHighlightingService.cs`)
```csharp
Task HighlightFileDiffAsync(FileDiff diff, string filePath, string searchQuery);
```
**Responsibilities**: TextMate tokenization, search highlighting, theme management

---

### GitService (`Diffy.App/Services/GitService.cs`)
```csharp
Task<string> GetFileContentAtHeadAsync(string repoPath, string filePath);
Task<string> GetFileContentAsync(string repoPath, string filePath);
Task<List<FileStatus>> GetChangedFilesAsync(string repoPath);
```
**Responsibilities**: Git operations via LibGit2Sharp wrapper

---

### HighlightedTextBlock (`Diffy.App/Controls/HighlightedTextBlock.cs`)
Custom Avalonia control that renders text with multiple colored segments.

**Properties**:
- `SourceText` - Plain string
- `Highlights` - List of `HighlightedSegment`

**Rendering**: Creates multiple `Run` elements with individual styling

---

## Data Models

### FileDiff
```csharp
public class FileDiff
{
    public string FilePath { get; set; }
    public bool IsBinary { get; set; }
    public List<DiffLine> InlineLines { get; set; }        // For inline view
    public List<AlignedDiffRow> AlignedRows { get; set; }  // For side-by-side
    public int Additions { get; set; }
    public int Deletions { get; set; }
}
```

### DiffLine
```csharp
public class DiffLine
{
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public string Content { get; set; }
    public DiffLineKind Kind { get; set; }  // Added, Removed, Unchanged, Placeholder
    public List<HighlightedSegment> Highlights { get; set; }
}
```

### HighlightedSegment
```csharp
public class HighlightedSegment
{
    public int Offset { get; set; }
    public int Length { get; set; }
    public string ColorHex { get; set; }         // Foreground
    public string BackgroundHex { get; set; }    // Background
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
}
```

---

## Performance Optimizations

### 1. Virtualization
- ListBox controls use Avalonia's built-in virtualization
- Only visible rows rendered
- Critical for diffs with 1000+ lines

### 2. Throttling & Debouncing
- `SelectedFile` changes: 100ms throttle
- Search queries: 200ms throttle  
- File watcher: 300ms debounce

### 3. Caching

**In ViewModel**:
- `_diffContentCache` - Raw diff strings (LRU)
- `_fullContentCache` - Full file content (LRU)

**In SyntaxHighlightingService**:
- `_highlightCache` - Tokenization results (LRU, max 100)
- `_registries` - TextMate grammar per theme

### 4. Selective Tokenization
- Only tokenize changed lines + 5-line context buffer
- Group continuous lines to maintain parser state
- Dramatically reduces work for large files

### 5. Progressive Highlighting (Large Files > 500 lines)
- Highlight first 200 lines immediately
- Process remaining in 100-line chunks in background
- Incremental UI updates via `INotifyPropertyChanged`

### 6. Repository Caching
**GitRepositoryWrapper**:
- Reference counting for shared repository instances
- Thread-safe access via locks
- Automatic disposal when refcount reaches zero

---

## Navigation Features

### Jump to Next/Previous Change
**Shortcuts**: F7 (next), Shift+F7 (previous)

**Implementation**:
1. Scan from last jump index for next Added/Removed line
2. Wrap around if needed
3. Fire `ScrollRequested` event
4. View scrolls to index using `ScrollIntoView()`

---

## Common Tasks

### Adding a New Highlight Type
1. Create `HighlightedSegment` objects with unique `BackgroundHex`
2. Add to `line.Highlights` list
3. Update `MergeHighlights()` priority rules if needed
4. No UI changes required

### Changing Diff Colors
**Theme colors**: Defined in `App.axaml` theme dictionaries as DynamicResources
**Intra-line colors**: Hardcoded in `DiffService.GetSubLineHighlights()`
**Minimap colors**: Defined in `DiffMinimapControl` constructor

### Supporting New File Types
TextMate handles automatically if grammar exists in package. No code changes needed.

### Improving Performance
**Current bottlenecks**:
1. TextMate tokenization (mitigated by selective tokenization + caching)
2. Minimap rendering on scroll (full redraw each time)

**Future optimizations**:
- Cache minimap as bitmap, redraw only viewport indicator
- Viewport-based progressive highlighting for very large files
- Per-line syntax caching for repeated lines

---

## Debugging Tips

### Common Issues

**Highlights not appearing**:
- Check segment Offset/Length within `line.Content.Length`
- Verify ColorHex format (must parse with `Color.TryParse`)
- Inspect `line.Highlights` after `MergeHighlights()`

**Incorrect alignment in side-by-side**:
- Verify `AlignedRows` count matches
- Check for Placeholder lines (`Kind = DiffLineKind.Placeholder`)

**Search highlighting not visible**:
- Verify `ApplySearchHighlighting()` called before `MergeHighlights()`
- Check segment priorities in merge output

**Performance slow**:
- Profile with Avalonia DevTools (F12 in Debug)
- Verify ListBox virtualization active
- Check syntax highlighting cache hit rate (should be >80%)
- Monitor selective tokenization (should only process changed lines)

### Logging Points
Add logging at these key points:
1. `LoadDiffAsync` start - file path and size
2. `DiffService.GenerateDiff` complete - timing
3. `SyntaxHighlightingService` start - cache hit/miss
4. `MergeHighlights` complete - segment count
5. Search operations - query, cache hit rate

---

## Architecture Principles

### Separation of Concerns
- **GitService** - Git operations only
- **DiffService** - Diff generation only
- **SyntaxHighlightingService** - Highlighting only
- **ViewModel** - Orchestration and state
- **View** - Rendering only

### Data Flow
Unidirectional: Git → Diff → Highlight → Merge → UI

No backwards dependencies.

### Async/Await
All I/O operations async with `CancellationToken` support.

UI updates on main thread via ReactiveUI scheduler.

---

## Key Libraries

| Library | Version | Purpose |
|---------|---------|---------|
| **DiffPlex** | 1.9.0 | Myers diff algorithm |
| **LibGit2Sharp** | 0.31.0 | Git operations |
| **TextMateSharp** | via AvaloniaEdit.TextMate | Syntax highlighting |
| **ReactiveUI** | 11.3.8 | Reactive MVVM |
| **Avalonia** | 11.3.11 | Cross-platform UI |

---

## Glossary

| Term | Definition |
|------|------------|
| **Aligned Rows** | Paired old/new lines for side-by-side rendering |
| **Intra-line Highlighting** | Character-level changes within modified lines |
| **Inline View** | Single-column diff mode |
| **Interval Tree** | Data structure for O(log n) overlap queries |
| **Minimap** | Bird's-eye view scrollbar with change indicators |
| **Placeholder Line** | Empty line for alignment (line number -1) |
| **Selective Tokenization** | Only tokenize changed lines + context |
| **Side-by-Side View** | Two-column parallel diff mode |
| **SubPieces** | DiffPlex term for character-level changes |
| **Virtualization** | Render only visible items for performance |

---

## Quick Reference

### Entry Points
- User selects file → `RepositoryTabViewModel.SelectedFile` changes
- Triggers → `DiffViewModel.LoadDiffAsync()`
- Calls → `GitService` → `DiffService` → `SyntaxHighlightingService`
- Updates → `CurrentDiff` property
- UI binds → `InlineLines` or `AlignedRows`

### File Locations
- DiffService: `src/Diffy.App/Services/DiffService.cs`
- SyntaxHighlightingService: `src/Diffy.App/Services/SyntaxHighlightingService.cs`
- GitService: `src/Diffy.App/Services/GitService.cs`
- DiffViewModel: `src/Diffy.App/ViewModels/DiffViewModel.cs`
- HighlightedTextBlock: `src/Diffy.App/Controls/HighlightedTextBlock.cs`
- DiffMinimapControl: `src/Diffy.App/Controls/DiffMinimapControl.cs`
- Models: `src/Diffy.Core/Models/DiffModels.cs`

---

**For detailed architecture information, see AGENTS.md**
