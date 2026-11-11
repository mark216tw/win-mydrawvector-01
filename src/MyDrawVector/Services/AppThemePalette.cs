using Windows.UI;

namespace MyDrawVector.Services;

public static class AppThemePalette
{
    public const string DefaultId = "classic-blue";

    public static IReadOnlyList<Option> All { get; } =
    [
        new("classic-blue", "ThemeColorClassicBlue", Color.FromArgb(255, 37, 99, 235)),
        new("sky-blue", "ThemeColorSkyBlue", Color.FromArgb(255, 2, 132, 199)),
        new("teal", "ThemeColorTeal", Color.FromArgb(255, 13, 148, 136)),
        new("forest-green", "ThemeColorForestGreen", Color.FromArgb(255, 21, 128, 61)),
        new("eye-care-yellow", "ThemeColorEyeCareYellow", Color.FromArgb(255, 202, 138, 4)),
        new("vivid-orange", "ThemeColorVividOrange", Color.FromArgb(255, 234, 88, 12)),
        new("coral-red", "ThemeColorCoralRed", Color.FromArgb(255, 220, 76, 76)),
        new("twilight-purple", "ThemeColorTwilightPurple", Color.FromArgb(255, 124, 58, 237)),
        new("rose-pink", "ThemeColorRosePink", Color.FromArgb(255, 219, 39, 119)),
        new("neutral-gray", "ThemeColorNeutralGray", Color.FromArgb(255, 100, 116, 139)),
    ];

    public static string Normalize(string? id)
    {
        return All.Any(option => option.Id == id) ? id! : DefaultId;
    }

    public static Option Get(string? id)
    {
        var normalized = Normalize(id);
        return All.First(option => option.Id == normalized);
    }

    public sealed record Option(string Id, string NameResourceKey, Color Color);
}
