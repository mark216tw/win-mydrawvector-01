using System.Security.Cryptography;
using Microsoft.UI.Xaml.Media.Imaging;
using MyDrawVector.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MyDrawVector.Services;

public sealed class ImageAssetStore
{
    private readonly Dictionary<string, StoredImage> _images = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, StoredImage> Images => _images;

    public StoredImage? Find(string id)
    {
        return _images.GetValueOrDefault(id);
    }

    public async Task<AssetInfo> AddAsync(byte[] data, string extension, string? originalFileName = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            throw new InvalidDataException("圖片內容為空。");
        }

        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var id = $"img_{hash[..16]}";
        if (_images.TryGetValue(id, out var existing))
        {
            return existing.Info;
        }

        var (width, height) = await GetDimensionsAsync(data);
        var bitmap = await CreateBitmapAsync(data);
        var normalizedExtension = NormalizeExtension(extension);
        var fileName = $"{id}{normalizedExtension}";
        _ = originalFileName;

        var info = new AssetInfo
        {
            Id = id,
            FileName = fileName,
            Width = width,
            Height = height,
        };

        _images[id] = new StoredImage(info, data, bitmap);
        return info;
    }

    public async Task LoadAsync(AssetInfo info, byte[] data)
    {
        if (_images.ContainsKey(info.Id))
        {
            return;
        }

        var bitmap = await CreateBitmapAsync(data);
        _images[info.Id] = new StoredImage(info, data, bitmap);
    }

    public static string NormalizeExtension(string? extension)
    {
        var value = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (!value.StartsWith('.'))
        {
            value = "." + value;
        }

        return value switch
        {
            ".png" => ".png",
            ".jpg" => ".jpg",
            ".jpeg" => ".jpg",
            ".bmp" => ".bmp",
            ".gif" => ".gif",
            _ => ".png",
        };
    }

    private static async Task<(int Width, int Height)> GetDimensionsAsync(byte[] data)
    {
        using var stream = await CreateStreamAsync(data);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return ((int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }

    private static async Task<BitmapImage> CreateBitmapAsync(byte[] data)
    {
        using var stream = await CreateStreamAsync(data);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private static async Task<InMemoryRandomAccessStream> CreateStreamAsync(byte[] data)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(data);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return stream;
    }
}
