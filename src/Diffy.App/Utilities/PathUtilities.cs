using System;
using System.IO;

namespace Diffy.App.Utilities;

/// <summary>
/// Utility methods for path normalization and comparison.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Normalizes a path for comparison by removing trailing separators.
    /// This provides consistent path comparison while preserving the original path format.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path with trailing separators removed.</returns>
    public static string NormalizePathForComparison(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Remove trailing separators for consistent comparison
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Normalizes a path for Git config by converting backslashes to forward slashes
    /// and removing trailing separators.
    /// Git on Windows prefers forward slashes for paths in configuration.
    /// </summary>
    /// <param name="path">The path to normalize for Git.</param>
    /// <returns>The Git-normalized path with forward slashes and no trailing separators.</returns>
    public static string NormalizePathForGit(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Normalize for comparison first to remove trailing separators
        var normalized = NormalizePathForComparison(path);

        // Convert to forward slashes for Git config (Git on Windows prefers forward slashes)
        return normalized.Replace("\\", "/");
    }
}
