using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Diffy.Core.Interfaces;

namespace Diffy.App.Services;

public class ShellIntegrationService : IShellIntegrationService
{
    private const string MenuKeyName = "Diffy";
    private const string MenuText = "Watch with Diffy";
    private const string WinRegistryRoot = @"Software\Classes\";

    public void RegisterContextMenuItem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RegisterForWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            RegisterForMacOS();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            RegisterForLinux();
        }
    }

    public void UnregisterContextMenuItem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            UnregisterFromWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            UnregisterFromMacOS();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            UnregisterFromLinux();
        }
    }

    public bool IsRegistered()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsRegisteredWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IsRegisteredMacOS();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return IsRegisteredLinux();
        }
        return false;
    }

    #region Windows Implementation

#pragma warning disable CA1416 // Windows-specific APIs are only used on Windows platform

    private void RegisterForWindows()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.WriteLine("Failed to get executable path.");
                return;
            }

            // Register for folder context menu
            RegisterForWindowsInternal(exePath, WinRegistryRoot + @"Directory\shell\" + MenuKeyName, "%1");

            // Register for folder background context menu
            RegisterForWindowsInternal(exePath, WinRegistryRoot + @"Directory\Background\shell\" + MenuKeyName, "%V");

            Debug.WriteLine("Context menu registered successfully in HKCU.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register context menu: {ex.Message}");
        }
    }

    private void UnregisterFromWindows()
    {
        try
        {
            UnregisterFromWindowsInternal(WinRegistryRoot + @"Directory\shell\" + MenuKeyName);
            UnregisterFromWindowsInternal(WinRegistryRoot + @"Directory\Background\shell\" + MenuKeyName);
            Debug.WriteLine("Context menu unregistered successfully from HKCU.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister context menu: {ex.Message}");
        }
    }

    private bool IsRegisteredWindows()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WinRegistryRoot + @"Directory\shell\" + MenuKeyName);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private void RegisterForWindowsInternal(string exePath, string subKeyPath, string argPlaceholder)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(subKeyPath);
        if (key != null)
        {
            key.SetValue("", MenuText);
            key.SetValue("Icon", exePath);

            using var commandKey = key.CreateSubKey("command");
            if (commandKey != null)
            {
                commandKey.SetValue("", $"\"{exePath}\" \"{argPlaceholder}\"");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private void UnregisterFromWindowsInternal(string subKeyPath)
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, false);
    }

#pragma warning restore CA1416

    #endregion

    #region macOS Implementation

#pragma warning disable CA1416 // macOS-specific APIs are only used on macOS platform

    private void RegisterForMacOS()
    {
        try
        {
            // Create .plist file for Finder service
            var servicesDir = GetMacOSServicesDirectory();
            var plistPath = Path.Combine(servicesDir, "com.diffy.app.FinderSync.plist");

            // Create services directory if it doesn't exist
            Directory.CreateDirectory(servicesDir);

            // Write Finder service definition
            var plistContent = GenerateMacOSFinderSyncPlistContent();
            File.WriteAllText(plistPath, plistContent);

            // Tell macOS to reload services
            ReloadMacOSServices();

            Debug.WriteLine("macOS shell integration registered successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register macOS shell integration: {ex.Message}");
        }
    }

    private void UnregisterFromMacOS()
    {
        try
        {
            var servicesDir = GetMacOSServicesDirectory();
            var plistPath = Path.Combine(servicesDir, "com.diffy.app.FinderSync.plist");

            if (File.Exists(plistPath))
            {
                File.Delete(plistPath);
                ReloadMacOSServices();
                Debug.WriteLine("macOS shell integration unregistered successfully.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister macOS shell integration: {ex.Message}");
        }
    }

    private bool IsRegisteredMacOS()
    {
        try
        {
            var servicesDir = GetMacOSServicesDirectory();
            var plistPath = Path.Combine(servicesDir, "com.diffy.app.FinderSync.plist");
            return File.Exists(plistPath);
        }
        catch
        {
            return false;
        }
    }

    private static string GetMacOSServicesDirectory()
    {
        // ~/Library/Services/
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return Path.Combine(".", "Library", "Services"); // Fallback

        return Path.Combine(home, "Library", "Services");
    }

    private static string GenerateMacOSFinderSyncPlistContent()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>NSServices</key>
    <array>
        <dict>
            <key>NSMessage</key>
            <string>openDiffyRepository</string>
            <key>NSServiceName</key>
            <string>com.diffy.app.watch</string>
            <key>NSSendTypes</key>
            <array>
                <string>public.folder</string>
            </array>
            <key>NSMenuItem</key>
            <dict>
                <key>default</key>
                <string>Watch with Diffy</string>
            </dict>
            <key>NSReturnTypes</key>
            <array>
                <string>public.file-url</string>
            </array>
        </dict>
    </array>
</dict>
</plist>";
    }

    private static void ReloadMacOSServices()
    {
        try
        {
            // Tell macOS to reload services using /usr/bin/killall Finder
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/killall",
                Arguments = "Finder",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            // Wait a bit then relaunch Finder
            process?.WaitForExit();

            Task.Run(async () =>
            {
                await Task.Delay(500);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "/usr/bin/open",
                    Arguments = "-a Finder",
                    CreateNoWindow = true
                });
            });
        }
        catch
        {
            // Ignore errors - service will reload on next login
        }
    }

