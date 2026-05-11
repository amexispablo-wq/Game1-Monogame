namespace Game1_Monogame;

public readonly record struct InputActionState(
    float HorizontalMovement,
    bool JumpPressed,
    bool FastFallHeld,
    GameColor? RequestedColor)
{
    public static InputActionState Empty { get; } = new(0f, false, false, null);
}
