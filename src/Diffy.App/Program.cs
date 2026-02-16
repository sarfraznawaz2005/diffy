using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Svg;

namespace Diffy.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
#if WINDOWS
    [STAThread]
#endif
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogException(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => LogException(e.Exception, "TaskScheduler.UnobservedTaskException");

        try
        {
            // Use cross-platform single instance check via file lock
            var lockFilePath = GetSingleInstanceLockPath();

            var stream = CreateSingleInstanceLock(lockFilePath);
            if (stream == null)
            {
                // Another instance is already running
                var singleInstanceService = new Services.SingleInstanceService();
                var fullArgs = args.Length > 0 ? string.Join(" ", args) : string.Empty;
                singleInstanceService.SendArgsAsync(fullArgs)
                    .Wait(TimeSpan.FromSeconds(3));
                return;
            }

            // Dispose lock file stream when app exits - wrap in using to ensure cleanup
            // FileOptions.DeleteOnClose ensures lock file is deleted on crash
            using (var disposable = new LockFileDisposable(stream))
            {
                // Start app
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
        }
        catch (Exception ex)
        {
            LogException(ex, "MainLoop");
        }
    }

    private static void LogException(Exception? ex, string source)
    {
        if (ex == null) return;

        try
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "errorlog.txt");
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] CRASH DETECTED\n" +
                          $"Exception: {ex.GetType().Name}\n" +
                          $"Message: {ex.Message}\n" +
                          $"Stack Trace:\n{ex.StackTrace}\n" +
                          new string('-', 80) + "\n\n";

            System.IO.File.AppendAllText(logPath, message);
        }
        catch
        {
            // Fail silently if we can't even write log
        }
    }

    /// <summary>
    /// Logs a message to the errorlog.txt file. Thread-safe.
    /// </summary>
    public static void Log(string message, string source = "DEBUG")
    {
        // Write log on background thread to avoid blocking UI thread
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "errorlog.txt");
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}\n" +
                               new string('-', 80) + "\n\n";

                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch
            {
                // Fail silently if we can't write log
            }
        });
    }

    private static System.IO.FileStream? CreateSingleInstanceLock(string lockFilePath)
    {
        try
        {
            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(lockFilePath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // Check for stale lock file (if file exists but we can open it, previous instance crashed)
            if (System.IO.File.Exists(lockFilePath))
            {
                try
                {
                    // Try to open with write access - if successful, it's stale
                    using var testStream = System.IO.File.Open(
                        lockFilePath,
                        System.IO.FileMode.Open,
                        System.IO.FileAccess.ReadWrite,
                        System.IO.FileShare.None);

                    // We got it! Delete stale lock and continue
                    System.Diagnostics.Debug.WriteLine("Found stale lock file - deleting");
                    testStream.Dispose();
                    System.IO.File.Delete(lockFilePath);
                }
                catch (System.IO.IOException)
                {
                    // File is locked by another instance - legitimate single instance
                    System.Diagnostics.Debug.WriteLine("Lock file held by another instance");
                    return null;
                }
            }

            // Create new lock file with DeleteOnClose for crash-proof cleanup
            // FileOptions.DeleteOnClose ensures file is deleted when stream is closed/disposed
            // even if app crashes hard (power loss, task kill, etc.)
            return new System.IO.FileStream(
                lockFilePath,
                System.IO.FileMode.CreateNew,
                System.IO.FileAccess.Write,
                System.IO.FileShare.None,
                4096, // 4KB buffer size
                System.IO.FileOptions.DeleteOnClose | System.IO.FileOptions.WriteThrough);
        }
        catch (System.IO.IOException)
        {
            // Lock file exists and is held by another instance
            return null;
        }
        catch (System.UnauthorizedAccessException ex)
        {
            // Permission issue - log and return null
            System.Diagnostics.Debug.WriteLine($"Permission denied accessing lock file: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Log other errors
            System.Diagnostics.Debug.WriteLine($"Error creating lock file at {lockFilePath}: {ex.Message}");
            return null;
        }
    }

    private static string GetSingleInstanceLockPath()
    {
        var appDataPath = GetAppDataPath();
        if (string.IsNullOrEmpty(appDataPath))
            return "Diffy-single-instance.lock"; // Fallback

        return System.IO.Path.Combine(appDataPath, "single-instance.lock");
    }

    private static string GetAppDataPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(appData, "Diffy");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS, use ~/Library/Application Support/Diffy/
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return System.IO.Path.Combine(home, "Library", "Application Support", "Diffy");
        }
        else
        {
            // On Linux, use XDG_DATA_HOME or ~/.local/share/diffy/
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(xdgDataHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var localShare = home != null ? System.IO.Path.Combine(home, ".local", "share") : "./share";
                xdgDataHome = localShare;
            }
            return System.IO.Path.Combine(xdgDataHome, "diffy");
        }
    }

    // Helper class to dispose lock file stream on app exit
    // Note: File is automatically deleted by FileOptions.DeleteOnClose, 
    // this class only needs to dispose the FileStream
    private class LockFileDisposable : IDisposable
    {
        private readonly System.IO.FileStream _stream;
        private bool _disposed;

        public LockFileDisposable(System.IO.FileStream stream)
        {
            _stream = stream;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stream?.Dispose(); // DeleteOnClose will auto-delete lock file
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        // Keep SVG controls alive for Avalonia Previewer
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}
