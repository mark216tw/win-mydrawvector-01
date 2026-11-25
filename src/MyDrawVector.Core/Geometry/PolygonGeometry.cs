using System.Numerics;
using MyDrawVector.Core.Models;

namespace MyDrawVector.Core.Geometry;

public readonly record struct PolygonSegment(
    PointD Start,
    PointD End,
    bool IsCurve,
    PointD Control1,
    PointD Control2);

public readonly record struct PolygonEdgeHit(
    int EdgeIndex,
    PointD Point,
    double Distance);

public static class PolygonGeometry
{
    private const int CurveSubdivisions = 16;

    public static List<PointD> GetLocalPoints(IReadOnlyList<PolygonVertex> vertices, double width, double height)
    {
        var result = new List<PointD>(vertices.Count);
        for (var i = 0; i < vertices.Count; i++)
        {
            result.Add(new PointD(vertices[i].X * width, vertices[i].Y * height));
        }

        return result;
    }

    public static List<PolygonSegment> BuildSegments(
        IReadOnlyList<PointD> points,
        IReadOnlyList<bool> smoothFlags,
        double smoothness,
        bool isClosed = true)
    {
        var segments = new List<PolygonSegment>();
        var count = points.Count;
        if (count < 2)
        {
            return segments;
        }

        var clampedSmoothness = Math.Clamp(smoothness, 0.0, 1.0);
        var factor = clampedSmoothness / 3.0;
        var segmentCount = isClosed ? count : count - 1;

        for (var i = 0; i < segmentCount; i++)
        {
            var nextIndex = (i + 1) % count;
            var p1 = points[i];
            var p2 = points[nextIndex];
            var startSmooth = i < smoothFlags.Count && smoothFlags[i];
            var endSmooth = nextIndex < smoothFlags.Count && smoothFlags[nextIndex];

            if ((!startSmooth && !endSmooth) || clampedSmoothness <= double.Epsilon || count < 3)
            {
                segments.Add(new PolygonSegment(p1, p2, false, p1, p2));
                continue;
            }

            var prevIndex = isClosed
                ? (i - 1 + count) % count
                : Math.Max(0, i - 1);
            var nextNextIndex = isClosed
                ? (i + 2) % count
                : Math.Min(count - 1, i + 2);

            var p0 = points[prevIndex];
            var p3 = points[nextNextIndex];

            var c1 = startSmooth
                ? new PointD(p1.X + ((p2.X - p0.X) * factor), p1.Y + ((p2.Y - p0.Y) * factor))
                : p1;

            var c2 = endSmooth
                ? new PointD(p2.X - ((p3.X - p1.X) * factor), p2.Y - ((p3.Y - p1.Y) * factor))
                : p2;

            segments.Add(new PolygonSegment(p1, p2, true, c1, c2));
        }

        return segments;
    }

    public static List<PolygonSegment> BuildSegments(PolygonObject polygon)
    {
        var width = Math.Max(1, polygon.Transform.Width);
        var height = Math.Max(1, polygon.Transform.Height);
        var points = GetLocalPoints(polygon.Vertices, width, height);
        var flags = polygon.Vertices.Select(v => v.IsSmooth).ToList();
        return BuildSegments(points, flags, polygon.Smoothness, isClosed: true);
    }

    public static List<PointD> FlattenSegments(IReadOnlyList<PolygonSegment> segments)
    {
        var polyline = new List<PointD>();
        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (i == 0)
            {
                polyline.Add(seg.Start);
            }

            if (!seg.IsCurve)
            {
                polyline.Add(seg.End);
                continue;
            }

            for (var step = 1; step <= CurveSubdivisions; step++)
            {
                var t = step / (double)CurveSubdivisions;
                polyline.Add(EvaluateCubicBezier(seg.Start, seg.Control1, seg.Control2, seg.End, t));
            }
        }

