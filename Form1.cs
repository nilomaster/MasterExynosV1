using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MasterUnlock
{
    public partial class MainForm : Form
    {
        // ============================================================
        // CONSTANTS & PALETTE (CYBER TECH / MASTER UNLOCK)
        // ============================================================
        public static string namesoftware = "Master Unlock";
        public static string version = "1.0";

        public static readonly Color ColorBgApp = Color.FromArgb(7, 12, 22);          // #070C16
        public static readonly Color ColorBgSidebar = Color.FromArgb(10, 18, 32);      // #0A1220
        public static readonly Color ColorBgHeader = Color.FromArgb(9, 16, 28);        // #09101C
        public static readonly Color ColorBgCard = Color.FromArgb(13, 25, 44);        // #0D192C
        public static readonly Color ColorBgCardHover = Color.FromArgb(18, 35, 60);   // #12233C
        public static readonly Color ColorBgInput = Color.FromArgb(11, 23, 40);       // #0B1728
        public static readonly Color ColorBgTerminal = Color.FromArgb(4, 8, 15);       // #04080F
        public static readonly Color ColorBorder = Color.FromArgb(0, 140, 255);       // Electric Blue #008CFF
        public static readonly Color ColorBorderDark = Color.FromArgb(22, 48, 80);    // #163050
        public static readonly Color ColorCyanGlow = Color.FromArgb(0, 210, 255);     // Neon Cyan #00D2FF
        public static readonly Color ColorTextWhite = Color.FromArgb(242, 247, 255);  // #F2F7FF
        public static readonly Color ColorTextMuted = Color.FromArgb(145, 172, 204);  // #91ACCC
        public static readonly Color ColorSuccess = Color.FromArgb(0, 230, 92);       // Neon Green #00E65C
        public static readonly Color ColorWarning = Color.FromArgb(255, 196, 0);      // Warning #FFC400
        public static readonly Color ColorDanger = Color.FromArgb(255, 52, 64);       // Red #FF3440

        // ============================================================
        // P/INVOKE FOR BORDERLESS WINDOW DRAGGING
        // ============================================================
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // ============================================================
        // UI CONTAINERS & VIEWS
        // ============================================================
        private Panel _sidebarPanel = null!;
        private Panel _topHeaderPanel = null!;
        private Label _lblHeaderBreadcrumb = null!;
        private CyberConnectionBadge _badgeConnection = null!;
        private Label _lblSidebarLivePort = null!;
        private Panel _mainContentContainer = null!;

        // 4 Dedicated Views
        private Panel _viewHome = null!;
        private Panel _viewReadInfo = null!;
        private Panel _viewFrpExynos = null!;
        private Panel _viewAbout = null!;

        // Sidebar Navigation Buttons
        private List<CyberNavButton> _navButtons = new List<CyberNavButton>();
        private CyberNavButton _btnNavHome = null!;
        private CyberNavButton _btnNavInfo = null!;
        private CyberNavButton _btnNavFrpExynos = null!;
        private CyberNavButton _btnNavAbout = null!;

        // View 1: Home Controls
        private Label _lblHomePortCount = null!;
        private Label _lblHomeMtpStatus = null!;
        private Label _lblHomeEngineStatus = null!;

        // View 2: Read Info Controls
        private string _selectedBrand = "Samsung";
        private List<CyberBrandTabButton> _brandButtons = new List<CyberBrandTabButton>();
        private FlowLayoutPanel _pnlMtpToolbar = null!;
        private Label _lblLogT = null!;
        private CyberButton _btnMtpRead = null!;
        private CyberButton _btnXiaomiFastboot = null!;
        private CyberButton _btnXiaomiSideload = null!;
        private CyberButton _btnInstallDrivers = null!;
        private CyberButton _btnMtpExport = null!;
        private CyberButton _btnMtpClear = null!;
        private RichTextBox _txtMtpLog = null!;

        // View 3: FRP Samsung Exynos Controls
        private ComboBox comboPorts = null!;
        private ComboBox cmbConfig = null!;
        private CyberButton _btnRefreshPorts = null!;
        private CyberButton _btnSaveConfig = null!;
        private CyberButton _btnFlash = null!;
        private CyberButton buttonStop = null!;
        private Label _lblDeviceCommercial = null!;
        private Label _lblDeviceChipset = null!;
        private Label _lblDeviceCompatibility = null!;
        private RichTextBox txtLog = null!;
        private CyberButton _btnClearLog = null!;
        private CyberButton _btnSaveLog = null!;
        private CyberProgressBar ProgressBar = null!;
        private Label _lblStatus = null!;
        private Label _lblTimer = null!;

        // Timers for real-time operations and auto USB/port detection
        private System.Windows.Forms.Timer _uiTimer = null!;
        private System.Windows.Forms.Timer _hotplugDebounceTimer = null!;
        private System.Windows.Forms.Timer _portWatcherTimer = null!;

        private List<ComPortInfo> _lastDetectedPorts = new List<ComPortInfo>();
        private string _lastConnectedPortName = "";

        // ============================================================
        // ENGINE & STATE
        // ============================================================
        private readonly ExynosFlashEngine _engine;
        private SamsungMtpDeviceInfo? _lastMtpInfo;
        public static bool busyState = false;
        public Stopwatch Watch = new Stopwatch();
        public CancellationTokenSource stop = new CancellationTokenSource();
        private readonly object _stopLock = new object();

        // Remote Config
        private const string ConfigServerUrl = "https://your.site/presets/";
        private static readonly HttpClient _httpClient = new HttpClient
        { Timeout = TimeSpan.FromSeconds(15) };

        private static readonly string[] ConfigNames =
        {
            "exynos850_dpolicy_extract",
            "exynos7884_dpolicy_extract",
            "exynos7885_dpolicy_extract",
            "exynos9610_dpolicy_integrity",
            "exynos9611_dpolicy_integrity",
            "exynos9820_dpolicy_integrity",
            "exynos9825_dpolicy_integrity",
            "exynos990_dpolicy_integrity",
            "exynos1280_dpolicy_extract",
            "exynos1380_dpolicy_extract",
            "exynos1480_dpolicy_extract",
            "exynos1580_dpolicy_extract",
            "exynos2100_dpolicy_extract",
            "exynos2200_dpolicy_extract",
            "exynos2400_dpolicy_extract",
        };
        private bool _configLoaded = false;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();

            _engine = new ExynosFlashEngine();
            _engine.OnLogReceived += OnEngineLog;
            _engine.OnProgressChanged += OnProgress;

            InitializeComponent();
            BuildMasterUnlockInterface();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _uiTimer.Tick += (s, e) =>
            {
                if (busyState)
                {
                    TimeSpan ts = Watch.Elapsed;
                    _lblTimer.Text = string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
                }
            };
            _uiTimer.Start();

            // USB Hotplug Debounce Timer (400ms after WM_DEVICECHANGE)
            _hotplugDebounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _hotplugDebounceTimer.Tick += (s, e) =>
            {
                _hotplugDebounceTimer.Stop();
                PerformAutoPortDetection(isHotplugEvent: true);
            };

            // Background Port Watcher Timer (1.5s interval)
            _portWatcherTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _portWatcherTimer.Tick += (s, e) => PerformAutoPortDetection(isHotplugEvent: false);
            _portWatcherTimer.Start();

            RefreshAll();
            PerformAutoPortDetection(isHotplugEvent: false);
            SwitchToView("Home");
        }

        // ============================================================
        // BORDERLESS WINDOW DRAGGING & CORNER RESIZE & USB HOTPLUG
        // ============================================================
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVICEARRIVAL = 0x8000;
            const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
            const int DBT_DEVNODES_CHANGED = 0x0007;

            if (m.Msg == WM_DEVICECHANGE)
            {
                int wp = m.WParam.ToInt32();
                if (wp == DBT_DEVICEARRIVAL || wp == DBT_DEVICEREMOVECOMPLETE || wp == DBT_DEVNODES_CHANGED)
                {
                    _hotplugDebounceTimer?.Stop();
                    _hotplugDebounceTimer?.Start();
                }
            }

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == HTCLIENT)
                {
                    Point cursor = PointToClient(Cursor.Position);
                    int resizeBorder = 8;
                    if (cursor.X <= resizeBorder && cursor.Y <= resizeBorder) m.Result = (IntPtr)HTTOPLEFT;
                    else if (cursor.X >= ClientSize.Width - resizeBorder && cursor.Y <= resizeBorder) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (cursor.X <= resizeBorder && cursor.Y >= ClientSize.Height - resizeBorder) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (cursor.X >= ClientSize.Width - resizeBorder && cursor.Y >= ClientSize.Height - resizeBorder) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (cursor.X <= resizeBorder) m.Result = (IntPtr)HTLEFT;
                    else if (cursor.X >= ClientSize.Width - resizeBorder) m.Result = (IntPtr)HTRIGHT;
                    else if (cursor.Y <= resizeBorder) m.Result = (IntPtr)HTTOP;
                    else if (cursor.Y >= ClientSize.Height - resizeBorder) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var penBorder = new Pen(ColorBorderDark, 1);
            e.Graphics.DrawRectangle(penBorder, 0, 0, Width - 1, Height - 1);

            using var penAccent = new Pen(ColorCyanGlow, 1.5f);
            int cl = 20;
            e.Graphics.DrawLine(penAccent, 0, 0, cl, 0);
            e.Graphics.DrawLine(penAccent, 0, 0, 0, cl);
            e.Graphics.DrawLine(penAccent, Width - 1, 0, Width - 1 - cl, 0);
            e.Graphics.DrawLine(penAccent, Width - 1, 0, Width - 1, cl);
            e.Graphics.DrawLine(penAccent, 0, Height - 1, cl, Height - 1);
            e.Graphics.DrawLine(penAccent, 0, Height - 1, 0, Height - 1 - cl);
            e.Graphics.DrawLine(penAccent, Width - 1, Height - 1, Width - 1 - cl, Height - 1);
            e.Graphics.DrawLine(penAccent, Width - 1, Height - 1, Width - 1, Height - 1 - cl);
        }

        // ============================================================
        // MASTER UNLOCK INTERFACE BUILDER
        // ============================================================
        private void BuildMasterUnlockInterface()
        {
            BackColor = ColorBgApp;
            ForeColor = ColorTextWhite;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // 1. Sidebar (Fixed Left - Width 230)
            BuildSidebar();

            // 2. Right Host Panel (Top Header + Multi-View Area)
            var pnlRightHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(0)
            };
            Controls.Add(pnlRightHost);

            // Ensure Left sidebar takes layout space first in WinForms
            _sidebarPanel.SendToBack();
            pnlRightHost.BringToFront();

            // 3. Top Header Bar (Breadcrumb Title + Min/Max/Close)
            BuildTopHeader(pnlRightHost);

            // 4. Main Multi-View Content Container
            _mainContentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(14, 10, 14, 12)
            };
            pnlRightHost.Controls.Add(_mainContentContainer);
            _topHeaderPanel.SendToBack();
            _mainContentContainer.BringToFront();

            // 5. Initialize All 4 Dedicated Views
            InitViewHome();
            InitViewReadInfo();
            InitViewFrpExynos();
            InitViewAbout();
        }

        // ============================================================
        // 1. SIDEBAR BUILDER
        // ============================================================
        private void BuildSidebar()
        {
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 230,
                BackColor = ColorBgSidebar,
                Padding = new Padding(0)
            };
            _sidebarPanel.MouseDown += Header_MouseDown;
            Controls.Add(_sidebarPanel);

            // Logo Header (Explicit Position: 12, 10, Width 206, Height 75)
            var pnlLogo = new Panel
            {
                Location = new Point(12, 10),
                Size = new Size(206, 75),
                BackColor = ColorBgSidebar
            };
            pnlLogo.MouseDown += Header_MouseDown;

            var picLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorBgSidebar
            };
            var logoImg = LoadAppLogo();
            if (logoImg != null) picLogo.Image = logoImg;
            picLogo.MouseDown += Header_MouseDown;
            pnlLogo.Controls.Add(picLogo);
            _sidebarPanel.Controls.Add(pnlLogo);

            // Separator Line
            var pnlSep = new Panel
            {
                Location = new Point(12, 92),
                Size = new Size(206, 1),
                BackColor = ColorBorderDark
            };
            _sidebarPanel.Controls.Add(pnlSep);

            // 4 Navigation Buttons (Clean Vertical Positions)
            _btnNavHome = new CyberNavButton("Inicio", CyberNavIcon.Home)
            {
                Location = new Point(12, 106),
                Size = new Size(206, 44),
                IsActive = true
            };

            _btnNavInfo = new CyberNavButton("Carregar Informacoes", CyberNavIcon.Smartphone)
            {
                Location = new Point(12, 158),
                Size = new Size(206, 44)
            };

            _btnNavFrpExynos = new CyberNavButton("FRP Samsung Exynos", CyberNavIcon.Lock)
            {
                Location = new Point(12, 210),
                Size = new Size(206, 44)
            };

            _btnNavAbout = new CyberNavButton("Sobre", CyberNavIcon.Info)
            {
                Location = new Point(12, 262),
                Size = new Size(206, 44)
            };

            _navButtons = new List<CyberNavButton> { _btnNavHome, _btnNavInfo, _btnNavFrpExynos, _btnNavAbout };

            _btnNavHome.Click += (_, _) => { SetActiveNav(_btnNavHome); SwitchToView("Home"); };
            _btnNavInfo.Click += (_, _) => { SetActiveNav(_btnNavInfo); SwitchToView("ReadInfo"); };
            _btnNavFrpExynos.Click += (_, _) => { SetActiveNav(_btnNavFrpExynos); SwitchToView("FrpExynos"); };
            _btnNavAbout.Click += (_, _) => { SetActiveNav(_btnNavAbout); SwitchToView("About"); };

            _sidebarPanel.Controls.AddRange(new Control[] { _btnNavHome, _btnNavInfo, _btnNavFrpExynos, _btnNavAbout });

            // Bottom Version Tag & Live Port Indicator
            var lblSidebarVersion = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Text = "Master Unlock v1.0 Pro",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(75, 100, 130),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _lblSidebarLivePort = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Text = "○ USB: Desconectado",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 115, 145),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(6, 0, 6, 0),
                AutoEllipsis = true
            };

            _sidebarPanel.Controls.Add(_lblSidebarLivePort);
            _sidebarPanel.Controls.Add(lblSidebarVersion);
        }

        private void SetActiveNav(CyberNavButton activeBtn)
        {
            foreach (var b in _navButtons) b.IsActive = (b == activeBtn);
        }

        // ============================================================
        // 2. TOP HEADER BAR
        // ============================================================
        private void BuildTopHeader(Panel parent)
        {
            _topHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = ColorBgHeader,
                Padding = new Padding(16, 0, 8, 0)
            };
            _topHeaderPanel.MouseDown += Header_MouseDown;
            parent.Controls.Add(_topHeaderPanel);

            // Breadcrumb Title (Dock = Left)
            _lblHeaderBreadcrumb = new Label
            {
                Text = "MASTER UNLOCK  •  INICIO",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Left,
                Width = 400,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _lblHeaderBreadcrumb.MouseDown += Header_MouseDown;
            _topHeaderPanel.Controls.Add(_lblHeaderBreadcrumb);

            // Window Buttons (Min, Max, Close)
            var btnClose = MakeWindowControlButton("X", ColorDanger, () => Close());
            var btnMax = MakeWindowControlButton("[ ]", ColorCyanGlow, () =>
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            });
            var btnMin = MakeWindowControlButton("-", ColorCyanGlow, () => WindowState = FormWindowState.Minimized);

            var pnlWindowControls = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 110,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = ColorBgHeader,
                Padding = new Padding(0, 6, 0, 0)
            };
            pnlWindowControls.Controls.Add(btnClose);
            pnlWindowControls.Controls.Add(btnMax);
            pnlWindowControls.Controls.Add(btnMin);

            // Live Connection Badge in Top Header (Dock = Right, to the left of window controls)
            _badgeConnection = new CyberConnectionBadge
            {
                Dock = DockStyle.Right,
                Width = 250,
                Height = 32,
                BackColor = ColorBgHeader
            };

            var pnlBadgeHolder = new Panel
            {
                Dock = DockStyle.Right,
                Width = 260,
                Height = 44,
                BackColor = ColorBgHeader,
                Padding = new Padding(0, 6, 10, 6)
            };
            pnlBadgeHolder.Controls.Add(_badgeConnection);

            _topHeaderPanel.Controls.Add(pnlBadgeHolder);
            _topHeaderPanel.Controls.Add(pnlWindowControls);
        }

        private Button MakeWindowControlButton(string text, Color hoverColor, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Width = 32,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                ForeColor = ColorTextMuted,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(2, 0, 2, 0)
            };
            btn.MouseEnter += (_, _) => { btn.ForeColor = hoverColor; btn.BackColor = Color.FromArgb(20, 35, 58); };
            btn.MouseLeave += (_, _) => { btn.ForeColor = ColorTextMuted; btn.BackColor = Color.Transparent; };
            btn.Click += (_, _) => onClick();
            return btn;
        }

        // ============================================================
        // VIEW SWITCHER
        // ============================================================
        private void SwitchToView(string viewName)
        {
            _viewHome.Visible = (viewName == "Home");
            _viewReadInfo.Visible = (viewName == "ReadInfo");
            _viewFrpExynos.Visible = (viewName == "FrpExynos");
            _viewAbout.Visible = (viewName == "About");

            switch (viewName)
            {
                case "Home":
                    _viewHome.BringToFront();
                    _lblHeaderBreadcrumb.Text = "MASTER UNLOCK  •  INICIO";
                    UpdateHomeStatus();
                    break;
                case "ReadInfo":
                    _viewReadInfo.BringToFront();
                    _lblHeaderBreadcrumb.Text = $"MASTER UNLOCK  •  CARREGAR INFORMACOES ({_selectedBrand.ToUpper()})";
                    break;
                case "FrpExynos":
                    _viewFrpExynos.BringToFront();
                    _lblHeaderBreadcrumb.Text = "MASTER UNLOCK  •  FRP SAMSUNG EXYNOS";
                    break;
                case "About":
                    _viewAbout.BringToFront();
                    _lblHeaderBreadcrumb.Text = "MASTER UNLOCK  •  SOBRE";
                    break;
            }
        }

        // ============================================================
        // VIEW 1: INICIO (HOME DASHBOARD)
        // ============================================================
        private void InitViewHome()
        {
            _viewHome = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(0)
            };
            _mainContentContainer.Controls.Add(_viewHome);

            // 1. Hero Card (Dock = Top)
            var cardHero = new CyberCardPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(18, 12, 18, 12)
            };

            var lblHeroTitle = new Label
            {
                Text = "Master Unlock Suite Pro",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Top,
                Height = 28
            };

            var lblHeroSub = new Label
            {
                Text = "Plataforma profissional integrada de engenharia e suporte para dispositivos móveis.\nDiagnóstico MTP multi-marcas em tempo real e desbloqueio FRP Exynos via exploit Bootrom / Odin sBoot.",
                Font = new Font("Segoe UI", 9.2f),
                ForeColor = ColorTextMuted,
                Dock = DockStyle.Fill
            };
            cardHero.Controls.Add(lblHeroSub);
            cardHero.Controls.Add(lblHeroTitle);

            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = ColorBgApp };

            // 2. 3 Status Cards TableLayoutPanel (Dock = Top)
            var pnlStatusGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 132,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = ColorBgApp,
                Margin = new Padding(0)
            };
            pnlStatusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            pnlStatusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            pnlStatusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            // Card 1: COM Ports
            var cardPorts = new CyberCardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(14, 10, 14, 10)
            };
            var lblPortHeader = new Label
            {
                Text = "PORTAS COM ATIVAS",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Top,
                Height = 20
            };
            _lblHomePortCount = new Label
            {
                Text = "Escaneando...",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                Dock = DockStyle.Top,
                Height = 28
            };
            var btnRescanHome = new CyberButton
            {
                Text = "Escanear Portas",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColorPrimary = Color.FromArgb(0, 70, 150),
                BackColorSecondary = Color.FromArgb(0, 115, 220),
                BorderColor = ColorCyanGlow,
                TextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnRescanHome.Click += (_, _) =>
            {
                RefreshAll();
                UpdateHomeStatus();
            };
            cardPorts.Controls.AddRange(new Control[] { btnRescanHome, _lblHomePortCount, lblPortHeader });

            // Card 2: Smartphone MTP
            var cardMtp = new CyberCardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Margin = new Padding(4, 0, 4, 0),
                Padding = new Padding(14, 10, 14, 10)
            };
            var lblMtpHeader = new Label
            {
                Text = "DISPOSITIVO MTP",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Top,
                Height = 20
            };
            _lblHomeMtpStatus = new Label
            {
                Text = "Nenhum aparelho lido",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColorTextMuted,
                Dock = DockStyle.Top,
                Height = 28,
                AutoEllipsis = true
            };
            var btnReadHome = new CyberButton
            {
                Text = "Ler Informacoes (MTP)",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColorPrimary = Color.FromArgb(0, 130, 65),
                BackColorSecondary = Color.FromArgb(0, 190, 85),
                BorderColor = ColorSuccess,
                TextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnReadHome.Click += (_, _) =>
            {
                SetActiveNav(_btnNavInfo);
                SwitchToView("ReadInfo");
                BtnMtpRead_Click(null, EventArgs.Empty);
            };
            cardMtp.Controls.AddRange(new Control[] { btnReadHome, _lblHomeMtpStatus, lblMtpHeader });

            // Card 3: Exynos Engine
            var cardEngine = new CyberCardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Margin = new Padding(8, 0, 0, 0),
                Padding = new Padding(14, 10, 14, 10)
            };
            var lblEngineHeader = new Label
            {
                Text = "MOTOR EXYNOS FRP",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Top,
                Height = 20
            };
            _lblHomeEngineStatus = new Label
            {
                Text = "Carregando presets...",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColorSuccess,
                Dock = DockStyle.Top,
                Height = 28
            };
            var btnOpenFrpHome = new CyberButton
            {
                Text = "Abrir Modulo FRP",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColorPrimary = Color.FromArgb(0, 90, 190),
                BackColorSecondary = Color.FromArgb(0, 140, 255),
                BorderColor = ColorCyanGlow,
                TextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnOpenFrpHome.Click += (_, _) =>
            {
                SetActiveNav(_btnNavFrpExynos);
                SwitchToView("FrpExynos");
            };
            cardEngine.Controls.AddRange(new Control[] { btnOpenFrpHome, _lblHomeEngineStatus, lblEngineHeader });

            pnlStatusGrid.Controls.Add(cardPorts, 0, 0);
            pnlStatusGrid.Controls.Add(cardMtp, 1, 0);
            pnlStatusGrid.Controls.Add(cardEngine, 2, 0);

            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = ColorBgApp };

            // 3. Quick Guide Card (Dock = Fill)
            var cardGuide = new CyberCardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(18, 14, 18, 14)
            };
            var lblGuideTitle = new Label
            {
                Text = "Instrucoes de Uso e Fluxo Recomendado",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Top,
                Height = 28
            };
            var txtGuide = new Label
            {
                Text = "1. Conecte o smartphone ligado normalmente ao computador através de um cabo USB de boa qualidade.\n\n" +
                       "2. Acesse a aba 'Carregar Informacoes' para efetuar a leitura completa do dispositivo via MTP (Modelo, Processador SoC, Versão Android, CSC e Patch de Segurança).\n\n" +
                       "3. Se o aparelho for equipado com processador Samsung Exynos, acerte o chip correspondente e acesse 'FRP Samsung Exynos'.\n\n" +
                       "4. Selecione a porta COM e o Preset desejado. Clique em 'Reset FRP' e acompanhe a execução no terminal em tempo real.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColorTextWhite,
                Dock = DockStyle.Fill
            };
            cardGuide.Controls.Add(txtGuide);
            cardGuide.Controls.Add(lblGuideTitle);

            // WinForms docking order: add Fill first, then Top items from bottom to top
            _viewHome.Controls.Add(cardGuide);
            _viewHome.Controls.Add(spacer2);
            _viewHome.Controls.Add(pnlStatusGrid);
            _viewHome.Controls.Add(spacer1);
            _viewHome.Controls.Add(cardHero);

            cardHero.SendToBack();
            spacer1.SendToBack();
            pnlStatusGrid.SendToBack();
            spacer2.SendToBack();
            cardGuide.BringToFront();
        }

        private void UpdateHomeStatus()
        {
            if (_lblHomePortCount != null)
            {
                int count = _lastDetectedPorts?.Count ?? 0;
                var sam = _lastDetectedPorts?.FirstOrDefault(p => p.IsSamsung || p.IsModem || p.IsDownloadMode);
                if (sam != null)
                    _lblHomePortCount.Text = $"{sam.PortName} (Samsung Ativa)";
                else if (count > 0)
                    _lblHomePortCount.Text = $"{count} Porta(s) Ativa(s)";
                else
                    _lblHomePortCount.Text = "Nenhuma porta ativa";
            }

            if (_lblHomeMtpStatus != null)
            {
                if (_lastMtpInfo != null && _lastMtpInfo.Success)
                    _lblHomeMtpStatus.Text = $"{_lastMtpInfo.CommercialName ?? _lastMtpInfo.ModelNumber}";
                else
                    _lblHomeMtpStatus.Text = "Nenhum aparelho lido";
            }

            if (_lblHomeEngineStatus != null)
            {
                _lblHomeEngineStatus.Text = $"{_engine.Presets.Count} Presets Prontos";
            }
        }

        // ============================================================
        // VIEW 2: CARREGAR INFORMACOES (MTP / FASTBOOT / SIDELOAD MULTI-BRAND READER)
        // ============================================================
        private void InitViewReadInfo()
        {
            _viewReadInfo = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(0)
            };
            _mainContentContainer.Controls.Add(_viewReadInfo);

            // 1. Brand Selector Header Bar (Dock = Top)
            var pnlBrandHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = ColorBgApp,
                Margin = new Padding(0)
            };

            string[] brands = { "Samsung", "Xiaomi", "Motorola", "LG", "Huawei", "Generico MTP" };
            _brandButtons.Clear();
            foreach (var b in brands)
            {
                var btnBrand = new CyberBrandTabButton(b)
                {
                    Size = new Size(118, 34),
                    IsActive = (b == _selectedBrand),
                    Margin = new Padding(0, 0, 8, 0)
                };
                btnBrand.Click += (s, e) =>
                {
                    _selectedBrand = b;
                    foreach (var tb in _brandButtons) tb.IsActive = (tb == btnBrand);
                    _lblHeaderBreadcrumb.Text = $"MASTER UNLOCK  •  CARREGAR INFORMACOES ({_selectedBrand.ToUpper()})";
                    UpdateBrandToolbar(_selectedBrand);
                };
                _brandButtons.Add(btnBrand);
                pnlBrandHeader.Controls.Add(btnBrand);
            }

            var spacerBrand = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = ColorBgApp };

            // 2. Action Toolbar (Dock = Top)
            _pnlMtpToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = ColorBgApp,
                Margin = new Padding(0)
            };

            _btnMtpRead = new CyberButton
            {
                Text = "  Ler Informacoes (MTP)",
                Size = new Size(190, 36),
                BackColorPrimary = Color.FromArgb(0, 90, 190),
                BackColorSecondary = Color.FromArgb(0, 140, 255),
                BorderColor = ColorCyanGlow,
                ButtonIcon = CyberButtonIcon.Smartphone,
                TextFont = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnMtpRead.Click += BtnMtpRead_Click;

            _btnXiaomiFastboot = new CyberButton
            {
                Text = "  Ler Fastboot (Xiaomi)",
                Size = new Size(195, 36),
                BackColorPrimary = Color.FromArgb(190, 95, 0),
                BackColorSecondary = Color.FromArgb(240, 130, 0),
                BorderColor = Color.FromArgb(255, 180, 0),
                BorderHoverColor = Color.White,
                ButtonIcon = CyberButtonIcon.Flash,
                TextFont = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnXiaomiFastboot.Click += BtnXiaomiFastboot_Click;

            _btnXiaomiSideload = new CyberButton
            {
                Text = "  Ler Sideload (Recovery)",
                Size = new Size(205, 36),
                BackColorPrimary = Color.FromArgb(115, 35, 175),
                BackColorSecondary = Color.FromArgb(155, 60, 220),
                BorderColor = Color.FromArgb(190, 105, 255),
                BorderHoverColor = Color.White,
                ButtonIcon = CyberButtonIcon.Recovery,
                TextFont = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnXiaomiSideload.Click += BtnXiaomiSideload_Click;

            _btnInstallDrivers = new CyberButton
            {
                Text = "  Instalar Drivers ADB",
                Size = new Size(185, 36),
                BackColorPrimary = Color.FromArgb(12, 115, 95),
                BackColorSecondary = Color.FromArgb(20, 165, 135),
                BorderColor = Color.FromArgb(40, 225, 185),
                BorderHoverColor = Color.White,
                ButtonIcon = CyberButtonIcon.Recovery,
                TextFont = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnInstallDrivers.Click += BtnInstallDrivers_Click;

            _btnMtpExport = new CyberButton
            {
                Text = "  Exportar Relatorio",
                Size = new Size(155, 36),
                BackColorPrimary = ColorBgCard,
                BackColorSecondary = ColorBgInput,
                BorderColor = ColorBorderDark,
                BorderHoverColor = ColorCyanGlow,
                ButtonIcon = CyberButtonIcon.Save,
                TextFont = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnMtpExport.Click += (_, _) => ExportMtpReport();

            _btnMtpClear = new CyberButton
            {
                Text = "  Limpar",
                Size = new Size(95, 36),
                BackColorPrimary = ColorBgCard,
                BackColorSecondary = ColorBgInput,
                BorderColor = ColorBorderDark,
                BorderHoverColor = ColorDanger,
                ButtonIcon = CyberButtonIcon.Trash,
                TextFont = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnMtpClear.Click += (_, _) => ClearMtpView();

            var spacerToolbar = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ColorBgApp };

            // 3. Body: Full Width Diagnostic Terminal Log (Dock = Fill)
            var pnlMtpBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp
            };

            var cardMtpLog = new CyberCardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(1)
            };

            var pnlLogHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = ColorBgHeader,
                Padding = new Padding(12, 0, 12, 0)
            };
            _lblLogT = new Label
            {
                Text = "TERMINAL DE DIAGNOSTICO MTP",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                Dock = DockStyle.Left,
                Width = 520,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlLogHeader.Controls.Add(_lblLogT);
            cardMtpLog.Controls.Add(pnlLogHeader);

            _txtMtpLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgTerminal,
                ForeColor = ColorTextWhite,
                Font = new Font("Consolas", 9.5f, FontStyle.Regular),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
            cardMtpLog.Controls.Add(_txtMtpLog);
            _txtMtpLog.BringToFront();

            pnlMtpBody.Controls.Add(cardMtpLog);
            cardMtpLog.BringToFront();

            _viewReadInfo.Controls.Add(pnlMtpBody);
            _viewReadInfo.Controls.Add(spacerToolbar);
            _viewReadInfo.Controls.Add(_pnlMtpToolbar);
            _viewReadInfo.Controls.Add(spacerBrand);
            _viewReadInfo.Controls.Add(pnlBrandHeader);

            pnlBrandHeader.SendToBack();
            spacerBrand.SendToBack();
            _pnlMtpToolbar.SendToBack();
            spacerToolbar.SendToBack();
            pnlMtpBody.BringToFront();

            UpdateBrandToolbar(_selectedBrand);
        }

        private void UpdateBrandToolbar(string brand)
        {
            if (_pnlMtpToolbar == null) return;
            _pnlMtpToolbar.SuspendLayout();
            _pnlMtpToolbar.Controls.Clear();

            if (brand.Equals("Xiaomi", StringComparison.OrdinalIgnoreCase))
            {
                _pnlMtpToolbar.Controls.Add(_btnXiaomiFastboot);
                _pnlMtpToolbar.Controls.Add(_btnXiaomiSideload);
                _pnlMtpToolbar.Controls.Add(_btnInstallDrivers);
                _pnlMtpToolbar.Controls.Add(_btnMtpRead);
                _pnlMtpToolbar.Controls.Add(_btnMtpExport);
                _pnlMtpToolbar.Controls.Add(_btnMtpClear);
                if (_lblLogT != null) _lblLogT.Text = "TERMINAL DE DIAGNOSTICO - XIAOMI (FASTBOOT / SIDELOAD / MTP)";
            }
            else if (brand.Equals("Samsung", StringComparison.OrdinalIgnoreCase))
            {
                _pnlMtpToolbar.Controls.Add(_btnMtpRead);
                _pnlMtpToolbar.Controls.Add(_btnMtpExport);
                _pnlMtpToolbar.Controls.Add(_btnMtpClear);
                if (_lblLogT != null) _lblLogT.Text = "TERMINAL DE DIAGNOSTICO - SAMSUNG MTP";
            }
            else
            {
                _pnlMtpToolbar.Controls.Add(_btnMtpRead);
                _pnlMtpToolbar.Controls.Add(_btnXiaomiFastboot);
                _pnlMtpToolbar.Controls.Add(_btnInstallDrivers);
                _pnlMtpToolbar.Controls.Add(_btnMtpExport);
                _pnlMtpToolbar.Controls.Add(_btnMtpClear);
                if (_lblLogT != null) _lblLogT.Text = $"TERMINAL DE DIAGNOSTICO - {brand.ToUpper()} (MTP / FASTBOOT)";
            }

            _pnlMtpToolbar.ResumeLayout(true);
        }

        private async void BtnInstallDrivers_Click(object? sender, EventArgs e)
        {
            if (busyState) return;

            var cts = ResetStop();
            SetBusy(true);
            _btnInstallDrivers.Enabled = false;

            Action<string, Color, bool> logger = (msg, col, breakline) =>
            {
                SendMtpLog(msg, col, breakline);
                SendLog(msg, col, breakline);
            };

            try
            {
                await XiaomiReader.InstallAdbSideloadDriversAsync(logger, cts.Token);
            }
            catch (Exception ex)
            {
                SendMtpLog($"[Erro Drivers] {ex.Message}", ColorDanger, true);
            }
            finally
            {
                _btnInstallDrivers.Enabled = true;
                SetBusy(false);
            }
        }

        private async void BtnXiaomiFastboot_Click(object? sender, EventArgs e)
        {
            if (busyState) return;

            var cts = ResetStop();
            SetBusy(true);
            _btnXiaomiFastboot.Enabled = false;

            Action<string, Color, bool> logger = (msg, col, breakline) =>
            {
                SendMtpLog(msg, col, breakline);
                SendLog(msg, col, breakline);
            };

            try
            {
                var info = await XiaomiReader.ReadFastbootInfoAsync(logger, cts.Token);
                if (info.Success)
                {
                    SendMtpLog("[FASTBOOT] Leitura Xiaomi Fastboot concluida com sucesso!", ColorSuccess, true);
                }
            }
            catch (Exception ex)
            {
                SendMtpLog($"[Erro Fastboot] {ex.Message}", ColorDanger, true);
            }
            finally
            {
                _btnXiaomiFastboot.Enabled = true;
                SetBusy(false);
            }
        }

        private async void BtnXiaomiSideload_Click(object? sender, EventArgs e)
        {
            if (busyState) return;

            var cts = ResetStop();
            SetBusy(true);
            _btnXiaomiSideload.Enabled = false;

            Action<string, Color, bool> logger = (msg, col, breakline) =>
            {
                SendMtpLog(msg, col, breakline);
                SendLog(msg, col, breakline);
            };

            try
            {
                var info = await XiaomiReader.ReadSideloadInfoAsync(logger, cts.Token);
                if (info.Success)
                {
                    SendMtpLog("[SIDELOAD] Leitura Xiaomi Sideload / Recovery concluida com sucesso!", ColorSuccess, true);
                }
            }
            catch (Exception ex)
            {
                SendMtpLog($"[Erro Sideload] {ex.Message}", ColorDanger, true);
            }
            finally
            {
                _btnXiaomiSideload.Enabled = true;
                SetBusy(false);
            }
        }

        private async void BtnMtpRead_Click(object? sender, EventArgs e)
        {
            if (busyState) return;

            var cts = ResetStop();
            SetBusy(true);
            _btnMtpRead.Enabled = false;

            Action<string, Color, bool> mtpLogger = (msg, col, breakline) =>
            {
                SendMtpLog(msg, col, breakline);
                SendLog(msg, col, breakline);
            };

            try
            {
                SendMtpLog($"[MTP] Iniciando leitura para marca '{_selectedBrand}'...", ColorCyanGlow, true);

                var info = await SamsungMtpReader.ReadDeviceInfoAsync(mtpLogger, cts.Token);
                _lastMtpInfo = info;

                if (info.Success)
                {
                    bool isExynos = info.Platform != null && info.Platform.Contains("Exynos", StringComparison.OrdinalIgnoreCase);

                    // Update Top Card in FRP View
                    _lblDeviceCommercial.Text = info.CommercialName ?? info.ModelNumber ?? "Dispositivo Samsung";
                    _lblDeviceChipset.Text = $"[{info.Platform ?? info.Chipset ?? "Desconhecido"}]";
                    _lblDeviceCompatibility.Text = isExynos ? "Compativel" : "Incompativel";
                    _lblDeviceCompatibility.ForeColor = isExynos ? ColorSuccess : ColorDanger;

                    SendMtpLog($"[MTP] Leitura concluida com sucesso!", ColorSuccess, true);
                }
                else
                {
                    SendMtpLog($"[MTP] Nao foi possivel ler os dados via MTP. Verifique o cabo e certifique-se de que a tela esteja ativa.", ColorWarning, true);
                }
            }
            catch (Exception ex)
            {
                SendMtpLog($"[Erro MTP] {ex.Message}", ColorDanger, true);
            }
            finally
            {
                _btnMtpRead.Enabled = true;
                SetBusy(false);
                UpdateHomeStatus();
            }
        }

        private void SendMtpLog(string text, Color color, bool breakline)
        {
            if (_txtMtpLog == null || _txtMtpLog.IsDisposed) return;
            Action act = () =>
            {
                _txtMtpLog.SelectionStart = _txtMtpLog.TextLength;
                _txtMtpLog.SelectionLength = 0;
                _txtMtpLog.SelectionColor = color;
                _txtMtpLog.AppendText(text);
                if (breakline) _txtMtpLog.AppendText(Environment.NewLine);
                _txtMtpLog.ScrollToCaret();
            };
            if (_txtMtpLog.InvokeRequired) _txtMtpLog.BeginInvoke(act);
            else act();
        }

        private void ClearMtpView()
        {
            _txtMtpLog.Clear();
        }

        private void ExportMtpReport()
        {
            if (_lastMtpInfo == null || !_lastMtpInfo.Success)
            {
                MessageBox.Show("Nenhum dado de dispositivo lido para exportar no momento.", "Master Unlock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Arquivo de Texto (*.txt)|*.txt",
                FileName = $"Relatorio_{_lastMtpInfo.ModelNumber}_{DateTime.Now:yyyyMMdd_HHmm}.txt",
                Title = "Salvar Relatorio MTP - Master Unlock"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var sb = new StringBuilder();
                sb.AppendLine("=================================================");
                sb.AppendLine("MASTER UNLOCK SUITE - RELATORIO DE DIAGNOSTICO");
                sb.AppendLine("=================================================");
                sb.AppendLine($"Data / Hora         : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Marca Selecionada   : {_selectedBrand}");
                sb.AppendLine($"Dispositivo         : {_lastMtpInfo.CommercialName}");
                sb.AppendLine($"Modelo              : {_lastMtpInfo.ModelNumber}");
                sb.AppendLine($"Plataforma / SoC    : {_lastMtpInfo.Platform} ({_lastMtpInfo.Chipset})");
                sb.AppendLine($"Versao Android      : {_lastMtpInfo.AndroidVersion}");
                sb.AppendLine($"Patch de Seguranca  : {_lastMtpInfo.SecurityPatchLevel}");
                sb.AppendLine($"CSC / Regiao        : {_lastMtpInfo.Csc}");
                sb.AppendLine($"Numero de Serie     : {_lastMtpInfo.SerialNumber}");
                sb.AppendLine($"IMEI                : {_lastMtpInfo.Imei}");
                sb.AppendLine("=================================================");
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Relatorio salvo com sucesso!", "Master Unlock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ============================================================
        // VIEW 3: FRP SAMSUNG EXYNOS FLASH MODULE
        // ============================================================
        private void InitViewFrpExynos()
        {
            _viewFrpExynos = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(0)
            };
            _mainContentContainer.Controls.Add(_viewFrpExynos);

            // 1. Top Configuration & Action Card (Dock = Top)
            var cardTopConfig = new CyberCardPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(12, 10, 12, 10)
            };

            // COM Port Selection
            var lblComTitle = new Label
            {
                Text = "Porta COM:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Location = new Point(10, 18),
                AutoSize = true
            };

            comboPorts = new ComboBox
            {
                Location = new Point(88, 14),
                Width = 155,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ColorBgInput,
                ForeColor = ColorTextWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };

            _btnRefreshPorts = new CyberButton
            {
                Text = "↻",
                Location = new Point(248, 13),
                Size = new Size(30, 28),
                BackColorPrimary = ColorBgCardHover,
                BackColorSecondary = ColorBgInput,
                BorderColor = ColorBorderDark,
                BorderHoverColor = ColorCyanGlow,
                TextFont = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            _btnRefreshPorts.Click += (_, _) => RefreshAll();

            // Preset Selection
            var lblPresetTitle = new Label
            {
                Text = "Preset Exynos:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Location = new Point(290, 18),
                AutoSize = true
            };

            cmbConfig = new ComboBox
            {
                Location = new Point(386, 14),
                Width = 220,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ColorBgInput,
                ForeColor = ColorTextWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
            cmbConfig.DropDown += CmbConfig_DropDown;

            _btnSaveConfig = new CyberButton
            {
                Text = "Salvar",
                Location = new Point(612, 13),
                Size = new Size(65, 28),
                BackColorPrimary = ColorBgCardHover,
                BackColorSecondary = ColorBgInput,
                BorderColor = ColorBorderDark,
                BorderHoverColor = ColorCyanGlow,
                TextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnSaveConfig.Click += (_, _) =>
            {
                RefreshAll();
                SendLog("[Config] Presets locais atualizados com sucesso.", ColorSuccess, true);
            };

            // Actions: Reset FRP + Stop
            _btnFlash = new CyberButton
            {
                Text = "  Reset FRP",
                Location = new Point(686, 12),
                Size = new Size(130, 32),
                BackColorPrimary = Color.FromArgb(0, 140, 55),
                BackColorSecondary = Color.FromArgb(0, 205, 80),
                BorderColor = ColorSuccess,
                BorderHoverColor = Color.White,
                ButtonIcon = CyberButtonIcon.Play,
                TextFont = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _btnFlash.Click += BtnFlash_Click;

            buttonStop = new CyberButton
            {
                Text = "  Stop",
                Location = new Point(824, 12),
                Size = new Size(80, 32),
                BackColorPrimary = Color.FromArgb(160, 20, 30),
                BackColorSecondary = Color.FromArgb(220, 40, 50),
                BorderColor = ColorDanger,
                BorderHoverColor = Color.White,
                ButtonIcon = CyberButtonIcon.Stop,
                Enabled = false,
                TextFont = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            buttonStop.Click += buttonStop_Click;

            cardTopConfig.Controls.AddRange(new Control[]
            {
                lblComTitle, comboPorts, _btnRefreshPorts,
                lblPresetTitle, cmbConfig, _btnSaveConfig,
                _btnFlash, buttonStop
            });

            var spacerFrp1 = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ColorBgApp };

            // 2. Device Status Card (Dock = Top)
            var pnlDeviceCard = new CyberCardPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(12, 6, 12, 6)
            };

            _lblDeviceCommercial = new Label
            {
                Text = "Aguardando leitura do dispositivo...",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Location = new Point(12, 6),
                Size = new Size(380, 18),
                AutoEllipsis = true
            };

            _lblDeviceChipset = new Label
            {
                Text = "[Conecte o smartphone via USB]",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ColorTextMuted,
                Location = new Point(12, 25),
                Size = new Size(380, 16),
                AutoEllipsis = true
            };

            _lblDeviceCompatibility = new Label
            {
                Text = "* Pronto",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                Dock = DockStyle.Right,
                Width = 200,
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlDeviceCard.Controls.AddRange(new Control[] { _lblDeviceCommercial, _lblDeviceChipset, _lblDeviceCompatibility });

            var spacerFrp2 = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = ColorBgApp };

            // 3. Bottom Status & Progress Bar (Dock = Bottom)
            var pnlBottomStatus = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = ColorBgApp,
                Padding = new Padding(0, 4, 0, 0)
            };

            ProgressBar = new CyberProgressBar
            {
                Dock = DockStyle.Top,
                Height = 5,
                Value = 0
            };
            pnlBottomStatus.Controls.Add(ProgressBar);

            var pnlStatusInfo = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(4, 4, 4, 0)
            };
            pnlBottomStatus.Controls.Add(pnlStatusInfo);

            _lblStatus = new Label
            {
                Text = "* Sistema pronto para operacao",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ColorSuccess,
                Dock = DockStyle.Left,
                Width = 450,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblTimer = new Label
            {
                Text = "00:00:00",
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                ForeColor = ColorTextMuted,
                Dock = DockStyle.Right,
                Width = 120,
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlStatusInfo.Controls.AddRange(new Control[] { _lblStatus, _lblTimer });

            // 4. Central Terminal Card (Dock = Fill)
            var cardTerminal = new CyberCardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(1)
            };

            // Terminal Header
            var pnlTermHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = ColorBgHeader,
                Padding = new Padding(12, 0, 12, 0)
            };

            var lblTermTitle = new Label
            {
                Text = "TERMINAL DE LOG EXYNOS",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                Dock = DockStyle.Left,
                Width = 260,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnSaveLog = new CyberButton
            {
                Text = "  Salvar Log",
                Dock = DockStyle.Right,
                Width = 105,
                Height = 26,
                BackColorPrimary = ColorBgCard,
                BackColorSecondary = ColorBgInput,
                BorderColor = ColorBorderDark,
                BorderHoverColor = ColorCyanGlow,
                ButtonIcon = CyberButtonIcon.Save,
                TextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnSaveLog.Click += (_, _) => SaveLogToFile();

            var pnlSpacer = new Panel { Dock = DockStyle.Right, Width = 8, BackColor = ColorBgHeader };

            _btnClearLog = new CyberButton
            {
                Text = "  Limpar",
                Dock = DockStyle.Right,
                Width = 85,
                Height = 26,
                BackColorPrimary = ColorBgCard,
                BackColorSecondary = ColorBgInput,
                BorderColor = ColorBorderDark,
                BorderHoverColor = ColorDanger,
                ButtonIcon = CyberButtonIcon.Trash,
                TextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnClearLog.Click += (_, _) => ClearLog();

            pnlTermHeader.Controls.AddRange(new Control[] { lblTermTitle, _btnSaveLog, pnlSpacer, _btnClearLog });
            cardTerminal.Controls.Add(pnlTermHeader);

            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgTerminal,
                ForeColor = ColorTextWhite,
                Font = new Font("Consolas", 9.5f, FontStyle.Regular),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
            cardTerminal.Controls.Add(txtLog);
            txtLog.BringToFront();

            _viewFrpExynos.Controls.Add(cardTerminal);
            _viewFrpExynos.Controls.Add(pnlBottomStatus);
            _viewFrpExynos.Controls.Add(spacerFrp2);
            _viewFrpExynos.Controls.Add(pnlDeviceCard);
            _viewFrpExynos.Controls.Add(spacerFrp1);
            _viewFrpExynos.Controls.Add(cardTopConfig);

            cardTopConfig.SendToBack();
            spacerFrp1.SendToBack();
            pnlDeviceCard.SendToBack();
            spacerFrp2.SendToBack();
            pnlBottomStatus.SendToBack();
            cardTerminal.BringToFront();
        }

        // ============================================================
        // VIEW 4: SOBRE (ABOUT VIEW)
        // ============================================================
        private void InitViewAbout()
        {
            _viewAbout = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBgApp,
                Padding = new Padding(0)
            };
            _mainContentContainer.Controls.Add(_viewAbout);

            var cardAbout = new CyberCardPanel
            {
                Size = new Size(640, 460),
                BackColor = ColorBgCard,
                BorderColor = ColorBorderDark,
                Padding = new Padding(24)
            };

            var picAbout = new PictureBox
            {
                Location = new Point(275, 18),
                Size = new Size(90, 85),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorBgCard,
                Image = LoadAppLogo()
            };

            var lblAboutTitle = new Label
            {
                Text = "Master Unlock Suite Pro",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = ColorCyanGlow,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 110),
                Size = new Size(600, 30)
            };

            var lblAboutVer = new Label
            {
                Text = "Versao 1.0 (Build 2026.1) - Cyber Tech Edition",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColorTextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 142),
                Size = new Size(600, 22)
            };

            var lblAboutDesc = new Label
            {
                Text = "Suíte avançada de diagnóstico, leitura de dados MTP multi-marcas e desbloqueio FRP para processadores Samsung Exynos através de exploit direto via Bootrom / Odin sBoot.\n\n" +
                       "Recursos Principais:\n" +
                       "• Leitura completa de informações MTP (Samsung, Xiaomi, Motorola, LG, Huawei)\n" +
                       "• Identificação inteligente de SoC (Exynos, Snapdragon, MTK, Unisoc)\n" +
                       "• Proteção de segurança contra chips não-Exynos\n" +
                       "• Suporte a carregamento de presets locais sob demanda\n" +
                       "• Terminal de depuração e monitoramento em tempo real",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColorTextWhite,
                Location = new Point(30, 180),
                Size = new Size(580, 210)
            };

            var lblCopy = new Label
            {
                Text = "© 2026 Master Dev Team. Todos os direitos reservados.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(80, 105, 135),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 415),
                Size = new Size(600, 20)
            };

            cardAbout.Controls.AddRange(new Control[] { picAbout, lblAboutTitle, lblAboutVer, lblAboutDesc, lblCopy });
            _viewAbout.Controls.Add(cardAbout);

            // Dynamic centering
            Action centerCard = () =>
            {
                int cx = Math.Max(20, (_viewAbout.Width - cardAbout.Width) / 2);
                int cy = Math.Max(20, (_viewAbout.Height - cardAbout.Height) / 2);
                cardAbout.Location = new Point(cx, cy);
            };
            _viewAbout.Resize += (s, e) => centerCard();
            centerCard();
        }

        // ============================================================
        // LOGGING SYSTEM WITH SYNTAX FORMATTING
        // ============================================================
        public void SendLog(string text, Color? colors = default, bool breakline = false)
        {
            if (txtLog == null || txtLog.IsDisposed) return;

            Action logAction = () =>
            {
                if (string.IsNullOrEmpty(text))
                {
                    if (breakline) AppendTextToLog(Environment.NewLine);
                    return;
                }

                if (text.Contains(" : ") && !text.StartsWith("[") && !text.StartsWith("-") && !text.StartsWith("="))
                {
                    int colonIdx = text.IndexOf(" : ", StringComparison.Ordinal);
                    string key = text.Substring(0, colonIdx);
                    string val = text.Substring(colonIdx + 3);

                    AppendColoredText(key.PadRight(22), ColorTextMuted);
                    AppendColoredText(": ", Color.FromArgb(70, 100, 140));

                    Color valColor = ColorCyanGlow;
                    if (val.Equals("LOCK", StringComparison.OrdinalIgnoreCase) || val.Contains("Incompativel", StringComparison.OrdinalIgnoreCase))
                        valColor = ColorDanger;
                    else if (val.Equals("OK", StringComparison.OrdinalIgnoreCase) || val.Equals("UNLOCK", StringComparison.OrdinalIgnoreCase))
                        valColor = ColorSuccess;
                    else if (val.Contains("Not supported", StringComparison.OrdinalIgnoreCase))
                        valColor = ColorWarning;

                    AppendColoredText(val, valColor);
                    if (breakline) AppendTextToLog(Environment.NewLine);
                }
                else
                {
                    AppendColoredText(text, colors ?? ColorTextWhite);
                    if (breakline) AppendTextToLog(Environment.NewLine);
                }

                txtLog.ScrollToCaret();
            };

            if (txtLog.InvokeRequired) txtLog.BeginInvoke(logAction);
            else logAction();
        }

        private void AppendColoredText(string text, Color color)
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color;
            txtLog.AppendText(text);
        }

        private void AppendTextToLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.AppendText(text);
        }

        private void ClearLog()
        {
            txtLog.Clear();
            _lblDeviceCommercial.Text = "Aguardando dispositivo...";
            _lblDeviceChipset.Text = "[Conecte o smartphone via USB]";
            _lblDeviceCompatibility.Text = "* Pronto";
            _lblDeviceCompatibility.ForeColor = ColorCyanGlow;
            _lblStatus.Text = "* Log limpo. Sistema pronto.";
            _lblStatus.ForeColor = ColorSuccess;
        }

        private void SaveLogToFile()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("Nenhum registro de log para salvar no momento.", "Master Unlock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Arquivo de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                FileName = $"MasterUnlock_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "Salvar Log de Diagnostico - Master Unlock"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, txtLog.Text, Encoding.UTF8);
                    SendLog($"[Log] Arquivo salvo com sucesso em: {sfd.FileName}", ColorSuccess, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha ao salvar arquivo de log: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ============================================================
        // PROGRESS & BUSY STATE
        // ============================================================
        public void progressBarRunning(bool isRunning, bool enableStop = false)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { progressBarRunning(isRunning, enableStop); });
                return;
            }

            busyState = isRunning;

            if (enableStop)
            {
                buttonStop.Enabled = isRunning;
            }

            if (isRunning)
            {
                ProgressBar.IsMarquee = true;
            }
            else
            {
                ProgressBar.IsMarquee = false;
                ProgressBar.Value = 0;
            }
        }

        private CancellationTokenSource ResetStop()
        {
            lock (_stopLock)
            {
                var old = stop;
                stop = new CancellationTokenSource();
                try { old?.Cancel(); } catch { }
                try { old?.Dispose(); } catch { }
                return stop;
            }
        }

        private void SetBusy(bool busy)
        {
            progressBarRunning(busy, enableStop: busy);
            _btnFlash.Enabled = !busy;
            _btnSaveConfig.Enabled = !busy;
            comboPorts.Enabled = !busy;
            cmbConfig.Enabled = !busy;
        }

        // ============================================================
        // STOP OPERATION
        // ============================================================
        private void buttonStop_Click(object? sender, EventArgs e)
        {
            try
            {
                if (stop != null && !stop.IsCancellationRequested)
                    stop.Cancel();
                _engine.TerminateCurrentProcess();
            }
            catch { }
            finally
            {
                busyState = false;
                buttonStop.Enabled = false;
                progressBarRunning(false);
                SendLog("Operacao interrompida pelo usuario.", ColorWarning, true);
                _lblStatus.Text = "Operacao interrompida.";
                _lblStatus.ForeColor = ColorWarning;
            }
        }

        // ============================================================
        // PRESETS & PORTS HELPER
        // ============================================================
        private string? FindLocalPresetsDirectory()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "presets"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets"),
                Path.Combine(Directory.GetCurrentDirectory(), "presets"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "presets")
            };

            foreach (string path in candidates)
            {
                if (Directory.Exists(path) && Directory.GetFiles(path, "*.json").Length > 0)
                    return Path.GetFullPath(path);
            }
            return null;
        }

        private void RefreshAll()
        {
            PerformAutoPortDetection(isHotplugEvent: false);

            if (!_configLoaded)
            {
                string? localDir = FindLocalPresetsDirectory();
                if (localDir != null)
                {
                    _engine.LoadPresetsFromDirectory(localDir);
                    _configLoaded = true;
                }
            }

            if (_configLoaded && _engine.Presets.Count > 0)
            {
                UpdateConfigCombo();
            }
            else if (cmbConfig != null)
            {
                cmbConfig.Items.Clear();
                cmbConfig.Items.Add("Auto Detect (Recommended)");
                if (cmbConfig.Items.Count > 0)
                    cmbConfig.SelectedIndex = 0;
            }
        }

        // ============================================================
        // AUTOMATIC USB & COM PORT DETECTION & HOTPLUG MONITOR
        // ============================================================
        private void PerformAutoPortDetection(bool isHotplugEvent)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PerformAutoPortDetection(isHotplugEvent)));
                return;
            }

            try
            {
                var ports = _engine.ScanDetailedComPorts();
                bool hasChanged = !ArePortListsEqual(_lastDetectedPorts, ports);

                if (hasChanged || isHotplugEvent)
                {
                    _lastDetectedPorts = ports;
                    UpdatePortDropdown(ports);

                    var samPort = ports.FirstOrDefault(p => p.IsSamsung || p.IsDownloadMode || p.IsModem);
                    var activePort = samPort ?? ports.FirstOrDefault();

                    if (activePort != null)
                    {
                        string portName = activePort.PortName;
                        string desc = !string.IsNullOrEmpty(activePort.FriendlyName) ? activePort.FriendlyName : activePort.Description;

                        _badgeConnection?.SetConnection(
                            isConnected: true,
                            portTitle: activePort.IsSamsung ? $"SAMSUNG ({portName})" : $"PORTA ({portName})",
                            statusDesc: activePort.IsDownloadMode ? "Modo Download Detectado" : (activePort.IsModem ? "Modem USB Conectado" : "Dispositivo COM Ativo"),
                            isSamsung: activePort.IsSamsung
                        );

                        if (_lblSidebarLivePort != null)
                        {
                            _lblSidebarLivePort.Text = $"● {portName}: {(activePort.IsSamsung ? "Samsung USB" : "Conectado")}";
                            _lblSidebarLivePort.ForeColor = activePort.IsSamsung ? ColorSuccess : ColorCyanGlow;
                        }

                        if (isHotplugEvent && portName != _lastConnectedPortName)
                        {
                            _lastConnectedPortName = portName;
                            SendLog($"[Plug & Play] Dispositivo conectado: {desc} [{portName}]", ColorSuccess, true);
                            SendMtpLog($"[Plug & Play] Dispositivo conectado: {desc} [{portName}]", ColorSuccess, true);
                        }
                    }
                    else
                    {
                        _badgeConnection?.SetConnection(
                            isConnected: false,
                            portTitle: "DESCONECTADO",
                            statusDesc: "Aguardando conexao USB...",
                            isSamsung: false
                        );

                        if (_lblSidebarLivePort != null)
                        {
                            _lblSidebarLivePort.Text = "○ USB: Desconectado";
                            _lblSidebarLivePort.ForeColor = Color.FromArgb(90, 115, 145);
                        }

                        if (isHotplugEvent && !string.IsNullOrEmpty(_lastConnectedPortName))
                        {
                            SendLog($"[Plug & Play] Dispositivo desconectado da porta {_lastConnectedPortName}.", ColorWarning, true);
                            SendMtpLog($"[Plug & Play] Dispositivo desconectado da porta {_lastConnectedPortName}.", ColorWarning, true);
                            _lastConnectedPortName = "";
                        }
                    }

                    UpdateHomeStatus();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in auto port detection: {ex.Message}");
            }
        }

        private bool ArePortListsEqual(List<ComPortInfo> list1, List<ComPortInfo> list2)
        {
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;
            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].PortName != list2[i].PortName || list1[i].HardwareId != list2[i].HardwareId)
                    return false;
            }
            return true;
        }

        private void UpdatePortDropdown(List<ComPortInfo> ports)
        {
            if (comboPorts == null) return;

            string? currentSelection = comboPorts.SelectedItem?.ToString();
            string currentCom = "";
            if (!string.IsNullOrEmpty(currentSelection))
            {
                var m = System.Text.RegularExpressions.Regex.Match(currentSelection, @"\b(COM\d+)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) currentCom = m.Groups[1].Value.ToUpper();
            }

            comboPorts.BeginUpdate();
            comboPorts.Items.Clear();

            int selectedIndex = -1;
            for (int i = 0; i < ports.Count; i++)
            {
                var p = ports[i];
                string itemText = !string.IsNullOrEmpty(p.FriendlyName) ? p.FriendlyName : p.PortName;
                comboPorts.Items.Add(itemText);

                if (!string.IsNullOrEmpty(currentCom) && p.PortName.Equals(currentCom, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
                else if (selectedIndex == -1 && (p.IsSamsung || p.IsModem || p.IsDownloadMode))
                {
                    selectedIndex = i;
                }
            }

            if (comboPorts.Items.Count > 0)
            {
                comboPorts.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            }
            else
            {
                comboPorts.SelectedIndex = -1;
            }
            comboPorts.EndUpdate();
        }

        private void CmbConfig_DropDown(object? sender, EventArgs e)
        {
            if (!_configLoaded)
            {
                string? localDir = FindLocalPresetsDirectory();
                if (localDir != null)
                {
                    _engine.LoadPresetsFromDirectory(localDir);
                    _configLoaded = true;
                    UpdateConfigCombo();
                    SendLog($"Configs carregadas: {_engine.Presets.Count} presets disponiveis.", ColorSuccess, true);
                }
                else if (!ConfigServerUrl.Contains("your.site"))
                {
                    _ = LoadConfigFromServerAsync();
                }
            }
        }

        private async Task LoadConfigFromServerAsync()
        {
            if (_configLoaded) return;

            SendLog("Conectando ao servidor de presets...", ColorCyanGlow, true);

            string tempDir = Path.Combine(Path.GetTempPath(), ".master_unlock_cfg_" + Environment.ProcessId);
            Directory.CreateDirectory(tempDir);

            int ok = 0, fail = 0;
            foreach (string name in ConfigNames)
            {
                try
                {
                    string json = await _httpClient.GetStringAsync(
                        ConfigServerUrl.TrimEnd('/') + "/" + name + ".json");
                    await File.WriteAllTextAsync(Path.Combine(tempDir, name + ".json"), json);
                    ok++;
                }
                catch { fail++; }
            }

            _engine.LoadPresetsFromDirectory(tempDir);
            try { Directory.Delete(tempDir, true); } catch { }

            _configLoaded = true;

            if (InvokeRequired) Invoke((MethodInvoker)delegate { UpdateConfigCombo(); });
            else UpdateConfigCombo();

            SendLog($"Presets: {ok} carregados, {fail} falhas.",
                fail == 0 ? ColorSuccess : ColorWarning, true);
        }

        private void UpdateConfigCombo()
        {
            if (cmbConfig == null) return;
            string? prev = cmbConfig.SelectedItem?.ToString();
            cmbConfig.Items.Clear();
            foreach (var p in _engine.Presets)
                cmbConfig.Items.Add(p.DisplayText ?? p.PresetName ?? "");

            if (cmbConfig.Items.Count > 0)
            {
                cmbConfig.SelectedIndex = prev != null && cmbConfig.Items.Contains(prev)
                    ? cmbConfig.Items.IndexOf(prev) : 0;
            }
            else
            {
                cmbConfig.SelectedIndex = -1;
            }
        }

        // ============================================================
        // MANUAL FLASH OPERATION (EXYNOS FRP)
        // ============================================================
        private async void BtnFlash_Click(object? sender, EventArgs e)
        {
            if (busyState) return;

            string? rawPort = comboPorts.SelectedItem?.ToString();
            string? display = cmbConfig.SelectedItem?.ToString();

            string comPort = "";
            if (!string.IsNullOrEmpty(rawPort))
            {
                var m = System.Text.RegularExpressions.Regex.Match(rawPort, @"\b(COM\d+)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                comPort = m.Success ? m.Groups[1].Value.ToUpper() : rawPort.Trim();
            }

            // CHECK 1: Non-Exynos SoC Block
            if (_lastMtpInfo != null && _lastMtpInfo.Success &&
                _lastMtpInfo.Platform != null && !_lastMtpInfo.Platform.Contains("Exynos", StringComparison.OrdinalIgnoreCase))
            {
                Watch.Reset();
                Watch.Start();
                txtLog.Text = null;

                SendLog("====================================================================================================", ColorDanger, true);
                SendLog("[BLOQUEIO DE SEGURANCA: APARELHO INCOMPATIVEL COM O MOTOR EXYNOS]", ColorDanger, true);
                SendLog("====================================================================================================", ColorDanger, true);
                SendLog($"Aparelho Detectado : {_lastMtpInfo.CommercialName ?? _lastMtpInfo.ModelNumber} ({_lastMtpInfo.ModelNumber})", ColorTextWhite, true);
                SendLog($"Plataforma / SoC   : {_lastMtpInfo.Platform} - {_lastMtpInfo.Chipset}", ColorWarning, true);
                SendLog("", ColorTextWhite, true);
                SendLog("MOTIVO DA INCOMPATIBILIDADE:", ColorDanger, true);
                SendLog("- O Master Unlock (Modulo Exynos) opera exclusivamente via exploit Bootrom/sBoot para chips SAMSUNG EXYNOS.", ColorTextWhite, true);
                SendLog($"- O seu aparelho utiliza processador {_lastMtpInfo.Platform}, o qual NAO possui arquitetura Exynos e nao aceita este exploit.", ColorTextWhite, true);
                SendLog("- O procedimento foi interrompido por seguranca para evitar corrupcao de boot ou travamento do aparelho.", ColorWarning, true);
                SendLog("", ColorTextWhite, true);
                SendLog("SOLUCAO RECOMENDADA:", ColorCyanGlow, true);
                if (_lastMtpInfo.Platform.Contains("MediaTek") || _lastMtpInfo.Platform.Contains("MTK"))
                {
                    SendLog("- Para aparelhos MediaTek (Helio / Dimensity): Utilize ferramentas compativeis com Bootrom MTK (ex: MTK Client, SP Flash Tool, SamFw MTK).", ColorSuccess, true);
                }
                else if (_lastMtpInfo.Platform.Contains("Qualcomm"))
                {
                    SendLog("- Para aparelhos Qualcomm Snapdragon: Utilize ferramentas com suporte a modo EDL (Emergency Download / Firehose).", ColorSuccess, true);
                }
                else if (_lastMtpInfo.Platform.Contains("UNISOC") || _lastMtpInfo.Platform.Contains("Spreadtrum"))
                {
                    SendLog("- Para aparelhos UNISOC / Spreadtrum: Utilize ferramentas compativeis com SPD Diag / Research Download.", ColorSuccess, true);
                }
                SendLog("====================================================================================================", ColorDanger, true);

                _lblStatus.Text = $"Bloqueado: Processador {_lastMtpInfo.Platform}";
                _lblStatus.ForeColor = ColorDanger;
                Watch.Stop();
                return;
            }

            if (string.IsNullOrEmpty(comPort))
            {
                SendLog("Por favor, conecte o dispositivo Samsung via USB e selecione a porta COM!", ColorWarning, true);
                return;
            }

            if (string.IsNullOrEmpty(display) || display.StartsWith("Auto Detect") || display.StartsWith("Clique"))
            {
                if (_engine.Presets.Count > 0)
                {
                    display = _engine.Presets[0].DisplayText;
                }
                else
                {
                    SendLog("Por favor, selecione uma configuracao de chipset/modelo Exynos valida!", ColorWarning, true);
                    return;
                }
            }

            var cfg = _engine.Presets.FirstOrDefault(p => p.DisplayText == display);
            if (cfg == null)
            {
                SendLog("A configuracao selecionada e invalida. Abra o dropdown para recarregar.", ColorDanger, true);
                return;
            }

            Watch.Reset();
            Watch.Start();
            txtLog.Text = null;
            ResetStop();

            SetBusy(true);

            SendLog("___________________________________________", Color.FromArgb(60, 60, 60), true);
            SendLog("INICIALIZANDO MOTOR DE DESBLOQUEIO EXYNOS...", ColorCyanGlow, true);
            SendLog($"  Plataforma: {cfg.ChipsetName}", ColorTextWhite, true);
            SendLog($"  Porta COM:  {comPort}", ColorTextWhite, true);
            SendLog("___________________________________________", Color.FromArgb(60, 60, 60), true);

            try
            {
                await Task.Delay(300, stop.Token);

                SendLog("Carregando configuracao do preset...", ColorCyanGlow, true);
                bool downloaded = await _engine.DownloadPresetAsync(cfg.PresetName!, ConfigServerUrl);
                if (!downloaded)
                {
                    SendLog("Falha ao carregar preset. Verifique a presenca dos arquivos locais ou conexao de rede.", ColorDanger, true);
                    return;
                }
                _engine.LoadExynosPresets();

                bool flashOk = await _engine.FlashAsync(comPort, cfg.PresetName!, stop.Token);

                if (flashOk)
                {
                    SendLog("FRP removido com sucesso!", ColorSuccess, true);
                    SendLog("Se apos reiniciar o USB reconectar a cada 2 segundos, execute uma Restauracao de Fabrica (Factory Reset).", ColorWarning, true);
                    _lblStatus.Text = "Concluido com sucesso";
                    _lblStatus.ForeColor = ColorSuccess;
                }
                else
                {
                    SendLog("FALHA NA EXECUCAO DO MOTOR EXYNOS.", ColorDanger, true);
                    _lblStatus.Text = "Falha na operacao";
                    _lblStatus.ForeColor = ColorDanger;
                }
            }
            catch (OperationCanceledException)
            {
                SendLog("Operacao cancelada pelo usuario.", ColorWarning, true);
            }
            catch (Exception ex)
            {
                SendLog($"Erro inesperado: {ex.Message}", ColorDanger, true);
            }
            finally
            {
                _engine.ClearPresets();
                Watch.Stop();
                SetBusy(false);
            }
        }

        // ============================================================
        // ENGINE CALLBACKS
        // ============================================================
        private void OnEngineLog(string text, Color color, bool breakline)
            => SendLog(text, color, breakline);

        private void OnProgress(double pct, string status)
        {
            if (InvokeRequired) { Invoke((MethodInvoker)delegate { OnProgress(pct, status); }); return; }
            ProgressBar.IsMarquee = false;
            ProgressBar.Value = Math.Max(0, Math.Min(100, (int)pct));
            _lblStatus.Text = status;
        }

        private Image? LoadAppLogo()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "LogoMasterUnlock.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogoMasterUnlock.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "LogoMasterUnlock.png"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "LogoMasterUnlock.png")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    try { return Image.FromFile(path); } catch { }
                }
            }
            return null;
        }

        private void Header_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }
    }

    // ============================================================
    // CUSTOM GDI+ CONTROLS (CYBER TECH SUITE)
    // ============================================================

    public enum CyberButtonIcon { None, Smartphone, Play, Stop, Trash, Save, Flash, Recovery }
    public enum CyberNavIcon { Home, Lock, Database, Wrench, Gear, List, Info, Smartphone }

    // Cyber Connection Badge (Live USB / Port connection pill indicator)
    public class CyberConnectionBadge : Control
    {
        private bool _isConnected = false;
        private string _portTitle = "DESCONECTADO";
        private string _statusDesc = "Aguardando conexao USB...";
        private bool _isSamsung = false;
        private System.Windows.Forms.Timer? _pulseTimer;
        private float _pulseAlpha = 0.6f;
        private bool _pulseAscending = true;

        public bool IsConnected => _isConnected;
        public string PortTitle => _portTitle;
        public string StatusDesc => _statusDesc;

        public CyberConnectionBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Height = 32;
            Width = 250;
            BackColor = MainForm.ColorBgHeader;

            _pulseTimer = new System.Windows.Forms.Timer { Interval = 60 };
            _pulseTimer.Tick += (s, e) =>
            {
                if (_isConnected)
                {
                    if (_pulseAscending)
                    {
                        _pulseAlpha += 0.04f;
                        if (_pulseAlpha >= 1f) { _pulseAlpha = 1f; _pulseAscending = false; }
                    }
                    else
                    {
                        _pulseAlpha -= 0.04f;
                        if (_pulseAlpha <= 0.4f) { _pulseAlpha = 0.4f; _pulseAscending = true; }
                    }
                    Invalidate();
                }
            };
            _pulseTimer.Start();
        }

        public void SetConnection(bool isConnected, string portTitle, string statusDesc, bool isSamsung)
        {
            _isConnected = isConnected;
            _portTitle = portTitle;
            _statusDesc = statusDesc;
            _isSamsung = isSamsung;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = Parent?.BackColor ?? MainForm.ColorBgHeader;
            if (parentBg == Color.Transparent) parentBg = MainForm.ColorBgHeader;
            using (var bgBrush = new SolidBrush(parentBg))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = CyberButton.CreateRoundedRectanglePath(rect, 6);

            Color cBg1 = _isConnected ? Color.FromArgb(12, 26, 46) : Color.FromArgb(10, 18, 30);
            Color cBg2 = _isConnected ? Color.FromArgb(8, 16, 32) : Color.FromArgb(6, 12, 20);
            using (var brush = new LinearGradientBrush(rect, cBg1, cBg2, LinearGradientMode.Vertical))
            {
                e.Graphics.FillPath(brush, path);
            }

            Color borderCol = _isConnected
                ? (_isSamsung ? MainForm.ColorSuccess : MainForm.ColorCyanGlow)
                : MainForm.ColorBorderDark;

            using (var pen = new Pen(borderCol, 1.1f))
            {
                e.Graphics.DrawPath(pen, path);
            }

            // Glowing Pulsing LED
            int ledX = 10;
            int ledY = Height / 2 - 5;
            int ledSize = 10;

            Color ledCoreColor = _isConnected
                ? (_isSamsung ? MainForm.ColorSuccess : MainForm.ColorCyanGlow)
                : Color.FromArgb(110, 130, 155);

            if (_isConnected)
            {
                int glowAlpha = (int)(90 * _pulseAlpha);
                using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, ledCoreColor)))
                {
                    e.Graphics.FillEllipse(glowBrush, ledX - 3, ledY - 3, ledSize + 6, ledSize + 6);
                }
            }

            using (var ledBrush = new SolidBrush(ledCoreColor))
            {
                e.Graphics.FillEllipse(ledBrush, ledX, ledY, ledSize, ledSize);
            }

            // Title & Subtitle text
            using var fontTitle = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontSub = new Font("Segoe UI", 7.5f, FontStyle.Regular);

            Color titleColor = _isConnected ? MainForm.ColorTextWhite : MainForm.ColorTextMuted;
            Color subColor = _isConnected
                ? (_isSamsung ? MainForm.ColorSuccess : MainForm.ColorCyanGlow)
                : Color.FromArgb(100, 125, 155);

            using var brushTitle = new SolidBrush(titleColor);
            using var brushSub = new SolidBrush(subColor);

            var sf = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            e.Graphics.DrawString(_portTitle, fontTitle, brushTitle, new RectangleF(28, 2, Width - 32, 14), sf);
            e.Graphics.DrawString(_statusDesc, fontSub, brushSub, new RectangleF(28, 16, Width - 32, 13), sf);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _pulseTimer?.Dispose();
            base.Dispose(disposing);
        }
    }

    // Brand Tab Button for Read Info View
    public class CyberBrandTabButton : Button
    {
        public string BrandName { get; set; }
        private bool _isActive = false;
        private bool _isHovered = false;

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; Invalidate(); }
        }

        public CyberBrandTabButton(string brand)
        {
            BrandName = brand;
            Text = brand;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = MainForm.ColorBgApp;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Always fill with solid background
            using (var bgBrush = new SolidBrush(MainForm.ColorBgApp))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = CyberButton.CreateRoundedRectanglePath(rect, 6);

            if (_isActive)
            {
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 90, 200), Color.FromArgb(0, 140, 255), LinearGradientMode.Vertical))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(MainForm.ColorCyanGlow, 1.4f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
            else if (_isHovered)
            {
                using (var brush = new SolidBrush(MainForm.ColorBgCardHover))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(MainForm.ColorBorderDark, 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
            else
            {
                using (var brush = new SolidBrush(MainForm.ColorBgCard))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(MainForm.ColorBorderDark, 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }

            Color tc = _isActive ? Color.White : (_isHovered ? MainForm.ColorCyanGlow : MainForm.ColorTextMuted);
            using var textBrush = new SolidBrush(tc);
            using var font = new Font("Segoe UI", 9f, _isActive ? FontStyle.Bold : FontStyle.Regular);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(BrandName, font, textBrush, ClientRectangle, sf);
        }
    }

    // Cyber Tech Action Button
    public class CyberButton : Button
    {
        public Color BackColorPrimary { get; set; } = Color.FromArgb(0, 90, 190);
        public Color BackColorSecondary { get; set; } = Color.FromArgb(0, 140, 255);
        public Color BorderColor { get; set; } = Color.FromArgb(0, 200, 255);
        public Color BorderHoverColor { get; set; } = Color.White;
        public CyberButtonIcon ButtonIcon { get; set; } = CyberButtonIcon.None;
        public Font TextFont { get; set; } = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        private bool _isHovered = false;
        private bool _isPressed = false;

        public CyberButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = MainForm.ColorBgCard;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Clear solid background
            Color containerBg = Parent?.BackColor ?? MainForm.ColorBgCard;
            if (containerBg == Color.Transparent) containerBg = MainForm.ColorBgCard;
            using (var bgBrush = new SolidBrush(containerBg))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = CreateRoundedRectanglePath(rect, 6);

            Color c1 = Enabled ? BackColorPrimary : Color.FromArgb(20, 30, 45);
            Color c2 = Enabled ? BackColorSecondary : Color.FromArgb(30, 45, 65);

            if (_isHovered && Enabled)
            {
                c1 = ControlPaint.Light(c1, 0.15f);
                c2 = ControlPaint.Light(c2, 0.20f);
            }
            if (_isPressed && Enabled)
            {
                c1 = ControlPaint.Dark(c1, 0.15f);
                c2 = ControlPaint.Dark(c2, 0.15f);
            }

            using (var brush = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Vertical))
            {
                e.Graphics.FillPath(brush, path);
            }

            Color bc = Enabled ? (_isHovered ? BorderHoverColor : BorderColor) : Color.FromArgb(40, 55, 75);
            using (var pen = new Pen(bc, 1.2f))
            {
                e.Graphics.DrawPath(pen, path);
            }

            int textLeft = 8;
            if (ButtonIcon != CyberButtonIcon.None)
            {
                DrawVectorIcon(e.Graphics, ButtonIcon, 10, Height / 2 - 8, 16, 16);
                textLeft = 30;
            }

            Color tc = Enabled ? Color.White : Color.FromArgb(100, 120, 140);
            using var textBrush = new SolidBrush(tc);
            var sf = new StringFormat
            {
                Alignment = (ButtonIcon == CyberButtonIcon.None) ? StringAlignment.Center : StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            };
            var textRect = new Rectangle(textLeft, 0, Width - textLeft - 6, Height);
            e.Graphics.DrawString(Text.Trim(), TextFont, textBrush, textRect, sf);
        }

        private void DrawVectorIcon(Graphics g, CyberButtonIcon icon, int x, int y, int w, int h)
        {
            using var pen = new Pen(Color.White, 1.5f);
            using var brush = new SolidBrush(Color.White);

            switch (icon)
            {
                case CyberButtonIcon.Smartphone:
                    g.DrawRectangle(pen, x + 2, y, 11, 16);
                    g.DrawLine(pen, x + 5, y + 13, x + 9, y + 13);
                    break;
                case CyberButtonIcon.Play:
                    Point[] pts = { new Point(x + 3, y), new Point(x + 14, y + 8), new Point(x + 3, y + 16) };
                    g.FillPolygon(brush, pts);
                    break;
                case CyberButtonIcon.Stop:
                    g.FillRectangle(brush, x + 2, y + 2, 12, 12);
                    break;
                case CyberButtonIcon.Trash:
                    g.DrawRectangle(pen, x + 3, y + 4, 10, 11);
                    g.DrawLine(pen, x + 1, y + 3, x + 15, y + 3);
                    g.DrawLine(pen, x + 5, y + 1, x + 11, y + 1);
                    break;
                case CyberButtonIcon.Save:
                    g.DrawRectangle(pen, x + 2, y + 1, 12, 14);
                    g.FillRectangle(brush, x + 4, y + 2, 8, 5);
                    g.DrawRectangle(pen, x + 4, y + 9, 8, 5);
                    break;
                case CyberButtonIcon.Flash:
                    Point[] bolt = {
                        new Point(x + 9, y),
                        new Point(x + 3, y + 8),
                        new Point(x + 8, y + 8),
                        new Point(x + 6, y + 16),
                        new Point(x + 13, y + 7),
                        new Point(x + 8, y + 7)
                    };
                    g.FillPolygon(brush, bolt);
                    break;
                case CyberButtonIcon.Recovery:
                    g.DrawLine(pen, x + 8, y + 1, x + 8, y + 11);
                    g.DrawLine(pen, x + 4, y + 7, x + 8, y + 11);
                    g.DrawLine(pen, x + 12, y + 7, x + 8, y + 11);
                    g.DrawLine(pen, x + 2, y + 14, x + 14, y + 14);
                    break;
            }
        }

        public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Cyber Sidebar Navigation Button
    public class CyberNavButton : Button
    {
        public string NavText { get; set; }
        public CyberNavIcon NavIcon { get; set; }
        private bool _isActive = false;
        private bool _isHovered = false;

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; Invalidate(); }
        }

        public CyberNavButton(string text, CyberNavIcon icon)
        {
            NavText = text;
            Text = text;
            NavIcon = icon;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = MainForm.ColorBgSidebar;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Wipe background with solid sidebar color
            using (var bgBrush = new SolidBrush(MainForm.ColorBgSidebar))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(2, 2, Width - 4, Height - 4);
            using var path = CyberButton.CreateRoundedRectanglePath(rect, 6);

            if (_isActive)
            {
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 75, 190), Color.FromArgb(0, 125, 250), LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(MainForm.ColorCyanGlow, 1.4f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
                // Neon cyan active indicator bar on the left
                using (var indBrush = new SolidBrush(MainForm.ColorCyanGlow))
                {
                    e.Graphics.FillRectangle(indBrush, 4, 9, 3.5f, Height - 18);
                }
            }
            else if (_isHovered)
            {
                using (var brush = new SolidBrush(Color.FromArgb(18, 36, 62)))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(Color.FromArgb(0, 140, 255), 1.1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
            else
            {
                using (var brush = new SolidBrush(Color.FromArgb(11, 21, 36)))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(Color.FromArgb(20, 38, 64), 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }

            Color iconColor = _isActive ? Color.White : (_isHovered ? MainForm.ColorCyanGlow : Color.FromArgb(126, 157, 191));
            DrawNavVectorIcon(e.Graphics, NavIcon, 14, Height / 2 - 8, 16, 16, iconColor);

            Color textColor = _isActive ? Color.White : (_isHovered ? Color.White : Color.FromArgb(160, 182, 212));
            using var font = new Font("Segoe UI", 9.5f, _isActive ? FontStyle.Bold : FontStyle.Regular);
            using var textBrush = new SolidBrush(textColor);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(NavText, font, textBrush, new Rectangle(40, 0, Width - 44, Height), sf);
        }

        private void DrawNavVectorIcon(Graphics g, CyberNavIcon icon, int x, int y, int w, int h, Color color)
        {
            using var pen = new Pen(color, 1.5f);
            using var brush = new SolidBrush(color);

            switch (icon)
            {
                case CyberNavIcon.Home:
                    Point[] roof = { new Point(x + 7, y), new Point(x + 14, y + 6), new Point(x, y + 6) };
                    g.FillPolygon(brush, roof);
                    g.DrawRectangle(pen, x + 2, y + 6, 10, 8);
                    g.DrawLine(pen, x + 5, y + 14, x + 5, y + 10);
                    g.DrawLine(pen, x + 9, y + 14, x + 9, y + 10);
                    g.DrawLine(pen, x + 5, y + 10, x + 9, y + 10);
                    break;
                case CyberNavIcon.Smartphone:
                    g.DrawRectangle(pen, x + 2, y, 11, 15);
                    g.DrawLine(pen, x + 5, y + 12, x + 9, y + 12);
                    break;
                case CyberNavIcon.Lock:
                    g.DrawArc(pen, x + 3, y, 9, 10, 180, 180);
                    g.FillRectangle(brush, x + 1, y + 6, 13, 9);
                    break;
                case CyberNavIcon.Info:
                    g.DrawEllipse(pen, x + 1, y + 1, 13, 13);
                    g.DrawLine(pen, x + 7, y + 6, x + 7, y + 11);
                    g.FillEllipse(brush, x + 6, y + 3, 2, 2);
                    break;
                default:
                    g.DrawEllipse(pen, x + 2, y + 2, 11, 11);
                    break;
            }
        }
    }

    // Cyber Card Panel with Glow Borders
    public class CyberCardPanel : Panel
    {
        public Color BorderColor { get; set; } = MainForm.ColorBorderDark;
        public int CornerRadius { get; set; } = 8;

        public CyberCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = MainForm.ColorBgCard;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color parentBg = Parent?.BackColor ?? MainForm.ColorBgApp;
            if (parentBg == Color.Transparent) parentBg = MainForm.ColorBgApp;
            using (var bgBrush = new SolidBrush(parentBg))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CyberButton.CreateRoundedRectanglePath(rect, CornerRadius);

            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(BorderColor, 1.2f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    // Slim Cyber Glowing Progress Bar
    public class CyberProgressBar : Control
    {
        private int _value = 0;
        private bool _isMarquee = false;
        private int _marqueePos = 0;
        private System.Windows.Forms.Timer? _marqueeTimer;

        public int Value
        {
            get => _value;
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public bool IsMarquee
        {
            get => _isMarquee;
            set
            {
                _isMarquee = value;
                if (_isMarquee)
                {
                    if (_marqueeTimer == null)
                    {
                        _marqueeTimer = new System.Windows.Forms.Timer { Interval = 25 };
                        _marqueeTimer.Tick += (s, e) =>
                        {
                            _marqueePos += 6;
                            if (_marqueePos > Width + 80) _marqueePos = -80;
                            Invalidate();
                        };
                    }
                    _marqueeTimer.Start();
                }
                else
                {
                    _marqueeTimer?.Stop();
                    Invalidate();
                }
            }
        }

        public CyberProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Height = 5;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var bgBrush = new SolidBrush(Color.FromArgb(6, 16, 30)))
            {
                e.Graphics.FillRectangle(bgBrush, 0, 0, Width, Height);
            }

            if (_isMarquee)
            {
                int mw = 80;
                var rect = new Rectangle(_marqueePos, 0, mw, Height);
                using var brush = new LinearGradientBrush(
                    new Rectangle(Math.Max(0, _marqueePos), 0, mw, Height),
                    Color.FromArgb(0, 180, 80), Color.FromArgb(0, 255, 160), LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(brush, rect);
            }
            else if (_value > 0)
            {
                int fillWidth = (int)((Width * _value) / 100.0);
                var rect = new Rectangle(0, 0, fillWidth, Height);
                using var brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(1, fillWidth), Height),
                    Color.FromArgb(0, 200, 80), Color.FromArgb(0, 255, 140), LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(brush, rect);
            }

            using var pen = new Pen(Color.FromArgb(14, 40, 70), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _marqueeTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
