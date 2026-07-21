using System.ComponentModel;
using System.Diagnostics;
using ContextMenuManager11.Models;
using ContextMenuManager11.Services;
using ContextMenuManager11.UI;

namespace ContextMenuManager11;

internal sealed class MainForm : Form
{
    private readonly RegistryScanner _scanner = new();
    private readonly RegistryEditService _editService = new();
    private readonly ExplorerRestartService _explorerService = new();
    private readonly FileIconService _iconService = new();
    private readonly BindingList<ContextMenuItem> _visibleItems = [];
    private readonly BindingSource _bindingSource = new();
    private readonly List<ContextMenuItem> _allItems = [];
    private readonly List<(ModernButton Button, NavigationEntry Entry)> _navigationButtons = [];
    private readonly SplitContainer _mainSplit = new();
    private readonly Panel _listPage = new();
    private readonly Panel _settingsPage = new();
    private readonly DataGridView _grid = new();
    private readonly Label _itemCount = new();
    private readonly Label _status = new();
    private readonly SlimProgressBar _progress = new();
    private readonly ModernCheckBox _classicMenuCheck = new();
    private readonly ModernCheckBox _enabledOnlyCheck = new();
    private readonly ModernButton _refreshButton = CreateButton("刷新", ModernButtonStyle.Secondary, 82);
    private readonly ModernButton _restartExplorerButton = CreateButton("重启 Explorer", ModernButtonStyle.Secondary, 118);
    private readonly ToolStripMenuItem _toggleMenuItem = new("启用 / 禁用");
    private readonly ToolStripMenuItem _editMenuItem = new("编辑");
    private readonly ToolStripMenuItem _propertiesMenuItem = new("属性");
    private readonly ToolStripMenuItem _openRegistryMenuItem = new("打开注册表");
    private readonly ToolStripMenuItem _openFileMenuItem = new("打开文件位置");
    private readonly ToolStripMenuItem _deleteMenuItem = new("删除");
    private readonly ToolStripMenuItem _refreshMenuItem = new("刷新");

    private readonly ClassicContextMenuService _classicService;
    private NavigationEntry _selectedNavigation = NavigationEntries[0];
    private bool _busy;
    private bool _closing;
    private bool _loadingClassicState;

    private static readonly NavigationEntry[] NavigationEntries =
    [
        new("文件", ContextMenuScope.File),
        new("文件夹", ContextMenuScope.Folder),
        new("目录", ContextMenuScope.Directory),
        new("目录背景", ContextMenuScope.DirectoryBackground),
        new("桌面背景", ContextMenuScope.DesktopBackground),
        new("磁盘分区", ContextMenuScope.Drive),
        new("所有对象", ContextMenuScope.AllFileSystemObjects),
        new("此电脑", ContextMenuScope.Computer),
        new("回收站", ContextMenuScope.RecycleBin),
        new("库", ContextMenuScope.Library),
        new("新建菜单", ContextMenuScope.NewMenu),
        new("发送到", ContextMenuScope.SendTo),
        new("打开方式", ContextMenuScope.OpenWith),
        new("Win11 设置", null, true)
    ];

    public MainForm()
    {
        _classicService = new ClassicContextMenuService(_editService);
        Text = "ContextMenuManager11";
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Font();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        ClientSize = new Size(960, 620);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        LoadApplicationIcon();

        BuildLayout();
        ConfigureGrid();
        WireEvents();
        SelectNavigation(NavigationEntries[0]);
        LoadClassicMenuState();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowEffects.Apply(Handle);
        _mainSplit.SplitterDistance = 184;
        BeginInvoke(async () => await RefreshItemsAsync());
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel)
        {
            return;
        }

        _closing = true;

