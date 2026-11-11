namespace MyDrawVector.Core.Models;

public sealed class TextObject : SceneObject
{
    public string Text { get; set; } = "文字";

    public string FontFamily { get; set; } = "Microsoft JhengHei";

    public double FontSize { get; set; } = 32;

    public int FontWeight { get; set; }

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    public string Color { get; set; } = "#FF111827";

    public TextAlign Align { get; set; } = TextAlign.Left;

    public double LineSpacing { get; set; } = 1.2;

    public override SceneObject Clone()
    {
        var clone = new TextObject();
        CopyBaseTo(clone);
        clone.Text = Text;
        clone.FontFamily = FontFamily;
        clone.FontSize = FontSize;
        clone.FontWeight = FontWeight;
        clone.Bold = Bold;
        clone.Italic = Italic;
        clone.Underline = Underline;
        clone.Color = Color;
        clone.Align = Align;
        clone.LineSpacing = LineSpacing;
        return clone;
    }
}
