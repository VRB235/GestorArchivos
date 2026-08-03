using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class MediaVaultView
{
    public MediaVaultView()
    {
        InitializeComponent();
    }

    private void BrowserListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MediaVaultViewModel viewModel)
            return;

        if (FindParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not MediaVaultBrowserEntryItem entry)
            return;

        if (viewModel.OpenBrowserEntryCommand.CanExecute(entry))
            viewModel.OpenBrowserEntryCommand.Execute(entry);

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
