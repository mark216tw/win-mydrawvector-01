using MyDrawVector.Core.Geometry;

namespace MyDrawVector.Core.Models;

public sealed class PolygonObject : SceneObject
{
    public List<PolygonVertex> Vertices { get; set; } = [];

    public string Fill { get; set; } = "#FF4F9CFF";

    public string Stroke { get; set; } = "#FF1F2937";

    public double StrokeWidth { get; set; } = 2;

    public string Dash { get; set; } = "solid";

    public bool IsFilled { get; set; } = true;

    public bool IsStroked { get; set; } = true;

    public double Smoothness { get; set; } = 0.5;

    public override bool ContainsLocal(PointD local)
    {
        if (Vertices.Count < 3 || Transform.Width <= 0 || Transform.Height <= 0)
        {
            return false;
        }

        var segments = PolygonGeometry.BuildSegments(this);
        if (IsFilled)
        {
            var polyline = PolygonGeometry.FlattenSegments(segments);
            if (PolygonGeometry.IsPointInside(local, polyline))
            {
                return true;
            }
        }

        if (IsStroked || !IsFilled)
        {
            var tolerance = Math.Max(6, (StrokeWidth / 2) + 4);
            var hit = PolygonGeometry.FindClosestEdge(local, segments);
            if (hit is not null && hit.Value.Distance <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    public override SceneObject Clone()
    {
        var clone = new PolygonObject();
        CopyBaseTo(clone);
        clone.Vertices = Vertices.Select(v => v.Clone()).ToList();
        clone.Fill = Fill;
        clone.Stroke = Stroke;
        clone.StrokeWidth = StrokeWidth;
        clone.Dash = Dash;
        clone.IsFilled = IsFilled;
        clone.IsStroked = IsStroked;
        clone.Smoothness = Smoothness;
        return clone;
    }
}
