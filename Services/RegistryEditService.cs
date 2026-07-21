using System.Diagnostics;
using ContextMenuManager11.Models;
using Microsoft.Win32;

namespace ContextMenuManager11.Services;

internal sealed class RegistryEditService
{
    public const string ClassicContextMenuClsid = "{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    public const string SendToDriveRuleId = "SendToDrive";
    public const string DeferBuildSendToRuleId = "DeferBuildSendTo";
    private const int HideBasedOnVelocityIdValue = 0x639bc8;

    public void Disable(ContextMenuItem item)
    {
        if (!item.IsEnabled)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.SystemRuleId))
        {
            SetSystemRule(item.SystemRuleId, false);
            return;
        }

        if (item.Scope == ContextMenuScope.NewMenu)
        {
            MoveKey(item.RegistryPath, GetDisabledNewMenuPath(item));
            return;
        }

        if (item.Scope == ContextMenuScope.OpenWith)
        {
            WithWritableKey(item.RegistryPath, key =>
                key.SetValue("NoOpenWith", string.Empty, RegistryValueKind.String));
            return;
        }

        if (item.Scope == ContextMenuScope.SendTo)
        {
            SetFileHidden(item.RegistryPath, true);
            return;
        }

        if (item.Kind == ContextMenuKind.Shell)
        {
            WithWritableKey(item.RegistryPath, key =>
            {
                key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                key.SetValue("ProgrammaticAccessOnly", string.Empty, RegistryValueKind.String);
                key.SetValue("HideBasedOnVelocityId", HideBasedOnVelocityIdValue, RegistryValueKind.DWord);
            });
            return;
        }

        MoveShellExToDisabled(item);
    }

    public void Enable(ContextMenuItem item)
    {
        if (item.IsEnabled)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.SystemRuleId))
        {
            SetSystemRule(item.SystemRuleId, true);
            return;
        }

        if (item.Scope == ContextMenuScope.NewMenu)
        {
            MoveKey(GetDisabledNewMenuPath(item), item.RegistryPath);
            return;
        }

        if (item.Scope == ContextMenuScope.OpenWith)
        {
            WithWritableKey(item.RegistryPath, key => key.DeleteValue("NoOpenWith", false));
            return;
        }

        if (item.Scope == ContextMenuScope.SendTo)
        {
            SetFileHidden(item.RegistryPath, false);
            return;
        }

        if (item.Kind == ContextMenuKind.Shell)
        {
            WithWritableKey(item.RegistryPath, key =>
            {
                key.DeleteValue("LegacyDisable", false);
                key.DeleteValue("ProgrammaticAccessOnly", false);
                key.DeleteValue("HideBasedOnVelocityId", false);
                if (IsHiddenByCommandFlags(key.GetValue("CommandFlags")))
                {
                    key.DeleteValue("CommandFlags", false);
                }
            });
            return;
        }

        MoveDisabledShellExToEnabled(item, GetDisabledPath(item));
    }

    public void UpdateShellItem(ContextMenuItem item, string displayName, string command, string icon)
    {
        if (item.Kind != ContextMenuKind.Shell)
        {
            throw new InvalidOperationException("shellex COM 扩展项不支持编辑。");
        }

        WithWritableKey(item.RegistryPath, key =>
        {
            key.SetValue(string.Empty, displayName, RegistryValueKind.String);
            if (string.IsNullOrWhiteSpace(icon))
            {
                key.DeleteValue("Icon", false);
            }
            else
            {
                key.SetValue("Icon", icon, RegistryValueKind.String);
            }

            using RegistryKey commandKey = key.CreateSubKey("command", true);
            commandKey.SetValue(string.Empty, command, RegistryValueKind.String);
        });
    }

    public void DeleteShellItem(ContextMenuItem item)
    {
        if (item.Kind != ContextMenuKind.Shell)
        {
            throw new InvalidOperationException("shellex COM 扩展项不允许永久删除，请使用禁用。");
        }

        DeleteKey(item.RegistryPath);
    }

    public bool IsClassicContextMenuEnabled()
    {
        using RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\CLSID\{ClassicContextMenuClsid}\InprocServer32",
            false);
        return key is not null;
    }

    public void SetClassicContextMenuEnabled(bool enabled)
    {
        if (enabled)
        {
            using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\CLSID\{ClassicContextMenuClsid}\InprocServer32",
                true);
            key.SetValue(string.Empty, string.Empty, RegistryValueKind.String);
        }
        else
        {
            using RegistryKey? parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\CLSID",
                true);
            parent?.DeleteSubKeyTree(ClassicContextMenuClsid, false);
        }
    }

    public void OpenRegistryPath(string fullPath)
    {
        using RegistryKey settings = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit",
            true);
        settings.SetValue("LastKey", $@"Computer\{RegistryPath.RegExePath(fullPath)}", RegistryValueKind.String);
        Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
    }

    private static void MoveShellExToDisabled(ContextMenuItem item)
    {
        string disabledPath = GetDisabledPath(item);
        CopyKey(item.RegistryPath, disabledPath);
        WithWritableKey(disabledPath, key =>
        {
            key.SetValue("ContextMenuManager11OriginalPath", item.RegistryPath, RegistryValueKind.String);
            key.SetValue("ContextMenuManager11DisabledAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);
        });
        DeleteKey(item.RegistryPath);
    }

    private static void MoveDisabledShellExToEnabled(ContextMenuItem item, string disabledPath)
    {
        CopyKey(disabledPath, item.RegistryPath);
        WithWritableKey(item.RegistryPath, key =>
        {
            key.DeleteValue("ContextMenuManager11OriginalPath", false);
            key.DeleteValue("ContextMenuManager11DisabledAt", false);
        });
        DeleteKey(disabledPath);
    }

    private static string GetDisabledPath(ContextMenuItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.DisabledRegistryPath))
        {
            return item.DisabledRegistryPath;
        }

        (RegistryRoot root, string subKeyPath) = RegistryPath.ParseFullPath(item.RegistryPath);
        string enabledContainer = RegistryPath.ParentSubKey(subKeyPath);
        string shellExPath = RegistryPath.ParentSubKey(enabledContainer);
        string leaf = RegistryPath.LeafName(subKeyPath);
        return RegistryPath.FullPath(root, $@"{shellExPath}\-ContextMenuHandlers\{leaf}");
    }

    private static void CopyKey(string sourceFullPath, string destinationFullPath)
    {
        (RegistryRoot sourceRoot, string sourceSubKey) = RegistryPath.ParseFullPath(sourceFullPath);
        (RegistryRoot destinationRoot, string destinationSubKey) = RegistryPath.ParseFullPath(destinationFullPath);
        using RegistryKey source = RegistryPath.RootKey(sourceRoot).OpenSubKey(sourceSubKey, false)
            ?? throw new InvalidOperationException($"注册表项不存在: {sourceFullPath}");
        using RegistryKey destination = RegistryPath.RootKey(destinationRoot).CreateSubKey(destinationSubKey, true)
            ?? throw new InvalidOperationException($"无法创建注册表项: {destinationFullPath}");
        CopyKeyRecursive(source, destination);
    }

    private static void MoveKey(string sourceFullPath, string destinationFullPath)
    {
        CopyKey(sourceFullPath, destinationFullPath);
        DeleteKey(sourceFullPath);
    }

    private static string GetDisabledNewMenuPath(ContextMenuItem item) =>
        !string.IsNullOrWhiteSpace(item.DisabledRegistryPath)
            ? item.DisabledRegistryPath
            : ReplaceLeaf(item.RegistryPath, "-ShellNew");

    private static string ReplaceLeaf(string fullPath, string leaf)
    {
        (RegistryRoot root, string subKeyPath) = RegistryPath.ParseFullPath(fullPath);
        return RegistryPath.FullPath(root, $@"{RegistryPath.ParentSubKey(subKeyPath)}\{leaf}");
    }

    private static void SetFileHidden(string path, bool hidden)
    {
        FileAttributes attributes = File.GetAttributes(path);
        attributes = hidden
            ? attributes | FileAttributes.Hidden
            : attributes & ~FileAttributes.Hidden;
        File.SetAttributes(path, attributes);
    }

    private static void SetSystemRule(string ruleId, bool enabled)
    {
        (string Path, string ValueName)[] rules = ruleId switch
        {
            SendToDriveRuleId =>
            [
                (@"HKLM\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDrivesInSendToMenu"),
                (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDrivesInSendToMenu")
            ],
            DeferBuildSendToRuleId =>
            [
                (@"HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer", "DelaySendToMenuBuild"),
                (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer", "DelaySendToMenuBuild")
            ],
            _ => throw new InvalidOperationException($"未知系统规则: {ruleId}")
        };

        foreach ((string path, string valueName) in rules)
        {
            (RegistryRoot root, string subKeyPath) = RegistryPath.ParseFullPath(path);
            if (enabled)
            {
                using RegistryKey? key = RegistryPath.RootKey(root).OpenSubKey(subKeyPath, true);
                key?.DeleteValue(valueName, false);
            }
            else
            {
                using RegistryKey key = RegistryPath.RootKey(root).CreateSubKey(subKeyPath, true);
                key.SetValue(valueName, 1, RegistryValueKind.DWord);
            }
        }
    }

    private static void CopyKeyRecursive(RegistryKey source, RegistryKey destination)
    {
        foreach (string valueName in source.GetValueNames())
        {
            destination.SetValue(valueName, source.GetValue(valueName) ?? string.Empty, source.GetValueKind(valueName));
        }

        foreach (string subKeyName in source.GetSubKeyNames())
        {
            using RegistryKey? sourceSubKey = source.OpenSubKey(subKeyName, false);
            using RegistryKey? destinationSubKey = destination.CreateSubKey(subKeyName, true);
            if (sourceSubKey is not null && destinationSubKey is not null)
            {
                CopyKeyRecursive(sourceSubKey, destinationSubKey);
            }
        }
    }

    private static bool IsHiddenByCommandFlags(object? value)
    {
        try
        {
            return value is not null
                && Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) % 16 >= 8;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static void WithWritableKey(string fullPath, Action<RegistryKey> action)
    {
        (RegistryRoot root, string subKeyPath) = RegistryPath.ParseFullPath(fullPath);
        using RegistryKey key = RegistryPath.RootKey(root).OpenSubKey(subKeyPath, true)
            ?? throw new InvalidOperationException($"注册表项不存在或不可写: {fullPath}");
        action(key);
    }

    private static void DeleteKey(string fullPath)
    {
        (RegistryRoot root, string subKeyPath) = RegistryPath.ParseFullPath(fullPath);
        using RegistryKey? parent = RegistryPath.RootKey(root).OpenSubKey(RegistryPath.ParentSubKey(subKeyPath), true);
        parent?.DeleteSubKeyTree(RegistryPath.LeafName(subKeyPath), false);
    }
}
