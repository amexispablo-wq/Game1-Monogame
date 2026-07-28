using System.Collections.Generic;

namespace ColorBlocks;

public sealed class LevelSnapshot
{
    public string LevelId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public NetworkVector2 PlayerSpawn { get; init; }
    public int WorldWidth { get; init; }
    public int WorldHeight { get; init; }
    public List<PlatformSnapshot> Platforms { get; init; } = new();
    public List<GoalSnapshot> Goals { get; init; } = new();
    public List<CheckpointFlagSnapshot> CheckpointFlags { get; init; } = new();
    public List<LaunchPadSnapshot> LaunchPads { get; init; } = new();
    public List<PowerUpSnapshot> PowerUps { get; init; } = new();
    public int PlatformCount => Platforms.Count;
    public int GoalCount => Goals.Count;
    public int CheckpointFlagCount => CheckpointFlags.Count;
    public int LaunchPadCount => LaunchPads.Count;
    public int PowerUpCount => PowerUps.Count;
}

public readonly record struct PlatformSnapshot(
    int X,
    int Y,
    int Width,
    int Height,
    GameColor Color,
    bool MoveVertical = false,
    float VerticalSpeed = Platform.DefaultVerticalSpeed,
    int VerticalDistanceBlocks = Platform.DefaultVerticalDistanceBlocks,
    PlatformVerticalDirection VerticalDirection = PlatformVerticalDirection.Up,
    bool MoveHorizontal = false,
    float HorizontalSpeed = Platform.DefaultHorizontalSpeed,
    int HorizontalDistanceBlocks = Platform.DefaultHorizontalDistanceBlocks,
    PlatformHorizontalDirection HorizontalDirection = PlatformHorizontalDirection.Right,
    bool ColorChangeEnabled = false,
    float ColorChangePeriodSeconds = Platform.DefaultColorChangePeriodSeconds,
    float ColorChangePhaseSeconds = 0f,
    IReadOnlyList<GameColor>? ColorCycle = null);

public readonly record struct GoalSnapshot(int X, int Y);

public readonly record struct CheckpointFlagSnapshot(int Id, int X, int Y, bool IsActive);

public readonly record struct LaunchPadSnapshot(
    int X,
    int Y,
    int Width,
    int Height,
    float RotationDegrees,
    float LaunchForce = 980f);

public readonly record struct PowerUpSnapshot(
    int X,
    int Y,
    int Width,
    int Height,
    PowerUpType Type,
    float DurationSeconds,
    float Multiplier,
    float RespawnSeconds,
    bool IsAvailable,
    float RespawnRemaining,
    bool Consumable = true);
