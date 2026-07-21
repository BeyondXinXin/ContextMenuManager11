using System.Runtime.InteropServices;
using ContextMenuManager11.Models;
using ContextMenuManager11.UI;

namespace ContextMenuManager11.Services;

internal sealed class FileIconService : IDisposable
{
    private const uint FileAttributeNormal = 0x80;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiUseFileAttributes = 0x000000010;

    private readonly Dictionary<string, Image> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Image GetIcon(ContextMenuItem item)
    {
        string key = $"item|{item.IconResourcePath}|{item.IconResourceIndex}|{item.FileExtension}|{item.Scope}|{item.BuiltInIcon}";
        return GetOrCreate(key, () =>
        {
            if (!string.IsNullOrWhiteSpace(item.BuiltInIcon))
            {
                return CreateBuiltInIcon(item.BuiltInIcon);
            }

            if (Path.GetExtension(item.IconResourcePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                Image? shortcutIcon = GetShellFileIcon(item.IconResourcePath, false);
                if (shortcutIcon is not null)
                {
                    return shortcutIcon;
                }
            }

            Image? image = ExtractResourceIcon(item.IconResourcePath, item.IconResourceIndex);
            if (image is not null)
            {
                return image;
            }

            if (!string.IsNullOrWhiteSpace(item.FileExtension))
            {
                image = GetShellFileIcon(item.FileExtension, true);
                if (image is not null)
                {
                    return image;
                }
            }

            if (File.Exists(item.ServicePath) || Directory.Exists(item.ServicePath))
            {
                image = GetShellFileIcon(item.ServicePath, false);
                if (image is not null)
                {
                    return image;
                }
            }

            ParsedIconLocation fallback = GetScopeIconLocation(item.Scope);
            return ExtractResourceIcon(fallback.Path, fallback.Index) ?? SystemIcons.Application.ToBitmap();
        });
    }

    public Image GetScopeIcon(ContextMenuScope? scope)
    {
        ParsedIconLocation location = GetScopeIconLocation(scope);
        return GetResourceIcon(location.Path, location.Index);
    }

    public Image GetResourceIcon(string path, int index) =>
        GetOrCreate($"resource|{path}|{index}", () => ExtractResourceIcon(path, index) ?? SystemIcons.Application.ToBitmap());

    public void Dispose()
    {
        foreach (Image image in _cache.Values.Distinct())
        {
            image.Dispose();
        }

        _cache.Clear();
    }

    private Image GetOrCreate(string key, Func<Image> factory)
    {
        if (_cache.TryGetValue(key, out Image? cached))
        {
            return cached;
        }

        Image image = factory();
        _cache[key] = image;
        return image;
    }

    private static Image? ExtractResourceIcon(string? resourcePath, int resourceIndex)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        string path = ShellCommandParser.TryResolveExistingPath(resourcePath)
            ?? Environment.ExpandEnvironmentVariables(resourcePath.Trim().Trim('"'));
        if (!File.Exists(path))
        {
            return null;
        }

        IntPtr[] largeIcons = [IntPtr.Zero];
        IntPtr[] smallIcons = [IntPtr.Zero];
        try
        {
            if (ExtractIconEx(path, resourceIndex, largeIcons, smallIcons, 1) == 0)
            {
                return GetShellFileIcon(path, false);
            }

            IntPtr handle = largeIcons[0] != IntPtr.Zero ? largeIcons[0] : smallIcons[0];
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            using Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            return icon.ToBitmap();
        }
        catch
        {
            return null;
        }
        finally
        {
            if (smallIcons[0] != IntPtr.Zero)
            {
                _ = DestroyIcon(smallIcons[0]);
            }

            if (largeIcons[0] != IntPtr.Zero)
            {
                _ = DestroyIcon(largeIcons[0]);
            }
        }
    }

    private static Image? GetShellFileIcon(string path, bool useFileAttributes)
    {
        ShFileInfo info = new();
        uint flags = ShgfiIcon | (useFileAttributes ? ShgfiUseFileAttributes : 0);
        IntPtr result = SHGetFileInfo(
            path,
            useFileAttributes ? FileAttributeNormal : 0,
            ref info,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            flags);
        if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using Icon icon = (Icon)Icon.FromHandle(info.IconHandle).Clone();
            return icon.ToBitmap();
        }
        finally
        {
            _ = DestroyIcon(info.IconHandle);
        }
    }

    private static Image CreateBuiltInIcon(string name)
    {
        Bitmap bitmap = new(28, 28);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using Pen pen = new(Theme.Accent, 1.8F)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        switch (name)
        {
            case "view":
                graphics.DrawRectangle(pen, 4, 5, 8, 7);
                graphics.DrawRectangle(pen, 16, 5, 8, 7);
                graphics.DrawRectangle(pen, 4, 16, 8, 7);
                graphics.DrawRectangle(pen, 16, 16, 8, 7);
                break;
            case "sort":
                graphics.DrawLine(pen, 4, 7, 16, 7);
                graphics.DrawLine(pen, 4, 13, 13, 13);
                graphics.DrawLine(pen, 4, 19, 10, 19);
                graphics.DrawLine(pen, 21, 5, 21, 22);
                graphics.DrawLine(pen, 18, 19, 21, 22);
                graphics.DrawLine(pen, 24, 19, 21, 22);
                break;
            case "group":
                graphics.DrawRectangle(pen, 4, 5, 20, 6);
                graphics.DrawRectangle(pen, 4, 16, 9, 7);
                graphics.DrawRectangle(pen, 17, 16, 7, 7);
                break;
            default:
                graphics.DrawArc(pen, 4, 4, 20, 20, -55, 285);
                graphics.DrawLine(pen, 20, 4, 24, 5);
                graphics.DrawLine(pen, 20, 4, 21, 8);
                break;
        }

        return bitmap;
    }

    private static ParsedIconLocation GetScopeIconLocation(ContextMenuScope? scope) => scope switch
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
        ContextMenuScope.NewMenu => new ParsedIconLocation("shell32.dll", -16769),
        ContextMenuScope.SendTo => new ParsedIconLocation("imageres.dll", -185),
        ContextMenuScope.OpenWith => new ParsedIconLocation("shell32.dll", -16770),
        _ => new ParsedIconLocation("imageres.dll", -19)
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(
        string fileName,
        int iconIndex,
        IntPtr[] largeIcons,
        IntPtr[] smallIcons,
        uint iconCount);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
}
