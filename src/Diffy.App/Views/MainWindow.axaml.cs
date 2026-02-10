using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Diffy.App.ViewModels;

namespace Diffy.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Subscribe to DataContext changes to set up key bindings when ViewModel is available
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(DataContext))
            {
                SetupPlatformSpecificKeyBindings();
            }
        };
    }

    /// <summary>
    /// Sets up platform-specific keyboard shortcuts.
    /// On macOS, uses Cmd+ (Meta) instead of Ctrl+ for standard shortcuts.
    /// </summary>
    private void SetupPlatformSpecificKeyBindings()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // Determine the appropriate modifier key
        var modifier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? KeyModifiers.Meta  // Cmd on macOS
            : KeyModifiers.Control; // Ctrl on Windows/Linux

        // Check if we already added the binding to avoid duplicates
        var existingBinding = KeyBindings.FirstOrDefault(kb =>
            kb.Gesture?.Key == Key.O &&
            (kb.Gesture?.KeyModifiers == KeyModifiers.Control || kb.Gesture?.KeyModifiers == KeyModifiers.Meta));

        if (existingBinding != null)
            return; // Already set up

        // Add platform-specific key binding for Open Repository
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.O, modifier),
            Command = vm.AddRepositoryCommand,
            CommandParameter = this.StorageProvider
        });
    }

    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Screens.Primary is { } primaryScreen)
        {
            var workingArea = primaryScreen.WorkingArea;
            var scaling = primaryScreen.Scaling;

            // Calculate 85% of the working area in pixels
            var targetWidthPixels = workingArea.Width * 0.85;
            var targetHeightPixels = workingArea.Height * 0.85;

            // Convert to logical units for Window properties
            var newWidth = targetWidthPixels / scaling;
            var newHeight = targetHeightPixels / scaling;

            // Ensure we don't go smaller than Min size
            Width = Math.Max(newWidth, MinWidth);
            Height = Math.Max(newHeight, MinHeight);

            // Center the window using pixel coordinates
            // Re-calculate actual pixels from the finalized logical Width/Height in case MinWidth logic applied
            var finalWidthPixels = Width * scaling;
            var finalHeightPixels = Height * scaling;

            var x = workingArea.X + (workingArea.Width - finalWidthPixels) / 2;
            var y = workingArea.Y + (workingArea.Height - finalHeightPixels) / 2;

            Position = new Avalonia.PixelPoint((int)x, (int)y);
        }
    }

    public void ActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Show();
        Activate();

        // Force to front on Windows
        Topmost = true;
        Topmost = false;

        Focus();
    }
}
