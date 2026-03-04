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

    public ScanHistoryEntry SaveBackup(List<Image> pages, UserProfile profile)
    {
        var entry = new ScanHistoryEntry
        {
            PageCount = pages.Count,
            ProfileName = profile.Name,
            ScanSource = profile.ScanSource.ToString(),
            Dpi = profile.ScanDpi,
            ColorMode = profile.ColorMode.ToString()
        };

        var folder = Path.Combine(HistoryRoot, entry.Id);
        Directory.CreateDirectory(folder);
        entry.BackupFolderPath = folder;

        // Clone ảnh trên UI thread (memory copy nhanh)
        // để background thread encode PNG + ghi file độc lập
        var clones = new List<Bitmap>(pages.Count);
        try
        {
            foreach (var p in pages)
                clones.Add(new Bitmap(p));
        }
        catch
        {
            foreach (var c in clones) c.Dispose();
            throw;
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
                for (int i = 0; i < clones.Count; i++)
                {
                    var path = Path.Combine(folder, $"page_{i + 1:D4}.png");
                    clones[i].Save(path, ImageFormat.Png);
                    totalSize += new FileInfo(path).Length;
                }

                lock (_lock)
                {
                    entry.TotalSizeBytes = totalSize;
                    PersistIndex();
                }
            }
            finally
            {
                foreach (var c in clones) c.Dispose();
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

        var files = Directory.GetFiles(entry.BackupFolderPath, "page_*.png")
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
