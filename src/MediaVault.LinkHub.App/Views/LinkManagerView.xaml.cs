using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.Views;

public partial class LinkManagerView
{
    public LinkManagerView()
    {
        InitializeComponent();
    }

    private void LinksListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not LinkManagerViewModel viewModel)
            return;

        if (FindParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not WebLink link)
            return;

        viewModel.SelectedWebLink = link;

        if (viewModel.OpenCommand.CanExecute(link))
            viewModel.OpenCommand.Execute(link);

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
