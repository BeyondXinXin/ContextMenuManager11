using System.Diagnostics;

namespace ContextMenuManager11.Services;

internal sealed class ExplorerRestartService
{
    public void Restart()
    {
        foreach (Process process in Process.GetProcessesByName("explorer"))
        {
            using (process)
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
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }
}
