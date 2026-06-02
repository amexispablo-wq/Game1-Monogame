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

    [JsonPropertyName("checkpointFlags")]
    public List<CheckpointFlagData> CheckpointFlags { get; set; } = new();

    [JsonPropertyName("launchPads")]
    public List<LaunchPadData> LaunchPads { get; set; } = new();

    [JsonPropertyName("playerSpawn")]
    public Vector2Data PlayerSpawn { get; set; } = new() { X = 100f, Y = 300f };

    [JsonPropertyName("musicId")]
    public string MusicId { get; set; } = LevelMusicLibrary.DefaultMusicId;

    [JsonPropertyName("allPlayers")]
    public bool AllPlayers { get; set; } = true;

    [JsonPropertyName("player1")]
    public bool Player1 { get; set; }

    [JsonPropertyName("player2")]
    public bool Player2 { get; set; }

    [JsonPropertyName("player3")]
    public bool Player3 { get; set; }

    [JsonPropertyName("player4")]
    public bool Player4 { get; set; }

    [JsonPropertyName("coloredRope")]
    public bool ColoredRope { get; set; }

    [JsonPropertyName("regularRope")]
    public bool RegularRope { get; set; }

    [JsonPropertyName("lavaRise")]
    public bool LavaRise { get; set; }
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

public sealed class CheckpointFlagData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public sealed class LaunchPadData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = LaunchPad.DefaultWidth;

    [JsonPropertyName("height")]
    public int Height { get; set; } = LaunchPad.DefaultHeight;

    [JsonPropertyName("rotation")]
    public float RotationDegrees { get; set; }
}

public sealed class Vector2Data
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}
