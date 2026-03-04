using System.Text.Json.Serialization;

namespace DTScan.Models;

public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Default";
    public string SavePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DTScan");
    public string FileNamePattern { get; set; } = "Scan_{date}_{time}_{counter}";
    public string SubFolderPattern { get; set; } = @"{year}\{month}";
    public SaveFormat DefaultFormat { get; set; } = SaveFormat.PNG;
    public int PagesPerBatch { get; set; } = 1;
    public bool AutoSave { get; set; } = false;
    public bool AutoSelectNextBatch { get; set; } = true;
    public bool DeleteAfterSave { get; set; } = false;
    public int ScanDpi { get; set; } = 300;
    public ScanColorMode ColorMode { get; set; } = ScanColorMode.Color;
    public ScanSource ScanSource { get; set; } = ScanSource.Flatbed;
    public ScanDriver ScanDriver { get; set; } = ScanDriver.Auto;
    public string PreferredScanner { get; set; } = "";
    public int Counter { get; set; } = 1;
    public ThemePreset ThemeColor { get; set; } = ThemePreset.Blue;
    public Dictionary<string, string> Shortcuts { get; set; } = DefaultShortcuts();

    public static Dictionary<string, string> DefaultShortcuts() => new()
    {
        ["Scan"] = "F2",
        ["Import"] = "F3",
        ["Save"] = "F4",
        ["SelectAll"] = "F5",
        ["DeselectAll"] = "F6",
        ["AutoSelect"] = "F7",
        ["RotateLeft"] = "F8",
        ["RotateRight"] = "F9",
        ["Delete"] = "F10",
        ["QuickLabel"] = "F11",
        ["Settings"] = "F12",
    };
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SaveFormat
{
    PNG,
    JPEG,
    BMP,
    TIFF,
    PDF
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScanColorMode
{
    BlackWhite = 0,
    Grayscale = 1,
    Color = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScanSource
{
    Flatbed,
    Feeder,
    Duplex
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScanDriver
{
    Auto,
    Twain,
    Wia
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemePreset
{
    Blue,
    Indigo,
    Emerald,
    Teal,
    Purple,
    Rose,
    Amber,
    Slate
}
