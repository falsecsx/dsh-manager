using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

 [assembly: AssemblyVersion("1.0.0.0")]
 [assembly: AssemblyFileVersion("1.0.0.0")]
 [assembly: AssemblyInformationalVersion("1.0.0")]

namespace DeepSeekHarness
{
    internal sealed class ContentStack : TableLayoutPanel
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width > 1 ? proposedSize.Width : Math.Max(1, Width);
            int height = Padding.Vertical;
            foreach (Control child in Controls)
            {
                if (Visible && !child.Visible) continue;
                Size size = child.GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal - child.Margin.Horizontal), 0));
                height += (child.AutoSize ? size.Height : child.Height) + child.Margin.Vertical;
            }
            return new Size(width, height);
        }
    }

    internal sealed class PageHost : TabControl
    {
        public PageHost() { TabStop = false; }
        protected override void WndProc(ref Message m)
        {
            // TCM_ADJUSTRECT: pages use the whole viewport; navigation lives in the sidebar.
            if (m.Msg == 0x1328 && !DesignMode) { m.Result = IntPtr.Zero; return; }
            base.WndProc(ref m);
        }
    }

    internal sealed class UiCard : Panel
    {
        private readonly Color borderColor = Color.FromArgb(232, 236, 241);
        public UiCard()
        {
            BackColor = Color.White;
            BorderStyle = BorderStyle.None;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            // Panel's default calculation uses old child bounds and can keep stale empty space.
            if (Controls.Count != 1) return base.GetPreferredSize(proposedSize);
            int width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            Control content = Controls[0];
            Size preferred = content.GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0));
            return new Size(width, preferred.Height + Padding.Vertical);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 4));
            using (var shadowPath = DshManagerForm.RoundedRect(new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, bounds.Height), 8))
            using (var fillPath = DshManagerForm.RoundedRect(bounds, 8))
            using (var shadow = new SolidBrush(Color.FromArgb(14, 39, 57, 82)))
            using (var fill = new SolidBrush(Color.White))
            using (var border = new Pen(borderColor))
            {
                e.Graphics.FillPath(shadow, shadowPath);
                e.Graphics.FillPath(fill, fillPath);
                e.Graphics.DrawPath(border, fillPath);
            }
        }
    }

    internal sealed class NavButton : Button
    {
        public bool IsSelected { get; set; }
        private bool hover;
        public NavButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(IsSelected ? Color.FromArgb(237, 243, 254) : hover ? Color.FromArgb(245, 247, 250) : Color.White);
            if (IsSelected) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(65, 118, 230)), 0, 8, 3, Math.Max(1, Height - 16));
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(16, 0, Width - 20, Height), IsSelected ? Color.FromArgb(65, 118, 230) : Color.FromArgb(97, 102, 107), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 4, Width - 10, Height - 8), Color.FromArgb(65, 118, 230), Color.Transparent);
        }
    }

    internal sealed class BrandProgressBar : Control
    {
        private readonly System.Windows.Forms.Timer timer;
        private int offset;
        public ProgressBarStyle Style { get; set; }
        public BrandProgressBar()
        {
            Height = 4;
            Style = ProgressBarStyle.Marquee;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += (s, e) => { offset = (offset + 3) % Math.Max(1, Width + 54); Invalidate(); };
            VisibleChanged += (s, e) => { if (Visible) timer.Start(); else timer.Stop(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(232, 236, 241));
            int width = Style == ProgressBarStyle.Marquee ? Math.Min(54, Math.Max(22, Width / 6)) : Width;
            int x = Style == ProgressBarStyle.Marquee ? offset - width : 0;
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(65, 118, 230)), x, 0, width, Height);
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class BrandCheckBox : CheckBox
    {
        public BrandCheckBox()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            Size textSize = TextRenderer.MeasureText(Text ?? string.Empty, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            return new Size(textSize.Width + 24, Math.Max(22, textSize.Height + 4));
        }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            var box = new Rectangle(0, Math.Max(0, (Height - 16) / 2), 16, 16);
            Color border = Enabled ? (Checked ? Color.FromArgb(65, 118, 230) : Color.FromArgb(181, 189, 200)) : Color.FromArgb(206, 211, 218);
            using (var fill = new SolidBrush(Checked && Enabled ? Color.FromArgb(65, 118, 230) : Color.White))
            using (var pen = new Pen(border, 1.2F))
            {
                e.Graphics.FillRectangle(fill, box);
                e.Graphics.DrawRectangle(pen, box);
            }
            if (Checked) using (var pen = new Pen(Color.White, 2F)) e.Graphics.DrawLines(pen, new[] { new Point(3, box.Y + 8), new Point(7, box.Y + 12), new Point(13, box.Y + 4) });
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(22, 0, Math.Max(1, Width - 22), Height), Enabled ? ForeColor : Color.FromArgb(150, 156, 166), TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(20, 2, Math.Max(1, Width - 22), Math.Max(1, Height - 4)), ForeColor, BackColor);
        }
    }

public class DshManagerForm : Form
{
        private const string ManagerVersion = "1.0.0";
        // 品牌色（与 Web GUI --dsw-static-deepseek-500 一致）
        private static readonly Color BrandBlue = Color.FromArgb(65, 118, 230);
        private static readonly Color BrandBlueHover = Color.FromArgb(56, 108, 224);
        private static readonly Color BrandBlueDown = Color.FromArgb(43, 85, 192);
        private static readonly Color TextPrimary = Color.FromArgb(27, 27, 28);
        private static readonly Color TextSecondary = Color.FromArgb(97, 102, 107);
        private static readonly Color BorderNeutral = Color.FromArgb(207, 211, 214);
        private static readonly Color HoverBg = Color.FromArgb(237, 243, 254);
        private static readonly Color OkGreen = Color.FromArgb(34, 197, 94);
        private static readonly Color ErrRed = Color.FromArgb(236, 19, 19);
        private static readonly Color PageBackground = Color.FromArgb(246, 248, 251);
        private static readonly Color SidebarBackground = Color.FromArgb(255, 255, 255);
        private static readonly Color CardBorder = Color.FromArgb(226, 230, 237);
        private static readonly Color LogBackground = Color.FromArgb(25, 29, 36);

        // 控件
        private TextBox txtInstallDir;
        private Label lblDshHome;
        private Button btnBrowse, btnInstall, btnStart, btnStop, btnAppWindow, btnOpenBrowser, btnCopyUrl;
        private RichTextBox txtLog;
        private Label lblStatus;
        private LinkLabel lblUrl;
        private Label lblTokenBadge;
        private CheckBox chkAutoUpdate, chkAutoOpen, chkAppWindow, chkGptCompatibility, chkReasoningControl;
        private NumericUpDown nudPort;
        private TabControl tabControl;
        private BrandProgressBar progressBar;
        private ToolStripStatusLabel statusLabel, versionLabel;
        private TableLayoutPanel manageLayout;
        private Panel logBody;
        private Button btnLogToggle;
        private Label lblLogSummary, lblSidebarStatus;
        private bool logExpanded;

        // 插件页控件
        private TextBox txtPluginServer;
        private Button btnPluginRefresh, btnPluginDownloadZip, btnPluginCopyTopic;
        private ComboBox cmbPluginCategory;
        private Label lblPluginStatus;
        private ListView pluginList;
        private System.Windows.Forms.Timer pluginTimer;
        private System.Windows.Forms.Timer compatibilityTimer;
        private string compatibilitySignature;
        private bool compatibilityReconcileBusy;

        // 美化页控件
        private ListView beautyList;
        private ComboBox cmbBeautyStyle, cmbBeautyStatus;
        private Button btnBeautyRefresh, btnBeautyEnable, btnBeautyDisable, btnBeautyOpen;
        private Label lblBeautyStatus;
        private string beautyCountText;
        private PluginList lastPluginData;
        private PluginList beautifyData;
        private bool beautyLoading = false;
        private System.Collections.Concurrent.ConcurrentDictionary<string, BeautyEntry> beautyState = new System.Collections.Concurrent.ConcurrentDictionary<string, BeautyEntry>();
        private System.Threading.SemaphoreSlim beautyOpLock = new System.Threading.SemaphoreSlim(1, 1); // 启用/停用互斥
        private bool beautyBusy; // 启用/停用进行中：期间不覆盖状态文案
        private Label lblAboutVersion;
        private Label lblBeautyFooter;
        private List<ListViewItem> beautyItems = new List<ListViewItem>();
        private int beautySortCol = 0;
        private bool beautySortDesc = false;

        // 状态
        private Process dshProcess;
        private bool isRunning = false;
        private string installDir;
        private string dshHome;
        private string pluginServer = "";
        private string currentUrl = "http://127.0.0.1:3080";
        // 新版 dsh（0.1.1+）启动时会打印一行 "dsh web: http://127.0.0.1:PORT/?token=xxxx"，
        // 该 token 是本次进程的一次性启动令牌：浏览器带上它访问会自动换取登录 cookie，
        // 从而免去手动输入「配对码」。管理器捕获这行后，所有打开动作都使用带 token 的 URL。
        private string authenticatedUrl = null;  // 带 ?token= 的完整访问 URL（捕获到即非空）
        private bool tokenAcquired = false;      // 本轮是否已成功捕获令牌
        private int dshPort = 3080;              // 本轮 dsh 实际端口（供令牌等待逻辑使用）
        private Icon appIcon;
        private bool pluginLoading = false;
        private bool gptCompatFixEnabled;
        private bool reasoningControlEnabled;
        private bool suppressGptCompatibilityChange;
        private bool suppressReasoningControlChange;
        private bool dshSetupBusy;
        private bool installPromptShown;
        private bool installationFound;

        public DshManagerForm(bool launchMode)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "DeepSeek Harness 管理器 v" + ManagerVersion;
            this.Size = new Size(1140, 780);
            this.MinimumSize = new Size(980, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = true;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = PageBackground;
            appIcon = CreateAppIcon();
            this.Icon = appIcon;

            // 先加载保存的设置，没有则用默认路径
            LoadSettings();
            LoadBeautyState();
            ReconcileBeautyState(); // 与真实 profile 对账（启动时状态自愈）
            if (string.IsNullOrEmpty(installDir))
            {
                installDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "DeepSeek-Harness");
            }

            BuildModernUI();
            ResumeLayout(true);
            CheckInstallation();
            ReconcileGptCompatibility();
            ReconcileReasoningControl();
            this.Shown += (s, e) =>
            {
                if (launchMode) QuickLaunch();
                if (!installationFound && !launchMode) PromptForInstallDirectory();
                RefreshPluginsAsync(false);   // 后台预取插件列表（失败静默回退缓存）
                RefreshBeautifyAsync(false);  // 后台预取美化插件（失败静默回退缓存）
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (pluginTimer != null) pluginTimer.Stop();
            if (compatibilityTimer != null) compatibilityTimer.Stop();
            base.OnFormClosed(e);
            if (appIcon != null)
            {
                try { DestroyIcon(appIcon.Handle); } catch { }
                appIcon.Dispose();
                appIcon = null;
            }
        }

        // All content rows size to their controls. Only list viewports consume spare space.
        private static TableLayoutPanel Stack()
        {
            var table = new ContentStack { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1, RowCount = 0, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private static void AddRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(control, 0, row);
        }

        private static FlowLayoutPanel Flow(params Control[] controls)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
            flow.Controls.AddRange(controls);
            return flow;
        }

