using System.Diagnostics;
using ContextMenuManager11.Core.Models;
using Microsoft.Win32;

namespace ContextMenuManager11.Core.Registry;

public sealed class RegistryEditService
{
    public const string ClassicContextMenuClsid = "{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    private const int HideBasedOnVelocityIdValue = 0x639bc8;

    public void Disable(ContextMenuItemModel item)
    {
        if (!item.IsEnabled)
        {
            return;
        }

        if (item.Kind == ContextMenuKind.Shell)
        {
            WithWritableKey(item.RegistryPath, key =>
            {
                key.SetValue("LegacyDisable", "", RegistryValueKind.String);
                key.SetValue("ProgrammaticAccessOnly", "", RegistryValueKind.String);
                key.SetValue("HideBasedOnVelocityId", HideBasedOnVelocityIdValue, RegistryValueKind.DWord);
            });
            return;
        }

        MoveShellExToDisabled(item);
    }

    public void Enable(ContextMenuItemModel item)
    {
        if (item.IsEnabled)
        {
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

        var disabledPath = GetDisabledPath(item);
        MoveDisabledShellExToEnabled(item, disabledPath);
    }

    public void UpdateShellItem(ContextMenuItemModel item, string displayName, string command, string icon)
    {
        if (item.Kind != ContextMenuKind.Shell)
        {
            throw new InvalidOperationException("shellex COM 扩展项第一版不支持编辑。");
        }

        WithWritableKey(item.RegistryPath, key =>
        {
            key.SetValue("", displayName ?? "", RegistryValueKind.String);
            if (string.IsNullOrWhiteSpace(icon))
            {
                key.DeleteValue("Icon", false);
            }
            else
            {
                key.SetValue("Icon", icon, RegistryValueKind.String);
            }

            using var commandKey = key.CreateSubKey("command", true);
            commandKey.SetValue("", command ?? "", RegistryValueKind.String);
        });
    }

    public void DeleteShellItem(ContextMenuItemModel item)
    {
        if (item.Kind != ContextMenuKind.Shell)
        {
            throw new InvalidOperationException("shellex COM 扩展项第一版不允许永久删除，请使用禁用。");
        }

        DeleteKey(item.RegistryPath);
    }

    public bool IsClassicContextMenuEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{ClassicContextMenuClsid}\InprocServer32", false);
        return key is not null;
    }

    public void SetClassicContextMenuEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{ClassicContextMenuClsid}\InprocServer32", true);
            key.SetValue("", "", RegistryValueKind.String);
        }
        else
        {
            using var parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID", true);
            parent?.DeleteSubKeyTree(ClassicContextMenuClsid, false);
        }
    }

    public void OpenRegistryPath(string fullPath)
    {
        Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", true)
            ?.SetValue("LastKey", $@"Computer\{RegistryPath.RegExePath(fullPath)}", RegistryValueKind.String);
        Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
    }

    private static void MoveShellExToDisabled(ContextMenuItemModel item)
    {
        var disabledPath = GetDisabledPath(item);
        CopyKey(item.RegistryPath, disabledPath);
        WithWritableKey(disabledPath, key =>
        {
            key.SetValue("ContextMenuManager11OriginalPath", item.RegistryPath, RegistryValueKind.String);
            key.SetValue("ContextMenuManager11DisabledAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);
        });
        DeleteKey(item.RegistryPath);
    }

    private static void MoveDisabledShellExToEnabled(ContextMenuItemModel item, string disabledPath)
    {
        CopyKey(disabledPath, item.RegistryPath);
        WithWritableKey(item.RegistryPath, key =>
        {
            key.DeleteValue("ContextMenuManager11OriginalPath", false);
            key.DeleteValue("ContextMenuManager11DisabledAt", false);
        });
        DeleteKey(disabledPath);
    }

    private static string GetDisabledPath(ContextMenuItemModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.DisabledRegistryPath))
        {
            return item.DisabledRegistryPath;
        }

        var (root, subKeyPath) = RegistryPath.ParseFullPath(item.RegistryPath);
        var parent = RegistryPath.ParentSubKey(subKeyPath);
        var leaf = RegistryPath.LeafName(subKeyPath);
        return RegistryPath.FullPath(root, $@"{parent}\DisabledContextMenuHandlers\{leaf}");
    }

    private static void CopyKey(string sourceFullPath, string destinationFullPath)
    {
        var (sourceRoot, sourceSubKey) = RegistryPath.ParseFullPath(sourceFullPath);
        var (destinationRoot, destinationSubKey) = RegistryPath.ParseFullPath(destinationFullPath);
        using var source = RegistryPath.RootKey(sourceRoot).OpenSubKey(sourceSubKey, false)
            ?? throw new InvalidOperationException($"注册表项不存在: {sourceFullPath}");
        using var destination = RegistryPath.RootKey(destinationRoot).CreateSubKey(destinationSubKey, true)
            ?? throw new InvalidOperationException($"无法创建注册表项: {destinationFullPath}");
        CopyKeyRecursive(source, destination);
    }

    private static void CopyKeyRecursive(RegistryKey source, RegistryKey destination)
    {
        foreach (var valueName in source.GetValueNames())
        {
            destination.SetValue(valueName, source.GetValue(valueName) ?? "", source.GetValueKind(valueName));
        }

        foreach (var subKeyName in source.GetSubKeyNames())
        {
            using var sourceSubKey = source.OpenSubKey(subKeyName, false);
            using var destinationSubKey = destination.CreateSubKey(subKeyName, true);
            if (sourceSubKey is not null && destinationSubKey is not null)
            {
                CopyKeyRecursive(sourceSubKey, destinationSubKey);
            }
        }
    }

    private static bool IsHiddenByCommandFlags(object? value)
    {
        if (value is null)
        {
            return false;
        }

        try
        {
            var flags = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return flags % 16 >= 8;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void WithWritableKey(string fullPath, Action<RegistryKey> action)
    {
        var (root, subKeyPath) = RegistryPath.ParseFullPath(fullPath);
        using var key = RegistryPath.RootKey(root).OpenSubKey(subKeyPath, true)
            ?? throw new InvalidOperationException($"注册表项不存在或不可写: {fullPath}");
        action(key);
    }

    private static void DeleteKey(string fullPath)
    {
        var (root, subKeyPath) = RegistryPath.ParseFullPath(fullPath);
        using var parent = RegistryPath.RootKey(root).OpenSubKey(RegistryPath.ParentSubKey(subKeyPath), true);
        parent?.DeleteSubKeyTree(RegistryPath.LeafName(subKeyPath), false);
    }
}
