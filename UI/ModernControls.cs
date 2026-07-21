using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ContextMenuManager11.UI;

internal sealed class RoundedPanel : Panel
{
    private int _radius = 2;
    private Color _fillColor = Theme.Card;
    private Color _borderColor = Theme.Border;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Padding = new Padding(1);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = Theme.RoundedRectangle(bounds, Radius);
        using SolidBrush fill = new(FillColor);
        using Pen border = new(BorderColor);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }
}

internal enum ModernButtonStyle
{
    Primary,
    Secondary,
    Danger,
    Ghost
}

internal sealed class ModernButton : Button
{
    private ModernButtonStyle _buttonStyle;
    private bool _hovered;
    private bool _pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = false;
        Height = 30;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Theme.Font(9F, FontStyle.Bold);
        Padding = new Padding(12, 0, 12, 1);
        UseVisualStyleBackColor = false;
        ApplyStyle();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ModernButtonStyle ButtonStyle
    {
        get => _buttonStyle;
        set
        {
            _buttonStyle = value;
            ApplyStyle();
            Invalidate();
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (Enabled)
        {
            ApplyStyle();
        }
        else
        {
            BackColor = Theme.Disabled;
            ForeColor = Theme.DisabledText;
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Window);
        if (ClientSize.Width < 4 || ClientSize.Height < 6)
        {
            return;
        }

        Rectangle bounds = new(1, 1, ClientSize.Width - 3, ClientSize.Height - 4);
        Color fillColor = !Enabled
            ? Theme.Disabled
            : _pressed
                ? FlatAppearance.MouseDownBackColor
                : _hovered ? FlatAppearance.MouseOverBackColor : BackColor;
        Color borderColor = Enabled ? FlatAppearance.BorderColor : Theme.Border;
        Color textColor = Enabled ? ForeColor : Theme.DisabledText;
        using SolidBrush fill = new(fillColor);
        using Pen border = new(borderColor);
        e.Graphics.FillRectangle(fill, bounds);
        e.Graphics.DrawRectangle(border, bounds);

        bool leftAligned = TextAlign is ContentAlignment.TopLeft
            or ContentAlignment.MiddleLeft
            or ContentAlignment.BottomLeft;
        Rectangle textBounds = leftAligned
            ? new Rectangle(bounds.Left + Padding.Left, bounds.Top, Math.Max(0, bounds.Width - Padding.Horizontal), bounds.Height)
            : bounds;
        if (Image is not null)
        {
            const int iconSize = 20;
            int imageX = leftAligned
                ? bounds.Left + Padding.Left
                : bounds.Left + Math.Max(6, (bounds.Width - iconSize - TextRenderer.MeasureText(Text, Font).Width - 8) / 2);
            Rectangle imageBounds = new(
                imageX,
                bounds.Top + (bounds.Height - iconSize) / 2,
                iconSize,
                iconSize);
            e.Graphics.DrawImage(Image, imageBounds);
            textBounds = new Rectangle(
                imageBounds.Right + 9,
                bounds.Top,
                Math.Max(0, bounds.Right - imageBounds.Right - 13),
                bounds.Height);
        }

        TextFormatFlags horizontalAlignment = leftAligned
            ? TextFormatFlags.Left
            : TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            textColor,
            horizontalAlignment | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (Focused && ShowFocusCues)
        {
            Rectangle focus = Rectangle.Inflate(bounds, -3, -3);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, textColor, fillColor);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    private void ApplyStyle()
    {
        (BackColor, ForeColor, FlatAppearance.BorderColor, FlatAppearance.BorderSize) = ButtonStyle switch
        {
            ModernButtonStyle.Primary => (Theme.AccentLight, Theme.Accent, Theme.Accent, 1),
            ModernButtonStyle.Danger => (Theme.Button, Theme.Danger, Theme.Danger, 1),
            ModernButtonStyle.Ghost => (Theme.Button, Theme.Text, Theme.Border, 1),
            _ => (Theme.Button, Theme.Text, Theme.Border, 1)
        };

        FlatAppearance.MouseOverBackColor = ButtonStyle switch
        {
            ModernButtonStyle.Primary => Theme.AccentLight,
            ModernButtonStyle.Danger => Color.FromArgb(253, 238, 236),
            _ => Theme.Subtle
        };
        FlatAppearance.MouseDownBackColor = FlatAppearance.MouseOverBackColor;
    }
}

internal sealed class SlimProgressBar : Control
{
    public SlimProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 3;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using SolidBrush track = new(Theme.Border);
        using SolidBrush fill = new(Theme.Accent);
        e.Graphics.FillRectangle(track, ClientRectangle);
        e.Graphics.FillRectangle(fill, new Rectangle(0, 0, Math.Max(16, Width / 3), Height));
    }
}

internal sealed class ModernCheckBox : CheckBox
{
    public ModernCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Window);
        Color outline = Enabled ? Theme.Muted : Theme.Border;
        Color text = Enabled ? Theme.Text : Theme.Muted;
        Rectangle box = new(1, Math.Max(1, (Height - 14) / 2), 13, 13);
        using Pen border = new(Checked && Enabled ? Theme.Accent : outline, 1.3F);
        using SolidBrush fill = new(Checked && Enabled ? Theme.Accent : Theme.Input);
        e.Graphics.FillRectangle(fill, box);
        e.Graphics.DrawRectangle(border, box);
        if (Checked)
        {
            using Pen check = new(Color.White, 1.7F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLines(check,
            [
                new Point(box.X + 3, box.Y + 7),
                new Point(box.X + 6, box.Y + 10),
                new Point(box.X + 11, box.Y + 4)
            ]);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(22, 0, Math.Max(0, Width - 22), Height),
            text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

internal sealed class ModernComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmNcPaint = 0x0085;

    public ModernComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 22;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color backgroundColor = !Enabled ? Theme.Disabled : selected ? Theme.AccentLight : Theme.Input;
        Color textColor = Enabled ? Theme.Text : Theme.DisabledText;
        using SolidBrush background = new(backgroundColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            e.Bounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is WmPaint or WmNcPaint && IsHandleCreated)
        {
            using Graphics graphics = CreateGraphics();
            using Pen border = new(Enabled && Focused ? Theme.Accent : Theme.Border);
            graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
    }
}
