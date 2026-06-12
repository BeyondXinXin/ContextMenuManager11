using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ContextMenuManager11.App.Services;
using ContextMenuManager11.Core.Explorer;
using ContextMenuManager11.Core.Models;
using ContextMenuManager11.Core.Registry;
using ContextMenuManager11.Core.Shell;

namespace ContextMenuManager11.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly RegistryScanner _scanner = new();
    private readonly RegistryEditService _editService = new();
    private readonly ClassicContextMenuService _classicService;
    private readonly ExplorerRestartService _explorerService = new();
    private readonly IDialogService _dialogService;
    private string _selectedNavigation = "文件右键";
    private string _statusMessage = "就绪";
    private bool _isBusy;
    private bool _isClassicContextMenuEnabled;

    public MainViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _classicService = new ClassicContextMenuService(_editService);
        List = new ContextMenuListViewModel();
        Detail = new ContextMenuDetailViewModel();

        RestartExplorerCommand = new RelayCommand(RestartExplorer);

        RefreshClassicState();
        RefreshInternal();
    }

    public ObservableCollection<string> NavigationItems { get; } =
    [
        "文件右键",
        "文件夹右键",
        "空白处右键",
        "驱动器右键",
        "新建菜单",
        "发送到菜单",
        "打开方式",
        "Win11 设置"
    ];

    public ContextMenuListViewModel List { get; }
    public ContextMenuDetailViewModel Detail { get; }

    public RelayCommand RestartExplorerCommand { get; }

    public string SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (SetProperty(ref _selectedNavigation, value))
            {
                List.ScopeFilter = ToScopeFilter(value);
                OnPropertyChanged(nameof(ListPageVisibility));
                OnPropertyChanged(nameof(Win11PageVisibility));
            }
        }
    }

    public ContextMenuItemModel? SelectedItem
    {
        get => List.SelectedItem;
        set
        {
            if (List.SelectedItem != value)
            {
                List.SelectedItem = value;
                Detail.Item = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsClassicContextMenuEnabled
    {
        get => _isClassicContextMenuEnabled;
        set
        {
            if (!SetProperty(ref _isClassicContextMenuEnabled, value))
            {
                return;
            }

            RunOperation(() =>
            {
                _classicService.SetEnabled(value);
                StatusMessage = "Win11 经典右键菜单设置已更新，需要重启 Explorer 后生效。";
                _dialogService.ShowInfo("设置已更新。需要重启 Explorer 后生效。");
            });
        }
    }

    public Visibility ListPageVisibility => SelectedNavigation == "Win11 设置" ? Visibility.Collapsed : Visibility.Visible;
    public Visibility Win11PageVisibility => SelectedNavigation == "Win11 设置" ? Visibility.Visible : Visibility.Collapsed;

    // ─── 公共方法（供 code-behind 调用）──────────────────────

    /// <summary>切换开关：启用 / 禁用指定项。</summary>
    public void ToggleItem(ContextMenuItemModel item)
    {
        if (!CanToggle(item))
            return;

        RunOperation(() =>
        {
            if (item.IsEnabled)
            {
                _editService.Disable(item);
                StatusMessage = $"已禁用: {item.DisplayName}";
            }
            else
            {
                _editService.Enable(item);
                StatusMessage = $"已启用: {item.DisplayName}";
            }

            RefreshInternal();
        });
    }

    /// <summary>编辑指定 Shell 菜单项。</summary>
    public void EditItem(ContextMenuItemModel item)
    {
        if (item.Kind != ContextMenuKind.Shell)
            return;

        if (!_dialogService.TryEditShellItem(item, out var displayName, out var command, out var icon))
            return;

        RunOperation(() =>
        {
            _editService.UpdateShellItem(item, displayName, command, icon);
            StatusMessage = $"已编辑: {displayName}";
            RefreshInternal();
        });
    }

    /// <summary>删除指定 Shell 菜单项。</summary>
    public void DeleteItem(ContextMenuItemModel item)
    {
        if (item.Kind != ContextMenuKind.Shell)
            return;

        if (!_dialogService.Confirm($"确定永久删除 shell 菜单项\"{item.DisplayName}\"吗？", "确认删除"))
            return;

        RunOperation(() =>
        {
            _editService.DeleteShellItem(item);
            StatusMessage = $"已删除: {item.DisplayName}";
            RefreshInternal();
        });
    }

    /// <summary>在注册表编辑器中打开该项的注册表路径。</summary>
    public void OpenRegistryForItem(ContextMenuItemModel item)
    {
        if (!IsRegistryBacked(item))
            return;

        RunOperation(() =>
            _editService.OpenRegistryPath(
                string.IsNullOrWhiteSpace(item.DisabledRegistryPath)
                    ? item.RegistryPath
                    : item.DisabledRegistryPath));
    }

    /// <summary>在资源管理器中定位该项的可执行文件。</summary>
    public void OpenFileLocationForItem(ContextMenuItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.Command))
        {
            _dialogService.ShowError("该项没有关联的命令路径。");
            return;
        }

        var target = File.Exists(item.Command)
            ? item.Command
            : ShellCommandParser.TryGetExecutablePath(item.Command);

        if (target is null)
        {
            _dialogService.ShowError("无法从当前项中解析出有效的文件路径。");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            ArgumentList = { $"/select,{target}" }
        });
    }

    /// <summary>刷新列表。</summary>
    public void RefreshList()
    {
        RefreshInternal();
    }

    // ─── 私有实现 ──────────────────────────────────────

    private void RefreshInternal()
    {
        RunOperation(() =>
        {
            var items = _scanner.Scan();
            List.SetItems(items);
            SelectedItem = null;
            StatusMessage = $"共 {items.Count} 个菜单项";
        });
    }

    private void RestartExplorer()
    {
        if (!_dialogService.Confirm("将关闭并重新启动 explorer.exe。继续吗？", "重启 Explorer"))
        {
            return;
        }

        RunOperation(() =>
        {
            _explorerService.Restart();
            StatusMessage = "Explorer 已重启";
        });
    }

    private void RefreshClassicState()
    {
        _isClassicContextMenuEnabled = _classicService.IsEnabled();
        OnPropertyChanged(nameof(IsClassicContextMenuEnabled));
    }

    private void RunOperation(Action action)
    {
        try
        {
            IsBusy = true;
            action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _dialogService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ContextMenuScope? ToScopeFilter(string navigation) => navigation switch
    {
        "文件右键" => ContextMenuScope.File,
        "文件夹右键" => ContextMenuScope.Directory,
        "空白处右键" => ContextMenuScope.DirectoryBackground,
        "驱动器右键" => ContextMenuScope.Drive,
        "新建菜单" => ContextMenuScope.NewMenu,
        "发送到菜单" => ContextMenuScope.SendTo,
        "打开方式" => ContextMenuScope.OpenWith,
        _ => null
    };

    private static bool CanToggle(ContextMenuItemModel? item) =>
        item?.Kind is ContextMenuKind.Shell or ContextMenuKind.ShellEx;

    private static bool IsRegistryBacked(ContextMenuItemModel? item) =>
        item?.Kind != ContextMenuKind.FileSystem && item?.RegistryPath.StartsWith("HK", StringComparison.OrdinalIgnoreCase) == true;
}
