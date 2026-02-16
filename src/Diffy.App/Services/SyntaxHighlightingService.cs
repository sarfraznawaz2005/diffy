using System.Collections.Concurrent;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using Diffy.Core.Models;
using TextMateSharp.Grammars;

namespace Diffy.App.Services;

public interface ISyntaxHighlightingService
{
    Task HighlightFileDiffAsync(FileDiff diff, string oldContent, string newContent, string? searchQuery = null);
    Task HighlightFileDiffProgressiveAsync(FileDiff diff, string oldContent, string newContent, string? searchQuery = null, int? viewportStart = null, int? viewportEnd = null, Action<int, int>? onChunkComplete = null, CancellationToken cancellationToken = default);
}

public class SyntaxHighlightingService : ISyntaxHighlightingService
{
    private readonly Diffy.Core.Interfaces.ISettingsService _settingsService;
    private readonly ConcurrentDictionary<ThemeName, RegistryOptions> _registries = new();
    private readonly ConcurrentDictionary<string, (Dictionary<int, List<HighlightedSegment>> Data, long Timestamp)> _highlightCache = new();
    private const int MaxCacheSize = 100;
    private long _cacheTimestamp;
    private const int ProgressiveHighlightingChunkSize = 100;
    private ThemeName? _lastThemeName;

    public SyntaxHighlightingService(Diffy.Core.Interfaces.ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // Subscribe to theme changes to clear registry cache
        _settingsService.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        // Clear registry cache when theme changes to force reload with new theme colors
        _registries.Clear();
        _highlightCache.Clear();
        _lastThemeName = null;
    }

    private RegistryOptions GetRegistryOptions()
    {
        var appTheme = _settingsService.GetTheme();
        ThemeName themeName;

        if (appTheme == Diffy.Core.Interfaces.AppTheme.Light)
        {
            themeName = ThemeName.LightPlus;
        }
        else
        {
            themeName = ThemeName.DarkPlus;
        }

        // If theme changed since last call, clear caches
        // Note: Always clear on first call when _lastThemeName is null
        if (!_lastThemeName.HasValue || _lastThemeName.Value != themeName)
        {
            _registries.Clear();
            _highlightCache.Clear();
        }
        _lastThemeName = themeName;

        return _registries.GetOrAdd(themeName, name => new RegistryOptions(name));
    }

    public async Task HighlightFileDiffAsync(FileDiff diff, string oldContent, string newContent, string? searchQuery = null)
    {
        await HighlightFileDiffProgressiveAsync(diff, oldContent, newContent, searchQuery, null, null, null, default);
    }

