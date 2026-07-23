#nullable enable
using System;

namespace ColorBlocks;

/// <summary>
/// Single lobby-wide listener for host START so guests join from any scene
/// (Party, Level Select, Editor, sandbox, Options) without missing the packet.
/// </summary>
public sealed class LevelStartRouter
{
    private readonly ColorBlocksGame _game;
    private bool _bound;
    private bool _applying;

    public LevelStartRouter(ColorBlocksGame game)
    {
        _game = game;
    }

    public (string Title, string Message)? PendingStartAlert { get; private set; }

    public bool TryConsumePendingStartAlert(out string title, out string message)
    {
        if (PendingStartAlert is not { } alert)
        {
            title = string.Empty;
            message = string.Empty;
            return false;
        }

        title = alert.Title;
        message = alert.Message;
        PendingStartAlert = null;
        return true;
    }

    public void Bind()
    {
        if (_bound)
        {
            return;
        }

        _game.SteamLobby.LevelStartReceived += OnLevelStartReceived;
        _bound = true;
        MultiplayerDebug.LogSim("LevelStartRouter bound");
    }

    public void Unbind()
    {
        if (!_bound)
        {
            return;
        }

        _game.SteamLobby.LevelStartReceived -= OnLevelStartReceived;
        _bound = false;
    }

    /// <summary>
    /// Consume a START that arrived before the router/scene was ready (pending queue).
    /// </summary>
    public void TryApplyPending()
    {
        if (!_bound || _applying)
        {
            return;
        }

        if (_game.SteamLobby.IsInLobby && _game.Party.IsLeader)
        {
            _game.SteamLobby.ClearPendingLevelStart();
            return;
        }

        if (!_game.SteamLobby.TryConsumePendingLevelStart(out PartyStartMessage message))
        {
            return;
        }

        ApplyLevelStart(message);
    }

    private void OnLevelStartReceived(PartyStartMessage message)
    {
        if (_applying)
        {
            return;
        }

        if (_game.SteamLobby.IsInLobby && _game.Party.IsLeader)
        {
            _game.SteamLobby.ClearPendingLevelStart();
            MultiplayerDebug.LogSim("START ignored — local is party leader (already starting)");
            return;
        }

        _game.SteamLobby.ClearPendingLevelStart();
        ApplyLevelStart(message);
    }

    private void ApplyLevelStart(PartyStartMessage message)
    {
        if (_applying)
        {
            return;
        }

        MultiplayerDebug.LogSim(
            $"CLIENT START RECEIVED level={message.LevelId} scene={_game.CurrentScene.GetType().Name} " +
            $"partyMembers={_game.Party.Members.Count} lobbyMembers={_game.SteamLobby.GetLobbyMemberCount()}");
        foreach (PartyMember member in _game.Party.Members)
        {
            MultiplayerDebug.LogSim(
                $"  start-roster '{member.DisplayName}' {(member.IsLocallyOwned ? "LOCAL" : "REMOTE")} " +
                $"type={member.MemberType} steam={member.OwningSteamId}");
        }

        if (!MultiplayerStartGate.ValidateClientStart(_game.SteamLobby, message, out string title, out string error))
        {
            PendingStartAlert = (title, error);
            MultiplayerDebug.LogSim($"START rejected — {title}: {error}");

            // Surface the alert on Party if guest was in editor/sandbox/options.
            if (_game.CurrentScene is not PartyScene and not LevelSelectScene)
            {
                _game.ChangeScene(new PartyScene(_game));
            }

            return;
        }

        _applying = true;
        try
        {
            Level level = LevelLibrary.LoadLevel(message.LevelId);
            _game.ChangeScene(new GameScene(
                _game,
                message.LevelId,
                message.RopeMode,
                message.LavaRiseEnabled,
                playerCollisionEnabled: level.PlayerCollision));
        }
        finally
        {
            _applying = false;
        }
    }
}
