#nullable enable
using System;
using Steamworks;

namespace ColorBlocks;

public sealed class SteamCallbackManager : IDisposable
{
    private bool _isDisposed;

    private Callback<LobbyCreated_t>? _lobbyCreated;
    private Callback<LobbyEnter_t>? _lobbyEnter;
    private Callback<LobbyChatUpdate_t>? _lobbyChatUpdate;
    private Callback<LobbyDataUpdate_t>? _lobbyDataUpdate;
    private Callback<LobbyInvite_t>? _lobbyInvite;
    private Callback<GameLobbyJoinRequested_t>? _gameLobbyJoinRequested;
    private Callback<LobbyMatchList_t>? _lobbyMatchList;
    private Callback<GameRichPresenceJoinRequested_t>? _gameRichPresenceJoinRequested;
    private Callback<LobbyChatMsg_t>? _lobbyChatMsg;

    public event Action<LobbyCreated_t>? LobbyCreated;
    public event Action<LobbyEnter_t>? LobbyEnter;
    public event Action<LobbyChatUpdate_t>? LobbyChatUpdate;
    public event Action<LobbyDataUpdate_t>? LobbyDataUpdate;
    public event Action<LobbyInvite_t>? LobbyInvite;
    public event Action<GameLobbyJoinRequested_t>? GameLobbyJoinRequested;
    public event Action<LobbyMatchList_t>? LobbyMatchList;
    public event Action<GameRichPresenceJoinRequested_t>? GameRichPresenceJoinRequested;
    public event Action<LobbyChatMsg_t>? LobbyChatMsg;

    public void Register()
    {
        if (_lobbyCreated is not null)
        {
            return;
        }

        _lobbyCreated = Callback<LobbyCreated_t>.Create(data => LobbyCreated?.Invoke(data));
        _lobbyEnter = Callback<LobbyEnter_t>.Create(data => LobbyEnter?.Invoke(data));
        _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(data => LobbyChatUpdate?.Invoke(data));
        _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(data => LobbyDataUpdate?.Invoke(data));
        _lobbyInvite = Callback<LobbyInvite_t>.Create(data => LobbyInvite?.Invoke(data));
        _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(data => GameLobbyJoinRequested?.Invoke(data));
        _lobbyMatchList = Callback<LobbyMatchList_t>.Create(data => LobbyMatchList?.Invoke(data));
        _gameRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(data => GameRichPresenceJoinRequested?.Invoke(data));
        _lobbyChatMsg = Callback<LobbyChatMsg_t>.Create(data => LobbyChatMsg?.Invoke(data));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lobbyCreated?.Dispose();
        _lobbyEnter?.Dispose();
        _lobbyChatUpdate?.Dispose();
        _lobbyDataUpdate?.Dispose();
        _lobbyInvite?.Dispose();
        _gameLobbyJoinRequested?.Dispose();
        _lobbyMatchList?.Dispose();
        _gameRichPresenceJoinRequested?.Dispose();
        _lobbyChatMsg?.Dispose();
    }
}
