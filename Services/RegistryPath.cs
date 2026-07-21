using ContextMenuManager11.Models;
using Microsoft.Win32;

namespace ContextMenuManager11.Services;

internal static class RegistryPath
{
    public static RegistryKey RootKey(RegistryRoot root) =>
        root == RegistryRoot.HKCU ? Microsoft.Win32.Registry.CurrentUser : Microsoft.Win32.Registry.LocalMachine;

    public static string RootPrefix(RegistryRoot root) => root == RegistryRoot.HKCU ? "HKCU" : "HKLM";

    public static string FullPath(RegistryRoot root, string subKeyPath) => $@"{RootPrefix(root)}\{subKeyPath}";

    public static (RegistryRoot Root, string SubKeyPath) ParseFullPath(string fullPath)
    {
        if (fullPath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryRoot.HKCU, fullPath[5..]);
        }

        if (fullPath.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryRoot.HKCU, fullPath["HKEY_CURRENT_USER\\".Length..]);
        }

        if (fullPath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryRoot.HKLM, fullPath[5..]);
        }

        if (fullPath.StartsWith(@"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryRoot.HKLM, fullPath["HKEY_LOCAL_MACHINE\\".Length..]);
        }

        throw new ArgumentException($"不支持的注册表路径: {fullPath}", nameof(fullPath));
    }

    public static string RegExePath(string fullPath)
    {
        if (fullPath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
        {
            return @"HKEY_CURRENT_USER\" + fullPath[5..];
        }

        if (fullPath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
        {
            return @"HKEY_LOCAL_MACHINE\" + fullPath[5..];
        }

        return fullPath;
    }

    public static string ParentSubKey(string subKeyPath)
    {
        int index = subKeyPath.LastIndexOf('\\');
        return index < 0 ? string.Empty : subKeyPath[..index];
    }

    public static string LeafName(string subKeyPath)
    {
        int index = subKeyPath.LastIndexOf('\\');
        return index < 0 ? subKeyPath : subKeyPath[(index + 1)..];
    }
}
