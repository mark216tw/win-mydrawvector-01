namespace MyDrawVector.Core.Geometry;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);

    public bool Contains(double px, double py)
    {
        return px >= X && px <= Right && py >= Y && py <= Bottom;
    }
}
