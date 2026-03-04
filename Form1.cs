using DTScan.Controls;
using DTScan.Forms;
using DTScan.Models;
using DTScan.Services;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DTScan;

public partial class Form1 : Form
{
    private readonly ProfileManager _profileManager = new();
    private readonly ScannerService _scannerService = new();
    private readonly SaveService _saveService = new();
    private readonly ScanHistoryService _scanHistoryService = new();
    private readonly List<Image> _originalImages = [];
    private readonly List<string?> _pageSessionIds = [];
    private readonly Dictionary<string, int> _sessionPageCounts = new();
    private int _nextBatchStart;
    private int _thumbnailWidth = 200;
    private System.Windows.Forms.Timer? _zoomDebounce;

    // Ghi nhớ trạng thái zoom/pan của preview trong Save dialog
    private static float _savePreviewZoom = 1f;
    private static PointF _savePreviewPan = PointF.Empty;

    private enum PackAction { Save, Skip, Cancel }

    public Form1()
    {
        InitializeComponent();
        WireEvents();
        _profileManager.Load();
        RefreshProfileCombo();
        UpdateUI();
        ApplyTheme();
    }

    // ═══════════════════════════════════════════════
    //  KEYBOARD SHORTCUTS
    // ═══════════════════════════════════════════════

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var shortcuts = _profileManager.ActiveProfile.Shortcuts;

        foreach (var (action, shortcutName) in shortcuts)
        {
            if (string.IsNullOrEmpty(shortcutName) ||
                string.Equals(shortcutName, "None", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Enum.TryParse<Keys>(shortcutName, true, out var key) && keyData == key)
            {
                ExecuteShortcutAction(action);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ExecuteShortcutAction(string action)
    {
        switch (action)
        {
            case "Scan": btnScan.PerformClick(); break;
            case "Import": btnImport.PerformClick(); break;
            case "Save": btnSave.PerformClick(); break;
            case "SelectAll": SetAllSelection(true); break;
            case "DeselectAll": SetAllSelection(false); break;
            case "AutoSelect": AutoSelectBatch(); break;
            case "RotateLeft": RotateSelected(RotateFlipType.Rotate270FlipNone); break;
            case "RotateRight": RotateSelected(RotateFlipType.Rotate90FlipNone); break;
            case "Delete": BtnDelete_Click(null, EventArgs.Empty); break;
            case "QuickLabel": BtnQuickLabel_Click(null, EventArgs.Empty); break;
            case "Settings": BtnSettings_Click(null, EventArgs.Empty); break;
        }
    }

    private string ShortcutSuffix(string action)
    {
        var shortcuts = _profileManager.ActiveProfile.Shortcuts;
        if (shortcuts.TryGetValue(action, out var key) &&
            !string.IsNullOrEmpty(key) &&
            !string.Equals(key, "None", StringComparison.OrdinalIgnoreCase))
            return $"  [{key}]";
        return "";
    }

    // ═══════════════════════════════════════════════
    //  EVENT WIRING
    // ═══════════════════════════════════════════════

    private void WireEvents()
    {
        btnScan.Click += BtnScan_Click;
        btnImport.Click += BtnImport_Click;
        btnSave.Click += BtnSave_Click;
        btnSelectAll.Click += (_, _) => SetAllSelection(true);
        btnDeselectAll.Click += (_, _) => SetAllSelection(false);
        btnAutoSelect.Click += BtnAutoSelect_Click;
        btnRotateLeft.Click += (_, _) => RotateSelected(RotateFlipType.Rotate270FlipNone);
        btnRotateRight.Click += (_, _) => RotateSelected(RotateFlipType.Rotate90FlipNone);
        btnDelete.Click += BtnDelete_Click;
        btnQuickLabel.Click += BtnQuickLabel_Click;
        btnHistory.Click += BtnHistory_Click;
        btnSettings.Click += BtnSettings_Click;
        cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
        trkZoom.ValueChanged += (_, _) => ApplyZoom();
        btnZoomIn.Click += (_, _) => { trkZoom.Value = Math.Min(trkZoom.Maximum, trkZoom.Value + 20); };
        btnZoomOut.Click += (_, _) => { trkZoom.Value = Math.Max(trkZoom.Minimum, trkZoom.Value - 20); };

        // Ctrl+Wheel trên vùng thumbnail → zoom
        var wheelFilter = new CtrlWheelZoomFilter(this);
        Application.AddMessageFilter(wheelFilter);
        FormClosed += (_, _) => Application.RemoveMessageFilter(wheelFilter);

        // Kéo thả file ảnh vào form
        AllowDrop = true;
        DragEnter += OnFileDragEnter;
        DragDrop += OnFileDragDrop;

        flpPages.AllowDrop = true;
        flpPages.DragEnter += OnFileDragEnter;
        flpPages.DragDrop += OnFileDragDrop;

        pnlEmptyState.AllowDrop = true;
        pnlEmptyState.DragEnter += OnFileDragEnter;
        pnlEmptyState.DragDrop += OnFileDragDrop;
    }

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif", ".pdf"
    };

    private void OnFileDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            && e.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.Any(f => _supportedExtensions.Contains(Path.GetExtension(f))))
        {
            e.Effect = DragDropEffects.Copy;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void OnFileDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;

        int count = ImportFiles(files.OrderBy(f => f));

        if (count > 0)
            SetStatus($"Imported {count} page(s) via drag-drop",
                Color.FromArgb(16, 185, 129));
    }

    // ═══════════════════════════════════════════════
    //  FILE IMPORT (ảnh + PDF)
    // ═══════════════════════════════════════════════

    private int ImportFiles(IEnumerable<string> filePaths)
    {
        int count = 0;
        int dpi = _profileManager.ActiveProfile.ScanDpi;

        foreach (var file in filePaths)
        {
            if (!_supportedExtensions.Contains(Path.GetExtension(file)))
                continue;
            try
            {
                if (Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var page in LoadPdfPages(file, dpi))
                    {
                        AddPage(page);
                        count++;
                    }
                }
                else
                {
                    using var img = Image.FromFile(file);
                    AddPage(new Bitmap(img));
                    count++;
                }
            }
            catch { /* skip invalid files */ }
        }

        return count;
    }

