#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Compact input pipeline snapshot for session logs and diagnostics export.
/// Records gameplay edges + suspicious phantom heuristics for Jump/Respawn/Color.
/// </summary>
public static class InputDiagnostics
{
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RespawnClusterWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DupEdgeWindow = TimeSpan.FromMilliseconds(50);
    private const int EdgeRingCapacity = 128;
    private const int RespawnClusterThreshold = 3;

    private static DateTime _lastPeriodicUtc = DateTime.MinValue;
    private static PartyInputSource _prevPartySource = PartyInputSource.Keyboard;
    private static int _prevPartyControllerId = -1;
    private static ActiveInputBackend _prevBackend = ActiveInputBackend.Keyboard;
    private static readonly bool[] PrevXInputConnected = new bool[InputManager.MaxLocalPlayers];
    private static bool _edgeStateReady;

    private static readonly string[] EdgeRing = new string[EdgeRingCapacity];
    private static int _edgeRingCount;
    private static int _edgeRingNext;

    private static bool _ghostPersonal;
    private static bool _ghostWorldRecord;

    private static int _edgeJump;
    private static int _edgeRespawn;
    private static int _edgeColor;
    private static int _srcKeyboard;
    private static int _srcGamepad;
    private static int _srcOther;
    private static int _suspiciousRespawnCluster;
    private static int _suspiciousDupEdge;
    private static int _respawnDebounceSuppressed;
    private static int _simApplyJump;
    private static int _simApplyRespawn;

    private static readonly Queue<DateTime> _recentRespawnUtc = new();
    private static string _lastEdgeAction = string.Empty;
    private static DateTime _lastEdgeActionUtc = DateTime.MinValue;

    public static void SetGhostOverlayActive(bool personalBest, bool worldRecord)
    {
        _ghostPersonal = personalBest;
        _ghostWorldRecord = worldRecord;
    }

    public static void NoteRespawnDebounceSuppressed()
    {
        _respawnDebounceSuppressed++;
        PushEdgeRing($"{DateTime.Now:HH:mm:ss.fff} EDGE Respawn suppressed debounce");
    }

    public static void NoteSimApplyJump() => _simApplyJump++;

    public static void NoteSimApplyRespawn() => _simApplyRespawn++;

