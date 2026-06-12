using System.IO;

namespace ContextMenuManager11.Core.Models;

public sealed class ContextMenuItemModel
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public ContextMenuScope Scope { get; set; }
    public ContextMenuKind Kind { get; set; }
    public RegistryRoot Root { get; set; }
    public string RegistryPath { get; set; } = "";
    public string DisabledRegistryPath { get; set; } = "";
    public string Command { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Clsid { get; set; } = "";
    public bool IsEnabled { get; set; }
    public bool IsShiftOnly { get; set; }
    public string RiskLevel { get; set; } = "低";
    public string VisibilityDetail { get; set; } = "";
    public string LastModifiedTime { get; set; } = "未知";

    /// <summary>
    /// 从 Command 或 Icon 中提取的服务文件路径（DLL/EXE）。
    /// </summary>
    public string ServicePath
    {
        get
        {
            var candidates = new[] { Command, Icon };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var path = ExtractPath(candidate);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }

            return "";
        }
    }

    /// <summary>
    /// 用于在列表中显示图标；优先返回 Shell 命令里可提取的图标路径。
    /// </summary>
    public string IconSourcePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Icon))
            {
                var path = ExtractPath(Icon);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }

            if (!string.IsNullOrWhiteSpace(Command))
            {
                var path = ExtractPath(Command);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }

            return "";
        }
    }

    private static string ExtractPath(string raw)
    {
        raw = raw.Trim('"');
        // 去掉命令行参数，只保留 exe/dll 路径
        if (raw.Contains(' '))
        {
            var spaceIdx = raw.IndexOf(' ');
            var firstPart = raw[..spaceIdx];
            if (firstPart.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || firstPart.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return firstPart;
            }
        }

        if (raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || raw.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        // 尝试从完整路径中提取（如 "C:\path\to\file.exe,0" 这种图标格式）
        var commaIdx = raw.LastIndexOf(',');
        if (commaIdx > 0)
        {
            var beforeComma = raw[..commaIdx];
            if (beforeComma.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || beforeComma.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return beforeComma;
            }
        }

        return "";
    }

    public string StatusText => IsEnabled ? (IsShiftOnly ? "启用(Shift)" : "启用") : "禁用";
    public string ScopeText => Scope switch
    {
        ContextMenuScope.File => "文件",
        ContextMenuScope.AllFileSystemObjects => "全部文件系统对象",
        ContextMenuScope.Directory => "文件夹",
        ContextMenuScope.DirectoryBackground => "空白处",
        ContextMenuScope.Folder => "Folder",
        ContextMenuScope.Drive => "驱动器",
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
        _ => Kind.ToString()
    };
    public string RootText => Root.ToString();
}
