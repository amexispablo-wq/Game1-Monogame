using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

public sealed class GameScene : IScene
{
    private const float MaxSceneFrameTime = 0.25f;

    private readonly ColorBlocksGame _game;
    private readonly string _levelId;
    private readonly RopeGameplayMode _ropeGameplayMode;
    private readonly bool _lavaRiseEnabled;
    private readonly bool _playerCollisionEnabled;
    private readonly GameSession _session;
    private readonly Level _level;
    private readonly PlayerManager _playerManager;
    private readonly GameSimulation _simulation;
    private readonly Camera _camera;
    private readonly PauseMenuOverlay _pauseMenu = new();
    private OptionsScene? _pauseOptions;
    private bool _debugDraw;
    private readonly DeveloperTuningPanel _tuningPanel = new();
    private bool _completionUiActive;
    private float _finalTime;
    private bool _newRecord;
    private float _completionUiElapsed;
    private Rectangle _completionReplayBounds;
    private Rectangle _completionNextBounds;
    private Rectangle _completionMenuBounds;
    private readonly UIFocusManager _completionFocus = new();
    private readonly List<FocusableGridCell> _completionOptionFocusables = new();
    private AlertPopup? _alertPopup;
    private Rectangle _deathRespawnStartBounds;
    private Rectangle _deathCheckpointBounds;
    private Rectangle _deathQuitBounds;
    private readonly UIFocusManager _deathFocus = new() { Name = "Death Menu" };
    private readonly List<FocusableGridCell> _deathOptionFocusables = new();
    private bool _deathMenuFocusInitialized;
    private Popup? _confirmPopup;
    private ConfirmActionKind _pendingConfirm;
    private bool _disconnectPending;
    private float _disconnectTimer;
    private const float DisconnectReturnDelay = 2.5f;
    private const float ClientSnapshotStallSeconds = 2f;
    private readonly ReplayRecorder _replayRecorder = new();
    private readonly GhostPlayer? _ghostPlayer;
    private readonly GhostPlayer? _worldRecordGhost;
    private readonly GhostMode _ghostMode;
    private readonly bool _editorTestMode;
    private bool _exited;
    private bool _savedNewRecordReplay;
    private bool _photoMode;
    private bool _observedGameplayActive;
    private bool _clientReceivedSnapshot;
    private float _clientSecondsWithoutSnapshot;

    public bool IsPhotoModeActive => _photoMode;

    public string LevelMusicId => _level.MusicId;

    private enum ConfirmActionKind
    {
        None,
        RestartLevel,
        QuitToMenu
    }

    public GameScene(
        ColorBlocksGame game,
        string levelId = "level_1",
        RopeGameplayMode ropeGameplayMode = RopeGameplayMode.ColoredPhysics,
        bool lavaRiseEnabled = false,
        GhostMode ghostMode = GhostMode.None,
        bool playerCollisionEnabled = false,
        bool editorTestMode = false,
        Level? levelOverride = null)
    {
        _game = game;
        _levelId = levelId;
        _ropeGameplayMode = ropeGameplayMode;
        _lavaRiseEnabled = lavaRiseEnabled;
        _playerCollisionEnabled = playerCollisionEnabled;
        _ghostMode = editorTestMode ? GhostMode.None : ghostMode;
        _editorTestMode = editorTestMode;
        int localOwnerId = _game.SteamLobby.IsAvailable
            ? SteamOwnerId.FromSteamId(_game.SteamLobby.LocalSteamId)
            : NetworkOwners.HostOwnerId;
        MultiplayerDebug.ResetSessionCounters();
        _session = !editorTestMode && _game.SteamLobby.IsInLobby
            ? GameSession.CreateOnline(
                _game.Party.IsLeader ? GameSessionRole.Host : GameSessionRole.Client,
                levelId,
                ropeGameplayMode,
                localOwnerId,
                _game.Steam.Username)
            : GameSession.CreateLocalTest(levelId, ropeGameplayMode);
        MultiplayerDebug.LogSim(
            $"GameScene start level={levelId} role={_session.Role} localOwner={localOwnerId} " +
            $"inLobby={_game.SteamLobby.IsInLobby} partyMembers={_game.Party.Members.Count} " +
            $"lobbyMembers={_game.SteamLobby.GetLobbyMemberCount()}");
        _session.LavaRiseEnabled = lavaRiseEnabled;
        _session.PlayerCollisionEnabled = playerCollisionEnabled;
        MultiplayerDebug.LogSim($"LevelLoadStarted level={levelId} override={levelOverride is not null}");
        _level = levelOverride ?? LevelLibrary.LoadLevel(levelId);
        MultiplayerDebug.LogSim($"LevelLoaded level={levelId} name='{_level.Name}'");
        _playerManager = new PlayerManager(_session, _level);
        _game.ActiveTuningPanel = _tuningPanel;
        _game.Party.ApplyPreferredInputForPrimaryLocalMember(_game.Input);
        if (!_editorTestMode)
        {
            _game.Party.LockAssignments();
        }

        if (_editorTestMode)
        {
            _playerManager.SpawnSoloTest(_game.Party.Members, _game.Input);
        }
        else
        {
            _playerManager.SpawnFromParty(_game.Party.Members, _game.Input, _game.SteamLobby);
        }

        _simulation = new GameSimulation(_session, _level, _playerManager, lavaRiseEnabled, playerCollisionEnabled)
        {
            RecordProgress = !_editorTestMode
        };

        MultiplayerDebug.ValidateGameplayStart(_game.SteamLobby, _game.Party, _session, _simulation);
        MultiplayerDebug.LogSim(
            $"GameplayInitialized level={levelId} role={_session.Role} " +
            $"players={_simulation.Players.Count} ropes={_simulation.Ropes.Count}");
        MultiplayerDebug.DumpEntityState(_session, _simulation);
        _camera = new Camera(GetPlayersCenter());
        if (!_editorTestMode)
        {
            _replayRecorder.StartRecording(
                _levelId,
                _ropeGameplayMode,
                _lavaRiseEnabled,
                _session.Settings.SimulationTicksPerSecond,
                _simulation.LavaRiseSpeed,
                _level.Lava?.SurfaceY ?? 0f,
                _level.ToData(),
                ReplayRecordingMode.FullSession);
            ReplayDiagnostics.ActiveRecorder = _replayRecorder;
        }

        _simulation.FixedTickCompleted += OnSimulationFixedTick;

        if (_ghostMode.IncludesPersonalBest())
        {
            _ghostPlayer = new GhostPlayer();
            _ghostPlayer.TryLoadBestRun(_levelId);
        }

        if (_ghostMode.IncludesWorldRecord() && SteamGhostService.SupportsWorldRecordGhost(_levelId))
        {
            _worldRecordGhost = new GhostPlayer { BorderColor = new Color(255, 210, 90) };
            if (_game.SteamGhosts.TryLoadWorldRecordGhost(_levelId, _playerManager.Players.Count, out ReplayFile cachedWorldRecord))
            {
                _worldRecordGhost.TryLoadReplayFile(cachedWorldRecord);
            }
        }

        // Official/Workshop levels always refresh the cached World Record ghost in the
        // background for this run's player count; if a newer ghost lands while playing,
        // it is picked up live.
        if (!_editorTestMode && SteamGhostService.SupportsWorldRecordGhost(_levelId))
        {
            int wrPlayerCount = _playerManager.Players.Count;
            _game.SteamGhosts.EnsureWorldRecordGhost(_levelId, wrPlayerCount, ready =>
            {
                if (!ready || _exited || _worldRecordGhost is null)
                {
                    return;
                }

                if (_game.SteamGhosts.TryLoadWorldRecordGhost(_levelId, wrPlayerCount, out ReplayFile downloaded))
                {
                    _worldRecordGhost.TryLoadReplayFile(downloaded);
                }
            });
        }

        if (!_editorTestMode)
        {
            _game.SteamLobby.MemberLeft += OnLobbyMemberLeft;
            _game.SteamLobby.LevelLeaveReceived += OnLevelLeaveReceived;

            if (_session.Role == GameSessionRole.Host && _game.SteamLobby.IsInLobby)
            {
                _game.SteamLobby.SetGameplayActive(true);
            }

            if (_session.Role == GameSessionRole.Client && _game.SteamLobby.IsGameplayActive)
            {
                _observedGameplayActive = true;
            }
        }
    }

