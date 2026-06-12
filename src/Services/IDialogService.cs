using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.App.Services;

public interface IDialogService
{
    void ShowInfo(string message);
    void ShowError(string message);
    bool Confirm(string message, string title);
    bool TryEditShellItem(ContextMenuItemModel item, out string displayName, out string command, out string icon);
}
