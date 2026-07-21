using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ContextMenuManager11.UI;

internal static class Theme
{
    public static readonly Color Window = Color.White;
    public static readonly Color Card = Color.White;
    public static readonly Color Subtle = Color.FromArgb(245, 245, 245);
    public static readonly Color Input = Color.White;
    public static readonly Color Button = Color.FromArgb(248, 248, 248);
    public static readonly Color Disabled = Color.FromArgb(232, 232, 232);
    public static readonly Color DisabledText = Color.FromArgb(132, 132, 132);
    public static readonly Color Border = Color.FromArgb(198, 198, 198);
    public static readonly Color Text = Color.FromArgb(16, 16, 16);
    public static readonly Color Muted = Color.FromArgb(72, 72, 72);
    public static readonly Color Accent = Color.FromArgb(0, 90, 158);
    public static readonly Color AccentHover = Color.FromArgb(0, 78, 140);
    public static readonly Color AccentLight = Color.FromArgb(226, 241, 252);
    public static readonly Color Success = Color.FromArgb(16, 124, 16);
    public static readonly Color Warning = Color.FromArgb(157, 93, 0);
    public static readonly Color Danger = Color.FromArgb(196, 43, 28);

    public static Font Font(float size = 9F, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", size, style, GraphicsUnit.Point);

    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(1, radius * 2);
        Rectangle arc = new(bounds.Location, new Size(diameter, diameter));
        GraphicsPath path = new();
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class WindowEffects
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Apply(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        int noRound = 1;
        int noBackdrop = 1;
        int dark = 0;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref noRound, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref noBackdrop, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
