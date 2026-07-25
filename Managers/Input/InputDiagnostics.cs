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
/// Records gameplay edges + suspicious phantom heuristics for Jump/Respawn/Color.
/// </summary>
public static class InputDiagnostics
{
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RespawnClusterWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DupEdgeWindow = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ThrashWindow = TimeSpan.FromMilliseconds(300);
    private const int EdgeRingCapacity = 128;
    private const int RespawnClusterThreshold = 3;
    private const float SoftClaimUnstableSeconds = 1.5f;

    private static DateTime _lastPeriodicUtc = DateTime.MinValue;
    private static PartyInputSource _prevPartySource = PartyInputSource.Keyboard;
    private static int _prevPartyControllerId = -1;
    private static ActiveInputBackend _prevBackend = ActiveInputBackend.Keyboard;
    private static readonly bool[] PrevLive = new bool[InputManager.MaxLocalPlayers];
    private static readonly bool[] PrevSoft = new bool[InputManager.MaxLocalPlayers];
    private static readonly bool[] PrevXInputConnected = new bool[InputManager.MaxLocalPlayers];
    private static readonly ulong[] PrevSteamHandle = new ulong[InputManager.MaxLocalPlayers];
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
    private static int _srcSteamLive;
    private static int _srcGamepadSoft;
    private static int _srcGamepad;
    private static int _srcOther;
    private static int _softClaimFlips;
    private static int _xInputSuppressEvents;
    private static int _suspiciousRespawnCluster;
    private static int _suspiciousDupEdge;
    private static int _suspiciousSoftClaimEdge;
    private static int _suspiciousThrash;
    private static int _suppressedPadEdges;
    private static int _respawnDebounceSuppressed;
    private static int _simApplyJump;
    private static int _simApplyRespawn;
    private static int _configLoadedCount;

    private static readonly Queue<DateTime> _recentRespawnUtc = new();
    private static string _lastEdgeAction = string.Empty;
    private static DateTime _lastEdgeActionUtc = DateTime.MinValue;
    private static DateTime _lastLiveSoftFlipUtc = DateTime.MinValue;

    public static void SetGhostOverlayActive(bool personalBest, bool worldRecord)
    {
        _ghostPersonal = personalBest;
        _ghostWorldRecord = worldRecord;
    }

    public static void NoteXInputSoftClaimSuppress(int slot, int frames)
    {
        _xInputSuppressEvents++;
        DiagnosticsLog.Info(
            "Input",
            $"SteamSlot[{slot}] XInput edge suppress Soft Claim ({frames}f)");
    }

    /// <summary>XInput rising edges stripped during Soft Claim thrash / hollow — phantom candidates blocked.</summary>
    public static void NoteSuppressedPadEdges(
        int slot,
        PlayerInputState pad,
        string reason,
        float softSeconds)
    {
        _suppressedPadEdges++;
        var actions = new StringBuilder();
        if (pad.JumpPressed)
        {
            actions.Append("Jump ");
        }

        if (pad.RespawnPressed)
        {
            actions.Append("Respawn ");
        }

        if (pad.RequestedColor is GameColor color)
        {
            actions.Append("Color").Append(color).Append(' ');
        }

        string line =
            $"EDGE_SUPPRESSED slot={slot} reason={reason} softSec={softSeconds:0.00} " +
            $"move=({pad.Move.X:0.00},{pad.Move.Y:0.00}) {actions.ToString().TrimEnd()}";
        DiagnosticsLog.Info("Input", line);
        PushEdgeRing($"{DateTime.Now:HH:mm:ss.fff} {line}");
    }

    public static void NoteRespawnDebounceSuppressed()
    {
        _respawnDebounceSuppressed++;
        PushEdgeRing($"{DateTime.Now:HH:mm:ss.fff} EDGE Respawn suppressed debounce");
    }

    public static void NoteSimApplyJump() => _simApplyJump++;

    public static void NoteSimApplyRespawn() => _simApplyRespawn++;

