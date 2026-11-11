using MyDrawVector.Core.Models;

namespace MyDrawVector.Services;

public sealed class TextToolStyle
{
    public string FontFamily { get; set; } = "Microsoft JhengHei";

    public double FontSize { get; set; } = 32;

    public int FontWeight { get; set; }

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    public string Color { get; set; } = "#FF111827";

    public TextAlign Align { get; set; } = TextAlign.Left;

    public double LineSpacing { get; set; } = 1.2;
}
