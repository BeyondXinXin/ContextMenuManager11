using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ContextMenuManager11.App.Converters;

/// <summary>
/// 将文件路径转换为 WPF ImageSource，尝试提取关联图标。
/// </summary>
public sealed class IconConverter : IValueConverter
{
    private static readonly BitmapSource DefaultIcon = CreateDefaultIcon();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return DefaultIcon;

        try
        {
            if (!System.IO.File.Exists(path))
                return DefaultIcon;

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return DefaultIcon;

            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return DefaultIcon;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static BitmapSource CreateDefaultIcon()
    {
        // 创建一个 16x16 的占位图标
        var width = 16;
        var height = 16;
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * stride + x * 4;
                pixels[idx + 0] = 0xCC; // B
                pixels[idx + 1] = 0xCC; // G
                pixels[idx + 2] = 0xCC; // R
                pixels[idx + 3] = 0xFF; // A
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, pixels, stride);

        bitmap.Freeze();
        return bitmap;
    }
}
