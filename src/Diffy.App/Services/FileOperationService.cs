using System.Diagnostics;
using System.Runtime.InteropServices;
using Diffy.Core.Interfaces;

namespace Diffy.App.Services;

/// <summary>
/// Service for opening files and folders.
/// </summary>
public class FileOperationService : IFileOperationService
{
    public Task OpenFileAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
        }, ct);
    }

    public Task OpenInEditorAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(filePath))
                return;

            var vsCodePath = FindVsCode();

            // If VS Code not found, try platform-specific fallback editor
            if (string.IsNullOrEmpty(vsCodePath))
            {
                // Platform-specific fallback
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS: Use TextEdit or default editor
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"-a TextEdit \"{filePath}\"",
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux: Try multiple editors in order of preference
                    var editors = new[]
                    {
                        "gedit",
                        "kate",
                        "mousepad",
                        "gedit"
                    };

                    foreach (var editor in editors)
                    {
                        try
                        {
                            var process = Process.Start(new ProcessStartInfo
                            {
                                FileName = editor,
                                Arguments = $"\"{filePath}\"",
                                RedirectStandardOutput = true,
                                UseShellExecute = false
                            });

                            // Wait briefly and check exit code
                            process?.WaitForExit(100);

                            // If exit code is 0, editor was found
                            if (process?.ExitCode == 0)
                                return;
                        }
                        catch
                        {
                            // Continue to next editor
                        }
                    }
                }
            }
        }, ct);
    }

    public Task OpenContainingFolderAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{filePath}\"",
                        UseShellExecute = true
                    });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"-R \"{filePath}\"",
                        UseShellExecute = true
                    });
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{directory}\"",
                        UseShellExecute = true
                    });
                }
            }
        }, ct);
    }

    private static string? FindVsCode()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Check standard install locations
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft VS Code", "Code.exe"),
                // Scoop install
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "scoop", "apps", "vscode", "current", "Code.exe"),
                // Chocolatey install
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft VS Code", "bin", "code.cmd"),
                // Portable install
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "vscode", "Code.exe")
            };

            return possiblePaths.FirstOrDefault(File.Exists);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: Check multiple install locations
            var possiblePaths = new[]
            {
                // Standard install via Visual Studio Code installer
                "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code",
                // User install (dragged to Applications)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Applications", "Visual Studio Code.app", "Contents", "Resources", "app", "bin", "code"),
                // Homebrew install (Apple Silicon)
                "/opt/homebrew/bin/code",
                // Homebrew install (Intel)
                "/usr/local/bin/code",
                // MacPorts install
                "/opt/local/bin/code",
                // nvm install
                Path.Combine(Environment.GetEnvironmentVariable("NVM_HOME") ?? "", "versions", "node", "current", "bin", "code"),
                // Check PATH
                FindCodeInPath()
            };

            return possiblePaths.FirstOrDefault(path => !string.IsNullOrEmpty(path) && File.Exists(path));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: Check multiple install methods
            var possiblePaths = new List<string>();

            // 1. Check PATH first
            var codePath = FindCodeInPath();
            if (!string.IsNullOrEmpty(codePath))
                possiblePaths.Add(codePath);

            // 2. Check standard install locations
            var standardLocations = new[]
            {
                "/usr/bin/code",
                "/usr/local/bin/code",
                "/opt/visual-studio-code/bin/code",
                "/opt/vscode/bin/code",
                "/snap/bin/code",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "bin", "code")
            };
            possiblePaths.AddRange(standardLocations.Where(File.Exists));

            // 3. Check snap install
            var snapPath = Path.Combine("/snap", "code", "current", "usr", "share", "code", "bin", "code");
            if (File.Exists(snapPath))
                possiblePaths.Add(snapPath);

            // 4. Check flatpak install
            var flatpakPath = Path.Combine("/var", "lib", "flatpak", "app", "com.visualstudio.code", "current", "active", "files", "bin", "code");
            if (File.Exists(flatpakPath))
                possiblePaths.Add(flatpakPath);

            // 5. Check AppImage or portable install in common locations
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var portablePaths = new[]
            {
                Path.Combine(homeDir, "VSCode-linux-x64", "bin", "code"),
                Path.Combine(homeDir, "vscode", "bin", "code"),
                Path.Combine(homeDir, ".vscode", "bin", "code"),
                Path.Combine(homeDir, "Applications", "VSCode-linux-x64", "bin", "code")
            };
            possiblePaths.AddRange(portablePaths.Where(File.Exists));

            return possiblePaths.FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Searches for the 'code' executable in the system PATH environment variable.
    /// </summary>
    /// <returns>The full path to the 'code' executable if found; otherwise, null.</returns>
    private static string? FindCodeInPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        var pathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var directories = pathEnv.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "code.exe" : "code";

        foreach (var dir in directories)
        {
            try
            {
                var codePath = Path.Combine(dir.Trim(), executableName);
                if (File.Exists(codePath))
                    return codePath;
            }
            catch
            {
                // Continue searching in case of invalid path characters
            }
        }

        return null;
    }
}

/// <summary>
/// Service for moving files to the system trash.
/// </summary>
public class TrashService : ITrashService
{
    public bool IsSupported => OperatingSystem.IsWindows() ||
                               OperatingSystem.IsMacOS() ||
                               OperatingSystem.IsLinux();

