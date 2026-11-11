using System.Collections.Concurrent;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MyDrawVector.Helpers;

public static class ColorHelper
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);
    public static Color FromHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Colors.Black;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8 ||
            !byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var a) ||
            !byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !byte.TryParse(hex[6..8], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return Colors.Black;
        }

        return Color.FromArgb(a, r, g, b);
    }

    public static string ToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static SolidColorBrush Brush(string? value)
    {
        var color = FromHex(value);
        return BrushCache.GetOrAdd(ToHex(color), _ => new SolidColorBrush(color));
    }
}
