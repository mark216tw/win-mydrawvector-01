namespace MyDrawVector.Core.Models;

public sealed class CanvasSettings
{
    public double Width { get; set; } = 1123;

    public double Height { get; set; } = 794;

    public string Background { get; set; } = "#FFFFFFFF";

    public string Preset { get; set; } = "A4-landscape";

    public CanvasSettings Clone()
    {
        return new CanvasSettings
        {
            Width = Width,
            Height = Height,
            Background = Background,
            Preset = Preset,
        };
    }
}
