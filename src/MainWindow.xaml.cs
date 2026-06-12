using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ContextMenuManager11.App.Services;
using ContextMenuManager11.App.ViewModels;
using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new WpfDialogService(this));
    }

    /// <summary>
    /// 双击列表项 → 编辑（仅 shell 类型支持）。
    /// </summary>
    private void ItemListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedItem is { Kind: ContextMenuKind.Shell } item)
        {
            ViewModel.EditItem(item);
        }
    }

    /// <summary>
    /// 右键列表项时先选中该项，再弹出右键菜单。
    /// </summary>
    private void ItemListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView listView)
        {
            var hit = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
            if (hit is not null)
            {
                var item = FindAncestor<ListViewItem>(hit.VisualHit);
                if (item is not null)
                {
                    item.IsSelected = true;
                }
            }
        }
    }

    /// <summary>
    /// 切换开关单击 → 立即启用/禁用。
    /// </summary>
    private void ToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is ContextMenuItemModel item)
        {
            ViewModel.ToggleItem(item);
            e.Handled = true;
        }
    }

    // ─── 右键菜单事件 ──────────────────────────────

    private void ContextMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is { Kind: ContextMenuKind.Shell } item)
            ViewModel.EditItem(item);
    }

    private void ContextMenu_Properties_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is not null)
            ShowDetailWindow();
    }

    private void ContextMenu_OpenRegistry_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is not null)
            ViewModel.OpenRegistryForItem(ViewModel.SelectedItem);
    }

    private void ContextMenu_OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is not null)
            ViewModel.OpenFileLocationForItem(ViewModel.SelectedItem);
    }

    private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is { Kind: ContextMenuKind.Shell } item)
            ViewModel.DeleteItem(item);
    }

    private void ContextMenu_Refresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshList();
    }

    // ─── 键盘快捷键 ──────────────────────────────

    private void ItemListView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F5:
                ViewModel.RefreshList();
                e.Handled = true;
                break;

            case Key.Delete:
                {
                    if (ViewModel.SelectedItem is { Kind: ContextMenuKind.Shell } shellItem)
                    {
                        ViewModel.DeleteItem(shellItem);
                        e.Handled = true;
                    }
                }
                break;

            case Key.Enter:
                {
                    if (ViewModel.SelectedItem is { Kind: ContextMenuKind.Shell } editItem)
                    {
                        ViewModel.EditItem(editItem);
                        e.Handled = true;
                    }
                }
                break;

            case Key.Apps:
                // Context menu key — let the default behavior handle it
                break;
        }
    }

    // ─── 详情弹出窗口 ──────────────────────────────

    private void ShowDetailWindow()
    {
        var detailWindow = new Views.DetailWindow(ViewModel.Detail)
        {
            Owner = this
        };
        detailWindow.ShowDialog();
    }

    // ─── 辅助方法 ──────────────────────────────

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
