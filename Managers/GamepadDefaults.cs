using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Default gamepad bindings using MonoGame's Xbox-style button layout.
/// PlayStation controllers map through Steam Input / OS drivers to these buttons.
/// Display names show both Xbox and PlayStation labels for the options screen.
/// </summary>
public static class GamepadDefaults
{
    public const float MoveDeadZone = 0.28f;
    public const float FastFallStickThreshold = 0.45f;
    public const float PullRopeTriggerThreshold = 0.35f;

    public static Buttons JumpButton => Buttons.A;
    public static Buttons RedButton => Buttons.X;
    public static Buttons GreenButton => Buttons.Y;
    public static Buttons BlueButton => Buttons.B;
    public static Buttons RespawnButton => Buttons.Back;
    public static Buttons PauseButton => Buttons.Start;
    public static Buttons MenuConfirmButton => Buttons.A;
    public static Buttons MenuCancelButton => Buttons.B;

    // Button-style actions that support gamepad rebinding (axis/trigger actions excluded).
    public static readonly GameplayInputAction[] RebindableButtonActions =
    {
        GameplayInputAction.Jump,
        GameplayInputAction.Respawn,
        GameplayInputAction.Red,
        GameplayInputAction.Blue,
        GameplayInputAction.Green
    };

    public static bool IsButtonRebindable(GameplayInputAction action)
    {
        foreach (GameplayInputAction candidate in RebindableButtonActions)
        {
            if (candidate == action)
            {
                return true;
            }
        }

        return false;
    }

    public static Buttons GetDefaultButton(GameplayInputAction action) => action switch
    {
        GameplayInputAction.Jump => JumpButton,
        GameplayInputAction.Respawn => RespawnButton,
        GameplayInputAction.Red => RedButton,
        GameplayInputAction.Blue => BlueButton,
        GameplayInputAction.Green => GreenButton,
        _ => Buttons.A
    };

    // Buttons offered when capturing a new gamepad binding.
    public static readonly Buttons[] CaptureButtons =
    {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.Back, Buttons.Start,
        Buttons.LeftStick, Buttons.RightStick
    };

    public static string FormatButton(Buttons button) => button switch
    {
        Buttons.A => "A / Cross",
        Buttons.B => "B / Circle",
        Buttons.X => "X / Square",
        Buttons.Y => "Y / Triangle",
        Buttons.LeftShoulder => "LB / L1",
        Buttons.RightShoulder => "RB / R1",
        Buttons.Back => "Back / Select",
        Buttons.Start => "Start / Options",
        Buttons.LeftStick => "L Stick Press",
        Buttons.RightStick => "R Stick Press",
        _ => button.ToString()
    };

    public static string GetGamepadDisplayName(GameplayInputAction action, IReadOnlyDictionary<string, string>? overrides)
    {
        if (IsButtonRebindable(action)
            && overrides != null
            && overrides.TryGetValue(action.ToString(), out string? stored)
            && System.Enum.TryParse(stored, out Buttons parsed))
        {
            return FormatButton(parsed);
        }

        return GetDisplayName(action);
    }

    public static string GetDisplayName(GameplayInputAction action)
    {
        return action switch
        {
            GameplayInputAction.MoveLeft => "Left Stick / D-Pad",
            GameplayInputAction.MoveRight => "Left Stick / D-Pad",
            GameplayInputAction.Jump => "A / Cross",
            GameplayInputAction.Respawn => "Back / Select",
            GameplayInputAction.FastFall => "Stick Down / D-Pad Down",
            GameplayInputAction.PullRope => "RT / R2",
            GameplayInputAction.Red => "X / Square",
            GameplayInputAction.Blue => "B / Circle",
            GameplayInputAction.Green => "Y / Triangle",
            _ => "—"
        };
    }
}
