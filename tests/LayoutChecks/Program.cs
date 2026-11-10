using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Run once normally on the current monitor, and once with --unaware for 96 DPI.
        Application.SetHighDpiMode(args.Contains("--unaware") ? HighDpiMode.DpiUnaware : HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 10));
        Type type = Assembly.Load("ContextMenuManager11").GetType("ContextMenuManager11.MainForm")!;
        using Form form = (Form)Activator.CreateInstance(type, true)!;
        form.Show();
        Application.DoEvents();
        var busy = type.GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while ((bool)busy.GetValue(form)! && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }
        DataGridView grid = (DataGridView)type.GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Size initial = form.Size;
        foreach (Size size in new[] { initial, form.MinimumSize, new Size(initial.Width + 200, initial.Height) })
        {
            form.Size = size;
            Application.DoEvents();
            Verify(form);
            if (grid.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width) > grid.ClientSize.Width)
                throw new Exception("Grid columns extend beyond the visible list.");
            if (grid.ColumnHeadersHeight < grid.Font.Height || grid.RowTemplate.Height < grid.Font.Height)
                throw new Exception("Grid text is taller than its row.");
            string output = Path.Combine(AppContext.BaseDirectory, $"layout-{form.DeviceDpi}-{form.Width}.png");
            using Bitmap bitmap = new(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(output);
            Console.WriteLine($"PASS dpi={form.DeviceDpi}, size={form.Size}, rows={grid.Rows.Count}: {output}");
        }
        form.Close();
    }

    private static void Verify(Control parent)
    {
        if (parent is DataGridView) return;
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            if (!parent.ClientRectangle.Contains(child.Bounds))
                throw new Exception($"{child.GetType().Name} '{child.Text}' {child.Bounds} exceeds {parent.GetType().Name} {parent.ClientRectangle}");
            if (child is Button && child.Height < child.Font.Height + 4)
                throw new Exception($"Button '{child.Text}' clips its text.");
            Verify(child);
        }
    }
}
