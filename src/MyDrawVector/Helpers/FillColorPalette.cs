using Windows.UI;

namespace MyDrawVector.Helpers;

public static class FillColorPalette
{
    public static IReadOnlyList<Option> All { get; } =
    [
        new("black", "PresetColorBlack", Color.FromArgb(255, 17, 24, 39)),
        new("slate", "PresetColorSlate", Color.FromArgb(255, 71, 85, 105)),
        new("gray", "PresetColorGray", Color.FromArgb(255, 156, 163, 175)),
        new("light-gray", "PresetColorLightGray", Color.FromArgb(255, 209, 213, 219)),
        new("white", "PresetColorWhite", Color.FromArgb(255, 255, 255, 255)),
        new("red", "PresetColorRed", Color.FromArgb(255, 239, 68, 68)),
        new("orange", "PresetColorOrange", Color.FromArgb(255, 249, 115, 22)),
        new("amber", "PresetColorAmber", Color.FromArgb(255, 245, 158, 11)),
        new("yellow", "PresetColorYellow", Color.FromArgb(255, 250, 204, 21)),
        new("lime", "PresetColorLime", Color.FromArgb(255, 132, 204, 22)),
        new("green", "PresetColorGreen", Color.FromArgb(255, 34, 197, 94)),
        new("teal", "PresetColorTeal", Color.FromArgb(255, 20, 184, 166)),
        new("cyan", "PresetColorCyan", Color.FromArgb(255, 6, 182, 212)),
        new("sky", "PresetColorSky", Color.FromArgb(255, 56, 189, 248)),
        new("blue", "PresetColorBlue", Color.FromArgb(255, 59, 130, 246)),
        new("indigo", "PresetColorIndigo", Color.FromArgb(255, 99, 102, 241)),
        new("violet", "PresetColorViolet", Color.FromArgb(255, 139, 92, 246)),
        new("purple", "PresetColorPurple", Color.FromArgb(255, 168, 85, 247)),
        new("pink", "PresetColorPink", Color.FromArgb(255, 236, 72, 153)),
        new("brown", "PresetColorBrown", Color.FromArgb(255, 146, 64, 14)),
    ];

    public static Option? Get(string? id)
    {
        return All.FirstOrDefault(option => option.Id == id);
    }

    public sealed record Option(string Id, string NameResourceKey, Color Color);
}
