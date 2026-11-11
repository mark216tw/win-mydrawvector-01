using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;
using Xunit;

namespace MyDrawVector.Core.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void RoundTrip_PreservesAllObjectTypes()
    {
        var document = Document.CreateDefault();
        document.Canvas.Width = 800;
        document.Canvas.Height = 600;
        document.Canvas.Background = "#FFFFFFFF";
        document.AddAsset(new AssetInfo
        {
            Id = "img_1",
            FileName = "img_1.png",
            Width = 640,
            Height = 480,
        });
        document.Objects.Add(new ShapeObject
        {
            Id = "shape_1",
            Name = "矩形 1",
            ZIndex = 0,
            Fill = "#FF22AA55",
            Stroke = "#FF000000",
            StrokeWidth = 3,
            Transform = new ObjectTransform
            {
                CenterX = 100,
                CenterY = 120,
                Width = 240,
                Height = 80,
                Rotation = 25,
                FlipH = true,
            },
        });
        document.Objects.Add(new TextObject
        {
            Id = "text_1",
            Name = "文字 1",
            ZIndex = 1,
            Text = "我的作品",
            FontFamily = "Microsoft JhengHei",
            FontSize = 36,
            FontWeight = 900,
            Bold = true,
            Align = TextAlign.Center,
        });
        document.Objects.Add(new ImageObject
        {
            Id = "image_1",
            Name = "圖片 1",
            ZIndex = 2,
            AssetId = "img_1",
        });
        document.Objects.Add(new PathObject
        {
            Id = "path_1",
            Name = "筆畫 1",
            ZIndex = 3,
            Points = [new PointD(0, 0), new PointD(0.5, 0.3), new PointD(1, 1)],
        });

        var json = DocumentSerializer.Serialize(document);
        var restored = DocumentSerializer.Deserialize(json);

        Assert.Equal(800, restored.Canvas.Width);
        Assert.Equal(4, restored.Objects.Count);
        Assert.IsType<ShapeObject>(restored.Objects[0]);
        Assert.IsType<TextObject>(restored.Objects[1]);
        Assert.IsType<ImageObject>(restored.Objects[2]);
        Assert.IsType<PathObject>(restored.Objects[3]);

        var shape = Assert.IsType<ShapeObject>(restored.Objects[0]);
        Assert.Equal(25, shape.Transform.Rotation);
        Assert.True(shape.Transform.FlipH);
        Assert.Equal("#FF22AA55", shape.Fill);

        var text = Assert.IsType<TextObject>(restored.Objects[1]);
        Assert.Equal("我的作品", text.Text);
        Assert.Equal(TextAlign.Center, text.Align);
        Assert.Equal(900, text.FontWeight);

        var path = Assert.IsType<PathObject>(restored.Objects[3]);
        Assert.Equal(3, path.Points.Count);
    }

    [Fact]
    public void CompactSerialization_RoundTripsWithoutIndentation()
    {
        var document = Document.CreateDefault();
        document.Objects.Add(new TextObject
        {
            Id = "text_1",
            Name = "文字 1",
            Text = "你好",
        });

        var compact = DocumentSerializer.SerializeCompact(document);
        Assert.DoesNotContain('\n', compact);

        var restored = DocumentSerializer.Deserialize(compact);
        var text = Assert.IsType<TextObject>(Assert.Single(restored.Objects));
        Assert.Equal("你好", text.Text);
    }

    [Fact]
    public void Clone_ProducesIndependentDocument()
    {
        var document = Document.CreateDefault();
        document.Objects.Add(new ShapeObject { Id = "a", Name = "A" });

        var clone = DocumentSerializer.Clone(document);
        clone.Objects[0].Name = "B";

        Assert.Equal("A", document.Objects[0].Name);
        Assert.Equal("B", clone.Objects[0].Name);
    }
}
