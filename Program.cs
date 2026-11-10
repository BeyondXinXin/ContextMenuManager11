namespace ContextMenuManager11;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(UI.Theme.Font());
        Application.Run(new MainForm());
    }
}
