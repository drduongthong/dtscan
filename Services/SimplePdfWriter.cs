using System.Globalization;
using System.Text;

namespace DTScan.Services;

public static class SimplePdfWriter
{
    public static void Save(Func<int, Image> pageLoader, int pageCount, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        var offsets = new List<long>();

        Write(fs, "%PDF-1.4\n%\xe2\xe3\xcf\xd3\n");

        // Object 1: Catalog
        offsets.Add(fs.Position);
        Write(fs, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Object 2: Pages
        offsets.Add(fs.Position);
        var kids = string.Join(" ",
            Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 3} 0 R"));
        Write(fs, $"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n");

        // Each page: Page + ContentStream + ImageXObject (3 objects per page)
        for (int i = 0; i < pageCount; i++)
        {
            using var img = pageLoader(i);
            byte[] jpegBytes = ImageToJpeg(img);

            int pageObj = 3 + i * 3;
            int contentObj = pageObj + 1;
            int imageObj = pageObj + 2;

            float dpiX = img.HorizontalResolution > 0 ? img.HorizontalResolution : 96f;
            float dpiY = img.VerticalResolution > 0 ? img.VerticalResolution : 96f;
            string pw = (img.Width * 72f / dpiX).ToString("F2", CultureInfo.InvariantCulture);
            string ph = (img.Height * 72f / dpiY).ToString("F2", CultureInfo.InvariantCulture);

            // Page
            offsets.Add(fs.Position);
            Write(fs, $"{pageObj} 0 obj\n<< /Type /Page /Parent 2 0 R " +
                $"/MediaBox [0 0 {pw} {ph}] /Contents {contentObj} 0 R " +
                $"/Resources << /XObject << /Im0 {imageObj} 0 R >> >> >>\nendobj\n");

            // Content stream
            string stream = $"q {pw} 0 0 {ph} 0 0 cm /Im0 Do Q";
            offsets.Add(fs.Position);
            Write(fs, $"{contentObj} 0 obj\n<< /Length {stream.Length} >>\nstream\n{stream}\nendstream\nendobj\n");

            // Image XObject
            offsets.Add(fs.Position);
            Write(fs, $"{imageObj} 0 obj\n<< /Type /XObject /Subtype /Image " +
                $"/Width {img.Width} /Height {img.Height} /ColorSpace /DeviceRGB " +
                $"/BitsPerComponent 8 /Filter /DCTDecode /Length {jpegBytes.Length} >>\nstream\n");
            fs.Write(jpegBytes);
            Write(fs, "\nendstream\nendobj\n");
        }

        // Xref table
        long xrefPos = fs.Position;
        int totalObjs = offsets.Count + 1;
        Write(fs, $"xref\n0 {totalObjs}\n0000000000 65535 f \n");
        foreach (long off in offsets)
            Write(fs, $"{off:D10} 00000 n \n");

        Write(fs, $"trailer\n<< /Size {totalObjs} /Root 1 0 R >>\n" +
            $"startxref\n{xrefPos}\n%%EOF\n");
    }

    private static byte[] ImageToJpeg(Image img)
    {
        using var bmp = new Bitmap(img.Width, img.Height,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
            g.DrawImage(img, 0, 0, img.Width, img.Height);

        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private static void Write(FileStream fs, string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        fs.Write(bytes);
    }
}
