using System.Diagnostics;
using ContextMenuManager11.Models;
using Microsoft.Win32;

namespace ContextMenuManager11.Services;

internal sealed class ShellMetadataResolver
{
    private readonly Dictionary<Guid, ShellMetadata> _cache = [];

    private static readonly IReadOnlyDictionary<Guid, string> KnownDisplayNames = new Dictionary<Guid, string>
    {
        [new Guid("5B69A6B4-393B-459C-8EBB-214237A9E7AC")] = "Bandizip",
        [new Guid("09A47860-11B0-4DA5-AFA5-26D86198A780")] = "Microsoft Defender",
        [new Guid("6D85624F-305A-491D-8848-C1927AA0D790")] = "上传到百度网盘",
        [new Guid("67F4D210-BFC2-4ADD-9A2A-C9B9E1F42C4F")] = "上传到“WPS云文档”",
        [new Guid("AA147FFB-0B1F-4BB1-9B1E-8D062B35C145")] = "自动同步文件夹到“WPS云文档”",
        [new Guid("AF1D7D2F-6AE8-4BAA-ABFA-738201F4871C")] = "WPS Office PDF",
        [new Guid("10A0FDD2-B0C0-4CD4-A7AE-E594CE3B91C8")] = "TortoiseGit",
        [new Guid("09799AFB-AD67-11D1-ABCD-00C04FC30936")] = "打开方式",
        [new Guid("A470F8CF-A1E8-4F65-8335-227475AA5C46")] = "加密",
        [new Guid("F81E9010-6EA4-11CE-A7FF-00AA003CA9F6")] = "授予访问权限",
        [new Guid("A2A9545D-A0C2-42B4-9708-A0B2BADD77C8")] = "固定到“开始”列表",
        [new Guid("90AA3A4E-1CBA-4233-B8BB-535773D48449")] = "固定到任务栏",
        [new Guid("E61BF828-5E63-4287-BEF1-60B1A4FDE0E3")] = "工作文件夹"
    };

    private static readonly IReadOnlyDictionary<Guid, string> KnownIconLocations = new Dictionary<Guid, string>
    {
        [new Guid("09799AFB-AD67-11D1-ABCD-00C04FC30936")] = "imageres.dll,-5340",
        [new Guid("A2A9545D-A0C2-42B4-9708-A0B2BADD77C8")] = "imageres.dll,-80",
        [new Guid("90AA3A4E-1CBA-4233-B8BB-535773D48449")] = "imageres.dll,-5354",
        [new Guid("A470F8CF-A1E8-4F65-8335-227475AA5C46")] = "imageres.dll,-59",
        [new Guid("E61BF828-5E63-4287-BEF1-60B1A4FDE0E3")] = "WorkFolders.exe",
        [new Guid("5B69A6B4-393B-459C-8EBB-214237A9E7AC")] = @".\Bandizip.exe",
        [new Guid("09A47860-11B0-4DA5-AFA5-26D86198A780")] = @".\EppManifest.dll",
        [new Guid("6D85624F-305A-491D-8848-C1927AA0D790")] = @".\BaiduNetdisk.exe",
        [new Guid("67F4D210-BFC2-4ADD-9A2A-C9B9E1F42C4F")] = @"..\..\ksolaunch.exe",
        [new Guid("10A0FDD2-B0C0-4CD4-A7AE-E594CE3B91C8")] = @".\TortoiseGitProc.exe",
        [new Guid("AF1D7D2F-6AE8-4BAA-ABFA-738201F4871C")] = @".\wpspdf.exe"
    };

    public ShellMetadata Resolve(string? clsid)
    {
        if (!Guid.TryParse(clsid, out Guid guid) || guid == Guid.Empty)
        {
            return ShellMetadata.Empty;
        }

        if (_cache.TryGetValue(guid, out ShellMetadata? cached))
        {
            return cached;
        }

        ShellMetadata metadata = ReadMetadata(guid);
        _cache[guid] = metadata;
        return metadata;
    }

