using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;
using Xunit;

namespace MyDrawVector.Core.Tests;

public sealed class DocumentPackageTests
{
    [Fact]
    public async Task SaveAndOpen_RoundTripsDocumentAndAssets()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MyDrawVectorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "test.mydraw");
        try
        {
            var document = Document.CreateDefault();
            document.Canvas.Width = 640;
            document.Canvas.Height = 480;
            document.AddAsset(new AssetInfo
            {
                Id = "img_test",
                FileName = "img_test.png",
                Width = 32,
                Height = 32,
            });
            document.Objects.Add(new TextObject
            {
                Name = "文字 1",
                Text = "測試",
                Transform = new ObjectTransform
                {
                    CenterX = 200,
                    CenterY = 150,
                    Width = 200,
                    Height = 80,
                },
            });

            var assets = new Dictionary<string, byte[]>
            {
                ["img_test"] = [1, 2, 3, 4, 5],
            };

            await DocumentPackageService.SaveAsync(document, assets, path);
            Assert.True(File.Exists(path));

            var (loaded, loadedAssets) = await DocumentPackageService.OpenAsync(path);
            Assert.Equal(640, loaded.Canvas.Width);
            Assert.Single(loaded.Objects);
            Assert.Equal("測試", Assert.IsType<TextObject>(loaded.Objects[0]).Text);
            Assert.Equal([1, 2, 3, 4, 5], loadedAssets["img_test"]);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Open_RejectsFileWithoutDocumentJson()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MyDrawVectorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "broken.mydraw");
        try
        {
            await using (var file = File.Create(path))
            using (var archive = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create))
            {
                _ = archive.CreateEntry("manifest.json");
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => DocumentPackageService.OpenAsync(path));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
