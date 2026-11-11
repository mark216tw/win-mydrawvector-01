using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MyDrawVector.Core.Models;

namespace MyDrawVector.Core.Serialization;

public static class DocumentPackageService
{
    private static readonly JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };

    public static async Task SaveAsync(
        Document document,
        IReadOnlyDictionary<string, byte[]> assets,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new InvalidOperationException("儲存路徑無效。");
        }

        Directory.CreateDirectory(folder);
        var tempPath = path + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            await using (var file = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             useAsync: true))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                var manifest = new
                {
                    schema = "mydrawvector",
                    version = 2,
                    appVersion = typeof(DocumentPackageService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
                    created = DateTimeOffset.Now,
                    modified = DateTimeOffset.Now,
                };
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, ManifestOptions);
                }

                var documentEntry = archive.CreateEntry("document.json", CompressionLevel.Optimal);
                await using (var documentStream = documentEntry.Open())
                await using (var writer = new StreamWriter(documentStream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(DocumentSerializer.Serialize(document));
                }

                foreach (var asset in document.Assets)
                {
                    if (!assets.TryGetValue(asset.Id, out var data) ||
                        !IsSafeAssetFileName(asset.FileName))
                    {
                        continue;
                    }

                    var assetEntry = archive.CreateEntry($"assets/{asset.FileName}", CompressionLevel.NoCompression);
                    await using var assetStream = assetEntry.Open();
                    await assetStream.WriteAsync(data);
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }

            throw;
        }
    }

    public static async Task<(Document Document, Dictionary<string, byte[]> Assets)> OpenAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var file = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);

        var documentEntry = archive.GetEntry("document.json")
            ?? throw new InvalidDataException("檔案缺少 document.json。");
        string json;
        await using (var stream = documentEntry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            json = await reader.ReadToEndAsync();
        }

        var document = DocumentSerializer.Deserialize(json);
        var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in document.Assets)
        {
            if (!IsSafeAssetFileName(asset.FileName))
            {
                continue;
            }

            var entry = archive.GetEntry($"assets/{asset.FileName}");
            if (entry is null)
            {
                continue;
            }

            await using var stream = entry.Open();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            assets[asset.Id] = memory.ToArray();
        }

        return (document, assets);
    }

    private static bool IsSafeAssetFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
               string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
               !fileName.Contains("..", StringComparison.Ordinal);
    }
}
