#nullable enable
using System;
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

    [JsonPropertyName("powerUps")]
    public List<PowerUpData> PowerUps { get; set; } = new();

    [JsonPropertyName("signs")]
    public List<SignData> Signs { get; set; } = new();

    [JsonPropertyName("playerSpawn")]
    public Vector2Data PlayerSpawn { get; set; } = new() { X = 100f, Y = 300f };

    [JsonPropertyName("playerSpawnColor")]
    public GameColor PlayerSpawnColor { get; set; } = GameColor.Red;

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

    [JsonPropertyName("anyRope")]
    public bool AnyRope { get; set; }

    [JsonPropertyName("coloredRope")]
    public bool ColoredRope { get; set; }

    [JsonPropertyName("regularRope")]
    public bool RegularRope { get; set; }

    [JsonPropertyName("lavaRise")]
    public bool LavaRise { get; set; }

    [JsonPropertyName("playerCollision")]
    public bool PlayerCollision { get; set; }

    [JsonPropertyName("lavaLine")]
    public LavaLineData? LavaLine { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("workshopId")]
    public string WorkshopId { get; set; } = string.Empty;

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }

    [JsonPropertyName("modifiedDate")]
    public DateTime? ModifiedDate { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("ownerSteamId")]
    public string OwnerSteamId { get; set; } = string.Empty;

    [JsonPropertyName("downloadedVersion")]
    public string DownloadedVersion { get; set; } = string.Empty;

    [JsonPropertyName("lastSync")]
    public DateTime? LastSync { get; set; }
}

public sealed class LavaLineData
{
    [JsonPropertyName("surfaceY")]
    public int SurfaceY { get; set; }

    [JsonPropertyName("riseSpeed")]
    public float RiseSpeed { get; set; } = ColorBlocks.LavaLine.DefaultRiseSpeed;
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

    [JsonPropertyName("moveVertical")]
    public bool MoveVertical { get; set; }

    [JsonPropertyName("verticalSpeed")]
    public float VerticalSpeed { get; set; } = Platform.DefaultVerticalSpeed;

    [JsonPropertyName("verticalDistanceBlocks")]
    public int VerticalDistanceBlocks { get; set; } = Platform.DefaultVerticalDistanceBlocks;

    [JsonPropertyName("verticalDirection")]
    public PlatformVerticalDirection VerticalDirection { get; set; } = PlatformVerticalDirection.Up;

    [JsonPropertyName("moveHorizontal")]
    public bool MoveHorizontal { get; set; }

    [JsonPropertyName("horizontalSpeed")]
    public float HorizontalSpeed { get; set; } = Platform.DefaultHorizontalSpeed;

    [JsonPropertyName("horizontalDistanceBlocks")]
    public int HorizontalDistanceBlocks { get; set; } = Platform.DefaultHorizontalDistanceBlocks;

    [JsonPropertyName("horizontalDirection")]
    public PlatformHorizontalDirection HorizontalDirection { get; set; } = PlatformHorizontalDirection.Right;

    [JsonPropertyName("colorChangeEnabled")]
    public bool ColorChangeEnabled { get; set; }

    [JsonPropertyName("colorCycle")]
    public List<GameColor> ColorCycle { get; set; } = new();

    [JsonPropertyName("colorChangePeriodSeconds")]
    public float ColorChangePeriodSeconds { get; set; } = Platform.DefaultColorChangePeriodSeconds;

    [JsonPropertyName("colorChangePhaseSeconds")]
    public float ColorChangePhaseSeconds { get; set; }
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

    [JsonPropertyName("launchForce")]
    public float LaunchForce { get; set; } = LaunchPad.LaunchPadForce;
}

public sealed class PowerUpData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = PowerUp.DefaultWidth;

    [JsonPropertyName("height")]
    public int Height { get; set; } = PowerUp.DefaultHeight;

    [JsonPropertyName("type")]
    public PowerUpType Type { get; set; } = PowerUpType.Speed;

    [JsonPropertyName("durationSeconds")]
    public float DurationSeconds { get; set; } = PowerUp.DefaultDurationSeconds;

    [JsonPropertyName("multiplier")]
    public float Multiplier { get; set; } = PowerUp.DefaultMultiplier;

    [JsonPropertyName("respawnSeconds")]
    public float RespawnSeconds { get; set; } = PowerUp.DefaultRespawnSeconds;

    [JsonPropertyName("consumable")]
    public bool Consumable { get; set; } = PowerUp.DefaultConsumable;
}

public sealed class SignData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("scale")]
    public int Scale { get; set; } = 2;
}

public sealed class Vector2Data
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}
