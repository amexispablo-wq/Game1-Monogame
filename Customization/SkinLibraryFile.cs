#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ColorBlocks;

public sealed class SkinLibraryFile
{
    [JsonPropertyName("skins")]
    public List<PlayerSkinEntry> Skins { get; set; } = new();

    [JsonPropertyName("selections")]
    public Dictionary<string, string> Selections { get; set; } = new();
}
