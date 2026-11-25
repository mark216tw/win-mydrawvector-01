namespace MyDrawVector.Core.Models;

public sealed class PolygonVertex
{
    public PolygonVertex()
    {
    }

    public PolygonVertex(double x, double y, bool isSmooth = false)
    {
        X = x;
        Y = y;
        IsSmooth = isSmooth;
    }

    public double X { get; set; }

    public double Y { get; set; }

    public bool IsSmooth { get; set; }

    public PolygonVertex Clone()
    {
        return new PolygonVertex(X, Y, IsSmooth);
    }
}
