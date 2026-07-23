using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Default gamepad bindings using MonoGame's Xbox-style button layout.
/// PlayStation pads (DualShock 4 / DualSense, USB or Bluetooth) are supported via
/// Steam Input when launched through Steam, or via SDL2 mappings when running standalone.
/// Display names show both Xbox and PlayStation labels for the options screen.
/// </summary>
public static class GamepadDefaults
{
    // Per-axis deadzone + rescale. Tuned for responsive sticks; deadzone filters idle noise.
    public const float MoveDeadZone = 0.28f;
    public const float FastFallProcessedThreshold = 0.45f;
    public const float MenuStickDirectionThreshold = 0.30f;
    public const float EditorPanStickThreshold = 0.08f;
    public const float EditorStickDeadZone = 0.12f;
    public const float EditorPanSpeedPixelsPerSecond = 1100f;
    public const float EditorCursorSpeedPixelsPerSecond = 780f;
    public const float EditorZoomRatePerSecond = 0.65f;
    public const float PullRopeTriggerThreshold = 0.35f;

    // Raw-axis edge threshold for binding capture prompts.
    public const float FastFallStickThreshold = 0.45f;

    /// <summary>
    /// Soft-claim XInput fallback often reports stuck diagonal corners (e.g. -1,1)
    /// while Steam is managing the pad. Both axes near full deflection = untrusted.
    /// </summary>
    public const float HollowCornerStickThreshold = 0.9f;

    public static bool IsHollowCornerStick(Vector2 raw) =>
        MathF.Abs(raw.X) >= HollowCornerStickThreshold
        && MathF.Abs(raw.Y) >= HollowCornerStickThreshold;

    public static float ProcessAxis(float raw, float deadzone = MoveDeadZone)
    {
        float abs = MathF.Abs(raw);
        if (abs <= deadzone)
        {
            return 0f;
        }

        float scaled = (abs - deadzone) / (1f - deadzone);
        return MathF.Sign(raw) * Math.Clamp(scaled, 0f, 1f);
    }

    public static Vector2 ProcessLeftStick(Vector2 raw) =>
        new(ProcessAxis(raw.X), ProcessAxis(raw.Y));

    public static Vector2 ProcessRightStick(Vector2 raw) =>
        new(ProcessAxis(raw.X), ProcessAxis(raw.Y));

    public static Vector2 ProcessEditorStick(Vector2 raw) =>
        new(ProcessAxis(raw.X, EditorStickDeadZone), ProcessAxis(raw.Y, EditorStickDeadZone));

    public static float ProcessHorizontalAxis(float rawX) => ProcessAxis(rawX);

    public static bool UsesAnalogStick(GamepadBindingKind kind) =>
        kind is GamepadBindingKind.DefaultAxis
            or GamepadBindingKind.StickLeft
            or GamepadBindingKind.StickRight
            or GamepadBindingKind.StickUp
            or GamepadBindingKind.StickDown;

    public static float ReadHorizontalMovement(
        Vector2 processedStick,
        GamepadActionBinding moveLeft,
        GamepadActionBinding moveRight,
        GamePadState current)
    {
        bool leftAnalog = UsesAnalogStick(moveLeft.Kind);
        bool rightAnalog = UsesAnalogStick(moveRight.Kind);

        if (leftAnalog && rightAnalog)
        {
            return Math.Clamp(processedStick.X, -1f, 1f);
        }

        float horizontal = 0f;
        if (leftAnalog)
        {
            horizontal -= Math.Max(0f, -processedStick.X);
        }
        else if (moveLeft.IsActive(current, GameplayInputAction.MoveLeft, processedStick))
        {
            horizontal -= 1f;
        }

        if (rightAnalog)
        {
            horizontal += Math.Max(0f, processedStick.X);
        }
        else if (moveRight.IsActive(current, GameplayInputAction.MoveRight, processedStick))
        {
            horizontal += 1f;
        }

        return Math.Clamp(horizontal, -1f, 1f);
    }

    public static bool ReadFastFallHeld(
        Vector2 processedStick,
        GamepadActionBinding fastFall,
        GamePadState current)
    {
        if (UsesAnalogStick(fastFall.Kind))
        {
            return processedStick.Y < -FastFallProcessedThreshold;
        }

        return fastFall.IsActive(current, GameplayInputAction.FastFall);
    }

    public static Buttons JumpButton => Buttons.A;
    public static Buttons RedButton => Buttons.X;
    public static Buttons GreenButton => Buttons.Y;
    public static Buttons BlueButton => Buttons.B;
    public static Buttons RespawnButton => Buttons.Back;
    public static Buttons PauseButton => Buttons.Start;
    public static Buttons MenuConfirmButton => Buttons.A;
    public static Buttons MenuCancelButton => Buttons.B;

    // Button-style actions that support gamepad rebinding.
    public static readonly GameplayInputAction[] RebindableButtonActions =
    {
        GameplayInputAction.MoveLeft,
        GameplayInputAction.MoveRight,
        GameplayInputAction.Jump,
        GameplayInputAction.Respawn,
        GameplayInputAction.FastFall,
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
        _ => Buttons.None
    };

    public static bool UsesDefaultAxisBinding(GameplayInputAction action) =>
        action is GameplayInputAction.MoveLeft or GameplayInputAction.MoveRight or GameplayInputAction.FastFall;

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
        if (overrides != null && overrides.TryGetValue(action.ToString(), out string? stored))
        {
            return GamepadActionBinding.FormatToken(stored, action);
        }

        return GetDisplayName(action);
    }

    public static string GetDisplayName(GameplayInputAction action)
    {
        return action switch
        {
            GameplayInputAction.MoveLeft => "Left Stick ←",
            GameplayInputAction.MoveRight => "Left Stick →",
            GameplayInputAction.Jump => "A / Cross",
            GameplayInputAction.Respawn => "Back / Select",
            GameplayInputAction.FastFall => "Left Stick ↓",
            GameplayInputAction.PullRope => "RT / R2",
            GameplayInputAction.Red => "X / Square",
            GameplayInputAction.Blue => "B / Circle",
            GameplayInputAction.Green => "Y / Triangle",
            _ => "—"
        };
    }
}
