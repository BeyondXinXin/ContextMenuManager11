using ContextMenuManager11.Models;
using Microsoft.Win32;

namespace ContextMenuManager11.Services;

internal sealed class RegistryScanner
{
    private const int HideBasedOnVelocityIdValue = 0x639bc8;

    private static readonly Dictionary<string, int> DefaultMenuNameResources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = 8496,
        ["edit"] = 8516,
        ["print"] = 8497,
        ["find"] = 8503,
        ["play"] = 8498,
        ["runas"] = 8505,
        ["explore"] = 8502,
        ["preview"] = 8499
    };

    private static readonly Dictionary<string, KnownShellHandler> KnownShellHandlers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{D969A300-E7FF-11D0-A93B-00A0C90F2719}"] = new(
            "新建", "Windows 新建菜单处理器", "shell32.dll", -16769),
        ["{F81E9010-6EA4-11CE-A7FF-00AA003CA9F6}"] = new(
            "授予访问权限", "Windows 文件共享菜单处理器"),
        ["{7BA4C740-9E81-11CF-99D3-00AA004AE837}"] = new(
            "发送到", "Windows 发送到菜单处理器", "imageres.dll", -185),
        ["{0AF96EDE-AEBF-41ED-A1C8-CF7A685505B6}"] = new(
            "在导航窗格中显示", "Windows 库导航窗格菜单处理器", "imageres.dll", -100)
    };

    private static readonly SceneTarget[] SceneTargets =
    [
        new(ContextMenuScope.File, @"Software\Classes\*"),
        new(ContextMenuScope.Folder, @"Software\Classes\Folder"),
        new(ContextMenuScope.Directory, @"Software\Classes\Directory"),
        new(ContextMenuScope.DirectoryBackground, @"Software\Classes\Directory\Background"),
        new(ContextMenuScope.DesktopBackground, @"Software\Classes\DesktopBackground"),
        new(ContextMenuScope.Drive, @"Software\Classes\Drive"),
        new(ContextMenuScope.AllFileSystemObjects, @"Software\Classes\AllFilesystemObjects"),
        new(ContextMenuScope.Computer, @"Software\Classes\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}"),
        new(ContextMenuScope.RecycleBin, @"Software\Classes\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}"),
        new(
            ContextMenuScope.Library,
            @"Software\Classes\LibraryFolder",
            @"Software\Classes\LibraryFolder\Background",
            @"Software\Classes\UserLibraryFolder")
    ];

    private readonly ShellMetadataResolver _metadataResolver = new();

    public IReadOnlyList<ContextMenuItem> Scan()
    {
        List<ContextMenuItem> items = [];
        foreach (RegistryRoot root in new[] { RegistryRoot.HKCU, RegistryRoot.HKLM })
        {
            foreach (SceneTarget target in SceneTargets)
            {
                foreach (string classPath in target.ClassPaths)
                {
                    ScanShell(root, target.Scope, $@"{classPath}\shell", items);
                    ScanShellEx(root, target.Scope, $@"{classPath}\shellex", items);
                }
            }

            ScanNewMenu(root, items);
        }

        ScanOpenWithApplications(items);
        ScanSendTo(items);
        AddExplorerBuiltInItems(items);
        ApplyExplorerOrdering(items);
        return Dedupe(items)
            .OrderBy(item => item.Scope)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.DiscoveryOrder)
            .ToList();
    }

    private void ScanShell(
        RegistryRoot root,
        ContextMenuScope scope,
        string shellSubKeyPath,
        ICollection<ContextMenuItem> items)
    {
        using RegistryKey? shellKey = RegistryPath.RootKey(root).OpenSubKey(shellSubKeyPath, false);
        if (shellKey is null)
        {
            return;
        }

        foreach (string keyName in shellKey.GetSubKeyNames())
        {
            try
            {
                using RegistryKey? itemKey = shellKey.OpenSubKey(keyName, false);
                if (itemKey is null)
                {
                    continue;
                }

                using RegistryKey? commandKey = itemKey.OpenSubKey("command", false);
                string command = ReadString(commandKey, string.Empty);
                string rawDisplayName = FirstMeaningful(ReadString(itemKey, "MUIVerb"), ReadString(itemKey, string.Empty));
                string displayName = WindowsResourceResolver.ResolveMenuText(rawDisplayName);
                if (string.IsNullOrWhiteSpace(displayName)
                    && DefaultMenuNameResources.TryGetValue(keyName, out int resourceId))
                {
                    displayName = WindowsResourceResolver.ResolveMenuText($"@windows.storage.dll,-{resourceId}");
                }

                string clsid = ReadShellClsid(itemKey, commandKey);
                ShellMetadata metadata = _metadataResolver.Resolve(clsid);
                string servicePath = FirstMeaningful(metadata.FilePath, ShellCommandParser.TryGetServicePath(command));
                ShellVisibility visibility = ReadShellVisibility(itemKey);
                string semanticName = GetKnownShellVerbName(scope, keyName);
                int sortOrder = ReadShellSortOrder(itemKey);
                ContextMenuItem item = new()
                {
                    Name = keyName,
                    DisplayName = FirstMeaningful(displayName, semanticName, metadata.DisplayName, keyName),
                    Description = CreateDescription(metadata.Description, servicePath, "静态菜单"),
                    Scope = scope,
                    Kind = ContextMenuKind.Shell,
                    Root = root,
                    RegistryPath = RegistryPath.FullPath(root, $@"{shellSubKeyPath}\{keyName}"),
                    Command = command,
                    Icon = ReadString(itemKey, "Icon"),
                    Clsid = clsid,
                    ServicePath = servicePath,
                    IsEnabled = visibility.IsEnabled,
                    IsShiftOnly = visibility.IsShiftOnly,
                    VisibilityDetail = visibility.Detail,
                    RiskLevel = root == RegistryRoot.HKLM ? "中" : "低",
                    SortOrder = sortOrder
                };
                ApplyShellIcon(item, itemKey, metadata);
                items.Add(item);
            }
            catch
            {
                // A malformed handler must not hide the remaining menu items.
            }
        }
    }

    private void ScanShellEx(
        RegistryRoot root,
        ContextMenuScope scope,
        string shellExSubKeyPath,
        ICollection<ContextMenuItem> items)
    {
        ScanShellExFamily(root, scope, shellExSubKeyPath, "ContextMenuHandlers", items);

        if (scope == ContextMenuScope.RecycleBin)
        {
            ScanShellExFamily(root, scope, shellExSubKeyPath, "PropertySheetHandlers", items);
        }
    }

    private void ScanShellExFamily(
        RegistryRoot root,
        ContextMenuScope scope,
        string shellExSubKeyPath,
        string familyName,
        ICollection<ContextMenuItem> items)
    {
        string enabledContainer = $@"{shellExSubKeyPath}\{familyName}";
        string disabledContainer = $@"{shellExSubKeyPath}\-{familyName}";
        ScanShellExContainer(root, scope, enabledContainer, enabledContainer, disabledContainer, true, items);
        ScanShellExContainer(root, scope, disabledContainer, enabledContainer, disabledContainer, false, items);

        // Compatibility with early ContextMenuManager11 builds.
        if (familyName.Equals("ContextMenuHandlers", StringComparison.OrdinalIgnoreCase))
        {
            string legacyContainer = $@"{enabledContainer}\DisabledContextMenuHandlers";
            ScanShellExContainer(
                root,
                scope,
                legacyContainer,
                enabledContainer,
                legacyContainer,
                false,
                items);
        }
    }

    private void ScanShellExContainer(
        RegistryRoot root,
        ContextMenuScope scope,
        string actualContainer,
        string enabledContainer,
        string disabledContainer,
        bool enabled,
        ICollection<ContextMenuItem> items)
    {
        using RegistryKey? containerKey = RegistryPath.RootKey(root).OpenSubKey(actualContainer, false);
        if (containerKey is null)
        {
            return;
        }

        foreach (string keyName in containerKey.GetSubKeyNames())
        {
            if (keyName.Equals("DisabledContextMenuHandlers", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using RegistryKey? handlerKey = containerKey.OpenSubKey(keyName, false);
                if (handlerKey is null)
                {
                    continue;
                }

                string rawClsid = ReadString(handlerKey, string.Empty);
                string clsid = Guid.TryParse(rawClsid, out Guid parsed)
                    ? parsed.ToString("B")
                    : Guid.TryParse(keyName, out parsed) ? parsed.ToString("B") : string.Empty;
                ShellMetadata metadata = _metadataResolver.Resolve(clsid);
                KnownShellHandler? knownHandler = GetKnownShellHandler(scope, actualContainer, clsid);
                bool keyIsGuid = Guid.TryParse(keyName, out _);
                string fallbackName = keyIsGuid
                    ? string.IsNullOrWhiteSpace(clsid) ? "未识别的右键扩展" : $"未识别的右键扩展 · {clsid[1..9]}"
                    : WindowsResourceResolver.CleanMenuText(keyName);
                ContextMenuItem item = new()
                {
                    Name = keyName,
                    DisplayName = FirstMeaningful(knownHandler?.DisplayName, metadata.DisplayName, fallbackName),
                    Description = CreateDescription(
                        FirstMeaningful(knownHandler?.Description, metadata.Description),
                        metadata.FilePath,
                        "COM 菜单扩展"),
                    Scope = scope,
                    Kind = ContextMenuKind.ShellEx,
                    Root = root,
                    RegistryPath = RegistryPath.FullPath(root, $@"{enabledContainer}\{keyName}"),
                    DisabledRegistryPath = RegistryPath.FullPath(root, $@"{disabledContainer}\{keyName}"),
                    Clsid = clsid,
                    ServicePath = metadata.FilePath,
                    IsEnabled = enabled,
                    VisibilityDetail = enabled ? "已注册并启用" : "已禁用，配置仍然保留",
                    RiskLevel = root == RegistryRoot.HKLM ? "高" : "中"
                };
                ApplyMetadataIcon(item, metadata);
                if (knownHandler is { IconResourcePath.Length: > 0 })
                {
                    item.IconResourcePath = knownHandler.IconResourcePath;
                    item.IconResourceIndex = knownHandler.IconResourceIndex;
                }
                items.Add(item);
            }
            catch
            {
                // Continue scanning when a COM handler has invalid data.
            }
        }
    }

    private static void ApplyShellIcon(ContextMenuItem item, RegistryKey itemKey, ShellMetadata metadata)
    {
        ParsedIconLocation location = ShellCommandParser.ParseIconLocation(item.Icon);
        if (location.IsEmpty && itemKey.GetValue("HasLUAShield") is not null)
        {
            location = new ParsedIconLocation("imageres.dll", -78);
        }

        if (location.IsEmpty && !string.IsNullOrWhiteSpace(metadata.IconPath))
        {
            location = new ParsedIconLocation(metadata.IconPath, metadata.IconIndex);
        }

        if (location.IsEmpty && !string.IsNullOrWhiteSpace(item.ServicePath))
        {
            location = new ParsedIconLocation(item.ServicePath, 0);
        }

        if (location.IsEmpty)
        {
            location = DefaultIcon(item.Scope);
        }

        item.IconResourcePath = location.Path;
        item.IconResourceIndex = location.Index;
    }

    private static void ApplyMetadataIcon(ContextMenuItem item, ShellMetadata metadata)
    {
        ParsedIconLocation location = !string.IsNullOrWhiteSpace(metadata.IconPath)
            ? new ParsedIconLocation(metadata.IconPath, metadata.IconIndex)
            : !string.IsNullOrWhiteSpace(metadata.FilePath)
                ? new ParsedIconLocation(metadata.FilePath, 0)
                : DefaultIcon(item.Scope);
        item.IconResourcePath = location.Path;
        item.IconResourceIndex = location.Index;
    }

    private static ParsedIconLocation DefaultIcon(ContextMenuScope scope) => scope switch
    {
        ContextMenuScope.Folder => new ParsedIconLocation("imageres.dll", -3),
        ContextMenuScope.Directory => new ParsedIconLocation("imageres.dll", -162),
        ContextMenuScope.DirectoryBackground => new ParsedIconLocation("imageres.dll", -162),
        ContextMenuScope.DesktopBackground => new ParsedIconLocation("imageres.dll", -183),
        ContextMenuScope.Drive => new ParsedIconLocation("imageres.dll", -30),
        ContextMenuScope.AllFileSystemObjects => new ParsedIconLocation("imageres.dll", -117),
        ContextMenuScope.Computer => new ParsedIconLocation("explorer.exe", 0),
        ContextMenuScope.RecycleBin => new ParsedIconLocation("imageres.dll", -55),
        ContextMenuScope.Library => new ParsedIconLocation("imageres.dll", -100),
        ContextMenuScope.SendTo => new ParsedIconLocation("imageres.dll", -185),
        _ => new ParsedIconLocation("imageres.dll", -19)
    };

    private static string ReadShellClsid(RegistryKey itemKey, RegistryKey? commandKey)
    {
        using RegistryKey? dropTargetKey = itemKey.OpenSubKey("DropTarget", false);
        foreach (string? candidate in new[]
        {
            commandKey?.GetValue("DelegateExecute")?.ToString(),
            itemKey.GetValue("ExplorerCommandHandler")?.ToString(),
            dropTargetKey?.GetValue("CLSID")?.ToString()
        })
        {
            if (Guid.TryParse(candidate, out Guid guid))
            {
                return guid.ToString("B");
            }
        }

        return string.Empty;
    }

    private static void ScanNewMenu(RegistryRoot root, ICollection<ContextMenuItem> items)
    {
        using RegistryKey? classesKey = RegistryPath.RootKey(root).OpenSubKey(@"Software\Classes", false);
        if (classesKey is null)
        {
            return;
        }

        foreach (string extension in classesKey.GetSubKeyNames().Where(name =>
                     name.StartsWith(".", StringComparison.Ordinal)
                     || name.Equals("Folder", StringComparison.OrdinalIgnoreCase)))
        {
            using RegistryKey? extensionKey = classesKey.OpenSubKey(extension, false);
            if (extensionKey is null)
            {
                continue;
            }

            string progId = ReadString(extensionKey, string.Empty);
            using RegistryKey? progIdKey = string.IsNullOrWhiteSpace(progId) ? null : classesKey.OpenSubKey(progId, false);
            using RegistryKey? commandKey = progIdKey?.OpenSubKey(@"shell\open\command", false);
            using RegistryKey? iconKey = progIdKey?.OpenSubKey("DefaultIcon", false);
            string command = ReadString(commandKey, string.Empty);

            List<string> registrationParents = [extension];
            if (!string.IsNullOrWhiteSpace(progId))
            {
                registrationParents.Add($@"{extension}\{progId}");
            }

            foreach (string registrationParent in registrationParents.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                using RegistryKey? registrationKey = classesKey.OpenSubKey(registrationParent, false);
                if (registrationKey is null)
                {
                    continue;
                }

                foreach (string shellNewPart in new[] { "ShellNew", "-ShellNew" })
                {
                    using RegistryKey? shellNewKey = registrationKey.OpenSubKey(shellNewPart, false);
                    if (shellNewKey is null || !HasEffectiveShellNewValue(shellNewKey))
                    {
                        continue;
                    }

                    string typeName = FirstMeaningful(
                        WindowsResourceResolver.ResolveMenuText(ReadString(shellNewKey, "MenuText")),
                        WindowsResourceResolver.ResolveMenuText(ReadString(shellNewKey, "ItemName")),
                        WindowsResourceResolver.ResolveMenuText(ReadString(progIdKey, "FriendlyTypeName")),
                        WindowsResourceResolver.ResolveMenuText(ReadString(progIdKey, string.Empty)),
                        WindowsResourceResolver.ResolveMenuText(ReadString(extensionKey, "FriendlyTypeName")));
                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        continue;
                    }

                    string iconLocation = FirstMeaningful(
                        ReadString(shellNewKey, "IconPath"),
                        ReadString(iconKey, string.Empty));
                    ParsedIconLocation icon = ShellCommandParser.ParseIconLocation(iconLocation);
                    string servicePath = ShellCommandParser.TryGetServicePath(command) ?? string.Empty;
                    if (icon.IsEmpty && !string.IsNullOrWhiteSpace(servicePath))
                    {
                        icon = new ParsedIconLocation(servicePath, 0);
                    }

                    bool enabled = shellNewPart.Equals("ShellNew", StringComparison.OrdinalIgnoreCase);
                    string enabledSubKey = $@"Software\Classes\{registrationParent}\ShellNew";
                    string disabledSubKey = $@"Software\Classes\{registrationParent}\-ShellNew";
                    items.Add(new ContextMenuItem
                    {
                        Name = extension,
                        DisplayName = typeName,
                        Description = $"{extension} · 新建文件",
                        Scope = ContextMenuScope.NewMenu,
                        Kind = ContextMenuKind.Registry,
                        Root = root,
                        RegistryPath = RegistryPath.FullPath(root, enabledSubKey),
                        DisabledRegistryPath = RegistryPath.FullPath(root, disabledSubKey),
                        Command = ReadShellNewSummary(shellNewKey),
                        Icon = iconLocation,
                        IconResourcePath = icon.Path ?? string.Empty,
                        IconResourceIndex = icon.Index,
                        FileExtension = extension.StartsWith(".", StringComparison.Ordinal) ? extension : string.Empty,
                        ServicePath = servicePath,
                        IsEnabled = enabled,
                        VisibilityDetail = enabled ? "显示在新建菜单中" : "已保留但不显示",
                        RiskLevel = root == RegistryRoot.HKLM ? "中" : "低"
                    });
                }
            }
        }
    }

    private static void ScanOpenWithApplications(ICollection<ContextMenuItem> items)
    {
        foreach (RegistryRoot root in new[] { RegistryRoot.HKCU, RegistryRoot.HKLM })
        {
            const string applicationsPath = @"Software\Classes\Applications";
            using RegistryKey? applicationsKey = RegistryPath.RootKey(root).OpenSubKey(applicationsPath, false);
            if (applicationsKey is null)
            {
                continue;
            }

            foreach (string appName in applicationsKey.GetSubKeyNames().Where(name => name.Contains('.')))
            {
                try
                {
                    using RegistryKey? appKey = applicationsKey.OpenSubKey(appName, false);
                    using RegistryKey? shellKey = appKey?.OpenSubKey("shell", false);
                    if (appKey is null || shellKey is null)
                    {
                        continue;
                    }

                    string selectedAction = shellKey.GetSubKeyNames()
                        .OrderBy(name => name.Equals("open", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                        .FirstOrDefault(name =>
                        {
                            using RegistryKey? actionKey = shellKey.OpenSubKey(name, false);
                            using RegistryKey? commandKey = actionKey?.OpenSubKey("command", false);
                            return actionKey?.GetValue("NeverDefault") is null
                                && ShellCommandParser.TryGetServicePath(ReadString(commandKey, string.Empty)) is not null;
                        }) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(selectedAction))
                    {
                        continue;
                    }

                    using RegistryKey? actionKey = shellKey.OpenSubKey(selectedAction, false);
                    using RegistryKey? commandKey = actionKey?.OpenSubKey("command", false);
                    using RegistryKey? iconKey = appKey.OpenSubKey("DefaultIcon", false);
                    string command = ReadString(commandKey, string.Empty);
                    string filePath = ShellCommandParser.TryGetServicePath(command) ?? string.Empty;
                    string fileDescription = ShellMetadataResolver.GetFileDescription(filePath);
                    string displayName = FirstMeaningful(
                        WindowsResourceResolver.ResolveMenuText(ReadString(appKey, "FriendlyAppName")),
                        fileDescription,
                        Path.GetFileNameWithoutExtension(filePath),
                        Path.GetFileNameWithoutExtension(appName));
                    string iconLocation = ReadString(iconKey, string.Empty);
                    ParsedIconLocation icon = ShellCommandParser.ParseIconLocation(iconLocation);
                    if (icon.IsEmpty)
                    {
                        icon = new ParsedIconLocation(filePath, 0);
                    }

                    bool enabled = appKey.GetValue("NoOpenWith") is null;
                    items.Add(new ContextMenuItem
                    {
                        Name = appName,
                        DisplayName = displayName,
                        Description = FirstMeaningful(fileDescription, "已注册的打开方式"),
                        Scope = ContextMenuScope.OpenWith,
                        Kind = ContextMenuKind.Registry,
                        Root = root,
                        RegistryPath = RegistryPath.FullPath(root, $@"{applicationsPath}\{appName}"),
                        Command = command,
                        Icon = iconLocation,
                        IconResourcePath = icon.Path ?? string.Empty,
                        IconResourceIndex = icon.Index,
                        ServicePath = filePath,
                        IsEnabled = enabled,
                        VisibilityDetail = enabled ? "显示在打开方式列表中" : "NoOpenWith",
                        RiskLevel = root == RegistryRoot.HKLM ? "中" : "低"
                    });
                }
                catch
                {
                    // Skip broken application registrations and continue scanning.
                }
            }
        }
    }

    private static void ScanSendTo(ICollection<ContextMenuItem> items)
    {
        string userSendToFolder = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
        if (!string.IsNullOrWhiteSpace(userSendToFolder) && Directory.Exists(userSendToFolder))
        {
            items.Add(new ContextMenuItem
            {
                Name = "SendToFolder",
                DisplayName = "SendTo",
                Description = "打开并管理“发送到”菜单项目所在文件夹",
                Scope = ContextMenuScope.SendTo,
                Kind = ContextMenuKind.FileSystem,
                Root = RegistryRoot.HKCU,
                RegistryPath = userSendToFolder,
                Command = userSendToFolder,
                IconResourcePath = "imageres.dll",
                IconResourceIndex = -3,
                ServicePath = userSendToFolder,
                IsEnabled = true,
                IsReadOnly = true,
                OpenOnClick = true,
                ReadOnlyActionText = "打开",
                VisibilityDetail = "管理文件夹",
                RiskLevel = "低"
            });
        }

        (RegistryRoot Root, string Folder)[] paths =
        [
            (RegistryRoot.HKCU, userSendToFolder),
            (RegistryRoot.HKLM, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\SendTo"))
        ];

        foreach ((RegistryRoot root, string folder) in paths)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFileSystemEntries(folder))
            {
                if (Path.GetFileName(file).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileAttributes attributes = File.GetAttributes(file);
                bool enabled = !attributes.HasFlag(FileAttributes.Hidden);
                bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
                string extension = isDirectory ? string.Empty : Path.GetExtension(file);
                items.Add(new ContextMenuItem
                {
                    Name = Path.GetFileName(file),
                    DisplayName = GetSendToDisplayName(file),
                    Description = isDirectory
                        ? "发送到文件夹"
                        : extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                            ? "发送到快捷方式"
                            : "发送到菜单项",
                    Scope = ContextMenuScope.SendTo,
                    Kind = ContextMenuKind.FileSystem,
                    Root = root,
                    RegistryPath = file,
                    Command = file,
                    Icon = file,
                    IconResourcePath = file,
                    FileExtension = extension,
                    ServicePath = file,
                    IsEnabled = enabled,
                    VisibilityDetail = enabled ? "显示在发送到菜单中" : "隐藏文件属性",
                    RiskLevel = root == RegistryRoot.HKLM ? "中" : "低",
                    SortOrder = 100
                });
            }
        }

        AddSendToRuleItems(items);
    }

    private static string GetSendToDisplayName(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".desklink" => "桌面快捷方式",
            ".mapimail" => "邮件收件人",
            ".mydocs" => "文档",
            ".zfsendtotarget" => "压缩 (zip) 文件夹",
            _ => Path.GetFileNameWithoutExtension(path)
        };
    }

    private static void AddSendToRuleItems(ICollection<ContextMenuItem> items)
    {
        const string policyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        const string explorerPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
        bool showDrives = IsSystemRuleEnabled(policyPath, "NoDrivesInSendToMenu");
        bool deferBuild = IsSystemRuleEnabled(explorerPath, "DelaySendToMenuBuild");

        items.Add(new ContextMenuItem
        {
            Name = "NoDrivesInSendToMenu",
            DisplayName = FirstMeaningful(
                WindowsResourceResolver.ResolveMenuText("@shell32.dll,-9309"),
                "可移动磁盘"),
            Description = "插入可移动磁盘时，在“发送到”菜单中显示它的所有分区",
            Scope = ContextMenuScope.SendTo,
            Kind = ContextMenuKind.Registry,
            Root = RegistryRoot.HKLM,
            RegistryPath = RegistryPath.FullPath(RegistryRoot.HKLM, policyPath),
            SystemRuleId = RegistryEditService.SendToDriveRuleId,
            Command = "NoDrivesInSendToMenu",
            IconResourcePath = "imageres.dll",
            IconResourceIndex = -30,
            IsEnabled = showDrives,
            VisibilityDetail = showDrives ? "显示可移动磁盘" : "策略已隐藏可移动磁盘",
            RiskLevel = "中",
            SortOrder = 200
        });

        items.Add(new ContextMenuItem
        {
            Name = "DelaySendToMenuBuild",
            DisplayName = "快速构建“发送到”子菜单",
            Description = "延迟构建“发送到”项目，减少打开主右键菜单时的等待",
            Scope = ContextMenuScope.SendTo,
            Kind = ContextMenuKind.Registry,
            Root = RegistryRoot.HKLM,
            RegistryPath = RegistryPath.FullPath(RegistryRoot.HKLM, explorerPath),
            SystemRuleId = RegistryEditService.DeferBuildSendToRuleId,
            Command = "DelaySendToMenuBuild",
            IconResourcePath = "imageres.dll",
            IconResourceIndex = -185,
            IsEnabled = deferBuild,
            VisibilityDetail = deferBuild ? "已启用延迟构建" : "已关闭延迟构建",
            RiskLevel = "中",
            SortOrder = 210
        });
    }

    private static bool IsSystemRuleEnabled(string subKeyPath, string valueName)
    {
        foreach (RegistryRoot root in new[] { RegistryRoot.HKLM, RegistryRoot.HKCU })
        {
            using RegistryKey? key = RegistryPath.RootKey(root).OpenSubKey(subKeyPath, false);
            if (ReadInt32(key?.GetValue(valueName)) == 1)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddExplorerBuiltInItems(ICollection<ContextMenuItem> items)
    {
        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DesktopBackground,
            "View",
            "查看",
            "Explorer 内置的桌面图标查看方式子菜单",
            "view",
            -2000);
        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DesktopBackground,
            "SortBy",
            "排序方式",
            "Explorer 内置的桌面图标排序子菜单",
            "sort",
            -1999);
        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DesktopBackground,
            "Refresh",
            "刷新",
            "刷新桌面内容和图标状态",
            "refresh",
            -1998);

        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DirectoryBackground,
            "View",
            "查看",
            "Explorer 内置的文件夹查看方式子菜单",
            "view",
            -2000);
        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DirectoryBackground,
            "SortBy",
            "排序方式",
            "Explorer 内置的文件和文件夹排序子菜单",
            "sort",
            -1999);
        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DirectoryBackground,
            "GroupBy",
            "分组依据",
            "Explorer 内置的文件和文件夹分组子菜单",
            "group",
            -1998);
        AddExplorerBuiltInItem(
            items,
            ContextMenuScope.DirectoryBackground,
            "Refresh",
            "刷新",
            "刷新当前文件夹内容",
            "refresh",
            -1997);
    }

    private static void AddExplorerBuiltInItem(
        ICollection<ContextMenuItem> items,
        ContextMenuScope scope,
        string name,
        string displayName,
        string description,
        string icon,
        int sortOrder)
    {
        items.Add(new ContextMenuItem
        {
            Name = $"ExplorerBuiltIn.{name}",
            DisplayName = displayName,
            Description = description,
            Scope = scope,
            Kind = ContextMenuKind.BuiltIn,
            Root = RegistryRoot.HKCU,
            RegistryPath = "Explorer 运行时生成",
            BuiltInIcon = icon,
            IsEnabled = true,
            IsReadOnly = true,
            ReadOnlyActionText = "系统",
            VisibilityDetail = "由 Explorer 运行时生成，不能单独启用或禁用",
            RiskLevel = "低",
            SortOrder = sortOrder
        });
    }

    private static void ApplyExplorerOrdering(IList<ContextMenuItem> items)
    {
        IReadOnlyDictionary<string, int> shellNewOrder = ReadShellNewOrder();
        for (int index = 0; index < items.Count; index++)
        {
            ContextMenuItem item = items[index];
            item.DiscoveryOrder = index;
            if (item.Scope == ContextMenuScope.NewMenu)
            {
                item.SortOrder = shellNewOrder.TryGetValue(item.Name, out int order)
                    ? order
                    : shellNewOrder.Count + 1000;
            }
        }
    }

    private static IReadOnlyDictionary<string, int> ReadShellNewOrder()
    {
        const string shellNewCachePath =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew";
        using RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(shellNewCachePath, false);
        string[] classes = key?.GetValue("Classes") as string[] ?? [];
        return classes
            .Select((name, index) => (Name: name, Index: index))
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<ContextMenuItem> Dedupe(IEnumerable<ContextMenuItem> items) => items
        .GroupBy(CreateIdentity, StringComparer.OrdinalIgnoreCase)
        .Select(group => group
            .OrderBy(item => item.Root == RegistryRoot.HKCU ? 0 : 1)
            .ThenByDescending(item => item.IsEnabled)
            .First());

    private static string CreateIdentity(ContextMenuItem item)
    {
        string value = item.Kind switch
        {
            ContextMenuKind.ShellEx => RegistryRegistrationIdentity(item.RegistryPath),
            ContextMenuKind.FileSystem => item.RegistryPath,
            ContextMenuKind.Registry when item.Scope == ContextMenuScope.OpenWith
                && !string.IsNullOrWhiteSpace(item.ServicePath) => item.ServicePath,
            _ => item.Name
        };
        return $"{item.Scope}|{item.Kind}|{value}";
    }

    private static string RegistryRegistrationIdentity(string fullPath)
    {
        int separatorIndex = fullPath.IndexOf('\\');
        return separatorIndex >= 0 && separatorIndex < fullPath.Length - 1
            ? fullPath[(separatorIndex + 1)..]
            : fullPath;
    }

    private static string GetKnownShellVerbName(ContextMenuScope scope, string keyName) =>
        scope == ContextMenuScope.RecycleBin
        && keyName.Equals("ModernProperties", StringComparison.OrdinalIgnoreCase)
            ? "现代属性"
            : string.Empty;

    private static int ReadShellSortOrder(RegistryKey key) =>
        ReadString(key, "Position").Trim().ToLowerInvariant() switch
        {
            "top" => -1000,
            "bottom" => 1000,
            _ => 0
        };

    private static KnownShellHandler? GetKnownShellHandler(
        ContextMenuScope scope,
        string actualContainer,
        string clsid)
    {
        if (scope == ContextMenuScope.RecycleBin
            && actualContainer.Contains("PropertySheetHandlers", StringComparison.OrdinalIgnoreCase))
        {
            return new KnownShellHandler(
                "属性",
                "回收站属性菜单",
                "imageres.dll",
                -55);
        }

        return KnownShellHandlers.GetValueOrDefault(clsid);
    }

    private static string CreateDescription(string metadataDescription, string servicePath, string fallback)
    {
        string description = FirstMeaningful(metadataDescription, ShellMetadataResolver.GetFileDescription(servicePath));
        return string.IsNullOrWhiteSpace(description) ? fallback : description;
    }

    private static string ReadShellNewSummary(RegistryKey shellNewKey)
    {
        List<string> parts = [];
        foreach (string valueName in new[] { "FileName", "Command", "NullFile", "Data", "ItemName" })
        {
            object? value = shellNewKey.GetValue(valueName);
            if (value is not null)
            {
                parts.Add($"{valueName}={value}");
            }
        }

        return string.Join("; ", parts);
    }

    private static bool HasEffectiveShellNewValue(RegistryKey shellNewKey) =>
        new[] { "NullFile", "Data", "FileName", "Directory", "Command" }
            .Any(valueName => shellNewKey.GetValue(valueName) is not null);

    private static ShellVisibility ReadShellVisibility(RegistryKey key)
    {
        List<string> disabledReasons = [];
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

        bool isShiftOnly = key.GetValue("Extended") is not null;
        List<string> details = [];
        if (disabledReasons.Count > 0)
        {
            details.Add("禁用方式: " + string.Join(", ", disabledReasons));
        }

        if (isShiftOnly)
        {
            details.Add("仅按住 Shift 时显示");
        }

        return new ShellVisibility(disabledReasons.Count == 0, isShiftOnly, string.Join("；", details));
    }

    private static bool IsHiddenByCommandFlags(object? value)
    {
        int? flags = ReadInt32(value);
        return flags.HasValue && flags.Value % 16 >= 8;
    }

    private static int? ReadInt32(object? value)
    {
        try
        {
            return value is null ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static string ReadString(RegistryKey? key, string name) => key?.GetValue(name)?.ToString() ?? string.Empty;

    private static string FirstMeaningful(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record SceneTarget(ContextMenuScope Scope, params string[] ClassPaths);

    private sealed record KnownShellHandler(
        string DisplayName,
        string Description,
        string IconResourcePath = "",
        int IconResourceIndex = 0);

    private readonly record struct ShellVisibility(bool IsEnabled, bool IsShiftOnly, string Detail);
}