    public async Task HighlightFileDiffProgressiveAsync(
        FileDiff diff,
        string oldContent,
        string newContent,
        string? searchQuery = null,
        int? viewportStart = null,
        int? viewportEnd = null,
        Action<int, int>? onChunkComplete = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registryOptions = GetRegistryOptions();
            var extension = Path.GetExtension(diff.FilePath).ToLower();
            var scope = registryOptions.GetScopeByExtension(extension);

            // Tokenize all lines (not selective) to support side-by-side view which shows entire file
            // Note: GetHighlights tokenizes entire content, which is needed because
            // DiffPlex's SideBySideDiffBuilder includes all lines, not just changed hunks
            var oldTask = Task.Run(() => GetHighlights(oldContent, scope, registryOptions), cancellationToken);
            var newTask = Task.Run(() => GetHighlights(newContent, scope, registryOptions), cancellationToken);

            await Task.WhenAll(oldTask, newTask);

            var oldHighlights = await oldTask;
            var newHighlights = await newTask;

            // Apply search highlighting manually after syntax highlighting
            if (!string.IsNullOrEmpty(searchQuery))
            {
                ApplySearchHighlighting(oldHighlights, oldContent, searchQuery);
                ApplySearchHighlighting(newHighlights, newContent, searchQuery);
            }

            // Determine visible line range
            var visibleStart = viewportStart ?? 0;
            var visibleEnd = viewportEnd ?? Math.Max(diff.InlineLines.Count - 1, 0);

            // Process in two phases: visible first, then background
            await ProcessVisibleLinesAsync(diff, oldHighlights, newHighlights, visibleStart, visibleEnd, cancellationToken);

            // Process remaining lines in background if viewport was specified
            if (viewportStart.HasValue && viewportEnd.HasValue)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessRemainingLinesAsync(diff, oldHighlights, newHighlights, visibleStart, visibleEnd, onChunkComplete, cancellationToken);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background highlighting failed: {ex.Message}");
                    }
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during rapid file switching - rethrow to let caller handle
            throw;
        }
        catch (Exception ex)
        {
            // Log unexpected errors but don't crash - caller will show unhighlighted diff
            System.Diagnostics.Debug.WriteLine($"Syntax highlighting failed for {diff.FilePath}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            // Don't rethrow - allow diff to display without highlighting
        }
    }

    private async Task ProcessVisibleLinesAsync(
        FileDiff diff,
        Dictionary<int, List<HighlightedSegment>> oldHighlights,
        Dictionary<int, List<HighlightedSegment>> newHighlights,
        int visibleStart,
        int visibleEnd,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Apply to InlineLines (visible range only)
            for (int i = visibleStart; i <= visibleEnd && i < diff.InlineLines.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = diff.InlineLines[i];
                ApplyHighlightsToLine(line, oldHighlights, newHighlights);
            }

            // Apply to Blocks (Side-by-Side)
            foreach (var block in diff.Blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var line in block.OldLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (line.OldLineNumber > 0 && oldHighlights.TryGetValue(line.OldLineNumber, out var segments))
                        MergeHighlights(line, segments);
                }
                foreach (var line in block.NewLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (line.NewLineNumber > 0 && newHighlights.TryGetValue(line.NewLineNumber, out var segments))
                        MergeHighlights(line, segments);
                }
            }
        }, cancellationToken);
    }

    private async Task ProcessRemainingLinesAsync(
        FileDiff diff,
        Dictionary<int, List<HighlightedSegment>> oldHighlights,
        Dictionary<int, List<HighlightedSegment>> newHighlights,
        int visibleStart,
        int visibleEnd,
        Action<int, int>? onChunkComplete,
        CancellationToken cancellationToken)
    {
        const int chunkSize = ProgressiveHighlightingChunkSize;
        var totalLines = diff.InlineLines.Count;

        // Process lines before viewport
        for (int i = 0; i < visibleStart; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkEnd = Math.Min(i + chunkSize - 1, visibleStart - 1);
            await ProcessChunkAsync(diff, oldHighlights, newHighlights, i, chunkEnd, cancellationToken);
            onChunkComplete?.Invoke(i, chunkEnd);
        }

        // Process lines after viewport
        for (int i = visibleEnd + 1; i < totalLines; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkEnd = Math.Min(i + chunkSize - 1, totalLines - 1);
            await ProcessChunkAsync(diff, oldHighlights, newHighlights, i, chunkEnd, cancellationToken);
            onChunkComplete?.Invoke(i, chunkEnd);
        }
    }

    private async Task ProcessChunkAsync(
        FileDiff diff,
        Dictionary<int, List<HighlightedSegment>> oldHighlights,
        Dictionary<int, List<HighlightedSegment>> newHighlights,
        int chunkStart,
        int chunkEnd,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            for (int i = chunkStart; i <= chunkEnd && i < diff.InlineLines.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = diff.InlineLines[i];
                ApplyHighlightsToLine(line, oldHighlights, newHighlights);
            }
        }, cancellationToken);
    }

    private void ApplyHighlightsToLine(
        DiffLine line,
        Dictionary<int, List<HighlightedSegment>> oldHighlights,
        Dictionary<int, List<HighlightedSegment>> newHighlights)
    {
        // Determine which highlights dictionary to use based on which line number is valid
        // Don't use line.Kind because "Modified" lines can have Kind=Removed but NewLineNumber>0
        Dictionary<int, List<HighlightedSegment>>? sourceHighlights = null;
        int? lineNum = null;

        // Priority: If NewLineNumber is valid, use newHighlights (NEW content)
        // Otherwise, if OldLineNumber is valid, use oldHighlights (OLD content)
        if (line.NewLineNumber > 0)
        {
            sourceHighlights = newHighlights;
            lineNum = line.NewLineNumber;
        }
        else if (line.OldLineNumber > 0)
        {
            sourceHighlights = oldHighlights;
            lineNum = line.OldLineNumber;
        }

        if (sourceHighlights != null && lineNum.HasValue && sourceHighlights.TryGetValue(lineNum.Value, out var segments))
        {
            MergeHighlights(line, segments);
        }
    }

    private void ApplySearchHighlighting(Dictionary<int, List<HighlightedSegment>> highlights, string content, string query)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(query)) return;

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            int lineNum = i + 1;
            var lineContent = lines[i];
            var searchIndices = new List<int>();
            int index = lineContent.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            while (index != -1)
            {
                searchIndices.Add(index);
                index = lineContent.IndexOf(query, index + query.Length, StringComparison.OrdinalIgnoreCase);
            }

            if (searchIndices.Count == 0) continue;

            if (!highlights.TryGetValue(lineNum, out var segments))
            {
                segments = new List<HighlightedSegment>();
                highlights[lineNum] = segments;
            }

            // Use Interval Tree for efficient segment splitting
            ApplySearchHighlightsWithIntervalTree(segments, searchIndices, query.Length);
        }
    }

    private void ApplySearchHighlightsWithIntervalTree(List<HighlightedSegment> segments, List<int> searchIndices, int queryLength)
    {
        if (segments.Count == 0)
        {
            // No existing segments, just add search highlights
            foreach (var start in searchIndices)
            {
                segments.Add(new HighlightedSegment
                {
                    Offset = start,
                    Length = queryLength,
                    BackgroundHex = "#FFFF00",
                    ColorHex = "#000000"
                });
            }
            segments.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            return;
        }

        // Build Interval Tree from existing segments
        var intervals = segments
            .Select((seg, idx) => new Interval<(HighlightedSegment Segment, int Index)>
            {
                Start = seg.Offset,
                End = seg.Offset + seg.Length,
                Value = (seg, idx)
            })
            .ToList();

        var tree = new IntervalTree<(HighlightedSegment Segment, int Index)>(intervals);

        // Collect all split points
        var points = tree.GetAllSplitPoints();
        foreach (var start in searchIndices)
        {
            points.Add(start);
            points.Add(start + queryLength);
        }

        // Build new segments based on split points
        var newSegments = new List<HighlightedSegment>();
        var pointList = points.ToList();

        for (int i = 0; i < pointList.Count - 1; i++)
        {
            int start = pointList[i];
            int end = pointList[i + 1];
            if (start >= end) continue;

            // Check if this range is covered by a search highlight
            bool isSearchHighlight = searchIndices.Any(si => si <= start && si + queryLength >= end);

            // Find the original segment covering this range
            var overlapping = tree.Query(start, end);
            var originalSeg = overlapping
                .OrderByDescending(x => x.Value.Index)
                .Select(x => x.Value.Segment)
                .FirstOrDefault();

            if (isSearchHighlight)
            {
                // Apply yellow background for search highlights
                newSegments.Add(new HighlightedSegment
                {
                    Offset = start,
                    Length = end - start,
                    BackgroundHex = "#FFFF00",
                    ColorHex = "#000000",
                    IsBold = originalSeg?.IsBold ?? false,
                    IsItalic = originalSeg?.IsItalic ?? false
                });
            }
            else if (originalSeg != null)
            {
                // Keep original segment styling
                newSegments.Add(new HighlightedSegment
                {
                    Offset = start,
                    Length = end - start,
                    ColorHex = originalSeg.ColorHex,
                    BackgroundHex = originalSeg.BackgroundHex,
                    IsBold = originalSeg.IsBold,
                    IsItalic = originalSeg.IsItalic
                });
            }
        }

        // Replace segments with new merged list
        segments.Clear();
        segments.AddRange(newSegments);
    }

    private void MergeHighlights(DiffLine line, List<HighlightedSegment> syntaxHighlights)
    {
        if (line.Highlights == null)
        {
            line.Highlights = syntaxHighlights;
            return;
        }

        // Use Interval Trees for efficient overlap queries
        // Build trees for both diff and syntax highlights
        var diffIntervals = line.Highlights
            .Select((h, index) => new Interval<(HighlightedSegment Segment, int Priority)>
            {
                Start = h.Offset,
                End = h.Offset + h.Length,
                Value = (h, index) // Higher index = higher priority (added later)
            })
            .ToList();

        var syntaxIntervals = syntaxHighlights
            .Select((h, index) => new Interval<(HighlightedSegment Segment, int Priority)>
            {
                Start = h.Offset,
                End = h.Offset + h.Length,
                Value = (h, index)
            })
            .ToList();

        var diffTree = new IntervalTree<(HighlightedSegment Segment, int Priority)>(diffIntervals);
        var syntaxTree = new IntervalTree<(HighlightedSegment Segment, int Priority)>(syntaxIntervals);

        // Collect all unique split points from both trees
        var diffPoints = diffTree.GetAllSplitPoints();
        var syntaxPoints = syntaxTree.GetAllSplitPoints();

        var points = new SortedSet<int>(diffPoints);
        points.UnionWith(syntaxPoints);
        points.Add(0);
        points.Add(line.Content.Length);

        var merged = new List<HighlightedSegment>();
        var pointList = points.ToList();

        for (int i = 0; i < pointList.Count - 1; i++)
        {
            int start = pointList[i];
            int end = pointList[i + 1];
            if (start >= end) continue;

            // Query both trees for overlapping intervals
            var overlappingDiffs = diffTree.Query(start, end);
            var overlappingSyntax = syntaxTree.Query(start, end);

            // Get the highest priority segment from each type
            var diffSeg = overlappingDiffs
                .OrderByDescending(x => x.Value.Priority)
                .Select(x => x.Value.Segment)
                .FirstOrDefault();

            var syntaxSeg = overlappingSyntax
                .OrderByDescending(x => x.Value.Priority)
                .Select(x => x.Value.Segment)
                .FirstOrDefault();

            if (diffSeg != null || syntaxSeg != null)
            {
                merged.Add(new HighlightedSegment
                {
                    Offset = start,
                    Length = end - start,
                    // Priority: Diff background > Syntax background > null
                    BackgroundHex = diffSeg?.BackgroundHex ?? syntaxSeg?.BackgroundHex,
                    // If there's a diff background, use diff's text color (black for readability on colored backgrounds)
                    // Otherwise, use the syntax color
                    ColorHex = diffSeg?.BackgroundHex != null
                        ? diffSeg?.ColorHex
                        : (syntaxSeg?.ColorHex ?? diffSeg?.ColorHex),
                    IsBold = syntaxSeg?.IsBold ?? diffSeg?.IsBold ?? false,
                    IsItalic = syntaxSeg?.IsItalic ?? diffSeg?.IsItalic ?? false
                });
            }
        }

        line.Highlights = merged;
    }

    private Dictionary<int, List<HighlightedSegment>> GetHighlights(string content, string scope, RegistryOptions registryOptions)
    {
        var result = new Dictionary<int, List<HighlightedSegment>>();
        if (string.IsNullOrEmpty(content)) return result;

        var document = new TextDocument(content);

        // Use TextMateSharp Registry to get grammar and theme
        var registry = new TextMateSharp.Registry.Registry(registryOptions);
        var grammar = registry.LoadGrammar(scope);
        var theme = registry.GetTheme();

        if (grammar == null || theme == null) return result;

        TextMateSharp.Grammars.IStateStack? state = null;

        for (int i = 1; i <= document.LineCount; i++)
        {
            var line = document.GetLineByNumber(i);
            var lineContent = document.GetText(line.Offset, line.Length);

            var lineHighlights = new List<HighlightedSegment>();
            var tokenizeResult = grammar.TokenizeLine(lineContent, state, TimeSpan.FromSeconds(1));
            state = tokenizeResult.RuleStack;

            foreach (var token in tokenizeResult.Tokens)
            {
                var startIndex = token.StartIndex;
                var endIndex = token.EndIndex;
                var scopes = token.Scopes;

                if (startIndex >= endIndex) continue;

                // Try to find the best matching theme rule
                int foreground = -1;
                var rules = theme.Match(scopes);
                if (rules != null)
                {
                    foreach (var rule in rules)
                    {
                        if (rule.foreground > 0)
                        {
                            foreground = rule.foreground;
                            break;
                        }
                    }
                }

                if (foreground > 0)
                {
                    var colorHex = theme.GetColor(foreground);
                    lineHighlights.Add(new HighlightedSegment
                    {
                        Offset = startIndex,
                        Length = endIndex - startIndex,
                        ColorHex = colorHex
                    });
                }
            }

            result[i] = lineHighlights;
        }

        return result;
    }

    private HashSet<int> GetChangedLineNumbers(FileDiff diff, bool isOldContent)
    {
        var changedLines = new HashSet<int>();

        foreach (var line in diff.InlineLines)
        {
            if (line.Kind == DiffLineKind.Added || line.Kind == DiffLineKind.Removed || line.Kind == DiffLineKind.Unchanged)
            {
                var lineNumber = isOldContent ? line.OldLineNumber : line.NewLineNumber;
                if (lineNumber > 0)
                    changedLines.Add(lineNumber);
            }
        }

        return changedLines;
    }

    private Dictionary<int, List<HighlightedSegment>> GetSelectiveHighlights(string content, HashSet<int> changedLineNumbers, string scope, RegistryOptions registryOptions)
    {
        var result = new Dictionary<int, List<HighlightedSegment>>();
        if (string.IsNullOrEmpty(content) || changedLineNumbers.Count == 0) return result;

        // Generate cache key using content length + hash to reduce collision probability
        // Using a combination significantly reduces collision risk vs hash alone
        var contentHash = content.GetHashCode();
        var contentLength = content.Length;
        var themeName = _settingsService.GetTheme().ToString();
        var cacheKey = $"{contentHash:X8}|{contentLength}|{themeName}|{scope}";

        // Check cache first
        if (_highlightCache.TryGetValue(cacheKey, out var cached))
            return cached.Data;

        var document = new TextDocument(content);
        var totalLines = document.LineCount;

        // Add context buffer (5 lines before/after each changed line)
        const int contextBuffer = 5;
        var linesToProcess = new HashSet<int>();

        foreach (var changedLine in changedLineNumbers)
        {
            var startLine = Math.Max(1, changedLine - contextBuffer);
            var endLine = Math.Min(totalLines, changedLine + contextBuffer);

            for (int i = startLine; i <= endLine; i++)
                linesToProcess.Add(i);
        }

        // Group lines into continuous ranges to maintain parser state
        var ranges = GroupLinesIntoRanges(linesToProcess);

        // Use TextMateSharp Registry to get grammar and theme
        var registry = new TextMateSharp.Registry.Registry(registryOptions);
        var grammar = registry.LoadGrammar(scope);
        var theme = registry.GetTheme();

        if (grammar == null || theme == null) return result;

        // Process each range
        foreach (var range in ranges)
        {
            TextMateSharp.Grammars.IStateStack? state = null;

            // Tokenize from the start of range to maintain state
            for (int i = range.Start; i <= range.End; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineContent = document.GetText(line.Offset, line.Length);

                var lineHighlights = new List<HighlightedSegment>();
                var tokenizeResult = grammar.TokenizeLine(lineContent, state, TimeSpan.FromSeconds(1));
                state = tokenizeResult.RuleStack;

                foreach (var token in tokenizeResult.Tokens)
                {
                    var startIndex = token.StartIndex;
                    var endIndex = token.EndIndex;
                    var scopes = token.Scopes;

                    if (startIndex >= endIndex) continue;

                    // Try to find the best matching theme rule
                    int foreground = -1;
                    var rules = theme.Match(scopes);
                    if (rules != null)
                    {
                        foreach (var rule in rules)
                        {
                            if (rule.foreground > 0)
                            {
                                foreground = rule.foreground;
                                break;
                            }
                        }
                    }

                    if (foreground > 0)
                    {
                        var colorHex = theme.GetColor(foreground);
                        lineHighlights.Add(new HighlightedSegment
                        {
                            Offset = startIndex,
                            Length = endIndex - startIndex,
                            ColorHex = colorHex
                        });
                    }
                }

                // Only store if this line was originally requested
                if (linesToProcess.Contains(i))
                    result[i] = lineHighlights;
            }
        }

        // Cache the result with FIFO eviction
        EvictCacheIfNeeded();
        _highlightCache.TryAdd(cacheKey, (result, Interlocked.Increment(ref _cacheTimestamp)));

        return result;
    }

    private void EvictCacheIfNeeded()
    {
        if (_highlightCache.Count >= MaxCacheSize)
        {
            // Evict the entry with the lowest timestamp (oldest)
            string? oldestKey = null;
            long oldestTimestamp = long.MaxValue;
            foreach (var kvp in _highlightCache)
            {
                if (kvp.Value.Timestamp < oldestTimestamp)
                {
                    oldestTimestamp = kvp.Value.Timestamp;
                    oldestKey = kvp.Key;
                }
            }
            if (oldestKey != null)
                _highlightCache.TryRemove(oldestKey, out _);
        }
    }

    private List<(int Start, int End)> GroupLinesIntoRanges(HashSet<int> lines)
    {
        if (lines.Count == 0) return new List<(int, int)>();

        var sortedLines = lines.OrderBy(x => x).ToList();
        var ranges = new List<(int, int)>();
        var currentStart = sortedLines[0];
        var currentEnd = sortedLines[0];

        for (int i = 1; i < sortedLines.Count; i++)
        {
            if (sortedLines[i] == currentEnd + 1)
            {
                currentEnd = sortedLines[i];
            }
            else
            {
                ranges.Add((currentStart, currentEnd));
                currentStart = sortedLines[i];
                currentEnd = sortedLines[i];
            }
        }

        ranges.Add((currentStart, currentEnd));
        return ranges;
    }
}