        // 先停止表格事件并解除数据绑定。若先 Dispose BindingSource，DataGridView
        // 会在结束当前单元格编辑时再次格式化已经不存在的行，导致 CurrencyManager 越界。
        _grid.CellFormatting -= Grid_CellFormatting;
        _grid.CellPainting -= Grid_CellPainting;
        _grid.CellClick -= Grid_CellClick;
        _grid.CellDoubleClick -= Grid_CellDoubleClick;
        _grid.CellMouseDown -= Grid_CellMouseDown;
        _grid.KeyDown -= Grid_KeyDown;
        _grid.DataSource = null;
        _bindingSource.DataSource = null;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _bindingSource.Dispose();
        _iconService.Dispose();
        base.OnFormClosed(e);
    }

    private void LoadApplicationIcon()
    {
        using Stream? iconStream = typeof(MainForm).Assembly.GetManifestResourceStream(
            "ContextMenuManager11.Assets.ContextMenuManager11.ico");
        if (iconStream is null)
        {
            return;
        }

        using Icon embeddedIcon = new(iconStream);
        Icon = (Icon)embeddedIcon.Clone();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // SplitContainer 在 CreateContent 中尚未完成 Dock 布局时只有默认宽度，
        // 此时直接设置两个最小尺寸会使约束无解并抛出异常。
        ApplyMainSplitMinimumSizes();
    }

    private void ApplyMainSplitMinimumSizes()
    {
        if (_mainSplit.IsDisposed || _mainSplit.Width <= 0)
        {
            return;
        }

        int availableWidth = _mainSplit.Width - _mainSplit.SplitterWidth;
        int panel1MinSize = Math.Min(176, Math.Max(0, availableWidth));
        int panel2MinSize = Math.Min(560, Math.Max(0, availableWidth - panel1MinSize));

        _mainSplit.Panel1MinSize = panel1MinSize;
        _mainSplit.Panel2MinSize = panel2MinSize;
    }

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border }, 0, 0);
        root.Controls.Add(CreateContent(), 0, 1);
        root.Controls.Add(CreateStatusBar(), 0, 2);
        Controls.Add(root);
    }

    private Control CreateContent()
    {
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.Orientation = Orientation.Vertical;
        _mainSplit.SplitterWidth = 1;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _mainSplit.IsSplitterFixed = true;
        _mainSplit.Panel1.Controls.Add(CreateNavigation());

        _listPage.Dock = DockStyle.Fill;
        _listPage.BackColor = Theme.Card;
        _listPage.Controls.Add(CreateListPage());
        _settingsPage.Dock = DockStyle.Fill;
        _settingsPage.BackColor = Theme.Card;
        _settingsPage.Controls.Add(CreateSettingsPage());
        _settingsPage.Visible = false;
        _mainSplit.Panel2.Controls.Add(_settingsPage);
        _mainSplit.Panel2.Controls.Add(_listPage);
        return _mainSplit;
    }

    private Control CreateNavigation()
    {
        return CreateNavigationButtons();
    }

    private Control CreateNavigationButtons()
    {
        FlowLayoutPanel menu = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Window,
            Padding = new Padding(10, 12, 10, 12),
            AutoScroll = false
        };

        foreach (NavigationEntry entry in NavigationEntries)
        {
            if (entry.Title == "新建菜单" || entry.IsSettings)
            {
                menu.Controls.Add(new Panel
                {
                    Width = 1,
                    Height = 6,
                    BackColor = Theme.Window,
                    Margin = new Padding(0)
                });
            }

            ModernButton button = CreateButton(entry.Title, ModernButtonStyle.Secondary, 162);
            button.Height = 30;
            button.Margin = new Padding(0, 0, 0, 2);
            button.Image = entry.IsSettings
                ? _iconService.GetResourceIcon("shell32.dll", 316)
                : _iconService.GetScopeIcon(entry.Scope);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Click += (_, _) => SelectNavigation(entry);
            _navigationButtons.Add((button, entry));
            menu.Controls.Add(button);
        }

        menu.Resize += (_, _) =>
        {
            int width = Math.Max(120, menu.ClientSize.Width - menu.Padding.Horizontal - 1);
            foreach ((ModernButton button, _) in _navigationButtons)
            {
                button.Width = width;
            }
        };
        return menu;
    }

    private Control CreateListPage()
    {
        return CreateGridHost();
    }

    private Control CreateGridHost()
    {
        Panel host = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
            Padding = new Padding(8, 8, 8, 0)
        };
        _grid.Dock = DockStyle.Fill;
        host.Controls.Add(_grid);
        return host;
    }

    private Control CreateSettingsPage()
    {
        TableLayoutPanel page = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20, 18, 20, 18),
            BackColor = Theme.Card
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Card,
            Margin = new Padding(0)
        };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label
        {
            Text = "Windows 11 右键菜单",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = Theme.Font(11.5F, FontStyle.Bold)
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Text = "选择默认显示简化菜单还是经典完整菜单。",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = Theme.Font(8.7F)
        }, 0, 1);

        _classicMenuCheck.Text = "启用";
        _classicMenuCheck.Dock = DockStyle.Fill;
        _classicMenuCheck.Font = Theme.Font(9F, FontStyle.Bold);

        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(218, 218, 218),
            Padding = new Padding(1),
            Margin = new Padding(0, 8, 0, 0)
        };
        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Window,
            Padding = new Padding(16, 10, 14, 10)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(new Label
        {
            Text = "默认显示经典右键菜单",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = Theme.Font(9F, FontStyle.Bold)
        }, 0, 0);
        content.Controls.Add(new Label
        {
            Text = "跳过 Windows 11 简化菜单，直接打开 Windows 10 风格的完整菜单。",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = Theme.Font(8.6F)
        }, 0, 1);
        content.Controls.Add(_classicMenuCheck, 1, 0);
        content.SetRowSpan(_classicMenuCheck, 2);
        card.Controls.Add(content);

        page.Controls.Add(header, 0, 0);
        page.Controls.Add(card, 0, 1);
        return page;
    }

    private Control CreateStatusBar()
    {
        Panel host = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window
        };
        TableLayoutPanel bar = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Theme.Window,
            Padding = new Padding(12, 6, 8, 6)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _status.Text = "准备就绪";
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Theme.Muted;
        _status.Font = Theme.Font(8.7F);
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(8, 8, 8, 8);
        _progress.Visible = false;

        _itemCount.Text = "0 / 0 项";
        _itemCount.Dock = DockStyle.Fill;
        _itemCount.TextAlign = ContentAlignment.MiddleRight;
        _itemCount.ForeColor = Theme.Muted;
        _itemCount.Font = Theme.Font(8.7F);

        _enabledOnlyCheck.Text = "只看启用";
        _enabledOnlyCheck.Dock = DockStyle.Fill;
        _enabledOnlyCheck.Margin = new Padding(8, 0, 0, 0);
        _enabledOnlyCheck.Font = Theme.Font(8.7F);

        _refreshButton.Dock = DockStyle.Fill;
        _refreshButton.Margin = new Padding(4, 0, 0, 0);
        _restartExplorerButton.Dock = DockStyle.Fill;
        _restartExplorerButton.Margin = new Padding(4, 0, 0, 0);

        bar.Controls.Add(_status, 0, 0);
        bar.Controls.Add(_progress, 1, 0);
        bar.Controls.Add(_enabledOnlyCheck, 2, 0);
        bar.Controls.Add(_itemCount, 3, 0);
        bar.Controls.Add(_refreshButton, 4, 0);
        bar.Controls.Add(_restartExplorerButton, 5, 0);
        host.Controls.Add(bar);
        host.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border });
        return host;
    }

    private void ConfigureGrid()
    {
        _bindingSource.DataSource = _visibleItems;
        _grid.AutoGenerateColumns = false;
        _grid.DataSource = _bindingSource;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.ReadOnly = true;
        _grid.BackgroundColor = Theme.Card;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Theme.Border;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersHeight = 30;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowHeadersVisible = false;
        _grid.RowTemplate.Height = 32;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Subtle,
            ForeColor = Theme.Text,
            Font = Theme.Font(8.8F, FontStyle.Bold),
            SelectionBackColor = Theme.Subtle,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Card,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.AccentLight,
            SelectionForeColor = Theme.Text,
            Font = Theme.Font(8.8F),
            Padding = new Padding(6, 0, 6, 0)
        };

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            Name = "Icon",
            HeaderText = string.Empty,
            Width = 40,
            MinimumWidth = 40,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Resizable = DataGridViewTriState.False,
            Frozen = true,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(CreateTextColumn(
            "名称",
            nameof(ContextMenuItem.DisplayName),
            480,
            DataGridViewAutoSizeColumnMode.Fill));
        _grid.Columns.Add(CreateTextColumn("来源", nameof(ContextMenuItem.RootText), 64));
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ToggleAction",
            HeaderText = "状态",
            Width = 76,
            MinimumWidth = 76,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(0)
            }
        });
        _grid.ContextMenuStrip = CreateGridContextMenu();
    }

    private void WireEvents()
    {
        _refreshButton.Click += async (_, _) => await RefreshItemsAsync();
        _restartExplorerButton.Click += async (_, _) => await RestartExplorerAsync();
        _enabledOnlyCheck.CheckedChanged += (_, _) => ApplyFilters();
        _classicMenuCheck.CheckedChanged += async (_, _) => await ClassicMenuCheck_ChangedAsync();
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellPainting += Grid_CellPainting;
        _grid.CellClick += Grid_CellClick;
        _grid.CellDoubleClick += Grid_CellDoubleClick;
        _grid.CellMouseDown += Grid_CellMouseDown;
        _grid.KeyDown += Grid_KeyDown;
        _grid.DataError += (_, _) => { };
    }

    private ContextMenuStrip CreateGridContextMenu()
    {
        ContextMenuStrip menu = new()
        {
            Font = Theme.Font(9F),
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            ShowImageMargin = false
        };
        _toggleMenuItem.Click += async (_, _) => await ToggleSelectedItemAsync();
        _editMenuItem.Click += async (_, _) => await EditSelectedItemAsync();
        _propertiesMenuItem.Click += (_, _) => ShowSelectedItemDetails();
        _openRegistryMenuItem.Click += (_, _) => OpenRegistryForSelectedItem();
        _openFileMenuItem.Click += (_, _) => OpenFileLocationForSelectedItem();
        _deleteMenuItem.Click += async (_, _) => await DeleteSelectedItemAsync();
        _refreshMenuItem.Click += async (_, _) => await RefreshItemsAsync();
        menu.Items.AddRange(
        [
            _toggleMenuItem,
            _editMenuItem,
            _propertiesMenuItem,
            new ToolStripSeparator(),
            _openRegistryMenuItem,
            _openFileMenuItem,
            new ToolStripSeparator(),
            _deleteMenuItem,
            new ToolStripSeparator(),
            _refreshMenuItem
        ]);
        menu.Opening += GridContextMenu_Opening;
        return menu;
    }

    private void SelectNavigation(NavigationEntry entry)
    {
        _selectedNavigation = entry;
        _listPage.Visible = !entry.IsSettings;
        _settingsPage.Visible = entry.IsSettings;
        if (entry.IsSettings)
        {
            _settingsPage.BringToFront();
        }
        else
        {
            _listPage.BringToFront();
            ApplyFilters();
        }

        foreach ((ModernButton button, NavigationEntry navigation) in _navigationButtons)
        {
            button.ButtonStyle = navigation == entry ? ModernButtonStyle.Primary : ModernButtonStyle.Secondary;
        }
    }

    private async Task RefreshItemsAsync()
    {
        if (_busy || _closing)
        {
            return;
        }

        SetBusy(true, "正在扫描右键菜单…");
        try
        {
            IReadOnlyList<ContextMenuItem> items = await Task.Run(_scanner.Scan);
            if (_closing)
            {
                return;
            }

            ReplaceItems(items);
            _status.Text = $"扫描完成，共 {items.Count} 个菜单项";
        }
        catch (Exception) when (_closing)
        {
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> RunOperationAsync(Action operation, string successMessage, bool refreshAfter)
    {
        if (_busy || _closing)
        {
            return false;
        }

        SetBusy(true, "正在应用更改…");
        try
        {
            await Task.Run(operation);
            if (refreshAfter)
            {
                IReadOnlyList<ContextMenuItem> items = await Task.Run(_scanner.Scan);
                if (_closing)
                {
                    return false;
                }

                ReplaceItems(items);
            }

            _status.Text = successMessage;
            return true;
        }
        catch (Exception) when (_closing)
        {
            return false;
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceItems(IEnumerable<ContextMenuItem> items)
    {
        _allItems.Clear();
        _allItems.AddRange(items);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_selectedNavigation.IsSettings)
        {
            return;
        }

        List<ContextMenuItem> filtered = _allItems.Where(MatchesNavigation)
            .Where(item => !_enabledOnlyCheck.Checked || item.IsEnabled)
            .ToList();

        _visibleItems.RaiseListChangedEvents = false;
        _visibleItems.Clear();
        foreach (ContextMenuItem item in filtered)
        {
            _visibleItems.Add(item);
        }

        _visibleItems.RaiseListChangedEvents = true;
        _bindingSource.ResetBindings(false);
        _itemCount.Text = $"{filtered.Count} / {_allItems.Count} 项";
    }

    private bool MatchesNavigation(ContextMenuItem item) =>
        _selectedNavigation.Scope is not { } scope || item.Scope == scope;

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (!TryGetGridItem(e.RowIndex, e.ColumnIndex, out ContextMenuItem item))
        {
            return;
        }

        string columnName = _grid.Columns[e.ColumnIndex].Name;
        if (columnName == "Icon")
        {
            e.Value = _iconService.GetIcon(item);
            e.FormattingApplied = true;
        }
        else if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(ContextMenuItem.DisplayName))
        {
            string details = string.Join(
                Environment.NewLine,
                new[] { item.Description, item.ServicePath }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = details;
        }
        else if (columnName == "ToggleAction")
        {
            e.Value = item.IsReadOnly ? item.ReadOnlyActionText : item.IsEnabled ? "已启用" : "已禁用";
            _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = CanToggle(item)
                ? item.IsEnabled ? "单击关闭此菜单项" : "单击启用此菜单项"
                : item.OpenOnClick ? "单击打开管理文件夹" : "Explorer 系统内置项目，仅用于查看";
            e.FormattingApplied = true;
        }
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (!TryGetGridItem(e.RowIndex, e.ColumnIndex, out ContextMenuItem item)
            || _grid.Columns[e.ColumnIndex].Name != "ToggleAction")
        {
            return;
        }

        Graphics? graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, DataGridViewPaintParts.Background
            | DataGridViewPaintParts.Border
            | DataGridViewPaintParts.SelectionBackground);

        if (CanToggle(item))
        {
            const int switchWidth = 44;
            const int switchHeight = 24;
            Rectangle switchBounds = new(
                e.CellBounds.Left + (e.CellBounds.Width - switchWidth) / 2,
                e.CellBounds.Top + (e.CellBounds.Height - switchHeight) / 2,
                switchWidth,
                switchHeight);
            Color trackColor = item.IsEnabled ? Theme.Accent : Color.FromArgb(148, 156, 166);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (System.Drawing.Drawing2D.GraphicsPath path = Theme.RoundedRectangle(switchBounds, switchHeight / 2))
            using (SolidBrush track = new(trackColor))
            {
                graphics.FillPath(track, path);
            }

            int knobSize = switchHeight - 6;
            int knobX = item.IsEnabled
                ? switchBounds.Right - knobSize - 3
                : switchBounds.Left + 3;
            graphics.FillEllipse(
                Brushes.White,
                new Rectangle(knobX, switchBounds.Top + 3, knobSize, knobSize));
        }
        else
        {
            TextRenderer.DrawText(
                graphics,
                item.IsReadOnly ? item.ReadOnlyActionText : "已注册",
                Theme.Font(8.2F),
                e.CellBounds,
                Theme.DisabledText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        e.Handled = true;
    }

    private async void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (!TryGetGridItem(e.RowIndex, e.ColumnIndex, out ContextMenuItem item)
            || _grid.Columns[e.ColumnIndex].Name != "ToggleAction")
        {
            return;
        }

        _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (CanToggle(item))
        {
            await ToggleSelectedItemAsync();
        }
        else if (item.OpenOnClick)
        {
            OpenFileLocationForSelectedItem();
        }
    }

    private async void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0
            && e.ColumnIndex >= 0
            && _grid.Columns[e.ColumnIndex].Name != "ToggleAction"
            && SelectedItem is { OpenOnClick: true })
        {
            OpenFileLocationForSelectedItem();
        }
        else if (e.RowIndex >= 0 && SelectedItem is { Kind: ContextMenuKind.Shell })
        {
            await EditSelectedItemAsync();
        }
    }

    private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
        {
            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        }
    }

    private async void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            await RefreshItemsAsync();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            await DeleteSelectedItemAsync();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && SelectedItem?.Kind == ContextMenuKind.Shell)
        {
            await EditSelectedItemAsync();
            e.Handled = true;
        }
    }

    private void GridContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        ContextMenuItem? item = SelectedItem;
        _toggleMenuItem.Enabled = CanToggle(item);
        _toggleMenuItem.Text = item?.IsEnabled == true ? "禁用" : "启用";
        _editMenuItem.Enabled = item?.Kind == ContextMenuKind.Shell;
        _propertiesMenuItem.Enabled = item is not null;
        _openRegistryMenuItem.Enabled = IsRegistryBacked(item);
        _openFileMenuItem.Enabled = ResolveItemFileTarget(item) is not null;
        _deleteMenuItem.Enabled = item?.Kind == ContextMenuKind.Shell;
    }

    private async Task ToggleSelectedItemAsync()
    {
        if (SelectedItem is not { } item || !CanToggle(item))
        {
            return;
        }

        bool wasEnabled = item.IsEnabled;
        await RunOperationAsync(
            () =>
            {
                if (wasEnabled)
                {
                    _editService.Disable(item);
                }
                else
                {
                    _editService.Enable(item);
                }
            },
            $"已{(wasEnabled ? "禁用" : "启用")}: {item.DisplayName}",
            true);
    }

    private async Task EditSelectedItemAsync()
    {
        if (SelectedItem is not { Kind: ContextMenuKind.Shell } item)
        {
            return;
        }

        using EditMenuItemDialog dialog = new(item);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await RunOperationAsync(
            () => _editService.UpdateShellItem(item, dialog.DisplayNameValue, dialog.CommandValue, dialog.IconValue),
            $"已编辑: {dialog.DisplayNameValue}",
            true);
    }

    private async Task DeleteSelectedItemAsync()
    {
        if (SelectedItem is not { Kind: ContextMenuKind.Shell } item)
        {
            return;
        }

        DialogResult result = MessageBox.Show(
            this,
            $"确定永久删除 shell 菜单项“{item.DisplayName}”吗？\n\n此操作无法由本工具自动撤销。",
            "确认删除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
        {
            return;
        }

        await RunOperationAsync(
            () => _editService.DeleteShellItem(item),
            $"已删除: {item.DisplayName}",
            true);
    }

    private void ShowSelectedItemDetails()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        using DetailDialog dialog = new(item);
        dialog.ShowDialog(this);
    }

    private void OpenRegistryForSelectedItem()
    {
        if (SelectedItem is not { } item || !IsRegistryBacked(item))
        {
            return;
        }

        try
        {
            _editService.OpenRegistryPath(string.IsNullOrWhiteSpace(item.DisabledRegistryPath)
                ? item.RegistryPath
                : item.DisabledRegistryPath);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void OpenFileLocationForSelectedItem()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        string? target = ResolveItemFileTarget(item);
        if (target is null)
        {
            ShowError("无法从当前项中解析出有效的文件路径。");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new("explorer.exe")
            {
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(Directory.Exists(target) ? target : $"/select,{target}");
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async Task RestartExplorerAsync()
    {
        DialogResult result = MessageBox.Show(
            this,
            "将关闭并重新启动 explorer.exe。继续吗？",
            "重启 Explorer",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result == DialogResult.Yes)
        {
            await RunOperationAsync(_explorerService.Restart, "Explorer 已重启", false);
        }
    }

    private void LoadClassicMenuState()
    {
        _loadingClassicState = true;
        try
        {
            _classicMenuCheck.Checked = _classicService.IsEnabled();
        }
        finally
        {
            _loadingClassicState = false;
        }
    }

    private async Task ClassicMenuCheck_ChangedAsync()
    {
        if (_loadingClassicState)
        {
            return;
        }

        bool enabled = _classicMenuCheck.Checked;
        bool succeeded = await RunOperationAsync(
            () => _classicService.SetEnabled(enabled),
            "Win11 经典右键菜单设置已更新，需要重启 Explorer 后生效。",
            false);
        if (succeeded)
        {
            MessageBox.Show(
                this,
                "设置已更新。需要重启 Explorer 后生效。",
                "ContextMenuManager11",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            LoadClassicMenuState();
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        if (_closing || IsDisposed)
        {
            return;
        }

        _busy = busy;
        UseWaitCursor = busy;
        _grid.Enabled = !busy;
        _refreshButton.Enabled = !busy;
        _restartExplorerButton.Enabled = !busy;
        _enabledOnlyCheck.Enabled = !busy;
        _classicMenuCheck.Enabled = !busy;
        _progress.Visible = busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = message;
        }
    }

    private void ShowError(string message)
    {
        _status.Text = message;
        MessageBox.Show(this, message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private ContextMenuItem? SelectedItem => _grid.CurrentRow?.DataBoundItem as ContextMenuItem;

    private static bool CanToggle(ContextMenuItem? item) =>
        item is not null
        && !item.IsReadOnly
        && (item.Kind is ContextMenuKind.Shell or ContextMenuKind.ShellEx
            || item.Scope is ContextMenuScope.NewMenu or ContextMenuScope.SendTo or ContextMenuScope.OpenWith);

    private static bool IsRegistryBacked(ContextMenuItem? item) =>
        item?.Kind != ContextMenuKind.FileSystem
        && item?.RegistryPath.StartsWith("HK", StringComparison.OrdinalIgnoreCase) == true;

    private static string? ResolveItemFileTarget(ContextMenuItem? item)
    {
        if (item is null)
        {
            return null;
        }

        foreach (string candidate in new[] { item.ServicePath, item.Command, item.RegistryPath })
        {
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return ShellCommandParser.TryGetExecutablePath(item.Command);
    }

    private static ModernButton CreateButton(string text, ModernButtonStyle style, int width) => new()
    {
        Text = text,
        Width = width,
        Height = 32,
        ButtonStyle = style
    };

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string header,
        string property,
        int width,
        DataGridViewAutoSizeColumnMode autoSizeMode = DataGridViewAutoSizeColumnMode.None) =>
        new()
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            MinimumWidth = 60,
            AutoSizeMode = autoSizeMode,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

    private bool TryGetGridItem(int rowIndex, int columnIndex, out ContextMenuItem item)
    {
        item = null!;
        if (_closing
            || _grid.IsDisposed
            || rowIndex < 0
            || columnIndex < 0
            || rowIndex >= _grid.Rows.Count
            || columnIndex >= _grid.Columns.Count
            || rowIndex >= _bindingSource.Count)
        {
            return false;
        }

        if (_grid.Rows[rowIndex].DataBoundItem is not ContextMenuItem boundItem)
        {
            return false;
        }

        item = boundItem;
        return true;
    }

    private sealed record NavigationEntry(
        string Title,
        ContextMenuScope? Scope,
        bool IsSettings = false);
}
