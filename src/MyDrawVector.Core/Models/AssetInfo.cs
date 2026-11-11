namespace MyDrawVector.Core.Models;

public sealed class AssetInfo
{
    public string Id { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }
}
