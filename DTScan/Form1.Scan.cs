using DTScan.Forms;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DTScan;

public partial class Form1
{
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif", ".pdf"
    };

    // ═══════════════════════════════════════════════
    //  DRAG & DROP
    // ═══════════════════════════════════════════════

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

    private async void OnFileDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;

        using var progress = new ProgressForm("📂  Đang import…");
        progress.Show(this);

        int count = 0;
        try
        {
            count = await ImportFilesAsync(files.OrderBy(f => f), progress);
        }
        catch { /* skip errors */ }
        finally
        {
            progress.ForceClose();
        }

        if (count > 0)
            SetStatus($"Imported {count} page(s) via drag-drop",
                Color.FromArgb(16, 185, 129));
    }

    // ═══════════════════════════════════════════════
    //  FILE IMPORT (ảnh + PDF)
    // ═══════════════════════════════════════════════

    private async Task<int> ImportFilesAsync(IEnumerable<string> filePaths, ProgressForm? progress = null)
    {
        int count = 0;
        int dpi = _profileManager.ActiveProfile.ScanDpi;
        var files = filePaths
            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        for (int fi = 0; fi < files.Count; fi++)
        {
            if (progress?.IsCancelled == true) break;

            var file = files[fi];
            progress?.UpdateStatus($"Đang import {fi + 1}/{files.Count}: {Path.GetFileName(file)}");
            progress?.SetProgress(fi, files.Count);

            try
            {
                if (Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var pages = await Task.Run(() => LoadPdfPages(file, dpi));
                    foreach (var page in pages)
                    {
                        AddPage(page);
                        page.Dispose();
                        count++;
                    }
                }
                else
                {
                    var bmp = await Task.Run(() =>
                    {
                        using var img = Image.FromFile(file);
                        return new Bitmap(img);
                    });
                    AddPage(bmp);
                    bmp.Dispose();
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

    // ═══════════════════════════════════════════════
    //  SCAN  (Flatbed / Feeder / Duplex)
    // ═══════════════════════════════════════════════

    private async void BtnScan_Click(object? sender, EventArgs e)
    {
        var profile = _profileManager.ActiveProfile;
        SetStatus("Scanning…", Color.FromArgb(245, 158, 11));
        btnScan.Enabled = false;

        int pagesBeforeScan = _pageStore.Count;
        int scannedCount = 0;

        using var progress = new ProgressForm("📷  Đang scan…");
        progress.Show(this);

        try
        {
            await RunOnStaThreadAsync(() =>
            {
                using var scanForm = new Form { ShowInTaskbar = false };
                _ = scanForm.Handle;

                _scannerService.Scan(
                    profile.ScanSource, profile.ScanDpi, profile.ColorMode,
                    profile.ScanDriver, scanForm.Handle, profile.PreferredScanner,
                    onPageScanned: page =>
                    {
                        try
                        {
                            this.Invoke(() =>
                            {
                                scannedCount++;
                                AddPage(page);
                                progress.UpdateStatus($"Đã scan {scannedCount} trang…");
                                SetStatus($"Scanning… {scannedCount} page(s)",
                                    Color.FromArgb(245, 158, 11));
                            });
                        }
                        catch (ObjectDisposedException) { }
                    });
            });

            if (scannedCount > 0)
            {
                var historyEntry = _scanHistoryService.SaveBackup(
                    i => _pageStore.Load(pagesBeforeScan + i),
                    scannedCount, profile);

                for (int i = pagesBeforeScan; i < _pageStore.Count; i++)
                    _pageSessionIds[i] = historyEntry.Id;
                _sessionPageCounts[historyEntry.Id] = scannedCount;

                SetStatus($"Scanned {scannedCount} page(s) — {profile.ScanSource}",
                    Color.FromArgb(16, 185, 129));

                if (profile.AutoSave)
                    PerformAutoSave(pagesBeforeScan, scannedCount);
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
            progress.ForceClose();
            btnScan.Enabled = true;
        }
    }

    // ═══════════════════════════════════════════════
    //  IMPORT
    // ═══════════════════════════════════════════════

    private async void BtnImport_Click(object? sender, EventArgs e)
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

        btnImport.Enabled = false;
        using var progress = new ProgressForm("📂  Đang import…");
        progress.Show(this);

        int count = 0;
        try
        {
            count = await ImportFilesAsync(dlg.FileNames, progress);
        }
        catch { /* skip errors */ }
        finally
        {
            progress.ForceClose();
            btnImport.Enabled = true;
        }

        SetStatus($"Imported {count} page(s)", Color.FromArgb(16, 185, 129));
    }
}
