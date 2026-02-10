namespace Diffy.Core.Models;

/// <summary>
/// User preferences for filtering the file list.
/// </summary>
public class FilterSettings
{
    /// <summary>
    /// Which file statuses to include in the list.
    /// </summary>
    public List<FileStatusKind> IncludedStatuses { get; set; } = new()
    {
        FileStatusKind.Modified,
        FileStatusKind.New,
        FileStatusKind.Deleted,
        FileStatusKind.Renamed,
        FileStatusKind.Unstaged
    };

    /// <summary>
    /// File extensions to include (e.g., ".cs", ".xaml"). Empty means all.
    /// </summary>
    public List<string> IncludedExtensions { get; set; } = new();

    /// <summary>
    /// Glob patterns to exclude (e.g., "**/bin/**", "**/obj/**").
    /// </summary>
    public List<string> ExcludedPatterns { get; set; } = new()
    {
        "**/bin/**",
        "**/obj/**",
        "**/.vs/**",
        "**/node_modules/**"
    };

    /// <summary>
    /// Whether to show binary files in the list.
    /// </summary>
    public bool ShowBinaryFiles { get; set; } = true;
}
