using System.IO;
using ContextMenuManager11.Core.Models;
using Microsoft.Win32;

namespace ContextMenuManager11.Core.Registry;

public sealed class RegistryScanner
{
    private const int HideBasedOnVelocityIdValue = 0x639bc8;

    private static readonly (ContextMenuScope Scope, ContextMenuKind Kind, string Path)[] Targets =
    [
        (ContextMenuScope.File, ContextMenuKind.Shell, @"Software\Classes\*\shell"),
        (ContextMenuScope.File, ContextMenuKind.ShellEx, @"Software\Classes\*\shellex\ContextMenuHandlers"),
        (ContextMenuScope.AllFileSystemObjects, ContextMenuKind.Shell, @"Software\Classes\AllFilesystemObjects\shell"),
        (ContextMenuScope.AllFileSystemObjects, ContextMenuKind.ShellEx, @"Software\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers"),
        (ContextMenuScope.Directory, ContextMenuKind.Shell, @"Software\Classes\Directory\shell"),
        (ContextMenuScope.Directory, ContextMenuKind.ShellEx, @"Software\Classes\Directory\shellex\ContextMenuHandlers"),
        (ContextMenuScope.DirectoryBackground, ContextMenuKind.Shell, @"Software\Classes\Directory\Background\shell"),
        (ContextMenuScope.DirectoryBackground, ContextMenuKind.ShellEx, @"Software\Classes\Directory\Background\shellex\ContextMenuHandlers"),
        (ContextMenuScope.Folder, ContextMenuKind.Shell, @"Software\Classes\Folder\shell"),
        (ContextMenuScope.Folder, ContextMenuKind.ShellEx, @"Software\Classes\Folder\shellex\ContextMenuHandlers"),
        (ContextMenuScope.Drive, ContextMenuKind.Shell, @"Software\Classes\Drive\shell"),
        (ContextMenuScope.Drive, ContextMenuKind.ShellEx, @"Software\Classes\Drive\shellex\ContextMenuHandlers")
    ];