    public void OnExit()
    {
        _exited = true;
        _game.ActiveTuningPanel = null;
        _simulation.FixedTickCompleted -= OnSimulationFixedTick;
        ReplayDiagnostics.ActiveRecorder = null;
        if (!_editorTestMode)
        {
            if (_session.Role == GameSessionRole.Host && _game.SteamLobby.IsInLobby)
            {
                _game.SteamLobby.SetGameplayActive(false);
            }

            _game.SteamLobby.MemberLeft -= OnLobbyMemberLeft;
            _game.SteamLobby.LevelLeaveReceived -= OnLevelLeaveReceived;

            _game.Party.UnlockAssignments();
            _replayRecorder.StopRecording();
            FinalizeSessionRecording();
        }

        _game.Input.GameplayInputBlocked = false;
        _game.Input.ClearGameplayBindings();
        _photoMode = false;
        _game.GameNetwork.Reset();
        GameAudio.StopAllLoops();
        _game.Music.Stop();
    }

    private void FinalizeSessionRecording()
    {
        ReplayData? session = _replayRecorder.ExportReplay();
        if (session is null)
        {
            return;
        }

        HighlightManager.ProcessSession(session);

        if (_savedNewRecordReplay || (_simulation.NewRecord && _simulation.IsLevelComplete))
        {
            ReplayFile replayFile = ReplayFileSerializer.CreateFromSession(
                session,
                _levelId,
                _simulation.FinalTime,
                _playerManager.Players.Count);
            ReplayStorage.SaveBestReplay(replayFile);
            UploadRecordToSteamLeaderboard();
        }
    }

    /// <summary>
    /// Publishes a new official record to the Steam leaderboard for Official/Workshop
    /// levels. Host-only in online sessions so a party run produces exactly one entry
    /// (carrying every participant's Steam id). Local levels are never uploaded.
    /// Runs asynchronously via Steam callbacks; never blocks gameplay.
    /// </summary>
    private void UploadRecordToSteamLeaderboard()
    {
        if (!SteamLeaderboardService.SupportsLeaderboards(_levelId)
            || !_game.SteamLeaderboards.IsAvailable
            || _session.Role == GameSessionRole.Client
            || _simulation.ForceUnofficial)
        {
            if (_simulation.ForceUnofficial)
            {
                MultiplayerDebug.LogSim("Skip Steam leaderboard upload — run ForceUnofficial");
            }

            return;
        }

        int levelVersion = LevelLibrary.GetLevel(_levelId)?.Version ?? 1;
        float finalTime = _simulation.FinalTime;
        int playerCount = _playerManager.Players.Count;

        var steamIds = new List<ulong>();
        foreach (PartyMember member in _game.Party.Members)
        {
            if (member.OwningSteamId != 0 && !steamIds.Contains(member.OwningSteamId))
            {
                steamIds.Add(member.OwningSteamId);
            }
        }

        string levelId = _levelId;
        string replayPath = ReplayStorage.GetBestReplayPath(levelId);
        ColorBlocksGame game = _game;
        int scoreCentiseconds = (int)MathF.Round(BestTimeStorage.RoundToCentiseconds(finalTime) * 100f);

        game.SteamReplays.ShareReplayFile(
            replayPath,
            SteamReplayService.GetRemoteReplayName(levelId, playerCount, scoreCentiseconds),
            ugcHandle =>
            {
                game.SteamLeaderboards.UploadRecord(
                    new SteamLeaderboardRecord
                    {
                        LevelId = levelId,
                        LevelVersion = levelVersion,
                        TimeSeconds = finalTime,
                        PlayerCount = playerCount,
                        SteamIds = steamIds,
                        ReplayUgcHandle = ugcHandle
                    },
                    success =>
                    {
                        if (success)
                        {
                            // Drop stale WR cache for this player-count board, then pull newest.
                            SteamGhostService.InvalidateWorldRecordGhost(levelId, playerCount);
                            game.SteamGhosts.EnsureWorldRecordGhost(levelId, playerCount);
                        }
                    });
            });
    }

    private void OnLobbyMemberLeft(ulong steamId)
    {
        if (_exited || _editorTestMode || !_game.SteamLobby.IsInLobby)
        {
            return;
        }

        if (steamId == _game.SteamLobby.LocalSteamId)
        {
            return;
        }

        // Mid-run disconnect: despawn peer, continue unofficial — do not kick host.
        if (_session.Role == GameSessionRole.Host)
        {
            HandlePeerLeftSimulation(steamId, "MemberLeft");
            return;
        }

        // Client: if host left lobby, end session.
        if (steamId == _game.SteamLobby.GetLobbyOwnerSteamId()
            || steamId == _game.Party.Leader?.OwningSteamId)
        {
            BeginSessionEndDisconnect($"MemberLeft host/leader sid={steamId}");
        }
    }

