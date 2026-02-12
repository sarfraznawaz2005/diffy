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

    public RepositoryTabView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (_currentVm != null)
            {
                _currentVm.ScrollRequested -= OnScrollRequested;
            }

            _currentVm = DataContext as RepositoryTabViewModel;

            if (_currentVm != null)
            {
                _currentVm.ScrollRequested += OnScrollRequested;
                
                // Ensure visual selection is applied when SelectedFileIndex changes programmatically
                _currentVm.WhenAnyValue(x => x.SelectedFileIndex)
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
            
            // Scroll into view and focus
            listBox.ScrollIntoView(index);
            listBox.Focus();
            
            // Force the container to update its visual state
            var container = listBox.ContainerFromIndex(index);
            if (container != null)
            {
                container.Focus();
            }
        }, DispatcherPriority.Background);
    }

    private void OnScrollRequested(int index, Diffy.Core.Models.DiffMode mode)
    {
        var vm = DataContext as RepositoryTabViewModel;
        if (vm == null) return;

        ListBox? listBox = mode == Diffy.Core.Models.DiffMode.SideBySide
            ? this.FindControl<ListBox>("SideBySideListBox")
            : this.FindControl<ListBox>("InlineListBox");

        if (listBox != null && index >= 0 && index < listBox.Items.Cast<object>().Count())
        {
            // Set selected index so it's highlighted/focused
            listBox.SelectedIndex = index;

            // Actually scroll it into view
            listBox.ScrollIntoView(index);
        }
    }
}
