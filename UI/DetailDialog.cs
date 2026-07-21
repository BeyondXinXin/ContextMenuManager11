using ContextMenuManager11.Models;

namespace ContextMenuManager11.UI;

internal sealed class DetailDialog : Form
{
    public DetailDialog(ContextMenuItem item)
    {
        Text = "菜单项属性";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(540, 460);
        ClientSize = new Size(640, 620);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Font();
        Controls.Add(CreateLayout(item));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowEffects.Apply(Handle);
    }

    private Control CreateLayout(ContextMenuItem item)
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.Controls.Add(CreateDetails(item), 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        return root;
    }

    private static Control CreateDetails(ContextMenuItem item)
    {
        TableLayoutPanel details = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = Theme.Window
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(details, "名称", item.DisplayName);
        AddRow(details, "说明", item.Description, 46);
        AddRow(details, "状态", item.StatusText);
        AddRow(details, "状态说明", item.VisibilityDetail);
        AddRow(details, "作用范围", item.ScopeText);
        AddRow(details, "菜单类型", item.KindText);
        AddRow(details, "来源", item.RootText);
        AddRow(details, "注册表路径", item.RegistryPath, 54);
        AddRow(details, "禁用后路径", item.DisabledRegistryPath, 54);
        AddRow(details, "命令", item.Command, 64);
        AddRow(details, "服务文件", item.ServicePath, 54);
        AddRow(details, "图标", item.Icon, 46);
        AddRow(details, "CLSID", item.Clsid, 46);
        AddRow(details, "风险等级", item.RiskLevel);
        AddRow(details, "最后修改时间", item.LastModifiedTime);
        return details;
    }

    private Control CreateFooter()
    {
        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 8),
            BackColor = Theme.Subtle
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        ModernButton close = new()
        {
            Text = "关闭",
            Dock = DockStyle.Fill,
            ButtonStyle = ModernButtonStyle.Secondary,
            DialogResult = DialogResult.OK
        };
        AcceptButton = close;
        CancelButton = close;
        footer.Controls.Add(close, 1, 0);
        return footer;
    }

    private static void AddRow(TableLayoutPanel details, string label, string value, int height = 36)
    {
        int row = details.RowCount++;
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        details.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Text,
            Font = Theme.Font(9F, FontStyle.Bold),
            Padding = new Padding(0, 5, 8, 0)
        }, 0, row);
        details.Controls.Add(new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Subtle,
            ForeColor = Theme.Text,
            Font = Theme.Font(9F),
            Margin = new Padding(0, 2, 0, 4)
        }, 1, row);
    }
}