    public static void NoteConfigurationLoaded(ulong handle)
    {
        _configLoadedCount++;
        DiagnosticsLog.Info(
            "Input",
            $"ConfigurationLoaded handle=0x{handle:X} (count={_configLoadedCount})");
    }

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
            $"Routed Move=({input.Move.X:0.00},{input.Move.Y:0.00}) MenuNavigate=({input.MenuNavigate.X:0.00},{input.MenuNavigate.Y:0.00})",
            $"GameplayInputBlocked: {input.GameplayInputBlocked}",
            $"Ghost overlays: personal={(_ghostPersonal ? "yes" : "no")} wr={(_ghostWorldRecord ? "yes" : "no")} (do NOT inject input)"
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
                $"Start={state.IsButtonDown(Buttons.Start)}" +
                (GamepadDefaults.IsHollowCornerStick(left) ? " hollowCorner=True" : string.Empty));
        }

        return sb.ToString();
    }

    /// <summary>Recent gameplay digital edges for diagnostics zip (oldest→newest).</summary>
    public static string BuildEdgeRingText()
    {
        if (_edgeRingCount == 0)
        {
            return "=== INPUT EDGE RING ===" + Environment.NewLine + "(empty — no Jump/Color/Respawn edges yet)";
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== INPUT EDGE RING (newest last, capacity " + EdgeRingCapacity + ") ===");
        sb.AppendLine("Format: time EDGE|EDGE_SUPPRESSED ... bind= softSec= unstable= ...");
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
        sb.AppendLine(
            $"EDGE by src: Keyboard={_srcKeyboard} SteamLive={_srcSteamLive} " +
            $"GamepadSoft={_srcGamepadSoft} Gamepad={_srcGamepad} Other={_srcOther}");
        sb.AppendLine(
            $"Sim APPLY: Jump={_simApplyJump} Respawn={_simApplyRespawn} " +
            $"(EDGE→APPLY Respawn gap={_edgeRespawn - _simApplyRespawn})");
        sb.AppendLine(
            $"Blocked: EDGE_SUPPRESSED={_suppressedPadEdges} RespawnDebounce={_respawnDebounceSuppressed} " +
            $"XInputSuppressKicks={_xInputSuppressEvents}");
        sb.AppendLine(
            $"SoftClaim flips: {_softClaimFlips} | ConfigurationLoaded: {_configLoadedCount}");
        sb.AppendLine(
            $"Suspicious: RespawnCluster={_suspiciousRespawnCluster} DupEdge={_suspiciousDupEdge} " +
            $"SoftClaimEdge={_suspiciousSoftClaimEdge} Thrash={_suspiciousThrash}");

        if (input is not null)
        {
            sb.AppendLine($"Bindings: {input.DescribeBindingsForDiagnostics()}");
            float softSec = input.SteamInput?.SoftClaimSeconds ?? 0f;
            sb.AppendLine(
                $"SoftClaim now: any={input.SteamInput?.HasAnySoftClaim == true} softSec={softSec:0.00} " +
                $"unstable={(softSec > 0f && softSec < SoftClaimUnstableSeconds)}");
        }
        else
        {
            sb.AppendLine("Bindings: (InputManager not passed)");
        }

        sb.AppendLine($"Verdict hint: {BuildVerdictHint()}");
        sb.AppendLine();
        sb.AppendLine("How to read:");
        sb.AppendLine("  EDGE_SUPPRESSED + Jump/Respawn = Soft Claim thrash almost injected phantom (blocked).");
        sb.AppendLine("  EDGE src=Keyboard bind=R = MonoGame saw R rising edge (not Soft Claim).");
        sb.AppendLine("  EDGE src=GamepadSoft softSec<1.5 = Soft Claim fallthrough digitals.");
        sb.AppendLine("  APPLY Respawn without EDGE = latch/UI path (search pause menu / death UI).");
        return sb.ToString();
    }

    /// <summary>
    /// Log Jump / Respawn / Color rising edges applied to gameplay this frame.
    /// </summary>
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

        string source = DescribeSource(member, input);
        SteamInputManager? steam = input.SteamInput;
        int slot = member.ControllerId;
        bool live = slot >= 0 && steam?.IsSlotLive(slot) == true;
        bool soft = slot >= 0 && steam?.HasSoftClaim(slot) == true;
        bool liveRaw = slot >= 0 && steam?.IsSlotLiveRaw(slot) == true;
        float softSec = steam?.SoftClaimSeconds ?? 0f;
        string ghost = FormatGhostFlag();

        CountSource(source);
        DateTime now = DateTime.UtcNow;

        if (state.JumpPressed)
        {
            _edgeJump++;
            RecordOneEdge(
                networkId,
                member,
                input,
                source,
                live,
                soft,
                liveRaw,
                softSec,
                ghost,
                state,
                "Jump",
                GameplayInputAction.Jump,
                now);
        }

        if (state.RespawnPressed)
        {
            _edgeRespawn++;
            RecordOneEdge(
                networkId,
                member,
                input,
                source,
                live,
                soft,
                liveRaw,
                softSec,
                ghost,
                state,
                "Respawn",
                GameplayInputAction.Respawn,
                now);
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
            RecordOneEdge(
                networkId,
                member,
                input,
                source,
                live,
                soft,
                liveRaw,
                softSec,
                ghost,
                state,
                "Color" + color,
                colorAction,
                now);
        }

        if ((source == "GamepadSoft" || soft) && softSec < SoftClaimUnstableSeconds)
        {
            _suspiciousSoftClaimEdge++;
            DiagnosticsLog.Info("Input", $"SUSPICIOUS SoftClaimEdge softSec={softSec:0.00}");
        }
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

    private static void RecordOneEdge(
        int networkId,
        PartyMember member,
        InputManager input,
        string source,
        bool live,
        bool soft,
        bool liveRaw,
        float softSec,
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

        bool unstable = soft && softSec < SoftClaimUnstableSeconds;
        string line =
            $"EDGE netId={networkId} src={source} bind={bindToken} prevDown={prevDown} currDown={currDown} " +
            $"ghost={ghost} live={live} soft={soft} softSec={softSec:0.00} unstable={unstable} raw={liveRaw} " +
            $"backend={input.ActiveInputBackend} party={member.InputSource}/{member.ControllerId} " +
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
            case "SteamLive":
                _srcSteamLive++;
                break;
            case "GamepadSoft":
                _srcGamepadSoft++;
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
        if (_suppressedPadEdges > 0 && _suppressedPadEdges >= Math.Max(1, total))
        {
            return "Many EDGE_SUPPRESSED — Soft Claim thrash inventing XInput edges (blocked). Harden sticky/live further.";
        }

        if (total == 0)
        {
            if (_suppressedPadEdges > 0)
            {
                return "No applied EDGE but EDGE_SUPPRESSED>0 — Soft Claim phantoms blocked; thrash still active.";
            }

            return "No EDGE yet — reproduce phantom then re-export.";
        }

        if (_srcGamepadSoft > 0 || _suspiciousSoftClaimEdge > 0 || _suspiciousThrash > 0)
        {
            return "Soft Claim / GamepadSoft edges present — Soft Claim fallthrough likely.";
        }

        if (_srcKeyboard >= total * 0.8 && _edgeRespawn > 0)
        {
            return "Keyboard Respawn edges dominate — Soft Claim unlikely; check bind=R / debounce / prevDown/currDown.";
        }

        if (_srcKeyboard >= total * 0.8)
        {
            return "Keyboard edges dominate — Soft Claim unlikely this session.";
        }

        if (_srcSteamLive >= total * 0.8)
        {
            return "SteamLive edges dominate — check digital bState flicker while sticky-live.";
        }

        return "Mixed sources — inspect EDGE ring bind=/src=/EDGE_SUPPRESSED lines.";
    }

    private static string DescribeSource(PartyMember member, InputManager input)
    {
        if (member.InputSource == PartyInputSource.Keyboard)
        {
            return "Keyboard";
        }

        if (member.InputSource != PartyInputSource.Gamepad)
        {
            return member.InputSource.ToString();
        }

        int slot = member.ControllerId;
        if (input.SteamInput is not null && input.SteamInput.IsSlotLive(slot))
        {
            return "SteamLive";
        }

        if (input.SteamInput?.HasSoftClaim(slot) == true)
        {
            return "GamepadSoft";
        }

        return "Gamepad";
    }

    private static void PushEdgeRing(string line)
    {
        EdgeRing[_edgeRingNext] = line;
        _edgeRingNext = (_edgeRingNext + 1) % EdgeRingCapacity;
        if (_edgeRingCount < EdgeRingCapacity)
        {
            _edgeRingCount++;
        }
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
        DateTime now = DateTime.UtcNow;
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
                    bool raw = steam?.IsSlotLiveRaw(i) == true;
                    DiagnosticsLog.Info(
                        "Input",
                        $"SteamSlot[{i}] live {PrevLive[i]} -> {live} (raw={raw})");
                    NoteThrash(now);
                }

                if (soft != PrevSoft[i])
                {
                    _softClaimFlips++;
                    DiagnosticsLog.Info(
                        "Input",
                        soft
                            ? $"SteamSlot[{i}] SOFT CLAIM — falling back to Gamepad/XInput"
                            : $"SteamSlot[{i}] soft-claim cleared");
                    NoteThrash(now);
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

    private static void NoteThrash(DateTime now)
    {
        if (_lastLiveSoftFlipUtc != DateTime.MinValue && now - _lastLiveSoftFlipUtc < ThrashWindow)
        {
            _suspiciousThrash++;
            DiagnosticsLog.Info(
                "Input",
                $"SUSPICIOUS Thrash dtMs={(now - _lastLiveSoftFlipUtc).TotalMilliseconds:0}");
        }

        _lastLiveSoftFlipUtc = now;
    }

    private static string CompactLine(InputManager input)
    {
        var sb = new StringBuilder();
        sb.Append($"backend={input.ActiveInputBackend} party={input.LastUsedPartyInputSource}/{input.LastUsedPartyControllerId} ");
        sb.Append($"managing={input.IsSteamInputManagingControllers}");
        sb.Append($" ghost={FormatGhostFlag()}");
        if (input.SteamInput is { } steamSoft)
        {
            sb.Append($" softSec={steamSoft.SoftClaimSeconds:0.00}");
        }

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
                    $" S{i}:0x{handle:X} live={steam.IsSlotLive(i)} raw={steam.IsSlotLiveRaw(i)} soft={steam.HasSoftClaim(i)} " +
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
            if (GamepadDefaults.IsHollowCornerStick(left))
            {
                sb.Append(" hollow");
            }
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
