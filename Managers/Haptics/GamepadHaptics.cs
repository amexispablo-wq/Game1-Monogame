#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// MonoGame GamePad rumble fallback when Steam Input haptics unavailable.
/// </summary>
public sealed class GamepadHaptics : IHaptics
{
    public void Play(HapticEvent hapticEvent, int localPlayerSlot = 0)
    {
        if (localPlayerSlot < 0 || localPlayerSlot >= InputManager.MaxLocalPlayers)
        {
            return;
        }

        (float left, float right, float seconds) = Resolve(hapticEvent);
        try
        {
            GamePad.SetVibration((PlayerIndex)localPlayerSlot, left, right);
            // Timed stop is owned by caller/update; one-shot pulse via short duration request.
            _ = seconds;
        }
        catch (Exception)
        {
            // Ignore platforms without rumble.
        }
    }

    public void Stop(int localPlayerSlot = 0)
    {
        if (localPlayerSlot < 0 || localPlayerSlot >= InputManager.MaxLocalPlayers)
        {
            return;
        }

        try
        {
            GamePad.SetVibration((PlayerIndex)localPlayerSlot, 0f, 0f);
        }
        catch (Exception)
        {
        }
    }

    private static (float Left, float Right, float Seconds) Resolve(HapticEvent hapticEvent) => hapticEvent switch
    {
        HapticEvent.SmallImpact => (0.15f, 0.15f, 0.05f),
        HapticEvent.MediumImpact => (0.35f, 0.35f, 0.08f),
        HapticEvent.LargeImpact => (0.65f, 0.65f, 0.12f),
        HapticEvent.Checkpoint => (0.25f, 0.10f, 0.10f),
        HapticEvent.Goal => (0.55f, 0.55f, 0.20f),
        HapticEvent.PullRope => (0.20f, 0.45f, 0.06f),
        _ => (0.2f, 0.2f, 0.05f)
    };
}
