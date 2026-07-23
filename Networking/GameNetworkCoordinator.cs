#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks;

public sealed class GameNetworkCoordinator
{
    private readonly SteamGameNetworkService _transport;
    private readonly SteamLobbyService _lobby;
    private readonly Dictionary<int, PlayerInputState> _clientLatchedInput = new();
    private bool _onlineSessionLogged;
    private DateTime _lastActiveRemoteInputLogUtc = DateTime.MinValue;
    private DateTime _lastEmptyRemoteInputLogUtc = DateTime.MinValue;
    private int _loggedInputRecv;
    private int _loggedSnapshotLatch;
    private int _loggedInputSend;
    private int _loggedSnapshotBroadcast;
    private int _lastLatchedSnapshotSequence = -1;
    private long _lastLatchedSnapshotTick = -1;

    public GameNetworkCoordinator(
        SteamGameNetworkService transport,
        SteamLobbyService lobby)
    {
        _transport = transport;
        _lobby = lobby;
    }

    public bool IsOnlineSession(GameSession session) =>
        session.Role is GameSessionRole.Host or GameSessionRole.Client;

    private GameSnapshot? _latestClientSnapshot;

    public void PumpIncoming(GameSession session, GameSimulation simulation)
    {
        if (!IsOnlineSession(session))
        {
            return;
        }

        if (!_onlineSessionLogged)
        {
            _onlineSessionLogged = true;
            MultiplayerDebug.LogNet(
                $"Online session pump START role={session.Role} localOwner={session.LocalOwnerId} " +
                $"lobby={_lobby.CurrentLobbyId}");
        }

        foreach (ReceivedNetworkPacket packet in _transport.ReceiveAll())
        {
            MultiplayerDebug.RecordPacketReceived(packet.IsSnapshot, packet.Payload.Length);
            if (!NetworkPacketCodec.TryDecode(
                    packet.Payload,
                    out NetworkPacketType packetType,
                    out InputFrame? inputFrame,
                    out GameSnapshot? snapshot))
            {
                MultiplayerDebug.LogWarn(
                    $"Decode fail from sid={packet.SenderSteamId} bytes={packet.Payload.Length} snapshot={packet.IsSnapshot}");
                continue;
            }

            if (session.IsHost && packetType == NetworkPacketType.InputFrame && inputFrame is not null)
            {
                inputFrame.Tick = simulation.CurrentTick.Value;
                simulation.InputBuffer.StoreFrame(inputFrame);
                if (_loggedInputRecv < 3 || (_loggedInputRecv % 120) == 0)
                {
                    MultiplayerDebug.LogNet(
                        $"Host recv InputFrame #{_loggedInputRecv + 1} from sid={packet.SenderSteamId} " +
                        $"owner={inputFrame.OwnerId} players={inputFrame.PlayerInputs.Count} tick={inputFrame.Tick}");
                }

                _loggedInputRecv++;
                LogHostRemoteInputSample(packet.SenderSteamId, inputFrame);
                continue;
            }

            if (!session.IsHost && packetType == NetworkPacketType.GameSnapshot && snapshot is not null)
            {
                // Host RestartLevel used to zero Sequence; accept tick+seq rewind as session restart.
                if (_lastLatchedSnapshotSequence >= 0
                    && snapshot.Sequence < _lastLatchedSnapshotSequence
                    && snapshot.Tick < _lastLatchedSnapshotTick)
                {
                    MultiplayerDebug.LogNet(
                        $"Client snapshot latch RESET — restart detect seq {_lastLatchedSnapshotSequence}→{snapshot.Sequence} " +
                        $"tick {_lastLatchedSnapshotTick}→{snapshot.Tick}");
                    _lastLatchedSnapshotSequence = -1;
                    _lastLatchedSnapshotTick = -1;
                }

                // Keep newest only — drop stale/equal seq (unreliable reordering / duplicates).
                if (snapshot.Sequence <= _lastLatchedSnapshotSequence)
                {
                    continue;
                }

                _lastLatchedSnapshotSequence = snapshot.Sequence;
                _lastLatchedSnapshotTick = snapshot.Tick;
                _latestClientSnapshot = snapshot;
                if (_loggedSnapshotLatch < 3 || (_loggedSnapshotLatch % 120) == 0)
                {
                    MultiplayerDebug.LogNet(
                        $"Client latch GameSnapshot #{_loggedSnapshotLatch + 1} seq={snapshot.Sequence} " +
                        $"tick={snapshot.Tick} players={snapshot.Players.Count} ropes={snapshot.Ropes.Count}");
                }

                _loggedSnapshotLatch++;
            }
        }
    }

