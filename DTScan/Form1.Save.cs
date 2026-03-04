using DTScan.Controls;
using DTScan.Models;
using DTScan.Services;

namespace DTScan;

public partial class Form1
{
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
            using var firstImage = _pageStore.Load(indices[0]);
            int firstPage = indices[0] + 1;
            int lastPage = indices[^1] + 1;

            var (action, label, deleteAfter, customSavePath) = ShowSavePackDialog(
                firstImage, indices.Count, firstPage, lastPage, profile);

            if (action == PackAction.Cancel) break;

            if (action == PackAction.Save)
            {
                try
                {
                    string path;

                    if (customSavePath != null)
                    {
                        path = _saveService.SaveToPath(
                            i => _pageStore.Load(indices[i]),
                            indices.Count, customSavePath);
                    }
                    else
                    {
                        var labels = Enumerable.Repeat(label, indices.Count).ToList();

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

                        path = _saveService.SavePages(
                            i => _pageStore.Load(indices[i]),
                            indices.Count, profile, labels, 1, resolvedFilePath);
                        _profileManager.Save();
                    }

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

    private (PackAction action, string label, bool deleteAfter, string? customSavePath) ShowSavePackDialog(
        Image firstPageImage, int pageCount, int firstPage, int lastPage,
        UserProfile profile)
    {
        var action = PackAction.Cancel;
        string label = "";
        bool deleteAfter = profile.DeleteAfterSave;
        string? customSavePath = null;

        string rangeText = firstPage == lastPage
            ? $"Trang {firstPage} (1 trang)"
            : $"Trang {firstPage}–{lastPage} ({pageCount} trang)";

        using var dlg = new Form
        {
            Text = $"💾 Lưu Pack — {rangeText}",
            ClientSize = new Size(740, 470),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
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
            Location = new Point(16, 412),
            Size = new Size(370, 36),
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
            Location = new Point(rx, ry),
            Size = new Size(rw, 24),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        });
        ry += 30;

        var txtLabel = new TextBox
        {
            Location = new Point(rx, ry),
            Size = new Size(rw, 46),
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(99, 102, 241),
            TextAlign = HorizontalAlignment.Center,
            PlaceholderText = "Nhập số…"
        };
        dlg.Controls.Add(txtLabel);
        ry += 56;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(rx, ry),
            Size = new Size(rw, 1),
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
            Text = "Tên file:",
            Location = new Point(rx, ry),
            AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 9f)
        });
        ry += 20;

        var lblFileName = new Label
        {
            Location = new Point(rx, ry),
            Size = new Size(rw, 38),
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
                Name = profile.Name,
                Counter = profile.Counter,
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
            Location = new Point(rx, ry),
            Size = new Size(rw, 24),
            Checked = profile.DeleteAfterSave,
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        dlg.Controls.Add(chkDelete);
        ry += 36;

        // ── Buttons ──
        var btnSave = new Button
        {
            Text = "💾  Lưu && Tiếp",
            Location = new Point(rx, ry),
            Size = new Size(138, 42),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
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
            Text = "⏭ Bỏ qua",
            Location = new Point(rx + 146, ry),
            Size = new Size(90, 42),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
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
            Text = "✕ Dừng",
            Location = new Point(rx + 244, ry),
            Size = new Size(74, 42),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(239, 68, 68)
        };
        btnCancelDlg.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
        btnCancelDlg.Click += (_, _) =>
        {
            action = PackAction.Cancel;
            dlg.DialogResult = DialogResult.Cancel;
        };
        dlg.Controls.Add(btnCancelDlg);

        var btnSaveAs = new Button
        {
            Text = "📂  Lưu tới đường dẫn khác…",
            Location = new Point(rx, ry + 52),
            Size = new Size(rw, 34),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(59, 130, 246),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        btnSaveAs.FlatAppearance.BorderSize = 0;
        btnSaveAs.Click += (_, _) =>
        {
            int filterIndex = profile.DefaultFormat switch
            {
                SaveFormat.JPEG => 2,
                SaveFormat.BMP => 3,
                SaveFormat.TIFF => 4,
                SaveFormat.PDF => 5,
                _ => 1
            };
            using var sfd = new SaveFileDialog
            {
                Title = "Chọn đường dẫn lưu",
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|BMP Image|*.bmp|TIFF Image|*.tiff|PDF Document|*.pdf",
                FilterIndex = filterIndex,
                InitialDirectory = Directory.Exists(profile.SavePath)
                    ? profile.SavePath : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = string.IsNullOrWhiteSpace(txtLabel.Text)
                    ? "scan" : txtLabel.Text.Trim()
            };
            if (sfd.ShowDialog(dlg) == DialogResult.OK)
            {
                customSavePath = sfd.FileName;
                action = PackAction.Save;
                label = txtLabel.Text.Trim();
                deleteAfter = chkDelete.Checked;
                dlg.DialogResult = DialogResult.OK;
            }
        };
        dlg.Controls.Add(btnSaveAs);

        dlg.AcceptButton = btnSave;
        dlg.Shown += (_, _) => txtLabel.Focus();
        dlg.ShowDialog(this);

        return (action, label, deleteAfter, customSavePath);
    }

    private static void AddInfoLabel(Form parent, int x, ref int y, string text)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(318, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f),
            AutoEllipsis = true
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
            MaximizeBox = false,
            MinimizeBox = false,
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
            Location = new Point(lx, ly),
            Size = new Size(colW, 26),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 88, 12)
        });
        ly += 28;

        var pnlExisting = new Panel
        {
            Location = new Point(lx, ly),
            Size = new Size(colW, 200),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
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
                Dock = DockStyle.Fill,
                Image = existingImage,
                BackColor = Color.White
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
            Location = new Point(lx, ly),
            Size = new Size(colW, 18),
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
            Location = new Point(lx, ly),
            Size = new Size(colW, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f)
        });
        ly += 20;

        dlg.Controls.Add(new Label
        {
            Text = $"📂  {dir}",
            Location = new Point(lx, ly),
            Size = new Size(colW, 18),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8f),
            AutoEllipsis = true
        });
        ly += 24;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(lx, ly),
            Size = new Size(colW, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        ly += 10;

        dlg.Controls.Add(new Label
        {
            Text = "Đổi tên file hiện tại:",
            Location = new Point(lx, ly),
            Size = new Size(colW, 22),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        });
        ly += 24;

        var txtExistingName = new TextBox
        {
            Text = originalNameNoExt,
            Location = new Point(lx, ly),
            Size = new Size(colW, 30),
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(234, 88, 12)
        };
        dlg.Controls.Add(txtExistingName);
        ly += 34;

        dlg.Controls.Add(new Label
        {
            Text = $"Phần mở rộng:  {ext}",
            Location = new Point(lx, ly),
            AutoSize = true,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });

        var lblExistingStatus = new Label
        {
            Location = new Point(lx + 150, ly - 2),
            Size = new Size(colW - 150, 20),
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
            Location = new Point(rx, ry),
            Size = new Size(colW, 26),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(59, 130, 246)
        });
        ry += 28;

        var pnlNew = new Panel
        {
            Location = new Point(rx, ry),
            Size = new Size(colW, 200),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        pnlNew.Controls.Add(new ZoomableImagePanel
        {
            Dock = DockStyle.Fill,
            Image = newImage,
            BackColor = Color.White
        });
        dlg.Controls.Add(pnlNew);
        ry += 204;

        dlg.Controls.Add(new Label
        {
            Text = "↑ Scroll zoom · Kéo di chuyển",
            Location = new Point(rx, ry),
            Size = new Size(colW, 18),
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });
        ry += 20;

        // Spacer to align with file info on the left
        ry += 40;

        dlg.Controls.Add(new Panel
        {
            Location = new Point(rx, ry),
            Size = new Size(colW, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        ry += 10;

        dlg.Controls.Add(new Label
        {
            Text = "Đặt tên file mới:",
            Location = new Point(rx, ry),
            Size = new Size(colW, 22),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        });
        ry += 24;

        var txtNewName = new TextBox
        {
            Text = suggestedNewName,
            Location = new Point(rx, ry),
            Size = new Size(colW, 30),
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(59, 130, 246)
        };
        dlg.Controls.Add(txtNewName);
        ry += 34;

        dlg.Controls.Add(new Label
        {
            Text = $"Phần mở rộng:  {ext}",
            Location = new Point(rx, ry),
            AutoSize = true,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Italic)
        });

        var lblNewStatus = new Label
        {
            Location = new Point(rx + 150, ry - 2),
            Size = new Size(colW - 150, 20),
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
            Location = new Point(margin, by),
            Size = new Size(900 - 2 * margin, 1),
            BackColor = Color.FromArgb(226, 232, 240)
        });
        by += 10;

        var btnSaveNew = new Button
        {
            Text = "💾  Lưu",
            Location = new Point(rx, by),
            Size = new Size(130, 42),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
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
            Location = new Point(rx + 138, by),
            Size = new Size(100, 42),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
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
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(248, 250, 252),
                Font = new Font("Segoe UI", 10f)
            };
            pwDlg.Controls.Add(new Label
            {
                Text = "Nhập mật khẩu để ghi đè file:",
                Location = new Point(16, 16),
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105)
            });
            var txtPw = new TextBox
            {
                Location = new Point(16, 44),
                Size = new Size(305, 30),
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 11f)
            };
            pwDlg.Controls.Add(txtPw);
            var btnConfirm = new Button
            {
                Text = "Xác nhận",
                Location = new Point(120, 90),
                Size = new Size(100, 36),
                BackColor = Color.FromArgb(234, 88, 12),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
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
                Text = "Hủy",
                Location = new Point(228, 90),
                Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
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
            Location = new Point(rx + 246, by),
            Size = new Size(80, 42),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
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

    private void PerformAutoSave(int startIndex, int count)
    {
        var profile = _profileManager.ActiveProfile;
        try
        {
            var resultPath = _saveService.SavePages(
                i => _pageStore.Load(startIndex + i), count, profile);
            _profileManager.Save();
            SetStatus($"Auto-saved {count} page(s) → {resultPath}",
                Color.FromArgb(16, 185, 129));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Auto-save failed: {ex.Message}", "DTScan",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
