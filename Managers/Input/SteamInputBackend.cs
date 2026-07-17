#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Reads gameplay + menu actions from Steam Input for a local player slot.
/// </summary>
public sealed class SteamInputBackend
{
    private readonly SteamInputManager _steam;
    private readonly bool[] _prevDigital = new bool[SteamInputActionNames.DigitalActions.Length];
    private readonly bool[] _currDigital = new bool[SteamInputActionNames.DigitalActions.Length];
    // Per-slot edge tracking: [slot, actionIndex]
    private readonly bool[,] _prevBySlot = new bool[InputManager.MaxLocalPlayers, SteamInputActionNames.DigitalActions.Length];
    private readonly bool[,] _currBySlot = new bool[InputManager.MaxLocalPlayers, SteamInputActionNames.DigitalActions.Length];

    public SteamInputBackend(SteamInputManager steam)
    {
        _steam = steam;
    }

    public bool IsActive => _steam.IsInitialized && _steam.ConnectedControllerCount > 0;

    public bool HasController(int localPlayerSlot) =>
        _steam.IsInitialized && _steam.GetHandleForSlot(localPlayerSlot).m_InputHandle != 0;

    public void BeginFrame()
    {
        if (!_steam.IsInitialized)
        {
            return;
        }

        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            for (int a = 0; a < SteamInputActionNames.DigitalActions.Length; a++)
            {
                _prevBySlot[slot, a] = _currBySlot[slot, a];
                _currBySlot[slot, a] = _steam.GetDigital(slot, SteamInputActionNames.DigitalActions[a]);
            }
        }

        for (int a = 0; a < SteamInputActionNames.DigitalActions.Length; a++)
        {
            _prevDigital[a] = _currDigital[a];
            _currDigital[a] = false;
            for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
            {
                if (_currBySlot[slot, a])
                {
                    _currDigital[a] = true;
                    break;
                }
            }
        }
    }

    public PlayerInputState ReadGameplay(int localPlayerSlot)
    {
        if (!HasController(localPlayerSlot))
        {
            return PlayerInputState.Empty;
        }

        float moveX = 0f;
        float moveY = 0f;
        if (_steam.TryGetAnalog(localPlayerSlot, SteamInputActionNames.Move, out float ax, out float ay))
        {
            Vector2 processed = GamepadDefaults.ProcessLeftStick(new Vector2(ax, ay));
            moveX = Math.Clamp(processed.X, -1f, 1f);
            moveY = processed.Y;
        }

        GameColor? color = null;
        if (WasPressed(localPlayerSlot, SteamInputActionNames.ColorRed))
        {
            color = GameColor.Red;
        }
        else if (WasPressed(localPlayerSlot, SteamInputActionNames.ColorBlue))
        {
            color = GameColor.Blue;
        }
        else if (WasPressed(localPlayerSlot, SteamInputActionNames.ColorGreen))
        {
            color = GameColor.Green;
        }

        bool fastFall = moveY < -GamepadDefaults.FastFallProcessedThreshold;
        bool pullRope = IsHeld(localPlayerSlot, SteamInputActionNames.PullRope);

        return new PlayerInputState(
            moveX,
            WasPressed(localPlayerSlot, SteamInputActionNames.Jump),
            WasPressed(localPlayerSlot, SteamInputActionNames.Respawn),
            fastFall,
            pullRope,
            color);
    }

    public void MergeMenuFlags(ref MenuInputFlags flags)
    {
        if (!IsActive)
        {
            return;
        }

        if (WasPressedAny(SteamInputActionNames.MenuAccept))
        {
            flags.ConfirmPressed = true;
            flags.Activity = true;
        }

        if (IsHeldAny(SteamInputActionNames.MenuAccept))
        {
            flags.ConfirmHeld = true;
        }

        if (WasPressedAny(SteamInputActionNames.MenuCancel))
        {
            flags.CancelPressed = true;
            flags.Activity = true;
        }

        if (WasPressedAny(SteamInputActionNames.MenuBack))
        {
            flags.BackPressed = true;
            flags.Activity = true;
        }

        if (WasPressedAny(SteamInputActionNames.Pause) || WasPressedAny(SteamInputActionNames.MenuStart))
        {
            flags.PausePressed = true;
            flags.Activity = true;
        }

        float navX = 0f;
        float navY = 0f;
        bool hasNav = false;
        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            if (!_steam.TryGetAnalog(slot, SteamInputActionNames.MenuNavigate, out float x, out float y))
            {
                continue;
            }

            Vector2 processed = GamepadDefaults.ProcessLeftStick(new Vector2(x, y));
            if (MathF.Abs(processed.X) > MathF.Abs(navX))
            {
                navX = processed.X;
            }

            if (MathF.Abs(processed.Y) > MathF.Abs(navY))
            {
                navY = processed.Y;
            }

            hasNav = true;
        }

        if (!hasNav)
        {
            return;
        }

        if (navY > GamepadDefaults.MenuStickDirectionThreshold)
        {
            flags.StickUpHeld = true;
            flags.Activity = true;
        }

        if (navY < -GamepadDefaults.MenuStickDirectionThreshold)
        {
            flags.StickDownHeld = true;
            flags.Activity = true;
        }

        if (navX < -GamepadDefaults.MenuStickDirectionThreshold)
        {
            flags.StickLeftHeld = true;
            flags.Activity = true;
        }

        if (navX > GamepadDefaults.MenuStickDirectionThreshold)
        {
            flags.StickRightHeld = true;
            flags.Activity = true;
        }
    }

    public bool WasPressed(int localPlayerSlot, string actionName)
    {
        int index = IndexOfDigital(actionName);
        if (index < 0 || localPlayerSlot < 0 || localPlayerSlot >= InputManager.MaxLocalPlayers)
        {
            return false;
        }

        return _currBySlot[localPlayerSlot, index] && !_prevBySlot[localPlayerSlot, index];
    }

    public bool IsHeld(int localPlayerSlot, string actionName)
    {
        int index = IndexOfDigital(actionName);
        if (index < 0 || localPlayerSlot < 0 || localPlayerSlot >= InputManager.MaxLocalPlayers)
        {
            return false;
        }

        return _currBySlot[localPlayerSlot, index];
    }

    public bool WasPressedAny(string actionName)
    {
        int index = IndexOfDigital(actionName);
        if (index < 0)
        {
            return false;
        }

        return _currDigital[index] && !_prevDigital[index];
    }

    public bool IsHeldAny(string actionName)
    {
        int index = IndexOfDigital(actionName);
        return index >= 0 && _currDigital[index];
    }

    private static int IndexOfDigital(string actionName)
    {
        for (int i = 0; i < SteamInputActionNames.DigitalActions.Length; i++)
        {
            if (string.Equals(SteamInputActionNames.DigitalActions[i], actionName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Scratch flags merged from Steam menu actions into InputManager.
/// </summary>
public struct MenuInputFlags
{
    public bool ConfirmPressed;
    public bool ConfirmHeld;
    public bool CancelPressed;
    public bool BackPressed;
    public bool PausePressed;
    public bool StickUpHeld;
    public bool StickDownHeld;
    public bool StickLeftHeld;
    public bool StickRightHeld;
    public bool Activity;
}
