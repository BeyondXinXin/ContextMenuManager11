using System.Windows;
using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.App.Views;

public partial class EditMenuItemWindow : Window
{
    public EditMenuItemWindow(ContextMenuItemModel item)
    {
        InitializeComponent();
        DisplayNameBox.Text = item.DisplayName;
        CommandBox.Text = item.Command;
        IconBox.Text = item.Icon;
    }

    public string DisplayNameValue => DisplayNameBox.Text.Trim();
    public string CommandValue => CommandBox.Text.Trim();
    public string IconValue => IconBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DisplayNameValue))
        {
            MessageBox.Show(this, "显示名称不能为空。", "编辑菜单项", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