#pragma warning restore CA1416

    #endregion

    #region Linux Implementation

#pragma warning disable CA1416 // Linux-only APIs are only used on Linux platform

    private void RegisterForLinux()
    {
        try
        {
            // Create .desktop file
            var applicationsDir = GetLinuxApplicationsDirectory();
            Directory.CreateDirectory(applicationsDir);

            var desktopFilePath = Path.Combine(applicationsDir, "com.diffy.app.desktop");
            var desktopContent = GenerateLinuxDesktopContent();
            File.WriteAllText(desktopFilePath, desktopContent);

            // Make desktop file executable
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = "+x \"" + desktopFilePath + "\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();

            // Update desktop database
            UpdateLinuxDesktopDatabase(applicationsDir);

            Debug.WriteLine("Linux shell integration registered successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register Linux shell integration: {ex.Message}");
        }
    }

    private void UnregisterFromLinux()
    {
        try
        {
            var applicationsDir = GetLinuxApplicationsDirectory();
            var desktopFilePath = Path.Combine(applicationsDir, "com.diffy.app.desktop");

            if (File.Exists(desktopFilePath))
            {
                File.Delete(desktopFilePath);
                UpdateLinuxDesktopDatabase(applicationsDir);
                Debug.WriteLine("Linux shell integration unregistered successfully.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister Linux shell integration: {ex.Message}");
        }
    }

    private bool IsRegisteredLinux()
    {
        try
        {
            var applicationsDir = GetLinuxApplicationsDirectory();
            var desktopFilePath = Path.Combine(applicationsDir, "com.diffy.app.desktop");
            return File.Exists(desktopFilePath);
        }
        catch
        {
            return false;
        }
    }

    private static string GetLinuxApplicationsDirectory()
    {
        // ~/.local/share/applications/
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(xdgDataHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                xdgDataHome = Path.Combine(".", ".local", "share");
            }
            else
            {
                xdgDataHome = Path.Combine(home, ".local", "share");
            }
        }

        if (string.IsNullOrEmpty(xdgDataHome))
        {
            xdgDataHome = Path.Combine(".", ".local", "share");
        }

        return Path.Combine(xdgDataHome ?? ".", "applications");
    }

    private static string GenerateLinuxDesktopContent()
    {
        var exePath = Process.GetCurrentProcess()?.MainModule?.FileName ?? "Diffy";
        var exeName = Path.GetFileNameWithoutExtension(exePath);

        return $@"[Desktop Entry]
Name=Diffy
GenericName=Diffy
Comment=Git Repository Watcher
Exec=""{exePath}"" %F
Icon=diffy
Terminal=false
Type=Application
Categories=Development;IDE;VersionControl;
Keywords=git;diff;watcher;
StartupWMClass=Diffy
Actions=WatchFolder;Name=Watch this folder with Diffy;
WatchFolder[Desktop Entry] Name=Watch this folder with Diffy;
WatchFolder[Exec]=""{exePath}"" %F;
";
    }

    private static void UpdateLinuxDesktopDatabase(string applicationsDir)
    {
        try
        {
            // Run update-desktop-database to refresh the application menu
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/update-desktop-database",
                Arguments = $"\"{applicationsDir}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch
        {
            // update-desktop-database might not be available, that's OK
        }
    }

#pragma warning restore CA1416

    #endregion
}
