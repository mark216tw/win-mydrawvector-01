using System.Text.Json.Serialization;
using MyDrawVector.Core.Geometry;

namespace MyDrawVector.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ShapeObject), "shape")]
[JsonDerivedType(typeof(TextObject), "text")]
[JsonDerivedType(typeof(ImageObject), "image")]
[JsonDerivedType(typeof(PathObject), "path")]
public abstract class SceneObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string LayerId { get; set; } = string.Empty;

    public int ZIndex { get; set; }

    public ObjectTransform Transform { get; set; } = new();

    public double Opacity { get; set; } = 1;

    public bool IsVisible { get; set; } = true;

    public bool IsLocked { get; set; }

    [JsonIgnore]
    public RectD WorldBounds => Transform.GetBounds();

    public PointD ToLocal(PointD world)
    {
        return Transform.WorldToLocal(world);
    }

    public bool HitTest(PointD world)
    {
        if (!IsVisible || IsLocked)
        {
            return false;
        }

        var local = ToLocal(world);
        return ContainsLocal(local);
    }

    public virtual bool ContainsLocal(PointD local)
    {
        return local.X >= 0 && local.X <= Transform.Width && local.Y >= 0 && local.Y <= Transform.Height;
    }

    public abstract SceneObject Clone();

    protected void CopyBaseTo(SceneObject target)
    {
        target.Id = Id;
        target.Name = Name;
        target.LayerId = LayerId;
        target.ZIndex = ZIndex;
        target.Transform = Transform.Clone();
        target.Opacity = Opacity;
        target.IsVisible = IsVisible;
        target.IsLocked = IsLocked;
    }
}
