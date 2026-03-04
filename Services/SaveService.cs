using System.Drawing.Imaging;
using DTScan.Models;

namespace DTScan.Services;

public sealed class SaveService
{
    public string BuildFirstTargetPath(UserProfile profile,
        string label = "", int batchNumber = 1)
    {
        var subFolder = PlaceholderResolver.Resolve(
            profile.SubFolderPattern, profile, 1, batchNumber, label);
        var dir = Path.Combine(profile.SavePath, subFolder);
        int pageNum = IsMultiPageFormat(profile.DefaultFormat) ? 0 : 1;
        var fileName = PlaceholderResolver.Resolve(
            profile.FileNamePattern, profile, pageNum, batchNumber, label)
            + PlaceholderResolver.GetExtension(profile.DefaultFormat);
        return Path.Combine(dir, fileName);
    }

    public string SavePages(Func<int, Image> pageLoader, int pageCount,
        UserProfile profile, List<string>? labels = null,
        int batchNumber = 1, string? resolvedFirstFilePath = null)
    {
        var firstLabel = labels is { Count: > 0 } ? labels[0] : "";
        var subFolder = PlaceholderResolver.Resolve(
            profile.SubFolderPattern, profile, 1, batchNumber, firstLabel);
        var dir = Path.Combine(profile.SavePath, subFolder);
        Directory.CreateDirectory(dir);

        string resultPath;

        if (IsMultiPageFormat(profile.DefaultFormat))
        {
            if (resolvedFirstFilePath != null)
            {
                resultPath = resolvedFirstFilePath;
            }
            else
            {
                var fileName = PlaceholderResolver.Resolve(
                    profile.FileNamePattern, profile, 0, batchNumber, firstLabel)
                    + PlaceholderResolver.GetExtension(profile.DefaultFormat);
                resultPath = GetUniquePath(Path.Combine(dir, fileName));
            }

            if (resolvedFirstFilePath != null && File.Exists(resultPath))
                File.Delete(resultPath);

            if (profile.DefaultFormat == SaveFormat.PDF)
                SimplePdfWriter.Save(pageLoader, pageCount, resultPath);
            else
                SaveMultiPageTiff(pageLoader, pageCount, resultPath);

            profile.Counter++;
        }
        else
        {
            resultPath = dir;
            for (int i = 0; i < pageCount; i++)
            {
                var pageLabel = labels != null && i < labels.Count ? labels[i] : "";
                string path;
                if (i == 0 && resolvedFirstFilePath != null)
                {
                    path = resolvedFirstFilePath;
                }
                else
                {
                    var fileName = PlaceholderResolver.Resolve(
                        profile.FileNamePattern, profile, i + 1, batchNumber, pageLabel)
                        + PlaceholderResolver.GetExtension(profile.DefaultFormat);
                    path = GetUniquePath(Path.Combine(dir, fileName));
                }

                if (i == 0 && resolvedFirstFilePath != null && File.Exists(path))
                    File.Delete(path);

                using var img = pageLoader(i);
                img.Save(path, GetImageFormat(profile.DefaultFormat));
                profile.Counter++;
            }
        }

        return resultPath;
    }

    private static bool IsMultiPageFormat(SaveFormat fmt) =>
        fmt is SaveFormat.PDF or SaveFormat.TIFF;

    public string SaveToPath(Func<int, Image> pageLoader, int pageCount, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var format = ext switch
        {
            ".jpg" or ".jpeg" => SaveFormat.JPEG,
            ".bmp" => SaveFormat.BMP,
            ".tiff" or ".tif" => SaveFormat.TIFF,
            ".pdf" => SaveFormat.PDF,
            _ => SaveFormat.PNG
        };

        if (IsMultiPageFormat(format))
        {
            if (format == SaveFormat.PDF)
                SimplePdfWriter.Save(pageLoader, pageCount, filePath);
            else
                SaveMultiPageTiff(pageLoader, pageCount, filePath);
        }
        else if (pageCount == 1)
        {
            using var img = pageLoader(0);
            img.Save(filePath, GetImageFormat(format));
        }
        else
        {
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            for (int i = 0; i < pageCount; i++)
            {
                var pagePath = i == 0
                    ? filePath
                    : Path.Combine(dir, $"{baseName}_{i + 1}{ext}");
                using var img = pageLoader(i);
                img.Save(pagePath, GetImageFormat(format));
            }
        }

        return filePath;
    }

    private static ImageFormat GetImageFormat(SaveFormat fmt) => fmt switch
    {
        SaveFormat.JPEG => ImageFormat.Jpeg,
        SaveFormat.BMP => ImageFormat.Bmp,
        SaveFormat.TIFF => ImageFormat.Tiff,
        _ => ImageFormat.Png
    };

    private static void SaveMultiPageTiff(Func<int, Image> pageLoader, int pageCount, string path)
    {
        if (pageCount == 0) return;

        var codec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Tiff.Guid);

        using var saveParams = new EncoderParameters(1);
        saveParams.Param[0] = new EncoderParameter(
            Encoder.SaveFlag, (long)EncoderValue.MultiFrame);

        using var first = pageLoader(0);
        first.Save(path, codec, saveParams);

        saveParams.Param[0].Dispose();
        saveParams.Param[0] = new EncoderParameter(
            Encoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);

        for (int i = 1; i < pageCount; i++)
        {
            using var page = pageLoader(i);
            first.SaveAdd(page, saveParams);
        }

        saveParams.Param[0].Dispose();
        saveParams.Param[0] = new EncoderParameter(
            Encoder.SaveFlag, (long)EncoderValue.Flush);
        first.SaveAdd(saveParams);
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int n = 2;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name}_{n}{ext}");
            n++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
