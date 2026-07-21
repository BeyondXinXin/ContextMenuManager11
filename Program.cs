namespace ContextMenuManager11;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Segoe UI Variable Text", 9F));
        Application.Run(new MainForm());
    }
}
