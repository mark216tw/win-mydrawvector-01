using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;

namespace MyDrawVector.Services;

public sealed class AutosaveInfo
{
    public string? OriginalPath { get; set; }

    public DateTime SavedAt { get; set; }
}

public sealed class AutosaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly ILogger<AutosaveService> _logger;

    public AutosaveService(ILogger<AutosaveService> logger)
    {
        _logger = logger;
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyDrawVector",
            "Autosave");
    }

    public string DocumentPath => Path.Combine(_folder, "autosave.mydraw");

    private string MetadataPath => Path.Combine(_folder, "autosave.json");

    public bool HasRecoverable => File.Exists(DocumentPath) && File.Exists(MetadataPath);

    public async Task SaveAsync(
        Document document,
        IReadOnlyDictionary<string, StoredImage> images,
        string? originalPath)
    {
        Directory.CreateDirectory(_folder);
        var assets = images.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Data,
            StringComparer.OrdinalIgnoreCase);
        await DocumentPackageService.SaveAsync(document, assets, DocumentPath);

        var info = new AutosaveInfo
        {
            OriginalPath = originalPath,
            SavedAt = DateTime.Now,
        };
        var temp = MetadataPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(info, JsonOptions));
        File.Move(temp, MetadataPath, true);
    }

    public AutosaveInfo? LoadInfo()
    {
        if (!HasRecoverable)
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(MetadataPath);
            return JsonSerializer.Deserialize<AutosaveInfo>(json);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to read autosave metadata");
            return null;
        }
    }

    public void Delete()
    {
        TryDelete(DocumentPath);
        TryDelete(MetadataPath);
        TryDelete(MetadataPath + ".tmp");
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to delete autosave file {Path}", path);
        }
    }
}
