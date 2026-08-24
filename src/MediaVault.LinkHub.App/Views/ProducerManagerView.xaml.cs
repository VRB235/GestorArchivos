using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class ProducerManagerView : UserControl
{
    public ProducerManagerView()
    {
        InitializeComponent();
    }

    private void CatalogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ProducerManagerViewModel viewModel)
            return;

        if (FindParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext
            is not ProducerCatalogItem item)
            return;

        viewModel.SelectedCatalogItem = item;

        if (viewModel.OpenCatalogLinksCommand.CanExecute(null))
            viewModel.OpenCatalogLinksCommand.Execute(null);

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
