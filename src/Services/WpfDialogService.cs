using System.Windows;
using ContextMenuManager11.App.Views;
using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.App.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly Window _owner;

    public WpfDialogService(Window owner)
    {
        _owner = owner;
    }

    public void ShowInfo(string message)
    {
        MessageBox.Show(_owner, message, "ContextMenuManager11", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message)
    {
        MessageBox.Show(_owner, message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public bool TryEditShellItem(ContextMenuItemModel item, out string displayName, out string command, out string icon)
    {
        var window = new EditMenuItemWindow(item)
        {
            Owner = _owner
        };

        var result = window.ShowDialog() == true;
        displayName = result ? window.DisplayNameValue : "";
        command = result ? window.CommandValue : "";
        icon = result ? window.IconValue : "";
        return result;
    }
}
