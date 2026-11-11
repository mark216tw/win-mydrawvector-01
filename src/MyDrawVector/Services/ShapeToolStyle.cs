namespace MyDrawVector.Services;

public sealed class ShapeToolStyle
{
    public string Fill { get; set; } = "#FF4F9CFF";

    public string Stroke { get; set; } = "#FF1F2937";

    public double StrokeWidth { get; set; } = 2;

    public string Dash { get; set; } = "solid";

    public double CornerRadius { get; set; } = 12;

    public bool IsFilled { get; set; } = true;

    public bool IsStroked { get; set; } = true;
}
