#nullable enable
using System.Collections.Generic;

namespace ColorBlocks;

public sealed class GameNetworkCoordinator
{
    private readonly SteamGameNetworkService _transport;
    private readonly SteamLobbyService _lobby;
    private readonly Dictionary<int, PlayerInputState> _clientLatchedInput = new();

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

        foreach (ReceivedNetworkPacket packet in _transport.ReceiveAll())
        {
            if (!NetworkPacketCodec.TryDecode(
                    packet.Payload,
                    out NetworkPacketType packetType,
                    out InputFrame? inputFrame,
                    out GameSnapshot? snapshot))
            {
                continue;
            }

            if (session.IsHost && packetType == NetworkPacketType.InputFrame && inputFrame is not null)
            {
                inputFrame.Tick = simulation.CurrentTick.Value;
                simulation.InputBuffer.StoreFrame(inputFrame);
                continue;
            }

            if (!session.IsHost && packetType == NetworkPacketType.GameSnapshot && snapshot is not null)
            {
                _latestClientSnapshot = snapshot;
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
            _transport.SendToUser(hostSteamId, payload, snapshot: false);
        }
    }

    public void BroadcastSnapshot(GameSession session, GameSnapshot snapshot)
    {
        if (session.Role != GameSessionRole.Host || !_lobby.IsInLobby)
        {
            return;
        }

        byte[] payload = NetworkPacketCodec.EncodeGameplaySnapshot(snapshot);
        foreach (LobbyMemberInfo member in _lobby.GetLobbyMembers())
        {
            if (member.SteamId == 0 || member.SteamId == _lobby.LocalSteamId)
            {
                continue;
            }

            _transport.SendToUser(member.SteamId, payload, snapshot: true);
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
        _transport.CloseAllSessions();
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
