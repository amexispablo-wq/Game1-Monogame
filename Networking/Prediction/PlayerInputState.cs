using System;

namespace ColorBlocks;

public readonly record struct PlayerInputState(
    float HorizontalMovement,
    bool JumpPressed,
    bool RespawnPressed,
    bool FastFallHeld,
    bool PullRopeHeld,
    GameColor? RequestedColor)
{
    public static PlayerInputState Empty { get; } = new(0f, false, false, false, false, null);

    public PlayerInputState Sanitized()
    {
        return this with { HorizontalMovement = Math.Clamp(HorizontalMovement, -1f, 1f) };
    }
}