        private static Label BodyLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, ForeColor = TextSecondary, Margin = new Padding(0, 4, 0, 4), TextAlign = ContentAlignment.MiddleLeft };
        }

        private static Button ActionButton(string text, bool primary, EventHandler action)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(80, 34), Padding = new Padding(12, 5, 12, 5), Margin = new Padding(0, 0, 8, 6) };
            if (primary) StylePrimary(button); else StyleSecondary(button);
            button.Click += action;
            return button;
        }

        private static UiCard Card(Control content)
        {
            var card = new UiCard { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(18, 14, 18, 14), Margin = new Padding(0, 0, 0, 10) };
            card.Controls.Add(content);
            return card;
        }

        private static TableLayoutPanel Grid(params float[] widths)
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = widths.Length, RowCount = 1, Margin = Padding.Empty };
            foreach (float width in widths) grid.ColumnStyles.Add(new ColumnStyle(width == 0 ? SizeType.AutoSize : SizeType.Percent, width));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return grid;
        }

        private static void ScrollPage(TabPage page, TableLayoutPanel content)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            scroll.Controls.Add(content);
            page.Controls.Add(scroll);
        }

        private void BuildModernUI()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 206));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = SidebarBackground, Padding = new Padding(20, 24, 14, 20), Margin = Padding.Empty };
            sidebar.Paint += (s, e) => { using (var pen = new Pen(CardBorder)) e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height); };
            var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var top = Stack();
            var brand = Grid(0, 100);
            brand.Margin = new Padding(0, 0, 0, 24);
            brand.Controls.Add(new PictureBox { Image = appIcon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(34, 34), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 10, 0) }, 0, 0);
            var brandText = Stack();
            var brandName = BodyLabel("DSH Manager"); brandName.Font = new Font("Segoe UI Semibold", 12F); brandName.ForeColor = TextPrimary; brandName.Margin = Padding.Empty;
            AddRow(brandText, brandName);
            var brandSub = BodyLabel("DeepSeek Harness"); brandSub.Font = new Font("Segoe UI", 8F); brandSub.Margin = Padding.Empty; AddRow(brandText, brandSub);
            brand.Controls.Add(brandText, 1, 0); AddRow(top, brand);
            NavButton[] navButtons = new NavButton[4]; string[] names = { "管理", "插件", "美化", "关于" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                navButtons[i] = CreateNavButton(names[i]);
                navButtons[i].Click += (s, e) => tabControl.SelectedIndex = index;
                AddRow(top, navButtons[i]);
            }
            side.Controls.Add(top, 0, 0);
            lblSidebarStatus = BodyLabel("● 正在检查状态");
            side.Controls.Add(lblSidebarStatus, 0, 2);
            sidebar.Controls.Add(side); root.Controls.Add(sidebar, 0, 0);
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 28, 16), Margin = Padding.Empty };
            tabControl = new PageHost { Dock = DockStyle.Fill, Margin = Padding.Empty };
            tabControl.SelectedIndexChanged += (s, e) => { if (tabControl.SelectedIndex >= 0) UpdateNavSelection(navButtons, tabControl.SelectedIndex); };
            content.Controls.Add(tabControl); root.Controls.Add(content, 1, 0);

            var manageTab = new TabPage("管理") { BackColor = PageBackground, Padding = Padding.Empty };
            manageLayout = Stack();
            AddRow(manageLayout, CreatePageHeader("管理 DSH", "安装、启动和配置本地 DeepSeek Harness 服务"));

            var status = Stack();
            lblStatus = BodyLabel("状态：未安装"); lblStatus.Font = new Font("Microsoft YaHei UI", 14F); lblStatus.ForeColor = TextPrimary;
            AddRow(status, lblStatus);
            var details = Grid(35, 45, 20);
            var token = Stack(); AddRow(token, BodyLabel("访问状态")); lblTokenBadge = BodyLabel("● 待启动"); AddRow(token, lblTokenBadge); details.Controls.Add(token, 0, 0);
            var address = Stack(); AddRow(address, BodyLabel("访问地址"));
            lblUrl = new LinkLabel { Text = currentUrl, AutoSize = true, Dock = DockStyle.Fill, LinkColor = BrandBlue, Margin = new Padding(0, 4, 12, 4) };
            lblUrl.LinkClicked += (s, e) => { try { Process.Start(GetOpenUrl()); } catch { } }; AddRow(address, lblUrl); details.Controls.Add(address, 1, 0);
            var port = Stack(); AddRow(port, BodyLabel("端口"));
            nudPort = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = 3080, Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 2, 0, 2) };
            nudPort.ValueChanged += (s, e) => { dshPort = (int)nudPort.Value; currentUrl = "http://127.0.0.1:" + dshPort; if (lblUrl != null) lblUrl.Text = currentUrl; }; AddRow(port, nudPort); details.Controls.Add(port, 2, 0);
            AddRow(status, details); AddRow(manageLayout, Card(status));

            btnInstall = ActionButton("下载并安装 DSH", false, BtnInstall_Click);
            btnStart = ActionButton("启动", true, BtnStart_Click);
            btnStop = ActionButton("停止", false, BtnStop_Click);
            btnAppWindow = ActionButton("应用窗口", false, (s, e) => OpenAppWindow());
            btnOpenBrowser = ActionButton("浏览器打开", false, (s, e) => OpenBrowser());
            btnCopyUrl = ActionButton("复制链接", false, (s, e) => CopyAccessUrl());
            var actions = Stack(); AddRow(actions, Flow(btnInstall, btnStart, btnStop, btnAppWindow, btnOpenBrowser, btnCopyUrl));
            progressBar = new BrandProgressBar { Dock = DockStyle.Top, Height = 4, Visible = false, Margin = Padding.Empty };
            AddRow(actions, progressBar); AddRow(manageLayout, Card(actions));

            var settings = Stack(); AddRow(settings, CreateCardTitle("安装与启动设置", ""));
            var path = Grid(0, 100, 0);
            var pathLabel = BodyLabel("安装目录"); pathLabel.Margin = new Padding(0, 0, 12, 0); path.Controls.Add(pathLabel, 0, 0);
            txtInstallDir = new TextBox { Text = installDir, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 5, 12, 5) }; path.Controls.Add(txtInstallDir, 1, 0);
            btnBrowse = ActionButton("选择已有安装/配置", false, (s, e) => SelectInstallDirectory());
            btnBrowse.Margin = new Padding(0, 0, 0, 6); path.Controls.Add(btnBrowse, 2, 0); AddRow(settings, path);
            lblDshHome = BodyLabel("配置目录: " + (string.IsNullOrEmpty(dshHome) ? "自动检测中" : dshHome));
            lblDshHome.ForeColor = TextSecondary; lblDshHome.AutoEllipsis = true; lblDshHome.Margin = new Padding(0, 0, 0, 6); AddRow(settings, lblDshHome);
            chkAutoUpdate = NewCheckBox("启动时自动更新"); chkAutoOpen = NewCheckBox("启动后自动打开界面"); chkAppWindow = NewCheckBox("使用应用窗口");
            AddRow(settings, Flow(chkAutoUpdate, chkAutoOpen, chkAppWindow)); AddRow(manageLayout, Card(settings));

            var compat = Stack(); AddRow(compat, CreateCardTitle("模型兼容设置", ""));
            chkGptCompatibility = NewCheckBox("启用 GPT 工具调用兼容修复"); chkGptCompatibility.Checked = gptCompatFixEnabled; chkGptCompatibility.CheckedChanged += ChkGptCompatibility_CheckedChanged;
            chkReasoningControl = NewCheckBox("启用模型思考强度调节"); chkReasoningControl.Checked = reasoningControlEnabled; chkReasoningControl.CheckedChanged += ChkReasoningControl_CheckedChanged;
            AddRow(compat, CompatibilityRow(chkGptCompatibility, "修复 GPT Responses 工具调用兼容性"));
            AddRow(compat, CompatibilityRow(chkReasoningControl, "为所有 openai-responses 模型提供 Low / Medium / High / Xhigh / Max"));
            AddRow(manageLayout, Card(compat));

            var log = Stack(); var logHeader = Grid(100, 0);
            btnLogToggle = ActionButton("展开运行日志", false, (s, e) => ToggleLog());
            lblLogSummary = BodyLabel("安装、更新和服务输出"); lblLogSummary.Dock = DockStyle.None; lblLogSummary.Margin = new Padding(4, 8, 0, 6);
            var logTitle = Flow(btnLogToggle, lblLogSummary); logTitle.WrapContents = false;
            logHeader.Controls.Add(logTitle, 0, 0);
            var copy = ActionButton("复制", false, (s, e) => { try { if (!string.IsNullOrEmpty(txtLog.Text)) Clipboard.SetText(txtLog.Text); } catch { } });
            var clear = ActionButton("清空", false, (s, e) => txtLog.Clear());
            copy.MinimumSize = clear.MinimumSize = new Size(56, 34); copy.Margin = new Padding(0, 0, 0, 6);
            var logActions = Flow(clear, copy); logActions.WrapContents = false;
            logHeader.Controls.Add(logActions, 1, 0); AddRow(log, logHeader);
            txtLog = new RichTextBox { ReadOnly = true, BackColor = LogBackground, ForeColor = Color.FromArgb(188, 239, 196), Font = new Font("Consolas", 9F), WordWrap = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
            logBody = new Panel { Dock = DockStyle.Top, Height = 180, Visible = false, BackColor = LogBackground, Padding = new Padding(10), Margin = new Padding(0, 6, 0, 0) }; logBody.Controls.Add(txtLog);
            AddRow(log, logBody); AddRow(manageLayout, Card(log)); ScrollPage(manageTab, manageLayout);
            tabControl.Controls.Add(manageTab);
            BuildPluginPage(tabControl); BuildBeautyPage(tabControl); BuildAboutPage(tabControl);
            ConfigurePluginListStyle(); ConfigureBeautyListStyle(); UpdateNavSelection(navButtons, 0);
            pluginTimer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
            pluginTimer.Tick += (s, e) => RefreshPluginsAsync(false); pluginTimer.Start();
            compatibilityTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            compatibilityTimer.Tick += (s, e) => AutoReconcileCompatibility();
            compatibilityTimer.Start();
            tabControl.SelectedIndexChanged += (s, e) => {
                if (tabControl.SelectedIndex == 1 && !pluginLoading && pluginList.Items.Count == 0) RefreshPluginsAsync(false);
                if (tabControl.SelectedIndex == 2 && !beautyLoading && beautifyData == null) RefreshBeautifyAsync(false);
            };
            Controls.Add(root);
            var strip = new StatusStrip { BackColor = SidebarBackground, SizingGrip = false };
            statusLabel = new ToolStripStatusLabel("就绪") { ForeColor = TextSecondary };
            versionLabel = new ToolStripStatusLabel("") { Spring = true, TextAlign = ContentAlignment.MiddleRight, ForeColor = TextSecondary };
            strip.Items.Add(statusLabel); strip.Items.Add(versionLabel); Controls.Add(strip);
        }

        private static BrandCheckBox NewCheckBox(string text)
        {
            return new BrandCheckBox { Text = text, AutoSize = true, ForeColor = TextPrimary, Margin = new Padding(0, 4, 20, 6) };
        }

        private static TableLayoutPanel CompatibilityRow(CheckBox check, string description)
        {
            var grid = Grid(42, 58);
            grid.Controls.Add(check, 0, 0);
            grid.Controls.Add(BodyLabel(description), 1, 0);
            return grid;
        }

        private static ComboBox FilterBox()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 126, Margin = new Padding(0, 4, 16, 6), FlatStyle = FlatStyle.Flat };
        }

        private static FlowLayoutPanel Filter(string title, ComboBox box)
        {
            var label = BodyLabel(title); label.Dock = DockStyle.None; label.Margin = new Padding(0, 7, 8, 6);
            var group = Flow(label, box); group.Dock = DockStyle.None; group.WrapContents = false;
            return group;
        }

        private static TableLayoutPanel ListPage(TabPage page, TableLayoutPanel header, ListView list, Control footer)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(header, 0, 0); table.Controls.Add(list, 0, 1); table.Controls.Add(footer, 0, 2); page.Controls.Add(table);
            return table;
        }

        private void BuildPluginPage(TabControl host)
        {
            var tab = new TabPage("插件") { BackColor = PageBackground };
            var header = Stack(); AddRow(header, CreatePageHeader("插件中心", "浏览、筛选并安装 DeepSeek Harness 社区插件"));
            var server = Stack(); AddRow(server, CreateCardTitle("插件服务器（可选）", ""));
            txtPluginServer = new TextBox { Text = pluginServer, Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 4) };
            txtPluginServer.Leave += (s, e) => { pluginServer = txtPluginServer.Text.Trim(); SaveSettings(); }; AddRow(server, txtPluginServer);
            AddRow(server, BodyLabel("留空直连 GitHub；配置代理地址后，无 VPN 也可刷新和下载。")); AddRow(header, Card(server));
            btnPluginRefresh = ActionButton("刷新列表", true, (s, e) => RefreshPluginsAsync(true));
            btnPluginDownloadZip = ActionButton("下载 ZIP", false, BtnPluginDownload_Click); btnPluginDownloadZip.Enabled = false;
            btnPluginCopyTopic = ActionButton("复制 Topic", false, (s, e) => { try { Clipboard.SetText("https://github.com/topics/dsh-plugin"); lblPluginStatus.Text = "Topic 链接已复制"; } catch { } });
            cmbPluginCategory = FilterBox(); cmbPluginCategory.SelectedIndexChanged += (s, e) => ApplyCategoryFilter();
            AddRow(header, Flow(btnPluginRefresh, btnPluginDownloadZip, btnPluginCopyTopic, Filter("分类", cmbPluginCategory)));
            lblPluginStatus = BodyLabel("尚未获取插件列表"); AddRow(header, lblPluginStatus);
            pluginList = NewList(); pluginList.Columns.Add("名称", 220); pluginList.Columns.Add("分类", 105); pluginList.Columns.Add("简介", 330); pluginList.Columns.Add("★", 50); pluginList.Columns.Add("最近更新", 110);
            pluginList.DoubleClick += (s, e) => OpenSelectedRepository(pluginList);
            pluginList.SelectedIndexChanged += (s, e) => btnPluginDownloadZip.Enabled = pluginList.SelectedItems.Count > 0;
            pluginList.ColumnClick += (s, e) => SortPluginsByColumn(e.Column);
            ListPage(tab, header, pluginList, BodyLabel("数据源：GitHub · 双击插件打开仓库 · 点击表头排序")); host.Controls.Add(tab);
        }

        private static ListView NewList()
        {
            return new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None, BackColor = Color.White, UseCompatibleStateImageBehavior = false, Margin = new Padding(0, 8, 0, 8), ShowItemToolTips = true };
        }

        private void OpenSelectedRepository(ListView list)
        {
            if (list.SelectedItems.Count == 0) return;
            PluginInfo plugin = list.SelectedItems[0].Tag as PluginInfo;
            if (plugin != null) { try { Process.Start(plugin.HtmlUrl); } catch { } }
        }

        private void ConfigurePluginListStyle() { ConfigureListView(pluginList); }
        private void ConfigureBeautyListStyle() { ConfigureListView(beautyList); }

        private void BuildBeautyPage(TabControl host)
        {
            var tab = new TabPage("美化") { BackColor = PageBackground };
            var header = Stack(); AddRow(header, CreatePageHeader("美化中心", "管理 DSH 前端主题和皮肤插件"));
            AddRow(header, Card(BodyLabel("启用后会自动下载并写入 DSH profile；停用后重启 DSH 服务生效。")));
            btnBeautyRefresh = ActionButton("刷新列表", true, (s, e) => RefreshBeautifyAsync(true));
            btnBeautyEnable = ActionButton("启用", false, BtnBeautyEnable_Click); btnBeautyEnable.Enabled = false;
            btnBeautyDisable = ActionButton("停用", false, BtnBeautyDisable_Click); btnBeautyDisable.Enabled = false;
            btnBeautyOpen = ActionButton("打开仓库", false, (s, e) => OpenSelectedRepository(beautyList)); btnBeautyOpen.Enabled = false;
            cmbBeautyStyle = FilterBox(); cmbBeautyStyle.SelectedIndexChanged += (s, e) => ApplyBeautyFilter();
            cmbBeautyStatus = FilterBox(); cmbBeautyStatus.Items.AddRange(new object[] { "全部", "已下载", "未下载" }); cmbBeautyStatus.SelectedIndex = 1; cmbBeautyStatus.SelectedIndexChanged += (s, e) => ApplyBeautyFilter();
            AddRow(header, Flow(btnBeautyRefresh, btnBeautyEnable, btnBeautyDisable, btnBeautyOpen));
            AddRow(header, Flow(Filter("风格", cmbBeautyStyle), Filter("状态", cmbBeautyStatus)));
            lblBeautyStatus = BodyLabel("尚未获取插件列表"); AddRow(header, lblBeautyStatus);
            beautyList = NewList(); beautyList.Columns.Add("名称", 210); beautyList.Columns.Add("风格", 100); beautyList.Columns.Add("简介", 290); beautyList.Columns.Add("★", 50); beautyList.Columns.Add("最近更新", 100); beautyList.Columns.Add("状态", 90);
            beautyList.DoubleClick += (s, e) => OpenSelectedRepository(beautyList);
            beautyList.SelectedIndexChanged += (s, e) => RefreshBeautyButtons();
            beautyList.ColumnClick += (s, e) => SortBeautifyByColumn(e.Column);
            lblBeautyFooter = BodyLabel("启用中：0 · 已下载：0 · 数据源：本地"); ListPage(tab, header, beautyList, lblBeautyFooter); host.Controls.Add(tab);
        }

        private void BuildAboutPage(TabControl host)
        {
            var tab = new TabPage("关于") { BackColor = PageBackground };
            var content = Stack(); AddRow(content, CreatePageHeader("关于 DSH Manager", "DeepSeek Harness 本地管理工具"));
            var intro = Grid(0, 100);
            intro.Controls.Add(new PictureBox { Image = appIcon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(72, 72), Padding = new Padding(16), BackColor = HoverBg, Margin = new Padding(0, 0, 20, 0), Anchor = AnchorStyles.Left }, 0, 0);
            var introText = Stack(); var name = BodyLabel("DeepSeek Harness Manager"); name.Font = new Font("Segoe UI Semibold", 17F); name.ForeColor = TextPrimary; AddRow(introText, name);
            lblAboutVersion = BodyLabel(""); UpdateAboutVersion(); AddRow(introText, lblAboutVersion);
            AddRow(introText, BodyLabel("面向本地 DSH 的安装、运行、插件与配置管理")); intro.Controls.Add(introText, 1, 0); AddRow(content, Card(intro));
            var features = Stack();
            var first = Grid(50, 50); first.Controls.Add(CreateAboutFeature("运行管理", "安装、更新、启动、停止和访问 DSH 服务"), 0, 0); first.Controls.Add(CreateAboutFeature("扩展中心", "浏览、筛选和下载社区插件与美化主题"), 1, 0); AddRow(features, first);
            var second = Grid(50, 50); second.Controls.Add(CreateAboutFeature("本地配置", "管理目录、端口、启动方式和自动更新"), 0, 0); second.Controls.Add(CreateAboutFeature("GPT 兼容", "按需启用工具调用与思考强度配置"), 1, 0); AddRow(features, second); AddRow(content, Card(features));
            var project = Stack(); AddRow(project, BodyLabel("DSH Manager v" + ManagerVersion + " · 基于 @deepseek-ai/dsh"));
            var ownLink = new LinkLabel { Text = "打开我的 DSH Manager 开源仓库 ↗", AutoSize = true, Dock = DockStyle.Top, LinkColor = BrandBlue, Margin = new Padding(0, 8, 0, 4) };
            ownLink.LinkClicked += (s, e) => { try { Process.Start("https://github.com/falsecsx/dsh-manager"); } catch { } };
            AddRow(project, ownLink);
            var upstreamLink = new LinkLabel { Text = "查看 DeepSeek Harness 上游项目 ↗", AutoSize = true, Dock = DockStyle.Top, LinkColor = TextSecondary, Margin = new Padding(0, 4, 0, 8) };
            upstreamLink.LinkClicked += (s, e) => { try { Process.Start("https://github.com/deepseek-ai/deepseek-harness"); } catch { } };
            AddRow(project, upstreamLink); AddRow(content, Card(project)); ScrollPage(tab, content); host.Controls.Add(tab);
        }

        private static Panel CreateAboutFeature(string title, string description)
        {
            var panel = Stack(); panel.Margin = new Padding(0, 4, 18, 16); AddRow(panel, CreateCardTitle(title, "")); AddRow(panel, BodyLabel(description)); return panel;
        }

        private static Panel CreateCardTitle(string title, string subtitle)
        {
            var panel = Stack(); var label = BodyLabel(title); label.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); label.ForeColor = TextPrimary; label.Margin = new Padding(0, 0, 0, 12); AddRow(panel, label); return panel;
        }

        private static Panel CreatePageHeader(string title, string subtitle)
        {
            var panel = Stack(); panel.Margin = new Padding(0, 0, 0, 18);
            var label = BodyLabel(title); label.Font = new Font("Microsoft YaHei UI", 18F); label.ForeColor = TextPrimary; label.Margin = Padding.Empty; AddRow(panel, label); AddRow(panel, BodyLabel(subtitle)); return panel;
        }

        private static NavButton CreateNavButton(string text)
        {
            return new NavButton { Text = text, Dock = DockStyle.Top, Height = 42, ForeColor = TextSecondary, Margin = new Padding(0, 0, 0, 6) };
        }

        private static void UpdateNavSelection(NavButton[] buttons, int selected)
        {
            for (int i = 0; i < buttons.Length; i++) { buttons[i].IsSelected = i == selected; buttons[i].Invalidate(); }
        }

        private void ToggleLog()
        {
            logExpanded = !logExpanded;
            logBody.Visible = logExpanded;
            btnLogToggle.Text = logExpanded ? "收起运行日志" : "展开运行日志";
        }


        private static void ConfigureListView(ListView list)
        {
            list.OwnerDraw = true;
            list.GridLines = false;
            list.HideSelection = false;
            list.DrawColumnHeader += StyledList_DrawColumnHeader;
            list.DrawItem += (s, e) => { };
            list.DrawSubItem += StyledList_DrawSubItem;
            list.Paint += StyledList_EmptyPaint;
            list.Resize += (s, e) => FitListColumns(list);
            list.FontChanged += (s, e) => FitListColumns(list);
            FitListColumns(list);
        }

        private static void FitListColumns(ListView list)
        {
            if (list.Columns.Count < 5) return;
            float scale = list.Font.SizeInPoints / 9F;
            int[] widths = { 190, 90, 0, 64, 100, 86 };
            int fixedWidth = 0;
            for (int i = 0; i < list.Columns.Count; i++)
            {
                if (i == 2) continue;
                list.Columns[i].Width = (int)Math.Ceiling(widths[i] * scale);
                fixedWidth += list.Columns[i].Width;
            }
            list.Columns[2].Width = Math.Max((int)(120 * scale), list.ClientSize.Width - fixedWidth - SystemInformation.VerticalScrollBarWidth - 2);
        }

        private static void StyledList_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(237, 243, 254))) e.Graphics.FillRectangle(bg, e.Bounds);
            using (var pen = new Pen(Color.FromArgb(224, 230, 239))) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            using (var font = new Font(((ListView)sender).Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, e.Header.Text, font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), TextPrimary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private static void StyledList_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            var list = (ListView)sender;
            bool selected = e.Item.Selected;
            Color bg = selected ? Color.FromArgb(232, 239, 255) : e.ItemIndex % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);
            using (var brush = new SolidBrush(bg)) e.Graphics.FillRectangle(brush, e.Bounds);
            Color text = selected ? Color.FromArgb(38, 89, 184) : e.SubItem.ForeColor.IsEmpty ? TextPrimary : e.SubItem.ForeColor;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 12), e.Bounds.Height), text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var line = new Pen(Color.FromArgb(239, 242, 246))) e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        private static void StyledList_EmptyPaint(object sender, PaintEventArgs e)
        {
            var list = (ListView)sender;
            if (list.Items.Count != 0) return;
            string message = "暂无内容，刷新后将显示可用项目";
            Size size = TextRenderer.MeasureText(message, list.Font);
            TextRenderer.DrawText(e.Graphics, message, list.Font, new Point(Math.Max(12, (list.ClientSize.Width - size.Width) / 2), Math.Max(36, (list.ClientSize.Height - size.Height) / 2)), TextSecondary);
        }

        // ── 按钮样式 ──
        private static void StylePrimary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = BrandBlue;
            b.FlatAppearance.MouseOverBackColor = BrandBlueHover;
            b.FlatAppearance.MouseDownBackColor = BrandBlueDown;
            b.BackColor = BrandBlue;
            b.ForeColor = Color.White;
        }

        private static void StyleSecondary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = BorderNeutral;
            b.FlatAppearance.MouseOverBackColor = HoverBg;
            b.BackColor = Color.White;
            b.ForeColor = TextPrimary;
        }

        internal static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Icon CreateAppIcon()
        {
            try
            {
                string exe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    Icon embedded = Icon.ExtractAssociatedIcon(exe);
                    if (embedded != null) return embedded;
                }
            }
            catch { }
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath path = RoundedRect(new Rectangle(2, 2, 28, 28), 8))
                    using (SolidBrush blue = new SolidBrush(BrandBlue))
                        g.FillPath(blue, path);
                    using (SolidBrush white = new SolidBrush(Color.White))
                    using (Font f = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        string s = "DSH";
                        SizeF sz = g.MeasureString(s, f);
                        g.DrawString(s, f, white, (32 - sz.Width) / 2, (32 - sz.Height) / 2 - 1);
                    }
                }
                IntPtr hicon = bmp.GetHicon();
                return Icon.FromHandle(hicon);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void Log(string text)
        {
            if (IsDisposed || txtLog == null || txtLog.IsDisposed) return;
            if (txtLog.InvokeRequired)
            {
                try { txtLog.Invoke(new Action<string>(Log), text); } catch { }
                return;
            }
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + "\n";
            txtLog.AppendText(line);
            txtLog.ScrollToCaret();
        }

        private void CheckInstallation()
        {
            string detectedHome = FindDshHome();
            if (!string.IsNullOrEmpty(detectedHome) && !string.Equals(dshHome, detectedHome, StringComparison.OrdinalIgnoreCase))
            {
                dshHome = detectedHome;
                if (lblDshHome != null) lblDshHome.Text = "配置目录: " + dshHome;
                SaveSettings();
                Log("[检测] 已找到 DSH 配置目录: " + detectedHome);
            }
            string discovered = FindDshInstallDirectory(installDir);
            bool installed = !string.IsNullOrEmpty(discovered);
            if (installed && !string.Equals(Path.GetFullPath(installDir ?? ""), Path.GetFullPath(discovered), StringComparison.OrdinalIgnoreCase))
            {
                installDir = discovered;
                if (txtInstallDir != null) txtInstallDir.Text = discovered;
                SaveSettings();
                Log("[检测] 已找到 DeepSeek Harness: " + discovered);
            }
            installationFound = installed;

            // 检查端口是否已在运行（区分 DSH 服务与其他程序占用，避免误报/误杀）
            int port = nudPort != null ? (int)nudPort.Value : 3080;
            bool alreadyRunning = IsPortInUse(port);
            bool dshRunning = false;
            if (alreadyRunning)
            {
                foreach (int pid in GetPidsListeningOn(port))
                {
                    string cmd = GetProcessCommandLine(pid);
                    if (cmd != null && cmd.IndexOf("bin.js", StringComparison.OrdinalIgnoreCase) >= 0)
                    { dshRunning = true; break; }
                }
            }

            if (installed && dshRunning)
            {
                isRunning = true;
                btnStart.Text = "已启动";
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnOpenBrowser.Enabled = true;
                btnAppWindow.Enabled = true;
                btnCopyUrl.Enabled = false;
                string ver = GetLocalVersion();
                lblStatus.Text = "状态: 运行中" + (string.IsNullOrEmpty(ver) ? "" : "  (v" + ver + ")");
                lblStatus.ForeColor = OkGreen;
                btnInstall.Text = "检测到已安装";
                btnInstall.Enabled = false;
                SetStatusBar("运行中" + (string.IsNullOrEmpty(ver) ? "" : "  v" + ver));
                SetTokenBadge("🔒 外部实例", Color.FromArgb(230, 150, 40));
            }
            else if (installed && alreadyRunning)
            {
                isRunning = false;
                btnStart.Text = "启动";
                btnStart.Enabled = false; // 端口被占，启动会失败
                btnStop.Enabled = false;  // 不是 DSH 服务，禁止停止（防误杀）
                btnOpenBrowser.Enabled = true;
                btnAppWindow.Enabled = true;
                btnCopyUrl.Enabled = false;
                string ver = GetLocalVersion();
                lblStatus.Text = "状态: 端口 " + port + " 被其他程序占用（非 DSH 服务）";
                lblStatus.ForeColor = Color.FromArgb(230, 150, 40);
                btnInstall.Text = "检测到已安装";
                btnInstall.Enabled = false;
                SetStatusBar("端口被其他程序占用");
                SetTokenBadge("🔒 待启动", Color.Gray);
            }
            else if (installed)
            {
                isRunning = false;
                btnStart.Text = "启动";
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                btnOpenBrowser.Enabled = false;
                btnAppWindow.Enabled = false;
                btnCopyUrl.Enabled = false;
                string ver = GetLocalVersion();
                lblStatus.Text = "状态: 就绪" + (string.IsNullOrEmpty(ver) ? "" : "  (v" + ver + ")");
                lblStatus.ForeColor = OkGreen;
                btnInstall.Text = "检测到已安装";
                btnInstall.Enabled = false;
                SetStatusBar("就绪" + (string.IsNullOrEmpty(ver) ? "" : "  v" + ver));
                SetTokenBadge("🔒 待启动", Color.Gray);
            }
            else
            {
                isRunning = false;
                btnStart.Text = "启动";
                btnStart.Enabled = false;
                btnStop.Enabled = false;
                btnOpenBrowser.Enabled = false;
                btnAppWindow.Enabled = false;
                btnCopyUrl.Enabled = false;
                lblStatus.Text = "状态: 未安装";
                lblStatus.ForeColor = Color.Gray;
                btnInstall.Text = "下载并安装 DSH";
                btnInstall.Enabled = true;
                SetStatusBar("未安装");
                SetTokenBadge("🔒 待启动", Color.Gray);
            }
        }

        private static bool IsDshInstallDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            try
            {
                string package = Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh");
                return Directory.Exists(package) && File.Exists(Path.Combine(package, "package.json")) && File.Exists(Path.Combine(package, "lib", "bin.js"));
            }
            catch { return false; }
        }

        private static string FindDshInstallBelow(string root, int depth)
        {
            if (string.IsNullOrWhiteSpace(root) || depth < 0) return null;
            try
            {
                string full = Path.GetFullPath(root);
                if (IsDshInstallDirectory(full)) return full;
                if (IsDshPackageDirectory(full))
                {
                    DirectoryInfo package = new DirectoryInfo(full);
                    if (package.Parent != null && package.Parent.Parent != null && package.Parent.Parent.Parent != null)
                        return package.Parent.Parent.Parent.FullName;
                }
                if (depth == 0 || !Directory.Exists(full)) return null;
                foreach (string child in Directory.GetDirectories(full))
                {
                    string found = FindDshInstallBelow(child, depth - 1);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }
            catch { }
            return null;
        }

        private static bool IsDshPackageDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            try
            {
                string manifest = Path.Combine(dir, "package.json");
                return File.Exists(manifest) && File.Exists(Path.Combine(dir, "lib", "bin.js")) &&
                    File.ReadAllText(manifest).IndexOf("@deepseek-ai/dsh", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static bool IsDshHomeDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            try { return File.Exists(Path.Combine(dir, "settings.yaml")); }
            catch { return false; }
        }

        private string FindDshHome()
        {
            var candidates = new List<string>();
            Action<string> add = p => { if (!string.IsNullOrWhiteSpace(p)) candidates.Add(p); };
            add(dshHome);
            add(Environment.GetEnvironmentVariable("DSH_HOME"));
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            add(Path.Combine(user, ".dsh"));
            add(Path.Combine(local, "DeepSeek-Harness"));
            add(Path.Combine(roaming, "DeepSeek-Harness"));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(candidate);
                    if (seen.Add(full) && IsDshHomeDirectory(full)) return full;
                }
                catch { }
            }
            return null;
        }

        private string FindDshInstallDirectory(string preferred)
        {
            var candidates = new List<string>();
            Action<string> add = p => { if (!string.IsNullOrWhiteSpace(p)) candidates.Add(p); };
            add(preferred);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            add(Path.Combine(user, "DeepSeek-Harness"));
            add(Path.Combine(user, "DeepSeek Harness"));
            add(Path.Combine(user, ".dsh"));
            add(Path.Combine(local, "DeepSeek-Harness"));
            add(Path.Combine(local, "Programs", "DeepSeek-Harness"));
            add(Path.Combine(roaming, "DeepSeek-Harness"));
            add(Path.Combine(roaming, "npm"));
            add(Path.Combine(roaming, "npm", "node_modules"));
            add(Path.Combine(local, "npm"));
            add(Path.Combine(local, "npm", "node_modules"));
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            add(Environment.CurrentDirectory);
            string dshHome = Environment.GetEnvironmentVariable("DSH_HOME");
            add(dshHome);
            if (!string.IsNullOrWhiteSpace(dshHome)) add(Path.Combine(dshHome, "profiles", "web"));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(candidate);
                    if (!seen.Add(full)) continue;
                    string found = FindDshInstallBelow(full, 4);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
                catch { }
            }
            return null;
        }

        private void SelectInstallDirectory()
        {
            if (dshSetupBusy) return;
            using (var dlg = new FolderBrowserDialog { Description = "选择 DeepSeek Harness 安装目录或配置目录（.dsh）", SelectedPath = installDir ?? dshHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ShowNewFolderButton = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string selected = dlg.SelectedPath;
                string selectedInstall = FindDshInstallBelow(selected, 4);
                if (!string.IsNullOrEmpty(selectedInstall))
                {
                    installDir = selectedInstall;
                    installationFound = true;
                    if (txtInstallDir != null) txtInstallDir.Text = installDir;
                    Log("[设置] 已选择 DSH 安装目录: " + installDir);
                }
                else if (IsDshHomeDirectory(selected))
                {
                    dshHome = Path.GetFullPath(selected);
                    if (lblDshHome != null) lblDshHome.Text = "配置目录: " + dshHome;
                    Log("[设置] 已选择 DSH 配置目录: " + dshHome);
                }
                else
                {
                    var result = MessageBox.Show(this, "该目录中未检测到 DeepSeek Harness 或 settings.yaml。是否仍将它设为下载和安装目录？", "确认安装目录", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (result != DialogResult.Yes) return;
                    installDir = Path.GetFullPath(selected);
                    installationFound = false;
                    if (txtInstallDir != null) txtInstallDir.Text = installDir;
                }
                SaveSettings();
                CheckInstallation();
                ReconcileGptCompatibility();
                ReconcileReasoningControl();
            }
        }

        private void PromptForInstallDirectory()
        {
            if (installPromptShown || installationFound || IsDisposed) return;
            installPromptShown = true;
            Log("[检测] 未找到 DeepSeek Harness。请选择已有安装目录，或使用“下载并安装 DSH”。");
            var result = MessageBox.Show(this, "没有自动找到 DeepSeek Harness。现在选择已有安装目录吗？\n\n也可以稍后点击“下载并安装 DSH”自动下载。", "未找到 DSH", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes) SelectInstallDirectory();
        }

        private void ChkGptCompatibility_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressGptCompatibilityChange || chkGptCompatibility == null) return;
            bool desired = chkGptCompatibility.Checked;
            string runningError;
            if (dshSetupBusy || DshBlocksCompatibility(out runningError))
            {
                suppressGptCompatibilityChange = true;
                chkGptCompatibility.Checked = gptCompatFixEnabled;
                suppressGptCompatibilityChange = false;
                MessageBox.Show("请先停止 DSH，再切换 GPT 工具调用兼容修复。", "无法切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool previous = gptCompatFixEnabled;
            bool ok = RunGptCompatibility(desired ? "on" : "off");
            if (!ok)
            {
                suppressGptCompatibilityChange = true;
                chkGptCompatibility.Checked = gptCompatFixEnabled;
                suppressGptCompatibilityChange = false;
                return;
            }
            gptCompatFixEnabled = desired;
            if (!SaveSettings())
            {
                if (RunGptCompatibility(previous ? "on" : "off"))
                {
                    gptCompatFixEnabled = previous;
                    suppressGptCompatibilityChange = true;
                    chkGptCompatibility.Checked = previous;
                    suppressGptCompatibilityChange = false;
                }
                Log("[GPT 兼容] 设置未能持久保存，请排除配置目录写入问题后重试。");
            }
        }

        private void ReconcileGptCompatibility()
        {
            if (!gptCompatFixEnabled && !File.Exists(GetGptCompatStatePath())) return;
            string runningError;
            if (dshSetupBusy || DshBlocksCompatibility(out runningError))
            {
                if (gptCompatFixEnabled) Log("[GPT 兼容] DSH 正在运行，将在停止后检查兼容配置。");
                return;
            }
            string state = GetGptCompatStatePath();
            if (gptCompatFixEnabled || File.Exists(state))
                RunGptCompatibility(gptCompatFixEnabled ? "on" : "off");
        }

        private void ChkReasoningControl_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressReasoningControlChange || chkReasoningControl == null) return;
            bool desired = chkReasoningControl.Checked;
            string runningError;
            if (dshSetupBusy || DshBlocksCompatibility(out runningError))
            {
                suppressReasoningControlChange = true;
                chkReasoningControl.Checked = reasoningControlEnabled;
                suppressReasoningControlChange = false;
                MessageBox.Show("请先停止 DSH，再切换模型思考强度调节。", "无法切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool previous = reasoningControlEnabled;
            if (!RunGptCompatibility(desired ? "reasoning-on" : "reasoning-off"))
            {
                suppressReasoningControlChange = true;
                chkReasoningControl.Checked = previous;
                suppressReasoningControlChange = false;
                return;
            }
            reasoningControlEnabled = desired;
            if (!SaveSettings())
            {
                RunGptCompatibility(previous ? "reasoning-on" : "reasoning-off");
                reasoningControlEnabled = previous;
                suppressReasoningControlChange = true;
                chkReasoningControl.Checked = previous;
                suppressReasoningControlChange = false;
            }
        }

        private void ReconcileReasoningControl()
        {
            if (!reasoningControlEnabled && !File.Exists(GetGptCompatStatePath())) return;
            string runningError;
            if (dshSetupBusy || DshBlocksCompatibility(out runningError))
            {
                if (reasoningControlEnabled) Log("[思考强度] DSH 正在运行，将在停止后检查配置。");
                return;
            }
            RunGptCompatibility(reasoningControlEnabled ? "reasoning-on" : "reasoning-off");
            compatibilitySignature = GetCompatibilitySignature();
        }

        private string GetCompatibilitySignature()
        {
            try
            {
                string target = GetDshSettingsYamlPath();
                if (!File.Exists(target)) return "missing";
                FileInfo info = new FileInfo(target);
                return info.Length.ToString() + ":" + info.LastWriteTimeUtc.Ticks.ToString();
            }
            catch { return "error"; }
        }

        private void AutoReconcileCompatibility()
        {
            if (compatibilityReconcileBusy || dshSetupBusy || (!gptCompatFixEnabled && !reasoningControlEnabled)) return;
            string signature = GetCompatibilitySignature();
            if (compatibilitySignature == null)
            {
                compatibilitySignature = signature;
                return;
            }
            if (signature == compatibilitySignature) return;
            compatibilitySignature = signature;
            compatibilityReconcileBusy = true;
            try
            {
                if (reasoningControlEnabled) RunGptCompatibility("reasoning-on", true);
                if (gptCompatFixEnabled) RunGptCompatibility("on", true);
                compatibilitySignature = GetCompatibilitySignature();
                Log("[兼容设置] 检测到模型配置变化，已自动刷新思考强度和工具调用设置。DSH 会自动热加载新配置。");
            }
            finally { compatibilityReconcileBusy = false; }
        }

        private bool EnsureReasoningControl()
        {
            string target = GetDshSettingsYamlPath();
            if (!reasoningControlEnabled && !File.Exists(GetGptCompatStatePath())) return true;
            if (reasoningControlEnabled && !File.Exists(target)) { Log("[思考强度] 未找到 DSH 配置: " + target); return false; }
            return RunGptCompatibility(reasoningControlEnabled ? "reasoning-on" : "reasoning-off");
        }

        private bool EnsureGptCompatibility()
        {
            string target = GetDshSettingsYamlPath();
            if (!gptCompatFixEnabled && !File.Exists(GetGptCompatStatePath())) return true;
            if (gptCompatFixEnabled && !File.Exists(target))
            {
                Log("[GPT 兼容] 未找到 DSH 配置: " + target);
                return false;
            }
            return RunGptCompatibility(gptCompatFixEnabled ? "on" : "off");
        }

        private string GetDshSettingsYamlPath()
        {
            return Path.GetFullPath(Path.Combine(GetDshHome(), "settings.yaml"));
        }

        private string GetGptCompatStateRoot()
        {
            return Path.Combine(Path.GetDirectoryName(SettingsPath), "gpt-compat");
        }

        private string GetGptCompatStatePath()
        {
            return Path.Combine(GetGptCompatStateRoot(), "state.json");
        }

        private bool RunGptCompatibility(string mode, bool allowRunning = false)
        {
            string featureLabel = mode.StartsWith("reasoning", StringComparison.OrdinalIgnoreCase) ? "思考强度" : "GPT 兼容";
            string runningError;
            if (mode != "status" && !allowRunning && DshBlocksCompatibility(out runningError))
            {
                Log("[" + featureLabel + "] " + runningError);
                return false;
            }
            string target = GetDshSettingsYamlPath();
            string node = FindNode();
            if (string.IsNullOrEmpty(node))
            {
                Log("[" + featureLabel + "] 未找到 node.exe，无法修改配置。");
                return false;
            }
            string resource = "DeepSeekHarness.GptCompat.cjs";
            string scriptPath = Path.Combine(Path.GetTempPath(), "dsh-gpt-compat-" + Guid.NewGuid().ToString("N") + ".cjs");
            string report = Path.Combine(Path.GetTempPath(), "dsh-gpt-compat-report-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                using (Stream input = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (input == null) { Log("[" + featureLabel + "] 内置兼容脚本缺失。"); return false; }
                    using (FileStream output = File.Create(scriptPath)) input.CopyTo(output);
                }
                Directory.CreateDirectory(GetGptCompatStateRoot());
                int rc;
                using (var process = new Process())
                {
                    process.StartInfo.FileName = node;
                    process.StartInfo.Arguments = string.Join(" ", new[] { scriptPath, mode, target, GetGptCompatStateRoot(), installDir ?? "", report }.Select(QuoteProcessArgument));
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    if (!process.WaitForExit(45000))
                    {
                        process.Kill();
                        process.WaitForExit();
                        Log("[" + featureLabel + "] 操作超时，请查看备份状态后重试。");
                        return false;
                    }
                    rc = process.ExitCode;
                }
                if (!File.Exists(report)) { Log("[" + featureLabel + "] 未收到操作结果（退出码 " + rc + "）。"); return false; }
                GptCompatibilityResult result;
                using (var input = File.OpenRead(report))
                    result = (GptCompatibilityResult)new DataContractJsonSerializer(typeof(GptCompatibilityResult)).ReadObject(input);
                Log("[" + featureLabel + "] " + result.Message);
                return rc == 0 && result.Ok;
            }
            catch (Exception ex)
            {
                Log("[" + featureLabel + "] 操作失败: " + ex.Message);
                return false;
            }
            finally
            {
                try { File.Delete(scriptPath); } catch { }
                try { File.Delete(report); } catch { }
            }
        }

        [DataContract]
        public class GptCompatibilityResult
        {
            [DataMember(Name = "ok")] public bool Ok;
            [DataMember(Name = "message")] public string Message;
        }

        private static string QuoteProcessArgument(string value)
        {
            // Windows CommandLineToArgvW escaping, including trailing backslashes.
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }

        private bool DshBlocksCompatibility(out string reason)
        {
            reason = "请先停止 DSH，再切换 GPT 工具调用兼容修复。";
            try
            {
                if (dshProcess != null && !dshProcess.HasExited) return true;
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name = 'node.exe'"))
                using (var processes = searcher.Get())
                    foreach (System.Management.ManagementObject process in processes)
                    {
                        string cmd = process["CommandLine"] as string;
                        if (cmd != null && Regex.IsMatch(cmd, @"[\\/]@deepseek-ai[\\/]dsh[\\/]lib[\\/]bin\.js.*\bweb\b", RegexOptions.IgnoreCase)) return true;
                    }
                return false;
            }
            catch (Exception ex)
            {
                reason = "无法确认 DSH 是否已停止，配置未修改：" + ex.Message;
                return true;
            }
        }

        private void SetStatusBar(string text)
        {
            if (statusLabel != null) statusLabel.Text = text;
            if (lblSidebarStatus != null)
            {
                lblSidebarStatus.Text = "●  " + text;
                lblSidebarStatus.ForeColor = text.IndexOf("运行", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("就绪", StringComparison.OrdinalIgnoreCase) >= 0 ? OkGreen : text.IndexOf("错误", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("占用", StringComparison.OrdinalIgnoreCase) >= 0 ? ErrRed : TextSecondary;
            }
        }

        // ── 安装 ──
        private async void BtnInstall_Click(object sender, EventArgs e)
        {
            if (dshSetupBusy) return;
            dshSetupBusy = true;
            btnInstall.Enabled = false;
            btnInstall.Text = "正在下载并安装...";
            progressBar.Visible = true;
            txtLog.Clear();

            await Task.Run(() => RunInstall());
            dshSetupBusy = false;
            if (IsDisposed) return;
            progressBar.Visible = false;
            CheckInstallation();
            UpdateAboutVersion();
            SaveSettings();
            ReconcileGptCompatibility();
            ReconcileReasoningControl();
        }

        private void RunInstall()
        {
            try
            {
                Log("[1/5] 检查 Node.js...");
                string nodePath = FindNode();
                if (nodePath == null)
                {
                    Log("[!] 未找到 Node.js，正在安装...");
                    if (!InstallNodeJS())
                    {
                        Log("[失败] Node.js 安装失败。请手动下载安装: https://nodejs.org/en/download/");
                        return;
                    }
                    nodePath = FindNode();
                    if (nodePath == null) { Log("[失败] 安装后无法找到 Node.js。"); return; }
                }
                Log("[OK] Node.js: " + GetNodeVersion(nodePath));
                Log("[OK] npm: " + GetNpmVersion(nodePath));

                Log("[2/5] 创建安装目录...");
                if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);
                Log("[OK] 目录: " + installDir);

                Log("[3/5] 写入 package.json...");
                // 依赖使用 "latest" 标签：安装即拉取最新版，避免写死旧版本号
                string pkg = @"
{
  ""name"": ""deepseek-harness"",
  ""version"": ""1.0.0"",
  ""private"": true,
  ""description"": ""DeepSeek Harness - Local AI Agent Web UI"",
  ""scripts"": {
    ""start"": ""dsh web"",
    ""web"": ""dsh web"",
    ""update"": ""npm install @deepseek-ai/dsh@latest""
  },
  ""dependencies"": {
    ""@deepseek-ai/dsh"": ""latest""
  },
  ""overrides"": {
    ""koffi"": ""3.1.5""
  }
}
";
                File.WriteAllText(Path.Combine(installDir, "package.json"), pkg.Trim(), Encoding.ASCII);
                Log("[OK] package.json 已创建。");

                Log("[4/5] 正在从 npm 下载 DeepSeek Harness（可能需要 2-5 分钟）...");
                int npmResult = RunNpmInstall(FindNode(), installDir);
                if (npmResult != 0)
                {
                    Log("[失败] npm 安装失败，请检查网络连接。");
                    return;
                }
                Log("[OK] DeepSeek Harness 安装完成！");

                Log("[5/5] 创建桌面快捷方式...");
                CreateShortcut();
                Log("[OK] 安装完成！");
            }
            catch (Exception ex)
            {
                Log("[失败] " + ex.Message);
            }
        }

        // ── 启动 ──
        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (isRunning || dshSetupBusy) return;
            dshSetupBusy = true;
            btnStart.Enabled = false;
            btnStop.Enabled = true;

            await Task.Run(() => RunStart());
            dshSetupBusy = false;
            CheckInstallation();
        }

        private void RunStart()
        {
            try
            {
                int port = (int)nudPort.Value;
                dshPort = port;
                currentUrl = "http://127.0.0.1:" + port;
                // 本轮启动重置令牌状态（令牌是进程级一次性的，重启即换）
                authenticatedUrl = null;
                tokenAcquired = false;
                SetTokenBadge("🔒 启动中…", Color.Gray);

                // 先检查端口是否已被占用（区分 DSH 与其他程序）
                if (IsPortInUse(port))
                {
                    bool isDsh = false;
                    foreach (int pid in GetPidsListeningOn(port))
                    {
                        string cmd = GetProcessCommandLine(pid);
                        if (cmd != null && cmd.IndexOf("bin.js", StringComparison.OrdinalIgnoreCase) >= 0)
                        { isDsh = true; break; }
                    }
                    if (isDsh)
                    {
                        Log("[!] 端口 " + port + " 已被占用，DSH 可能已在运行（非本管理器启动）。");
                        Log("[提示] 该实例的访问令牌由启动它的终端打印（形如 http://127.0.0.1:" + port + "/?token=xxxx）。");
                        Log("[提示] 若界面要求配对码，请用「停止」后再由本管理器「启动」，即可自动免配对码；或在原终端复制带 token 的链接。");
                        SetStatus("运行中(外部)", OkGreen);
                        SetTokenBadge("🔒 外部实例", Color.FromArgb(230, 150, 40));
                        isRunning = true;
                        if (chkAutoOpen.Checked) AutoOpenView();
                        Invoke(new Action(() => { btnStart.Text = "已启动"; btnStart.Enabled = false; btnStop.Enabled = true; btnOpenBrowser.Enabled = true; btnAppWindow.Enabled = true; btnCopyUrl.Enabled = false; }));
                        return;
                    }
                    Log("[!] 端口 " + port + " 已被其他程序占用，请更换端口或停止该程序。");
                    SetStatus("端口被其他程序占用", ErrRed);
                    Invoke(new Action(() => { btnStart.Text = "启动"; btnStart.Enabled = false; btnStop.Enabled = false; btnOpenBrowser.Enabled = false; btnAppWindow.Enabled = false; }));
                    return;
                }

                if (chkAutoUpdate.Checked)
                {
                    Log("[DeepSeek Harness] 正在检查更新...");
                    string nodePath = FindNode();
                    if (nodePath != null)
                    {
                        try
                        {
                            string latest = RunNpmView(nodePath, installDir);
                            if (!string.IsNullOrEmpty(latest))
                            {
                                string local = GetLocalVersion();
                                if (!string.IsNullOrEmpty(local) && CompareVersions(latest, local) > 0)
                                {
                                    Log("[DeepSeek Harness] 发现新版本: " + local + " -> " + latest);
                                    Log("[DeepSeek Harness] 正在下载新版（大包可能需要几分钟，下方 [npm] 日志实时显示进度，请耐心等待）...");
                                    int ec = RunNpmInstall(nodePath, installDir);
                                    string after = GetLocalVersion();
                                    if (ec == 0 && !string.IsNullOrEmpty(after) && CompareVersions(after, local) > 0)
                                        Log("[OK] 已更新到 " + after + "！");
                                    else if (ec == 0)
                                        Log("[!] 更新命令完成但版本未变化（当前 " + (after ?? "未知") + "）。若仍如此，请手动执行: npm install @deepseek-ai/dsh@latest --save");
                                    else
                                        Log("[失败] 更新失败。");
                                }
                                else Log("[DeepSeek Harness] 已是最新版本。");
                            }
                        }
                        catch (Exception ex) { Log("[DeepSeek Harness] 更新检查失败: " + ex.Message); }
                    }
                }

                // 直接使用 node.exe 运行 dsh，并实时捕获其输出
                string nodePathDsh = FindNode();
                if (nodePathDsh == null)
                {
                    Log("[失败] 找不到 Node.js。");
                    return;
                }

                string dshScript = Path.Combine(installDir, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
                if (!File.Exists(dshScript))
                {
                    Log("[失败] 找不到 dsh 启动脚本: " + dshScript);
                    return;
                }

                if (!EnsureGptCompatibility()) return;
                if (!EnsureReasoningControl()) return;
                Log("[DeepSeek Harness] 正在启动 Web UI，端口: " + port);
                dshProcess = new Process();
                dshProcess.StartInfo.FileName = nodePathDsh;
                dshProcess.StartInfo.Arguments = "\"" + dshScript + "\" web --port " + port + " --no-open";
                dshProcess.StartInfo.WorkingDirectory = installDir;
                dshProcess.StartInfo.UseShellExecute = false;
                dshProcess.StartInfo.CreateNoWindow = true;
                dshProcess.StartInfo.RedirectStandardOutput = true;
                dshProcess.StartInfo.RedirectStandardError = true;
                dshProcess.EnableRaisingEvents = true;
                dshProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) HandleDshOutput(e.Data); };
                dshProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) HandleDshOutput(e.Data); };
                dshProcess.Exited += (s, ev) => {
                    Log("[DeepSeek Harness] 进程已退出。");
                    bool stillRunning = IsPortInUse(port);
                    if (!stillRunning)
                    {
                        isRunning = false;
                        SetStatus("已停止", Color.Gray);
                        SetTokenBadge("🔒 待启动", Color.Gray);
                        Log("[DeepSeek Harness] 服务器已停止。");
                        Invoke(new Action(() => { btnStart.Text = "启动"; btnStart.Enabled = true; btnStop.Enabled = false; btnOpenBrowser.Enabled = false; btnAppWindow.Enabled = false; btnCopyUrl.Enabled = false; if (lblUrl != null) lblUrl.Text = "地址: http://127.0.0.1:" + port; }));
                    }
                    else
                    {
                        Log("[DeepSeek Harness] 服务器仍在运行（子进程）。");
                    }
                };

                dshProcess.Start();
                dshProcess.BeginOutputReadLine();
                dshProcess.BeginErrorReadLine();

                // 等待服务器启动：端口监听 + 捕获访问令牌（新版 dsh 启动时打印带 token 的 URL）
                Log("[DeepSeek Harness] 等待服务器启动...");
                int waited = 0;
                bool portUp = false;
                int portUpAt = -1;
                while (waited < 25)
                {
                    Thread.Sleep(1000);
                    waited++;
                    if (!portUp && IsPortInUse(port))
                    {
                        portUp = true;
                        portUpAt = waited;
                        Log("[DeepSeek Harness] 服务器端口已就绪。");
                    }
                    if (portUp)
                    {
                        // 拿到令牌立即继续；端口就绪后最多再等 8 秒（兼容旧版 dsh 无令牌机制，不长等）
                        if (tokenAcquired) break;
                        if (waited - portUpAt >= 8) break;
                    }
                    if (dshProcess.HasExited)
                    {
                        Log("[失败] 进程已退出，服务器启动失败。");
                        SetStatus("错误", ErrRed);
                        SetTokenBadge("🔒 未就绪", ErrRed);
                        Invoke(new Action(() => { btnStart.Text = "启动"; btnStart.Enabled = true; btnStop.Enabled = false; btnOpenBrowser.Enabled = false; btnAppWindow.Enabled = false; btnCopyUrl.Enabled = false; }));
                        return;
                    }
                }

                if (!portUp)
                {
                    Log("[!] 服务器启动超时，但进程仍在运行。");
                }

                isRunning = true;
                SetStatus("运行中", OkGreen);
                SetStatusBar("运行中");
                if (tokenAcquired)
                {
                    Log("[OK] 已自动获取访问令牌，打开界面无需再输入配对码。");
                    SetTokenBadge("🔓 免配对码", OkGreen);
                }
                else
                {
                    // 旧版 dsh 无令牌机制（本来就不需要配对码）；新版若未捕获到则给出手动指引
                    Log("[!] 未捕获到访问令牌。若界面提示输入配对码，请直接复制本界面「地址」（或点击下方日志中 dsh 打印的 http://127.0.0.1:" + port + "/?token=... 链接）在浏览器打开。");
                    SetTokenBadge("🔒 见日志", Color.FromArgb(230, 150, 40));
                }
                Invoke(new Action(() => { btnStart.Text = "已启动"; btnStart.Enabled = false; btnStop.Enabled = true; btnOpenBrowser.Enabled = true; btnAppWindow.Enabled = true; btnCopyUrl.Enabled = tokenAcquired; }));

                if (chkAutoOpen.Checked)
                {
                    // 自动打开前短暂兜底等待令牌（主循环已等过令牌窗口，这里最多 3 秒）
                    for (int i = 0; i < 3 && !tokenAcquired && !dshProcess.HasExited; i++) Thread.Sleep(1000);
                    AutoOpenView();
                }

                // 保持线程存活，直到进程退出（或端口不再响应）
                dshProcess.WaitForExit();
                Thread.Sleep(3000);
                if (!IsPortInUse(port))
                {
                    isRunning = false;
                    SetStatus("已停止", Color.Gray);
                    SetStatusBar("已停止");
                    SetTokenBadge("🔒 待启动", Color.Gray);
                    Log("[DeepSeek Harness] 服务器已停止。");
                    Invoke(new Action(() => { btnStart.Text = "启动"; btnStart.Enabled = true; btnStop.Enabled = false; btnOpenBrowser.Enabled = false; btnAppWindow.Enabled = false; btnCopyUrl.Enabled = false; }));
                }
                else
                {
                    Log("[DeepSeek Harness] 端口仍在监听，服务器可能由子进程托管。");
                }
            }
            catch (Exception ex)
            {
                Log("[失败] " + ex.Message);
                isRunning = false;
                SetStatus("错误", ErrRed);
                SetStatusBar("错误");
                SetTokenBadge("🔒 错误", ErrRed);
                Invoke(new Action(() => { btnStart.Text = "启动"; btnStart.Enabled = true; btnStop.Enabled = false; btnOpenBrowser.Enabled = false; btnAppWindow.Enabled = false; btnCopyUrl.Enabled = false; }));
            }
        }

        /// <summary>
        /// 处理 dsh 的一行 stdout/stderr 输出：
        /// 1) 从中捕获新版 dsh 打印的带令牌访问 URL（"dsh web: http://127.0.0.1:PORT/?token=xxxx"）；
        /// 2) 写入日志面板时对 token 参数打码，避免令牌长期留在日志/截图里泄露。
        /// </summary>
        private void HandleDshOutput(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // 捕获带 token 的 URL（兼容 dsh web: / Local: / 直接打印 URL 等多种形式，含 LAN 地址）
            var m = Regex.Match(line, @"https?://[^\s]*[?&]token=[A-Za-z0-9_\-\.]+", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string url = m.Value.Trim().TrimEnd(')', ']', '"', '\'');
                // 优先采用回环地址那条（管理器本机打开用 127.0.0.1）；LAN 地址仅在没有回环时兜底
                bool isLoopback = url.Contains("127.0.0.1") || url.Contains("localhost") || url.Contains("[::1]");
                if (!tokenAcquired || isLoopback)
                {
                    authenticatedUrl = url;
                    tokenAcquired = true;
                    currentUrl = Regex.Replace(url, @"[?&]token=[A-Za-z0-9_\-\.]+", ""); // 记录干净地址用于显示
                    try
                    {
                        Invoke(new Action(() =>
                        {
                            if (lblUrl != null) lblUrl.Text = "地址: " + currentUrl + "  ✓已带令牌";
                            if (lblTokenBadge != null) { lblTokenBadge.Text = "🔓 免配对码"; lblTokenBadge.ForeColor = OkGreen; }
                            if (btnCopyUrl != null) btnCopyUrl.Enabled = true;
                        }));
                    }
                    catch { }
                }
            }

            // 日志打码：隐藏 token 值，只保留提示，避免令牌泄露到日志面板/截图
            string safe = Regex.Replace(line, @"([?&]token=)[A-Za-z0-9_\-\.]+", "$1****（已隐藏）", RegexOptions.IgnoreCase);
            Log("[dsh] " + safe);
        }

        /// <summary>更新令牌状态徽标（线程安全）。</summary>
        private void SetTokenBadge(string text, Color color)
        {
            if (lblTokenBadge == null || lblTokenBadge.IsDisposed) return;
            try
            {
                if (lblTokenBadge.InvokeRequired) lblTokenBadge.Invoke(new Action<string, Color>(SetTokenBadge), text, color);
                else { lblTokenBadge.Text = "● " + text.Replace("🔒", "").Replace("🔓", "").TrimStart('●', ' '); lblTokenBadge.ForeColor = color; }
            }
            catch { }
        }

        /// <summary>获取用于打开界面的 URL：优先带令牌（免配对码），否则回退干净地址。</summary>
        private string GetOpenUrl()
        {
            return !string.IsNullOrEmpty(authenticatedUrl) ? authenticatedUrl : currentUrl;
        }

        /// <summary>复制访问链接（带令牌）到剪贴板，方便在其他浏览器/设备打开。</summary>
        private void CopyAccessUrl()
        {
            try
            {
                string url = GetOpenUrl();
                Clipboard.SetText(url);
                Log("[OK] 已复制访问链接到剪贴板（含免配对码令牌，请勿外传）。");
            }
            catch (Exception ex) { Log("[失败] 复制链接失败: " + ex.Message); }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            int port = (int)nudPort.Value;

            // 1) 终止自己启动的 dsh 进程树
            if (dshProcess != null && !dshProcess.HasExited)
            {
                try
                {
                    Process p = StartProc("taskkill", "/PID " + dshProcess.Id + " /T /F");
                    p.WaitForExit(5000);
                }
                catch
                {
                    try { dshProcess.Kill(); dshProcess.WaitForExit(3000); } catch { }
                }
            }

            // 2) 端口残留：仅当命令行确认是 DSH（含 bin.js）才结束，绝不误杀无关进程
            try
            {
                foreach (int pid in GetPidsListeningOn(port))
                {
                    if (dshProcess != null && pid == dshProcess.Id) continue;
                    string cmd = GetProcessCommandLine(pid);
                    if (cmd != null && cmd.IndexOf("bin.js", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try { Process p = StartProc("taskkill", "/PID " + pid + " /T /F"); p.WaitForExit(3000); } catch { }
                    }
                    else Log("[!] 端口 " + port + " 的进程 PID " + pid + " 不是 DSH，已跳过（不结束）");
                }
            }
            catch { }

            dshProcess = null;
            isRunning = false;
            authenticatedUrl = null;
            tokenAcquired = false;
            currentUrl = "http://127.0.0.1:" + port;
            SetStatus("已停止", Color.Gray);
            SetStatusBar("已停止");
            SetTokenBadge("🔒 待启动", Color.Gray);
            if (lblUrl != null) lblUrl.Text = "地址: " + currentUrl;
            Log("[DeepSeek Harness] 服务器已停止。");
            btnStart.Text = "启动";
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnOpenBrowser.Enabled = false;
            btnAppWindow.Enabled = false;
            btnCopyUrl.Enabled = false;
        }

        /// <summary>通过 netstat 找到监听指定端口的进程 PID（不依赖 WMI）。</summary>
        private List<int> GetPidsListeningOn(int port)
        {
            var pids = new List<int>();
            string output = StartProcCapture("netstat", "-ano -p tcp", null, 15000);
            if (output == null) return pids;
            string pattern = @"TCP\s+(\[[^\]]+\]|[^:]+):" + port + @"\s+\S+\s+LISTENING\s+(\d+)";
            foreach (string line in output.Split('\n'))
            {
                Match m = Regex.Match(line, pattern);
                if (m.Success)
                {
                    int pid;
                    if (int.TryParse(m.Groups[2].Value, out pid) && pid > 0 && !pids.Contains(pid)) pids.Add(pid);
                }
            }
            return pids;
        }

        private void OpenBrowser()
        {
            string url = GetOpenUrl();
            try { Process.Start(url); Log("[OK] 已在默认浏览器打开界面。"); }
            catch (Exception ex) { Log("[失败] 打开浏览器失败: " + ex.Message); }
        }

        /// <summary>按用户偏好打开界面：应用窗口（Edge/Chrome --app 模式）或默认浏览器。</summary>
        private void AutoOpenView()
        {
            if (chkAppWindow.Checked) OpenAppWindow();
            else OpenBrowser();
        }

        /// <summary>查找 Edge 可执行文件（标准安装路径 + PATH）。</summary>
        private static string FindEdge()
        {
            string[] candidates = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Microsoft\\Edge\\Application\\msedge.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Microsoft\\Edge\\Application\\msedge.exe"
            };
            foreach (string c in candidates) if (File.Exists(c)) return c;
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string p in pathEnv.Split(';'))
            {
                string entry = p.Trim();
                if (entry.Length == 0) continue;
                string f = entry + "\\msedge.exe";
                if (File.Exists(f)) return f;
            }
            return null;
        }

        /// <summary>查找 Chrome 可执行文件（标准安装路径）。</summary>
        private static string FindChrome()
        {
            string[] candidates = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Google\\Chrome\\Application\\chrome.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Google\\Chrome\\Application\\chrome.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Google\\Chrome\\Application\\chrome.exe"
            };
            foreach (string c in candidates) if (File.Exists(c)) return c;
            return null;
        }

        /// <summary>
        /// 以「应用程序窗口」方式打开界面：使用 Edge/Chrome 的 --app 模式，
        /// 得到一个没有标签页、没有地址栏的独立窗口（观感类似桌面应用）。
        /// 找不到 Edge/Chrome 时回退到默认浏览器。
        /// </summary>
        private void OpenAppWindow()
        {
            string url = GetOpenUrl();
            try
            {
                string edge = FindEdge();
                if (edge != null)
                {
                    // --app 模式 + --new-window，避免与已有 Edge 实例合并窗口导致应用窗口失效
                    Process.Start(edge, "--app=\"" + url + "\" --start-maximized --new-window");
                    Log("[应用窗口] 已用 Edge 打开应用窗口（" + (tokenAcquired ? "已带免配对码令牌" : "未带令牌") + "）。");
                    return;
                }
                string chrome = FindChrome();
                if (chrome != null)
                {
                    Process.Start(chrome, "--app=\"" + url + "\" --start-maximized --new-window");
                    Log("[应用窗口] 已用 Chrome 打开应用窗口（" + (tokenAcquired ? "已带免配对码令牌" : "未带令牌") + "）。");
                    return;
                }
                Log("[!] 未找到 Edge/Chrome，改用默认浏览器打开。");
                OpenBrowser();
            }
            catch (Exception ex)
            {
                Log("[失败] 打开应用窗口失败: " + ex.Message);
                try { Process.Start(url); } catch { }
            }
        }

        private void QuickLaunch()
        {
            // --launch 模式：跳转到管理页并自动启动（或打开界面）
            tabControl.SelectedIndex = 0;
            if (isRunning)
            {
                AutoOpenView();
            }
            else if (btnStart.Enabled)
            {
                Log("[--launch] 自动启动 DeepSeek Harness...");
                btnStart.PerformClick();
            }
            else
            {
                Log("[--launch] 未检测到安装，请先在「安装」页完成安装。");
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action<string, Color>(SetStatus), text, color);
                return;
            }
            lblStatus.Text = "状态: " + text;
            lblStatus.ForeColor = color;
        }

        // ═══════════════════ 插件页 ═══════════════════

        [DataContract]
        public class PluginInfo
        {
            [DataMember(Name = "fullName")] public string FullName { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "description")] public string Description { get; set; }
            [DataMember(Name = "htmlUrl")] public string HtmlUrl { get; set; }
            [DataMember(Name = "stars")] public int Stars { get; set; }
            [DataMember(Name = "updatedAt")] public string UpdatedAt { get; set; }
            [DataMember(Name = "topics")] public List<string> Topics { get; set; }
            [DataMember(Name = "category")] public string Category { get; set; }
        }

        [DataContract]
        public class PluginList
        {
            [DataMember(Name = "fetchedAt")] public string FetchedAt { get; set; }
            [DataMember(Name = "total")] public int Total { get; set; }
            [DataMember(Name = "source")] public string Source { get; set; }
            [DataMember(Name = "plugins")] public List<PluginInfo> Plugins { get; set; }
        }

        [DataContract]
        public class GhRepo
        {
            [DataMember(Name = "full_name")] public string FullName { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "description")] public string Description { get; set; }
            [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
            [DataMember(Name = "stargazers_count")] public int Stars { get; set; }
            [DataMember(Name = "updated_at")] public string UpdatedAt { get; set; }
            [DataMember(Name = "topics")] public List<string> Topics { get; set; }
        }

        [DataContract]
        public class GhSearch
        {
            [DataMember(Name = "total_count")] public int Total { get; set; }
            [DataMember(Name = "items")] public List<GhRepo> Items { get; set; }
        }

        /// <summary>美化插件本地状态（beautify.json 条目）。</summary>
        [DataContract]
        public class BeautyEntry
        {
            [DataMember(Name = "repo")] public string Repo { get; set; }
            [DataMember(Name = "pkgName")] public string PkgName { get; set; }
            [DataMember(Name = "localDir")] public string LocalDir { get; set; }
            [DataMember(Name = "status")] public string Status { get; set; } // downloaded | enabled
        }

        [DataContract]
        public class BeautyState
        {
            [DataMember(Name = "plugins")] public List<BeautyEntry> Plugins { get; set; }
        }

        /// <summary>node 脚本：抓取任意 URL，把响应文本写入文件（argv[2]=输出文件, argv[3]=URL）。</summary>
        private const string SCRIPT_FETCH_TEXT = @"
