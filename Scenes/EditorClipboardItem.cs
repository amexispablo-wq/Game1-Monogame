using System.Collections.Generic;

namespace ColorBlocks;

public sealed class EditorClipboardItem
{
    public EditorObjectKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public GameColor Color { get; set; }
    public float RotationDegrees { get; set; }
    public float LaunchForce { get; set; } = LaunchPad.LaunchPadForce;
    public PowerUpType PowerUpType { get; set; } = PowerUpType.Speed;
    public float PowerUpDurationSeconds { get; set; } = PowerUp.DefaultDurationSeconds;
    public float PowerUpMultiplier { get; set; } = PowerUp.DefaultMultiplier;
    public float PowerUpRespawnSeconds { get; set; } = PowerUp.DefaultRespawnSeconds;
    public bool PowerUpConsumable { get; set; } = PowerUp.DefaultConsumable;

    public bool MoveVertical { get; set; }
    public float VerticalSpeed { get; set; } = Platform.DefaultVerticalSpeed;
    public int VerticalDistanceBlocks { get; set; } = Platform.DefaultVerticalDistanceBlocks;
    public PlatformVerticalDirection VerticalDirection { get; set; } = PlatformVerticalDirection.Up;
    public bool MoveHorizontal { get; set; }
    public float HorizontalSpeed { get; set; } = Platform.DefaultHorizontalSpeed;
    public int HorizontalDistanceBlocks { get; set; } = Platform.DefaultHorizontalDistanceBlocks;
    public PlatformHorizontalDirection HorizontalDirection { get; set; } = PlatformHorizontalDirection.Right;

    public bool ColorChangeEnabled { get; set; }
    public List<GameColor> ColorCycle { get; set; } = new();
    public float ColorChangePeriodSeconds { get; set; } = Platform.DefaultColorChangePeriodSeconds;
    public float ColorChangePhaseSeconds { get; set; }

    public EditorClipboardItem(EditorObjectKind kind, int x, int y, int width, int height, GameColor color, float rotationDegrees)
    {
        Kind = kind;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Color = color;
        RotationDegrees = rotationDegrees;
    }
}
