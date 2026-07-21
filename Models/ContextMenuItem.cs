namespace ContextMenuManager11.Models;

internal sealed class ContextMenuItem
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ContextMenuScope Scope { get; set; }
    public ContextMenuKind Kind { get; set; }
    public RegistryRoot Root { get; set; }
    public string RegistryPath { get; set; } = string.Empty;
    public string DisabledRegistryPath { get; set; } = string.Empty;
    public string SystemRuleId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string IconResourcePath { get; set; } = string.Empty;
    public int IconResourceIndex { get; set; }
    public string BuiltInIcon { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string Clsid { get; set; } = string.Empty;
    public string ServicePath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsShiftOnly { get; set; }
    public bool IsReadOnly { get; set; }
    public bool OpenOnClick { get; set; }
    public string ReadOnlyActionText { get; set; } = "已注册";
    public int SortOrder { get; set; }
    public int DiscoveryOrder { get; set; }
    public string RiskLevel { get; set; } = "低";
    public string VisibilityDetail { get; set; } = string.Empty;
    public string LastModifiedTime { get; set; } = "未知";

    public string StatusText => IsReadOnly
        ? ReadOnlyActionText
        : IsEnabled
        ? IsShiftOnly ? "启用 (Shift)" : "启用"
        : "禁用";

    public string ScopeText => Scope switch
    {
        ContextMenuScope.File => "文件",
        ContextMenuScope.AllFileSystemObjects => "全部文件系统对象",
        ContextMenuScope.Folder => "文件夹",
        ContextMenuScope.Directory => "目录",
        ContextMenuScope.DirectoryBackground => "空白处",
        ContextMenuScope.DesktopBackground => "桌面背景",
        ContextMenuScope.Drive => "驱动器",
        ContextMenuScope.Computer => "此电脑",
        ContextMenuScope.RecycleBin => "回收站",
        ContextMenuScope.Library => "库",
        ContextMenuScope.NewMenu => "新建菜单",
        ContextMenuScope.SendTo => "发送到菜单",
        ContextMenuScope.OpenWith => "打开方式",
        _ => Scope.ToString()
    };

    public string KindText => Kind switch
    {
        ContextMenuKind.Shell => "shell",
        ContextMenuKind.ShellEx => "shellex",
        ContextMenuKind.Registry => "registry",
        ContextMenuKind.FileSystem => "filesystem",
        ContextMenuKind.BuiltIn => "Explorer 内置",
        _ => Kind.ToString()
    };

    public string RootText => Kind == ContextMenuKind.BuiltIn ? "Explorer" : Root.ToString();
}