const fs=require('fs');
const out=process.argv[2], u=process.argv[3];
fetch(u,{headers:{'User-Agent':'DSH-Manager/1.0'}})
 .then(r=>{if(!r.ok)process.exit(2);return r.text()})
 .then(t=>fs.writeFileSync(out,t))
 .catch(e=>process.exit(1));
";

        /// <summary>node 脚本：抓取 GitHub topic:dsh-plugin 前 3 页，合并写入文件。</summary>
        private const string SCRIPT_FETCH_GITHUB = @"
const fs=require('fs');
const out=process.argv[2];
const items=[];let total=0;
(async()=>{
 for(let p=1;p<=3;p++){
  const r=await fetch('https://api.github.com/search/repositories?q=topic:dsh-plugin&sort=updated&per_page=100&page='+p,{headers:{'User-Agent':'DSH-Manager/1.0'}});
  if(!r.ok)process.exit(2);
  const j=await r.json();
  total=j.total_count||0;
  (j.items||[]).forEach(i=>items.push(i));
 }
 fs.writeFileSync(out,JSON.stringify({total_count:total,items:items}));
})().catch(e=>process.exit(1));
";

        /// <summary>node 脚本：获取仓库默认分支并下载 ZIP 到文件（argv[2]=repo, argv[3]=输出文件, argv[4]=代理 127.0.0.1:7897 可空）。
        /// 原生 https + http.request CONNECT 隧道（undici fetch 分块传输会挂起）。</summary>
        private const string SCRIPT_DOWNLOAD_ZIP = @"
