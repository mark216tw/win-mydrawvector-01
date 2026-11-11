using System.Text.Json;
using MyDrawVector.Core.Models;

namespace MyDrawVector.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _folder;
    private readonly string _path;

    public SettingsService()
    {
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyDrawVector");
        _path = Path.Combine(_folder, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
                File.Move(temp, _path, true);
            }
            catch
            {
                // Settings must never prevent the editor from working.
            }
        }
    }

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Current.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Current.RecentFiles.Insert(0, path);
        if (Current.RecentFiles.Count > 10)
        {
            Current.RecentFiles.RemoveRange(10, Current.RecentFiles.Count - 10);
        }

        Save();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                if (string.IsNullOrWhiteSpace(settings.ThemeMode))
                {
                    var legacy = JsonSerializer.Deserialize<LegacySettings>(json);
                    if (legacy?.IsDarkMode == true)
                    {
                        settings.ThemeMode = "Dark";
                    }
                }

                return Normalize(settings);
            }
        }
        catch
        {
            // Fall back to defaults when the settings file is unreadable.
        }

        return Normalize(new AppSettings());
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.ThemeMode = settings.ThemeMode switch
        {
            "Light" => "Light",
            "Dark" => "Dark",
            _ => "System",
        };
        settings.ThemeColor = AppThemePalette.Normalize(settings.ThemeColor);
        settings.ShapeToolStyles ??= [];
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var key = kind.ToString();
            if (!settings.ShapeToolStyles.TryGetValue(key, out var style) || style is null)
            {
                settings.ShapeToolStyles[key] = CreateLegacyShapeStyle(settings);
            }
        }

        settings.TextStyle ??= new TextToolStyle();
        return settings;
    }

    private static ShapeToolStyle CreateLegacyShapeStyle(AppSettings settings)
    {
        return new ShapeToolStyle
        {
            Fill = settings.ShapeFill,
            Stroke = settings.ShapeStroke,
            StrokeWidth = settings.ShapeStrokeWidth,
            Dash = settings.ShapeDash,
            CornerRadius = settings.ShapeCornerRadius,
            IsFilled = settings.ShapeIsFilled,
            IsStroked = settings.ShapeIsStroked,
        };
    }

    private sealed class LegacySettings
    {
        public bool IsDarkMode { get; set; }
    }
}
