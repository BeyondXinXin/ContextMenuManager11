using System.Windows;
using ContextMenuManager11.App.ViewModels;

namespace ContextMenuManager11.App.Views;

public partial class DetailWindow : Window
{
    public DetailWindow(ContextMenuDetailViewModel detailViewModel)
    {
        InitializeComponent();
        DataContext = detailViewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