    public static IReadOnlyList<string> BuildSnapshotLines(InputManager input)
    {
        var lines = new List<string>
        {
            "=== INPUT SNAPSHOT ===",
            $"ActiveInputBackend: {input.ActiveInputBackend}",
            $"AnalogContext: {input.AnalogContext}",
            $"PartyLastUsed: {input.LastUsedPartyInputSource} controllerId={input.LastUsedPartyControllerId}",
            $"Routed Move=({input.Move.X:0.00},{input.Move.Y:0.00}) MenuNavigate=({input.MenuNavigate.X:0.00},{input.MenuNavigate.Y:0.00})",
            $"GameplayInputBlocked: {input.GameplayInputBlocked}",
            $"Ghost overlays: personal={(_ghostPersonal ? "yes" : "no")} wr={(_ghostWorldRecord ? "yes" : "no")} (do NOT inject input)",
            "--- MonoGame GamePad ---"
        };

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
            bool back = state.IsButtonDown(Buttons.Back);
            string hollow = GamepadDefaults.IsHollowCornerStick(left) ? " hollowCorner=True" : string.Empty;
            lines.Add(
                $"XInput[{i}]: connected=true left=({left.X:0.00},{left.Y:0.00}) " +
                $"A={a} B={b} X={x} Y={y} Start={start} Back={back}{hollow}");
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
                $"Start={state.IsButtonDown(Buttons.Start)}" +
                (GamepadDefaults.IsHollowCornerStick(left) ? " hollowCorner=True" : string.Empty));
        }

        return sb.ToString();
    }

    public static string BuildEdgeRingText()
    {
        if (_edgeRingCount == 0)
        {
            return "=== INPUT EDGE RING ===" + Environment.NewLine + "(empty — no Jump/Color/Respawn edges yet)";
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== INPUT EDGE RING (newest last, capacity " + EdgeRingCapacity + ") ===");
        int start = _edgeRingCount < EdgeRingCapacity
            ? 0
            : _edgeRingNext;
        int count = Math.Min(_edgeRingCount, EdgeRingCapacity);
        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % EdgeRingCapacity;
            sb.AppendLine(EdgeRing[idx]);
        }

        return sb.ToString();
    }

    public static string BuildPhantomInputSummary(InputManager? input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PHANTOM INPUT SUMMARY ===");
        sb.AppendLine(
            $"Ghost overlays: personal={(_ghostPersonal ? "yes" : "no")} wr={(_ghostWorldRecord ? "yes" : "no")} " +
            "(do NOT inject input)");
        sb.AppendLine($"EDGE totals: Jump={_edgeJump} Color={_edgeColor} Respawn={_edgeRespawn}");
        sb.AppendLine($"EDGE by src: Keyboard={_srcKeyboard} Gamepad={_srcGamepad} Other={_srcOther}");
        sb.AppendLine(
            $"Sim APPLY: Jump={_simApplyJump} Respawn={_simApplyRespawn} " +
            $"(EDGE→APPLY Respawn gap={_edgeRespawn - _simApplyRespawn})");
        sb.AppendLine($"Blocked: RespawnDebounce={_respawnDebounceSuppressed}");
        sb.AppendLine(
            $"Suspicious: RespawnCluster={_suspiciousRespawnCluster} DupEdge={_suspiciousDupEdge}");

        if (input is not null)
        {
            sb.AppendLine($"Bindings: {input.DescribeBindingsForDiagnostics()}");
        }
        else
        {
            sb.AppendLine("Bindings: (InputManager not passed)");
        }

        sb.AppendLine($"Verdict hint: {BuildVerdictHint()}");
        return sb.ToString();
    }

    public static void RecordGameplayEdges(
        int networkId,
        PartyMember member,
        PlayerInputState state,
        InputManager input)
    {
        if (!state.JumpPressed && !state.RespawnPressed && state.RequestedColor is null)
        {
            return;
        }

        string source = DescribeSource(member);
        string ghost = FormatGhostFlag();
        CountSource(source);
        DateTime now = DateTime.UtcNow;

        if (state.JumpPressed)
        {
            _edgeJump++;
            RecordOneEdge(networkId, member, input, source, ghost, state, "Jump", GameplayInputAction.Jump, now);
        }

        if (state.RespawnPressed)
        {
            _edgeRespawn++;
            RecordOneEdge(networkId, member, input, source, ghost, state, "Respawn", GameplayInputAction.Respawn, now);
            NoteRespawnCluster(now);
        }

        if (state.RequestedColor is GameColor color)
        {
            _edgeColor++;
            GameplayInputAction colorAction = color switch
            {
                GameColor.Red => GameplayInputAction.Red,
                GameColor.Blue => GameplayInputAction.Blue,
                GameColor.Green => GameplayInputAction.Green,
                _ => GameplayInputAction.Red
            };
            RecordOneEdge(networkId, member, input, source, ghost, state, "Color" + color, colorAction, now);
        }
    }

    public static void UpdateSessionLogging(InputManager input)
    {
        LogEdgeTransitions(input);

        DateTime now = DateTime.UtcNow;
        if (now - _lastPeriodicUtc < PeriodicInterval)
        {
            return;
        }

        if (!ShouldPeriodicLog())
        {
            return;
        }

        _lastPeriodicUtc = now;
        DiagnosticsLog.Info("Input", CompactLine(input));
    }

    private static void RecordOneEdge(
        int networkId,
        PartyMember member,
        InputManager input,
        string source,
        string ghost,
        PlayerInputState state,
        string actionLabel,
        GameplayInputAction action,
        DateTime now)
    {
        input.DescribeGameplayBindState(
            member,
            action,
            out string bindToken,
            out bool prevDown,
            out bool currDown);

        string line =
            $"EDGE netId={networkId} src={source} bind={bindToken} prevDown={prevDown} currDown={currDown} " +
            $"ghost={ghost} backend={input.ActiveInputBackend} party={member.InputSource}/{member.ControllerId} " +
            $"move=({state.Move.X:0.00},{state.Move.Y:0.00}) {actionLabel}";

        DiagnosticsLog.Info("Input", line);
        PushEdgeRing($"{now:HH:mm:ss.fff} {line}");
        NoteDupEdge(actionLabel, now);
    }

    private static void NoteDupEdge(string actionLabel, DateTime now)
    {
        if (actionLabel == _lastEdgeAction
            && _lastEdgeActionUtc != DateTime.MinValue
            && now - _lastEdgeActionUtc < DupEdgeWindow)
        {
            _suspiciousDupEdge++;
            DiagnosticsLog.Info(
                "Input",
                $"SUSPICIOUS DupEdge action={actionLabel} dtMs={(now - _lastEdgeActionUtc).TotalMilliseconds:0}");
        }

        _lastEdgeAction = actionLabel;
        _lastEdgeActionUtc = now;
    }

    private static void NoteRespawnCluster(DateTime now)
    {
        _recentRespawnUtc.Enqueue(now);
        while (_recentRespawnUtc.Count > 0 && now - _recentRespawnUtc.Peek() > RespawnClusterWindow)
        {
            _recentRespawnUtc.Dequeue();
        }

        if (_recentRespawnUtc.Count >= RespawnClusterThreshold)
        {
            _suspiciousRespawnCluster++;
            DiagnosticsLog.Info(
                "Input",
                $"SUSPICIOUS RespawnCluster count={_recentRespawnUtc.Count} windowSec={RespawnClusterWindow.TotalSeconds:0}");
            _recentRespawnUtc.Clear();
        }
    }

    private static void CountSource(string source)
    {
        switch (source)
        {
            case "Keyboard":
                _srcKeyboard++;
                break;
            case "Gamepad":
                _srcGamepad++;
                break;
            default:
                _srcOther++;
                break;
        }
    }

    private static string FormatGhostFlag()
    {
        if (_ghostPersonal && _ghostWorldRecord)
        {
            return "personal+wr";
        }

        if (_ghostPersonal)
        {
            return "personal";
        }

        if (_ghostWorldRecord)
        {
            return "wr";
        }

        return "off";
    }

    private static string BuildVerdictHint()
    {
        int total = _edgeJump + _edgeRespawn + _edgeColor;
        if (total == 0)
        {
            return "No EDGE yet — reproduce phantom then re-export.";
        }

        if (_srcKeyboard >= total * 0.8 && _edgeRespawn > 0)
        {
            return "Keyboard Respawn edges dominate — check bind=R / debounce / prevDown/currDown.";
        }

        if (_srcKeyboard >= total * 0.8)
        {
            return "Keyboard edges dominate this session.";
        }

        if (_srcGamepad >= total * 0.8)
        {
            return "Gamepad edges dominate this session.";
        }

        return "Mixed sources — inspect EDGE ring bind=/src= lines.";
    }

    private static string DescribeSource(PartyMember member) =>
        member.InputSource == PartyInputSource.Keyboard
            ? "Keyboard"
            : member.InputSource == PartyInputSource.Gamepad
                ? "Gamepad"
                : member.InputSource.ToString();

    private static void PushEdgeRing(string line)
    {
        EdgeRing[_edgeRingNext] = line;
        _edgeRingNext = (_edgeRingNext + 1) % EdgeRingCapacity;
        if (_edgeRingCount < EdgeRingCapacity)
        {
            _edgeRingCount++;
        }
    }

    private static bool ShouldPeriodicLog()
    {
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

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            bool xConnected = GamePad.GetState((PlayerIndex)i).IsConnected;
            if (_edgeStateReady && xConnected != PrevXInputConnected[i])
            {
                DiagnosticsLog.Info(
                    "Input",
                    $"XInput[{i}] connected {PrevXInputConnected[i]} -> {xConnected}");
            }

            PrevXInputConnected[i] = xConnected;
        }

        _edgeStateReady = true;
    }

    private static string CompactLine(InputManager input)
    {
        var sb = new StringBuilder();
        sb.Append($"backend={input.ActiveInputBackend} party={input.LastUsedPartyInputSource}/{input.LastUsedPartyControllerId} ");
        sb.Append($"ghost={FormatGhostFlag()}");

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            GamePadState state = GamePad.GetState((PlayerIndex)i);
            if (!state.IsConnected)
            {
                continue;
            }

            Vector2 left = state.ThumbSticks.Left;
            sb.Append($" X{i}:on left=({left.X:0.00},{left.Y:0.00})");
            if (GamepadDefaults.IsHollowCornerStick(left))
            {
                sb.Append(" hollow");
            }
        }

        return sb.ToString();
    }
}
