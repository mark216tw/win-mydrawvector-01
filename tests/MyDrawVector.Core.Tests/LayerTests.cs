using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;
using Xunit;

namespace MyDrawVector.Core.Tests;

public sealed class LayerTests
{
    [Fact]
    public void DefaultDocument_HasOneLayer()
    {
        var document = Document.CreateDefault();

        Assert.Single(document.Layers);
        Assert.Equal(0, document.Layers[0].Order);
    }

    [Fact]
    public void EnsureLayers_AssignsLegacyObjectsToFirstLayer()
    {
        var document = new Document
        {
            Objects = [new ShapeObject { Name = "舊物件" }],
        };

        document.EnsureLayers();

        Assert.Single(document.Layers);
        Assert.Equal(document.Layers[0].Id, document.Objects[0].LayerId);
    }

    [Fact]
    public void OrderedObjects_UsesLayerOrderThenObjectOrder()
    {
        var document = Document.CreateDefault();
        var bottom = document.Layers[0];
        var top = new Layer { Name = "上層", Order = 1 };
        document.Layers.Add(top);

        var a = new ShapeObject { Name = "A", LayerId = bottom.Id, ZIndex = 0 };
        var b = new ShapeObject { Name = "B", LayerId = bottom.Id, ZIndex = 1 };
        var c = new ShapeObject { Name = "C", LayerId = top.Id, ZIndex = 0 };
        document.Objects.AddRange([c, b, a]);
        document.NormalizeZIndexes();

        Assert.Equal(["A", "B", "C"], document.OrderedObjects().Select(o => o.Name));
    }

    [Fact]
    public void NextZIndex_IsPerLayer()
    {
        var document = Document.CreateDefault();
        var layer2 = new Layer { Name = "圖層 2", Order = 1 };
        document.Layers.Add(layer2);
        document.Objects.Add(new ShapeObject { LayerId = document.Layers[0].Id, ZIndex = 4 });

        Assert.Equal(5, document.NextZIndex(document.Layers[0].Id));
        Assert.Equal(0, document.NextZIndex(layer2.Id));
    }

    [Fact]
    public void Deserialize_MigratesVersionOneJson()
    {
        const string json = """
        {
          "canvas": { "width": 800, "height": 600, "background": "#FFFFFFFF", "preset": "custom" },
          "objects": [
            { "type": "shape", "id": "o1", "name": "矩形 1", "zIndex": 0, "kind": "Rectangle", "transform": { "centerX": 10, "centerY": 20, "width": 30, "height": 40, "rotation": 0, "flipH": false, "flipV": false } }
          ],
          "assets": []
        }
        """;

        var document = DocumentSerializer.Deserialize(json);

        Assert.Single(document.Layers);
        Assert.Equal(document.Layers[0].Id, document.Objects[0].LayerId);
    }

    [Fact]
    public void Clone_PreservesLayerId()
    {
        var document = Document.CreateDefault();
        var item = new ShapeObject { LayerId = document.Layers[0].Id };
        var clone = item.Clone();

        Assert.Equal(item.LayerId, clone.LayerId);
    }
}
