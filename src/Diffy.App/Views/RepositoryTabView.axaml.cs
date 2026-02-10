using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Diffy.App.ViewModels;

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
            }
        };
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
