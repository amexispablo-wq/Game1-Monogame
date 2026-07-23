#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Compact input pipeline snapshot for session logs and diagnostics export.
/// Proves Steam live vs soft-claim vs hollow XInput vs party-join.
/// </summary>
public static class InputDiagnostics
{
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromSeconds(2);

    private static DateTime _lastPeriodicUtc = DateTime.MinValue;
    private static PartyInputSource _prevPartySource = PartyInputSource.Keyboard;
    private static int _prevPartyControllerId = -1;
    private static ActiveInputBackend _prevBackend = ActiveInputBackend.Keyboard;
    private static readonly bool[] PrevLive = new bool[InputManager.MaxLocalPlayers];
    private static readonly bool[] PrevSoft = new bool[InputManager.MaxLocalPlayers];
    private static readonly bool[] PrevXInputConnected = new bool[InputManager.MaxLocalPlayers];
    private static readonly ulong[] PrevSteamHandle = new ulong[InputManager.MaxLocalPlayers];
    private static bool _edgeStateReady;

    public static IReadOnlyList<string> BuildSnapshotLines(InputManager input)
    {
        var lines = new List<string>
        {
            "=== INPUT SNAPSHOT ===",
            $"ActiveInputBackend: {input.ActiveInputBackend}",
            $"AnalogContext: {input.AnalogContext}",
            $"SteamInputEnabled: {input.IsSteamInputEnabled}",
            $"SteamControllerAvailable: {input.IsSteamControllerAvailable}",
            $"SteamManaging: {input.IsSteamInputManagingControllers}",
            $"PartyLastUsed: {input.LastUsedPartyInputSource} controllerId={input.LastUsedPartyControllerId}",
            $"Routed Move=({input.Move.X:0.00},{input.Move.Y:0.00}) MenuNavigate=({input.MenuNavigate.X:0.00},{input.MenuNavigate.Y:0.00})"
        };

        SteamInputManager? steam = input.SteamInput;
        if (steam is null)
        {
            lines.Add("SteamInputManager: (not bound)");
        }
        else
        {
            lines.AddRange(steam.BuildDiagnosticsLines());
        }

        lines.Add("--- XInput / MonoGame GamePad ---");
        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            GamePadState state = GamePad.GetState((PlayerIndex)i);
            if (!state.IsConnected)
            {
                lines.Add($"XInput[{i}]: connected=false");
                continue;
            }

            Vector2 left = state.ThumbSticks.Left;
            bool a = state.IsButtonDown(Buttons.A);
            bool b = state.IsButtonDown(Buttons.B);
            bool x = state.IsButtonDown(Buttons.X);
            bool y = state.IsButtonDown(Buttons.Y);
            bool start = state.IsButtonDown(Buttons.Start);
            lines.Add(
                $"XInput[{i}]: connected=true left=({left.X:0.00},{left.Y:0.00}) " +
                $"A={a} B={b} X={x} Y={y} Start={start}");
        }

