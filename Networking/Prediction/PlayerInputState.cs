using System;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public readonly record struct PlayerInputState(
    float HorizontalMovement,
    bool JumpPressed,
    bool RespawnPressed,
    bool FastFallHeld,
    bool PullRopeHeld,
    GameColor? RequestedColor,
    Vector2 Move = default,
    Vector2 MenuNavigate = default)
{
    public static PlayerInputState Empty { get; } = new(0f, false, false, false, false, null);

    public PlayerInputState Sanitized()
    {
        return this with
        {
            HorizontalMovement = Math.Clamp(HorizontalMovement, -1f, 1f),
            Move = new Vector2(
                Math.Clamp(Move.X, -1f, 1f),
                Math.Clamp(Move.Y, -1f, 1f)),
            MenuNavigate = new Vector2(
                Math.Clamp(MenuNavigate.X, -1f, 1f),
                Math.Clamp(MenuNavigate.Y, -1f, 1f))
        };
    }

    /// <summary>
    /// Same-tick merge: freshest axes/holds, OR edge presses, keep first non-null color.
    /// Prevents burst packets from wiping Jump/Color after a true edge.
    /// </summary>
    public PlayerInputState MergePreferringEdges(PlayerInputState incoming)
    {
        PlayerInputState a = Sanitized();
        PlayerInputState b = incoming.Sanitized();
        return new PlayerInputState(
            b.HorizontalMovement,
            a.JumpPressed || b.JumpPressed,
            a.RespawnPressed || b.RespawnPressed,
            b.FastFallHeld,
            b.PullRopeHeld,
            b.RequestedColor ?? a.RequestedColor,
            b.Move,
            b.MenuNavigate);
    }
}
