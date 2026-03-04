using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Text.Json;
using DTScan.Models;

namespace DTScan.Services;

public sealed class ScanHistoryService
{
    private static readonly string HistoryRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DTScan", "History");

    private static readonly string IndexFile = Path.Combine(HistoryRoot, "history.json");

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Task> _pendingSaves = new();
    private List<ScanHistoryEntry> _entries = [];

    public IReadOnlyList<ScanHistoryEntry> Entries => _entries.AsReadOnly();

    public ScanHistoryService()
    {
        Directory.CreateDirectory(HistoryRoot);
        Load();
    }

    // ═══════════════════════════════════════════════
    //  SAVE BACKUP — chạy nền, trả entry ngay lập tức
    // ═══════════════════════════════════════════════

    public ScanHistoryEntry SaveBackup(Func<int, Image> pageLoader, int pageCount,
        UserProfile profile)
    {
        var entry = new ScanHistoryEntry
        {
            PageCount = pageCount,
            ProfileName = profile.Name,
            ScanSource = profile.ScanSource.ToString(),
            Dpi = profile.ScanDpi,
            ColorMode = profile.ColorMode.ToString()
        };

        var folder = Path.Combine(HistoryRoot, entry.Id);
        Directory.CreateDirectory(folder);
        entry.BackupFolderPath = folder;

        // Encode từng trang sang JPEG — load 1 trang từ disk tại 1 thời điểm
        var codec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, 80L);

        var encodedPages = new List<byte[]>(pageCount);
        for (int i = 0; i < pageCount; i++)
        {
            using var img = pageLoader(i);
            using var ms = new MemoryStream();
            img.Save(ms, codec, encParams);
            encodedPages.Add(ms.ToArray());
        }

        lock (_lock)
        {
            _entries.Add(entry);
            PersistIndex();
        }

        var task = Task.Run(() =>
        {
            try
            {
                long totalSize = 0;
                for (int i = 0; i < encodedPages.Count; i++)
                {
                    var path = Path.Combine(folder, $"page_{i + 1:D4}.jpg");
                    File.WriteAllBytes(path, encodedPages[i]);
                    totalSize += encodedPages[i].Length;
                }

                lock (_lock)
                {
                    entry.TotalSizeBytes = totalSize;
                    PersistIndex();
                }
            }
            finally
            {
                encodedPages.Clear();
                _pendingSaves.TryRemove(entry.Id, out _);
            }
        });

        _pendingSaves[entry.Id] = task;

        return entry;
    }

    // ═══════════════════════════════════════════════
    //  LOAD / DELETE — chờ pending save nếu có
    // ═══════════════════════════════════════════════

    public List<Image> LoadBackupImages(ScanHistoryEntry entry)
    {
        WaitForPendingSave(entry.Id);

        var images = new List<Image>();
        if (!Directory.Exists(entry.BackupFolderPath)) return images;

        var files = Directory.GetFiles(entry.BackupFolderPath, "page_*.jpg")
            .Concat(Directory.GetFiles(entry.BackupFolderPath, "page_*.png"))
            .OrderBy(f => f).ToList();

        foreach (var file in files)
        {
            using var img = Image.FromFile(file);
            images.Add(new Bitmap(img));
        }

        return images;
    }

    public void DeleteEntry(ScanHistoryEntry entry)
    {
        WaitForPendingSave(entry.Id);

        try
        {
            if (Directory.Exists(entry.BackupFolderPath))
                Directory.Delete(entry.BackupFolderPath, true);
        }
        catch { /* ignore cleanup errors */ }

        lock (_lock)
        {
            _entries.Remove(entry);
            PersistIndex();
        }
    }

    public void DeleteEntry(string entryId)
    {
        ScanHistoryEntry? entry;
        lock (_lock)
        {
            entry = _entries.Find(e => e.Id == entryId);
        }
        if (entry != null) DeleteEntry(entry);
    }

    // ═══════════════════════════════════════════════
    //  INTERNAL
    // ═══════════════════════════════════════════════

    private void WaitForPendingSave(string entryId)
    {
        if (_pendingSaves.TryRemove(entryId, out var task))
        {
            try { task.Wait(); } catch { }
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(IndexFile))
            {
                var json = File.ReadAllText(IndexFile);
                _entries = JsonSerializer.Deserialize<List<ScanHistoryEntry>>(json) ?? [];
            }
        }
        catch { _entries = []; }

        int removed = _entries.RemoveAll(e => !Directory.Exists(e.BackupFolderPath));
        if (removed > 0) PersistIndex();
    }

    private void PersistIndex()
    {
        var json = JsonSerializer.Serialize(_entries,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(IndexFile, json);
    }
}