        return lines;
    }

    public static string BuildSnapshotText(InputManager input) =>
        string.Join(Environment.NewLine, BuildSnapshotLines(input));

    public static string BuildGamepadOnlyText(InputManager input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== GAMEPAD / XINPUT ===");
        sb.AppendLine($"ActiveInputBackend: {input.ActiveInputBackend}");
        sb.AppendLine($"SteamManaging: {input.IsSteamInputManagingControllers}");
        sb.AppendLine($"PartyLastUsed: {input.LastUsedPartyInputSource} id={input.LastUsedPartyControllerId}");
        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            GamePadState state = GamePad.GetState((PlayerIndex)i);
            if (!state.IsConnected)
            {
                sb.AppendLine($"XInput[{i}]: connected=false");
                continue;
            }

            Vector2 left = state.ThumbSticks.Left;
            sb.AppendLine(
                $"XInput[{i}]: connected=true left=({left.X:0.00},{left.Y:0.00}) " +
                $"A={state.IsButtonDown(Buttons.A)} B={state.IsButtonDown(Buttons.B)} " +
                $"X={state.IsButtonDown(Buttons.X)} Y={state.IsButtonDown(Buttons.Y)} " +
                $"Start={state.IsButtonDown(Buttons.Start)}");
        }

        return sb.ToString();
    }

    /// <summary>Call once per frame from InputManager after party/backend resolve.</summary>
    public static void UpdateSessionLogging(InputManager input)
    {
        LogEdgeTransitions(input);

        DateTime now = DateTime.UtcNow;
        if (now - _lastPeriodicUtc < PeriodicInterval)
        {
            return;
        }

        if (!ShouldPeriodicLog(input))
        {
            return;
        }

        _lastPeriodicUtc = now;
        DiagnosticsLog.Info("Input", CompactLine(input));
    }

    private static bool ShouldPeriodicLog(InputManager input)
    {
        SteamInputManager? steam = input.SteamInput;
        if (steam is { IsInitialized: true } && steam.ConnectedControllerCount > 0)
        {
            return true;
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (GamePad.GetState((PlayerIndex)i).IsConnected)
            {
                return true;
            }
        }

        return false;
    }

    private static void LogEdgeTransitions(InputManager input)
    {
        if (input.LastUsedPartyInputSource != _prevPartySource
            || input.LastUsedPartyControllerId != _prevPartyControllerId)
        {
            DiagnosticsLog.Info(
                "Input",
                $"PartyLastUsed {_prevPartySource}/{_prevPartyControllerId} -> " +
                $"{input.LastUsedPartyInputSource}/{input.LastUsedPartyControllerId}");
            _prevPartySource = input.LastUsedPartyInputSource;
            _prevPartyControllerId = input.LastUsedPartyControllerId;
        }

        if (input.ActiveInputBackend != _prevBackend)
        {
            DiagnosticsLog.Info("Input", $"ActiveInputBackend {_prevBackend} -> {input.ActiveInputBackend}");
            _prevBackend = input.ActiveInputBackend;
        }

        SteamInputManager? steam = input.SteamInput;
        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            ulong handle = steam?.GetSlotHandleRaw(i) ?? 0;
            bool live = steam?.IsSlotLive(i) == true;
            bool soft = steam?.HasSoftClaim(i) == true;
            bool xConnected = GamePad.GetState((PlayerIndex)i).IsConnected;

            if (_edgeStateReady)
            {
                if (handle != PrevSteamHandle[i])
                {
                    DiagnosticsLog.Info(
                        "Input",
                        $"SteamSlot[{i}] handle 0x{PrevSteamHandle[i]:X} -> 0x{handle:X}");
                }

                if (live != PrevLive[i])
                {
                    DiagnosticsLog.Info("Input", $"SteamSlot[{i}] live {PrevLive[i]} -> {live}");
                }

                if (soft != PrevSoft[i])
                {
                    DiagnosticsLog.Info(
                        "Input",
                        soft
                            ? $"SteamSlot[{i}] SOFT CLAIM — falling back to Gamepad/XInput"
                            : $"SteamSlot[{i}] soft-claim cleared");
                }

                if (xConnected != PrevXInputConnected[i])
                {
                    DiagnosticsLog.Info(
                        "Input",
                        $"XInput[{i}] connected {PrevXInputConnected[i]} -> {xConnected}");
                }
            }

            PrevSteamHandle[i] = handle;
            PrevLive[i] = live;
            PrevSoft[i] = soft;
            PrevXInputConnected[i] = xConnected;
        }

        _edgeStateReady = true;
    }

    private static string CompactLine(InputManager input)
    {
        var sb = new StringBuilder();
        sb.Append($"backend={input.ActiveInputBackend} party={input.LastUsedPartyInputSource}/{input.LastUsedPartyControllerId} ");
        sb.Append($"managing={input.IsSteamInputManagingControllers}");

        SteamInputManager? steam = input.SteamInput;
        if (steam is { IsInitialized: true })
        {
            sb.Append($" steamCount={steam.ConnectedControllerCount}");
            string missing = steam.GetMissingActionSummary();
            if (missing.Length > 0)
            {
                sb.Append($" missing=[{missing}]");
            }

            for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
            {
                ulong handle = steam.GetSlotHandleRaw(i);
                if (handle == 0)
                {
                    continue;
                }

                steam.TryGetAnalog(i, SteamInputActionNames.Move, out float mx, out float my);
                sb.Append(
                    $" S{i}:0x{handle:X} live={steam.IsSlotLive(i)} soft={steam.HasSoftClaim(i)} " +
                    $"move=({mx:0.00},{my:0.00})");
            }
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            GamePadState state = GamePad.GetState((PlayerIndex)i);
            if (!state.IsConnected)
            {
                continue;
            }

            Vector2 left = state.ThumbSticks.Left;
            sb.Append($" X{i}:on left=({left.X:0.00},{left.Y:0.00})");
        }

        bool anyX = false;
        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (GamePad.GetState((PlayerIndex)i).IsConnected)
            {
                anyX = true;
                break;
            }
        }

        if (!anyX && steam is { ConnectedControllerCount: > 0 })
        {
            sb.Append(" XInput=none(hollow?)");
        }

        return sb.ToString();
    }
}