    public void SendLocalInput(GameSession session, GameSimulation simulation, ILocalPlayerInputSource inputSource)
    {
        if (session.Role != GameSessionRole.Client || !_lobby.IsInLobby)
        {
            return;
        }

        InputFrame frame = BuildLocalInputFrame(simulation, session, inputSource);
        byte[] payload = NetworkPacketCodec.EncodeInputFrame(frame);
        ulong hostSteamId = _lobby.GetLobbyOwnerSteamId();
        if (hostSteamId != 0 && hostSteamId != _lobby.LocalSteamId)
        {
            if (_transport.SendToUser(hostSteamId, payload, snapshot: false))
            {
                MultiplayerDebug.RecordPacketSent(snapshot: false, payload.Length);
                if (_loggedInputSend < 3 || (_loggedInputSend % 120) == 0)
                {
                    MultiplayerDebug.LogNet(
                        $"Client send InputFrame #{_loggedInputSend + 1} → host={hostSteamId} " +
                        $"owner={frame.OwnerId} players={frame.PlayerInputs.Count} tick={frame.Tick}");
                }

                _loggedInputSend++;
            }
        }
    }

    public void BroadcastSnapshot(GameSession session, GameSnapshot snapshot)
    {
        if (session.Role != GameSessionRole.Host || !_lobby.IsInLobby)
        {
            return;
        }

        byte[] payload = NetworkPacketCodec.EncodeGameplaySnapshot(snapshot);
        int sent = 0;
        foreach (LobbyMemberInfo member in _lobby.GetLobbyMembers())
        {
            if (member.SteamId == 0 || member.SteamId == _lobby.LocalSteamId)
            {
                continue;
            }

            if (_transport.SendToUser(member.SteamId, payload, snapshot: true))
            {
                MultiplayerDebug.RecordPacketSent(snapshot: true, payload.Length);
                sent++;
            }
        }

        if (sent > 0)
        {
            if (_loggedSnapshotBroadcast < 3 || (_loggedSnapshotBroadcast % 120) == 0)
            {
                MultiplayerDebug.LogNet(
                    $"Host BroadcastSnapshot #{_loggedSnapshotBroadcast + 1} seq={snapshot.Sequence} " +
                    $"tick={snapshot.Tick} recipients={sent} players={snapshot.Players.Count} ropes={snapshot.Ropes.Count}");
            }

            _loggedSnapshotBroadcast++;
        }
    }

    public bool TryConsumeClientSnapshot(out GameSnapshot snapshot)
    {
        if (_latestClientSnapshot is null)
        {
            snapshot = null!;
            return false;
        }

        snapshot = _latestClientSnapshot;
        _latestClientSnapshot = null;
        return true;
    }

    public void Reset()
    {
        _latestClientSnapshot = null;
        _clientLatchedInput.Clear();
        _onlineSessionLogged = false;
        _lastActiveRemoteInputLogUtc = DateTime.MinValue;
        _lastEmptyRemoteInputLogUtc = DateTime.MinValue;
        _loggedInputRecv = 0;
        _loggedSnapshotLatch = 0;
        _loggedInputSend = 0;
        _loggedSnapshotBroadcast = 0;
        _lastLatchedSnapshotSequence = -1;
        _lastLatchedSnapshotTick = -1;
        _transport.CloseAllSessions();
        MultiplayerDebug.LogNet("GameNetworkCoordinator.Reset");
        MultiplayerDebug.ResetSessionCounters();
    }

