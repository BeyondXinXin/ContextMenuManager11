using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.App.ViewModels;

public sealed class ContextMenuListViewModel : ObservableObject
{
    private string _searchText = "";
    private string _statusFilter = "全部";
    private string _rootFilter = "全部";
    private string _kindFilter = "全部";
    private ContextMenuScope? _scopeFilter;
    private ContextMenuItemModel? _selectedItem;

    public ContextMenuListViewModel()
    {
        FilteredItems = CollectionViewSource.GetDefaultView(Items);
        FilteredItems.Filter = FilterItem;
    }

    public ObservableCollection<ContextMenuItemModel> Items { get; } = [];
    public ICollectionView FilteredItems { get; }
    public IReadOnlyList<string> StatusOptions { get; } = ["全部", "启用", "禁用"];
    public IReadOnlyList<string> RootOptions { get; } = ["全部", "HKCU", "HKLM"];
    public IReadOnlyList<string> KindOptions { get; } = ["全部", "shell", "shellex", "registry", "filesystem"];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredItems.Refresh();
            }
        }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                FilteredItems.Refresh();
            }
        }
    }

    public string RootFilter
    {
        get => _rootFilter;
        set
        {
            if (SetProperty(ref _rootFilter, value))
            {
                FilteredItems.Refresh();
            }
        }
    }

    public string KindFilter
    {
        get => _kindFilter;
        set
        {
            if (SetProperty(ref _kindFilter, value))
            {
                FilteredItems.Refresh();
            }
        }
    }

    public ContextMenuScope? ScopeFilter
    {
        get => _scopeFilter;
        set
        {
            if (SetProperty(ref _scopeFilter, value))
            {
                FilteredItems.Refresh();
            }
        }
    }

    public ContextMenuItemModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public void SetItems(IEnumerable<ContextMenuItemModel> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        FilteredItems.Refresh();
    }

    private bool FilterItem(object value)
    {
        if (value is not ContextMenuItemModel item)
        {
            return false;
        }

        if (_scopeFilter.HasValue && item.Scope != _scopeFilter.Value)
        {
            return false;
        }

        if (_statusFilter == "启用" && !item.IsEnabled)
        {
            return false;
        }

        if (_statusFilter == "禁用" && item.IsEnabled)
        {
            return false;
        }

        if (_rootFilter != "全部" && item.RootText != _rootFilter)
        {
            return false;
        }

        if (_kindFilter == "shell" && item.Kind != ContextMenuKind.Shell)
        {
            return false;
        }

        if (_kindFilter == "shellex" && item.Kind != ContextMenuKind.ShellEx)
        {
            return false;
        }

        if (_kindFilter == "registry" && item.Kind != ContextMenuKind.Registry)
        {
            return false;
        }

        if (_kindFilter == "filesystem" && item.Kind != ContextMenuKind.FileSystem)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        return Contains(item.DisplayName, _searchText)
            || Contains(item.Name, _searchText)
            || Contains(item.Command, _searchText)
            || Contains(item.Icon, _searchText)
            || Contains(item.RegistryPath, _searchText)
            || Contains(item.Clsid, _searchText);
    }

    private static bool Contains(string value, string searchText) =>
        value?.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
}
