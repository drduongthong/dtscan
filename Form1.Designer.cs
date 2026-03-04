namespace DTScan
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            // ── Colors ──
            var sidebarBg = Color.FromArgb(30, 41, 59);
            var sidebarBtnHover = Color.FromArgb(51, 65, 85);
            var sidebarBtnDown = Color.FromArgb(71, 85, 105);
            var accentBlue = Color.FromArgb(59, 130, 246);
            var contentBg = Color.FromArgb(241, 245, 249);
            var dividerColor = Color.FromArgb(51, 65, 85);

            // ── Form ──
            Text = "DTScan — Document Scanner";
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "DTScan.ico"));
            ClientSize = new Size(1150, 720);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = contentBg;
            Font = new Font("Segoe UI", 10f);

            // ══════════════════════════════════════
            //  SIDEBAR
            // ══════════════════════════════════════
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left, Width = 240,
                BackColor = sidebarBg, Padding = new Padding(0)
            };

            // Logo area
            pnlLogo = new Panel
            {
                Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(15, 23, 42)
            };
            lblAppName = new Label
            {
                Text = "DTScan", Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter,
                Height = 48, Padding = new Padding(0, 12, 0, 0)
            };
            lblAppSubtitle = new Label
            {
                Text = "Document Scanner", Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(148, 163, 184),
                TextAlign = ContentAlignment.TopCenter, Height = 24
            };
            pnlLogo.Controls.Add(lblAppSubtitle);
            pnlLogo.Controls.Add(lblAppName);

            // Sidebar buttons (added in reverse because of DockStyle.Top)
            btnScan = CreateSidebarButton("📷   Scan", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnImport = CreateSidebarButton("📂   Import Images", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnSave = CreateSidebarButton("💾   Save Selected", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            pnlDivider1 = CreateDivider(dividerColor);
            btnSelectAll = CreateSidebarButton("☑   Select All", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnDeselectAll = CreateSidebarButton("☐   Deselect All", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnAutoSelect = CreateSidebarButton("⚡   Auto Select Batch", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            pnlDivider2 = CreateDivider(dividerColor);
            btnRotateLeft = CreateSidebarButton("↶   Rotate Left", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnRotateRight = CreateSidebarButton("↷   Rotate Right", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnDelete = CreateSidebarButton("🗑   Delete Selected", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnDelete.ForeColor = Color.FromArgb(252, 165, 165);
            var pnlDivider3 = CreateDivider(dividerColor);
            btnQuickLabel = CreateSidebarButton("📝   Quick Label", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnQuickLabel.ForeColor = Color.FromArgb(196, 181, 253);

            btnHistory = CreateSidebarButton("📋   Lịch sử Scan", sidebarBg, sidebarBtnHover, sidebarBtnDown);
            btnHistory.ForeColor = Color.FromArgb(125, 211, 252);

            // ══════════════════════════════════════
            //  RIGHT SIDEBAR — ACTIVE PROFILE
            // ══════════════════════════════════════
            var profileAccent = Color.FromArgb(99, 102, 241);
            var profileBg = Color.FromArgb(15, 23, 42);

            pnlRightSidebar = new Panel
            {
                Dock = DockStyle.Right, Width = 230,
                BackColor = profileBg, Padding = new Padding(0)
            };

            // ── Accent border bên trái ──
            pnlRightBorder = new Panel
            {
                Dock = DockStyle.Left, Width = 2,
                BackColor = profileAccent
            };

            // ── Header ──
            pnlProfileHeader = new Panel
            {
                Dock = DockStyle.Top, Height = 52,
                BackColor = Color.FromArgb(30, 27, 75),
                Padding = new Padding(14, 0, 14, 0)
            };
            lblCurrentUser = new Label
            {
                Text = "👤  ACTIVE PROFILE",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(199, 210, 254),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlProfileHeader.Controls.Add(lblCurrentUser);

            // ── ComboBox chọn profile ──
            var pnlComboWrap = new Panel
            {
                Dock = DockStyle.Top, Height = 40,
                Padding = new Padding(14, 6, 14, 6)
            };
            cmbProfile = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            pnlComboWrap.Controls.Add(cmbProfile);

            // ── Divider ──
            var pnlProfileDivider = new Panel
            {
                Dock = DockStyle.Top, Height = 1,
                BackColor = Color.FromArgb(51, 65, 85),
                Padding = new Padding(14, 0, 14, 0)
            };

            // ── Section: Scan Settings ──
            var lblSectionScan = new Label
            {
                Text = "SCAN",
                Dock = DockStyle.Top, Height = 26,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(14, 0, 0, 2)
            };
            lblProfileFormat = new Label
            {
                Dock = DockStyle.Top, Height = 22,
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Segoe UI", 9f),
                Padding = new Padding(14, 2, 8, 0),
                Text = "📁  PNG · 300 DPI"
            };
            lblProfileBatch = new Label
            {
                Dock = DockStyle.Top, Height = 22,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 9f),
                Padding = new Padding(14, 0, 8, 0),
                Text = "📄  Batch: 1 · Flatbed"
            };

            // ── Divider 2 ──
            var pnlProfileDivider2 = new Panel
            {
                Dock = DockStyle.Top, Height = 1,
                BackColor = Color.FromArgb(51, 65, 85)
            };

            // ── Section: Output ──
            var lblSectionOutput = new Label
            {
                Text = "OUTPUT",
                Dock = DockStyle.Top, Height = 26,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(14, 0, 0, 2)
            };
            lblProfilePath = new Label
            {
                Dock = DockStyle.Top, Height = 22,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 8f),
                Padding = new Padding(14, 2, 8, 0),
                AutoEllipsis = true,
                Text = "📂  ..."
            };
            lblProfileFlags = new Label
            {
                Dock = DockStyle.Top, Height = 22,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8f),
                Padding = new Padding(14, 0, 8, 0),
                Text = ""
            };

            // ── Settings button tích hợp ──
            btnSettings = new Button
            {
                Text = "⚙   Profile Settings",
                Dock = DockStyle.Bottom, Height = 42,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(199, 210, 254),
                BackColor = Color.FromArgb(30, 27, 75),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(49, 46, 129);
            btnSettings.FlatAppearance.MouseDownBackColor = Color.FromArgb(67, 56, 202);

            // Assemble right sidebar (reverse order for DockStyle.Top)
            pnlRightSidebar.Controls.Add(lblProfileFlags);
            pnlRightSidebar.Controls.Add(lblProfilePath);
            pnlRightSidebar.Controls.Add(lblSectionOutput);
            pnlRightSidebar.Controls.Add(pnlProfileDivider2);
            pnlRightSidebar.Controls.Add(lblProfileBatch);
            pnlRightSidebar.Controls.Add(lblProfileFormat);
            pnlRightSidebar.Controls.Add(lblSectionScan);
            pnlRightSidebar.Controls.Add(pnlProfileDivider);
            pnlRightSidebar.Controls.Add(pnlComboWrap);
            pnlRightSidebar.Controls.Add(pnlProfileHeader);
            pnlRightSidebar.Controls.Add(pnlRightBorder);
            pnlRightSidebar.Controls.Add(btnSettings);

            // Assemble sidebar (top-to-bottom via reverse add order)
            pnlSidebar.Controls.Add(btnHistory);
            pnlSidebar.Controls.Add(btnQuickLabel);
            pnlSidebar.Controls.Add(pnlDivider3);
            pnlSidebar.Controls.Add(btnDelete);
            pnlSidebar.Controls.Add(btnRotateRight);
            pnlSidebar.Controls.Add(btnRotateLeft);
            pnlSidebar.Controls.Add(pnlDivider2);
            pnlSidebar.Controls.Add(btnAutoSelect);
            pnlSidebar.Controls.Add(btnDeselectAll);
            pnlSidebar.Controls.Add(btnSelectAll);
            pnlSidebar.Controls.Add(pnlDivider1);
            pnlSidebar.Controls.Add(btnSave);
            pnlSidebar.Controls.Add(btnImport);
            pnlSidebar.Controls.Add(btnScan);
            pnlSidebar.Controls.Add(pnlLogo);

            // ══════════════════════════════════════
            //  STATUS BAR
            // ══════════════════════════════════════
            statusBar = new StatusStrip { BackColor = Color.White };
            tslStatus = new ToolStripStatusLabel("Ready")
            {
                ForeColor = Color.FromArgb(16, 185, 129),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            tslSpring = new ToolStripStatusLabel { Spring = true };
            tslPageCount = new ToolStripStatusLabel("Pages: 0")
            {
                ForeColor = Color.FromArgb(71, 85, 105),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched
            };
            tslSelected = new ToolStripStatusLabel("Selected: 0")
            {
                ForeColor = Color.FromArgb(71, 85, 105),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched
            };
            tslBatch = new ToolStripStatusLabel("Batch: 1 page")
            {
                ForeColor = Color.FromArgb(71, 85, 105),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched
            };
            tslUser = new ToolStripStatusLabel("User: Default")
            {
                ForeColor = Color.FromArgb(59, 130, 246),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            statusBar.Items.AddRange(new ToolStripItem[] { tslStatus, tslSpring, tslPageCount, tslSelected, tslBatch, tslUser });

            // ══════════════════════════════════════
            //  CONTENT AREA
            // ══════════════════════════════════════
            pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = contentBg, Padding = new Padding(12) };

            flpPages = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                BackColor = contentBg, Padding = new Padding(8),
                WrapContents = true
            };

            // Empty state
            pnlEmptyState = new Panel
            {
                Dock = DockStyle.Fill, BackColor = contentBg, Visible = true
            };
            lblEmptyIcon = new Label
            {
                Text = "📄", Font = new Font("Segoe UI", 48f),
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Top, Height = 140,
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            lblEmptyText = new Label
            {
                Text = "No pages yet.\nClick 'Scan' or 'Import Images' to get started.",
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Top, Height = 60,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 11f)
            };
            pnlEmptyState.Controls.Add(lblEmptyText);
            pnlEmptyState.Controls.Add(lblEmptyIcon);

            // ── Zoom bar ──
            pnlZoomBar = new Panel
            {
                Dock = DockStyle.Top, Height = 38,
                BackColor = Color.White,
                Padding = new Padding(8, 0, 8, 0)
            };
            var pnlZoomDivider = new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            lblZoomIcon = new Label
            {
                Text = "\uD83D\uDD0D", Location = new Point(12, 8),
                AutoSize = true, Font = new Font("Segoe UI", 10f)
            };
            btnZoomOut = new Button
            {
                Text = "\u2212", Size = new Size(28, 26),
                Location = new Point(42, 6),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.FromArgb(241, 245, 249)
            };
            btnZoomOut.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            trkZoom = new TrackBar
            {
                Minimum = 120, Maximum = 1000, Value = 200,
                TickFrequency = 50, SmallChange = 20, LargeChange = 80,
                Location = new Point(76, 4), Size = new Size(200, 30),
                AutoSize = false, TickStyle = TickStyle.None
            };
            btnZoomIn = new Button
            {
                Text = "+", Size = new Size(28, 26),
                Location = new Point(282, 6),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.FromArgb(241, 245, 249)
            };
            btnZoomIn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            lblZoomValue = new Label
            {
                Text = "200px", Location = new Point(316, 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5f)
            };
            pnlZoomBar.Controls.Add(lblZoomIcon);
            pnlZoomBar.Controls.Add(btnZoomOut);
            pnlZoomBar.Controls.Add(trkZoom);
            pnlZoomBar.Controls.Add(btnZoomIn);
            pnlZoomBar.Controls.Add(lblZoomValue);
            pnlZoomBar.Controls.Add(pnlZoomDivider);

            pnlContent.Controls.Add(flpPages);
            pnlContent.Controls.Add(pnlEmptyState);
            pnlContent.Controls.Add(pnlZoomBar);

            // ══════════════════════════════════════
            //  ASSEMBLE FORM
            // ══════════════════════════════════════
            Controls.Add(pnlContent);
            Controls.Add(pnlRightSidebar);
            Controls.Add(pnlSidebar);
            Controls.Add(statusBar);

            // ── Scan button highlight ──
            btnScan.BackColor = accentBlue;
            btnScan.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            btnScan.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
            btnScan.FlatAppearance.MouseDownBackColor = Color.FromArgb(29, 78, 216);
            btnScan.Height = 48;
        }

        #endregion

        // ── Helper: create sidebar button ──
        private static Button CreateSidebarButton(string text,
            Color bg, Color hover, Color down)
        {
            var btn = new Button
            {
                Text = text, Dock = DockStyle.Top, Height = 42,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = bg,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0),
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hover;
            btn.FlatAppearance.MouseDownBackColor = down;
            return btn;
        }

        private static Panel CreateDivider(Color color) => new()
        {
            Dock = DockStyle.Top, Height = 1,
            BackColor = color, Margin = new Padding(20, 4, 20, 4)
        };

        // ── Control fields ──
        private Panel pnlSidebar;
        private Panel pnlLogo;
        private Label lblAppName;
        private Label lblAppSubtitle;
        private Button btnScan;
        private Button btnImport;
        private Button btnSave;
        private Panel pnlDivider1;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Button btnAutoSelect;
        private Panel pnlDivider2;
        private Button btnRotateLeft;
        private Button btnRotateRight;
        private Button btnDelete;
        private Button btnQuickLabel;
        private Button btnHistory;
        private Button btnSettings;
        private Panel pnlRightSidebar;
        private Label lblCurrentUser;
        private ComboBox cmbProfile;
        private Label lblProfileFormat;
        private Label lblProfileBatch;
        private Label lblProfilePath;
        private Label lblProfileFlags;
        private Panel pnlContent;
        private FlowLayoutPanel flpPages;
        private Panel pnlEmptyState;
        private Label lblEmptyIcon;
        private Label lblEmptyText;
        private StatusStrip statusBar;
        private ToolStripStatusLabel tslStatus;
        private ToolStripStatusLabel tslSpring;
        private ToolStripStatusLabel tslPageCount;
        private ToolStripStatusLabel tslSelected;
        private ToolStripStatusLabel tslBatch;
        private ToolStripStatusLabel tslUser;
        private Panel pnlZoomBar;
        private Label lblZoomIcon;
        private Button btnZoomOut;
        private TrackBar trkZoom;
        private Button btnZoomIn;
        private Label lblZoomValue;
        private Panel pnlRightBorder;
        private Panel pnlProfileHeader;
    }
}