    /// <summary>Drop client edge latch (e.g. local pause) so resume cannot fire a stale Jump/Color.</summary>
    public void ClearClientLatchedInput() => _clientLatchedInput.Clear();

    private void LogHostRemoteInputSample(ulong senderSteamId, InputFrame inputFrame)
    {
        bool anyActive = false;
        float maxAbsMove = 0f;
        bool anyJump = false;
        int networkId = -1;
        foreach (PlayerInputEntry entry in inputFrame.PlayerInputs)
        {
            networkId = entry.NetworkId;
            float absH = Math.Abs(entry.Input.HorizontalMovement);
            float absMove = Math.Max(Math.Abs(entry.Input.Move.X), Math.Abs(entry.Input.Move.Y));
            maxAbsMove = Math.Max(maxAbsMove, Math.Max(absH, absMove));
            if (entry.Input.JumpPressed)
            {
                anyJump = true;
            }

            if (absH > 0.01f || absMove > 0.01f || entry.Input.JumpPressed || entry.Input.PullRopeHeld)
            {
                anyActive = true;
            }
        }

        DateTime now = DateTime.UtcNow;
        if (anyActive)
        {
            if ((now - _lastActiveRemoteInputLogUtc).TotalSeconds < 1.0)
            {
                return;
            }

            _lastActiveRemoteInputLogUtc = now;
            MultiplayerDebug.LogNet(
                $"Host recv InputFrame ACTIVE sid={senderSteamId} owner={inputFrame.OwnerId} " +
                $"N{networkId} move={maxAbsMove:0.00} jump={anyJump} tick={inputFrame.Tick}");
            return;
        }

        if ((now - _lastEmptyRemoteInputLogUtc).TotalSeconds < 5.0)
        {
            return;
        }

        _lastEmptyRemoteInputLogUtc = now;
        MultiplayerDebug.LogNet(
            $"Host recv InputFrame EMPTY sid={senderSteamId} owner={inputFrame.OwnerId} " +
            $"players={inputFrame.PlayerInputs.Count} tick={inputFrame.Tick}");
    }

    public string GetOnlineRoleLabel(GameSession session) =>
        session.Role switch
        {
            GameSessionRole.Host => "HOST-AUTH",
            GameSessionRole.Client => "CLIENT-SNAPSHOT",
            _ => "LOCAL"
        };

    private void AccumulateClientLocalInput(GameSimulation simulation, ILocalPlayerInputSource inputSource)
    {
        foreach (Player player in simulation.Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            PlayerInputState current = inputSource.GetPlayerInput(player.NetworkId);
            if (_clientLatchedInput.TryGetValue(player.NetworkId, out PlayerInputState latched))
            {
                _clientLatchedInput[player.NetworkId] = new PlayerInputState(
                    current.HorizontalMovement,
                    latched.JumpPressed || current.JumpPressed,
                    latched.RespawnPressed || current.RespawnPressed,
                    current.FastFallHeld,
                    current.PullRopeHeld,
                    current.RequestedColor ?? latched.RequestedColor,
                    current.Move,
                    current.MenuNavigate);
            }
            else
            {
                _clientLatchedInput[player.NetworkId] = current;
            }
        }
    }

    private InputFrame BuildLocalInputFrame(
        GameSimulation simulation,
        GameSession session,
        ILocalPlayerInputSource inputSource)
    {
        AccumulateClientLocalInput(simulation, inputSource);
        SimulationTick tick = simulation.CurrentTick;
        InputFrame frame = new(tick, session.LocalOwnerId);
        foreach (Player player in simulation.Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            PlayerInputState input = _clientLatchedInput.TryGetValue(player.NetworkId, out PlayerInputState latched)
                ? latched
                : PlayerInputState.Empty;
            frame.AddPlayerInput(player.NetworkId, input);

            _clientLatchedInput[player.NetworkId] = new PlayerInputState(
                input.HorizontalMovement,
                false,
                false,
                input.FastFallHeld,
                input.PullRopeHeld,
                null,
                input.Move,
                input.MenuNavigate);
        }

        return frame;
    }
}
