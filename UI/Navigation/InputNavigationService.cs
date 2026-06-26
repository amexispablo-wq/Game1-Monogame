using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class InputNavigationService
{
    public InputNavigationDevice ActiveDevice { get; private set; } = InputNavigationDevice.Mouse;

    public bool IsMouseActive => ActiveDevice == InputNavigationDevice.Mouse;
    public bool IsKeyboardActive => ActiveDevice == InputNavigationDevice.Keyboard;
    public bool IsGamepadActive => ActiveDevice == InputNavigationDevice.Gamepad;

    public bool AllowPointerHoverFocus => IsMouseActive;
    public bool AllowPointerHoverVisual => IsMouseActive;

    public void Update(InputManager input)
    {
        if (input.MouseActivityThisFrame)
        {
            ActiveDevice = InputNavigationDevice.Mouse;
        }

        if (input.KeyboardMenuActivityThisFrame)
        {
            ActiveDevice = InputNavigationDevice.Keyboard;
        }

        if (input.GamepadActivityThisFrame)
        {
            ActiveDevice = InputNavigationDevice.Gamepad;
        }
    }

    public bool ShouldDrawFocusHighlight() => ActiveDevice != InputNavigationDevice.Mouse;
}
