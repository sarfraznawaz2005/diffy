using System;
using System.Runtime.InteropServices;

namespace Diffy.App.Utilities;

/// <summary>
/// Helper class for platform-appropriate string comparisons, especially for file paths.
/// Handles case-sensitivity differences between Windows (case-insensitive) and Unix-like systems (case-sensitive).
/// </summary>
public static class StringComparisonHelper
{
    /// <summary>
    /// Compares two paths for equality using platform-appropriate case sensitivity.
    /// Windows: Case-insensitive comparison
    /// macOS/Linux: Case-sensitive comparison
    /// </summary>
    public static bool PathEquals(string? path1, string? path2)
    {
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
            return string.Equals(path1, path2);

        // On Windows: use case-insensitive comparison
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);

        // On macOS/Linux: use case-sensitive comparison
        return string.Equals(path1, path2, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a path contains a substring using platform-appropriate case sensitivity.
    /// Windows: Case-insensitive comparison
    /// macOS/Linux: Case-sensitive comparison
    /// </summary>
    public static bool PathContains(string? path, string? value)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(value))
            return false;

        // On Windows: use case-insensitive comparison
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return path.Contains(value, StringComparison.OrdinalIgnoreCase);

        // On macOS/Linux: use case-sensitive comparison
        return path.Contains(value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the appropriate StringComparison for the current platform.
    /// Useful when you need to pass StringComparison to other methods.
    /// </summary>
    public static StringComparison GetPathComparison()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
