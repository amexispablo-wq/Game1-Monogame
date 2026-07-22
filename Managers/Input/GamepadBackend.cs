#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// MonoGame GamePad gameplay reader (Xbox-layout). Used when Steam Input inactive for a slot.
/// </summary>
public sealed class GamepadBackend
{
    public PlayerInputState Read(
        GamePadState current,
        GamePadState previous,
        GamepadActionBinding moveLeft,
        GamepadActionBinding moveRight,
        GamepadActionBinding jump,
        GamepadActionBinding respawn,
        GamepadActionBinding fastFall,
        GamepadActionBinding red,
        GamepadActionBinding blue,
        GamepadActionBinding green)
    {
        if (!current.IsConnected)
        {
            return PlayerInputState.Empty;
        }

        Vector2 processedStick = GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left);
        float horizontal = GamepadDefaults.ReadHorizontalMovement(
            processedStick,
            moveLeft,
            moveRight,
            current);
        bool fastFallHeld = GamepadDefaults.ReadFastFallHeld(
            processedStick,
            fastFall,
            current);
        bool pullRope = current.Triggers.Right > GamepadDefaults.PullRopeTriggerThreshold;

        GameColor? requestedColor = null;
        if (WasBindingPressed(current, previous, red, GameplayInputAction.Red))
        {
            requestedColor = GameColor.Red;
        }
        else if (WasBindingPressed(current, previous, green, GameplayInputAction.Green))
        {
            requestedColor = GameColor.Green;
        }
        else if (WasBindingPressed(current, previous, blue, GameplayInputAction.Blue))
        {
            requestedColor = GameColor.Blue;
        }

        return new PlayerInputState(
            horizontal,
            WasBindingPressed(current, previous, jump, GameplayInputAction.Jump),
            WasBindingPressed(current, previous, respawn, GameplayInputAction.Respawn),
            fastFallHeld,
            pullRope,
            requestedColor,
            processedStick,
            MenuNavigate: default);
    }

    private static bool WasBindingPressed(
        GamePadState current,
        GamePadState previous,
        GamepadActionBinding binding,
        GameplayInputAction action)
    {
        Vector2 processedCurrent = GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left);
        Vector2 processedPrevious = GamepadDefaults.ProcessLeftStick(previous.ThumbSticks.Left);
        return binding.IsActive(current, action, processedCurrent)
            && !binding.IsActive(previous, action, processedPrevious);
    }
}
