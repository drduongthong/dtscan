namespace DTScan.Models;

public class ScanHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int PageCount { get; set; }
    public string ProfileName { get; set; } = "";
    public string ScanSource { get; set; } = "";
    public int Dpi { get; set; }
    public string ColorMode { get; set; } = "";
    public string BackupFolderPath { get; set; } = "";
    public long TotalSizeBytes { get; set; }
}
