using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class ActressesView : UserControl
{
    public ActressesView()
    {
        InitializeComponent();
    }

    private void VideosListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ActressesViewModel viewModel)
            return;

        if (FindParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext
            is not ActressVideoListItem item)
            return;

        viewModel.SelectedVideo = item;

        if (viewModel.OpenSelectedVideoCommand.CanExecute(null))
            viewModel.OpenSelectedVideoCommand.Execute(null);

        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
                return match;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
