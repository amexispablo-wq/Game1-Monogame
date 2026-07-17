#nullable enable
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Keyboard gameplay reader. Binding resolution stays in InputManager settings snapshot.
/// </summary>
public sealed class KeyboardBackend
{
    public PlayerInputState Read(
        KeyboardState current,
        KeyboardState previous,
        Keys moveLeft,
        Keys moveRight,
        Keys jump,
        Keys respawn,
        Keys fastFall,
        Keys pullRope,
        Keys red,
        Keys blue,
        Keys green)
    {
        float horizontalMovement = 0f;
        if (current.IsKeyDown(moveLeft))
        {
            horizontalMovement -= 1f;
        }

        if (current.IsKeyDown(moveRight))
        {
            horizontalMovement += 1f;
        }

        GameColor? requestedColor = null;
        if (IsNewKeyPress(current, previous, red))
        {
            requestedColor = GameColor.Red;
        }
        else if (IsNewKeyPress(current, previous, blue))
        {
            requestedColor = GameColor.Blue;
        }
        else if (IsNewKeyPress(current, previous, green))
        {
            requestedColor = GameColor.Green;
        }

        return new PlayerInputState(
            horizontalMovement,
            IsNewKeyPress(current, previous, jump),
            IsNewKeyPress(current, previous, respawn),
            current.IsKeyDown(fastFall),
            current.IsKeyDown(pullRope),
            requestedColor);
    }

    private static bool IsNewKeyPress(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && !previous.IsKeyDown(key);
}
