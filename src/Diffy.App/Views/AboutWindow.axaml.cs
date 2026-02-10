using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Diffy.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = GetVersion();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (version is not null)
        {
            var buildIndex = version.IndexOf('+');
            if (buildIndex > 0)
            {
                version = version[..buildIndex];
            }
            return $"v{version}";
        }

        version = assembly.GetName().Version?.ToString();
        return string.IsNullOrEmpty(version) ? "unknown" : $"v{version}";
    }
}
