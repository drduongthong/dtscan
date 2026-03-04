namespace DTScan.Models;

public sealed record ThemeColorSet(
    Color Accent,
    Color AccentHover,
    Color AccentDown,
    Color AccentDark,
    Color AccentDarkHover,
    Color AccentDarkDown,
    Color AccentLight
);

public static class ThemeColors
{
    public static ThemeColorSet Get(ThemePreset preset) => preset switch
    {
        ThemePreset.Blue => new(
            Accent: Color.FromArgb(59, 130, 246),
            AccentHover: Color.FromArgb(37, 99, 235),
            AccentDown: Color.FromArgb(29, 78, 216),
            AccentDark: Color.FromArgb(30, 27, 75),
            AccentDarkHover: Color.FromArgb(49, 46, 129),
            AccentDarkDown: Color.FromArgb(67, 56, 202),
            AccentLight: Color.FromArgb(199, 210, 254)
        ),
        ThemePreset.Indigo => new(
            Accent: Color.FromArgb(99, 102, 241),
            AccentHover: Color.FromArgb(79, 70, 229),
            AccentDown: Color.FromArgb(67, 56, 202),
            AccentDark: Color.FromArgb(30, 27, 75),
            AccentDarkHover: Color.FromArgb(49, 46, 129),
            AccentDarkDown: Color.FromArgb(67, 56, 202),
            AccentLight: Color.FromArgb(199, 210, 254)
        ),
        ThemePreset.Emerald => new(
            Accent: Color.FromArgb(16, 185, 129),
            AccentHover: Color.FromArgb(5, 150, 105),
            AccentDown: Color.FromArgb(4, 120, 87),
            AccentDark: Color.FromArgb(6, 78, 59),
            AccentDarkHover: Color.FromArgb(4, 120, 87),
            AccentDarkDown: Color.FromArgb(5, 150, 105),
            AccentLight: Color.FromArgb(167, 243, 208)
        ),
        ThemePreset.Teal => new(
            Accent: Color.FromArgb(20, 184, 166),
            AccentHover: Color.FromArgb(13, 148, 136),
            AccentDown: Color.FromArgb(15, 118, 110),
            AccentDark: Color.FromArgb(17, 94, 89),
            AccentDarkHover: Color.FromArgb(15, 118, 110),
            AccentDarkDown: Color.FromArgb(13, 148, 136),
            AccentLight: Color.FromArgb(153, 246, 228)
        ),
        ThemePreset.Purple => new(
            Accent: Color.FromArgb(139, 92, 246),
            AccentHover: Color.FromArgb(124, 58, 237),
            AccentDown: Color.FromArgb(109, 40, 217),
            AccentDark: Color.FromArgb(46, 16, 101),
            AccentDarkHover: Color.FromArgb(76, 29, 149),
            AccentDarkDown: Color.FromArgb(109, 40, 217),
            AccentLight: Color.FromArgb(221, 214, 254)
        ),
        ThemePreset.Rose => new(
            Accent: Color.FromArgb(244, 63, 94),
            AccentHover: Color.FromArgb(225, 29, 72),
            AccentDown: Color.FromArgb(190, 18, 60),
            AccentDark: Color.FromArgb(76, 5, 25),
            AccentDarkHover: Color.FromArgb(136, 19, 55),
            AccentDarkDown: Color.FromArgb(190, 18, 60),
            AccentLight: Color.FromArgb(255, 228, 230)
        ),
        ThemePreset.Amber => new(
            Accent: Color.FromArgb(245, 158, 11),
            AccentHover: Color.FromArgb(217, 119, 6),
            AccentDown: Color.FromArgb(180, 83, 9),
            AccentDark: Color.FromArgb(120, 53, 15),
            AccentDarkHover: Color.FromArgb(146, 64, 14),
            AccentDarkDown: Color.FromArgb(180, 83, 9),
            AccentLight: Color.FromArgb(254, 243, 199)
        ),
        ThemePreset.Slate => new(
            Accent: Color.FromArgb(100, 116, 139),
            AccentHover: Color.FromArgb(71, 85, 105),
            AccentDown: Color.FromArgb(51, 65, 85),
            AccentDark: Color.FromArgb(30, 41, 59),
            AccentDarkHover: Color.FromArgb(51, 65, 85),
            AccentDarkDown: Color.FromArgb(71, 85, 105),
            AccentLight: Color.FromArgb(203, 213, 225)
        ),
        _ => Get(ThemePreset.Blue)
    };
}
