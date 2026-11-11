namespace MyDrawVector.Core.Models;

public sealed class PathObject : SceneObject
{
    public List<PointD> Points { get; set; } = [];

    public string Stroke { get; set; } = "#FF1F2937";

    public double StrokeWidth { get; set; } = 3;

    public string Dash { get; set; } = "solid";

    public override bool ContainsLocal(PointD local)
    {
        if (Points.Count == 0)
        {
            return false;
        }

        if (base.ContainsLocal(local))
        {
            return true;
        }

        var tolerance = Math.Max(6, StrokeWidth + 4);
        for (var i = 1; i < Points.Count; i++)
        {
            var a = new PointD(Points[i - 1].X * Transform.Width, Points[i - 1].Y * Transform.Height);
            var b = new PointD(Points[i].X * Transform.Width, Points[i].Y * Transform.Height);
            if (DistanceToSegment(local, a, b) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static double DistanceToSegment(PointD point, PointD start, PointD end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= double.Epsilon)
        {
            return Math.Sqrt(Math.Pow(point.X - start.X, 2) + Math.Pow(point.Y - start.Y, 2));
        }

        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var px = start.X + (t * dx);
        var py = start.Y + (t * dy);
        return Math.Sqrt(Math.Pow(point.X - px, 2) + Math.Pow(point.Y - py, 2));
    }

    public override SceneObject Clone()
    {
        var clone = new PathObject();
        CopyBaseTo(clone);
        clone.Points = Points.Select(p => new PointD(p.X, p.Y)).ToList();
        clone.Stroke = Stroke;
        clone.StrokeWidth = StrokeWidth;
        clone.Dash = Dash;
        return clone;
    }
}
