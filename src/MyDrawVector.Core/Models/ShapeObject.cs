namespace MyDrawVector.Core.Models;

public sealed class ShapeObject : SceneObject
{
    public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;

    public string Fill { get; set; } = "#FF4F9CFF";

    public string Stroke { get; set; } = "#FF1F2937";

    public double StrokeWidth { get; set; } = 2;

    public string Dash { get; set; } = "solid";

    public double CornerRadius { get; set; } = 12;

    public bool IsFilled { get; set; } = true;

    public bool IsStroked { get; set; } = true;

    public override bool ContainsLocal(PointD local)
    {
        var width = Transform.Width;
        var height = Transform.Height;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (Kind == ShapeKind.Ellipse)
        {
            var nx = ((local.X - (width / 2)) / (width / 2));
            var ny = ((local.Y - (height / 2)) / (height / 2));
            return (nx * nx) + (ny * ny) <= 1.0;
        }

        return base.ContainsLocal(local);
    }

    public override SceneObject Clone()
    {
        var clone = new ShapeObject();
        CopyBaseTo(clone);
        clone.Kind = Kind;
        clone.Fill = Fill;
        clone.Stroke = Stroke;
        clone.StrokeWidth = StrokeWidth;
        clone.Dash = Dash;
        clone.CornerRadius = CornerRadius;
        clone.IsFilled = IsFilled;
        clone.IsStroked = IsStroked;
        return clone;
    }
}
