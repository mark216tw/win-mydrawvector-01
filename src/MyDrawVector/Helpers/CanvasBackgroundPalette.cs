using Windows.UI;

namespace MyDrawVector.Helpers;

public static class CanvasBackgroundPalette
{
    public const string TransparentId = "transparent";

    public static Color CheckerLightColor { get; } = Color.FromArgb(255, 255, 255, 255);

    public static Color CheckerDarkColor { get; } = Color.FromArgb(255, 242, 243, 245);

    public static IReadOnlyList<Option> All { get; } =
    [
        new(TransparentId, "CanvasBackgroundTransparent", Color.FromArgb(0, 0, 0, 0)),
        new("white", "CanvasBackgroundWhite", Color.FromArgb(255, 255, 255, 255)),
        new("light-gray", "CanvasBackgroundLightGray", Color.FromArgb(255, 243, 244, 246)),
        new("gray", "CanvasBackgroundGray", Color.FromArgb(255, 156, 163, 175)),
        new("black", "CanvasBackgroundBlack", Color.FromArgb(255, 17, 24, 39)),
        new("cream", "CanvasBackgroundCream", Color.FromArgb(255, 255, 247, 230)),
        new("light-blue", "CanvasBackgroundLightBlue", Color.FromArgb(255, 224, 242, 254)),
        new("light-green", "CanvasBackgroundLightGreen", Color.FromArgb(255, 220, 252, 231)),
        new("light-yellow", "CanvasBackgroundLightYellow", Color.FromArgb(255, 254, 249, 195)),
        new("light-pink", "CanvasBackgroundLightPink", Color.FromArgb(255, 252, 231, 243)),
    ];

    public static Option? Get(string? id)
    {
        return All.FirstOrDefault(option => option.Id == id);
    }

    public static Option? Match(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var normalized = ColorHelper.ToHex(ColorHelper.FromHex(hex));
        return All.FirstOrDefault(option => ColorHelper.ToHex(option.Color) == normalized);
    }

    public sealed record Option(string Id, string NameResourceKey, Color Color);
}