    private static List<Image> LoadPdfPages(string filePath, int dpi = 300)
    {
        return Task.Run(async () =>
        {
            var result = new List<Image>();
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
            var doc = await PdfDocument.LoadFromFileAsync(file);

            for (uint i = 0; i < doc.PageCount; i++)
            {
                using var page = doc.GetPage(i);
                var options = new PdfPageRenderOptions
                {
                    DestinationWidth = (uint)(page.Size.Width * dpi / 96),
                    DestinationHeight = (uint)(page.Size.Height * dpi / 96)
                };

                using var stream = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(stream, options);

                // Đọc rendered image từ WinRT stream → byte[] → Bitmap
                stream.Seek(0);
                var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
                await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);

                using var reader = DataReader.FromBuffer(buffer);
                var bytes = new byte[buffer.Length];
                reader.ReadBytes(bytes);

                using var ms = new MemoryStream(bytes);
                using var tmp = new Bitmap(ms);
                result.Add(new Bitmap(tmp));
            }

            return result;
        }).GetAwaiter().GetResult();
    }

    private sealed class CtrlWheelZoomFilter(Form1 owner) : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if ((Control.ModifierKeys & Keys.Control) == 0) return false;

            var pt = owner.flpPages.PointToClient(Cursor.Position);
            if (!owner.flpPages.ClientRectangle.Contains(pt)) return false;

            int delta = (short)((long)m.WParam >> 16);
            int step = delta > 0 ? 20 : -20;
            owner.trkZoom.Value = Math.Clamp(
                owner.trkZoom.Value + step,
                owner.trkZoom.Minimum,
                owner.trkZoom.Maximum);
            return true;
        }
    }

    // ═══════════════════════════════════════════════
    //  SCAN  (Flatbed / Feeder / Duplex)
    // ═══════════════════════════════════════════════

    private void BtnScan_Click(object? sender, EventArgs e)
    {
        var profile = _profileManager.ActiveProfile;
        SetStatus("Scanning…", Color.FromArgb(245, 158, 11));
        btnScan.Enabled = false;

        try
        {
            var pages = _scannerService.Scan(
                profile.ScanSource, profile.ScanDpi, profile.ColorMode,
                profile.ScanDriver, this.Handle, profile.PreferredScanner);

            if (pages.Count > 0)
            {
                var historyEntry = _scanHistoryService.SaveBackup(pages, profile);
                foreach (var page in pages)
                    AddPage(page, historyEntry.Id);

                SetStatus($"Scanned {pages.Count} page(s) — {profile.ScanSource}",
                    Color.FromArgb(16, 185, 129));

                if (profile.AutoSave)
                    PerformAutoSave(pages);
            }
            else
            {
                SetStatus("Scan cancelled", Color.FromArgb(148, 163, 184));
            }
        }
        catch (Exception ex)
        {
            SetStatus("Scan failed", Color.FromArgb(239, 68, 68));
            MessageBox.Show(ex.Message, "Scan Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnScan.Enabled = true;
        }
    }

    // ═══════════════════════════════════════════════
    //  IMPORT
    // ═══════════════════════════════════════════════

    private void BtnImport_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import Images",
            Filter = "Supported Files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif;*.pdf"
                   + "|Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif"
                   + "|PDF Files|*.pdf"
                   + "|All Files|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        int count = ImportFiles(dlg.FileNames);
        SetStatus($"Imported {count} page(s)", Color.FromArgb(16, 185, 129));
    }

    // ═══════════════════════════════════════════════
    //  SAVE — PACK-BY-PACK WORKFLOW
    //  Hiện preview trang đầu pack, nhập label, lưu
    // ═══════════════════════════════════════════════

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var profile = _profileManager.ActiveProfile;
        int batchSize = profile.PagesPerBatch;

        if (GetThumbnails().Count == 0) return;

        // Nếu user đã chọn tay → lần đầu dùng đúng selection đó
        bool hadSelection = GetThumbnails().Any(t => t.IsSelected);
        int cursor = hadSelection ? GetSelectedIndices()[0] : _nextBatchStart;

        if (!hadSelection && cursor >= GetThumbnails().Count)
            cursor = 0;

        bool useManualSelection = hadSelection;

        while (true)
        {
            var thumbnails = GetThumbnails();
            if (thumbnails.Count == 0 || cursor >= thumbnails.Count) break;

            if (useManualSelection)
            {
                // Lần đầu: giữ nguyên selection của user (Select All, v.v.)
                useManualSelection = false;
            }
            else
            {
                // Các lần sau: auto-batch theo PagesPerBatch
                foreach (var t in thumbnails) t.IsSelected = false;
                int end = Math.Min(cursor + batchSize, thumbnails.Count);
                for (int i = cursor; i < end; i++)
                    thumbnails[i].IsSelected = true;
                UpdateUI();
            }

            var indices = GetSelectedIndices();
            if (indices.Count == 0) break;

            // Hiện dialog Save Pack với zoomed preview trang đầu
            var firstImage = _originalImages[indices[0]];
            int firstPage = indices[0] + 1;
            int lastPage = indices[^1] + 1;

            var (action, label, deleteAfter) = ShowSavePackDialog(
                firstImage, indices.Count, firstPage, lastPage, profile);

            if (action == PackAction.Cancel) break;

            if (action == PackAction.Save)
            {
                try
                {
                    var images = indices.Select(i => _originalImages[i]).ToList();
                    var labels = Enumerable.Repeat(label, images.Count).ToList();

                    // Check for file name conflict
                    string? resolvedFilePath = null;
                    var expectedPath = _saveService.BuildFirstTargetPath(profile, label);
                    if (File.Exists(expectedPath))
                    {
                        var resolution = ShowDuplicateFileDialog(expectedPath, firstImage);
                        if (resolution == null) break; // User cancelled

                        if (resolution.Value.renameExistingTo != null)
                            File.Move(expectedPath, resolution.Value.renameExistingTo);

                        resolvedFilePath = resolution.Value.newFilePath;
                    }

                    var path = _saveService.SavePages(
                        images, profile, labels, 1, resolvedFilePath);
                    _profileManager.Save();

                    string range = firstPage == lastPage
                        ? $"trang {firstPage}" : $"trang {firstPage}–{lastPage}";
                    SetStatus($"✓ Saved {range} [{label}] → {path}",
                        Color.FromArgb(16, 185, 129));

                    if (deleteAfter)
                        DeletePagesByIndices(indices);
                    else
                        cursor = indices[^1] + 1;
                }
                catch (Exception ex)
                {
                    SetStatus("Save failed", Color.FromArgb(239, 68, 68));
                    MessageBox.Show(ex.Message, "Save Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                }
            }
            else // Skip
            {
                cursor = indices[^1] + 1;
            }

            if (!profile.AutoSelectNextBatch) break;
        }

        _nextBatchStart = cursor;
        foreach (var t in GetThumbnails()) t.IsSelected = false;
        UpdateUI();
    }

    // ═══════════════════════════════════════════════
    //  SAVE PACK DIALOG
    //  Bên trái: zoom trang đầu để đọc số giấy khám
    //  Bên phải: nhập label, thông tin, preview tên file
    // ═══════════════════════════════════════════════

    private (PackAction action, string label, bool deleteAfter) ShowSavePackDialog(
        Image firstPageImage, int pageCount, int firstPage, int lastPage,
        UserProfile profile)
    {
        var action = PackAction.Cancel;
        string label = "";
        bool deleteAfter = profile.DeleteAfterSave;

        string rangeText = firstPage == lastPage
            ? $"Trang {firstPage} (1 trang)"
            : $"Trang {firstPage}–{lastPage} ({pageCount} trang)";

        using var dlg = new Form
        {
            Text = $"💾 Lưu Pack — {rangeText}",
            ClientSize = new Size(740, 470),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 10f),
            KeyPreview = true
        };

        // ── Bên trái: zoomable preview trang đầu pack ──
        var pnlPreview = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(370, 390),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        var zoomPreview = new ZoomableImagePanel
        {
            Dock = DockStyle.Fill,
            Image = firstPageImage,
            ZoomFactor = _savePreviewZoom,
            PanOffset = _savePreviewPan,
            BackColor = Color.White
        };
        pnlPreview.Controls.Add(zoomPreview);
        dlg.Controls.Add(pnlPreview);

        var lblPreviewHint = new Label
        {
            Text = $"↑ Trang {firstPage} — Scroll zoom · Kéo di chuyển · Double-click reset",
            Location = new Point(16, 412), Size = new Size(370, 36),
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
        };
        zoomPreview.ZoomChanged += (_, _) =>
        {
            int pct = (int)(zoomPreview.ZoomFactor * 100);
            lblPreviewHint.Text = $"↑ Trang {firstPage} — Zoom: {pct}%";
        };
        dlg.FormClosing += (_, _) =>
        {
            _savePreviewZoom = zoomPreview.ZoomFactor;
            _savePreviewPan = zoomPreview.PanOffset;
        };
        dlg.Controls.Add(lblPreviewHint);

        // ── Bên phải: nhập label + thông tin ──
        int rx = 406, rw = 318;
        int ry = 16;

        dlg.Controls.Add(new Label
        {
            Text = "Nhãn / Số giấy khám:",
            Location = new Point(rx, ry), Size = new Size(rw, 24),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        });
        ry += 30;

        var txtLabel = new TextBox
        {
            Location = new Point(rx, ry), Size = new Size(rw, 46),
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(99, 102, 241),
            TextAlign = HorizontalAlignment.Center,
            PlaceholderText = "Nhập số…"
        };
        dlg.Controls.Add(txtLabel);
        ry += 56;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(rx, ry), Size = new Size(rw, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        ry += 12;

        AddInfoLabel(dlg, rx, ref ry, $"📄  {rangeText}");
        AddInfoLabel(dlg, rx, ref ry, $"📁  Format: {profile.DefaultFormat}");

        var subFolder = PlaceholderResolver.Resolve(profile.SubFolderPattern, profile);
        var savePath = Path.Combine(profile.SavePath, subFolder);
        AddInfoLabel(dlg, rx, ref ry, $"📂  {savePath}");
        ry += 4;

        dlg.Controls.Add(new Label
        {
            Text = "Tên file:", Location = new Point(rx, ry), AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9f)
        });
        ry += 20;

        var lblFileName = new Label
        {
            Location = new Point(rx, ry), Size = new Size(rw, 38),
            ForeColor = Color.FromArgb(59, 130, 246),
            Font = new Font("Consolas", 9.5f),
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(8, 6, 8, 6),
            AutoEllipsis = true
        };
        dlg.Controls.Add(lblFileName);
        ry += 46;

        void UpdateFilePreview()
        {
            string lbl = txtLabel.Text.Trim();
            if (string.IsNullOrEmpty(lbl)) lbl = "___";
            var tmp = new UserProfile
            {
                Name = profile.Name, Counter = profile.Counter,
                DefaultFormat = profile.DefaultFormat
            };
            lblFileName.Text = PlaceholderResolver.Resolve(
                profile.FileNamePattern, tmp, firstPage, 1, lbl)
                + PlaceholderResolver.GetExtension(profile.DefaultFormat);
        }
        txtLabel.TextChanged += (_, _) => UpdateFilePreview();
        UpdateFilePreview();

        var chkDelete = new CheckBox
        {
            Text = "🗑  Xóa các trang này sau khi lưu",
            Location = new Point(rx, ry), Size = new Size(rw, 24),
            Checked = profile.DeleteAfterSave,
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        dlg.Controls.Add(chkDelete);
        ry += 36;

        // ── Buttons ──
        var btnSave = new Button
        {
            Text = "💾  Lưu && Tiếp", Location = new Point(rx, ry),
            Size = new Size(138, 42),
            BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            action = PackAction.Save;
            label = txtLabel.Text.Trim();
            deleteAfter = chkDelete.Checked;
            dlg.DialogResult = DialogResult.OK;
        };
        dlg.Controls.Add(btnSave);

        var btnSkip = new Button
        {
            Text = "⏭ Bỏ qua", Location = new Point(rx + 146, ry),
            Size = new Size(90, 42),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        btnSkip.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnSkip.Click += (_, _) =>
        {
            action = PackAction.Skip;
            dlg.DialogResult = DialogResult.OK;
        };
        dlg.Controls.Add(btnSkip);

        var btnCancelDlg = new Button
        {
            Text = "✕ Dừng", Location = new Point(rx + 244, ry),
            Size = new Size(74, 42),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(239, 68, 68)
        };
        btnCancelDlg.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
        btnCancelDlg.Click += (_, _) =>
        {
            action = PackAction.Cancel;
            dlg.DialogResult = DialogResult.Cancel;
        };
        dlg.Controls.Add(btnCancelDlg);

        dlg.AcceptButton = btnSave;
        dlg.Shown += (_, _) => txtLabel.Focus();
        dlg.ShowDialog(this);

        return (action, label, deleteAfter);
    }

    private static void AddInfoLabel(Form parent, int x, ref int y, string text)
    {
        parent.Controls.Add(new Label
        {
            Text = text, Location = new Point(x, y),
            Size = new Size(318, 20), ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f), AutoEllipsis = true
        });
        y += 22;
    }

    // ═══════════════════════════════════════════════
    //  DUPLICATE FILE DIALOG
    //  Hiển thị file bị trùng, cho phép đổi tên hoặc ghi đè
    // ═══════════════════════════════════════════════

    private (string newFilePath, string? renameExistingTo)? ShowDuplicateFileDialog(
        string existingPath, Image newImage)
    {
        var dir = Path.GetDirectoryName(existingPath)!;
        var originalNameNoExt = Path.GetFileNameWithoutExtension(existingPath);
        var ext = Path.GetExtension(existingPath);
        var fileInfo = new FileInfo(existingPath);

        // Suggest unique name for the new file
        string suggestedNewName = originalNameNoExt;
        int sn = 2;
        while (File.Exists(Path.Combine(dir, suggestedNewName + ext)))
        {
            suggestedNewName = $"{originalNameNoExt}_{sn}";
            sn++;
        }

        (string newFilePath, string? renameExistingTo)? result = null;

        using var dlg = new Form
        {
            Text = "⚠  File đã tồn tại",
            ClientSize = new Size(900, 520),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 10f),
            KeyPreview = true
        };

        const int colW = 420, margin = 16;
        int lx = margin, rx = margin + colW + 28;

        // ═══════ LEFT COLUMN: Existing file ═══════
        int ly = 14;

        dlg.Controls.Add(new Label
        {
            Text = "📄  FILE HIỆN TẠI (trên đĩa)",
            Location = new Point(lx, ly), Size = new Size(colW, 26),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 88, 12)
        });
        ly += 28;

        var pnlExisting = new Panel
        {
            Location = new Point(lx, ly), Size = new Size(colW, 200),
            BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
        };

        Image? existingImage = null;
        try
        {
            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdfPages = LoadPdfPages(existingPath, 150);
                if (pdfPages.Count > 0) existingImage = pdfPages[0];
            }
            else
            {
                using var img = Image.FromFile(existingPath);
                existingImage = new Bitmap(img);
            }
        }
        catch { }

        if (existingImage != null)
        {
            pnlExisting.Controls.Add(new ZoomableImagePanel
            {
                Dock = DockStyle.Fill, Image = existingImage, BackColor = Color.White
            });
        }
        else
        {
            pnlExisting.Controls.Add(new Label
            {
                Text = "Không thể xem trước",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            });
        }
        dlg.Controls.Add(pnlExisting);
        ly += 204;

        dlg.Controls.Add(new Label
        {
            Text = "↑ Scroll zoom · Kéo di chuyển",
            Location = new Point(lx, ly), Size = new Size(colW, 18),
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });
        ly += 20;

        string sizeText = fileInfo.Length < 1024 * 1024
            ? $"{fileInfo.Length / 1024.0:F1} KB"
            : $"{fileInfo.Length / (1024.0 * 1024.0):F2} MB";
        dlg.Controls.Add(new Label
        {
            Text = $"📏  {sizeText}  ·  {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm}",
            Location = new Point(lx, ly), Size = new Size(colW, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f)
        });
        ly += 20;

        dlg.Controls.Add(new Label
        {
            Text = $"📂  {dir}",
            Location = new Point(lx, ly), Size = new Size(colW, 18),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8f), AutoEllipsis = true
        });
        ly += 24;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(lx, ly), Size = new Size(colW, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        ly += 10;

        dlg.Controls.Add(new Label
        {
            Text = "Đổi tên file hiện tại:",
            Location = new Point(lx, ly), Size = new Size(colW, 22),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        });
        ly += 24;

        var txtExistingName = new TextBox
        {
            Text = originalNameNoExt,
            Location = new Point(lx, ly), Size = new Size(colW, 30),
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(234, 88, 12)
        };
        dlg.Controls.Add(txtExistingName);
        ly += 34;

        dlg.Controls.Add(new Label
        {
            Text = $"Phần mở rộng:  {ext}",
            Location = new Point(lx, ly), AutoSize = true,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });

        var lblExistingStatus = new Label
        {
            Location = new Point(lx + 150, ly - 2), Size = new Size(colW - 150, 20),
            Font = new Font("Consolas", 8.5f),
            TextAlign = ContentAlignment.MiddleRight
        };
        dlg.Controls.Add(lblExistingStatus);
        ly += 24;

        // ═══════ RIGHT COLUMN: New file ═══════
        int ry = 14;

        dlg.Controls.Add(new Label
        {
            Text = "📄  FILE MỚI (đang lưu)",
            Location = new Point(rx, ry), Size = new Size(colW, 26),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(59, 130, 246)
        });
        ry += 28;

        var pnlNew = new Panel
        {
            Location = new Point(rx, ry), Size = new Size(colW, 200),
            BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
        };
        pnlNew.Controls.Add(new ZoomableImagePanel
        {
            Dock = DockStyle.Fill, Image = newImage, BackColor = Color.White
        });
        dlg.Controls.Add(pnlNew);
        ry += 204;

        dlg.Controls.Add(new Label
        {
            Text = "↑ Scroll zoom · Kéo di chuyển",
            Location = new Point(rx, ry), Size = new Size(colW, 18),
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });
        ry += 20;

        // Spacer to align with file info on the left
        ry += 40;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(rx, ry), Size = new Size(colW, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        ry += 10;

        dlg.Controls.Add(new Label
        {
            Text = "Đặt tên file mới:",
            Location = new Point(rx, ry), Size = new Size(colW, 22),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        });
        ry += 24;

        var txtNewName = new TextBox
        {
            Text = suggestedNewName,
            Location = new Point(rx, ry), Size = new Size(colW, 30),
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(59, 130, 246)
        };
        dlg.Controls.Add(txtNewName);
        ry += 34;

        dlg.Controls.Add(new Label
        {
            Text = $"Phần mở rộng:  {ext}",
            Location = new Point(rx, ry), AutoSize = true,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });

        var lblNewStatus = new Label
        {
            Location = new Point(rx + 150, ry - 2), Size = new Size(colW - 150, 20),
            Font = new Font("Consolas", 8.5f),
            TextAlign = ContentAlignment.MiddleRight
        };
        dlg.Controls.Add(lblNewStatus);
        ry += 24;

        // ═══════ Real-time validation ═══════
        void UpdateStatus()
        {
            var eName = txtExistingName.Text.Trim();
            var nName = txtNewName.Text.Trim();
            var ePath = Path.Combine(dir, eName + ext);
            var nPath = Path.Combine(dir, nName + ext);
            bool eChanged = !string.Equals(ePath, existingPath, StringComparison.OrdinalIgnoreCase);
            bool sameNames = !string.IsNullOrEmpty(eName) && !string.IsNullOrEmpty(nName)
                && string.Equals(eName, nName, StringComparison.OrdinalIgnoreCase);

            // Existing file status
            if (string.IsNullOrEmpty(eName))
            {
                lblExistingStatus.Text = "⚠ trống";
                lblExistingStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else if (sameNames)
            {
                lblExistingStatus.Text = "⚠ trùng tên";
                lblExistingStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else if (!eChanged)
            {
                lblExistingStatus.Text = "giữ nguyên";
                lblExistingStatus.ForeColor = Color.FromArgb(148, 163, 184);
            }
            else if (File.Exists(ePath))
            {
                lblExistingStatus.Text = "⚠ đã tồn tại";
                lblExistingStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else
            {
                lblExistingStatus.Text = "✓ " + eName + ext;
                lblExistingStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }

            // New file status
            bool newConflict = File.Exists(nPath) &&
                !(string.Equals(nPath, existingPath, StringComparison.OrdinalIgnoreCase) && eChanged);
            if (string.IsNullOrEmpty(nName))
            {
                lblNewStatus.Text = "⚠ trống";
                lblNewStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else if (sameNames)
            {
                lblNewStatus.Text = "⚠ trùng tên";
                lblNewStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else if (newConflict)
            {
                lblNewStatus.Text = "⚠ đã tồn tại";
                lblNewStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else
            {
                lblNewStatus.Text = "✓ " + nName + ext;
                lblNewStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }
        }
        txtExistingName.TextChanged += (_, _) => UpdateStatus();
        txtNewName.TextChanged += (_, _) => UpdateStatus();
        UpdateStatus();

        // ═══════ Buttons ═══════
        int by = Math.Max(ly, ry) + 10;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(margin, by), Size = new Size(900 - 2 * margin, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        by += 10;

        var btnSaveNew = new Button
        {
            Text = "💾  Lưu",
            Location = new Point(rx, by), Size = new Size(130, 42),
            BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnSaveNew.FlatAppearance.BorderSize = 0;
        btnSaveNew.Click += (_, _) =>
        {
            var existingName = txtExistingName.Text.Trim();
            var newName = txtNewName.Text.Trim();
            if (string.IsNullOrEmpty(existingName) || string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Tên file không được để trống!",
                    "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existingFinalPath = Path.Combine(dir, existingName + ext);
            var newFinalPath = Path.Combine(dir, newName + ext);

            if (string.Equals(existingFinalPath, newFinalPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Hai file không thể có cùng tên!",
                    "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool existingChanged = !string.Equals(
                existingFinalPath, existingPath, StringComparison.OrdinalIgnoreCase);

            if (existingChanged && File.Exists(existingFinalPath))
            {
                MessageBox.Show($"Đã có file \"{existingName + ext}\" khác trong thư mục!",
                    "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (File.Exists(newFinalPath) &&
                !(string.Equals(newFinalPath, existingPath, StringComparison.OrdinalIgnoreCase)
                    && existingChanged))
            {
                MessageBox.Show($"Đã có file \"{newName + ext}\" trong thư mục!",
                    "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            result = (newFinalPath, existingChanged ? existingFinalPath : null);
            dlg.DialogResult = DialogResult.OK;
        };
        dlg.Controls.Add(btnSaveNew);

        var btnOverwrite = new Button
        {
            Text = "⚠ Ghi đè",
            Location = new Point(rx + 138, by), Size = new Size(100, 42),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(254, 243, 199),
            ForeColor = Color.FromArgb(146, 64, 14)
        };
        btnOverwrite.FlatAppearance.BorderColor = Color.FromArgb(251, 191, 36);
        btnOverwrite.Click += (_, _) =>
        {
            using var pwDlg = new Form
            {
                Text = "🔒  Xác nhận ghi đè",
                ClientSize = new Size(340, 150),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(248, 250, 252),
                Font = new Font("Segoe UI", 10f)
            };
            pwDlg.Controls.Add(new Label
            {
                Text = "Nhập mật khẩu để ghi đè file:",
                Location = new Point(16, 16), AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105)
            });
            var txtPw = new TextBox
            {
                Location = new Point(16, 44), Size = new Size(305, 30),
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 11f)
            };
            pwDlg.Controls.Add(txtPw);
            var btnConfirm = new Button
            {
                Text = "Xác nhận", Location = new Point(120, 90), Size = new Size(100, 36),
                BackColor = Color.FromArgb(234, 88, 12), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (_, _) =>
            {
                if (txtPw.Text == "dthong")
                {
                    pwDlg.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("Sai mật khẩu!", "DTScan",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtPw.Clear();
                    txtPw.Focus();
                }
            };
            pwDlg.Controls.Add(btnConfirm);
            var btnCancelPw = new Button
            {
                Text = "Hủy", Location = new Point(228, 90), Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            btnCancelPw.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCancelPw.Click += (_, _) => { pwDlg.DialogResult = DialogResult.Cancel; };
            pwDlg.Controls.Add(btnCancelPw);
            pwDlg.AcceptButton = btnConfirm;
            pwDlg.Shown += (_, _) => txtPw.Focus();

            if (pwDlg.ShowDialog(dlg) == DialogResult.OK)
            {
                result = (existingPath, null);
                dlg.DialogResult = DialogResult.OK;
            }
        };
        dlg.Controls.Add(btnOverwrite);

        var btnCancelDup = new Button
        {
            Text = "✕ Hủy",
            Location = new Point(rx + 246, by), Size = new Size(80, 42),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(239, 68, 68)
        };
        btnCancelDup.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
        btnCancelDup.Click += (_, _) =>
        {
            result = null;
            dlg.DialogResult = DialogResult.Cancel;
        };
        dlg.Controls.Add(btnCancelDup);

        dlg.ClientSize = new Size(900, by + 54);
        dlg.AcceptButton = btnSaveNew;
        dlg.Shown += (_, _) => { txtNewName.Focus(); txtNewName.SelectAll(); };
        dlg.ShowDialog(this);

        existingImage?.Dispose();

        return result;
    }

    private void PerformAutoSave(List<Image> pages)
    {
        var profile = _profileManager.ActiveProfile;
        try
        {
            var resultPath = _saveService.SavePages(pages, profile);
            _profileManager.Save();
            SetStatus($"Auto-saved {pages.Count} page(s) → {resultPath}",
                Color.FromArgb(16, 185, 129));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Auto-save failed: {ex.Message}", "DTScan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ═══════════════════════════════════════════════
    //  LABELING
    // ═══════════════════════════════════════════════

    private void BtnQuickLabel_Click(object? sender, EventArgs e)
    {
        var thumbnails = GetThumbnails();
        if (thumbnails.Count == 0)
        {
            MessageBox.Show("No pages available to label.",
                "DTScan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ShowQuickLabelDialog(thumbnails);
    }

    private void ShowQuickLabelDialog(List<PageThumbnailControl> thumbnails)
    {
        using var dlg = new Form
        {
            Text = "Quick Label — Gán nhãn hàng loạt",
            Size = new Size(520, 560),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 10f)
        };

        var lblHeader = new Label
        {
            Text = "Gán nhãn / số cho từng trang scan.\n"
                 + "Ví dụ: giấy khám scan không theo thứ tự → nhập 03, 01, 02...",
            Dock = DockStyle.Top, Height = 52, Padding = new Padding(16, 10, 16, 0),
            ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9f)
        };
        dlg.Controls.Add(lblHeader);

        var pnlFill = new Panel
        {
            Dock = DockStyle.Top, Height = 44,
            Padding = new Padding(16, 6, 16, 6),
            BackColor = Color.FromArgb(239, 246, 255)
        };
        var nudStart = new NumericUpDown { Location = new Point(135, 6), Size = new Size(70, 28), Minimum = 0, Maximum = 9999, Value = 1 };
        var nudStep = new NumericUpDown { Location = new Point(260, 6), Size = new Size(60, 28), Minimum = 1, Maximum = 100, Value = 1 };
        var nudPad = new NumericUpDown { Location = new Point(382, 6), Size = new Size(50, 28), Minimum = 1, Maximum = 6, Value = 2 };
        var btnFill = new Button
        {
            Text = "Fill ↓", Location = new Point(440, 4), Size = new Size(55, 30),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White
        };
        btnFill.FlatAppearance.BorderSize = 0;
        pnlFill.Controls.AddRange([
            new Label { Text = "Điền nhanh từ:", Location = new Point(8, 10), AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105) },
            nudStart,
            new Label { Text = "bước:", Location = new Point(215, 10), AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105) },
            nudStep,
            new Label { Text = "digits:", Location = new Point(330, 10), AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105) },
            nudPad, btnFill
        ]);
        dlg.Controls.Add(pnlFill);

        var pnlList = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 8, 16, 8) };
        dlg.Controls.Add(pnlList);

        var labelInputs = new List<TextBox>();
        int y = 0;
        foreach (var thumb in thumbnails)
        {
            var row = new Panel { Location = new Point(0, y), Size = new Size(460, 34) };
            row.Controls.Add(new Label
            {
                Text = $"Trang {thumb.PageNumber}:", Location = new Point(0, 6),
                Size = new Size(80, 22), ForeColor = Color.FromArgb(71, 85, 105)
            });
            var txt = new TextBox
            {
                Text = thumb.DocumentLabel, Location = new Point(85, 2), Size = new Size(370, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 102, 241), PlaceholderText = "Nhãn / Số..."
            };
            row.Controls.Add(txt);
            labelInputs.Add(txt);
            pnlList.Controls.Add(row);
            y += 38;
        }

        btnFill.Click += (_, _) =>
        {
            int s = (int)nudStart.Value, st = (int)nudStep.Value, p = (int)nudPad.Value;
            for (int i = 0; i < labelInputs.Count; i++)
                labelInputs[i].Text = (s + i * st).ToString($"D{p}");
        };

        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 55 };
        var btnApply = new Button
        {
            Text = "✓  Apply", Size = new Size(120, 38), Location = new Point(260, 8),
            BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnApply.FlatAppearance.BorderSize = 0;
        btnApply.Click += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
        var btnCancelQL = new Button
        {
            Text = "Cancel", Size = new Size(100, 38), Location = new Point(390, 8),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnCancelQL.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancelQL.Click += (_, _) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
        pnlBottom.Controls.AddRange([btnApply, btnCancelQL]);
        dlg.Controls.Add(pnlBottom);

        dlg.Controls.SetChildIndex(pnlBottom, 0);
        dlg.Controls.SetChildIndex(pnlList, 0);
        dlg.Controls.SetChildIndex(pnlFill, 0);
        dlg.Controls.SetChildIndex(lblHeader, 0);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            for (int i = 0; i < thumbnails.Count; i++)
                thumbnails[i].DocumentLabel = labelInputs[i].Text;
            SetStatus("Labels applied", Color.FromArgb(16, 185, 129));
        }
    }

    // ═══════════════════════════════════════════════
    //  SCAN HISTORY
    // ═══════════════════════════════════════════════

    private void BtnHistory_Click(object? sender, EventArgs e)
    {
        using var form = new ScanHistoryForm(_scanHistoryService);
        if (form.ShowDialog(this) == DialogResult.OK && form.RestoredImages is { Count: > 0 })
        {
            foreach (var img in form.RestoredImages)
                AddPage(img);
            SetStatus($"Khôi phục {form.RestoredImages.Count} trang từ lịch sử",
                Color.FromArgb(16, 185, 129));
        }
    }

    // ═══════════════════════════════════════════════
    //  SELECTION
    // ═══════════════════════════════════════════════

    private void SetAllSelection(bool selected)
    {
        foreach (var ctrl in GetThumbnails())
            ctrl.IsSelected = selected;
        UpdateUI();
    }

    private void BtnAutoSelect_Click(object? sender, EventArgs e) => AutoSelectBatch();

    private void AutoSelectBatch()
    {
        var thumbnails = GetThumbnails();
        int batchSize = _profileManager.ActiveProfile.PagesPerBatch;

        foreach (var t in thumbnails) t.IsSelected = false;

        if (_nextBatchStart >= thumbnails.Count)
            _nextBatchStart = 0;

        int end = Math.Min(_nextBatchStart + batchSize, thumbnails.Count);
        for (int i = _nextBatchStart; i < end; i++)
            thumbnails[i].IsSelected = true;

        UpdateUI();
    }

    // ═══════════════════════════════════════════════
    //  ROTATE / DELETE
    // ═══════════════════════════════════════════════

    private void RotateSelected(RotateFlipType rotation)
    {
        var thumbnails = GetThumbnails();
        for (int i = 0; i < thumbnails.Count; i++)
        {
            if (!thumbnails[i].IsSelected) continue;
            _originalImages[i].RotateFlip(rotation);
            thumbnails[i].PageImage = CreateThumbnail(_originalImages[i]);
            thumbnails[i].Invalidate();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var indices = GetSelectedIndices();
        if (indices.Count == 0) return;

        if (MessageBox.Show($"Delete {indices.Count} page(s)?", "Confirm Delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        DeletePagesByIndices(indices);
        UpdateUI();
    }

    private void DeletePagesByIndices(List<int> indices)
    {
        for (int i = indices.Count - 1; i >= 0; i--)
        {
            int idx = indices[i];

            var sessionId = _pageSessionIds[idx];
            if (sessionId != null && _sessionPageCounts.TryGetValue(sessionId, out var sessionRemaining))
            {
                sessionRemaining--;
                if (sessionRemaining <= 0)
                {
                    _scanHistoryService.DeleteEntry(sessionId);
                    _sessionPageCounts.Remove(sessionId);
                }
                else
                {
                    _sessionPageCounts[sessionId] = sessionRemaining;
                }
            }
            _pageSessionIds.RemoveAt(idx);

            _originalImages[idx].Dispose();
            _originalImages.RemoveAt(idx);
            var ctrl = (PageThumbnailControl)flpPages.Controls[idx];
            flpPages.Controls.RemoveAt(idx);
            ctrl.Dispose();
        }

        var remaining = GetThumbnails();
        for (int i = 0; i < remaining.Count; i++)
            remaining[i].PageNumber = i + 1;

        _nextBatchStart = Math.Min(_nextBatchStart, _originalImages.Count);
    }

    // ═══════════════════════════════════════════════
    //  PROFILE
    // ═══════════════════════════════════════════════

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var editor = new ProfileEditorForm(_profileManager, _scannerService);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            RefreshProfileCombo();
            UpdateUI();
            ApplyTheme();
        }
    }

    private void CmbProfile_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbProfile.SelectedItem is UserProfile p)
        {
            _profileManager.ActiveProfile = p;
            _profileManager.Save();
            _nextBatchStart = 0;
            UpdateUI();
            ApplyTheme();
        }
    }

    private void RefreshProfileCombo()
    {
        cmbProfile.SelectedIndexChanged -= CmbProfile_SelectedIndexChanged;
        cmbProfile.Items.Clear();
        cmbProfile.DisplayMember = "Name";
        foreach (var p in _profileManager.Profiles)
            cmbProfile.Items.Add(p);
        cmbProfile.SelectedItem = _profileManager.ActiveProfile;
        cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
    }

    // ═══════════════════════════════════════════════
    //  PAGE MANAGEMENT
    // ═══════════════════════════════════════════════

    private void AddPage(Image original, string? sessionId = null)
    {
        _originalImages.Add(original);
        _pageSessionIds.Add(sessionId);
        if (sessionId != null)
        {
            _sessionPageCounts.TryAdd(sessionId, 0);
            _sessionPageCounts[sessionId]++;
        }
        int pageNum = _originalImages.Count;
        int thumbH = (int)(_thumbnailWidth * 1.5);

        var thumb = new PageThumbnailControl
        {
            Size = new Size(_thumbnailWidth, thumbH),
            PageImage = CreateThumbnail(original, _thumbnailWidth - 16, thumbH - 72),
            PageNumber = pageNum
        };
        thumb.SelectionChanged += (_, _) => UpdateUI();
        thumb.DoubleClick += (_, _) => ShowPreview(original);

        flpPages.Controls.Add(thumb);
        UpdateUI();
    }

    private void ApplyZoom()
    {
        _thumbnailWidth = trkZoom.Value;
        lblZoomValue.Text = $"{_thumbnailWidth}px";
        int thumbH = (int)(_thumbnailWidth * 1.5);

        // Resize controls immediately (cheap — no bitmap work)
        flpPages.SuspendLayout();
        var thumbnails = GetThumbnails();
        for (int i = 0; i < thumbnails.Count; i++)
            thumbnails[i].Size = new Size(_thumbnailWidth, thumbH);
        flpPages.ResumeLayout();

        // Debounce the expensive thumbnail regeneration
        _zoomDebounce?.Stop();
        _zoomDebounce ??= new System.Windows.Forms.Timer { Interval = 150 };
        _zoomDebounce.Tick -= OnZoomDebounce;
        _zoomDebounce.Tick += OnZoomDebounce;
        _zoomDebounce.Start();
    }

    private void OnZoomDebounce(object? sender, EventArgs e)
    {
        _zoomDebounce?.Stop();
        int thumbH = (int)(_thumbnailWidth * 1.5);
        var thumbnails = GetThumbnails();
        for (int i = 0; i < thumbnails.Count; i++)
        {
            thumbnails[i].PageImage?.Dispose();
            thumbnails[i].PageImage = CreateThumbnail(
                _originalImages[i], _thumbnailWidth - 16, thumbH - 72);
            thumbnails[i].Invalidate();
        }
    }

    private static Image CreateThumbnail(Image original, int maxW = 184, int maxH = 228)
    {
        // Render tại 2x kích thước hiển thị để thumbnail luôn sắc nét
        int renderW = maxW * 2;
        int renderH = maxH * 2;
        float scale = Math.Min((float)renderW / original.Width,
                               (float)renderH / original.Height);
        // Giới hạn scale ≤ 1 để không phóng to quá ảnh gốc
        scale = Math.Min(scale, 1f);
        int w = Math.Max(1, (int)(original.Width * scale));
        int h = Math.Max(1, (int)(original.Height * scale));
        var bmp = new Bitmap(w, h);
        bmp.SetResolution(original.HorizontalResolution, original.VerticalResolution);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(original, 0, 0, w, h);
        return bmp;
    }

    private List<int> GetSelectedIndices()
    {
        var thumbnails = GetThumbnails();
        var indices = new List<int>();
        for (int i = 0; i < thumbnails.Count; i++)
            if (thumbnails[i].IsSelected)
                indices.Add(i);
        return indices;
    }

    private List<PageThumbnailControl> GetThumbnails() =>
        flpPages.Controls.OfType<PageThumbnailControl>().ToList();

    // ═══════════════════════════════════════════════
    //  THEME
    // ═══════════════════════════════════════════════

    private void ApplyTheme()
    {
        var theme = ThemeColors.Get(_profileManager.ActiveProfile.ThemeColor);

        // Scan button (accent)
        btnScan.BackColor = theme.Accent;
        btnScan.FlatAppearance.MouseOverBackColor = theme.AccentHover;
        btnScan.FlatAppearance.MouseDownBackColor = theme.AccentDown;

        // Right sidebar accent border
        pnlRightBorder.BackColor = theme.Accent;

        // Profile header
        pnlProfileHeader.BackColor = theme.AccentDark;
        lblCurrentUser.ForeColor = theme.AccentLight;

        // Settings button
        btnSettings.BackColor = theme.AccentDark;
        btnSettings.ForeColor = theme.AccentLight;
        btnSettings.FlatAppearance.MouseOverBackColor = theme.AccentDarkHover;
        btnSettings.FlatAppearance.MouseDownBackColor = theme.AccentDarkDown;
    }

    // ═══════════════════════════════════════════════
    //  UI UPDATE
    // ═══════════════════════════════════════════════

    private void UpdateUI()
    {
        var thumbnails = GetThumbnails();
        int total = thumbnails.Count;
        int selected = thumbnails.Count(t => t.IsSelected);
        int labeled = thumbnails.Count(t => !string.IsNullOrWhiteSpace(t.DocumentLabel));
        var profile = _profileManager.ActiveProfile;

        pnlEmptyState.Visible = total == 0;
        flpPages.Visible = total > 0;

        tslPageCount.Text = $"Pages: {total}";
        tslSelected.Text = $"Selected: {selected}";
        tslBatch.Text = $"Batch: {profile.PagesPerBatch} | Labeled: {labeled}";
        tslUser.Text = $"User: {profile.Name}";

        btnAutoSelect.Text = $"⚡   Auto Select ({profile.PagesPerBatch}){ShortcutSuffix("AutoSelect")}";

        btnScan.Text = (profile.ScanSource switch
        {
            ScanSource.Feeder => "📷   Scan (Feeder)",
            ScanSource.Duplex => "📷   Scan (Duplex)",
            _ => "📷   Scan"
        }) + ShortcutSuffix("Scan");

        btnImport.Text = $"📂   Import{ShortcutSuffix("Import")}";
        btnSave.Text = $"💾   Save{ShortcutSuffix("Save")}";
        btnSelectAll.Text = $"☑   Select All{ShortcutSuffix("SelectAll")}";
        btnDeselectAll.Text = $"☐   Deselect All{ShortcutSuffix("DeselectAll")}";
        btnRotateLeft.Text = $"↶   Rotate Left{ShortcutSuffix("RotateLeft")}";
        btnRotateRight.Text = $"↷   Rotate Right{ShortcutSuffix("RotateRight")}";
        btnDelete.Text = $"🗑   Delete{ShortcutSuffix("Delete")}";
        btnQuickLabel.Text = $"📝   Quick Label{ShortcutSuffix("QuickLabel")}";
        btnSettings.Text = $"⚙   Settings{ShortcutSuffix("Settings")}";

        // ── Profile info panel ──
        lblProfileFormat.Text = $"📁  {profile.DefaultFormat} · {profile.ScanDpi} DPI · {profile.ColorMode}";

        string source = profile.ScanSource switch
        {
            ScanSource.Feeder => "Feeder",
            ScanSource.Duplex => "Duplex",
            _ => "Flatbed"
        };
        lblProfileBatch.Text = $"📄  Batch: {profile.PagesPerBatch} · {source} · {profile.ScanDriver}";

        var subFolder = PlaceholderResolver.Resolve(profile.SubFolderPattern, profile);
        lblProfilePath.Text = $"📂  {Path.Combine(profile.SavePath, subFolder)}";

        var flags = new List<string>();
        if (profile.AutoSave) flags.Add("AutoSave");
        if (profile.DeleteAfterSave) flags.Add("AutoDelete");
        if (profile.AutoSelectNextBatch) flags.Add("AutoNext");
        lblProfileFlags.Text = flags.Count > 0 ? $"⚙  {string.Join(" · ", flags)}" : "";
    }

    private void SetStatus(string text, Color color)
    {
        tslStatus.Text = text;
        tslStatus.ForeColor = color;
    }

    // ═══════════════════════════════════════════════
    //  PREVIEW
    // ═══════════════════════════════════════════════

    private void ShowPreview(Image image)
    {
        using var form = new Form
        {
            Text = "DTScan — Preview",
            WindowState = FormWindowState.Maximized,
            BackColor = Color.FromArgb(15, 23, 42),
            KeyPreview = true
        };
        var zp = new ZoomableImagePanel
        {
            Dock = DockStyle.Fill,
            Image = image,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        form.KeyDown += (_, args) => { if (args.KeyCode == Keys.Escape) form.Close(); };
        form.Controls.Add(zp);
        form.ShowDialog(this);
    }
}
