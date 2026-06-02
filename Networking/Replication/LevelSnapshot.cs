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
    public int PlatformCount => Platforms.Count;
    public int GoalCount => Goals.Count;
    public int CheckpointFlagCount => CheckpointFlags.Count;
    public int LaunchPadCount => LaunchPads.Count;
}

public readonly record struct PlatformSnapshot(
    int X,
    int Y,
    int Width,
    int Height,
    GameColor Color);

public readonly record struct GoalSnapshot(int X, int Y);

public readonly record struct CheckpointFlagSnapshot(int Id, int X, int Y, bool IsActive);

public readonly record struct LaunchPadSnapshot(int X, int Y, int Width, int Height, float RotationDegrees);
