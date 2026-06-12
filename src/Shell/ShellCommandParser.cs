using System.IO;

namespace ContextMenuManager11.Core.Shell;

public static class ShellCommandParser
{
    public static string? TryGetExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        string candidate;
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end <= 1)
            {
                return null;
            }

            candidate = text[1..end];
        }
        else
        {
            var exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex < 0)
            {
                return null;
            }

            candidate = text[..(exeIndex + 4)].Trim();
        }

        return File.Exists(candidate) ? candidate : null;
    }
}