    public IReadOnlyList<ContextMenuItemModel> Scan()
    {
        var items = new List<ContextMenuItemModel>();
        foreach (var root in new[] { RegistryRoot.HKCU, RegistryRoot.HKLM })
        {
            foreach (var target in Targets)
            {
                var location = new RegistryLocation(root, target.Scope, target.Kind, target.Path);
                try
                {
                    if (target.Kind == ContextMenuKind.Shell)
                    {
                        ScanShell(location, items);
                    }
                    else
                    {
                        ScanShellEx(location, items);
                    }
                }
                catch (Exception ex)
                {
                    items.Add(new ContextMenuItemModel
                    {
                        Name = $"扫描失败: {target.Path}",
                        DisplayName = ex.Message,
                        Scope = target.Scope,
                        Kind = target.Kind,
                        Root = root,
                        RegistryPath = location.FullPath,
                        IsEnabled = false,
                        RiskLevel = "高"
                    });
                }
            }

            ScanNewMenu(root, items);
            ScanOpenWith(root, items);
        }

        ScanSendTo(items);

        return items
            .OrderBy(i => i.Scope)
            .ThenBy(i => i.Kind)
            .ThenBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ScanShell(RegistryLocation location, List<ContextMenuItemModel> items)
    {
        using var key = RegistryPath.RootKey(location.Root).OpenSubKey(location.SubKeyPath, false);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetSubKeyNames())
        {
            if (name.Equals("DisabledContextMenuHandlers", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var itemKey = key.OpenSubKey(name, false);
            if (itemKey is null)
            {
                continue;
            }

            using var commandKey = itemKey.OpenSubKey("command", false);
            var visibility = ReadShellVisibility(itemKey);
            var displayName = ReadString(itemKey, "MUIVerb");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = ReadString(itemKey, "");
            }

            items.Add(new ContextMenuItemModel
            {
                Name = name,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName,
                Scope = location.Scope,
                Kind = ContextMenuKind.Shell,
                Root = location.Root,
                RegistryPath = $@"{location.FullPath}\{name}",
                Command = ReadString(commandKey, ""),
                Icon = ReadString(itemKey, "Icon"),
                IsEnabled = visibility.IsEnabled,
                IsShiftOnly = visibility.IsShiftOnly,
                VisibilityDetail = visibility.Detail,
                RiskLevel = location.Root == RegistryRoot.HKLM ? "中" : "低"
            });
        }
    }

    private static void ScanShellEx(RegistryLocation location, List<ContextMenuItemModel> items)
    {
        using var key = RegistryPath.RootKey(location.Root).OpenSubKey(location.SubKeyPath, false);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetSubKeyNames())
        {
            if (name.Equals("DisabledContextMenuHandlers", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var itemKey = key.OpenSubKey(name, false);
            if (itemKey is null)
            {
                continue;
            }

            items.Add(CreateShellExItem(location, name, itemKey, true, ""));
        }

        using var disabledKey = key.OpenSubKey("DisabledContextMenuHandlers", false);
        if (disabledKey is null)
        {
            return;
        }

        foreach (var name in disabledKey.GetSubKeyNames())
        {
            using var itemKey = disabledKey.OpenSubKey(name, false);
            if (itemKey is null)
            {
                continue;
            }

            var disabledPath = $@"{location.FullPath}\DisabledContextMenuHandlers\{name}";
            items.Add(CreateShellExItem(location, name, itemKey, false, disabledPath));
        }
    }

    private static void ScanNewMenu(RegistryRoot root, List<ContextMenuItemModel> items)
    {
        using var classesKey = RegistryPath.RootKey(root).OpenSubKey(@"Software\Classes", false);
        if (classesKey is null)
        {
            return;
        }

        foreach (var extension in classesKey.GetSubKeyNames().Where(name => name.StartsWith(".", StringComparison.Ordinal)))
        {
            using var extensionKey = classesKey.OpenSubKey(extension, false);
            using var shellNewKey = extensionKey?.OpenSubKey("ShellNew", false);
            if (extensionKey is null || shellNewKey is null)
            {
                continue;
            }

            var displayName = ReadFileTypeName(classesKey, extensionKey, extension);
            items.Add(new ContextMenuItemModel
            {
                Name = extension,
                DisplayName = displayName,
                Scope = ContextMenuScope.NewMenu,
                Kind = ContextMenuKind.Registry,
                Root = root,
                RegistryPath = RegistryPath.FullPath(root, $@"Software\Classes\{extension}\ShellNew"),
                Command = ReadShellNewSummary(shellNewKey),
                Icon = ReadString(extensionKey, "Content Type"),
                IsEnabled = true,
                VisibilityDetail = "ShellNew",
                RiskLevel = root == RegistryRoot.HKLM ? "中" : "低"
            });
        }
    }

    private static void ScanOpenWith(RegistryRoot root, List<ContextMenuItemModel> items)
    {
        using var classesKey = RegistryPath.RootKey(root).OpenSubKey(@"Software\Classes", false);
        if (classesKey is null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in classesKey.GetSubKeyNames().Where(name => name.StartsWith(".", StringComparison.Ordinal)))
        {
            using var extensionKey = classesKey.OpenSubKey(extension, false);
            if (extensionKey is null)
            {
                continue;
            }

            using var openWithList = extensionKey.OpenSubKey("OpenWithList", false);
            if (openWithList is not null)
            {
                foreach (var appName in openWithList.GetSubKeyNames())
                {
                    var path = RegistryPath.FullPath(root, $@"Software\Classes\{extension}\OpenWithList\{appName}");
                    if (!seen.Add(path))
                    {
                        continue;
                    }

                    items.Add(new ContextMenuItemModel
                    {
                        Name = appName,
                        DisplayName = $"{appName} ({extension})",
                        Scope = ContextMenuScope.OpenWith,
                        Kind = ContextMenuKind.Registry,
                        Root = root,
                        RegistryPath = path,
                        Command = ReadApplicationCommand(classesKey, appName),
                        Icon = appName,
                        IsEnabled = true,
                        VisibilityDetail = "OpenWithList",
                        RiskLevel = root == RegistryRoot.HKLM ? "中" : "低"
                    });
                }
            }

            using var openWithProgids = extensionKey.OpenSubKey("OpenWithProgids", false);
            if (openWithProgids is null)
            {
                continue;
            }

            foreach (var progId in openWithProgids.GetValueNames().Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                var path = RegistryPath.FullPath(root, $@"Software\Classes\{extension}\OpenWithProgids");
                var key = $"{path}\\{progId}";
                if (!seen.Add(key))
                {
                    continue;
                }

                items.Add(new ContextMenuItemModel
                {
                    Name = progId,
                    DisplayName = $"{ReadProgIdName(classesKey, progId)} ({extension})",
                    Scope = ContextMenuScope.OpenWith,
                    Kind = ContextMenuKind.Registry,
                    Root = root,
                    RegistryPath = path,
                    Command = progId,
                    IsEnabled = true,
                    VisibilityDetail = "OpenWithProgids",
                    RiskLevel = root == RegistryRoot.HKLM ? "中" : "低"
                });
            }
        }
    }

    private static void ScanSendTo(List<ContextMenuItemModel> items)
    {
        var sendToPaths = new[]
        {
            (RegistryRoot.HKCU, Environment.GetFolderPath(Environment.SpecialFolder.SendTo)),
            (RegistryRoot.HKLM, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\SendTo"))
        };

        foreach (var (root, folder) in sendToPaths)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                items.Add(new ContextMenuItemModel
                {
                    Name = Path.GetFileName(file),
                    DisplayName = Path.GetFileNameWithoutExtension(file),
                    Scope = ContextMenuScope.SendTo,
                    Kind = ContextMenuKind.FileSystem,
                    Root = root,
                    RegistryPath = file,
                    Command = file,
                    Icon = file,
                    IsEnabled = true,
                    VisibilityDetail = folder,
                    RiskLevel = root == RegistryRoot.HKLM ? "中" : "低"
                });
            }
        }
    }

    private static ContextMenuItemModel CreateShellExItem(RegistryLocation location, string name, RegistryKey key, bool enabled, string disabledPath)
    {
        var clsid = ReadString(key, "");
        if (string.IsNullOrWhiteSpace(clsid) && name.StartsWith('{') && name.EndsWith('}'))
        {
            clsid = name;
        }

        return new ContextMenuItemModel
        {
            Name = name,
            DisplayName = name,
            Scope = location.Scope,
            Kind = ContextMenuKind.ShellEx,
            Root = location.Root,
            RegistryPath = $@"{location.FullPath}\{name}",
            DisabledRegistryPath = disabledPath,
            Clsid = clsid,
            IsEnabled = enabled,
            RiskLevel = location.Root == RegistryRoot.HKLM ? "高" : "中"
        };
    }

    private static string ReadFileTypeName(RegistryKey classesKey, RegistryKey extensionKey, string extension)
    {
        var progId = ReadString(extensionKey, "");
        var progIdName = ReadProgIdName(classesKey, progId);
        return string.IsNullOrWhiteSpace(progIdName) ? extension : $"{progIdName} ({extension})";
    }

    private static string ReadProgIdName(RegistryKey classesKey, string progId)
    {
        if (string.IsNullOrWhiteSpace(progId))
        {
            return "";
        }

        using var progIdKey = classesKey.OpenSubKey(progId, false);
        var name = ReadString(progIdKey, "");
        return string.IsNullOrWhiteSpace(name) ? progId : name;
    }

    private static string ReadApplicationCommand(RegistryKey classesKey, string appName)
    {
        using var commandKey = classesKey.OpenSubKey($@"Applications\{appName}\shell\open\command", false);
        return ReadString(commandKey, "");
    }

    private static string ReadShellNewSummary(RegistryKey shellNewKey)
    {
        var parts = new List<string>();
        foreach (var valueName in new[] { "FileName", "Command", "NullFile", "Data", "ItemName" })
        {
            var value = shellNewKey.GetValue(valueName);
            if (value is not null)
            {
                parts.Add(string.IsNullOrEmpty(valueName) ? value.ToString() ?? "" : $"{valueName}={value}");
            }
        }

        return string.Join("; ", parts);
    }

    private static ShellVisibility ReadShellVisibility(RegistryKey key)
    {
        var disabledReasons = new List<string>();
        if (key.GetValue("LegacyDisable") is not null)
        {
            disabledReasons.Add("LegacyDisable");
        }

        if (key.GetValue("ProgrammaticAccessOnly") is not null)
        {
            disabledReasons.Add("ProgrammaticAccessOnly");
        }

        if (ReadInt32(key.GetValue("HideBasedOnVelocityId")) == HideBasedOnVelocityIdValue)
        {
            disabledReasons.Add("HideBasedOnVelocityId");
        }

        if (IsHiddenByCommandFlags(key.GetValue("CommandFlags")))
        {
            disabledReasons.Add("CommandFlags");
        }

        var isShiftOnly = key.GetValue("Extended") is not null;
        var details = new List<string>();
        if (disabledReasons.Count > 0)
        {
            details.Add("禁用: " + string.Join(", ", disabledReasons));
        }

        if (isShiftOnly)
        {
            details.Add("仅 Shift 右键显示");
        }

        return new ShellVisibility(disabledReasons.Count == 0, isShiftOnly, string.Join("; ", details));
    }

    private static bool IsHiddenByCommandFlags(object? value)
    {
        var flags = ReadInt32(value);
        return flags.HasValue && flags.Value % 16 >= 8;
    }

    private static int? ReadInt32(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private readonly record struct ShellVisibility(bool IsEnabled, bool IsShiftOnly, string Detail);

    private static string ReadString(RegistryKey? key, string name)
    {
        if (key is null)
        {
            return "";
        }

        return key.GetValue(name)?.ToString() ?? "";
    }
}