    private void OnLevelLeaveReceived(ulong steamId)
    {
        if (_exited || _editorTestMode)
        {
            return;
        }

        // Echo on the client that just broadcast — already leaving.
        if (_session.Role == GameSessionRole.Client)
        {
            return;
        }

        HandlePeerLeftSimulation(steamId, "LevelLeaveReceived");
    }

    private void HandlePeerLeftSimulation(ulong steamId, string reason)
    {
        int removed = _simulation.RemovePeerFromSimulation(steamId);
        MultiplayerDebug.LogSim(
            $"HandlePeerLeftSimulation ({reason}) steam={steamId} removed={removed} " +
            $"players={_simulation.Players.Count} unofficial={_simulation.ForceUnofficial}");

        if (_simulation.Players.Count == 0)
        {
            BeginSessionEndDisconnect($"{reason} — no players left");
        }
    }

    private void NotifyHostLeftLevelIfClient()
    {
        if (_editorTestMode || _session.Role != GameSessionRole.Client || !_game.SteamLobby.IsInLobby)
        {
            return;
        }

        _game.SteamLobby.BroadcastLeaveLevel();
    }

    private void BeginSessionEndDisconnect(string reason)
    {
        if (_disconnectPending || _editorTestMode)
        {
            return;
        }

        MultiplayerDebug.LogSim($"Session end → return to Party ({reason})");
        _disconnectPending = true;
        _disconnectTimer = DisconnectReturnDelay;
    }

    private void CancelSessionEndDisconnect(string reason)
    {
        if (!_disconnectPending)
        {
            return;
        }

        MultiplayerDebug.LogSim($"Session end cancelled ({reason})");
        _disconnectPending = false;
        _disconnectTimer = 0f;
        _clientSecondsWithoutSnapshot = 0f;
    }

    private void CheckClientSessionEnd(float dt)
    {
        if (_session.Role != GameSessionRole.Client || _editorTestMode)
        {
            return;
        }

        if (_disconnectPending)
        {
            // Host started another level while we were still exiting — stay for START handler.
            if (_game.SteamLobby.IsGameplayActive)
            {
                CancelSessionEndDisconnect("lobby gameplay=1 during disconnect");
                _observedGameplayActive = true;
            }

            return;
        }

        if (_game.SteamLobby.IsGameplayActive)
        {
            _observedGameplayActive = true;
            // While host marks gameplay active, never treat snapshot silence as disconnect
            // (unreliable drops / backlog must not flash "Player disconnected").
            _clientSecondsWithoutSnapshot = 0f;
            return;
        }

        if (_observedGameplayActive)
        {
            BeginSessionEndDisconnect("lobby gameplay=0 (host left level)");
            return;
        }

        // Fallback only when gameplay already idle: host left without clearing flag edge-case.
        if (_clientReceivedSnapshot)
        {
            _clientSecondsWithoutSnapshot += dt;
            if (_clientSecondsWithoutSnapshot >= ClientSnapshotStallSeconds)
            {
                BeginSessionEndDisconnect(
                    $"snapshot stall {_clientSecondsWithoutSnapshot:0.0}s after host left (gameplay idle)");
            }
        }
    }

    private IReadOnlyList<Player> Players => _simulation.Players;
    private IReadOnlyList<Rope> Ropes => _simulation.Ropes;

