namespace ColorBlocks;

public enum InputDeviceType
{
    None,
    Keyboard,
    Gamepad
}

public readonly record struct InputDevice(InputDeviceType DeviceType, int DeviceIndex = 0)
{
    public static InputDevice None { get; } = new(InputDeviceType.None);
    public static InputDevice Keyboard { get; } = new(InputDeviceType.Keyboard);

    public static InputDevice Gamepad(int deviceIndex)
    {
        return new InputDevice(InputDeviceType.Gamepad, deviceIndex);
    }
}
