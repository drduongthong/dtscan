using DTScan.Controls;
using DTScan.Forms;

namespace DTScan;

public partial class Form1
{
    // ═══════════════════════════════════════════════
    //  PAGE MANAGEMENT
    // ═══════════════════════════════════════════════

    private void AddPage(Image original, string? sessionId = null)
    {
        _pageStore.Add(original);
        _pageSessionIds.Add(sessionId);
        if (sessionId != null)
        {
            _sessionPageCounts.TryAdd(sessionId, 0);
            _sessionPageCounts[sessionId]++;
        }
        int pageNum = _pageStore.Count;
        int thumbH = (int)(_thumbnailWidth * 1.5);

        var thumb = new PageThumbnailControl
        {
            Size = new Size(_thumbnailWidth, thumbH),
            PageImage = CreateThumbnail(original, _thumbnailWidth - 16, thumbH - 72),
            PageNumber = pageNum
        };
        thumb.SelectionChanged += (_, _) => UpdateUI();
        thumb.DoubleClick += (_, _) =>
        {
            using var img = _pageStore.Load(thumb.PageNumber - 1);
            ShowPreview(img);
        };

        flpPages.Controls.Add(thumb);
        UpdateUI();
    }

    // ═══════════════════════════════════════════════
    //  ZOOM / THUMBNAILS
    // ═══════════════════════════════════════════════

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

    private async void OnZoomDebounce(object? sender, EventArgs e)
    {
        _zoomDebounce?.Stop();
        _zoomCts?.Cancel();
        _zoomCts?.Dispose();
        _zoomCts = new CancellationTokenSource();
        var token = _zoomCts.Token;

        int width = _thumbnailWidth;
        int thumbH = (int)(width * 1.5);
        int count = _pageStore.Count;

        var newThumbs = await Task.Run(() =>
        {
            var thumbs = new Image?[count];
            for (int i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    foreach (var t in thumbs) t?.Dispose();
                    return null;
                }
                using var img = _pageStore.Load(i);
                thumbs[i] = CreateThumbnail(img, width - 16, thumbH - 72);
            }
            return thumbs;
        });

        if (newThumbs == null || token.IsCancellationRequested)
        {
            if (newThumbs != null)
                foreach (var t in newThumbs) t?.Dispose();
            return;
        }

        var thumbnails = GetThumbnails();
        int applyCount = Math.Min(thumbnails.Count, newThumbs.Length);
        for (int i = 0; i < applyCount; i++)
        {
            thumbnails[i].PageImage?.Dispose();
            thumbnails[i].PageImage = newThumbs[i];
            thumbnails[i].Invalidate();
        }
        for (int i = applyCount; i < newThumbs.Length; i++)
            newThumbs[i]?.Dispose();
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
            using var img = _pageStore.Load(i);
            img.RotateFlip(rotation);
            _pageStore.Replace(i, img);
            thumbnails[i].PageImage = CreateThumbnail(img);
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

            _pageStore.RemoveAt(idx);
            var ctrl = (PageThumbnailControl)flpPages.Controls[idx];
            flpPages.Controls.RemoveAt(idx);
            ctrl.Dispose();
        }

        var remaining = GetThumbnails();
        for (int i = 0; i < remaining.Count; i++)
            remaining[i].PageNumber = i + 1;

        _nextBatchStart = Math.Min(_nextBatchStart, _pageStore.Count);
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
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 10f)
        };

        var lblHeader = new Label
        {
            Text = "Gán nhãn / số cho từng trang scan.\n"
                 + "Ví dụ: giấy khám scan không theo thứ tự → nhập 03, 01, 02...",
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(16, 10, 16, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 9f)
        };
        dlg.Controls.Add(lblHeader);

        var pnlFill = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(16, 6, 16, 6),
            BackColor = Color.FromArgb(239, 246, 255)
        };
        var nudStart = new NumericUpDown { Location = new Point(135, 6), Size = new Size(70, 28), Minimum = 0, Maximum = 9999, Value = 1 };
        var nudStep = new NumericUpDown { Location = new Point(260, 6), Size = new Size(60, 28), Minimum = 1, Maximum = 100, Value = 1 };
        var nudPad = new NumericUpDown { Location = new Point(382, 6), Size = new Size(50, 28), Minimum = 1, Maximum = 6, Value = 2 };
        var btnFill = new Button
        {
            Text = "Fill ↓",
            Location = new Point(440, 4),
            Size = new Size(55, 30),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White
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
                Text = $"Trang {thumb.PageNumber}:",
                Location = new Point(0, 6),
                Size = new Size(80, 22),
                ForeColor = Color.FromArgb(71, 85, 105)
            });
            var txt = new TextBox
            {
                Text = thumb.DocumentLabel,
                Location = new Point(85, 2),
                Size = new Size(370, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 102, 241),
                PlaceholderText = "Nhãn / Số..."
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
            Text = "✓  Apply",
            Size = new Size(120, 38),
            Location = new Point(260, 8),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        btnApply.FlatAppearance.BorderSize = 0;
        btnApply.Click += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
        var btnCancelQL = new Button
        {
            Text = "Cancel",
            Size = new Size(100, 38),
            Location = new Point(390, 8),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
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
            {
                AddPage(img);
                img.Dispose();
            }
            SetStatus($"Khôi phục {form.RestoredImages.Count} trang từ lịch sử",
                Color.FromArgb(16, 185, 129));
        }
    }

    // ═══════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════

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
}
