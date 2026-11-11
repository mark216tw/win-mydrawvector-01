namespace MyDrawVector.Core.Geometry;

public static class GridSnap
{
    public static double Snap(double value, double gridSize, double threshold = double.PositiveInfinity)
    {
        if (gridSize <= 0 || threshold < 0)
        {
            return value;
        }

        var snapped = Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
        return Math.Abs(snapped - value) <= threshold ? snapped : value;
    }
}
