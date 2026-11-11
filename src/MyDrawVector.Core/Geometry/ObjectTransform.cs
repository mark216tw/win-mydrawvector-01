using System.Numerics;
using MyDrawVector.Core.Models;

namespace MyDrawVector.Core.Geometry;

public sealed class ObjectTransform
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Width { get; set; } = 100;

    public double Height { get; set; } = 100;

    public double Rotation { get; set; }

    public bool FlipH { get; set; }

    public bool FlipV { get; set; }

    public Matrix3x2 ToMatrix()
    {
        var angle = (float)(Rotation * Math.PI / 180.0);
        var matrix = Matrix3x2.CreateTranslation((float)(-Width / 2), (float)(-Height / 2));
        matrix *= Matrix3x2.CreateScale(FlipH ? -1f : 1f, FlipV ? -1f : 1f);
        matrix *= Matrix3x2.CreateRotation(angle);
        matrix *= Matrix3x2.CreateTranslation((float)CenterX, (float)CenterY);
        return matrix;
    }

    public PointD LocalToWorld(PointD local)
    {
        var point = Vector2.Transform(new Vector2((float)local.X, (float)local.Y), ToMatrix());
        return new PointD(point.X, point.Y);
    }

    public PointD WorldToLocal(PointD world)
    {
        if (!Matrix3x2.Invert(ToMatrix(), out var inverse))
        {
            return new PointD(0, 0);
        }

        var point = Vector2.Transform(new Vector2((float)world.X, (float)world.Y), inverse);
        return new PointD(point.X, point.Y);
    }

    public PointD[] GetWorldCorners()
    {
        return
        [
            LocalToWorld(new PointD(0, 0)),
            LocalToWorld(new PointD(Width, 0)),
            LocalToWorld(new PointD(Width, Height)),
            LocalToWorld(new PointD(0, Height)),
        ];
    }

    public RectD GetBounds()
    {
        var corners = GetWorldCorners();
        var minX = corners.Min(c => c.X);
        var minY = corners.Min(c => c.Y);
        var maxX = corners.Max(c => c.X);
        var maxY = corners.Max(c => c.Y);
        return new RectD(minX, minY, maxX - minX, maxY - minY);
    }

    public ObjectTransform Clone()
    {
        return new ObjectTransform
        {
            CenterX = CenterX,
            CenterY = CenterY,
            Width = Width,
            Height = Height,
            Rotation = Rotation,
            FlipH = FlipH,
            FlipV = FlipV,
        };
    }
}
