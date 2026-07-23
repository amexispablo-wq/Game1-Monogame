#nullable enable
using System;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

/// <summary>
/// Steam Input gameplay backend only. No menu knowledge — InputManager synthesizes UI.
/// </summary>
public sealed class SteamInputBackend
{
    private readonly SteamInputManager _steam;
    private readonly bool[] _prevDigital = new bool[SteamInputActionNames.DigitalActions.Length];
    private readonly bool[] _currDigital = new bool[SteamInputActionNames.DigitalActions.Length];
    private readonly bool[,] _prevBySlot = new bool[InputManager.MaxLocalPlayers, SteamInputActionNames.DigitalActions.Length];
    private readonly bool[,] _currBySlot = new bool[InputManager.MaxLocalPlayers, SteamInputActionNames.DigitalActions.Length];
    private readonly Vector2[] _moveBySlot = new Vector2[InputManager.MaxLocalPlayers];
    private readonly bool[] _prevSlotLive = new bool[InputManager.MaxLocalPlayers];
    private bool _slotLiveStateReady;

    public SteamInputBackend(SteamInputManager steam)
    {
        _steam = steam;
    }

    /// <summary>
    /// Steam Input drives pads only when at least one slot has live actions (bActive).
    /// Soft-claimed handles do not activate — InputManager falls through to GamepadBackend.
    /// </summary>
    public bool IsActive => _steam.IsControllerAvailable;

    /// <summary>Aggregated Move vector (strongest magnitude across connected slots).</summary>
    public Vector2 MoveVector { get; private set; }

    /// <summary>True only when this slot's Steam actions are live — not mere handle presence.</summary>
    public bool HasController(int localPlayerSlot) => _steam.IsSlotLive(localPlayerSlot);

    public Vector2 GetMoveVector(int localPlayerSlot)
    {
        if (localPlayerSlot < 0 || localPlayerSlot >= InputManager.MaxLocalPlayers)
        {
            return Vector2.Zero;
        }

        return _moveBySlot[localPlayerSlot];
    }

    public void BeginFrame()
    {
        MoveVector = Vector2.Zero;
        if (!_steam.IsInitialized)
        {
            for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
            {
                _moveBySlot[slot] = Vector2.Zero;
            }

            return;
        }

        float bestLenSq = 0f;
        Vector2 best = Vector2.Zero;
        bool anyOwnershipChange = false;

        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            for (int a = 0; a < SteamInputActionNames.DigitalActions.Length; a++)
            {
                _prevBySlot[slot, a] = _currBySlot[slot, a];
                _currBySlot[slot, a] = _steam.GetDigital(slot, SteamInputActionNames.DigitalActions[a]);
            }

            bool live = _steam.IsSlotLive(slot);
            if (_slotLiveStateReady && live != _prevSlotLive[slot])
            {
                // Soft↔live (or held buttons on first live) must not synthesize rising edges.
                for (int a = 0; a < SteamInputActionNames.DigitalActions.Length; a++)
                {
                    _prevBySlot[slot, a] = _currBySlot[slot, a];
                }

                anyOwnershipChange = true;
                DiagnosticsLog.Info(
                    "Input",
                    $"SteamSlot[{slot}] edge suppress live transition ({_prevSlotLive[slot]} -> {live})");
            }

            _prevSlotLive[slot] = live;

            Vector2 move = Vector2.Zero;
            if (_steam.TryGetAnalog(slot, SteamInputActionNames.Move, out float ax, out float ay))
            {
                move = GamepadDefaults.ProcessLeftStick(new Vector2(ax, ay));
            }

            _moveBySlot[slot] = move;
            float lenSq = move.LengthSquared();
            if (lenSq > bestLenSq)
            {
                bestLenSq = lenSq;
                best = move;
            }
        }

        _slotLiveStateReady = true;
        MoveVector = best;

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

            if (anyOwnershipChange)
            {
                _prevDigital[a] = _currDigital[a];
            }
        }
    }

    public PlayerInputState ReadGameplay(int localPlayerSlot)
    {
        if (!HasController(localPlayerSlot))
        {
            return PlayerInputState.Empty;
        }

        Vector2 move = GetMoveVector(localPlayerSlot);
        float moveX = Math.Clamp(move.X, -1f, 1f);

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

        return new PlayerInputState(
            moveX,
            WasPressed(localPlayerSlot, SteamInputActionNames.Jump),
            WasPressed(localPlayerSlot, SteamInputActionNames.Respawn),
            move.Y < -GamepadDefaults.FastFallProcessedThreshold,
            IsHeld(localPlayerSlot, SteamInputActionNames.PullRope),
            color,
            move,
            MenuNavigate: default);
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