const fs=require('fs'),http=require('http'),https=require('https'),tls=require('tls');
const repo=process.argv[2], out=process.argv[3], proxy=process.argv[4]||'';
function getBuf(u,redir){
 return new Promise(function(resolve,reject){
  const url=new URL(u);
  let done=false;
  const finish=function(err,buf){if(done)return;done=true;err?reject(err):resolve(buf);};
  const handleRes=function(res2){
   if(res2.statusCode>=300&&res2.statusCode<400&&res2.headers.location){
    res2.resume();
    if((redir||0)>=5)return finish(new Error('too many redirects'));
    getBuf(new URL(res2.headers.location,u).toString(),(redir||0)+1).then(function(b){finish(null,b);},function(e){finish(e);});
    return;
   }
   if(res2.statusCode!==200){res2.resume();return finish(new Error('HTTP '+res2.statusCode));}
   const chunks=[];res2.on('data',function(c){chunks.push(c);});
   res2.on('end',function(){finish(null,Buffer.concat(chunks));});
   res2.on('error',function(e){finish(e);});
  };
  const doReq=function(opts){const r=https.request(opts,handleRes);r.on('error',function(e){finish(e);});r.end();};
  if(proxy){
   const p=new URL(proxy.indexOf('://')===-1?'http://'+proxy:proxy);
   const creq=http.request({host:p.hostname,port:p.port||80,method:'CONNECT',path:url.host+':443',headers:{Host:url.host}});
   creq.on('connect',function(res,socket){
    if(res.statusCode!==200){socket.destroy();return finish(new Error('proxy CONNECT '+res.statusCode));}
    const ts=tls.connect({socket:socket,servername:url.hostname},function(){
     doReq({createConnection:function(){return ts;},hostname:url.hostname,path:url.pathname+url.search,method:'GET',headers:Object.assign({Host:url.hostname,'User-Agent':'DSH-Manager/1.0'})});
    });
    ts.on('error',function(e){finish(e);});
   });
   creq.on('error',function(e){finish(e);});
   creq.end();
  } else {
   doReq({hostname:url.hostname,port:443,path:url.pathname+url.search,method:'GET',headers:Object.assign({Host:url.hostname,'User-Agent':'DSH-Manager/1.0'})});
  }
 });
}
(async()=>{
 const info=await getBuf('https://api.github.com/repos/'+repo);
 const j=JSON.parse(info.toString('utf8'));
 const br=j.default_branch||'main';
 const zip=await getBuf('https://codeload.github.com/'+repo+'/zip/refs/heads/'+br);
 fs.writeFileSync(out,zip);
})().catch(function(e){console.error('DLERR '+e.message);process.exit(1);});
";

        private const string TOPIC_URL = "https://github.com/topics/dsh-plugin";
        private int pluginSortCol = 0;
        private bool pluginSortDesc = false;
        private List<ListViewItem> allPluginItems = new List<ListViewItem>();

        /// <summary>node 脚本：多关键词搜索 GitHub 美化/皮肤插件（按 ⭐ 排序合并去重，写入文件）。</summary>
        private const string SCRIPT_FETCH_BEAUTIFY = @"
