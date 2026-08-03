#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// DesktopGL uses SDL GameController — Steam Input often hides the physical pad from SDL
/// and only exposes a virtual Xbox 360 pad via XInput. Poll XInput directly so late plug
/// still works when <see cref="GamePad.GetState"/> stays disconnected.
/// </summary>
internal static class XInputGamepadPoller
{
    private const uint ErrorDeviceNotConnected = 1167;
    private const short StickMax = short.MaxValue;
    private const byte TriggerMax = byte.MaxValue;

    private const ushort DpadUp = 0x0001;
    private const ushort DpadDown = 0x0002;
    private const ushort DpadLeft = 0x0004;
    private const ushort DpadRight = 0x0008;
    private const ushort Start = 0x0010;
    private const ushort Back = 0x0020;
    private const ushort LeftThumb = 0x0040;
    private const ushort RightThumb = 0x0080;
    private const ushort LeftShoulder = 0x0100;
    private const ushort RightShoulder = 0x0200;
    private const ushort A = 0x1000;
    private const ushort B = 0x2000;
    private const ushort X = 0x4000;
    private const ushort Y = 0x8000;

    private static readonly bool[] Connected = new bool[InputManager.MaxLocalPlayers];
    private static bool _resolved;
    private static bool _available;
    private static XInputGetStateDelegate? _getState;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint XInputGetStateDelegate(uint dwUserIndex, out XInputState pState);

    public static bool IsAvailable
    {
        get
        {
            EnsureResolved();
            return _available;
        }
    }

    public static int CountConnected()
    {
        if (!IsAvailable || _getState is null)
        {
            return 0;
        }

        int count = 0;
        for (uint i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (_getState(i, out _) == 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Prefer MonoGame/SDL state when connected; otherwise fill from XInput.
    /// Returns true when this slot is driven by XInput fallback.
    /// </summary>
    public static bool ResolveSlot(int index, GamePadState monoGameState, out GamePadState state)
    {
        if (monoGameState.IsConnected)
        {
            state = monoGameState;
            Connected[index] = false;
            return false;
        }

        if (!TryRead(index, out state))
        {
            state = monoGameState;
            if (Connected[index])
            {
                Connected[index] = false;
                DiagnosticsLog.Info("Input", $"XInput[{index}] connected true -> false");
            }

            return false;
        }

        if (!Connected[index])
        {
            Connected[index] = true;
            DiagnosticsLog.Info("Input", $"XInput[{index}] connected false -> true (SDL miss — using XInput fallback)");
        }

        return true;
    }

    public static bool TryRead(int index, out GamePadState state)
    {
        state = default;
        if (index < 0 || index >= InputManager.MaxLocalPlayers)
        {
            return false;
        }

        EnsureResolved();
        if (!_available || _getState is null)
        {
            return false;
        }

        uint result = _getState((uint)index, out XInputState xstate);
        if (result == ErrorDeviceNotConnected)
        {
            return false;
        }

        if (result != 0)
        {
            return false;
        }

        state = ToGamePadState(xstate);
        return true;
    }

    private static GamePadState ToGamePadState(XInputState xstate)
    {
        XInputGamepad pad = xstate.Gamepad;

        var thumbSticks = new GamePadThumbSticks(
            new Vector2(pad.ThumbLX / (float)StickMax, pad.ThumbLY / (float)StickMax),
            new Vector2(pad.ThumbRX / (float)StickMax, pad.ThumbRY / (float)StickMax));

        var triggers = new GamePadTriggers(
            pad.LeftTrigger / (float)TriggerMax,
            pad.RightTrigger / (float)TriggerMax);

        Buttons buttons = 0;
        ushort b = pad.Buttons;
        if ((b & A) != 0) buttons |= Buttons.A;
        if ((b & B) != 0) buttons |= Buttons.B;
        if ((b & X) != 0) buttons |= Buttons.X;
        if ((b & Y) != 0) buttons |= Buttons.Y;
        if ((b & Start) != 0) buttons |= Buttons.Start;
        if ((b & Back) != 0) buttons |= Buttons.Back;
        if ((b & LeftShoulder) != 0) buttons |= Buttons.LeftShoulder;
        if ((b & RightShoulder) != 0) buttons |= Buttons.RightShoulder;
        if ((b & LeftThumb) != 0) buttons |= Buttons.LeftStick;
        if ((b & RightThumb) != 0) buttons |= Buttons.RightStick;
        if ((b & DpadUp) != 0) buttons |= Buttons.DPadUp;
        if ((b & DpadDown) != 0) buttons |= Buttons.DPadDown;
        if ((b & DpadLeft) != 0) buttons |= Buttons.DPadLeft;
        if ((b & DpadRight) != 0) buttons |= Buttons.DPadRight;
        if (pad.LeftTrigger > 0) buttons |= Buttons.LeftTrigger;
        if (pad.RightTrigger > 0) buttons |= Buttons.RightTrigger;

        var dpad = new GamePadDPad(
            (b & DpadUp) != 0 ? ButtonState.Pressed : ButtonState.Released,
            (b & DpadDown) != 0 ? ButtonState.Pressed : ButtonState.Released,
            (b & DpadLeft) != 0 ? ButtonState.Pressed : ButtonState.Released,
            (b & DpadRight) != 0 ? ButtonState.Pressed : ButtonState.Released);

        return new GamePadState(thumbSticks, triggers, new GamePadButtons(buttons), dpad);
    }

    private static void EnsureResolved()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string[] dlls = ["xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll"];
        foreach (string dll in dlls)
        {
            if (!NativeLibrary.TryLoad(dll, out IntPtr handle))
            {
                continue;
            }

            if (NativeLibrary.TryGetExport(handle, "XInputGetState", out IntPtr export))
            {
                _getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateDelegate>(export);
                _available = true;
                DiagnosticsLog.Info("Input", $"XInput fallback ready ({dll})");
                return;
            }
        }

        DiagnosticsLog.Warn("Input", "XInput DLL not found — Steam/XInput hot-plug fallback unavailable");
    }
}
