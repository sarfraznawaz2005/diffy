using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Diffy.App.Services;
using Diffy.App.ViewModels;
using Diffy.App.Views;
using Diffy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Diffy.App;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        Services = serviceProvider; // Set the static property

        // Apply saved theme
        var settingsService = serviceProvider.GetRequiredService<Core.Interfaces.ISettingsService>();
        var theme = settingsService.GetTheme();
        RequestedThemeVariant = theme switch
        {
            Core.Interfaces.AppTheme.Light => ThemeVariant.Light,
            Core.Interfaces.AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = serviceProvider.GetRequiredService<ViewModels.MainWindowViewModel>();
            var mainWindow = new Views.MainWindow
            {
                DataContext = vm
            };
            // Single instance support
            var singleInstanceService = serviceProvider.GetRequiredService<ISingleInstanceService>();
            singleInstanceService.ArgumentsReceived += (args) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!string.IsNullOrEmpty(args))
                    {
                        var repoPath = args.Trim().Trim('"').Trim();
                        if (repoPath.EndsWith("\""))
                        {
                            repoPath = repoPath.Substring(0, repoPath.Length - 1).Trim();
                        }
                        _ = vm.OpenRepositoryAsync(repoPath).ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                System.Diagnostics.Debug.WriteLine($"OpenRepositoryAsync from single-instance failed: {t.Exception?.InnerException?.Message}");
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                    mainWindow.ActivateWindow();
                });
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            // Defer heavy initialization until after window is shown
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                // Start listening for other instances
                singleInstanceService.StartListening();

                // Handle command line arguments or auto-open
                if (desktop.Args != null && desktop.Args.Length > 0)
                {
                    // Join arguments in case a space was misinterpreted due to escaping, 
                    // then clean up potential trailing quotes from Windows shell escaping quirks.
                    var fullArgs = string.Join(" ", desktop.Args);
                    var repoPath = fullArgs.Trim().Trim('"').Trim();

                    // If it ends with a quote that was intended to be an escaped backslash
                    if (repoPath.EndsWith("\""))
                    {
                        repoPath = repoPath.Substring(0, repoPath.Length - 1).Trim();
                    }

                    vm.StatusText = $"Launching with: {repoPath}";
                    await vm.OpenRepositoryAsync(repoPath);
                }
                else
                {
                    // Small delay to ensure window is fully rendered before starting heavy IO
                    await Task.Delay(50);
                    await vm.AutoOpenLastRepositoryAsync();
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register services
        services.AddSingleton<IGitRepositoryFactory, LibGit2SharpRepositoryFactory>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IDiffService, DiffService>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IFileOperationService, FileOperationService>();
        services.AddSingleton<ITrashService, TrashService>();
        services.AddSingleton<ISyntaxHighlightingService, SyntaxHighlightingService>();
        services.AddSingleton<IShellIntegrationService, ShellIntegrationService>();
        services.AddSingleton<ISingleInstanceService, SingleInstanceService>();

        // Register view models
        services.AddTransient<MainWindowViewModel>();
    }
}