const fs=require('fs');
const out=process.argv[2];
const queries=['topic:dsh-plugin theme','topic:dsh-plugin skin','topic:dsh-plugin anime','topic:dsh-plugin miku','topic:dsh-plugin cyberpunk','topic:dsh-plugin retro','topic:dsh-plugin 二次元','topic:dsh-plugin glass','topic:dsh-plugin ui'];
const map={};
(async()=>{
 for(const q of queries){
  try{
   const r=await fetch('https://api.github.com/search/repositories?q='+encodeURIComponent(q)+'&sort=stars&order=desc&per_page=30',{headers:{'User-Agent':'DSH-Manager/1.0'}});
   if(!r.ok)continue;
   const j=await r.json();
   (j.items||[]).forEach(i=>{if(!map[i.full_name])map[i.full_name]=i;});
  }catch(e){}
 }
 const items=Object.keys(map).map(k=>map[k]).sort((a,b)=>b.stargazers_count-a.stargazers_count);
 fs.writeFileSync(out,JSON.stringify({total_count:items.length,items:items}));
})().catch(e=>process.exit(1));
";

        /// <summary>node 脚本：登记/移除 profile 插件（复刻 dsh plugin 的 reconcile 逻辑）：
        /// argv[2]=profile package.json, argv[3]=add|remove, argv[4]=包名, argv[5]=插件 package.json 路径（add 时用于检查 dsh.bundle）。
        /// 声明了 dsh.bundle 的依赖会追加进 dsh.profile.bundles（即启用为 profile 层）。</summary>
        private const string SCRIPT_EDIT_PLUGIN = @"
