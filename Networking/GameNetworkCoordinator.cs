#nullable enable
using System.Collections.Generic;

namespace ColorBlocks;

public sealed class GameNetworkCoordinator
{
    private readonly SteamGameNetworkService _transport;
    private readonly SteamLobbyService _lobby;
    private readonly Dictionary<int, PlayerInputState> _clientLatchedInput = new();
    private bool _onlineSessionLogged;

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
                MultiplayerDebug.LogNet(
                    $"Host recv InputFrame from sid={packet.SenderSteamId} owner={inputFrame.OwnerId} " +
                    $"players={inputFrame.PlayerInputs.Count} → tick={inputFrame.Tick}");
                continue;
            }

            if (!session.IsHost && packetType == NetworkPacketType.GameSnapshot && snapshot is not null)
            {
                _latestClientSnapshot = snapshot;
                MultiplayerDebug.LogNet(
                    $"Client latch GameSnapshot seq={snapshot.Sequence} tick={snapshot.Tick} " +
                    $"players={snapshot.Players.Count} ropes={snapshot.Ropes.Count}");
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
                MultiplayerDebug.LogNet(
                    $"Client send InputFrame → host={hostSteamId} owner={frame.OwnerId} " +
                    $"players={frame.PlayerInputs.Count} tick={frame.Tick}");
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
            MultiplayerDebug.LogNet(
                $"Host BroadcastSnapshot seq={snapshot.Sequence} tick={snapshot.Tick} " +
                $"recipients={sent} players={snapshot.Players.Count} ropes={snapshot.Ropes.Count}");
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
        _transport.CloseAllSessions();
        MultiplayerDebug.LogNet("GameNetworkCoordinator.Reset");
        MultiplayerDebug.ResetSessionCounters();
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
