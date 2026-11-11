using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.Models;
using Xunit;

namespace MyDrawVector.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void WorldLocalRoundTrip_WithRotationAndFlip()
    {
        var transform = new ObjectTransform
        {
            CenterX = 300,
            CenterY = 200,
            Width = 240,
            Height = 120,
            Rotation = 37,
            FlipH = true,
            FlipV = false,
        };

        var local = new PointD(12, 34);
        var world = transform.LocalToWorld(local);
        var roundTrip = transform.WorldToLocal(world);

        Assert.Equal(local.X, roundTrip.X, 4);
        Assert.Equal(local.Y, roundTrip.Y, 4);
    }

    [Fact]
    public void Corners_BoundsAccountForRotation()
    {
        var transform = new ObjectTransform
        {
            CenterX = 0,
            CenterY = 0,
            Width = 200,
            Height = 100,
            Rotation = 90,
        };

        var bounds = transform.GetBounds();

        Assert.Equal(-50, bounds.X, 3);
        Assert.Equal(-100, bounds.Y, 3);
        Assert.Equal(100, bounds.Width, 3);
        Assert.Equal(200, bounds.Height, 3);
    }

    [Fact]
    public void EllipseHitTest_RespectsShape()
    {
        var ellipse = new ShapeObject
        {
            Kind = ShapeKind.Ellipse,
            Transform = new ObjectTransform
            {
                CenterX = 0,
                CenterY = 0,
                Width = 200,
                Height = 100,
            },
        };

        Assert.True(ellipse.HitTest(new PointD(0, 0)));
        Assert.False(ellipse.HitTest(new PointD(99, 49)));
    }

    [Theory]
    [InlineData(ShapeKind.Line)]
    [InlineData(ShapeKind.Arrow)]
    public void LineAndArrowHitTest_UseBoundingBox(ShapeKind kind)
    {
        var line = new ShapeObject
        {
            Kind = kind,
            Transform = new ObjectTransform
            {
                CenterX = 100,
                CenterY = 100,
                Width = 200,
                Height = 200,
            },
            StrokeWidth = 2,
        };

        Assert.True(line.HitTest(new PointD(100, 100)));
        Assert.True(line.HitTest(new PointD(10, 190)));
        Assert.False(line.HitTest(new PointD(-1, 100)));
    }

    [Fact]
    public void FlipChangesLocalAxisDirection()
    {
        var transform = new ObjectTransform
        {
            CenterX = 0,
            CenterY = 0,
            Width = 100,
            Height = 100,
            FlipH = true,
        };

        var left = transform.LocalToWorld(new PointD(0, 50));
        var right = transform.LocalToWorld(new PointD(100, 50));

        Assert.True(left.X > right.X);
    }

    [Theory]
    [InlineData(9, 20, 0)]
    [InlineData(11, 20, 20)]
    [InlineData(31, 20, 40)]
    [InlineData(-11, 20, -20)]
    public void GridSnap_RoundsToNearestMultiple(double value, double grid, double expected)
    {
        Assert.Equal(expected, GridSnap.Snap(value, grid), 4);
    }

    [Fact]
    public void GridSnap_ReturnsInputForInvalidGrid()
    {
        Assert.Equal(37.5, GridSnap.Snap(37.5, 0), 4);
    }

    [Theory]
    [InlineData(12, 20, 8, 20)]
    [InlineData(18, 20, 8, 20)]
    [InlineData(35, 20, 8, 40)]
    [InlineData(9, 20, 8, 9)]
    [InlineData(31, 20, 8, 31)]
    [InlineData(20, 20, 0, 20)]
    [InlineData(21, 20, 0, 21)]
    public void GridSnap_RespectsThreshold(double value, double grid, double threshold, double expected)
    {
        Assert.Equal(expected, GridSnap.Snap(value, grid, threshold), 4);
    }
}
