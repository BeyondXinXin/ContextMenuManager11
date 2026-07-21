using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ContextMenuManager11.Services;

internal static partial class ShellCommandParser
{
    private static readonly HashSet<string> HostExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe",
        "conhost.exe",
        "explorer.exe",
        "mshta.exe",
        "powershell.exe",
        "pwsh.exe",
        "rundll32.exe"
    };

    public static string? TryGetExecutablePath(string command) => TryGetServicePath(command);

    public static string? TryGetServicePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(command.Trim()).Replace("\\\\", "\\", StringComparison.Ordinal);
        List<string> candidates = [];
        AddMatches(QuotedFileRegex(), expanded, candidates);
        AddMatches(AbsoluteFileRegex(), expanded, candidates);
        AddMatches(BareFileRegex(), expanded, candidates);

        List<string> resolved = candidates
            .Select(TryResolveExistingPath)
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return resolved.LastOrDefault(path => !HostExecutables.Contains(Path.GetFileName(path)))
            ?? resolved.FirstOrDefault();
    }

    public static ParsedIconLocation ParseIconLocation(string? iconLocation)
    {
        if (string.IsNullOrWhiteSpace(iconLocation))
        {
            return default;
        }

        string value = Environment.ExpandEnvironmentVariables(iconLocation.Trim()).TrimStart('@').Trim();
        int iconIndex = 0;
        int comma = value.LastIndexOf(',');
        if (comma > 0 && int.TryParse(value[(comma + 1)..].Trim(), out int parsedIndex))
        {
            iconIndex = parsedIndex;
            value = value[..comma];
        }

        value = value.Trim().Trim('"');
        string path = TryResolveExistingPath(value) ?? value;
        return new ParsedIconLocation(path, iconIndex);
    }

    public static string? TryResolveExistingPath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        string value = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.IsFile)
        {
            value = uri.LocalPath;
        }

        if (File.Exists(value))
        {
            return Path.GetFullPath(value);
        }

        if (Path.IsPathRooted(value))
        {
            return null;
        }

        foreach (string directory in new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) })
        {
            string path = Path.Combine(directory, value);
            if (File.Exists(path))
            {
                return path;
            }
        }

        foreach (RegistryKey root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            using RegistryKey? appPath = root.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{value}", false);
            string? registeredPath = appPath?.GetValue(string.Empty)?.ToString();
            if (File.Exists(registeredPath))
            {
                return Path.GetFullPath(registeredPath);
            }
        }

        return null;
    }

    private static void AddMatches(Regex regex, string input, ICollection<string> candidates)
    {
        foreach (Match match in regex.Matches(input))
        {
            string value = match.Groups["path"].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(value);
            }
        }
    }

    [GeneratedRegex("\\\"(?<path>[^\\\"]+\\.(?:exe|dll|ocx|cpl|com))\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuotedFileRegex();

    [GeneratedRegex("(?<path>(?:[A-Za-z]:\\\\|\\\\\\\\)[^\\\",;]*?\\.(?:exe|dll|ocx|cpl|com))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AbsoluteFileRegex();

    [GeneratedRegex("(?<![\\w.\\\\-])(?<path>[\\w.-]+\\.(?:exe|dll|ocx|cpl|com))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BareFileRegex();
}

internal readonly record struct ParsedIconLocation(string Path, int Index)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Path);
}
