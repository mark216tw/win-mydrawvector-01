namespace MyDrawVector.Core.Models;

public sealed class Document
{
    public CanvasSettings Canvas { get; set; } = new();

    public List<Layer> Layers { get; set; } = [];

    public List<SceneObject> Objects { get; set; } = [];

    public List<AssetInfo> Assets { get; set; } = [];

    public static Document CreateDefault()
    {
        var document = new Document
        {
            Canvas = new CanvasSettings(),
            Layers = [],
            Objects = [],
            Assets = [],
        };
        document.EnsureLayers();
        return document;
    }

    public void EnsureLayers()
    {
        Layers ??= [];
        Objects ??= [];
        Assets ??= [];

        if (Layers.Count == 0)
        {
            Layers.Add(new Layer { Name = "圖層 1", Order = 0 });
        }

        foreach (var layer in Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Id))
            {
                layer.Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(layer.Name))
            {
                layer.Name = "圖層";
            }
        }

        NormalizeLayerOrders();

        var fallbackLayer = Layers.OrderBy(l => l.Order).First();
        var validIds = Layers.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in Objects)
        {
            if (string.IsNullOrWhiteSpace(item.LayerId) || !validIds.Contains(item.LayerId))
            {
                item.LayerId = fallbackLayer.Id;
            }
        }

        NormalizeZIndexes();
    }

    public Layer? FindLayer(string id)
    {
        return Layers.FirstOrDefault(l => l.Id == id);
    }

    public IEnumerable<SceneObject> OrderedObjects()
    {
        foreach (var layer in Layers.OrderBy(l => l.Order))
        {
            foreach (var item in Objects.Where(o => o.LayerId == layer.Id).OrderBy(o => o.ZIndex))
            {
                yield return item;
            }
        }
    }

    public IEnumerable<SceneObject> ObjectsInLayer(string layerId)
    {
        return Objects.Where(o => o.LayerId == layerId).OrderBy(o => o.ZIndex);
    }

    public SceneObject? FindObject(string id)
    {
        return Objects.FirstOrDefault(o => o.Id == id);
    }

    public int NextZIndex(string layerId)
    {
        var items = Objects.Where(o => o.LayerId == layerId).ToList();
        return items.Count == 0 ? 0 : items.Max(o => o.ZIndex) + 1;
    }

    public int CountObjects(string layerId)
    {
        return Objects.Count(o => o.LayerId == layerId);
    }

    public void NormalizeLayerOrders()
    {
        var index = 0;
        foreach (var layer in Layers.OrderBy(l => l.Order).ThenBy(l => l.Name).ToList())
        {
            layer.Order = index++;
        }
    }

    public void NormalizeZIndexes()
    {
        foreach (var layer in Layers)
        {
            var index = 0;
            foreach (var item in Objects.Where(o => o.LayerId == layer.Id).OrderBy(o => o.ZIndex).ToList())
            {
                item.ZIndex = index++;
            }
        }
    }

    public void AddAsset(AssetInfo asset)
    {
        if (!Assets.Any(a => a.Id == asset.Id))
        {
            Assets.Add(asset);
        }
    }

    public AssetInfo? FindAsset(string id)
    {
        return Assets.FirstOrDefault(a => a.Id == id);
    }
}
