using System.Drawing.Imaging;

namespace DTScan.Services;

/// <summary>
/// Lưu ảnh trang scan dưới dạng temp file trên đĩa thay vì giữ
/// toàn bộ Bitmap trong RAM. Giảm peak memory từ O(N × 32 MB) xuống
/// gần 0 (chỉ giữ đường dẫn file).
/// </summary>
public sealed class PageStore : IDisposable
{
    private static readonly ImageCodecInfo JpegCodec = ImageCodecInfo.GetImageEncoders()
        .First(c => c.FormatID == ImageFormat.Jpeg.Guid);

    private readonly string _tempDir;
    private readonly List<string> _paths = [];

    public PageStore()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DTScan", "Pages",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public int Count => _paths.Count;

    /// <summary>
    /// Lưu ảnh vào temp file (JPEG quality 95 — near-lossless).
    /// KHÔNG dispose ảnh nguồn — caller tự quản lý.
    /// </summary>
    public void Add(Image image)
    {
        var path = Path.Combine(_tempDir, $"p_{_paths.Count:D4}.jpg");
        using var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, 95L);
        image.Save(path, JpegCodec, encParams);
        _paths.Add(path);
    }

    /// <summary>
    /// Load ảnh từ temp file. Caller PHẢI Dispose ảnh trả về.
    /// </summary>
    public Image Load(int index)
    {
        using var fs = File.OpenRead(_paths[index]);
        using var img = Image.FromStream(fs);
        return new Bitmap(img);
    }

    /// <summary>
    /// Thay thế ảnh tại vị trí (dùng cho rotate).
    /// </summary>
    public void Replace(int index, Image image)
    {
        using var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, 95L);
        image.Save(_paths[index], JpegCodec, encParams);
    }

    public void RemoveAt(int index)
    {
        try { File.Delete(_paths[index]); } catch { }
        _paths.RemoveAt(index);
    }

    public void Dispose()
    {
        _paths.Clear();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