    public static string GetFileDescription(string? filePath)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(filePath);
            return FirstMeaningful(info.FileDescription, info.ProductName, info.CompanyName, Path.GetFileName(filePath));
        }
        catch
        {
            return Path.GetFileName(filePath);
        }
    }

    private static ShellMetadata ReadMetadata(Guid guid)
    {
        string displayName = string.Empty;
        string description = string.Empty;
        string filePath = string.Empty;
        ParsedIconLocation icon = default;
        string registryPath = string.Empty;

        foreach ((RegistryRoot root, string basePath) in ClsidLocations())
        {
            string subKeyPath = $@"{basePath}\{guid:B}";
            using RegistryKey? key = RegistryPath.RootKey(root).OpenSubKey(subKeyPath, false);
            if (key is null)
            {
                continue;
            }

            registryPath = string.IsNullOrEmpty(registryPath) ? RegistryPath.FullPath(root, subKeyPath) : registryPath;
            displayName = FirstMeaningful(
                displayName,
                ResolveValue(key, "LocalizedString"),
                ResolveValue(key, "InfoTip"),
                ResolveValue(key, string.Empty));

            using (RegistryKey? defaultIcon = key.OpenSubKey("DefaultIcon", false))
            {
                if (icon.IsEmpty)
                {
                    icon = ShellCommandParser.ParseIconLocation(defaultIcon?.GetValue(string.Empty)?.ToString());
                }
            }

            if (string.IsNullOrEmpty(filePath))
            {
                filePath = FindServerPath(key);
            }
        }

        description = GetFileDescription(filePath);
        KnownDisplayNames.TryGetValue(guid, out string? knownDisplayName);
        displayName = FirstMeaningful(knownDisplayName, displayName, description);
        ParsedIconLocation knownIcon = ResolveKnownIcon(guid, filePath);
        if (!knownIcon.IsEmpty)
        {
            icon = knownIcon;
        }

        if (icon.IsEmpty && !string.IsNullOrWhiteSpace(filePath))
        {
            icon = new ParsedIconLocation(filePath, 0);
        }

        return new ShellMetadata(displayName, description, filePath, icon.Path ?? string.Empty, icon.Index, registryPath);
    }

    private static ParsedIconLocation ResolveKnownIcon(Guid guid, string servicePath)
    {
        if (!KnownIconLocations.TryGetValue(guid, out string? iconLocation))
        {
            return default;
        }

        ParsedIconLocation icon = ShellCommandParser.ParseIconLocation(iconLocation);
        if (Path.IsPathRooted(icon.Path) && File.Exists(icon.Path))
        {
            return icon;
        }

        if (!string.IsNullOrWhiteSpace(servicePath))
        {
            string? directory = Path.GetDirectoryName(servicePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                string candidate = Path.GetFullPath(Path.Combine(directory, icon.Path));
                if (File.Exists(candidate))
                {
                    return new ParsedIconLocation(candidate, icon.Index);
                }
            }
        }

        string? systemPath = ShellCommandParser.TryResolveExistingPath(icon.Path);
        return systemPath is null ? default : new ParsedIconLocation(systemPath, icon.Index);
    }

    private static string FindServerPath(RegistryKey clsidKey)
    {
        foreach (string keyName in new[] { "InprocServer32", "LocalServer32" })
        {
            using RegistryKey? serverKey = clsidKey.OpenSubKey(keyName, false);
            if (serverKey is null)
            {
                continue;
            }

            string? codeBase = serverKey.GetValue("CodeBase")?.ToString();
            string? path = ShellCommandParser.TryGetServicePath(codeBase)
                ?? ShellCommandParser.TryGetServicePath(serverKey.GetValue(string.Empty)?.ToString());
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static string ResolveValue(RegistryKey key, string valueName) =>
        WindowsResourceResolver.ResolveMenuText(key.GetValue(valueName)?.ToString());

    private static IEnumerable<(RegistryRoot Root, string Path)> ClsidLocations()
    {
        yield return (RegistryRoot.HKCU, @"Software\Classes\CLSID");
        yield return (RegistryRoot.HKLM, @"Software\Classes\CLSID");
        yield return (RegistryRoot.HKLM, @"Software\Classes\WOW6432Node\CLSID");
    }

    private static string FirstMeaningful(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

internal sealed record ShellMetadata(
    string DisplayName,
    string Description,
    string FilePath,
    string IconPath,
    int IconIndex,
    string RegistryPath)
{
    public static readonly ShellMetadata Empty = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        string.Empty);
}
