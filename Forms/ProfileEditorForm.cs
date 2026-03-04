using DTScan.Models;
using DTScan.Services;

namespace DTScan.Forms;

public sealed class ProfileEditorForm : Form
{
    private readonly ProfileManager _profileManager;
    private readonly ScannerService _scannerService;

    private static readonly (string Key, string Label)[] ShortcutActions =
    [
        ("Scan", "Scan"),
        ("Import", "Import Images"),
        ("Save", "Save Selected"),
        ("SelectAll", "Select All"),
        ("DeselectAll", "Deselect All"),
        ("AutoSelect", "Auto Select"),
        ("RotateLeft", "Rotate Left"),
        ("RotateRight", "Rotate Right"),
        ("Delete", "Delete"),
        ("QuickLabel", "Quick Label"),
        ("Settings", "Settings"),
    ];

    private static readonly string[] ShortcutKeyOptions =
        ["None", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"];

    private ComboBox cmbProfile = null!;
    private Button btnNewProfile = null!;
    private Button btnDeleteProfile = null!;
    private UserProfile? _currentProfile;
    private TextBox txtName = null!;
    private ComboBox cmbTheme = null!;
    private Panel pnlThemePreview = null!;
    private TextBox txtSavePath = null!;
    private Button btnBrowse = null!;
    private TextBox txtSubFolder = null!;
    private TextBox txtFilePattern = null!;
    private ComboBox cmbFormat = null!;
    private ComboBox cmbDpi = null!;
    private ComboBox cmbColorMode = null!;
    private NumericUpDown nudPagesPerBatch = null!;
    private CheckBox chkAutoSave = null!;
    private CheckBox chkAutoSelect = null!;
    private CheckBox chkDeleteAfterSave = null!;
    private ComboBox cmbScanSource = null!;
    private ComboBox cmbScanDriver = null!;
    private ComboBox cmbScanner = null!;
    private Button btnRefreshScanners = null!;
    private Button btnResetCounter = null!;
    private Label lblPreview = null!;
    private Label lblCounterValue = null!;
    private readonly Dictionary<string, ComboBox> _shortcutCombos = new();
    private bool _loading;

    public ProfileEditorForm(ProfileManager profileManager, ScannerService scannerService)
    {
        _profileManager = profileManager;
        _scannerService = scannerService;
        BuildUI();
        PopulateProfileList();
        LoadProfile(_profileManager.ActiveProfile);
    }

    // ═══════════════════════════════════════════════
    //  BUILD UI — Tabbed Layout
    // ═══════════════════════════════════════════════

    private void BuildUI()
    {
        Text = "⚙  Profile Settings";
        Size = new Size(580, 700);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f);

        // ── Top — Profile selector ──
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top, Height = 60,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        pnlTop.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom, Height = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        });

        cmbProfile = new ComboBox
        {
            Location = new Point(20, 18), Size = new Size(290, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
        pnlTop.Controls.Add(cmbProfile);

        btnNewProfile = CreateSmallButton("+  New", new Point(320, 16));
        btnNewProfile.Click += BtnNewProfile_Click;
        pnlTop.Controls.Add(btnNewProfile);

        btnDeleteProfile = CreateSmallButton("−  Delete", new Point(410, 16));
        btnDeleteProfile.BackColor = Color.FromArgb(254, 226, 226);
        btnDeleteProfile.ForeColor = Color.FromArgb(185, 28, 28);
        btnDeleteProfile.Click += BtnDeleteProfile_Click;
        pnlTop.Controls.Add(btnDeleteProfile);

        // ── Bottom — Save / Cancel ──
        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom, Height = 56,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        btnPanel.Controls.Add(new Panel
        {
            Dock = DockStyle.Top, Height = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        });

        var btnSave = new Button
        {
            Text = "💾  Save", Size = new Size(120, 38),
            Location = new Point(320, 9),
            BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;
        btnPanel.Controls.Add(btnSave);

        var btnCancel = new Button
        {
            Text = "Cancel", Size = new Size(100, 38),
            Location = new Point(448, 9),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f)
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnPanel.Controls.Add(btnCancel);

        // ── Center — TabControl ──
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Point(14, 6)
        };

        var tabGeneral = new TabPage("  General  ") { AutoScroll = true, BackColor = BackColor };
        BuildGeneralTab(tabGeneral);
        tabs.TabPages.Add(tabGeneral);

        var tabScan = new TabPage("  Scan & Batch  ") { AutoScroll = true, BackColor = BackColor };
        BuildScanTab(tabScan);
        tabs.TabPages.Add(tabScan);

        var tabShortcuts = new TabPage("  Shortcuts  ") { AutoScroll = true, BackColor = BackColor };
        BuildShortcutsTab(tabShortcuts);
        tabs.TabPages.Add(tabShortcuts);

        // Assemble (add order determines dock priority)
        Controls.Add(tabs);
        Controls.Add(pnlTop);
        Controls.Add(btnPanel);
    }

    // ═══════════════════════════════════════════════
    //  TAB: General — Name, Theme, Save, Preview
    // ═══════════════════════════════════════════════

    private void BuildGeneralTab(TabPage tab)
    {
        const int cw = 490;
        var panel = new Panel { Location = new Point(16, 8), Width = cw };
        int y = 0;

        AddLabel(panel, "Display Name:", ref y);
        txtName = AddTextBox(panel, ref y, cw);
        txtName.TextChanged += (_, _) => UpdatePreview();

        AddLabel(panel, "Theme Color:", ref y);
        var themeRow = new Panel { Location = new Point(0, y), Size = new Size(cw, 30) };
        panel.Controls.Add(themeRow);
        cmbTheme = new ComboBox
        {
            Location = new Point(0, 2), Size = new Size(cw - 80, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbTheme.Items.AddRange(Enum.GetNames<ThemePreset>());
        pnlThemePreview = new Panel
        {
            Location = new Point(cw - 70, 2), Size = new Size(60, 24),
            BorderStyle = BorderStyle.FixedSingle
        };
        cmbTheme.SelectedIndexChanged += (_, _) =>
        {
            if (Enum.TryParse<ThemePreset>(cmbTheme.SelectedItem?.ToString(), out var t))
                pnlThemePreview.BackColor = ThemeColors.Get(t).Accent;
        };
        themeRow.Controls.Add(cmbTheme);
        themeRow.Controls.Add(pnlThemePreview);
        y += 36;

        AddDivider(panel, ref y, cw);

        AddLabel(panel, "Save Path:", ref y);
        var pathRow = new Panel { Location = new Point(0, y), Size = new Size(cw, 30) };
        panel.Controls.Add(pathRow);
        txtSavePath = new TextBox { Location = new Point(0, 2), Size = new Size(cw - 50, 26) };
        txtSavePath.TextChanged += (_, _) => UpdatePreview();
        pathRow.Controls.Add(txtSavePath);
        btnBrowse = new Button
        {
            Text = "...", Location = new Point(cw - 44, 0), Size = new Size(40, 28),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnBrowse.Click += BtnBrowse_Click;
        pathRow.Controls.Add(btnBrowse);
        y += 38;

        AddLabel(panel, "Sub-folder Pattern:", ref y);
        txtSubFolder = AddTextBox(panel, ref y, cw);
        txtSubFolder.TextChanged += (_, _) => UpdatePreview();

        AddLabel(panel, "File Name Pattern:", ref y);
        txtFilePattern = AddTextBox(panel, ref y, cw);
        txtFilePattern.TextChanged += (_, _) => UpdatePreview();

        panel.Controls.Add(new Label
        {
            Text = "Placeholders:  {date}  {time}  {year}  {month}  {day}  {counter}  {user}  {page}  {batch}  {label}",
            Location = new Point(0, y), Size = new Size(cw, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
        });
        y += 22;

        panel.Controls.Add(new Label
        {
            Text = "→ {label} = nhãn/số do user nhập trên mỗi trang (ví dụ: 03, 01, 02...)",
            Location = new Point(0, y), Size = new Size(cw, 20),
            ForeColor = Color.FromArgb(99, 102, 241),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold | FontStyle.Italic)
        });
        y += 26;

        AddLabel(panel, "Format:", ref y);
        cmbFormat = new ComboBox
        {
            Location = new Point(0, y), Size = new Size(cw, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbFormat.Items.AddRange(Enum.GetNames<SaveFormat>());
        cmbFormat.SelectedIndexChanged += (_, _) => UpdatePreview();
        panel.Controls.Add(cmbFormat);
        y += 36;

        AddDivider(panel, ref y, cw);

        panel.Controls.Add(new Label
        {
            Text = "📄  File Name Preview",
            Location = new Point(0, y), AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        });
        y += 24;

        lblPreview = new Label
        {
            Location = new Point(0, y), Size = new Size(cw, 40),
            ForeColor = Color.FromArgb(59, 130, 246),
            Font = new Font("Consolas", 9.5f),
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(8, 6, 8, 6),
            AutoEllipsis = true
        };
        panel.Controls.Add(lblPreview);
        y += 48;

        panel.Height = y;
        tab.Controls.Add(panel);
    }

    // ═══════════════════════════════════════════════
    //  TAB: Scan & Batch
    // ═══════════════════════════════════════════════

    private void BuildScanTab(TabPage tab)
    {
        const int cw = 490;
        var panel = new Panel { Location = new Point(16, 8), Width = cw };
        int y = 0;

        AddLabel(panel, "Scanner Device:", ref y);
        var scannerRow = new Panel { Location = new Point(0, y), Size = new Size(cw, 30) };
        panel.Controls.Add(scannerRow);
        cmbScanner = new ComboBox
        {
            Location = new Point(0, 2), Size = new Size(cw - 100, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbScanner.Items.Add("(Auto — first available)");
        cmbScanner.SelectedIndex = 0;
        scannerRow.Controls.Add(cmbScanner);
        btnRefreshScanners = CreateSmallButton("🔄 Refresh", new Point(cw - 92, 0));
        btnRefreshScanners.Click += BtnRefreshScanners_Click;
        scannerRow.Controls.Add(btnRefreshScanners);
        y += 38;

        AddDivider(panel, ref y, cw);

        AddLabel(panel, "DPI:", ref y);
        cmbDpi = new ComboBox
        {
            Location = new Point(0, y), Size = new Size(cw, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbDpi.Items.AddRange(["150", "200", "300", "600", "1200"]);
        panel.Controls.Add(cmbDpi);
        y += 36;

        AddLabel(panel, "Color Mode:", ref y);
        cmbColorMode = new ComboBox
        {
            Location = new Point(0, y), Size = new Size(cw, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbColorMode.Items.AddRange(["Black & White", "Grayscale", "Color"]);
        panel.Controls.Add(cmbColorMode);
        y += 36;

        AddLabel(panel, "Scan Source:", ref y);
        cmbScanSource = new ComboBox
        {
            Location = new Point(0, y), Size = new Size(cw, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbScanSource.Items.AddRange(["Flatbed", "Feeder (ADF)", "Duplex"]);
        panel.Controls.Add(cmbScanSource);
        y += 36;

        AddLabel(panel, "Scan Driver:", ref y);
        cmbScanDriver = new ComboBox
        {
            Location = new Point(0, y), Size = new Size(cw, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbScanDriver.Items.AddRange(["Auto (TWAIN → WIA)", "TWAIN", "WIA"]);
        panel.Controls.Add(cmbScanDriver);
        y += 36;

        AddDivider(panel, ref y, cw);

        AddLabel(panel, "Pages per Batch:", ref y);
        nudPagesPerBatch = new NumericUpDown
        {
            Location = new Point(0, y), Size = new Size(cw, 28),
            Minimum = 1, Maximum = 100, Value = 1
        };
        nudPagesPerBatch.ValueChanged += (_, _) => UpdatePreview();
        panel.Controls.Add(nudPagesPerBatch);
        y += 34;

        chkAutoSave = new CheckBox
        {
            Text = "Auto save after scanning", Location = new Point(0, y),
            Size = new Size(cw, 24)
        };
        panel.Controls.Add(chkAutoSave);
        y += 28;

        chkAutoSelect = new CheckBox
        {
            Text = "Auto-select next batch after saving", Location = new Point(0, y),
            Size = new Size(cw, 24)
        };
        panel.Controls.Add(chkAutoSelect);
        y += 28;

        chkDeleteAfterSave = new CheckBox
        {
            Text = "Xóa các trang sau khi lưu (delete after save)", Location = new Point(0, y),
            Size = new Size(cw, 24)
        };
        panel.Controls.Add(chkDeleteAfterSave);
        y += 34;

        var counterRow = new Panel { Location = new Point(0, y), Size = new Size(cw, 30) };
        panel.Controls.Add(counterRow);
        lblCounterValue = new Label
        {
            Text = "Counter: 1", Location = new Point(0, 5), AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        counterRow.Controls.Add(lblCounterValue);
        btnResetCounter = CreateSmallButton("Reset Counter", new Point(120, 0));
        btnResetCounter.Click += (_, _) =>
        {
            lblCounterValue.Text = "Counter: 1";
            UpdatePreview();
        };
        counterRow.Controls.Add(btnResetCounter);
        y += 40;

        panel.Height = y;
        tab.Controls.Add(panel);
    }

    // ═══════════════════════════════════════════════
    //  TAB: Keyboard Shortcuts
    // ═══════════════════════════════════════════════

    private void BuildShortcutsTab(TabPage tab)
    {
        const int cw = 490;
        var panel = new Panel { Location = new Point(16, 8), Width = cw };
        int y = 0;

        panel.Controls.Add(new Label
        {
            Text = "Assign keyboard shortcuts to sidebar actions.",
            Location = new Point(0, y), Size = new Size(cw - 110, 22),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f, FontStyle.Italic)
        });

        var btnResetShortcuts = CreateSmallButton("Reset Default", new Point(cw - 100, y - 2));
        btnResetShortcuts.Click += (_, _) =>
        {
            var defaults = UserProfile.DefaultShortcuts();
            foreach (var (key, _) in ShortcutActions)
            {
                if (_shortcutCombos.TryGetValue(key, out var cmb))
                    cmb.SelectedItem = defaults.TryGetValue(key, out var dk) ? dk : "None";
            }
        };
        panel.Controls.Add(btnResetShortcuts);
        y += 32;

        foreach (var (actionKey, actionLabel) in ShortcutActions)
        {
            var row = new Panel { Location = new Point(0, y), Size = new Size(cw, 32) };
            row.Controls.Add(new Label
            {
                Text = actionLabel, Location = new Point(0, 6),
                Size = new Size(200, 20), ForeColor = Color.FromArgb(71, 85, 105)
            });
            var cmb = new ComboBox
            {
                Location = new Point(210, 2), Size = new Size(110, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmb.Items.AddRange(ShortcutKeyOptions);
            row.Controls.Add(cmb);
            _shortcutCombos[actionKey] = cmb;
            panel.Controls.Add(row);
            y += 34;
        }
        y += 10;

        panel.Height = y;
        tab.Controls.Add(panel);
    }

    // ═══════════════════════════════════════════════
    //  LOAD / SAVE PROFILE
    // ═══════════════════════════════════════════════

    private void CmbProfile_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        if (cmbProfile.SelectedItem is UserProfile p)
        {
            if (_currentProfile != null)
                SaveToProfile(_currentProfile);
            LoadProfile(p);
        }
    }

    private void LoadProfile(UserProfile p)
    {
        _loading = true;
        _currentProfile = p;
        txtName.Text = p.Name;
        cmbTheme.SelectedItem = p.ThemeColor.ToString();
        pnlThemePreview.BackColor = ThemeColors.Get(p.ThemeColor).Accent;
        txtSavePath.Text = p.SavePath;
        txtSubFolder.Text = p.SubFolderPattern;
        txtFilePattern.Text = p.FileNamePattern;
        cmbFormat.SelectedItem = p.DefaultFormat.ToString();
        cmbDpi.SelectedItem = p.ScanDpi.ToString();
        cmbColorMode.SelectedIndex = (int)p.ColorMode;
        cmbScanSource.SelectedIndex = (int)p.ScanSource;
        cmbScanDriver.SelectedIndex = (int)p.ScanDriver;
        nudPagesPerBatch.Value = p.PagesPerBatch;

        // Restore preferred scanner selection
        if (string.IsNullOrEmpty(p.PreferredScanner))
        {
            cmbScanner.SelectedIndex = 0;
        }
        else
        {
            var idx = cmbScanner.Items.IndexOf(p.PreferredScanner);
            if (idx < 0)
            {
                cmbScanner.Items.Add(p.PreferredScanner);
                idx = cmbScanner.Items.Count - 1;
            }
            cmbScanner.SelectedIndex = idx;
        }

        chkAutoSave.Checked = p.AutoSave;
        chkAutoSelect.Checked = p.AutoSelectNextBatch;
        chkDeleteAfterSave.Checked = p.DeleteAfterSave;
        lblCounterValue.Text = $"Counter: {p.Counter}";

        var defaults = UserProfile.DefaultShortcuts();
        foreach (var (key, _) in ShortcutActions)
        {
            if (_shortcutCombos.TryGetValue(key, out var cmb))
            {
                var val = p.Shortcuts.TryGetValue(key, out var k) ? k
                        : defaults.TryGetValue(key, out var dk) ? dk : "None";
                cmb.SelectedItem = val;
            }
        }

        UpdatePreview();
        _loading = false;
    }

    private void SaveToProfile(UserProfile p)
    {
        if (_loading) return;

        p.Name = txtName.Text.Trim();
        p.ThemeColor = Enum.TryParse<ThemePreset>(cmbTheme.SelectedItem?.ToString(), out var theme)
            ? theme : ThemePreset.Blue;
        p.SavePath = txtSavePath.Text.Trim();
        p.SubFolderPattern = txtSubFolder.Text.Trim();
        p.FileNamePattern = txtFilePattern.Text.Trim();
        p.DefaultFormat = Enum.Parse<SaveFormat>(cmbFormat.SelectedItem?.ToString() ?? "PNG");
        p.ScanDpi = int.TryParse(cmbDpi.SelectedItem?.ToString(), out int dpi) ? dpi : 300;
        p.ColorMode = cmbColorMode.SelectedIndex >= 0
            ? (ScanColorMode)cmbColorMode.SelectedIndex : ScanColorMode.Color;
        p.ScanSource = cmbScanSource.SelectedIndex >= 0
            ? (ScanSource)cmbScanSource.SelectedIndex : ScanSource.Flatbed;
        p.ScanDriver = cmbScanDriver.SelectedIndex >= 0
            ? (ScanDriver)cmbScanDriver.SelectedIndex : ScanDriver.Auto;
        p.PreferredScanner = cmbScanner.SelectedIndex > 0
            ? cmbScanner.SelectedItem?.ToString() ?? "" : "";
        p.PagesPerBatch = (int)nudPagesPerBatch.Value;
        p.AutoSave = chkAutoSave.Checked;
        p.AutoSelectNextBatch = chkAutoSelect.Checked;
        p.DeleteAfterSave = chkDeleteAfterSave.Checked;
        if (lblCounterValue.Text.StartsWith("Counter: ")
            && int.TryParse(lblCounterValue.Text[9..], out int c))
            p.Counter = c;

        p.Shortcuts.Clear();
        foreach (var (key, _) in ShortcutActions)
        {
            if (_shortcutCombos.TryGetValue(key, out var cmb))
                p.Shortcuts[key] = cmb.SelectedItem?.ToString() ?? "None";
        }
    }

    // ═══════════════════════════════════════════════
    //  PROFILE LIST
    // ═══════════════════════════════════════════════

    private void PopulateProfileList(UserProfile? selectProfile = null)
    {
        cmbProfile.SelectedIndexChanged -= CmbProfile_SelectedIndexChanged;
        cmbProfile.Items.Clear();
        cmbProfile.DisplayMember = "Name";
        foreach (var p in _profileManager.Profiles)
            cmbProfile.Items.Add(p);
        cmbProfile.SelectedItem = selectProfile ?? _profileManager.ActiveProfile;
        cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
    }

    private void UpdatePreview()
    {
        var tempProfile = new UserProfile
        {
            Name = txtName.Text,
            Counter = int.TryParse(lblCounterValue.Text.Replace("Counter: ", ""), out int c) ? c : 1,
            DefaultFormat = Enum.TryParse<SaveFormat>(cmbFormat.SelectedItem?.ToString(), out var f) ? f : SaveFormat.PNG
        };
        var preview = PlaceholderResolver.GetPreview(
            txtFilePattern.Text, txtSubFolder.Text, tempProfile);
        lblPreview.Text = preview;
    }

    // ═══════════════════════════════════════════════
    //  BUTTON HANDLERS
    // ═══════════════════════════════════════════════

    private void BtnRefreshScanners_Click(object? sender, EventArgs e)
    {
        var selected = cmbScanner.SelectedItem?.ToString();
        cmbScanner.Items.Clear();
        cmbScanner.Items.Add("(Auto — first available)");

        Cursor = Cursors.WaitCursor;
        try
        {
            var scanners = _scannerService.GetAvailableScanners(this.Handle);
            foreach (var name in scanners)
                cmbScanner.Items.Add(name);

            if (scanners.Count == 0)
                MessageBox.Show("No scanners detected.\n\n" +
                    "• Ensure the scanner is connected and powered on.\n" +
                    "• Check that TWAIN or WIA drivers are installed.",
                    "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error detecting scanners:\n{ex.Message}",
                "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { Cursor = Cursors.Default; }

        // Restore previous selection if still available
        var idx = selected != null ? cmbScanner.Items.IndexOf(selected) : -1;
        cmbScanner.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void BtnNewProfile_Click(object? sender, EventArgs e)
    {
        using var input = new Form
        {
            Text = "New Profile", Size = new Size(350, 160),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false
        };
        var lbl = new Label { Text = "Profile Name:", Location = new Point(15, 20), AutoSize = true };
        var txt = new TextBox { Location = new Point(15, 45), Size = new Size(300, 26) };
        var ok = new Button
        {
            Text = "Create", DialogResult = DialogResult.OK,
            Location = new Point(140, 80), Size = new Size(80, 32)
        };
        input.Controls.AddRange([lbl, txt, ok]);
        input.AcceptButton = ok;

        if (input.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
        {
            if (_currentProfile != null)
                SaveToProfile(_currentProfile);
            var p = _profileManager.CreateProfile(txt.Text.Trim());
            PopulateProfileList(p);
            LoadProfile(p);
        }
    }

    private void BtnDeleteProfile_Click(object? sender, EventArgs e)
    {
        if (cmbProfile.SelectedItem is not UserProfile p) return;
        if (_profileManager.Profiles.Count <= 1)
        {
            MessageBox.Show("Cannot delete the last profile.", "DTScan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show($"Delete profile \"{p.Name}\"?", "Confirm Delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _currentProfile = null;
            _profileManager.DeleteProfile(p);
            PopulateProfileList();
            LoadProfile(_profileManager.ActiveProfile);
        }
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select save directory",
            SelectedPath = txtSavePath.Text
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtSavePath.Text = dlg.SelectedPath;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (cmbProfile.SelectedItem is not UserProfile p) return;
        SaveToProfile(p);
        _profileManager.ActiveProfile = p;
        _profileManager.Save();
        DialogResult = DialogResult.OK;
        Close();
    }

    // ═══════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════

    private static void AddLabel(Panel parent, string text, ref int y)
    {
        var lbl = new Label
        {
            Text = text, Location = new Point(0, y), AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        parent.Controls.Add(lbl);
        y += 22;
    }

    private static TextBox AddTextBox(Panel parent, ref int y, int width = 500)
    {
        var txt = new TextBox { Location = new Point(0, y), Size = new Size(width, 26) };
        parent.Controls.Add(txt);
        y += 34;
        return txt;
    }

    private static void AddDivider(Panel parent, ref int y, int width)
    {
        y += 6;
        parent.Controls.Add(new Panel
        {
            Location = new Point(0, y), Size = new Size(width, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        y += 10;
    }

    private static Button CreateSmallButton(string text, Point location) => new()
    {
        Text = text, Location = location, Size = new Size(82, 30),
        FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
        BackColor = Color.FromArgb(241, 245, 249),
        ForeColor = Color.FromArgb(51, 65, 85),
        Font = new Font("Segoe UI", 8.5f)
    };
}
