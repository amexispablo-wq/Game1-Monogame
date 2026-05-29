using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ColorBlocks;

public sealed class LevelData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("platforms")]
    public List<PlatformData> Platforms { get; set; } = new();

    [JsonPropertyName("goals")]
    public List<GoalData> Goals { get; set; } = new();

    [JsonPropertyName("playerSpawn")]
    public Vector2Data PlayerSpawn { get; set; } = new() { X = 100f, Y = 300f };
}

public sealed class PlatformData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("color")]
    public GameColor Color { get; set; } = GameColor.Red;
}

public sealed class GoalData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public sealed class Vector2Data
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}
