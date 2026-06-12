using System.Diagnostics;

namespace ContextMenuManager11.Core.Explorer;

public sealed class ExplorerRestartService
{
    public void Restart()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(2500))
                {
                    process.Kill();
                    process.WaitForExit(2500);
                }
            }
            catch
            {
                // Explorer may already have exited or be protected by the current desktop state.
            }
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }
}
