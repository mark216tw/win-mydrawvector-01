using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;

namespace MyDrawVector.Services;

public sealed class FileService
{
    public Task SaveAsync(
        Document document,
        IReadOnlyDictionary<string, StoredImage> images,
        string path)
    {
        var assets = images.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Data,
            StringComparer.OrdinalIgnoreCase);
        return DocumentPackageService.SaveAsync(document, assets, path);
    }

    public Task<(Document Document, Dictionary<string, byte[]> Assets)> OpenAsync(string path)
    {
        return DocumentPackageService.OpenAsync(path);
    }
}
