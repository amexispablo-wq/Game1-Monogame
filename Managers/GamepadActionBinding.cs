using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public enum GamepadBindingKind
{
    DefaultAxis,
    Button,
    StickLeft,
    StickRight,
    StickUp,
    StickDown,
    DPadLeft,
    DPadRight,
    DPadUp,
    DPadDown
}

public readonly struct GamepadActionBinding
{
    public GamepadBindingKind Kind { get; }
    public Buttons Button { get; }

    private GamepadActionBinding(GamepadBindingKind kind, Buttons button = Buttons.None)
    {
        Kind = kind;
        Button = button;
    }

    public static GamepadActionBinding DefaultFor(GameplayInputAction action) =>
        GamepadDefaults.UsesDefaultAxisBinding(action)
            ? new GamepadActionBinding(GamepadBindingKind.DefaultAxis)
            : new GamepadActionBinding(GamepadBindingKind.Button, GamepadDefaults.GetDefaultButton(action));

    public static GamepadActionBinding Parse(string? stored, GameplayInputAction action)
    {
        if (string.IsNullOrWhiteSpace(stored) || stored == GamepadBindingTokens.Default)
        {
            return DefaultFor(action);
        }

        if (Enum.TryParse(stored, out Buttons button))
        {
            return new GamepadActionBinding(GamepadBindingKind.Button, button);
        }

        return stored switch
        {
            GamepadBindingTokens.StickLeft => new GamepadActionBinding(GamepadBindingKind.StickLeft),
            GamepadBindingTokens.StickRight => new GamepadActionBinding(GamepadBindingKind.StickRight),
            GamepadBindingTokens.StickUp => new GamepadActionBinding(GamepadBindingKind.StickUp),
            GamepadBindingTokens.StickDown => new GamepadActionBinding(GamepadBindingKind.StickDown),
            GamepadBindingTokens.DPadLeft => new GamepadActionBinding(GamepadBindingKind.DPadLeft),
            GamepadBindingTokens.DPadRight => new GamepadActionBinding(GamepadBindingKind.DPadRight),
            GamepadBindingTokens.DPadUp => new GamepadActionBinding(GamepadBindingKind.DPadUp),
            GamepadBindingTokens.DPadDown => new GamepadActionBinding(GamepadBindingKind.DPadDown),
            _ => DefaultFor(action)
        };
    }

    public static string FormatToken(string? stored, GameplayInputAction action)
    {
        if (string.IsNullOrWhiteSpace(stored) || stored == GamepadBindingTokens.Default)
        {
            return GamepadDefaults.GetDisplayName(action);
        }

        if (Enum.TryParse(stored, out Buttons button))
        {
            return GamepadDefaults.FormatButton(button);
        }

        return stored switch
        {
            GamepadBindingTokens.StickLeft => "Left Stick ←",
            GamepadBindingTokens.StickRight => "Left Stick →",
            GamepadBindingTokens.StickUp => "Left Stick ↑",
            GamepadBindingTokens.StickDown => "Left Stick ↓",
            GamepadBindingTokens.DPadLeft => "D-Pad ←",
            GamepadBindingTokens.DPadRight => "D-Pad →",
            GamepadBindingTokens.DPadUp => "D-Pad ↑",
            GamepadBindingTokens.DPadDown => "D-Pad ↓",
            _ => GamepadDefaults.GetDisplayName(action)
        };
    }

    public bool IsActive(GamePadState current, GameplayInputAction action, Vector2 processedStick)
    {
        _ = action;
        return Kind switch
        {
            GamepadBindingKind.Button => current.IsButtonDown(Button),
            GamepadBindingKind.DefaultAxis => IsDefaultAxisActive(processedStick, action),
            GamepadBindingKind.StickLeft => processedStick.X < -GamepadDefaults.MenuStickDirectionThreshold,
            GamepadBindingKind.StickRight => processedStick.X > GamepadDefaults.MenuStickDirectionThreshold,
            GamepadBindingKind.StickUp => processedStick.Y > GamepadDefaults.MenuStickDirectionThreshold,
            GamepadBindingKind.StickDown => processedStick.Y < -GamepadDefaults.MenuStickDirectionThreshold,
            GamepadBindingKind.DPadLeft => current.DPad.Left == ButtonState.Pressed,
            GamepadBindingKind.DPadRight => current.DPad.Right == ButtonState.Pressed,
            GamepadBindingKind.DPadUp => current.DPad.Up == ButtonState.Pressed,
            GamepadBindingKind.DPadDown => current.DPad.Down == ButtonState.Pressed,
            _ => false
        };
    }

    public bool IsActive(GamePadState current, GameplayInputAction action) =>
        IsActive(current, action, GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left));

    public static bool TryCaptureAnyEdge(
        GamePadState current,
        GamePadState previous,
        out string token)
    {
        token = string.Empty;

        if (WasStickLeftEdge(current, previous))
        {
            token = GamepadBindingTokens.StickLeft;
            return true;
        }

        if (WasStickRightEdge(current, previous))
        {
            token = GamepadBindingTokens.StickRight;
            return true;
        }

        if (WasStickUpEdge(current, previous))
        {
            token = GamepadBindingTokens.StickUp;
            return true;
        }

        if (WasStickDownEdge(current, previous))
        {
            token = GamepadBindingTokens.StickDown;
            return true;
        }

        if (WasDPadLeftEdge(current, previous))
        {
            token = GamepadBindingTokens.DPadLeft;
            return true;
        }

        if (WasDPadRightEdge(current, previous))
        {
            token = GamepadBindingTokens.DPadRight;
            return true;
        }

        if (WasDPadUpEdge(current, previous))
        {
            token = GamepadBindingTokens.DPadUp;
            return true;
        }

        if (WasDPadDownEdge(current, previous))
        {
            token = GamepadBindingTokens.DPadDown;
            return true;
        }

        return false;
    }

    private static bool IsDefaultAxisActive(Vector2 processedStick, GameplayInputAction action) =>
        action switch
        {
            GameplayInputAction.MoveLeft => processedStick.X < -GamepadDefaults.MenuStickDirectionThreshold,
            GameplayInputAction.MoveRight => processedStick.X > GamepadDefaults.MenuStickDirectionThreshold,
            GameplayInputAction.FastFall => processedStick.Y < -GamepadDefaults.FastFallProcessedThreshold,
            _ => false
        };

    private static bool WasStickLeftEdge(GamePadState current, GamePadState previous) =>
        GamepadDefaults.ProcessHorizontalAxis(current.ThumbSticks.Left.X) < -GamepadDefaults.MenuStickDirectionThreshold
        && GamepadDefaults.ProcessHorizontalAxis(previous.ThumbSticks.Left.X) >= -GamepadDefaults.MenuStickDirectionThreshold;

    private static bool WasStickRightEdge(GamePadState current, GamePadState previous) =>
        GamepadDefaults.ProcessHorizontalAxis(current.ThumbSticks.Left.X) > GamepadDefaults.MenuStickDirectionThreshold
        && GamepadDefaults.ProcessHorizontalAxis(previous.ThumbSticks.Left.X) <= GamepadDefaults.MenuStickDirectionThreshold;

    private static bool WasStickDownEdge(GamePadState current, GamePadState previous) =>
        GamepadDefaults.ProcessAxis(current.ThumbSticks.Left.Y) < -GamepadDefaults.MenuStickDirectionThreshold
        && GamepadDefaults.ProcessAxis(previous.ThumbSticks.Left.Y) >= -GamepadDefaults.MenuStickDirectionThreshold;

    private static bool WasStickUpEdge(GamePadState current, GamePadState previous) =>
        GamepadDefaults.ProcessAxis(current.ThumbSticks.Left.Y) > GamepadDefaults.MenuStickDirectionThreshold
        && GamepadDefaults.ProcessAxis(previous.ThumbSticks.Left.Y) <= GamepadDefaults.MenuStickDirectionThreshold;

    private static bool WasDPadLeftEdge(GamePadState current, GamePadState previous) =>
        current.DPad.Left == ButtonState.Pressed && previous.DPad.Left == ButtonState.Released;

    private static bool WasDPadRightEdge(GamePadState current, GamePadState previous) =>
        current.DPad.Right == ButtonState.Pressed && previous.DPad.Right == ButtonState.Released;

    private static bool WasDPadUpEdge(GamePadState current, GamePadState previous) =>
        current.DPad.Up == ButtonState.Pressed && previous.DPad.Up == ButtonState.Released;

    private static bool WasDPadDownEdge(GamePadState current, GamePadState previous) =>
        current.DPad.Down == ButtonState.Pressed && previous.DPad.Down == ButtonState.Released;
}

public static class GamepadBindingTokens
{
    public const string Default = "Default";
    public const string StickLeft = "StickLeft";
    public const string StickRight = "StickRight";
    public const string StickUp = "StickUp";
    public const string StickDown = "StickDown";
    public const string DPadLeft = "DPadLeft";
    public const string DPadRight = "DPadRight";
    public const string DPadUp = "DPadUp";
    public const string DPadDown = "DPadDown";
}
