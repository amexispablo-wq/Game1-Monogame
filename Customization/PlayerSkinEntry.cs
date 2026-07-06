#nullable enable
using System;
using System.Text.Json.Serialization;

namespace ColorBlocks;

public sealed class PlayerSkinEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Skin";

    [JsonPropertyName("pixels")]
    public bool[] Pixels { get; set; } = PlayerSkinData.CreateEmptyPixels();

    public PlayerSkinData ToSkinData()
    {
        var data = new PlayerSkinData();
        if (Pixels.Length == PlayerSkinData.GridSize * PlayerSkinData.GridSize)
        {
            Array.Copy(Pixels, data.Pixels, Pixels.Length);
        }

        return data;
    }

    public static PlayerSkinEntry FromSkinData(string name, PlayerSkinData data)
    {
        return new PlayerSkinEntry
        {
            Name = name,
            Pixels = (bool[])data.Pixels.Clone()
        };
    }
}
