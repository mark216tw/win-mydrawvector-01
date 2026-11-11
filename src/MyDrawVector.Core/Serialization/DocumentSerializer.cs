using System.Text.Json;
using System.Text.Json.Serialization;
using MyDrawVector.Core.Models;

namespace MyDrawVector.Core.Serialization;

public static class DocumentSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static string SerializeCompact(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, CompactOptions);
    }

    public static Document Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new InvalidDataException("文件內容無效。");
        document.EnsureLayers();
        return document;
    }

    public static Document Clone(Document document)
    {
        return Deserialize(Serialize(document));
    }
}
