using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.App.ViewModels;

public sealed class ContextMenuDetailViewModel : ObservableObject
{
    private ContextMenuItemModel? _item;

    public ContextMenuItemModel? Item
    {
        get => _item;
        set => SetProperty(ref _item, value);
    }
}