    private void UpdateGameplayAudio()
    {
        bool pulling = false;
        foreach (Rope rope in Ropes)
        {
            if (rope.IsPulling)
            {
                pulling = true;
                break;
            }
        }

        GameAudio.SetPullRopeLoop(pulling);

        if (!_simulation.LavaActive)
        {
            GameAudio.UpdateLavaProximity(float.MaxValue);
            return;
        }

        float nearest = float.MaxValue;
        foreach (Player player in Players)
        {
            float distance = SfxManager.DistanceToLavaSurface(player.Bounds.Bottom, _simulation.LavaSurfaceY);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        GameAudio.UpdateLavaProximity(nearest);
    }

    public void Update(GameTime gameTime)
    {
        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxSceneFrameTime);
        Viewport viewport = _game.Viewport;

        if (!_editorTestMode && _game.Input.ReplayForceSavePressed)
        {
            ReplayData? forced = _replayRecorder.ExportReplay();
            if (forced is not null)
            {
                ReplayFile replayFile = ReplayFileSerializer.CreateFromSession(
                    forced,
                    _levelId,
                    _simulation.ElapsedTime,
                    _playerManager.Players.Count);
                ReplayStorage.SaveBestReplay(replayFile);
            }
        }

        if (_disconnectPending)
        {
            _disconnectTimer -= dt;
            if (_disconnectTimer <= 0f)
            {
                _game.ChangeScene(new PartyScene(_game));
                return;
            }
        }

        if (_alertPopup is not null)
        {
            _game.Input.GameplayInputBlocked = true;
            _alertPopup.Update(gameTime, _game.Input, viewport.Width, viewport.Height);
            if (_alertPopup.IsDismissed)
            {
                _alertPopup = null;
            }

            return;
        }

        if (_confirmPopup is not null)
        {
            _game.Input.GameplayInputBlocked = true;
            _confirmPopup.Update(gameTime, _game.Input, viewport.Width, viewport.Height);
            if (_confirmPopup.Result == PopupResult.Confirmed)
            {
                ConfirmActionKind action = _pendingConfirm;
                _confirmPopup = null;
                _pendingConfirm = ConfirmActionKind.None;
                ExecuteConfirmedAction(action);
            }
            else if (_confirmPopup.Result == PopupResult.Cancelled)
            {
                _confirmPopup = null;
                _pendingConfirm = ConfirmActionKind.None;
                if (_pauseMenu.IsOpen)
                {
                    // stay paused
                }
            }

            return;
        }

        if (_pauseOptions is not null)
        {
            _game.Input.GameplayInputBlocked = true;
            GameAudio.SetPullRopeLoop(false);
            _pauseOptions.Update(gameTime);
            return;
        }

        if (_pauseMenu.IsOpen)
        {
            _game.Input.GameplayInputBlocked = true;
            GameAudio.SetPullRopeLoop(false);
            PauseMenuChoice? choice = _pauseMenu.Update(gameTime, _game.Input, viewport);
            if (choice.HasValue)
            {
                HandlePauseMenuChoice(choice.Value);
            }

            return;
        }

        if (_photoMode)
        {
            _game.Input.GameplayInputBlocked = true;
            if (_game.Input.PhotoModeTogglePressed)
            {
                _photoMode = false;
                _game.Input.GameplayInputBlocked = false;
            }

            return;
        }

        if (_simulation.IsPlayerDead)
        {
            _game.Input.GameplayInputBlocked = true;
            GameAudio.SetPullRopeLoop(false);
            if (!_deathMenuFocusInitialized)
            {
                _deathFocus.ResetFocus();
                if (_game.Input.IsAnyGamepadConnected())
                {
                    _game.Input.Navigation.PreferGamepad();
                }

                _deathMenuFocusInitialized = true;
            }

            UpdateDeathUi(gameTime);
            UpdateCamera(gameTime);
            return;
        }

        _deathMenuFocusInitialized = false;

        if (_simulation.IsLevelComplete)
        {
            _game.Input.GameplayInputBlocked = true;
            GameAudio.SetPullRopeLoop(false);
            BeginCompletionUi();
            _completionUiElapsed += dt;
            UpdateCompletionUi(gameTime);
            UpdateCamera(gameTime);
            return;
        }

        _game.Input.GameplayInputBlocked = false;

        if (_game.Input.PhotoModeTogglePressed)
        {
            _photoMode = true;
            _game.Input.GameplayInputBlocked = true;
            return;
        }

        if (_game.Input.GameplayPausePressed)
        {
            _simulation.SetPaused(true);
            _game.GameNetwork.ClearClientLatchedInput();
            _pauseMenu.Open();
            return;
        }

        if (_game.Input.DebugTogglePressed)
        {
            _debugDraw = !_debugDraw;
        }

        if (_game.Input.TuningPanelTogglePressed)
        {
            _tuningPanel.Toggle();
        }

        _tuningPanel.Update(gameTime, _game.Input, viewport, _game.Party);

        GameNetworkCoordinator network = _game.GameNetwork;
        network.PumpIncoming(_session, _simulation);
        MultiplayerDebug.UpdateRates(
            _simulation.InputBuffer.FrameCount,
            network.IsOnlineSession(_session));
        if (network.IsOnlineSession(_session))
        {
            MultiplayerDebug.LogSimulationRunningOnce(_session, _simulation);
            MultiplayerDebug.LogTickPeriodic(_session, _simulation);
            MultiplayerDebug.CheckClientStall(_session, _simulation);
            CheckClientSessionEnd(dt);
        }

        if (_game.Party.TryHotSwapLocalInputFromActivity(_game.Input))
        {
            _playerManager.SyncInputDevicesFromParty(_game.Party.Members, _game.Input);
        }

        if (_session.Role == GameSessionRole.Client)
        {
            network.SendLocalInput(_session, _simulation, _game.Input);
            if (network.TryConsumeClientSnapshot(out GameSnapshot snapshot))
            {
                _clientReceivedSnapshot = true;
                _clientSecondsWithoutSnapshot = 0f;
                _simulation.ApplySnapshot(snapshot);
                if (!_editorTestMode)
                {
                    _replayRecorder.RecordFrame(_simulation, _camera);
                }

                _ghostPlayer?.SyncToGameplayTick(_simulation.CurrentTick.Value);
                _worldRecordGhost?.SyncToGameplayTick(_simulation.CurrentTick.Value);
                UpdateGameplayAudio();
            }

            BeginCompletionUi();
            UpdateCamera(gameTime);
            return;
        }

        _simulation.Advance(dt, _game.Input);
        _ghostPlayer?.SyncToGameplayTick(_simulation.CurrentTick.Value);
        _worldRecordGhost?.SyncToGameplayTick(_simulation.CurrentTick.Value);
        UpdateGameplayAudio();
        BeginCompletionUi();
        if (_simulation.IsLevelComplete && _simulation.NewRecord)
        {
            _savedNewRecordReplay = true;
        }

        // Snapshots broadcast from OnSimulationFixedTick (once per sim tick) — not every render frame.

        UpdateCamera(gameTime);
    }

    private void HandlePauseMenuChoice(PauseMenuChoice choice)
    {
        switch (choice)
        {
            case PauseMenuChoice.Resume:
                _simulation.SetPaused(false);
                _pauseMenu.Close();
                break;
            case PauseMenuChoice.Respawn:
                _simulation.SetPaused(false);
                _pauseMenu.Close();
                _simulation.PauseMenuRespawn();
                break;
            case PauseMenuChoice.RestartLevel:
                _confirmPopup = new Popup(
                    "Restart Level",
                    "Restart from the beginning?\nProgress since last checkpoint will be lost.",
                    "Restart",
                    "Cancel");
                _pendingConfirm = ConfirmActionKind.RestartLevel;
                break;
            case PauseMenuChoice.Options:
                OpenPauseOptions();
                break;
            case PauseMenuChoice.BackToMenu:
                _simulation.SetPaused(false);
                _pauseMenu.Close();
                ReturnFromGameplay();
                break;
        }
    }

    private void OpenPauseOptions()
    {
        _pauseOptions = new OptionsScene(_game, ClosePauseOptions);
    }

    private void ClosePauseOptions()
    {
        _pauseOptions = null;
        // Stay paused; pause menu remains open underneath.
    }

