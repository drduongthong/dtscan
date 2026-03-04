using DTScan.Controls;
using DTScan.Models;
using DTScan.Services;

namespace DTScan.Forms;

public sealed class ScanHistoryForm : Form
{
    private readonly ScanHistoryService _historyService;
    private ListView _listView = null!;
    private Label _lblTimestamp = null!;
    private Label _lblPageCount = null!;
    private Label _lblProfile = null!;
    private Label _lblDpi = null!;
    private Label _lblSource = null!;
    private Label _lblColorMode = null!;
    private Label _lblSize = null!;
    private Label _lblPath = null!;
    private Button _btnPreview = null!;
    private Button _btnDelete = null!;
    private Button _btnRestore = null!;

    public List<Image>? RestoredImages { get; private set; }

    public ScanHistoryForm(ScanHistoryService historyService)
    {
        _historyService = historyService;
        BuildUI();
        RefreshList();
    }

    private void BuildUI()
    {
        Text = "📋 Lịch sử Scan";
        ClientSize = new Size(860, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10f);

        // ── Left: ListView ──
        _listView = new ListView
        {
            Location = new Point(16, 16),
            Size = new Size(480, 440),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.White
        };
        _listView.Columns.Add("#", 30);
        _listView.Columns.Add("Thời gian", 130);
        _listView.Columns.Add("Trang", 45);
        _listView.Columns.Add("Profile", 80);
        _listView.Columns.Add("DPI", 45);
        _listView.Columns.Add("Nguồn", 55);
        _listView.Columns.Add("Kích thước", 75);
        _listView.SelectedIndexChanged += OnSelectionChanged;
        Controls.Add(_listView);

        // ── Empty state ──
        Controls.Add(new Label
        {
            Text = "Chưa có lịch sử scan nào.",
            Location = new Point(16, 240),
            Size = new Size(480, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 11f, FontStyle.Italic),
            Name = "lblEmpty"
        });

        // ── Right: Detail panel ──
        int rx = 512, rw = 330;
        int ry = 16;

        CreateSectionLabel("📋  CHI TIẾT BẢN SAO LƯU", rx, ref ry);
        ry += 8;

        _lblTimestamp = CreateDetailRow("Thời gian:", rx, ref ry);
        _lblPageCount = CreateDetailRow("Số trang:", rx, ref ry);
        _lblProfile = CreateDetailRow("Profile:", rx, ref ry);
        _lblDpi = CreateDetailRow("DPI:", rx, ref ry);
        _lblSource = CreateDetailRow("Nguồn scan:", rx, ref ry);
        _lblColorMode = CreateDetailRow("Chế độ màu:", rx, ref ry);
        _lblSize = CreateDetailRow("Dung lượng:", rx, ref ry);
        _lblPath = CreateDetailRow("Đường dẫn:", rx, ref ry);
        ry += 12;

        // ── Action buttons ──
        _btnPreview = new Button
        {
            Text = "👁  Xem lại",
            Location = new Point(rx, ry),
            Size = new Size(rw, 40),
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnPreview.FlatAppearance.BorderSize = 0;
        _btnPreview.Click += BtnPreview_Click;
        Controls.Add(_btnPreview);
        ry += 48;

        _btnRestore = new Button
        {
            Text = "🔄  Khôi phục vào danh sách",
            Location = new Point(rx, ry),
            Size = new Size(rw, 40),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnRestore.FlatAppearance.BorderSize = 0;
        _btnRestore.Click += BtnRestore_Click;
        Controls.Add(_btnRestore);
        ry += 48;

        _btnDelete = new Button
        {
            Text = "🗑  Xóa bản sao lưu",
            Location = new Point(rx, ry),
            Size = new Size(rw, 40),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnDelete.FlatAppearance.BorderSize = 0;
        _btnDelete.Click += BtnDelete_Click;
        Controls.Add(_btnDelete);

        // ── Close button ──
        var btnClose = new Button
        {
            Text = "✕  Đóng",
            Location = new Point(rx, 468),
            Size = new Size(rw, 36),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }

    // ═══════════════════════════════════════════════
    //  UI HELPERS
    // ═══════════════════════════════════════════════

    private void CreateSectionLabel(string text, int x, ref int y)
    {
        Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(330, 24),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        });
        y += 28;
    }

    private Label CreateDetailRow(string labelText, int x, ref int y)
    {
        Controls.Add(new Label
        {
            Text = labelText,
            Location = new Point(x, y),
            Size = new Size(100, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f)
        });
        var val = new Label
        {
            Text = "—",
            Location = new Point(x + 105, y),
            Size = new Size(225, 20),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoEllipsis = true
        };
        Controls.Add(val);
        y += 24;
        return val;
    }

    // ═══════════════════════════════════════════════
    //  LIST MANAGEMENT
    // ═══════════════════════════════════════════════

    private void RefreshList()
    {
        _listView.Items.Clear();
        var entries = _historyService.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var item = new ListViewItem((i + 1).ToString());
            item.SubItems.Add(entry.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
            item.SubItems.Add(entry.PageCount.ToString());
            item.SubItems.Add(entry.ProfileName);
            item.SubItems.Add(entry.Dpi.ToString());
            item.SubItems.Add(entry.ScanSource);
            item.SubItems.Add(FormatSize(entry.TotalSizeBytes));
            item.Tag = entry;
            _listView.Items.Add(item);
        }

        var lblEmpty = Controls.Find("lblEmpty", false).FirstOrDefault();
        if (lblEmpty != null)
            lblEmpty.Visible = entries.Count == 0;
    }

    private ScanHistoryEntry? GetSelectedEntry()
    {
        if (_listView.SelectedItems.Count == 0) return null;
        return _listView.SelectedItems[0].Tag as ScanHistoryEntry;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        bool hasSelection = entry != null;
        _btnPreview.Enabled = hasSelection;
        _btnDelete.Enabled = hasSelection;
        _btnRestore.Enabled = hasSelection;

        if (entry != null)
        {
            _lblTimestamp.Text = entry.Timestamp.ToString("dd/MM/yyyy HH:mm:ss");
            _lblPageCount.Text = entry.PageCount.ToString();
            _lblProfile.Text = entry.ProfileName;
            _lblDpi.Text = $"{entry.Dpi} DPI";
            _lblSource.Text = entry.ScanSource;
            _lblColorMode.Text = entry.ColorMode;
            _lblSize.Text = FormatSize(entry.TotalSizeBytes);
            _lblPath.Text = entry.BackupFolderPath;
        }
        else
        {
            _lblTimestamp.Text = _lblPageCount.Text = _lblProfile.Text =
            _lblDpi.Text = _lblSource.Text = _lblColorMode.Text =
            _lblSize.Text = _lblPath.Text = "—";
        }
    }

    // ═══════════════════════════════════════════════
    //  PREVIEW
    // ═══════════════════════════════════════════════

    private void BtnPreview_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry == null) return;

        var images = _historyService.LoadBackupImages(entry);
        if (images.Count == 0)
        {
            MessageBox.Show("Không tìm thấy ảnh backup.", "DTScan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ShowPreviewForm(images, entry);
        foreach (var img in images) img.Dispose();
    }

    private void ShowPreviewForm(List<Image> images, ScanHistoryEntry entry)
    {
        using var form = new Form
        {
            Text = $"👁 Xem lại — {entry.Timestamp:dd/MM/yyyy HH:mm:ss} ({images.Count} trang)",
            ClientSize = new Size(900, 650),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(15, 23, 42),
            KeyPreview = true,
            Font = new Font("Segoe UI", 10f)
        };

        int currentIndex = 0;

        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(30, 41, 59)
        };
        var lblPage = new Label
        {
            Text = $"Trang 1 / {images.Count}",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var btnPrev = new Button
        {
            Text = "◀", Dock = DockStyle.Left, Width = 60,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand
        };
        btnPrev.FlatAppearance.BorderSize = 0;
        var btnNext = new Button
        {
            Text = "▶", Dock = DockStyle.Right, Width = 60,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand
        };
        btnNext.FlatAppearance.BorderSize = 0;

        pnlTop.Controls.Add(lblPage);
        pnlTop.Controls.Add(btnNext);
        pnlTop.Controls.Add(btnPrev);

        var zoomPanel = new ZoomableImagePanel
        {
            Dock = DockStyle.Fill,
            Image = images[0],
            BackColor = Color.FromArgb(15, 23, 42)
        };

        void UpdatePage()
        {
            lblPage.Text = $"Trang {currentIndex + 1} / {images.Count}";
            zoomPanel.Image = images[currentIndex];
            zoomPanel.ZoomFactor = 1f;
            zoomPanel.PanOffset = PointF.Empty;
        }

        btnPrev.Click += (_, _) =>
        {
            if (currentIndex > 0) { currentIndex--; UpdatePage(); }
        };
        btnNext.Click += (_, _) =>
        {
            if (currentIndex < images.Count - 1) { currentIndex++; UpdatePage(); }
        };
        form.KeyDown += (_, args) =>
        {
            switch (args.KeyCode)
            {
                case Keys.Escape: form.Close(); break;
                case Keys.Left:
                    if (currentIndex > 0) { currentIndex--; UpdatePage(); }
                    break;
                case Keys.Right:
                    if (currentIndex < images.Count - 1) { currentIndex++; UpdatePage(); }
                    break;
            }
        };

        form.Controls.Add(zoomPanel);
        form.Controls.Add(pnlTop);
        form.ShowDialog(this);
    }

    // ═══════════════════════════════════════════════
    //  RESTORE / DELETE
    // ═══════════════════════════════════════════════

    private void BtnRestore_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry == null) return;

        var images = _historyService.LoadBackupImages(entry);
        if (images.Count == 0)
        {
            MessageBox.Show("Không tìm thấy ảnh backup.", "DTScan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RestoredImages = images;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry == null) return;

        if (MessageBox.Show(
            $"Xóa bản sao lưu ngày {entry.Timestamp:dd/MM/yyyy HH:mm}?\n" +
            $"{entry.PageCount} trang · {FormatSize(entry.TotalSizeBytes)}",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _historyService.DeleteEntry(entry);
        RefreshList();
        OnSelectionChanged(null, EventArgs.Empty);
    }

    // ═══════════════════════════════════════════════
    //  UTILS
    // ═══════════════════════════════════════════════

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
