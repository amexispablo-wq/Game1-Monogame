#nullable enable
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// MonoGame DesktopGL only registers pads on SDL JoyDeviceAdded. If that event
/// is missed (Steam overlay, late USB, focus race), <see cref="GamePad.GetState"/>
/// stays disconnected forever. Periodically re-run Joystick.AddDevices so newly
/// present SDL joysticks get opened and promoted to GamePad slots.
/// Also logs SDL / MonoGame / XInput counts so Steam-hidden pads are visible in diag.
/// </summary>
public sealed class GamepadHotPlugRescan
{
    private const float PollIntervalSeconds = 0.5f;

    private static readonly MethodInfo? AddDevicesMethod = typeof(Joystick).GetMethod(
        "AddDevices",
        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

    private float _pollTimer;
    private int _lastMonoGameCount = -1;
    private int _lastXInputCount = -1;
    private int _lastSdlCount = -1;
    private bool _loggedMissingHook;
    private bool _sdlResolved;
    private bool _sdlAvailable;
    private SdlNumJoysticksDelegate? _sdlNumJoysticks;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SdlNumJoysticksDelegate();

    public void Update(float dt)
    {
        _pollTimer -= dt;
        if (_pollTimer > 0f)
        {
            return;
        }

        _pollTimer = PollIntervalSeconds;

        if (AddDevicesMethod is not null)
        {
            try
            {
                AddDevicesMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Warn("Input", $"Gamepad hot-plug rescan failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else if (!_loggedMissingHook)
        {
            _loggedMissingHook = true;
            DiagnosticsLog.Warn("Input", "Joystick.AddDevices not found — SDL rescan disabled (XInput fallback still active)");
        }

        int monoGame = CountMonoGamePads();
        int xinput = XInputGamepadPoller.CountConnected();
        int sdl = TryCountSdlJoysticks();

        if (_lastMonoGameCount < 0)
        {
            _lastMonoGameCount = monoGame;
            _lastXInputCount = xinput;
            _lastSdlCount = sdl;
            DiagnosticsLog.Info(
                "Input",
                $"Gamepad probe startup: SDL={sdl} MonoGame={monoGame} XInput={xinput}");
            return;
        }

        if (monoGame == _lastMonoGameCount && xinput == _lastXInputCount && sdl == _lastSdlCount)
        {
            return;
        }

        DiagnosticsLog.Info(
            "Input",
            $"Gamepad probe: SDL {_lastSdlCount}->{sdl} MonoGame {_lastMonoGameCount}->{monoGame} XInput {_lastXInputCount}->{xinput}");
        _lastMonoGameCount = monoGame;
        _lastXInputCount = xinput;
        _lastSdlCount = sdl;
    }

    private static int CountMonoGamePads()
    {
        int count = 0;
        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (GamePad.GetState((PlayerIndex)i).IsConnected)
            {
                count++;
            }
        }

        return count;
    }

    private int TryCountSdlJoysticks()
    {
        EnsureSdl();
        if (!_sdlAvailable || _sdlNumJoysticks is null)
        {
            return -1;
        }

        try
        {
            return _sdlNumJoysticks();
        }
        catch
        {
            return -1;
        }
    }

    private void EnsureSdl()
    {
        if (_sdlResolved)
        {
            return;
        }

        _sdlResolved = true;
        if (!NativeLibrary.TryLoad("SDL2", out IntPtr handle)
            && !NativeLibrary.TryLoad("SDL2.dll", out handle))
        {
            return;
        }

        if (NativeLibrary.TryGetExport(handle, "SDL_NumJoysticks", out IntPtr export))
        {
            _sdlNumJoysticks = Marshal.GetDelegateForFunctionPointer<SdlNumJoysticksDelegate>(export);
            _sdlAvailable = true;
        }
    }
}
