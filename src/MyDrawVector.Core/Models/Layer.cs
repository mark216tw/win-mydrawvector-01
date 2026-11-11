namespace MyDrawVector.Core.Models;

public sealed class Layer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "圖層";

    public int Order { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsLocked { get; set; }

    public Layer Clone()
    {
        return new Layer
        {
            Id = Id,
            Name = Name,
            Order = Order,
            IsVisible = IsVisible,
            IsLocked = IsLocked,
        };
    }
}
