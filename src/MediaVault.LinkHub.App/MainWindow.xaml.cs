using System.Windows;
using MediaVault.LinkHub.Infrastructure.Data;

namespace MediaVault.LinkHub.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (SqliteDatabasePathProvider.IsDevelopment)
            Title = "MediaVault & LinkHub [Development]";
    }
}