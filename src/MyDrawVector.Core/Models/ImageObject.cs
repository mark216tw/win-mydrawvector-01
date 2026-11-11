namespace MyDrawVector.Core.Models;

public sealed class ImageObject : SceneObject
{
    public string AssetId { get; set; } = string.Empty;

    public string Stretch { get; set; } = "fill";

    public override SceneObject Clone()
    {
        var clone = new ImageObject();
        CopyBaseTo(clone);
        clone.AssetId = AssetId;
        clone.Stretch = Stretch;
        return clone;
    }
}
