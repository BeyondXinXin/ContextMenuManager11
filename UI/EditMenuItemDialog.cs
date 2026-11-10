using ContextMenuManager11.Models;

namespace ContextMenuManager11.UI;

internal sealed class EditMenuItemDialog : Form
{
    private readonly TextBox _displayName = CreateTextBox();
    private readonly TextBox _command = CreateTextBox();
    private readonly TextBox _icon = CreateTextBox();

    public EditMenuItemDialog(ContextMenuItem item)
    {
        SuspendLayout();
        Text = "编辑菜单项";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 292);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Font();

        _displayName.Text = item.DisplayName;
        _command.Text = item.Command;
        _icon.Text = item.Icon;
        Controls.Add(CreateLayout());
        ResumeLayout(true);
    }

    public string DisplayNameValue => _displayName.Text.Trim();

    public string CommandValue => _command.Text.Trim();

    public string IconValue => _icon.Text.Trim();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowEffects.Apply(Handle);
        _displayName.SelectAll();
        _displayName.Focus();
    }

    private Control CreateLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.Controls.Add(CreateFields(), 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        return root;
    }

    private Control CreateFields()
    {
        TableLayoutPanel fields = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(22, 16, 22, 8),
            BackColor = Theme.Window
        };
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        fields.Controls.Add(CreateLabel("显示名称"), 0, 0);
        fields.Controls.Add(_displayName, 0, 1);
        fields.Controls.Add(CreateLabel("命令"), 0, 2);
        fields.Controls.Add(_command, 0, 3);
        fields.Controls.Add(CreateLabel("图标"), 0, 4);
        fields.Controls.Add(_icon, 0, 5);
        fields.Controls.Add(new Label
        {
            Text = "仅静态 shell 菜单项支持编辑。",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.Font(8.5F)
        }, 0, 6);
        return fields;
    }

    private Control CreateFooter()
    {
        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 16, 8),
            BackColor = Theme.Subtle
        };

        ModernButton save = new()
        {
            Text = "保存",
            Width = 92,
            Height = 32,
            ButtonStyle = ModernButtonStyle.Primary
        };
        save.Click += Save_Click;

        ModernButton cancel = new()
        {
            Text = "取消",
            Width = 92,
            Height = 32,
            Margin = new Padding(8, 0, 0, 0),
            ButtonStyle = ModernButtonStyle.Secondary,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = save;
        CancelButton = cancel;
        footer.Controls.Add(save);
        footer.Controls.Add(cancel);
        return footer;
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DisplayNameValue))
        {
            MessageBox.Show(this, "显示名称不能为空。", "编辑菜单项", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _displayName.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static TextBox CreateTextBox() => new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Font(9.5F),
        Margin = new Padding(0, 2, 0, 3)
    };

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Theme.Text,
        TextAlign = ContentAlignment.BottomLeft,
        Font = Theme.Font(9F, FontStyle.Bold),
        Margin = new Padding(0)
    };
}
