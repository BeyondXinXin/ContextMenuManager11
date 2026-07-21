using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace ContextMenuManager11.Services;

internal static class WindowsResourceResolver
{
    public static string ResolveMenuText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string text = Environment.ExpandEnvironmentVariables(value.Trim());
        if (text.StartsWith('@'))
        {
            StringBuilder buffer = new(2048);
            if (SHLoadIndirectString(text, buffer, (uint)buffer.Capacity, IntPtr.Zero) != 0)
            {
                return string.Empty;
            }

            text = buffer.ToString();
        }

        return CleanMenuText(text);
    }

    public static string CleanMenuText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        const string escapedAmpersand = "\u0001";
        string cleaned = value
            .Replace("&&", escapedAmpersand, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace(escapedAmpersand, "&", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return string.Concat(cleaned.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format));
    }

    [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHLoadIndirectString(
        string source,
        StringBuilder outputBuffer,
        uint outputBufferLength,
        IntPtr reserved);
}