    private void ExecuteConfirmedAction(ConfirmActionKind action)
    {
        switch (action)
        {
            case ConfirmActionKind.RestartLevel:
                if (_simulation.IsPlayerDead)
                {
                    _simulation.RespawnFromStart();
                }
                else
                {
                    _simulation.SetPaused(false);
                    _pauseMenu.Close();
                    _game.Input.GameplayInputBlocked = false;
                    ResetGameplaySession();
                }
                break;
            case ConfirmActionKind.QuitToMenu:
                ReturnFromGameplay();
                break;
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        Viewport viewport = _game.Viewport;

        GameplayWorldRenderer.Draw(
            spriteBatch,
            _game.Pixel,
            viewport,
            _camera,
            _level,
            Ropes,
            Players,
            _simulation.LavaActive,
            _simulation.LavaSurfaceY,
            _simulation.ElapsedTime,
            gameTime,
            _debugDraw,
            _ghostPlayer,
            drawPlayerIndicators: !_photoMode,
            worldRecordGhost: _worldRecordGhost);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if (!_photoMode)
        {
            DrawTimer(spriteBatch, _game.Pixel, viewport);
        }

        if (_debugDraw && !_photoMode)
        {
            DrawDebugHud(spriteBatch, _game.Pixel, viewport);
        }

        if (_simulation.IsLevelComplete)
        {
            DrawCompletionUi(spriteBatch, _game.Pixel, viewport, gameTime);
        }

        if (_simulation.IsPlayerDead)
        {
            DrawDeathUi(spriteBatch, _game.Pixel, viewport, gameTime);
        }

        if (_pauseMenu.IsOpen && _pauseOptions is null)
        {
            _pauseMenu.Draw(spriteBatch, _game.Pixel, gameTime, viewport, _game.Input);
        }

        if (_confirmPopup is not null)
        {
            _confirmPopup.Draw(gameTime, spriteBatch, _game.Pixel);
        }

        if (_alertPopup is not null)
        {
            _alertPopup.Draw(spriteBatch, _game.Pixel, viewport.Width, viewport.Height, gameTime, _game.Input);
        }

        if (_disconnectPending)
        {
            DrawDisconnectOverlay(spriteBatch, _game.Pixel, viewport);
        }

        spriteBatch.End();

        if (_pauseOptions is not null)
        {
            _pauseOptions.Draw(gameTime, spriteBatch);
        }
    }

    private void DrawDisconnectOverlay(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 170));
        DrawCenteredText(spriteBatch, pixel, "Player disconnected.", viewport.Width / 2, viewport.Height / 2 - 20, 3, Color.White);
        DrawCenteredText(spriteBatch, pixel, "Returning to Party...", viewport.Width / 2, viewport.Height / 2 + 20, 2, new Color(200, 210, 225));
    }

    private void OnSimulationFixedTick()
    {
        if (_editorTestMode)
        {
            return;
        }

        _replayRecorder.RecordFrame(_simulation, _camera);

        // Host: one snapshot per fixed tick (≤60 Hz). Per-frame broadcast flooded Steam (~1000/s).
        if (_session.Role == GameSessionRole.Host)
        {
            _game.GameNetwork.BroadcastSnapshot(_session, _simulation.LastSnapshot);
        }
    }

    private Vector2 GetPlayersCenter()
    {
        return GameplayCameraHelper.GetPlayersCenter(Players, _level.PlayerStart);
    }

    private float GetTargetCameraZoom(Viewport viewport)
    {
        return GameplayCameraHelper.GetTargetCameraZoom(Players, Ropes, viewport);
    }

    private void UpdateCamera(GameTime gameTime)
    {
        GameplayCameraHelper.UpdateSmoothFollow(
            _camera,
            gameTime,
            GetPlayersCenter(),
            GetTargetCameraZoom(_game.Viewport));
    }

    private void BeginCompletionUi()
    {
        if (!_simulation.IsLevelComplete || _completionUiActive)
        {
            return;
        }

        _completionUiActive = true;
        _finalTime = _simulation.FinalTime;
        _newRecord = _simulation.NewRecord;
        _completionUiElapsed = 0f;
    }

    private void UpdateCompletionUi(GameTime gameTime)
    {
        LayoutCompletionUi();

        bool showNext = CanGoToNextLevel();
        _completionOptionFocusables.Clear();
        _completionFocus.Clear();
        _completionOptionFocusables.Add(new FocusableGridCell(_completionReplayBounds, () => true));
        if (showNext)
        {
            _completionOptionFocusables.Add(new FocusableGridCell(_completionNextBounds, () => true));
        }

        _completionOptionFocusables.Add(new FocusableGridCell(_completionMenuBounds, () => true));

        int replayIndex = _completionFocus.Add(_completionOptionFocusables[0], "Replay");
        int nextIndex = -1;
        int focusSlot = 1;
        if (showNext)
        {
            nextIndex = _completionFocus.Add(_completionOptionFocusables[focusSlot], "NextLevel");
            focusSlot++;
        }

        int menuIndex = _completionFocus.Add(_completionOptionFocusables[focusSlot], "BackToMenu");
        if (showNext)
        {
            _completionFocus.Navigation.LinkHorizontal(replayIndex, nextIndex);
            _completionFocus.Navigation.LinkHorizontal(nextIndex, menuIndex);
        }
        else
        {
            _completionFocus.Navigation.LinkHorizontal(replayIndex, menuIndex);
        }

        _completionFocus.FinalizeFocus(showNext ? "NextLevel" : "Replay");
        _completionFocus.Update(gameTime, _game.Input);

        InputManager input = _game.Input;
        if (input.UiPointerPressed)
        {
            if (_completionReplayBounds.Contains(input.UiPointerPosition))
            {
                ReplayLevel();
                return;
            }

            if (showNext && _completionNextBounds.Contains(input.UiPointerPosition))
            {
                GoToNextLevel();
                return;
            }

            if (_completionMenuBounds.Contains(input.UiPointerPosition))
            {
                ReturnToLevelSelect();
                return;
            }
        }

        for (int i = 0; i < _completionOptionFocusables.Count; i++)
        {
            if (!_completionOptionFocusables[i].WasActivated)
            {
                continue;
            }

            ActivateCompletionOption(i, showNext);
            return;
        }

        for (int i = 0; i < _completionOptionFocusables.Count; i++)
        {
            bool confirmed = _completionFocus.Focused == _completionOptionFocusables[i]
                && ((input.Navigation.IsKeyboardActive && input.KeyboardMenuConfirmPressed)
                    || (input.Navigation.IsGamepadActive && input.GamepadMenuConfirmPressed));
            if (!confirmed)
            {
                continue;
            }

            ActivateCompletionOption(i, showNext);
            return;
        }

        if (input.ExitPressed || input.MenuCancelPressed)
        {
            ReturnToLevelSelect();
        }
    }

    private void ActivateCompletionOption(int optionIndex, bool showNext)
    {
        if (optionIndex == 0)
        {
            ReplayLevel();
            return;
        }

        if (showNext && optionIndex == 1)
        {
            GoToNextLevel();
            return;
        }

        ReturnToLevelSelect();
    }

    private bool CanGoToNextLevel()
    {
        if (_editorTestMode)
        {
            return false;
        }

        if (_game.SteamLobby.IsInLobby && !_game.Party.IsLeader)
        {
            return false;
        }

        return LevelLibrary.TryGetNextLevelId(_levelId, out _);
    }

    private void GoToNextLevel()
    {
        if (!CanGoToNextLevel() || !LevelLibrary.TryGetNextLevelId(_levelId, out string nextLevelId))
        {
            return;
        }

        Level nextLevel = LevelLibrary.LoadLevel(nextLevelId);
        (RopeGameplayMode ropeMode, bool lavaRise, bool playerCollision) =
            LevelRules.ResolvePredefinedPlaySettings(nextLevel, _ropeGameplayMode);

        if (_game.SteamLobby.IsInLobby)
        {
            if (!_game.Party.IsLeader)
            {
                return;
            }

            MultiplayerDebug.LogSim(
                $"HOST NEXT LEVEL level={nextLevelId} partyMembers={_game.Party.Members.Count} " +
                $"rope={ropeMode} lava={lavaRise} collision={playerCollision}");
            if (!_game.SteamLobby.BroadcastLevelStart(nextLevelId, ropeMode, lavaRise))
            {
                _alertPopup = new AlertPopup(
                    "VERSION MISMATCH",
                    $"Host: {SessionDiagnostics.HostBuildLabel} Client: {SessionDiagnostics.ClientBuildLabel}");
                return;
            }
        }

        LevelSelectScene.SyncPlaySettings(ropeMode, lavaRise, playerCollision);
        _game.ChangeScene(new GameScene(
            _game,
            nextLevelId,
            ropeMode,
            lavaRise,
            _ghostMode,
            playerCollision));
    }

    private void ReplayLevel()
    {
        _completionUiActive = false;
        _completionUiElapsed = 0f;
        _game.Input.GameplayInputBlocked = false;
        ResetGameplaySession();
    }

    private void ResetGameplaySession()
    {
        _simulation.RestartLevel();
        _camera.Position = GetPlayersCenter();
        _camera.SetZoom(GetTargetCameraZoom(_game.Viewport));
        _ghostPlayer?.Reset();
        _worldRecordGhost?.Reset();
        if (!_editorTestMode)
        {
            _replayRecorder.ResetSession();
        }

        _savedNewRecordReplay = false;
    }

    private void ReturnToLevelSelect()
    {
        ReturnFromGameplay();
    }

    private void ReturnFromGameplay()
    {
        if (_editorTestMode)
        {
            _game.ChangeScene(new EditorScene(_game, _levelId));
            return;
        }

        NotifyHostLeftLevelIfClient();
        _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
    }

    private void LayoutCompletionUi()
    {
        Viewport viewport = _game.Viewport;
        bool showNext = CanGoToNextLevel();
        int buttonCount = showNext ? 3 : 2;
        int margin = Math.Max(12, (int)(viewport.Width * 0.06f));
        int maxPanelWidth = showNext ? 780 : 620;
        int panelWidth = Math.Min(maxPanelWidth, Math.Max(1, viewport.Width - (margin * 2)));
        int panelHeight = Math.Min(340, Math.Max(220, (int)(viewport.Height * 0.46f)));
        panelHeight = Math.Min(panelHeight, Math.Max(1, viewport.Height - (margin * 2)));
        Rectangle panel = new(
            (viewport.Width - panelWidth) / 2,
            (viewport.Height - panelHeight) / 2,
            panelWidth,
            panelHeight);

        int buttonGap = Math.Max(10, panel.Width / 32);
        int buttonHeight = Math.Clamp(panel.Height / 5, 44, 58);
        int sidePad = Math.Max(16, panelWidth / 10);
        int buttonWidth = (panel.Width - (buttonGap * (buttonCount - 1)) - sidePad) / buttonCount;
        int buttonsY = panel.Bottom - buttonHeight - Math.Max(18, panel.Height / 10);
        int totalButtonsWidth = (buttonWidth * buttonCount) + (buttonGap * (buttonCount - 1));
        int buttonsX = panel.X + ((panel.Width - totalButtonsWidth) / 2);

        _completionReplayBounds = new Rectangle(buttonsX, buttonsY, buttonWidth, buttonHeight);
        if (showNext)
        {
            _completionNextBounds = new Rectangle(
                _completionReplayBounds.Right + buttonGap,
                buttonsY,
                buttonWidth,
                buttonHeight);
            _completionMenuBounds = new Rectangle(
                _completionNextBounds.Right + buttonGap,
                buttonsY,
                buttonWidth,
                buttonHeight);
        }
        else
        {
            _completionNextBounds = Rectangle.Empty;
            _completionMenuBounds = new Rectangle(
                _completionReplayBounds.Right + buttonGap,
                buttonsY,
                buttonWidth,
                buttonHeight);
        }
    }

    private static string FormatTime(float seconds)
    {
        int totalCentiseconds = (int)MathF.Floor(MathF.Max(0f, seconds) * 100f);
        int minutes = totalCentiseconds / 6000;
        int remainingCentiseconds = totalCentiseconds % 6000;
        int wholeSeconds = remainingCentiseconds / 100;
        int centiseconds = remainingCentiseconds % 100;
        return $"{minutes:00}:{wholeSeconds:00}:{centiseconds:00}";
    }

    private static int GetResponsiveTextScale(Viewport viewport, int divisor, int min, int max)
    {
        return Math.Clamp(viewport.Height / divisor, min, max);
    }

    private static int FitTextScale(string text, int preferredScale, int maxWidth)
    {
        int scale = Math.Max(1, preferredScale);
        while (scale > 1 && SimpleTextRenderer.MeasureString(text, scale).X > maxWidth)
        {
            scale--;
        }

        return scale;
    }

    private static void DrawCenteredText(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        string text,
        int centerX,
        int y,
        int scale,
        Color color)
    {
        Point textSize = SimpleTextRenderer.MeasureString(text, scale);
        Vector2 position = new(centerX - (textSize.X * 0.5f), y);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, position, scale, color);
    }

    private void DrawTimer(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        string timeText = FormatTime(_simulation.ElapsedTime);
        int margin = Math.Max(8, (int)(viewport.Width * 0.03f));
        int scale = FitTextScale(
            timeText,
            GetResponsiveTextScale(viewport, 180, 2, 6),
            viewport.Width - (margin * 2));
        int y = Math.Max(8, (int)(viewport.Height * 0.035f));

        DrawCenteredText(spriteBatch, pixel, timeText, viewport.Width / 2 + 2, y + 2, scale, Color.Black * 0.45f);
        DrawCenteredText(spriteBatch, pixel, timeText, viewport.Width / 2, y, scale, Color.White);
    }

    private void DrawDebugHud(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int margin = Math.Max(8, (int)(Math.Min(viewport.Width, viewport.Height) * 0.022f));
        int scale = 1;
        int lineHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 3;
        SteamManager steam = _game.Steam;
        SteamLobbyService lobby = _game.SteamLobby;
        SteamInputManager steamInput = _game.SteamInput;

        List<string> lines = MultiplayerDebug.BuildPanelLines(
            lobby,
            _game.Party,
            _session,
            _simulation,
            _game.GameNetwork,
            _levelId);

        lines.Add(string.Empty);
        lines.Add("STEAM / INPUT");
        lines.Add($"STEAM INIT {FormatDebugBool(steam.IsInitialized)} STATUS {steam.Status}");
        lines.Add($"STEAM USER {steam.Username} ID {steam.SteamId}");
        lines.Add($"OVERLAY {FormatDebugBool(steam.IsOverlayEnabled)}");
        lines.Add($"STEAM INPUT {(steamInput.IsInitialized ? "Enabled" : "Disabled")} SET {steamInput.CurrentActionSetName}");
        lines.Add($"LAYOUT {steamInput.ActiveLayoutLabel} GLYPH {steamInput.GlyphSource}");
        lines.Add($"CONTROLLERS {steamInput.ConnectedControllerCount}");
        lines.Add($"CHECKPOINT {FormatCheckpointDebugText()} RESPAWN {FormatVector(_playerManager.RespawnPosition)}");
        lines.Add($"SIM STEP {_simulation.PhysicsWorld.LastSimulationStepSeconds * 1000f:0.0}ms");

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            ulong handle = steamInput.GetHandleForSlot(i).m_InputHandle;
            if (!_game.Input.IsGamepadConnected(i) && handle == 0)
            {
                continue;
            }

            string assigned = FindAssignedPlayerLabel(i);
            lines.Add(
                $"PAD{i + 1} type={steamInput.GetControllerType(i)} label={steamInput.GetControllerLabel(i)} handle={handle} player={assigned}");
        }

        int y = Math.Max(margin, viewport.Height - margin - (lines.Count * lineHeight));
        Color ropeModeColor = _ropeGameplayMode == RopeGameplayMode.Neutral
            ? new Color(210, 180, 140)
            : Color.White;

        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 position = new(margin, y + (i * lineHeight));
            string line = lines[i];
            Color textColor = i == 0
                ? ropeModeColor
                : line.StartsWith("  !", StringComparison.Ordinal)
                    ? new Color(255, 120, 100)
                    : Color.White;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, line, position + new Vector2(1f, 1f), scale, Color.Black * 0.55f);
            SimpleTextRenderer.DrawString(spriteBatch, pixel, line, position, scale, textColor);
        }
    }

    private static string FormatInputDevice(InputDevice device)
    {
        return device.DeviceType switch
        {
            InputDeviceType.Keyboard => "Keyboard",
            InputDeviceType.Gamepad => $"Gamepad {device.DeviceIndex + 1}",
            _ => "None"
        };
    }

    private static string GetNetworkRoleText(INetworkEntity entity)
    {
        return entity.IsLocal ? "LOCAL" : "REMOTE";
    }

    private static string FormatDebugBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string FormatLayoutRefresh(DateTime utc)
    {
        if (utc == DateTime.MinValue)
        {
            return "never";
        }

        return utc.ToLocalTime().ToString("HH:mm:ss");
    }

    private string FindAssignedPlayerLabel(int controllerSlot)
    {
        foreach (PartyMember member in _game.Party.Members)
        {
            if (member.InputSource == PartyInputSource.Gamepad && member.ControllerId == controllerSlot)
            {
                return member.DisplayName;
            }
        }

        return "—";
    }

    private string FormatCheckpointDebugText()
    {
        return _playerManager.CurrentCheckpointId is { } id ? $"#{id}" : "NONE";
    }

    private static string FormatVector(Vector2 value)
    {
        return $"{value.X:0},{value.Y:0}";
    }

    private static string GetAuthorityText(INetworkEntity entity)
    {
        return entity.IsHostControlled ? "HOST" : "REPLICA";
    }

    private void DrawCompletionUi(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, GameTime gameTime)
    {
        LayoutCompletionUi();

        float fade = MathHelper.Clamp(_completionUiElapsed / 0.45f, 0f, 1f);
        bool showNext = CanGoToNextLevel();
        int margin = Math.Max(12, (int)(viewport.Width * 0.06f));
        int maxPanelWidth = showNext ? 780 : 620;
        int panelWidth = Math.Min(maxPanelWidth, Math.Max(1, viewport.Width - (margin * 2)));
        int panelHeight = Math.Min(340, Math.Max(220, (int)(viewport.Height * 0.46f)));
        panelHeight = Math.Min(panelHeight, Math.Max(1, viewport.Height - (margin * 2)));
        Rectangle panel = new(
            (viewport.Width - panelWidth) / 2,
            (viewport.Height - panelHeight) / 2,
            panelWidth,
            panelHeight);

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * (0.32f * fade));
        spriteBatch.Draw(pixel, panel, new Color(22, 26, 34) * (0.92f * fade));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(255, 220, 80) * fade, Math.Max(2, panelHeight / 96));

        string title = "LEVEL COMPLETE";
        string finalTime = $"TIME {FormatTime(_finalTime)}";
        int maxTextWidth = Math.Max(1, panel.Width - Math.Max(24, panel.Width / 8));
        int titleScale = FitTextScale(title, GetResponsiveTextScale(viewport, 150, 3, 7), maxTextWidth);
        int bodyScale = FitTextScale(finalTime, GetResponsiveTextScale(viewport, 220, 2, 5), maxTextWidth);
        int recordScale = FitTextScale("NEW RECORD", GetResponsiveTextScale(viewport, 220, 2, 5), maxTextWidth);
        int centerX = panel.Center.X;
        int topPadding = Math.Max(18, panel.Height / 7);
        int lineGap = Math.Max(20, panel.Height / 7);

        DrawCenteredText(spriteBatch, pixel, title, centerX, panel.Top + topPadding, titleScale, Color.White * fade);
        DrawCenteredText(spriteBatch, pixel, finalTime, centerX, panel.Top + topPadding + lineGap, bodyScale, Color.White * fade);

        if (_newRecord)
        {
            DrawCenteredText(
                spriteBatch,
                pixel,
                "NEW RECORD",
                centerX,
                panel.Top + topPadding + (lineGap * 2),
                recordScale,
                new Color(255, 220, 80) * fade);
        }

        DrawCompletionButton(spriteBatch, pixel, _completionReplayBounds, "REPLAY", fade);
        if (showNext)
        {
            DrawCompletionButton(spriteBatch, pixel, _completionNextBounds, "NEXT LEVEL", fade);
        }

        DrawCompletionButton(spriteBatch, pixel, _completionMenuBounds, "BACK TO MENU", fade);

        if (_completionOptionFocusables.Count >= 2)
        {
            _completionFocus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);
        }
    }

    private void DrawCompletionButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label, float fade)
    {
        bool highlighted = _game.Input.Navigation.AllowPointerHoverVisual
            && bounds.Contains(_game.Input.UiPointerPosition);
        Color fill = highlighted ? new Color(58, 50, 24) : new Color(36, 32, 20);
        Color border = highlighted ? new Color(255, 232, 120) : new Color(255, 220, 80);
        Color textColor = new Color(255, 244, 196) * fade;

        spriteBatch.Draw(pixel, bounds, fill * (0.92f * fade));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, border * fade, highlighted ? 4 : 3);

        int scale = FitTextScale(label, GetResponsiveTextScale(_game.Viewport, 220, 2, 4), bounds.Width - 20);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, scale, textColor);
    }

    private void UpdateDeathUi(GameTime gameTime)
    {
        if (_confirmPopup is not null)
        {
            return;
        }

        LayoutDeathUi();
        _deathOptionFocusables.Clear();
        _deathFocus.Clear();

        // Order: checkpoint first, then restart start, then quit.
        _deathOptionFocusables.Add(new FocusableGridCell(_deathCheckpointBounds, () => _simulation.HasCheckpoint)
        {
            IsEnabled = _simulation.HasCheckpoint
        });
        _deathOptionFocusables.Add(new FocusableGridCell(_deathRespawnStartBounds, () => true));
        _deathOptionFocusables.Add(new FocusableGridCell(_deathQuitBounds, () => true));

        var optionIndices = new List<int>();
        optionIndices.Add(_deathFocus.Add(_deathOptionFocusables[0], "RespawnCheckpoint"));
        optionIndices.Add(_deathFocus.Add(_deathOptionFocusables[1], "RespawnStart"));
        optionIndices.Add(_deathFocus.Add(_deathOptionFocusables[2], "Quit"));
        _deathFocus.Navigation.WireVerticalChain(optionIndices);

        string defaultFocus = _simulation.HasCheckpoint ? "RespawnCheckpoint" : "RespawnStart";
        _deathFocus.FinalizeFocus(defaultFocus);
        _deathFocus.Update(gameTime, _game.Input);

        InputManager input = _game.Input;
        if (input.UiPointerPressed)
        {
            if (_simulation.HasCheckpoint && _deathCheckpointBounds.Contains(input.UiPointerPosition))
            {
                _simulation.RespawnFromCheckpoint();
                return;
            }

            if (_deathRespawnStartBounds.Contains(input.UiPointerPosition))
            {
                OpenQuitOrRestartConfirm(ConfirmActionKind.RestartLevel);
                return;
            }

            if (_deathQuitBounds.Contains(input.UiPointerPosition))
            {
                OpenQuitOrRestartConfirm(ConfirmActionKind.QuitToMenu);
                return;
            }
        }

        for (int i = 0; i < _deathOptionFocusables.Count; i++)
        {
            if (!_deathOptionFocusables[i].IsEnabled)
            {
                continue;
            }

            if (_deathOptionFocusables[i].WasActivated
                || (_deathFocus.Focused == _deathOptionFocusables[i] && input.MenuConfirmPressed))
            {
                HandleDeathMenuChoice(i);
                return;
            }
        }

        if (input.ExitPressed || input.MenuCancelPressed)
        {
            OpenQuitOrRestartConfirm(ConfirmActionKind.QuitToMenu);
        }
    }

    private void OpenQuitOrRestartConfirm(ConfirmActionKind kind)
    {
        if (kind == ConfirmActionKind.RestartLevel)
        {
            // No checkpoint → nothing to lose; skip confirm.
            if (!_simulation.HasCheckpoint)
            {
                ExecuteConfirmedAction(ConfirmActionKind.RestartLevel);
                return;
            }

            _confirmPopup = new Popup(
                "Restart Level",
                "Restart from the beginning?\nProgress will be lost.",
                "Restart",
                "Cancel");
        }
        else
        {
            _confirmPopup = new Popup(
                "Quit",
                "Return to level select?",
                "Quit",
                "Cancel");
        }

        _pendingConfirm = kind;
    }

    private void HandleDeathMenuChoice(int optionIndex)
    {
        switch (optionIndex)
        {
            case 0 when _simulation.HasCheckpoint:
                _simulation.RespawnFromCheckpoint();
                break;
            case 1:
                OpenQuitOrRestartConfirm(ConfirmActionKind.RestartLevel);
                break;
            default:
                OpenQuitOrRestartConfirm(ConfirmActionKind.QuitToMenu);
                break;
        }
    }

    private void LayoutDeathUi()
    {
        Viewport viewport = _game.Viewport;
        int buttonWidth = Math.Min(440, Math.Max(220, (int)(viewport.Width * 0.34f)));
        int buttonHeight = Math.Clamp((int)(viewport.Height * 0.085f), 44, 64);
        int gap = Math.Max(12, buttonHeight / 4);
        int totalHeight = (buttonHeight * 3) + (gap * 2);
        int x = (viewport.Width - buttonWidth) / 2;
        int firstY = (viewport.Height / 2) - (totalHeight / 2) + (int)(viewport.Height * 0.06f);

        _deathCheckpointBounds = new Rectangle(x, firstY, buttonWidth, buttonHeight);
        _deathRespawnStartBounds = new Rectangle(x, firstY + buttonHeight + gap, buttonWidth, buttonHeight);
        _deathQuitBounds = new Rectangle(x, firstY + (buttonHeight + gap) * 2, buttonWidth, buttonHeight);
    }

    private void DrawDeathUi(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, GameTime gameTime)
    {
        LayoutDeathUi();

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(12, 4, 2) * 0.78f);

        string title = "YOU DIED";
        int titleScale = FitTextScale(title, GetResponsiveTextScale(viewport, 90, 4, 10), viewport.Width - 80);
        int titleY = (viewport.Height / 2) - (int)(viewport.Height * 0.22f);
        DrawCenteredText(spriteBatch, pixel, title, (viewport.Width / 2) + 3, titleY + 3, titleScale, new Color(60, 8, 0));
        DrawCenteredText(spriteBatch, pixel, title, viewport.Width / 2, titleY, titleScale, new Color(255, 96, 40));

        bool hasCheckpoint = _simulation.HasCheckpoint;
        DrawDeathButton(spriteBatch, pixel, _deathCheckpointBounds, "RESPAWN CHECKPOINT", enabled: hasCheckpoint);
        DrawDeathButton(spriteBatch, pixel, _deathRespawnStartBounds, "RESTART LEVEL", enabled: true);
        DrawDeathButton(spriteBatch, pixel, _deathQuitBounds, "QUIT", enabled: true);
        if (_deathOptionFocusables.Count > 0)
        {
            _deathFocus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);
        }
    }

    private void DrawDeathButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label, bool enabled)
    {
        bool highlighted = enabled
            && _game.Input.Navigation.AllowPointerHoverVisual
            && bounds.Contains(_game.Input.UiPointerPosition);
        Color fill = enabled
            ? (highlighted ? new Color(70, 30, 14) : new Color(44, 20, 12))
            : new Color(26, 24, 28);
        Color border = enabled
            ? (highlighted ? new Color(255, 170, 70) : new Color(210, 96, 40))
            : new Color(70, 66, 74);
        Color textColor = enabled ? new Color(255, 226, 196) : new Color(120, 116, 126);

        spriteBatch.Draw(pixel, bounds, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, border, highlighted ? 4 : 3);

        int scale = FitTextScale(label, GetResponsiveTextScale(_game.Viewport, 220, 2, 4), bounds.Width - 24);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, scale, textColor);
    }
}