const fs=require('fs');
const p=process.argv[2], op=process.argv[3], name=process.argv[4], pluginPkg=process.argv[5]||'';
const j=JSON.parse(fs.readFileSync(p,'utf8'));
j.dependencies=j.dependencies||{};
j.dsh=j.dsh||{}; j.dsh.profile=j.dsh.profile||{}; j.dsh.profile.bundles=j.dsh.profile.bundles||[];
const bundles=j.dsh.profile.bundles;
if(op==='add'){
 j.dependencies[name]='file:./node_modules/'+name;
 let isBundle=false;
 try{const pk=JSON.parse(fs.readFileSync(pluginPkg,'utf8')); isBundle=!!(pk.dsh&&pk.dsh.bundle&&pk.dsh.bundle.patch);}catch(e){}
 if(isBundle&&bundles.indexOf(name)<0)bundles.push(name);
}else if(op==='remove'){
 delete j.dependencies[name];
 const i=bundles.indexOf(name); if(i>=0)bundles.splice(i,1);
}
fs.writeFileSync(p,JSON.stringify(j,null,2));
";

        /// <summary>运行 node 脚本（写入临时 js 文件，避免 -e 引号问题；输出走文件，避免管道阻塞）。</summary>
        private int RunNodeScript(string nodePath, string script, string[] scriptArgs, string proxy, int timeoutMs)
        {
            if (string.IsNullOrEmpty(nodePath)) return -1;
            string jsPath = Path.Combine(Path.GetTempPath(), "dsh-fetch-" + Guid.NewGuid().ToString("N") + ".js");
            try
            {
                File.WriteAllText(jsPath, script, Encoding.UTF8);
                StringBuilder arg = new StringBuilder();
                arg.Append('"').Append(jsPath).Append('"');
                if (scriptArgs != null)
                {
                    foreach (string a in scriptArgs)
                    {
                        arg.Append(" \"").Append(a.Replace("\"", "\\\"")).Append('"');
                    }
                }
                Process p = new Process();
                p.StartInfo.FileName = nodePath;
                p.StartInfo.Arguments = arg.ToString();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.EnvironmentVariables["HTTPS_PROXY"] = proxy ?? "";
                p.StartInfo.EnvironmentVariables["HTTP_PROXY"] = proxy ?? "";
                p.Start();
                if (!p.WaitForExit(timeoutMs))
                {
                    try
                    {
                        Process k = new Process();
                        k.StartInfo.FileName = "taskkill";
                        k.StartInfo.Arguments = "/PID " + p.Id + " /T /F";
                        k.StartInfo.UseShellExecute = false;
                        k.StartInfo.CreateNoWindow = true;
                        k.Start();
                        k.WaitForExit(3000);
                    }
                    catch { }
                    try { p.Kill(); } catch { }
                    return -2;
                }
                return p.ExitCode;
            }
            catch { return -1; }
            finally
            {
                try { File.Delete(jsPath); } catch { }
            }
        }

        /// <summary>探测本地 Clash/V2Ray 类代理端口（进程名匹配 + 常见端口兜底）。</summary>
        private static List<string> DetectProxyCandidates()
        {
            var list = new List<string>();
            var seen = new HashSet<int>();
            try
            {
                string output = StartProcCapture("netstat", "-ano -p tcp", null, 15000);
                if (output != null)
                {
                    Regex re = new Regex(@"TCP\s+127\.0\.0\.1:(\d+)\s+\S+\s+LISTENING\s+(\d+)");
                    foreach (Match m in re.Matches(output))
                    {
                        int port, pid;
                        if (!int.TryParse(m.Groups[1].Value, out port) || !int.TryParse(m.Groups[2].Value, out pid)) continue;
                        if (seen.Contains(port)) continue;
                        seen.Add(port);
                        string name = "";
                        try { name = Process.GetProcessById(pid).ProcessName.ToLowerInvariant(); } catch { }
                        if (name.Contains("verge") || name.Contains("clash") || name.Contains("mihomo") ||
                            name.Contains("v2ray") || name.Contains("xray") || name.Contains("sing"))
                            list.Add("http://127.0.0.1:" + port);
                    }
                }
            }
            catch { }
            foreach (int port in new int[] { 7897, 7890, 7891, 7892, 1080, 10808, 2080 })
            {
                if (!seen.Contains(port)) list.Add("http://127.0.0.1:" + port);
            }
            return list;
        }

        /// <summary>GET 文本（TLS12 + UA），带超时。</summary>
        private static string WebGet(string url, int timeoutSec)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Task<string> task = Task.Run(() =>
            {
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "DSH-Manager/1.0";
                    wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    return wc.DownloadString(url);
                }
            });
            if (!task.Wait(TimeSpan.FromSeconds(timeoutSec))) throw new TimeoutException("timeout");
            return task.Result;
        }

        /// <summary>WebClient 下载文件（TLS12），带超时。</summary>
        private static void WebDownloadFile(string url, string dest, int timeoutSec)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Task task = Task.Run(() =>
            {
                using (var wc = new WebClient()) wc.DownloadFile(url, dest);
            });
            if (!task.Wait(TimeSpan.FromSeconds(timeoutSec))) throw new TimeoutException("timeout");
        }

        private static PluginList ParsePluginList(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var ser = new DataContractJsonSerializer(typeof(PluginList));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return ser.ReadObject(ms) as PluginList;
            }
            catch { return null; }
        }

        private static GhSearch ParseGhSearch(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var ser = new DataContractJsonSerializer(typeof(GhSearch));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return ser.ReadObject(ms) as GhSearch;
            }
            catch { return null; }
        }

        private static PluginList ConvertGh(GhSearch gh)
        {
            var list = new PluginList
            {
                Total = gh.Total,
                FetchedAt = DateTime.Now.ToString("o"),
                Source = "github",
                Plugins = new List<PluginInfo>()
            };
            foreach (GhRepo r in gh.Items ?? new List<GhRepo>())
            {
                list.Plugins.Add(new PluginInfo
                {
                    FullName = r.FullName,
                    Name = r.Name,
                    Description = r.Description,
                    HtmlUrl = r.HtmlUrl,
                    Stars = r.Stars,
                    UpdatedAt = r.UpdatedAt,
                    Topics = r.Topics ?? new List<string>(),
                    Category = Categorize(r.Topics, r.Description)
                });
            }
            return list;
        }

        /// <summary>关键词分类（与插件服务器规则一致）。</summary>
        private static string Categorize(List<string> topics, string desc)
        {
            string hay = string.Join(" ", topics ?? new List<string>()).ToLowerInvariant() + " " + (desc ?? "").ToLowerInvariant();
            string[][] rules = {
                new[] { "界面主题", "theme", "skin", "ui", "style", "外观", "主题", "皮肤" },
                new[] { "身份认证", "auth", "authentication", "login", "security", "认证", "登录" },
                new[] { "计费支付", "billing", "payment", "pay", "计费", "支付" },
                new[] { "视觉多模态", "vision", "multimodal", "image", "ocr", "视觉", "多模态", "图像" },
                new[] { "智能体协作", "agent", "preset", "relay", "collaboration", "multi-agent", "智能体", "协作", "预设" },
                new[] { "网关与集成", "gateway", "api", "integration", "bridge", "网关", "集成", "桥接" },
                new[] { "工具命令", "tool", "command", "util", "helper", "工具", "命令" },
                new[] { "合集资源", "awesome", "list", "directory", "合集", "清单", "导航" }
            };
            foreach (string[] rule in rules)
            {
                for (int i = 1; i < rule.Length; i++)
                {
                    if (hay.IndexOf(rule[i], StringComparison.Ordinal) >= 0) return rule[0];
                }
            }
            return "其他";
        }

        private string GetServerUrl()
        {
            string s = txtPluginServer != null ? txtPluginServer.Text.Trim() : pluginServer;
            if (string.IsNullOrEmpty(s)) return "";
            if (!s.StartsWith("http://") && !s.StartsWith("https://")) s = "http://" + s;
            return s.TrimEnd('/');
        }

        /// <summary>插件数据缓存文件。</summary>
        private string PluginCachePath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "DeepSeek-Harness-Manager");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "plugins-cache.json");
            }
        }

        private void SavePluginCache(PluginList data)
        {
            try
            {
                var ser = new DataContractJsonSerializer(typeof(PluginList));
                using (var ms = new MemoryStream())
                {
                    ser.WriteObject(ms, data);
                    File.WriteAllBytes(PluginCachePath, ms.ToArray());
                }
            }
            catch { }
        }

        private PluginList LoadPluginCache()
        {
            try
            {
                if (!File.Exists(PluginCachePath)) return null;
                var ser = new DataContractJsonSerializer(typeof(PluginList));
                using (var fs = File.OpenRead(PluginCachePath))
                    return ser.ReadObject(fs) as PluginList;
            }
            catch { return null; }
        }

        private static string FormatTime(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "--";
            DateTime dt;
            if (DateTime.TryParse(iso, out dt)) return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            return iso;
        }

        // ==================== 美化页数据（独立多关键词搜索） ====================

        private string BeautifyCachePath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "DeepSeek-Harness-Manager");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "beautify-cache.json");
            }
        }

        private void SaveBeautifyCache(PluginList data)
        {
            try
            {
                var ser = new DataContractJsonSerializer(typeof(PluginList));
                using (var ms = new MemoryStream())
                {
                    ser.WriteObject(ms, data);
                    File.WriteAllBytes(BeautifyCachePath, ms.ToArray());
                }
            }
            catch { }
        }

        private PluginList LoadBeautifyCache()
        {
            try
            {
                if (!File.Exists(BeautifyCachePath)) return null;
                var ser = new DataContractJsonSerializer(typeof(PluginList));
                using (var fs = File.OpenRead(BeautifyCachePath))
                    return ser.ReadObject(fs) as PluginList;
            }
            catch { return null; }
        }

        /// <summary>合并新旧美化缓存（按 fullName 去重），防止搜索结果波动/部分查询失败导致旧条目丢失。
        /// 新结果覆盖 fetchedAt 与计数，旧缓存里独有的条目全部保留。</summary>
        private static PluginList MergeBeautifyCaches(PluginList fresh, PluginList old)
        {
            if (old == null || old.Plugins == null || old.Plugins.Count == 0) return fresh;
            var merged = new List<PluginInfo>();
            var seen = new HashSet<string>();
            foreach (PluginInfo p in old.Plugins)
            {
                if (p == null || string.IsNullOrEmpty(p.FullName)) continue;
                if (seen.Add(p.FullName)) merged.Add(p);
            }
            foreach (PluginInfo p in fresh.Plugins)
            {
                if (p == null || string.IsNullOrEmpty(p.FullName)) continue;
                if (seen.Add(p.FullName)) merged.Add(p);
            }
            return new PluginList { FetchedAt = fresh.FetchedAt, Total = merged.Count, Source = fresh.Source, Plugins = merged };
        }

        /// <summary>刷新美化插件列表（多关键词搜索、按⭐排序；30 分钟缓存；失败回退缓存/300 列表过滤）。</summary>
        private async void RefreshBeautifyAsync(bool force)
        {
            if (beautyLoading) return;
            if (!force)
            {
                PluginList cached = LoadBeautifyCache();
                if (cached != null && cached.Plugins != null && cached.Plugins.Count > 0)
                {
                    DateTime ft;
                    bool fresh = DateTime.TryParse(cached.FetchedAt, out ft) && (DateTime.UtcNow - ft.ToUniversalTime()).TotalMinutes < 30;
                    if (fresh && !beautyBusy)
                    {
                        beautifyData = cached;
                        ApplyBeautifyToUI();
                        lblBeautyStatus.Text = "美化插件: " + cached.Plugins.Count + " 个（缓存，30 分钟自动刷新）";
                        return;
                    }
                }
            }
            beautyLoading = true;
            btnBeautyRefresh.Enabled = false;
            lblBeautyStatus.Text = "正在搜索美化插件...";
            PluginList result = null;
            string err = null;
            try { result = await Task.Run(() => FetchBeautifyList(out err)); }
            catch (Exception ex) { err = ex.Message; }
            if (IsDisposed) return;
            beautyLoading = false;
            btnBeautyRefresh.Enabled = true;
            if (!beautyBusy)
            {
                if (result != null && result.Plugins != null && result.Plugins.Count > 0)
                {
                    PluginList merged = MergeBeautifyCaches(result, LoadBeautifyCache());
                    SaveBeautifyCache(merged);
                    beautifyData = merged;
                    ApplyBeautifyToUI();
                    lblBeautyStatus.Text = "美化插件: " + merged.Plugins.Count + " 个（GitHub 搜索+本地缓存，按⭐排序）";
                }
                else
                {
                    PluginList cached = LoadBeautifyCache();
                    if (cached != null && cached.Plugins != null && cached.Plugins.Count > 0)
                    {
                        beautifyData = cached;
                        ApplyBeautifyToUI();
                        lblBeautyStatus.Text = "搜索失败，显示本地缓存（" + (err ?? "未知错误") + "）";
                    }
                    else
                    {
                        ApplyBeautifyToUI(); // 兜底：从已拉取的 300 列表过滤
                        lblBeautyStatus.Text = "美化搜索失败: " + (err ?? "未知错误") + "（已用插件列表过滤）";
                    }
                }
            }
        }

        /// <summary>抓取美化插件：配置了插件服务器时优先走服务器（无 GitHub 访问也能浏览），失败才回退多关键词搜索（直连 → 本地代理）。</summary>
        private PluginList FetchBeautifyList(out string error)
        {
            error = null;
            string node = FindNode();
            string server = GetServerUrl();
            if (!string.IsNullOrEmpty(server))
            {
                // 优先 /api/beautify（服务端已过滤美化），旧版服务器无此端点时回退 /api/plugins
                string[] urls = { server + "/api/beautify?force=1", server + "/api/plugins?force=1" };
                foreach (string url in urls)
                {
                    PluginList srv = FetchServerList(url);
                    if (srv != null && srv.Plugins != null && srv.Plugins.Count > 0)
                    {
                        var beauties = new List<PluginInfo>();
                        foreach (PluginInfo p in srv.Plugins)
                        {
                            string cat;
                            if (IsBeautifyPlugin(p, out cat)) { p.Category = cat; beauties.Add(p); }
                        }
                        if (beauties.Count > 0)
                        {
                            var merged = new PluginList { FetchedAt = srv.FetchedAt, Total = beauties.Count, Source = "server" };
                            merged.Plugins = beauties;
                            return merged;
                        }
                    }
                }
            }
            if (node == null) { error = "未找到 node.exe"; return null; }
            string tmpFile = Path.Combine(Path.GetTempPath(), "dsh-beautify-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                if (RunNodeScript(node, SCRIPT_FETCH_BEAUTIFY, new string[] { tmpFile }, null, 90000) == 0 && new FileInfo(tmpFile).Length > 0)
                {
                    PluginList r = ParseGitHubFile(tmpFile);
                    if (r != null && r.Plugins != null && r.Plugins.Count > 0) return r;
                }
                foreach (string proxy in DetectProxyCandidates())
                {
                    if (RunNodeScript(node, SCRIPT_FETCH_BEAUTIFY, new string[] { tmpFile }, proxy, 90000) == 0 && new FileInfo(tmpFile).Length > 0)
                    {
                        PluginList r = ParseGitHubFile(tmpFile);
                        if (r != null && r.Plugins != null && r.Plugins.Count > 0) return r;
                    }
                }
                error = string.IsNullOrEmpty(server) ? "GitHub 搜索失败（网络不通或限流）" : "服务器与 GitHub 均不可用";
                return null;
            }
            finally
            {
                try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
            }
        }

        /// <summary>刷新插件列表（后台拉取，UI 线程更新）。</summary>
        private async void RefreshPluginsAsync(bool force)
        {
            if (pluginLoading) return;
            pluginLoading = true;
            btnPluginRefresh.Enabled = false;
            lblPluginStatus.Text = "正在获取插件列表...";
            PluginList result = null;
            string err = null;
            try { result = await Task.Run(() => FetchPluginList(force, out err)); }
            catch (Exception ex) { err = ex.Message; }
            if (IsDisposed) return;
            pluginLoading = false;
            btnPluginRefresh.Enabled = true;
            if (result != null && result.Plugins != null && result.Plugins.Count > 0)
            {
                SavePluginCache(result);
                lastPluginData = result;
                ApplyPluginsToUI(result);
                ApplyBeautifyToUI();
                string src = result.Source == "cache" ? " | 来源: 缓存" : "";
                lblPluginStatus.Text = "上次更新: " + FormatTime(result.FetchedAt) + src +
                    " | 共 " + result.Total + " 个（显示最近 " + result.Plugins.Count + " 个活跃）";
                if (beautyList.Items.Count == 0 && !beautyBusy) lblBeautyStatus.Text = "美化插件: " + beautyItems.Count + " 个（按风格分组）";
            }
            else
            {
                PluginList cached = LoadPluginCache();
                if (cached != null && cached.Plugins != null && cached.Plugins.Count > 0)
                {
                    lastPluginData = cached;
                    ApplyPluginsToUI(cached);
                    ApplyBeautifyToUI();
                    lblPluginStatus.Text = "连接失败，显示本地缓存: " + FormatTime(cached.FetchedAt) + "（" + (err ?? "未知错误") + "）";
                }
                else
                {
                    pluginList.Items.Clear();
                    pluginList.Groups.Clear();
                    lblPluginStatus.Text = "获取失败: " + (err ?? "未知错误") + "（无本地缓存）";
                }
            }
        }

        /// <summary>取数级联：配置了服务器 → 走服务器；服务器不可达则回退直连 GitHub；仍失败由上层回退缓存。</summary>
        private PluginList FetchPluginList(bool force, out string error)
        {
            error = null;
            string server = GetServerUrl();
            if (!string.IsNullOrEmpty(server))
            {
                string url = server + "/api/plugins" + (force ? "?force=1" : "");
                PluginList r = FetchServerList(url);
                if (r != null) return r;
                error = "插件服务器不可达: " + server;
                // 服务器不可达时继续尝试直连 GitHub（例如 VPN 可用）
                PluginList r2 = FetchGitHubDirect();
                if (r2 != null) return r2;
                error = "插件服务器与 GitHub 均不可达（可能需要网络/代理）";
                return null;
            }
            PluginList r3 = FetchGitHubDirect();
            if (r3 != null) return r3;
            error = "无法连接 GitHub（可能需要 VPN，或在上方配置插件服务器）";
            return null;
        }

        private PluginList FetchServerList(string url)
        {
            // 1) WebClient（http 直连本机/局域网服务器最快；https 服务器在健康机器上也可用）
            try
            {
                string json = WebGet(url, 15);
                PluginList r = ParsePluginList(json);
                if (r != null) return r;
            }
            catch { }
            // 2) node 兜底（本机 schannel 损坏时的备选）
            string node = FindNode();
            if (node != null)
            {
                string outFile = Path.GetTempFileName();
                try
                {
                    if (RunNodeScript(node, SCRIPT_FETCH_TEXT, new string[] { outFile, url }, null, 45000) == 0)
                    {
                        PluginList r = ParsePluginList(File.ReadAllText(outFile));
                        if (r != null) return r;
                    }
                }
                catch { }
                finally { try { File.Delete(outFile); } catch { } }
            }
            return null;
        }

        private PluginList FetchGitHubDirect()
        {
            string node = FindNode();
            if (node != null)
            {
                // 1) node 直连
                string outFile = Path.GetTempFileName();
                try
                {
                    if (RunNodeScript(node, SCRIPT_FETCH_GITHUB, new string[] { outFile }, null, 90000) == 0)
                    {
                        PluginList r = ParseGitHubFile(outFile);
                        if (r != null) return r;
                    }
                }
                catch { }
                finally { try { File.Delete(outFile); } catch { } }

                // 2) node 走本地代理（Clash/V2Ray 等）
                foreach (string proxy in DetectProxyCandidates())
                {
                    string outFile2 = Path.GetTempFileName();
                    try
                    {
                        if (RunNodeScript(node, SCRIPT_FETCH_GITHUB, new string[] { outFile2 }, proxy, 60000) == 0)
                        {
                            PluginList r = ParseGitHubFile(outFile2);
                            if (r != null) return r;
                        }
                    }
                    catch { }
                    finally { try { File.Delete(outFile2); } catch { } }
                }
            }
            // 3) WebClient（schannel 正常的机器）
            try
            {
                var gh = new GhSearch { Total = 0, Items = new List<GhRepo>() };
                for (int page = 1; page <= 3; page++)
                {
                    string json = WebGet("https://api.github.com/search/repositories?q=topic:dsh-plugin&sort=updated&per_page=100&page=" + page, 20);
                    GhSearch p = ParseGhSearch(json);
                    if (p == null) return null;
                    gh.Total = p.Total;
                    if (p.Items != null) gh.Items.AddRange(p.Items);
                }
                return ConvertGh(gh);
            }
            catch { }
            return null;
        }

        private static PluginList ParseGitHubFile(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                string json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json)) return null;
                GhSearch gh = ParseGhSearch(json);
                if (gh == null) return null;
                return ConvertGh(gh);
            }
            catch { return null; }
        }

        private void ApplyPluginsToUI(PluginList data)
        {
            if (pluginList.InvokeRequired)
            {
                pluginList.Invoke(new Action<PluginList>(ApplyPluginsToUI), data);
                return;
            }
            pluginList.BeginUpdate();
            pluginList.Items.Clear();
            pluginList.Groups.Clear();
            allPluginItems = new List<ListViewItem>();
            var catCount = new Dictionary<string, int>();
            foreach (PluginInfo p in data.Plugins)
            {
                string cat = string.IsNullOrEmpty(p.Category) ? "其他" : p.Category;
                int n;
                catCount.TryGetValue(cat, out n);
                catCount[cat] = n + 1;
            }
            List<string> cats = catCount.Keys.OrderByDescending(c => catCount[c]).ToList();
            foreach (string c in cats) pluginList.Groups.Add(new ListViewGroup(c, HorizontalAlignment.Left));
            foreach (PluginInfo p in data.Plugins)
            {
                string cat = string.IsNullOrEmpty(p.Category) ? "其他" : p.Category;
                ListViewItem item = new ListViewItem(p.Name);
                item.SubItems.Add(cat);
                item.SubItems.Add(p.Description ?? "");
                item.SubItems.Add(p.Stars.ToString());
                string upd = p.UpdatedAt ?? "";
                if (upd.Length >= 10) upd = upd.Substring(0, 10);
                item.SubItems.Add(upd);
                item.Tag = p;
                ListViewGroup g = pluginList.Groups[cat];
                if (g != null) item.Group = g;
                allPluginItems.Add(item);
            }
            pluginList.EndUpdate();
            string cur = cmbPluginCategory.SelectedItem as string;
            cmbPluginCategory.Items.Clear();
            cmbPluginCategory.Items.Add("全部");
            foreach (string c in cats) cmbPluginCategory.Items.Add(c);
            if (cur != null && cmbPluginCategory.Items.Contains(cur)) cmbPluginCategory.SelectedItem = cur;
            else cmbPluginCategory.SelectedIndex = 0;
            ApplyCategoryFilter();
        }

        private void ApplyCategoryFilter()
        {
            string sel = cmbPluginCategory.SelectedItem as string;
            pluginList.BeginUpdate();
            pluginList.Items.Clear();
            foreach (ListViewItem item in allPluginItems)
            {
                if (string.IsNullOrEmpty(sel) || sel == "全部") pluginList.Items.Add(item);
                else if (item.SubItems[1].Text == sel) pluginList.Items.Add(item);
            }
            pluginList.EndUpdate();
        }

        private void SortPluginsByColumn(int col)
        {
            if (col == pluginSortCol) pluginSortDesc = !pluginSortDesc;
            else { pluginSortCol = col; pluginSortDesc = false; }
            allPluginItems.Sort(delegate(ListViewItem a, ListViewItem b)
            {
                string sa = a.SubItems[col].Text, sb = b.SubItems[col].Text;
                int dir = pluginSortDesc ? -1 : 1;
                if (col == 3)
                {
                    int ia, ib;
                    int.TryParse(sa, out ia);
                    int.TryParse(sb, out ib);
                    return ia.CompareTo(ib) * dir;
                }
                return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase) * dir;
            });
            ApplyCategoryFilter();
        }

        // ==================== 美化页 ====================

        private string BeautyStatePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepSeek-Harness-Manager", "beautify.json"); }
        }

        private string GetBeautyStatusText(string fullName)
        {
            BeautyEntry e;
            if (beautyState.TryGetValue(fullName, out e) && e != null)
                return e.Status == "enabled" ? "已启用" : "已下载";
            return "未下载";
        }

        private void SaveBeautyState()
        {
            try
            {
                var st = new BeautyState { Plugins = beautyState.Values.ToList() };
                string dir = Path.GetDirectoryName(BeautyStatePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (var ms = new MemoryStream())
                {
                    var ser = new DataContractJsonSerializer(typeof(BeautyState));
                    ser.WriteObject(ms, st);
                    File.WriteAllBytes(BeautyStatePath, ms.ToArray());
                }
            }
            catch { }
        }

        private void LoadBeautyState()
        {
            try
            {
                if (!File.Exists(BeautyStatePath)) return;
                var ser = new DataContractJsonSerializer(typeof(BeautyState));
                using (var fs = File.OpenRead(BeautyStatePath))
                {
                    var st = ser.ReadObject(fs) as BeautyState;
                    if (st != null && st.Plugins != null)
                    {
                        beautyState.Clear();
                        foreach (BeautyEntry e in st.Plugins)
                            // 自愈：enabled 但无 PkgName 的遗留数据丢弃（无法停用）
                            if (e != null && e.Repo != null && !(e.Status == "enabled" && string.IsNullOrEmpty(e.PkgName)))
                                beautyState[e.Repo] = e;
                    }
                }
            }
            catch { }
        }

        /// <summary>是否为美化类插件（二次元/皮肤/主题等），并给出风格分类。</summary>
        private static bool IsBeautifyPlugin(PluginInfo p, out string style)
        {
            style = null;
            if (p == null) return false;
            string hay = ((p.Name ?? "") + " " + (p.Description ?? "") + " " + string.Join(" ", p.Topics ?? new List<string>())).ToLowerInvariant();
            if (Regex.IsMatch(hay, "miku|初音|anime|touhou|灵梦|东方|原神|genshin|waifu|二次元|vocaloid|崩铁|崩坏|星穹|碧蓝|galgame|立绘"))
                style = "二次元动漫";
            else if (Regex.IsMatch(hay, "cyber|赛博|霓虹|night ?city"))
                style = "赛博朋克";
            else if (Regex.IsMatch(hay, "qq20|怀旧|复古|retro|win95|vista|2005|2006|2007|2008"))
                style = "怀旧复古";
            else if (Regex.IsMatch(hay, "glass|frosted|玻璃|磨砂|毛玻璃|transparent"))
                style = "玻璃质感";
            else if (Regex.IsMatch(hay, "skin|theme|皮肤|主题|换肤|美化|cosmetic|配色|色系"))
                style = "换肤系统";
            return style != null;
        }

        private void ApplyBeautifyToUI()
        {
            if (beautyList.InvokeRequired)
            {
                beautyList.Invoke(new Action(ApplyBeautifyToUI));
                return;
            }
            beautyList.BeginUpdate();
            beautyList.Items.Clear();
            beautyList.Groups.Clear();
            beautyItems = new List<ListViewItem>();
            PluginList source = (beautifyData != null && beautifyData.Plugins != null && beautifyData.Plugins.Count > 0)
                ? beautifyData : lastPluginData;
            if (source == null || source.Plugins == null)
            {
                beautyList.EndUpdate();
                lblBeautyStatus.Text = "尚无插件数据，请先刷新";
                return;
            }
            var beauties = new List<PluginInfo>();
            foreach (PluginInfo p in source.Plugins)
            {
                string style;
                if (IsBeautifyPlugin(p, out style)) beauties.Add(p);
            }
            // 合并本地已安装/已下载插件（真实 profile 对账结果；GitHub 目录缺失也能显示，修复"已启用却不显示"）
            foreach (var kv in beautyState)
            {
                BeautyEntry e = kv.Value;
                if (e == null || string.IsNullOrEmpty(e.PkgName) || string.IsNullOrEmpty(e.Repo)) continue;
                bool exists = false;
                foreach (PluginInfo p in beauties)
                    if (p.FullName == e.Repo) { exists = true; break; }
                if (exists) continue;
                string shortName = e.PkgName.StartsWith("@") && e.PkgName.IndexOf('/') > 0
                    ? e.PkgName.Substring(e.PkgName.IndexOf('/') + 1) : e.PkgName;
                beauties.Add(new PluginInfo
                {
                    FullName = e.Repo,
                    Name = shortName,
                    Description = e.Status == "enabled" ? "本地已启用（profile bundles 已登记）" : "本地已下载（GitHub 目录未收录）",
                    HtmlUrl = "https://github.com/" + e.Repo,
                    Stars = 0,
                    UpdatedAt = "",
                    Category = "已安装"
                });
            }
            // 已启用置顶
            var ordered = beauties.OrderByDescending(p => GetBeautyStatusText(p.FullName) == "已启用" ? 1 : 0).ToList();
            beauties = ordered;
            var catCount = new Dictionary<string, int>();
            foreach (PluginInfo p in beauties)
            {
                string style;
                if (!IsBeautifyPlugin(p, out style)) style = p.Category ?? "其他";
                int n;
                catCount.TryGetValue(style, out n);
                catCount[style] = n + 1;
            }
            List<string> styles = catCount.Keys.OrderByDescending(s => catCount[s]).ToList();
            foreach (string s in styles) beautyList.Groups.Add(new ListViewGroup(s, HorizontalAlignment.Left));
            foreach (PluginInfo p in beauties)
            {
                string style;
                if (!IsBeautifyPlugin(p, out style)) style = p.Category ?? "其他";
                ListViewItem item = new ListViewItem(p.Name);
                item.SubItems.Add(style);
                item.SubItems.Add(p.Description ?? "");
                item.SubItems.Add(p.Stars.ToString());
                string upd = p.UpdatedAt ?? "";
                if (upd.Length >= 10) upd = upd.Substring(0, 10);
                item.SubItems.Add(upd);
                string stText = GetBeautyStatusText(p.FullName);
                item.SubItems.Add(stText);
                if (stText == "已启用")
                {
                    item.BackColor = Color.FromArgb(215, 240, 220); // 整行浅绿标识启用中
                    item.SubItems[5].ForeColor = OkGreen;
                }
                item.Tag = p;
                ListViewGroup g = beautyList.Groups[style];
                if (g != null) item.Group = g;
                beautyItems.Add(item);
            }
            beautyList.EndUpdate();
            string cur = cmbBeautyStyle.SelectedItem as string;
            cmbBeautyStyle.Items.Clear();
            cmbBeautyStyle.Items.Add("全部");
            foreach (string s in styles) cmbBeautyStyle.Items.Add(s);
            if (cur != null && cmbBeautyStyle.Items.Contains(cur)) cmbBeautyStyle.SelectedItem = cur;
            else cmbBeautyStyle.SelectedIndex = 0;
            beautyCountText = "美化插件: " + beauties.Count + " 个（共 " + source.Plugins.Count + " 个搜索结果，含本地已安装）";
            lblBeautyStatus.Text = beautyCountText;
            UpdateBeautyFooter();
            ApplyBeautyFilter();
        }

        /// <summary>更新美化页底部摘要（启用中/已下载计数 + 数据源 + 图例）。</summary>
        private void UpdateBeautyFooter()
        {
            if (lblBeautyFooter == null) return;
            int enabledCount = 0, downloadedCount = 0;
            foreach (var kv in beautyState)
            {
                BeautyEntry e = kv.Value;
                if (e == null) continue;
                if (e.Status == "enabled") enabledCount++;
                else if (e.Status == "downloaded") downloadedCount++;
            }
            string src = beautifyData != null ? (beautifyData.Source == "server" ? "插件服务器" : "GitHub") : "本地";
            lblBeautyFooter.Text = "启用中: " + enabledCount + " 个 · 已下载: " + downloadedCount + " 个 · 数据源: " + src +
                " ｜ 浅绿 = 启用中 ｜ 启用/停用后重启 DSH 服务生效";
        }

        private void ApplyBeautyFilter()
        {
            string sel = cmbBeautyStyle.SelectedItem as string;
            string stSel = cmbBeautyStatus != null ? cmbBeautyStatus.SelectedItem as string : null;
            if (string.IsNullOrEmpty(stSel)) stSel = "全部";
            beautyList.BeginUpdate();
            beautyList.Items.Clear();
            int shown = 0;
            foreach (ListViewItem item in beautyItems)
            {
                if (!string.IsNullOrEmpty(sel) && sel != "全部" && item.SubItems[1].Text != sel) continue;
                if (stSel != "全部")
                {
                    PluginInfo p = item.Tag as PluginInfo;
                    string st = p != null ? GetBeautyStatusText(p.FullName) : "未下载";
                    bool match;
                    if (stSel == "已下载") match = st == "已下载" || st == "已启用";
                    else match = st == "未下载"; // 未下载
                    if (!match) continue;
                }
                beautyList.Items.Add(item);
                shown++;
            }
            beautyList.EndUpdate();
            // 空结果引导
            if (shown == 0 && stSel != "全部" && beautyItems.Count > 0)
                lblBeautyStatus.Text = "没有「" + stSel + "」的插件 —— 切换「状态: 全部」浏览全部 " + beautyItems.Count + " 个美化插件";
            else if (lblBeautyStatus.Text != null && lblBeautyStatus.Text.StartsWith("没有「") && beautyCountText != null)
                lblBeautyStatus.Text = beautyCountText; // 恢复计数文案
        }

        private void SortBeautifyByColumn(int col)
        {
            if (col == beautySortCol) beautySortDesc = !beautySortDesc;
            else { beautySortCol = col; beautySortDesc = false; }
            beautyItems.Sort(delegate(ListViewItem a, ListViewItem b)
            {
                string sa = a.SubItems[col].Text, sb = b.SubItems[col].Text;
                int dir = beautySortDesc ? -1 : 1;
                if (col == 3)
                {
                    int ia, ib;
                    int.TryParse(sa, out ia);
                    int.TryParse(sb, out ib);
                    return ia.CompareTo(ib) * dir;
                }
                return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase) * dir;
            });
            ApplyBeautyFilter();
        }

        private void RefreshBeautyButtons()
        {
            bool has = beautyList.SelectedItems.Count > 0;
            btnBeautyOpen.Enabled = has;
            btnBeautyDisable.Enabled = false;
            btnBeautyEnable.Enabled = false;
            btnBeautyEnable.Text = "启用";
            if (has)
            {
                PluginInfo p = beautyList.SelectedItems[0].Tag as PluginInfo;
                if (p != null)
                {
                    string st = GetBeautyStatusText(p.FullName);
                    if (st == "已启用")
                    {
                        btnBeautyEnable.Text = "已启用"; // 正在使用中
                        btnBeautyDisable.Enabled = true;
                    }
                    else btnBeautyEnable.Enabled = true;
                }
            }
        }

        private async void BtnBeautyEnable_Click(object sender, EventArgs e)
        {
            if (beautyList.SelectedItems.Count == 0) return;
            PluginInfo sel = beautyList.SelectedItems[0].Tag as PluginInfo;
            if (sel == null) return;
            if (!await beautyOpLock.WaitAsync(0)) { lblBeautyStatus.Text = "正在处理其他操作，请稍候再试"; return; }
            try
            {
                if (IsDisposed) return;
                string safeName = sel.FullName.Replace('/', '_').Replace('\\', '_');
                string localDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh-beautify", safeName);
                string zipPath = null;
                // 无本地缓存才需要下载：弹保存框让用户选择 ZIP 存放位置（取消则中止）
                if (!File.Exists(Path.Combine(localDir, "package.json")))
                {
                    string dlDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    if (!Directory.Exists(dlDir)) dlDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    using (var dlg = new SaveFileDialog { FileName = sel.Name + ".zip", Filter = "ZIP 压缩包 (*.zip)|*.zip", InitialDirectory = dlDir, Title = "选择下载的压缩包保存位置" })
                    {
                        if (dlg.ShowDialog() != DialogResult.OK)
                        {
                            lblBeautyStatus.Text = "已取消启用 " + sel.Name;
                            return;
                        }
                        zipPath = dlg.FileName;
                    }
                }
                beautyBusy = true;
                btnBeautyEnable.Enabled = false;
                btnBeautyDisable.Enabled = false;
                lblBeautyStatus.Text = "正在准备 " + sel.Name + " ...";
                string err = null;
                bool ok = await Task.Run(() => PrepareBeautyPlugin(sel, localDir, zipPath, out err));
                if (IsDisposed) return;
                if (!ok)
                {
                    lblBeautyStatus.Text = "准备失败: " + (err ?? "未知错误");
                    return;
                }
                string pkgName = FindPluginPkgName(localDir);
                if (string.IsNullOrEmpty(pkgName))
                {
                    lblBeautyStatus.Text = "未识别到插件包名（package.json 缺失），无法自动启用";
                    Log("[美化] 未识别包名，仓库: " + sel.FullName + "，本地目录: " + localDir);
                    return;
                }
                if (!IsSafePkgName(pkgName))
                {
                    lblBeautyStatus.Text = "插件包名不合法，已停止: " + pkgName;
                    Log("[美化] 非法包名: " + pkgName);
                    return;
                }
                lblBeautyStatus.Text = "正在启用 " + pkgName + " ...";
                bool ok2 = await Task.Run(() => EnableBeautyPlugin(sel.FullName, pkgName, localDir, out err));
                if (IsDisposed) return;
                if (ok2)
                {
                    lblBeautyStatus.Text = "已启用 " + pkgName + " —— 重启 DSH 服务后生效";
                    Log("[美化] 已启用 " + pkgName + "（" + sel.FullName + "）");
                }
                else
                {
                    lblBeautyStatus.Text = "启用失败: " + (err ?? "未知错误");
                    Log("[美化] 启用失败 " + pkgName + ": " + err + "；可手动: cd %USERPROFILE%\\.dsh\\profiles\\web && npm install \"" + localDir + "\"");
                }
            }
            finally
            {
                beautyBusy = false;
                beautyOpLock.Release();
                if (!IsDisposed)
                {
                    RefreshBeautyButtons();
                    ApplyBeautyFilter();
                }
            }
        }

        private async void BtnBeautyDisable_Click(object sender, EventArgs e)
        {
            if (beautyList.SelectedItems.Count == 0) return;
            PluginInfo sel = beautyList.SelectedItems[0].Tag as PluginInfo;
            if (sel == null) return;
            BeautyEntry entry;
            if (!beautyState.TryGetValue(sel.FullName, out entry) || entry == null || string.IsNullOrEmpty(entry.PkgName))
            {
                lblBeautyStatus.Text = "该插件没有启用记录，无需停用";
                return;
            }
            if (!await beautyOpLock.WaitAsync(0)) { lblBeautyStatus.Text = "正在处理其他操作，请稍候再试"; return; }
            try
            {
                if (IsDisposed) return;
                beautyBusy = true;
                btnBeautyEnable.Enabled = false;
                btnBeautyDisable.Enabled = false;
                lblBeautyStatus.Text = "正在停用 " + entry.PkgName + " ...";
                string err = null;
                bool ok = await Task.Run(() => DisableBeautyPlugin(entry.PkgName, out err));
                if (IsDisposed) return;
                if (ok)
                {
                    entry.Status = "downloaded";
                    SaveBeautyState();
                    lblBeautyStatus.Text = "已停用 " + entry.PkgName + " —— 重启 DSH 服务后生效";
                    Log("[美化] 已停用 " + entry.PkgName);
                }
                else lblBeautyStatus.Text = "停用失败: " + (err ?? "未知错误");
            }
            finally
            {
                beautyBusy = false;
                beautyOpLock.Release();
                if (!IsDisposed)
                {
                    RefreshBeautyButtons();
                    ApplyBeautyFilter();
                }
            }
        }

        /// <summary>准备本地插件目录：已有则直接返回；否则下载 ZIP（保存到 zipPath，保留不删）并安全解压出插件根目录。</summary>
        private bool PrepareBeautyPlugin(PluginInfo p, string localDir, string zipPath, out string error)
        {
            error = null;
            try
            {
                if (File.Exists(Path.Combine(localDir, "package.json"))) return true;
                if (Directory.Exists(localDir)) Directory.Delete(localDir, true);
                if (string.IsNullOrEmpty(zipPath)) { error = "未指定 ZIP 保存路径"; return false; }
                string tmpExt = Path.Combine(Path.GetTempPath(), "dsh-beautify-x-" + Guid.NewGuid().ToString("N"));
                try
                {
                    if (!DownloadPluginZip(p, zipPath, out error)) return false;
                    if (!IsZipFile(zipPath)) { error = "下载的文件不是有效的 ZIP 压缩包（可能损坏或代理返回了错误内容）"; return false; }
                    string safeErr;
                    if (!SafeExtractZip(zipPath, tmpExt, out safeErr)) { error = "压缩包解压被拒绝: " + safeErr; return false; }
                    string root = FindPluginRoot(tmpExt);
                    if (root == null)
                    {
                        error = "ZIP 内未找到含 package.json 的插件目录";
                        return false;
                    }
                    string parent = Path.GetDirectoryName(localDir);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    DirectoryCopy(root, localDir);
                    return true;
                }
                finally
                {
                    try { if (Directory.Exists(tmpExt)) Directory.Delete(tmpExt, true); } catch { }
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>在解压目录中找插件根（含 package.json；优先 cordis/dsh 特征目录）。</summary>
        private string FindPluginRoot(string dir)
        {
            string best = null;
            FindPluginRootRec(dir, 0, ref best);
            return best;
        }

        private void FindPluginRootRec(string dir, int depth, ref string best)
        {
            if (best != null || depth > 4 || !Directory.Exists(dir)) return;
            try
            {
                // 优先检查当前目录本身（解压根即仓库根时直接命中，避免 example/ 等子目录误选）
                string pjHere = Path.Combine(dir, "package.json");
                if (File.Exists(pjHere))
                {
                    string text = File.ReadAllText(pjHere);
                    bool isDsh = text.IndexOf("cordis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 Regex.IsMatch(text, "\"name\"\\s*:\\s*\"[^\"]*dsh[^\"]*\"", RegexOptions.IgnoreCase);
                    if (isDsh) { best = dir; return; }
                    if (best == null) best = dir;
                }
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string pj = Path.Combine(sub, "package.json");
                    if (File.Exists(pj))
                    {
                        string text = File.ReadAllText(pj);
                        bool isDsh = text.IndexOf("cordis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     Regex.IsMatch(text, "\"name\"\\s*:\\s*\"[^\"]*dsh[^\"]*\"", RegexOptions.IgnoreCase);
                        if (isDsh) { best = sub; return; }
                        if (best == null) best = sub;
                    }
                    FindPluginRootRec(sub, depth + 1, ref best);
                    if (best != null) return;
                }
            }
            catch { }
        }

        private string FindPluginPkgName(string dir)
        {
            string pj = Path.Combine(dir, "package.json");
            if (!File.Exists(pj)) return null;
            try
            {
                var m = Regex.Match(File.ReadAllText(pj), "\"name\"\\s*:\\s*\"([^\"]+)\"");
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
            catch { return null; }
        }

        private bool IsSafePkgName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf("..") >= 0) return false;
            if (!Regex.IsMatch(name, "^@?[a-z0-9][a-z0-9-._]*(\\/[a-z0-9][a-z0-9-._]*)?$")) return false;
            foreach (string seg in name.Split('/'))
            {
                if (seg == "node_modules" || seg == "package.json" || seg.StartsWith(".")) return false;
            }
            return true;
        }

        /// <summary>启用：复制插件到 profiles/web/node_modules/&lt;pkgName&gt; + 登记 dependencies + bundles（先备份，失败自动回滚）。
        /// 含归属校验（P0-3）、同名冲突拒绝（P2-3）、依赖安装、DSH 预检与皮肤互斥（启用新皮肤自动停用旧皮肤）。</summary>
        private bool EnableBeautyPlugin(string repo, string pkgName, string localDir, out string error)
        {
            error = null;
            try
            {
                string profileDir = GetProfileDir();
                string pj = Path.Combine(profileDir, "package.json");
                if (!File.Exists(pj)) { error = "未找到 profile package.json: " + pj; return false; }
                string node = FindNode();
                if (string.IsNullOrEmpty(node)) { error = "未找到 node.exe"; return false; }
                // 0) 归属与冲突校验：同名包被其他仓库占用 / 与 DSH 内置包冲突 / 目标目录非本管理器安装 → 拒绝
                foreach (BeautyEntry e in beautyState.Values)
                    if (e != null && e.Repo != repo && e.PkgName == pkgName)
                    { error = "包名 " + pkgName + " 已被其他仓库占用（" + e.Repo + "），请先停用该插件"; return false; }
                string target = Path.Combine(profileDir, "node_modules", pkgName);
                string inBox = Path.Combine(GetDshHome(), "profiles", "node_modules", pkgName);
                if (Directory.Exists(inBox))
                { error = "包名与 DSH 内置插件冲突（" + pkgName + "），拒绝覆盖"; return false; }
                if (Directory.Exists(target) && !HasBeautyEntryForPkg(pkgName))
                { error = "目标目录已存在且非本管理器安装（" + target + "），拒绝覆盖"; return false; }
                // 0.5) 依赖安装：缓存目录缺依赖时用 npm 安装（构建产物缺失的仓库由此暴露）
                string depsResult = InstallPluginDeps(localDir, node);
                if (depsResult != "ok" && depsResult != "no-deps" && depsResult != "no-manifest" && depsResult != "no-npm")
                { error = "插件依赖安装失败: " + depsResult; return false; }
                // 1) 备份 profile 文件
                string bkDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh-beautify", "backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(bkDir);
                foreach (string f in new string[] { "package.json", "cordis.patch.yml", "cordis.yml" })
                {
                    string fp = Path.Combine(profileDir, f);
                    if (File.Exists(fp)) File.Copy(fp, Path.Combine(bkDir, f), true);
                }
                // 2) 复制插件目录（旧目录移到备份，不直接删除）
                if (Directory.Exists(target)) MoveDirToBackup(target, bkDir);
                string targetParent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetParent)) Directory.CreateDirectory(targetParent);
                DirectoryCopy(localDir, target);
                // 3) 登记依赖 + 追加 bundles（复刻 dsh plugin reconcile）
                int rc = RunNodeScript(node, SCRIPT_EDIT_PLUGIN, new string[] { pj, "add", pkgName, Path.Combine(localDir, "package.json") }, null, 20000);
                if (rc != 0)
                {
                    RollbackProfile(pj, bkDir, target);
                    error = "登记插件失败（node 退出码 " + rc + "），已回滚";
                    return false;
                }
                // 3.5) 皮肤互斥：启用新皮肤时自动停用其他皮肤（源码保留为已下载，可一键切回）
                if (IsSkinPackage(pkgName, localDir))
                {
                    foreach (BeautyEntry e in beautyState.Values)
                    {
                        if (e != null && e.Status == "enabled" && e.Repo != repo &&
                            !string.IsNullOrEmpty(e.PkgName) && IsSkinPackage(e.PkgName, e.LocalDir))
                        {
                            RunNodeScript(node, SCRIPT_EDIT_PLUGIN, new string[] { pj, "remove", e.PkgName, "" }, null, 20000);
                            e.Status = "downloaded";
                        }
                    }
                }
                // 4) DSH 预检：compose 失败则整体回滚（manifest 还原 + 删除新目录）
                if (!ProfilePrecheck(node))
                {
                    RollbackProfile(pj, bkDir, target);
                    error = "DSH 启动预检失败（插件与现有配置冲突），已自动回滚";
                    return false;
                }
                // 5) 状态
                BeautyEntry entry;
                if (beautyState.TryGetValue(repo, out entry) && entry != null)
                {
                    entry.PkgName = pkgName;
                    entry.LocalDir = localDir;
                    entry.Status = "enabled";
                }
                else
                {
                    entry = new BeautyEntry { Repo = repo, PkgName = pkgName, LocalDir = localDir, Status = "enabled" };
                    beautyState[repo] = entry;
                }
                SaveBeautyState();
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>停用：从 profile package.json 移除依赖 + bundles，插件目录移到备份（先备份 manifest）。</summary>
        private bool DisableBeautyPlugin(string pkgName, out string error)
        {
            error = null;
            try
            {
                string profileDir = GetProfileDir();
                string pj = Path.Combine(profileDir, "package.json");
                if (!File.Exists(pj)) { error = "未找到 profile package.json"; return false; }
                string node = FindNode();
                if (string.IsNullOrEmpty(node)) { error = "未找到 node.exe"; return false; }
                string bkDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh-beautify", "backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(bkDir);
                if (File.Exists(pj)) File.Copy(pj, Path.Combine(bkDir, "package.json"), true);
                int rc = RunNodeScript(node, SCRIPT_EDIT_PLUGIN, new string[] { pj, "remove", pkgName, "" }, null, 20000);
                if (rc != 0) { error = "移除插件失败（node 退出码 " + rc + "）"; return false; }
                string target = Path.Combine(profileDir, "node_modules", pkgName);
                if (Directory.Exists(target))
                {
                    if (HasBeautyEntryForPkg(pkgName)) MoveDirToBackup(target, bkDir);
                    else Log("[!] 停用：目录 " + target + " 无管理器安装记录，未删除（保留原样）");
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // ── 美化启用/停用的安全辅助 ──

        /// <summary>校验文件是否为有效 ZIP（魔数 PK\x03\x04）。</summary>
        private static bool IsZipFile(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length < 4) return false;
                    byte[] head = new byte[4];
                    fs.Read(head, 0, 4);
                    return head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;
                }
            }
            catch { return false; }
        }

        /// <summary>安全解压 ZIP：逐条校验路径，拒绝绝对路径、..、盘符与越界条目（Zip Slip 防护）。</summary>
        private static bool SafeExtractZip(string zipPath, string destDir, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(destDir);
                string fullDest = Path.GetFullPath(destDir).TrimEnd('\\', '/') + "\\";
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string name = (entry.FullName ?? "").Replace('\\', '/');
                        if (name.StartsWith("/") || name.IndexOf(':') >= 0 || name.Split('/').Contains(".."))
                        { error = "非法压缩包条目: " + entry.FullName; return false; }
                        string outPath = Path.GetFullPath(Path.Combine(destDir, name));
                        if (!outPath.StartsWith(fullDest, StringComparison.OrdinalIgnoreCase))
                        { error = "压缩包条目越界: " + entry.FullName; return false; }
                        if (name.EndsWith("/")) { Directory.CreateDirectory(outPath); continue; }
                        string parent = Path.GetDirectoryName(outPath);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        entry.ExtractToFile(outPath, true);
                    }
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>插件 package.json 是否声明了运行时依赖（dependencies 非空）。</summary>
        private static bool HasRuntimeDeps(string localDir)
        {
            try
            {
                string pj = Path.Combine(localDir, "package.json");
                if (!File.Exists(pj)) return false;
                var m = Regex.Match(File.ReadAllText(pj), "\"dependencies\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Singleline);
                if (!m.Success) return false;
                return m.Groups[1].Value.Trim().Length > 0;
            }
            catch { return false; }
        }

        /// <summary>在插件缓存目录安装运行时依赖（npm install --production）。返回 ok / no-deps / no-manifest / no-npm / 错误信息。</summary>
        private string InstallPluginDeps(string localDir, string nodePath)
        {
            try
            {
                string pj = Path.Combine(localDir, "package.json");
                if (!File.Exists(pj)) return "no-manifest";
                if (!HasRuntimeDeps(localDir)) return "no-deps";
                if (Directory.Exists(Path.Combine(localDir, "node_modules"))) return "ok";
                string npm = Path.GetDirectoryName(nodePath) + "\\npm.cmd";
                if (!File.Exists(npm)) return "no-npm";
                string r = StartProcCapture(npm, "install --production --no-audit --no-fund --loglevel=error", localDir, 240000);
                if (r == null) return "npm 安装超时或启动失败";
                if (r.IndexOf("npm ERR", StringComparison.OrdinalIgnoreCase) >= 0) return "npm 报告错误";
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>DSH profile 启动预检：`dsh --profile web --dump-config` 必须成功退出（exit 0）。
        /// 能捕获 bundle 目录缺失/无法解析等硬错误（这些正是"下次启动 fail-loud"的来源）。</summary>
        private bool ProfilePrecheck(string nodePath)
        {
            try
            {
                string bin = Path.Combine(installDir, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
                if (!File.Exists(bin)) return true; // 无法定位 dsh 时不做预检（不阻塞启用）
                Process p = new Process();
                p.StartInfo.FileName = nodePath;
                p.StartInfo.Arguments = "\"" + bin + "\" --profile web --dump-config";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                string all = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } return true; }
                if (p.ExitCode != 0) return false;
                if (all.IndexOf("cannot resolve", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                return true;
            }
            catch { return true; }
        }

        /// <summary>回滚：从备份还原 profile 文件 + 删除新复制的插件目录。</summary>
        private static void RollbackProfile(string pj, string bkDir, string target)
        {
            try
            {
                string profileDir = Path.GetDirectoryName(pj);
                foreach (string f in new string[] { "package.json", "cordis.patch.yml", "cordis.yml" })
                {
                    string b = Path.Combine(bkDir, f);
                    string d = Path.Combine(profileDir, f);
                    if (File.Exists(b)) File.Copy(b, d, true);
                }
            }
            catch { }
            try { if (Directory.Exists(target)) Directory.Delete(target, true); } catch { }
        }

        /// <summary>把目标插件目录移到备份目录（替代直接删除，保证可恢复）。</summary>
        private static void MoveDirToBackup(string target, string bkDir)
        {
            try
            {
                string dest = Path.Combine(bkDir, "node_modules", new DirectoryInfo(target).Name);
                string parent = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.Move(target, dest);
            }
            catch { }
        }

        /// <summary>是否存在本管理器登记的条目占用该包名。</summary>
        private bool HasBeautyEntryForPkg(string pkgName)
        {
            foreach (BeautyEntry e in beautyState.Values)
                if (e != null && e.PkgName == pkgName) return true;
            return false;
        }

        /// <summary>判断是否为互斥皮肤包（skin.json 或 patch 含 ui-skin 特征）。</summary>
        private static bool IsSkinPackage(string pkgName, string localDir)
        {
            if (string.IsNullOrEmpty(pkgName)) return false;
            if (!string.IsNullOrEmpty(localDir) && File.Exists(Path.Combine(localDir, "skin.json"))) return true;
            if (!string.IsNullOrEmpty(localDir))
            {
                string patch = Path.Combine(localDir, "cordis.patch.yml");
                if (File.Exists(patch))
                    try { if (File.ReadAllText(patch).IndexOf("ui-skin", StringComparison.OrdinalIgnoreCase) >= 0) return true; } catch { }
            }
            return false;
        }

        /// <summary>按 PID 读取进程命令行（System.Management）。</summary>
        private static string GetProcessCommandLine(int pid)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + pid))
                {
                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        object v = mo["CommandLine"];
                        if (v != null) return v.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>把本地状态与真实 profile 对账：enabled 必须同时满足 bundles 含包名且目录存在，否则降级为已下载；
        /// downloaded 但本地缓存目录已不存在的条目移除。</summary>
        private void ReconcileBeautyState()
        {
            try
            {
                string profileDir = GetProfileDir();
                string pj = Path.Combine(profileDir, "package.json");
                var bundles = new HashSet<string>();
                if (File.Exists(pj))
                {
                    string text = File.ReadAllText(pj);
                    var m = Regex.Match(text, "\"bundles\"\\s*:\\s*\\[([^\\]]*)\\]", RegexOptions.Singleline);
                    if (m.Success)
                        foreach (Match mm in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                            bundles.Add(mm.Groups[1].Value);
                }
                bool changed = false;
                var toRemove = new List<string>();
                foreach (var kv in beautyState)
                {
                    BeautyEntry e = kv.Value;
                    if (e == null) continue;
                    if (e.Status == "enabled")
                    {
                        bool ok = !string.IsNullOrEmpty(e.PkgName) && bundles.Contains(e.PkgName) &&
                                  Directory.Exists(Path.Combine(profileDir, "node_modules", e.PkgName));
                        if (!ok) { e.Status = "downloaded"; changed = true; }
                    }
                    if (e.Status == "downloaded" && string.IsNullOrEmpty(e.LocalDir)) { toRemove.Add(kv.Key); changed = true; }
                }
                foreach (string k in toRemove) { BeautyEntry tmp; beautyState.TryRemove(k, out tmp); }
                if (changed) SaveBeautyState();
            }
            catch { }
        }

        private void UpdateAboutVersion()
        {
            if (lblAboutVersion == null) return;
            string ver = GetLocalVersion();
            lblAboutVersion.Text = "已安装版本: " + (string.IsNullOrEmpty(ver) ? "未安装" : ver) + "（基于 @deepseek-ai/dsh）";
        }

        private string GetDshHome()
        {
            if (!string.IsNullOrWhiteSpace(dshHome) && Directory.Exists(dshHome)) return dshHome;
            string h = Environment.GetEnvironmentVariable("DSH_HOME");
            if (!string.IsNullOrEmpty(h) && Directory.Exists(h)) return h;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
        }

        private string GetProfileDir()
        {
            return Path.Combine(GetDshHome(), "profiles", "web");
        }

        private static void DirectoryCopy(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(src))
                DirectoryCopy(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        private async void BtnPluginDownload_Click(object sender, EventArgs e)
        {
            if (pluginList.SelectedItems.Count == 0) return;
            PluginInfo sel = pluginList.SelectedItems[0].Tag as PluginInfo;
            if (sel == null) return;
            using (var dlg = new SaveFileDialog { FileName = sel.Name + ".zip", Filter = "ZIP 压缩包 (*.zip)|*.zip" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                string dest = dlg.FileName;
                btnPluginDownloadZip.Enabled = false;
                lblPluginStatus.Text = "正在下载 " + sel.FullName + " ...";
                bool ok = false;
                string err = null;
                try { ok = await Task.Run(() => DownloadPluginZip(sel, dest, out err)); }
                catch (Exception ex) { err = ex.Message; }
                btnPluginDownloadZip.Enabled = pluginList.SelectedItems.Count > 0;
                if (ok)
                {
                    lblPluginStatus.Text = "已保存: " + dest;
                    try { Process.Start("explorer", "/select,\"" + dest + "\""); } catch { }
                }
                else
                {
                    lblPluginStatus.Text = "下载失败: " + (err ?? "未知错误");
                }
            }
        }

        /// <summary>下载插件 ZIP：配置了服务器走服务器代理；否则 node 直连/走本地代理。</summary>
        private bool DownloadPluginZip(PluginInfo p, string dest, out string error)
        {
            error = null;
            string node = FindNode();
            string server = GetServerUrl();
            if (!string.IsNullOrEmpty(server))
            {
                string url = server + "/api/plugins/zip?repo=" + p.FullName;
                try { WebDownloadFile(url, dest, 120); if (IsZipFile(dest)) return true; } catch { }
                if (node != null)
                {
                    if (RunNodeScript(node, SCRIPT_DOWNLOAD_FILE, new string[] { dest, url, "" }, null, 120000) == 0 && IsZipFile(dest))
                        return true;
                    foreach (string proxy in DetectProxyCandidates())
                    {
                        if (RunNodeScript(node, SCRIPT_DOWNLOAD_FILE, new string[] { dest, url, proxy }, proxy, 120000) == 0 && IsZipFile(dest))
                            return true;
                    }
                }
                error = "无法从服务器下载有效 ZIP（" + server + "）";
                return false;
            }
            if (node != null)
            {
                if (RunNodeScript(node, SCRIPT_DOWNLOAD_ZIP, new string[] { p.FullName, dest, "" }, null, 150000) == 0 && IsZipFile(dest))
                    return true;
                foreach (string proxy in DetectProxyCandidates())
                {
                    if (RunNodeScript(node, SCRIPT_DOWNLOAD_ZIP, new string[] { p.FullName, dest, proxy }, proxy, 120000) == 0 && IsZipFile(dest))
                        return true;
                }
            }
            error = "无法从 GitHub 下载（可能需要 VPN，或在上方配置插件服务器）";
            return false;
        }

        /// <summary>简单 semver 比较：a &gt; b 返回正数，a &lt; b 返回负数，相等返回 0。预发布版本低于正式版。</summary>
        private static int CompareVersions(string a, string b)
        {
            Match ma = Regex.Match(a ?? "", @"^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$");
            Match mb = Regex.Match(b ?? "", @"^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$");
            if (!ma.Success || !mb.Success) return string.CompareOrdinal(a ?? "", b ?? "");
            for (int i = 1; i <= 3; i++)
            {
                int na, nb;
                int.TryParse(ma.Groups[i].Value, out na);
                int.TryParse(mb.Groups[i].Value, out nb);
                if (na != nb) return na.CompareTo(nb);
            }
            bool pa = ma.Groups[4].Success, pb = mb.Groups[4].Success;
            if (pa != pb) return pa ? -1 : 1; // 正式版 > 预发布版
            if (!pa) return 0;
            // 预发布段按点分段比较：数字段按数值（避免 rc.9 > rc.10 的字符串误判），其余按字典序
            string[] sa = ma.Groups[4].Value.Split('.');
            string[] sb = mb.Groups[4].Value.Split('.');
            int n = Math.Min(sa.Length, sb.Length);
            for (int i = 0; i < n; i++)
            {
                int ia, ib;
                bool na = int.TryParse(sa[i], out ia);
                bool nb = int.TryParse(sb[i], out ib);
                if (na && nb) { if (ia != ib) return ia.CompareTo(ib); }
                else { int c = string.CompareOrdinal(sa[i], sb[i]); if (c != 0) return c; }
            }
            return sa.Length.CompareTo(sb.Length);
        }

        // ── 辅助方法 ──
        private string FindNode()
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string p in pathEnv.Split(';'))
            {
                string entry = p.Trim();
                if (entry.Length == 0) continue;
                string f = entry + "\\node.exe";
                if (File.Exists(f)) return f;
            }
            string[] dirs = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\nodejs",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\nodejs",
                Environment.ExpandEnvironmentVariables("%APPDATA%\\npm"),
                Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%\\fnm\\current")
            };
            foreach (string d in dirs)
            {
                if (d != null && Directory.Exists(d))
                {
                    string[] files = Directory.GetFiles(d, "node.exe");
                    if (files.Length > 0) return files[0];
                }
            }
            return null;
        }

        private string GetNodeVersion(string nodePath)
        {
            string r = StartProcCapture(nodePath, "--version", null, 15000);
            return string.IsNullOrEmpty(r) ? "unknown" : r.Trim();
        }

        private string GetNpmVersion(string nodePath)
        {
            string npm = Path.GetDirectoryName(nodePath) + "\\npm.cmd";
            string r = StartProcCapture(npm, "--version", null, 30000);
            return string.IsNullOrEmpty(r) ? "unknown" : r.Trim();
        }

        private string GetLocalVersion()
        {
            if (string.IsNullOrEmpty(installDir)) return null;
            string pkg = installDir + "\\node_modules\\@deepseek-ai\\dsh\\package.json";
            if (!File.Exists(pkg)) return null;
            try { var m = Regex.Match(File.ReadAllText(pkg), "\"version\"\\s*:\\s*\"([^\"]+)\""); return m.Success ? m.Groups[1].Value : null; } catch { return null; }
        }

        private string RunNpmView(string nodePath, string wd)
        {
            string npm = Path.GetDirectoryName(nodePath) + "\\npm.cmd";
            string r = StartProcCapture(npm, "view @deepseek-ai/dsh version", wd, 60000);
            return string.IsNullOrEmpty(r) ? null : r.Trim();
        }

        private int RunNpmInstall(string nodePath, string wd)
        {
            string npm = Path.GetDirectoryName(nodePath) + "\\npm.cmd";
            try
            {
                // 显式安装 @latest 并 --save：绕过 package-lock.json 对旧版本的锁定（否则 npm install 永远装 lock 里的旧版，
                // 造成"每次启动都检测到新版本、更新成功但版本不变"的循环）
                Process p = new Process();
                p.StartInfo.FileName = npm;
                p.StartInfo.Arguments = "install @deepseek-ai/dsh@latest --save --no-audit --no-fund --loglevel=notice";
                p.StartInfo.WorkingDirectory = wd;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("[npm] " + e.Data); };
                p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("[npm] " + e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                // 心跳提示：npm 下载大包时输出很少，用户容易以为卡死。每 15 秒报一次已用时间。
                int totalMs = 600000; // 10 分钟超时
                int waited = 0;
                int heartbeat = 15000;
                while (waited < totalMs)
                {
                    if (p.WaitForExit(heartbeat))
                        break;
                    waited += heartbeat;
                    try
                    {
                        if (!p.HasExited)
                        {
                            int secs = waited / 1000;
                            Log("[npm] 安装进行中... 已用时 " + secs + " 秒（正在下载依赖，请耐心等待）");
                        }
                    }
                    catch { }
                    Application.DoEvents();
                }
                if (!p.HasExited) // 超时
                {
                    try { Process k = StartProc("taskkill", "/PID " + p.Id + " /T /F"); k.WaitForExit(3000); } catch { }
                    try { p.Kill(); } catch { }
                    return -1;
                }
                return p.ExitCode;
            }
            catch { return -1; }
        }

        /// <summary>node 脚本：下载二进制文件到本地（argv[2]=URL, argv[3]=输出文件, argv[4]=代理可空）。原生 https + CONNECT 隧道。</summary>
        private const string SCRIPT_DOWNLOAD_FILE = @"
const fs=require('fs'),http=require('http'),https=require('https'),tls=require('tls');
const u=process.argv[2], out=process.argv[3], proxy=process.argv[4]||'';
function getBuf(u0,redir){
 return new Promise(function(resolve,reject){
  const url=new URL(u0);
  let done=false;
  const finish=function(err,buf){if(done)return;done=true;err?reject(err):resolve(buf);};
  const handleRes=function(res2){
   if(res2.statusCode>=300&&res2.statusCode<400&&res2.headers.location){
    res2.resume();
    if((redir||0)>=5)return finish(new Error('too many redirects'));
    getBuf(new URL(res2.headers.location,u0).toString(),(redir||0)+1).then(function(b){finish(null,b);},function(e){finish(e);});
    return;
   }
   if(res2.statusCode!==200){res2.resume();return finish(new Error('HTTP '+res2.statusCode));}
   const chunks=[];res2.on('data',function(c){chunks.push(c);});
   res2.on('end',function(){finish(null,Buffer.concat(chunks));});
   res2.on('error',function(e){finish(e);});
  };
  const doReq=function(opts){const r=https.request(opts,handleRes);r.on('error',function(e){finish(e);});r.end();};
  if(proxy){
   const p=new URL(proxy.indexOf('://')===-1?'http://'+proxy:proxy);
   const creq=http.request({host:p.hostname,port:p.port||80,method:'CONNECT',path:url.host+':443',headers:{Host:url.host}});
   creq.on('connect',function(res,socket){
    if(res.statusCode!==200){socket.destroy();return finish(new Error('proxy CONNECT '+res.statusCode));}
    const ts=tls.connect({socket:socket,servername:url.hostname},function(){
     doReq({createConnection:function(){return ts;},hostname:url.hostname,path:url.pathname+url.search,method:'GET',headers:Object.assign({Host:url.hostname,'User-Agent':'DSH-Manager/1.0'})});
    });
    ts.on('error',function(e){finish(e);});
   });
   creq.on('error',function(e){finish(e);});
   creq.end();
  } else {
   doReq({hostname:url.hostname,port:443,path:url.pathname+url.search,method:'GET',headers:Object.assign({Host:url.hostname,'User-Agent':'DSH-Manager/1.0'})});
  }
 });
}
getBuf(u).then(function(b){fs.writeFileSync(out,b);}).catch(function(e){console.error('DLERR '+e.message);process.exit(1);});
";

        private bool InstallNodeJS()
        {
            Log("[.] 尝试使用 winget...");
            try { Process p = StartProc("winget", "install OpenJS.NodeJS.LTS --silent --accept-package-agreements"); p.WaitForExit(60000); if (p.ExitCode == 0) return true; } catch { }

            Log("[.] winget 不可用，正在下载最新 LTS 版 Node.js...");
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if (arch == "ARM64") arch = "arm64"; else arch = "x64";
            string version = GetLatestLtsNodeVersion(); // 查询失败时回退到已知版本
            string url = "https://nodejs.org/dist/" + version + "/node-" + version + "-" + arch + ".msi";
            string msiPath = Path.GetTempPath() + "node-install.msi";

            Log("[.] 正在下载 Node.js " + version + " ...");
            bool downloaded = false;
            // 1) WebClient（schannel 正常的机器）
            try { WebDownloadFile(url, msiPath, 180); downloaded = new FileInfo(msiPath).Length > 0; } catch { }
            // 2) node 直连 / 走本地代理（本机 schannel 损坏时可用）
            if (!downloaded)
            {
                string nodePath = FindNode();
                if (nodePath != null)
                {
                    List<string> candidates = new List<string>();
                    candidates.Add("");
                    candidates.AddRange(DetectProxyCandidates());
                    foreach (string pr in candidates)
                    {
                        if (RunNodeScript(nodePath, SCRIPT_DOWNLOAD_FILE, new string[] { url, msiPath, pr }, pr, 180000) == 0 &&
                            new FileInfo(msiPath).Length > 0) { downloaded = true; break; }
                    }
                }
            }
            // 3) powershell 兜底
            if (!downloaded)
            {
                try
                {
                    Process p = StartProc("powershell", "-Command \"try { $wc = New-Object System.Net.WebClient; $wc.DownloadFile('" + url + "', '" + msiPath + "'); Write-Host 'OK' } catch { exit 1 }\"");
                    p.WaitForExit(180000);
                    if (p.ExitCode == 0 && new FileInfo(msiPath).Length > 0) downloaded = true;
                }
                catch { }
            }
            if (!downloaded) return false;

            Log("[.] 正在静默安装 Node.js...");
            try { Process p = StartProc("msiexec", "/i \"" + msiPath + "\" /qn ADDLOCAL=ALL /norestart"); p.WaitForExit(120000); return p.ExitCode == 0; } catch { return false; }
        }

        /// <summary>从 https://nodejs.org/dist/index.json 解析最新 LTS 版本号；失败回退到已知版本。</summary>
        private string GetLatestLtsNodeVersion()
        {
            // 1) WebClient
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "DSH-Manager/1.0";
                    string json = wc.DownloadString("https://nodejs.org/dist/index.json");
                    string v = ParseLtsFromJson(json);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch { }
            // 2) node 兜底
            string nodePath = FindNode();
            if (nodePath != null)
            {
                string outFile = Path.GetTempFileName();
                try
                {
                    if (RunNodeScript(nodePath, SCRIPT_FETCH_TEXT, new string[] { outFile, "https://nodejs.org/dist/index.json" }, null, 60000) == 0)
                    {
                        string v = ParseLtsFromJson(File.ReadAllText(outFile));
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }
                catch { }
                finally { try { File.Delete(outFile); } catch { } }
            }
            return "v22.14.0";
        }

        private static string ParseLtsFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            MatchCollection ltsMatches = Regex.Matches(json, "\"lts\":\"[A-Za-z]+\"");
            foreach (Match lm in ltsMatches)
            {
                string before = json.Substring(0, lm.Index);
                int lastVer = before.LastIndexOf("\"version\":\"", StringComparison.Ordinal);
                if (lastVer >= 0)
                {
                    string v = before.Substring(lastVer + 11);
                    int q = v.IndexOf('"');
                    if (q > 0) return v.Substring(0, q);
                }
            }
            return null;
        }

        private void CreateShortcut()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = desktop + "\\DeepSeek Harness.lnk";
            string exePath = typeof(DshManagerForm).Assembly.Location;
            try
            {
                string ps = "$ws=New-Object -ComObject WScript.Shell;$s=$ws.CreateShortcut('" + shortcutPath.Replace("'", "''") + "');$s.TargetPath='" + exePath.Replace("'", "''") + "';$s.Arguments='--launch';$s.WorkingDirectory='" + installDir.Replace("'", "''") + "';$s.Description='DeepSeek Harness Web UI';$s.Save()";
                Process p = StartProc("powershell", "-Command \"" + ps.Replace("\"", "\\\"") + "\"");
                p.WaitForExit(10000);
            }
            catch { }
        }

        private string SettingsPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "DeepSeek-Harness-Manager");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.txt");
            }
        }

        private void LoadSettings()
        {
            installDir = null;
            dshHome = null;
            pluginServer = "";
            gptCompatFixEnabled = false;
            reasoningControlEnabled = false;
            try
            {
                string path = SettingsPath;
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        string t = line.Trim();
                        if (t.Length == 0) continue;
                        int eq = t.IndexOf('=');
                        if (eq > 0)
                        {
                            string key = t.Substring(0, eq).Trim();
                            string val = t.Substring(eq + 1).Trim();
                            if (key == "installDir") installDir = val;
                            else if (key == "dshHome") dshHome = val;
                            else if (key == "pluginServer") pluginServer = val;
                            else if (key == "gptCompatFixEnabled") gptCompatFixEnabled = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                            else if (key == "reasoningControlEnabled") reasoningControlEnabled = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (string.IsNullOrEmpty(installDir))
                        {
                            // 旧版单行格式：整行即安装目录
                            installDir = t;
                        }
                    }
                }
            }
            catch { }
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) installDir = null;
            if (string.IsNullOrEmpty(dshHome) || !Directory.Exists(dshHome)) dshHome = null;
        }

        private bool SaveSettings()
        {
            try
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(installDir)) sb.AppendLine("installDir=" + installDir);
                if (!string.IsNullOrEmpty(dshHome)) sb.AppendLine("dshHome=" + dshHome);
                if (!string.IsNullOrEmpty(pluginServer)) sb.AppendLine("pluginServer=" + pluginServer);
                sb.AppendLine("gptCompatFixEnabled=" + (gptCompatFixEnabled ? "true" : "false"));
                sb.AppendLine("reasoningControlEnabled=" + (reasoningControlEnabled ? "true" : "false"));
                string settingsPath = SettingsPath;
                string temp = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temp, sb.ToString(), Encoding.UTF8);
                    if (File.Exists(settingsPath)) File.Replace(temp, settingsPath, null);
                    else File.Move(temp, settingsPath);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                return true;
            }
            catch (Exception ex) { Log("[设置] 保存失败: " + ex.Message); return false; }
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("127.0.0.1", port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        private Process StartProc(string file, string args, string wd = null)
        {
            Process p = new Process();
            p.StartInfo.FileName = file;
            p.StartInfo.Arguments = args;
            if (wd != null) p.StartInfo.WorkingDirectory = wd;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            return p;
        }

        /// <summary>启动进程并完整捕获 stdout+stderr（异步读取，避免管道阻塞）。失败/超时返回 null。</summary>
        private static string StartProcCapture(string file, string args, string wd, int timeoutMs)
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = file;
                p.StartInfo.Arguments = args;
                if (wd != null) p.StartInfo.WorkingDirectory = wd;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                StringBuilder sb = new StringBuilder();
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try
                    {
                        Process k = new Process();
                        k.StartInfo.FileName = "taskkill";
                        k.StartInfo.Arguments = "/PID " + p.Id + " /T /F";
                        k.StartInfo.UseShellExecute = false;
                        k.StartInfo.CreateNoWindow = true;
                        k.Start();
                        k.WaitForExit(3000);
                    }
                    catch { }
                    try { p.Kill(); } catch { }
                    return null;
                }
                return sb.ToString();
            }
            catch { return null; }
        }

        private static void BringExistingToFront()
        {
            try
            {
                string name = Process.GetCurrentProcess().ProcessName;
                foreach (Process p in Process.GetProcessesByName(name))
                {
                    if (p.Id == Process.GetCurrentProcess().Id) continue;
                    IntPtr h = p.MainWindowHandle;
                    if (h != IntPtr.Zero)
                    {
                        SetForegroundWindow(h);
                        return;
                    }
                }
            }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool launch = args != null && Array.IndexOf(args, "--launch") >= 0;

            bool createdNew;
            using (var mutex = new Mutex(true, "DSH-Manager-SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // 已有实例：激活既有窗口后退出
                    BringExistingToFront();
                    return;
                }
                Application.Run(new DshManagerForm(launch));
                GC.KeepAlive(mutex);
            }
        }
    }
}