    public Task<bool> MoveToTrashAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return MoveToTrashWindows(filePath);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return MoveToTrashMacOs(filePath);
                }
                else if (OperatingSystem.IsLinux())
                {
                    return MoveToTrashLinux(filePath);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }, ct);
    }

    private static bool MoveToTrashWindows(string filePath)
    {
        try
        {
            // Use Windows API via P/Invoke for reliable Recycle Bin access
            return NativeWindows.MoveToRecycleBin(filePath);
        }
        catch
        {
            // Fallback to permanent deletion if API fails
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool MoveToTrashMacOs(string filePath)
    {
        // Try AppleScript first (most reliable when available)
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e 'tell application \"Finder\" to delete POSIX file \"{filePath}\"'",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                    return true;
            }
        }
        catch
        {
            // AppleScript failed, try fallback
        }

        // Fallback 1: Try using macOS 'trash' command (if installed via Homebrew)
        try
        {
            var trashProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "trash",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (trashProcess != null)
            {
                trashProcess.WaitForExit();
                if (trashProcess.ExitCode == 0)
                    return true;
            }
        }
        catch
        {
            // trash command not available
        }

        // Fallback 2: Direct move to Trash folder
        if (MoveToTrashMacDirect(filePath))
            return true;

        // Last resort: permanent deletion
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Directly moves a file to the macOS Trash folder.
    /// This is a fallback when AppleScript is not available.
    /// </summary>
    private static bool MoveToTrashMacDirect(string filePath)
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var trashDir = Path.Combine(homeDir, ".Trash");

            // Ensure trash directory exists
            Directory.CreateDirectory(trashDir);

            var fileName = Path.GetFileName(filePath);
            var destPath = Path.Combine(trashDir, fileName);

            // Handle duplicates by appending number
            var counter = 1;
            var originalFileName = fileName;
            while (File.Exists(destPath))
            {
                var extension = Path.GetExtension(originalFileName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                fileName = $"{nameWithoutExt}_{counter}{extension}";
                destPath = Path.Combine(trashDir, fileName);
                counter++;
            }

            // Move file to trash
            File.Move(filePath, destPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool MoveToTrashLinux(string filePath)
    {
        // Try multiple trash methods in order of preference

        // Method 1: gio trash (GNOME/GIO-based DEs)
        if (TryMoveToTrashWithCommand("gio", $"trash \"{filePath}\""))
            return true;

        // Method 2: kioclient5 (KDE Plasma 5+)
        if (TryMoveToTrashWithCommand("kioclient5", $"move \"{filePath}\" trash:/"))
            return true;

        // Method 3: kioclient (older KDE)
        if (TryMoveToTrashWithCommand("kioclient", $"move \"{filePath}\" trash:/"))
            return true;

        // Method 4: trash-cli (command-line tool)
        if (TryMoveToTrashWithCommand("trash", $"\"{filePath}\""))
            return true;

        // Method 5: Direct move to ~/.local/share/Trash/
        if (MoveToTrashDirect(filePath))
            return true;

        // Method 6: Fallback to permanent deletion
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to move a file to trash using a specific command.
    /// </summary>
    private static bool TryMoveToTrashWithCommand(string command, string arguments)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Directly moves a file to the XDG trash directory.
    /// This is a fallback when desktop environment tools are not available.
    /// </summary>
    private static bool MoveToTrashDirect(string filePath)
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var trashFilesDir = Path.Combine(homeDir, ".local", "share", "Trash", "files");
            var trashInfoDir = Path.Combine(homeDir, ".local", "share", "Trash", "info");

            // Ensure trash directories exist
            Directory.CreateDirectory(trashFilesDir);
            Directory.CreateDirectory(trashInfoDir);

            var fileName = Path.GetFileName(filePath);
            var destPath = Path.Combine(trashFilesDir, fileName);
            var infoPath = Path.Combine(trashInfoDir, $"{fileName}.trashinfo");

            // Handle duplicates by appending number
            var counter = 1;
            var originalFileName = fileName;
            while (File.Exists(destPath) || File.Exists(infoPath))
            {
                var extension = Path.GetExtension(originalFileName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                fileName = $"{nameWithoutExt}_{counter}{extension}";
                destPath = Path.Combine(trashFilesDir, fileName);
                infoPath = Path.Combine(trashInfoDir, $"{fileName}.trashinfo");
                counter++;
            }

            // Move file to trash
            File.Move(filePath, destPath);

            // Create .trashinfo file (XDG Trash specification)
            var deletionDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            var trashInfoContent = $"[Trash Info]\nPath={filePath}\nDeletionDate={deletionDate}\n";
            File.WriteAllText(infoPath, trashInfoContent);

            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Native Windows API methods for file operations.
/// </summary>
internal static class NativeWindows
{
    // SHFileOperation flags
    private const uint FO_DELETE = 0x0003;
    private const uint FOF_ALLOWUNDO = 0x0040;
    private const uint FOF_NOCONFIRMATION = 0x0010;
    private const uint FOF_NOERRORUI = 0x0400;
    private const uint FOF_SILENT = 0x0004;

    // SHChangeNotify flags
    private const uint SHCNE_DELETE = 0x00000004;
    private const uint SHCNF_PATHW = 0x0005;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>
    /// Moves a file to the Recycle Bin using the Windows Shell API.
    /// </summary>
    public static bool MoveToRecycleBin(string filePath)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        // Ensure the file exists
        if (!File.Exists(filePath))
            return false;

        // Double-null terminate the path as required by SHFileOperation
        var fromPath = filePath + "\0\0";

        var fileOp = new SHFILEOPSTRUCT
        {
            hwnd = IntPtr.Zero,
            wFunc = FO_DELETE,
            pFrom = fromPath,
            pTo = null,
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT),
            fAnyOperationsAborted = false,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = null
        };

        var result = SHFileOperation(ref fileOp);

        if (result == 0)
        {
            // Notify the shell that an item was deleted
            var filePathPtr = Marshal.StringToHGlobalUni(filePath);
            try
            {
                SHChangeNotify(SHCNE_DELETE, SHCNF_PATHW, filePathPtr, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(filePathPtr);
            }
            return true;
        }

        return false;
    }
}
