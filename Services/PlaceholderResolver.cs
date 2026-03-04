using DTScan.Models;

namespace DTScan.Services;

public static class PlaceholderResolver
{
    public static string Resolve(string pattern, UserProfile profile,
        int pageNumber = 0, int batchNumber = 0, string label = "")
    {
        var now = DateTime.Now;
        // If {label} is used but no label given, fall back to page number
        var resolvedLabel = string.IsNullOrWhiteSpace(label)
            ? pageNumber.ToString("D3")
            : SanitizePath(label);

        return pattern
            .Replace("{date}", now.ToString("yyyy-MM-dd"))
            .Replace("{time}", now.ToString("HHmmss"))
            .Replace("{year}", now.ToString("yyyy"))
            .Replace("{month}", now.ToString("MM"))
            .Replace("{day}", now.ToString("dd"))
            .Replace("{counter}", profile.Counter.ToString("D4"))
            .Replace("{user}", SanitizePath(profile.Name))
            .Replace("{page}", pageNumber.ToString("D3"))
            .Replace("{batch}", batchNumber.ToString("D3"))
            .Replace("{label}", resolvedLabel);
    }

    public static string GetPreview(string filePattern, string subFolderPattern,
        UserProfile profile, string sampleLabel = "03")
    {
        var folder = Resolve(subFolderPattern, profile, 1, 1, sampleLabel);
        var file = Resolve(filePattern, profile, 1, 1, sampleLabel);
        var ext = GetExtension(profile.DefaultFormat);
        return Path.Combine(folder, file + ext);
    }

    public static string GetExtension(SaveFormat format) => format switch
    {
        SaveFormat.PNG => ".png",
        SaveFormat.JPEG => ".jpg",
        SaveFormat.BMP => ".bmp",
        SaveFormat.TIFF => ".tiff",
        SaveFormat.PDF => ".pdf",
        _ => ".png"
    };

    private static string SanitizePath(string input)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        return input;
    }
}
