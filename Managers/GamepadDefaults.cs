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