        return polyline;
    }

    public static bool IsPointInside(PointD point, IReadOnlyList<PointD> polygon)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var xi = polygon[i].X;
            var yi = polygon[i].Y;
            var xj = polygon[j].X;
            var yj = polygon[j].Y;

            var intersects = ((yi > point.Y) != (yj > point.Y))
                && (point.X < (((xj - xi) * (point.Y - yi)) / ((yj - yi) + double.Epsilon)) + xi);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static PolygonEdgeHit? FindClosestEdge(PointD localPoint, IReadOnlyList<PolygonSegment> segments)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        PolygonEdgeHit? best = null;
        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (!seg.IsCurve)
            {
                var (closest, dist) = ClosestPointOnLineSegment(localPoint, seg.Start, seg.End);
                if (best is null || dist < best.Value.Distance)
                {
                    best = new PolygonEdgeHit(i, closest, dist);
                }

                continue;
            }

            var prev = seg.Start;
            for (var step = 1; step <= CurveSubdivisions; step++)
            {
                var t = step / (double)CurveSubdivisions;
                var curr = EvaluateCubicBezier(seg.Start, seg.Control1, seg.Control2, seg.End, t);
                var (closest, dist) = ClosestPointOnLineSegment(localPoint, prev, curr);
                if (best is null || dist < best.Value.Distance)
                {
                    best = new PolygonEdgeHit(i, closest, dist);
                }

                prev = curr;
            }
        }

        return best;
    }

    public static void ApplyWorldVertices(
        PolygonObject polygon,
        IReadOnlyList<PointD> worldPoints,
        IReadOnlyList<bool> smoothFlags)
    {
        if (worldPoints.Count == 0)
        {
            return;
        }

        var angle = (float)(polygon.Transform.Rotation * Math.PI / 180.0);
        var rotScale = Matrix3x2.CreateScale(polygon.Transform.FlipH ? -1f : 1f, polygon.Transform.FlipV ? -1f : 1f)
            * Matrix3x2.CreateRotation(angle);

        if (!Matrix3x2.Invert(rotScale, out var invRotScale))
        {
            invRotScale = Matrix3x2.Identity;
        }

        var unshifted = new Vector2[worldPoints.Count];
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        for (var i = 0; i < worldPoints.Count; i++)
        {
            var v = Vector2.Transform(new Vector2((float)worldPoints[i].X, (float)worldPoints[i].Y), invRotScale);
            unshifted[i] = v;
            if (v.X < minX) minX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.X > maxX) maxX = v.X;
            if (v.Y > maxY) maxY = v.Y;
        }

        var spanX = maxX - minX;
        var spanY = maxY - minY;
        var newWidth = Math.Max(1.0, spanX);
        var newHeight = Math.Max(1.0, spanY);

        var uCenter = new Vector2((float)((minX + maxX) / 2.0), (float)((minY + maxY) / 2.0));
        var newCenter = Vector2.Transform(uCenter, rotScale);

        polygon.Transform.CenterX = newCenter.X;
        polygon.Transform.CenterY = newCenter.Y;
        polygon.Transform.Width = newWidth;
        polygon.Transform.Height = newHeight;

        var newVertices = new List<PolygonVertex>(worldPoints.Count);
        for (var i = 0; i < worldPoints.Count; i++)
        {
            var nx = spanX > 1e-6 ? (unshifted[i].X - minX) / newWidth : 0.5;
            var ny = spanY > 1e-6 ? (unshifted[i].Y - minY) / newHeight : 0.5;
            var isSmooth = i < smoothFlags.Count && smoothFlags[i];
            newVertices.Add(new PolygonVertex(nx, ny, isSmooth));
        }

        polygon.Vertices = newVertices;
    }

    public static List<PointD> GetWorldVertices(PolygonObject polygon)
    {
        var width = polygon.Transform.Width;
        var height = polygon.Transform.Height;
        var result = new List<PointD>(polygon.Vertices.Count);
        for (var i = 0; i < polygon.Vertices.Count; i++)
        {
            var v = polygon.Vertices[i];
            result.Add(polygon.Transform.LocalToWorld(new PointD(v.X * width, v.Y * height)));
        }

        return result;
    }

    public static PointD EvaluateCubicBezier(PointD p0, PointD p1, PointD p2, PointD p3, double t)
    {
        var u = 1.0 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        var x = (uuu * p0.X) + (3.0 * uu * t * p1.X) + (3.0 * u * tt * p2.X) + (ttt * p3.X);
        var y = (uuu * p0.Y) + (3.0 * uu * t * p1.Y) + (3.0 * u * tt * p2.Y) + (ttt * p3.Y);
        return new PointD(x, y);
    }

    public static (PointD Closest, double Distance) ClosestPointOnLineSegment(PointD point, PointD start, PointD end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= double.Epsilon)
        {
            var dist = double.Hypot(point.X - start.X, point.Y - start.Y);
            return (start, dist);
        }

        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        var closest = new PointD(start.X + (t * dx), start.Y + (t * dy));
        return (closest, double.Hypot(point.X - closest.X, point.Y - closest.Y));
    }
}
