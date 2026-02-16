using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Diffy.App.ViewModels;
using ReactiveUI;

namespace Diffy.App.Views;

public partial class RepositoryTabView : UserControl
{
    private RepositoryTabViewModel? _currentVm;
    private IDisposable? _selectedFileIndexSubscription;

    public RepositoryTabView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (_currentVm != null)
            {
                _currentVm.ScrollRequested -= OnScrollRequested;
            }

            // Dispose previous subscription to prevent leaks
            _selectedFileIndexSubscription?.Dispose();
            _selectedFileIndexSubscription = null;

            _currentVm = DataContext as RepositoryTabViewModel;

            if (_currentVm != null)
            {
                _currentVm.ScrollRequested += OnScrollRequested;

                // Ensure visual selection is applied when SelectedFileIndex changes programmatically
                _selectedFileIndexSubscription = _currentVm.WhenAnyValue(x => x.SelectedFileIndex)
                    .Subscribe(index => EnsureFileListSelection(index));
            }
        };
    }

    private void EnsureFileListSelection(int index)
    {
        if (index < 0) return;

        // Use dispatcher to ensure UI is ready
        Dispatcher.UIThread.Post(() =>
        {
            var listBox = this.FindControl<ListBox>("FileListBox");
            if (listBox == null) return;

            // Ensure SelectedIndex is set
            if (listBox.SelectedIndex != index)
            {
                listBox.SelectedIndex = index;
            }

            // Scroll into view without stealing focus - focus stealing
            // causes fights with user click interactions and can block the UI
            listBox.ScrollIntoView(index);
        }, DispatcherPriority.Background);
    }

    private void OnScrollRequested(int index, Diffy.Core.Models.DiffMode mode)
    {
        var vm = DataContext as RepositoryTabViewModel;
        if (vm == null) return;

        ListBox? listBox = mode == Diffy.Core.Models.DiffMode.SideBySide
            ? this.FindControl<ListBox>("SideBySideListBox")
            : this.FindControl<ListBox>("InlineListBox");

        if (listBox != null && index >= 0 && index < listBox.ItemCount)
        {
            // Set selected index so it's highlighted/focused
            listBox.SelectedIndex = index;

            // Actually scroll it into view
            listBox.ScrollIntoView(index);
        }
    }
}
