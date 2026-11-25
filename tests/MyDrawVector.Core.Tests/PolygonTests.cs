using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;
using Xunit;

namespace MyDrawVector.Core.Tests;

public sealed class PolygonTests
{
    [Fact]
    public void PolygonObject_SerializationRoundTrip_PreservesVerticesAndProperties()
    {
        var document = new Document();
        document.EnsureLayers();

        var polygon = new PolygonObject
        {
            Name = "曲線多邊形",
            LayerId = document.Layers[0].Id,
            Fill = "#FF22AA88",
            Stroke = "#FF112233",
            StrokeWidth = 3.5,
            Dash = "dash",
            IsFilled = true,
            IsStroked = true,
            Smoothness = 0.75,
            Vertices =
            [
                new PolygonVertex(0.5, 0.0, isSmooth: true),
                new PolygonVertex(1.0, 1.0, isSmooth: false),
                new PolygonVertex(0.0, 1.0, isSmooth: true),
            ],
        };
        polygon.Transform.CenterX = 200;
        polygon.Transform.CenterY = 150;
        polygon.Transform.Width = 120;
        polygon.Transform.Height = 90;

        document.Objects.Add(polygon);

        var json = DocumentSerializer.Serialize(document);
        var loaded = DocumentSerializer.Deserialize(json);

        var restored = Assert.IsType<PolygonObject>(Assert.Single(loaded.Objects));
        Assert.Equal("曲線多邊形", restored.Name);
        Assert.Equal("#FF22AA88", restored.Fill);
        Assert.Equal("#FF112233", restored.Stroke);
        Assert.Equal(3.5, restored.StrokeWidth);
        Assert.Equal("dash", restored.Dash);
        Assert.Equal(0.75, restored.Smoothness);
        Assert.Equal(3, restored.Vertices.Count);
        Assert.True(restored.Vertices[0].IsSmooth);
        Assert.False(restored.Vertices[1].IsSmooth);
        Assert.True(restored.Vertices[2].IsSmooth);
    }

    [Fact]
    public void ApplyWorldVertices_WithRotationAndFlip_PreservesUnmovedWorldVertices()
    {
        var polygon = new PolygonObject();
        polygon.Transform.Rotation = 35;
        polygon.Transform.FlipH = true;
        polygon.Transform.FlipV = false;

        var initialWorld = new List<PointD>
        {
            new(100, 100),
            new(260, 120),
            new(210, 250),
            new(90, 210),
        };
        var flags = new List<bool> { false, true, false, true };

        PolygonGeometry.ApplyWorldVertices(polygon, initialWorld, flags);

        var restoredInitial = PolygonGeometry.GetWorldVertices(polygon);
        for (var i = 0; i < initialWorld.Count; i++)
        {
            Assert.Equal(initialWorld[i].X, restoredInitial[i].X, 2);
            Assert.Equal(initialWorld[i].Y, restoredInitial[i].Y, 2);
        }

        // Move vertex 1 only and verify vertices 0, 2, 3 do not drift in world space
        var updatedWorld = new List<PointD>(restoredInitial)
        {
            [1] = new(340, 60),
        };

        PolygonGeometry.ApplyWorldVertices(polygon, updatedWorld, flags);
        var afterMove = PolygonGeometry.GetWorldVertices(polygon);

        for (var i = 0; i < updatedWorld.Count; i++)
        {
            Assert.Equal(updatedWorld[i].X, afterMove[i].X, 2);
            Assert.Equal(updatedWorld[i].Y, afterMove[i].Y, 2);
        }
    }

    [Fact]
    public void BuildSegments_MixedCornerAndSmooth_ProducesLineAndBezierSegments()
    {
        var points = new List<PointD>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };
        var flags = new List<bool> { false, false, true, false };

        var segments = PolygonGeometry.BuildSegments(points, flags, smoothness: 0.5, isClosed: true);

        Assert.Equal(4, segments.Count);
        Assert.False(segments[0].IsCurve); // 0 -> 1 both corner
        Assert.True(segments[1].IsCurve);  // 1 -> 2 corner to smooth
        Assert.Equal(points[1].X, segments[1].Control1.X, 4);
        Assert.Equal(points[1].Y, segments[1].Control1.Y, 4);
        Assert.True(segments[2].IsCurve);  // 2 -> 3 smooth to corner
        Assert.Equal(points[3].X, segments[2].Control2.X, 4);
        Assert.Equal(points[3].Y, segments[2].Control2.Y, 4);
        Assert.False(segments[3].IsCurve); // 3 -> 0 both corner
    }

    [Fact]
    public void ContainsLocal_FilledAndUnfilled_HitTestsAccurately()
    {
        var polygon = new PolygonObject
        {
            IsFilled = true,
            IsStroked = true,
            StrokeWidth = 2,
        };

        PolygonGeometry.ApplyWorldVertices(
            polygon,
            [new PointD(0, 0), new PointD(100, 0), new PointD(100, 100), new PointD(0, 100)],
            [false, false, false, false]);

        Assert.True(polygon.ContainsLocal(new PointD(50, 50)));
        Assert.False(polygon.ContainsLocal(new PointD(150, 50)));

        // Unfilled polygon should miss center but hit edge
        polygon.IsFilled = false;
        Assert.False(polygon.ContainsLocal(new PointD(50, 50)));
        Assert.True(polygon.ContainsLocal(new PointD(50, 1)));
    }

    [Fact]
    public void Clone_CreatesDeepCopyOfVertices()
    {
        var original = new PolygonObject
        {
            Smoothness = 0.8,
            Vertices =
            [
                new PolygonVertex(0, 0, true),
                new PolygonVertex(1, 0, false),
                new PolygonVertex(0.5, 1, true),
            ],
        };

        var clone = Assert.IsType<PolygonObject>(original.Clone());
        clone.Vertices[0].X = 0.25;
        clone.Vertices[0].IsSmooth = false;

        Assert.Equal(0, original.Vertices[0].X);
        Assert.True(original.Vertices[0].IsSmooth);
        Assert.Equal(0.8, clone.Smoothness);
    }
}
