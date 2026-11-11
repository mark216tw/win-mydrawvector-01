namespace MyDrawVector.Services;

public sealed class AppSettings
{
    public int WindowX { get; set; } = -1;

    public int WindowY { get; set; } = -1;

    public int WindowWidth { get; set; } = 1280;

    public int WindowHeight { get; set; } = 820;

    public bool IsWindowMaximized { get; set; }

    public bool ShowGrid { get; set; }

    public bool SnapToGrid { get; set; } = true;

    public bool AutoSelectToolOnObjectClick { get; set; }

    public string ThemeMode { get; set; } = string.Empty;

    public string ThemeColor { get; set; } = AppThemePalette.DefaultId;

    public string ShapeFill { get; set; } = "#FF4F9CFF";

    public string ShapeStroke { get; set; } = "#FF1F2937";

    public double ShapeStrokeWidth { get; set; } = 2;

    public string ShapeDash { get; set; } = "solid";

    public double ShapeCornerRadius { get; set; } = 12;

    public bool ShapeIsFilled { get; set; } = true;

    public bool ShapeIsStroked { get; set; } = true;

    public Dictionary<string, ShapeToolStyle> ShapeToolStyles { get; set; } = [];

    public TextToolStyle TextStyle { get; set; } = new();

    public string PencilStroke { get; set; } = "#FF1F2937";

    public double PencilStrokeWidth { get; set; } = 3;

    public string PencilDash { get; set; } = "solid";

    public List<string> RecentFiles { get; set; } = [];
}
