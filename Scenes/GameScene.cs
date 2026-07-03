using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class GameScene : IScene
{
    private const float MaxSceneFrameTime = 0.25f;

    private readonly ColorBlocksGame _game;
    private readonly string _levelId;
    private readonly RopeGameplayMode _ropeGameplayMode;
    private readonly bool _lavaRiseEnabled;
    private readonly GameSession _session;
    private readonly Level _level;
    private readonly PlayerManager _playerManager;
    private readonly GameSimulation _simulation;
    private readonly Camera _camera;
    private readonly PauseMenuOverlay _pauseMenu = new();
    private bool _debugDraw;
    private bool _completionUiActive;
    private float _finalTime;
    private bool _newRecord;
    private float _completionUiElapsed;
    private Rectangle _completionReplayBounds;
    private Rectangle _completionMenuBounds;
    private readonly UIFocusManager _completionFocus = new();
    private readonly List<FocusableGridCell> _completionOptionFocusables = new();
    private Rectangle _deathRespawnStartBounds;
    private Rectangle _deathCheckpointBounds;
    private Rectangle _deathQuitBounds;
    private readonly UIFocusManager _deathFocus = new();
    private readonly List<FocusableGridCell> _deathOptionFocusables = new();
    private bool _disconnectPending;
    private float _disconnectTimer;
    private const float DisconnectReturnDelay = 2.5f;

    public GameScene(
        ColorBlocksGame game,
        string levelId = "level_1",
        RopeGameplayMode ropeGameplayMode = RopeGameplayMode.ColoredPhysics,
        bool lavaRiseEnabled = false)
    {
        _game = game;
        _levelId = levelId;
        _ropeGameplayMode = ropeGameplayMode;
        _lavaRiseEnabled = lavaRiseEnabled;
        int localOwnerId = _game.SteamLobby.IsAvailable
            ? SteamOwnerId.FromSteamId(_game.SteamLobby.LocalSteamId)
            : NetworkOwners.HostOwnerId;
        _session = _game.SteamLobby.IsInLobby
            ? GameSession.CreateOnline(
                _game.Party.IsLeader ? GameSessionRole.Host : GameSessionRole.Client,
                levelId,
                ropeGameplayMode,
                localOwnerId,
                _game.Steam.Username)
            : GameSession.CreateLocalTest(levelId, ropeGameplayMode);
        _session.LavaRiseEnabled = lavaRiseEnabled;
        _level = LevelManager.LoadLevel(levelId);
        _playerManager = new PlayerManager(_session, _level);
        _game.Party.ApplyPreferredInputForPrimaryLocalMember(_game.Input);
        _game.Party.LockAssignments();
        _playerManager.SpawnFromParty(_game.Party.Members, _game.Input, _game.Steam.Username);
        _simulation = new GameSimulation(_session, _level, _playerManager, lavaRiseEnabled);
        _camera = new Camera(GetPlayersCenter());
        _game.SteamLobby.MemberLeft += OnLobbyMemberLeft;
        _game.Music.PlayLevelMusic(_level.MusicId);
    }

    public void OnExit()
    {
        _game.SteamLobby.MemberLeft -= OnLobbyMemberLeft;
        _game.Input.ClearGameplayBindings();
        _game.Party.UnlockAssignments();
        _game.GameNetwork.Reset();
        _game.Music.Stop();
    }

    private void OnLobbyMemberLeft(ulong steamId)
    {
        if (!_game.SteamLobby.IsInLobby || steamId == _game.SteamLobby.LocalSteamId)
        {
            return;
        }

        _disconnectPending = true;
        _disconnectTimer = DisconnectReturnDelay;
    }

    private IReadOnlyList<Player> Players => _simulation.Players;
    private IReadOnlyList<Rope> Ropes => _simulation.Ropes;

    public void Update(GameTime gameTime)
    {
        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxSceneFrameTime);
        Viewport viewport = _game.Viewport;

        if (_disconnectPending)
        {
            _disconnectTimer -= dt;
            if (_disconnectTimer <= 0f)
            {
                _game.ChangeScene(new PartyScene(_game));
                return;
            }
        }

        if (_pauseMenu.IsOpen)
        {
            _game.Input.GameplayInputBlocked = true;
            PauseMenuChoice? choice = _pauseMenu.Update(gameTime, _game.Input, viewport);
            if (choice.HasValue)
            {
                HandlePauseMenuChoice(choice.Value);
            }

            return;
        }

        if (_simulation.IsPlayerDead)
        {
            _game.Input.GameplayInputBlocked = true;
            UpdateDeathUi(gameTime);
            UpdateCamera(gameTime);
            return;
        }

        if (_simulation.IsLevelComplete)
        {
            _game.Input.GameplayInputBlocked = true;
            BeginCompletionUi();
            _completionUiElapsed += dt;
            UpdateCompletionUi(gameTime);
            UpdateCamera(gameTime);
            return;
        }

        _game.Input.GameplayInputBlocked = false;

        if (_game.Input.GameplayPausePressed)
        {
            _simulation.SetPaused(true);
            _pauseMenu.Open();
            return;
        }

        if (_game.Input.DebugTogglePressed)
        {
            _debugDraw = !_debugDraw;
        }

        GameNetworkCoordinator network = _game.GameNetwork;
        network.PumpIncoming(_session, _simulation);

        if (_session.Role == GameSessionRole.Client)
        {
            network.SendLocalInput(_session, _simulation, _game.Input);
            if (network.TryConsumeClientSnapshot(out GameSnapshot snapshot))
            {
                _simulation.ApplySnapshot(snapshot);
            }

            BeginCompletionUi();
            UpdateCamera(gameTime);
            return;
        }

        _simulation.Advance(dt, _game.Input);
        BeginCompletionUi();

        if (_session.Role == GameSessionRole.Host)
        {
            network.BroadcastSnapshot(_session, _simulation.LastSnapshot);
        }

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
                _simulation.SetPaused(false);
                _pauseMenu.Close();
                _game.Input.GameplayInputBlocked = false;
                _simulation.RestartLevel();
                break;
            case PauseMenuChoice.BackToMenu:
                _simulation.SetPaused(false);
                _pauseMenu.Close();
                _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
                break;
            case PauseMenuChoice.QuitGame:
                _game.ExitGame();
                break;
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        Viewport viewport = _game.Viewport;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_game.Pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(36, 41, 52));
        spriteBatch.End();

        spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _camera.GetTransform(viewport));

        _level.Draw(spriteBatch, _game.Pixel, _debugDraw, _simulation.ElapsedTime, isEditorMode: false);
        foreach (Rope rope in Ropes)
        {
            rope.Draw(spriteBatch, _game.Pixel, _debugDraw);
        }

        foreach (Player player in Players)
        {
            player.Draw(spriteBatch, _game.Pixel, _debugDraw);
        }

        if (_simulation.LavaActive)
        {
            // Drawn after players so anyone who sinks is covered by the molten body.
            // Clipped to a padded view of the visible world (no need to fill the
            // entire infinite plane), which keeps it cheap regardless of map size.
            Rectangle lavaView = _camera.GetVisibleWorldRectangle(viewport, 96);
            LavaLine.Draw(
                spriteBatch,
                _game.Pixel,
                lavaView,
                _simulation.LavaSurfaceY,
                (float)gameTime.TotalGameTime.TotalSeconds,
                drawParticles: true);
        }

        spriteBatch.End();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawTimer(spriteBatch, _game.Pixel, viewport);
        if (_debugDraw)
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

        if (_pauseMenu.IsOpen)
        {
            _pauseMenu.Draw(spriteBatch, _game.Pixel, gameTime, viewport, _game.Input);
        }

        if (_disconnectPending)
        {
            DrawDisconnectOverlay(spriteBatch, _game.Pixel, viewport);
        }

        spriteBatch.End();
    }

    private void DrawDisconnectOverlay(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 170));
        DrawCenteredText(spriteBatch, pixel, "Player disconnected.", viewport.Width / 2, viewport.Height / 2 - 20, 3, Color.White);
        DrawCenteredText(spriteBatch, pixel, "Returning to Party...", viewport.Width / 2, viewport.Height / 2 + 20, 2, new Color(200, 210, 225));
    }

    private Vector2 GetPlayersCenter()
    {
        int localCount = 0;
        Vector2 total = Vector2.Zero;
        foreach (Player player in Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            localCount++;
            total += player.Position + (player.Size * 0.5f);
        }

        if (localCount == 0)
        {
            return _level.PlayerStart;
        }

        return total / localCount;
    }

    private float GetTargetCameraZoom(Viewport viewport)
    {
        int localCount = 0;
        foreach (Player player in Players)
        {
            if (player.IsLocal)
            {
                localCount++;
            }
        }

        if (localCount <= 1 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return 1f;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (Player player in Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            Vector2 center = player.Position + (player.Size * 0.5f);
            minX = MathF.Min(minX, center.X);
            minY = MathF.Min(minY, center.Y);
            maxX = MathF.Max(maxX, center.X);
            maxY = MathF.Max(maxY, center.Y);
        }

        foreach (Rope rope in Ropes)
        {
            foreach (RopeNode node in rope.Nodes)
            {
                minX = MathF.Min(minX, node.Position.X);
                minY = MathF.Min(minY, node.Position.Y);
                maxX = MathF.Max(maxX, node.Position.X);
                maxY = MathF.Max(maxY, node.Position.Y);
            }
        }

        const float cameraPadding = 360f;
        float groupWidth = MathF.Max(1f, maxX - minX + cameraPadding);
        float groupHeight = MathF.Max(1f, maxY - minY + cameraPadding);
        float zoomX = viewport.Width / groupWidth;
        float zoomY = viewport.Height / groupHeight;

        return MathHelper.Clamp(MathF.Min(zoomX, zoomY), 0.55f, 1f);
    }

    private void UpdateCamera(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float smoothing = 1f - MathF.Exp(-6f * dt);
        _camera.Position = Vector2.Lerp(_camera.Position, GetPlayersCenter(), smoothing);
        _camera.SetZoom(MathHelper.Lerp(_camera.Zoom, GetTargetCameraZoom(_game.Viewport), smoothing));
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

        _completionOptionFocusables.Clear();
        _completionFocus.Clear();
        _completionOptionFocusables.Add(new FocusableGridCell(_completionReplayBounds, () => true));
        _completionOptionFocusables.Add(new FocusableGridCell(_completionMenuBounds, () => true));

        int replayIndex = _completionFocus.Add(_completionOptionFocusables[0], "Replay");
        int menuIndex = _completionFocus.Add(_completionOptionFocusables[1], "BackToMenu");
        _completionFocus.Navigation.LinkHorizontal(replayIndex, menuIndex);

        _completionFocus.FinalizeFocus("Replay");
        _completionFocus.Update(gameTime, _game.Input);

        InputManager input = _game.Input;
        if (input.UiPointerPressed)
        {
            if (_completionReplayBounds.Contains(input.UiPointerPosition))
            {
                ReplayLevel();
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

            if (i == 0)
            {
                ReplayLevel();
            }
            else
            {
                ReturnToLevelSelect();
            }

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

            if (i == 0)
            {
                ReplayLevel();
            }
            else
            {
                ReturnToLevelSelect();
            }

            return;
        }

        if (input.ExitPressed || input.MenuCancelPressed)
        {
            ReturnToLevelSelect();
        }
    }

    private void ReplayLevel()
    {
        _completionUiActive = false;
        _completionUiElapsed = 0f;
        _game.Input.GameplayInputBlocked = false;
        _simulation.RestartLevel();
    }

    private void ReturnToLevelSelect()
    {
        _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
    }

    private void LayoutCompletionUi()
    {
        Viewport viewport = _game.Viewport;
        int margin = Math.Max(12, (int)(viewport.Width * 0.08f));
        int panelWidth = Math.Min(620, Math.Max(1, viewport.Width - (margin * 2)));
        int panelHeight = Math.Min(340, Math.Max(220, (int)(viewport.Height * 0.46f)));
        panelHeight = Math.Min(panelHeight, Math.Max(1, viewport.Height - (margin * 2)));
        Rectangle panel = new(
            (viewport.Width - panelWidth) / 2,
            (viewport.Height - panelHeight) / 2,
            panelWidth,
            panelHeight);

        int buttonGap = Math.Max(14, panel.Width / 24);
        int buttonHeight = Math.Clamp(panel.Height / 5, 44, 58);
        int buttonWidth = (panel.Width - buttonGap - (panelWidth / 6)) / 2;
        int buttonsY = panel.Bottom - buttonHeight - Math.Max(18, panel.Height / 10);
        int buttonsX = panel.X + ((panel.Width - ((buttonWidth * 2) + buttonGap)) / 2);

        _completionReplayBounds = new Rectangle(buttonsX, buttonsY, buttonWidth, buttonHeight);
        _completionMenuBounds = new Rectangle(_completionReplayBounds.Right + buttonGap, buttonsY, buttonWidth, buttonHeight);
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
        List<string> lines = new()
        {
            "PARTY",
            $"MEMBERS {_game.Party.Members.Count}/{PartyManager.MaxMembers} LOCKED {FormatDebugBool(_game.Party.AssignmentsLocked)}",
            $"LEADER {_game.Party.Leader?.DisplayName ?? "NONE"}",
            $"LOBBY ID {(_game.Party.LobbyId?.ToString() ?? "NONE")}",
            $"LOBBY OWNER {lobby.GetLobbyOwnerSteamId()}",
            $"CURRENT LEVEL {_levelId}",
            $"CURRENT ROPE {_ropeGameplayMode.ToDebugName()}",
            $"TICK {_simulation.CurrentTick.Value} RATE {_simulation.TickRate.TicksPerSecond}",
            $"SNAPS {_simulation.SnapshotCount} INPUT {_simulation.InputBuffer.FrameCount} DROPPED {_simulation.InputBuffer.DroppedFrameCount}",
            $"ACTIVE CHECKPOINT {FormatCheckpointDebugText()}",
            $"RESPAWN POS {FormatVector(_playerManager.RespawnPosition)}",
            $"LAUNCH PADS {_level.LaunchPads.Count} LAST FORCE {FormatVector(_simulation.PhysicsWorld.LastLaunchForce)}",
            $"SESSION {_session.Role} OWNER {_session.LocalOwnerId} HOST {_session.HostOwnerId}",
            $"NET {_game.GameNetwork.GetOnlineRoleLabel(_session)} SNAP {_simulation.LastSnapshot.Sequence}",
            $"STEAM INITIALIZED: {FormatDebugBool(steam.IsInitialized)}",
            $"STEAM IN LOBBY: {FormatDebugBool(lobby.IsInLobby)}",
            $"STEAM USERNAME: {steam.Username}",
            $"STEAMID: {steam.SteamId}",
            $"OWNER: {lobby.GetLobbyOwnerSteamId()}",
            $"OVERLAY ENABLED: {FormatDebugBool(steam.IsOverlayEnabled)}",
            $"STEAM STATUS: {steam.Status}",
            $"STEAM INPUT: {FormatDebugBool(_game.SteamInput.IsInitialized)}"
        };

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (_game.Input.IsGamepadConnected(i))
            {
                lines.Add($"PAD{i + 1} {_game.SteamInput.GetControllerLabel(i)}");
            }
        }

        foreach (PartyMember member in _game.Party.Members)
        {
            string leaderTag = member.IsLeader ? " LEADER" : string.Empty;
            lines.Add(
                $"MEMBER {member.DisplayName} NET{member.NetworkPlayerId} OWNER{member.OwnerId} TYPE {member.MemberType} INPUT {member.GetInputLabel()}{leaderTag}");
        }

        foreach (Player player in Players)
        {
            lines.Add(
                $"PLAYER P{player.PlayerIndex + 1} PARTY{player.PartyMemberId.Value} N{player.NetworkId} INPUT {FormatInputDevice(player.AssignedInput)} {GetNetworkRoleText(player)} {GetAuthorityText(player)}");
        }

        foreach (Rope rope in Ropes)
        {
            int tension = (int)MathF.Round(rope.LastTension * 100f);
            lines.Add($"ROPE N{rope.NetworkId} O{rope.OwnerId} {GetNetworkRoleText(rope)} {GetAuthorityText(rope)} T{tension}");
        }

        int y = Math.Max(margin, viewport.Height - margin - (lines.Count * lineHeight));
        Color ropeModeColor = _ropeGameplayMode == RopeGameplayMode.Neutral
            ? new Color(210, 180, 140)
            : Color.White;

        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 position = new(margin, y + (i * lineHeight));
            Color textColor = i == 0 ? ropeModeColor : Color.White;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, lines[i], position + new Vector2(1f, 1f), scale, Color.Black * 0.55f);
            SimpleTextRenderer.DrawString(spriteBatch, pixel, lines[i], position, scale, textColor);
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
        int margin = Math.Max(12, (int)(viewport.Width * 0.08f));
        int panelWidth = Math.Min(620, Math.Max(1, viewport.Width - (margin * 2)));
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
        LayoutDeathUi();
        _deathOptionFocusables.Clear();
        _deathFocus.Clear();
        _deathOptionFocusables.Add(new FocusableGridCell(_deathRespawnStartBounds, () => true));
        var checkpointOption = new FocusableGridCell(_deathCheckpointBounds, () => _simulation.HasCheckpoint)
        {
            IsEnabled = _simulation.HasCheckpoint
        };
        _deathOptionFocusables.Add(checkpointOption);
        _deathOptionFocusables.Add(new FocusableGridCell(_deathQuitBounds, () => true));

        var optionIndices = new List<int>();
        optionIndices.Add(_deathFocus.Add(_deathOptionFocusables[0], "RespawnStart"));
        optionIndices.Add(_deathFocus.Add(_deathOptionFocusables[1], "RespawnCheckpoint"));
        optionIndices.Add(_deathFocus.Add(_deathOptionFocusables[2], "Quit"));
        _deathFocus.Navigation.WireVerticalChain(optionIndices);

        _deathFocus.FinalizeFocus("RespawnStart");
        _deathFocus.Update(gameTime, _game.Input);

        InputManager input = _game.Input;
        if (input.UiPointerPressed)
        {
            if (_deathRespawnStartBounds.Contains(input.UiPointerPosition))
            {
                _simulation.RespawnFromStart();
                return;
            }

            if (_simulation.HasCheckpoint && _deathCheckpointBounds.Contains(input.UiPointerPosition))
            {
                _simulation.RespawnFromCheckpoint();
                return;
            }

            if (_deathQuitBounds.Contains(input.UiPointerPosition))
            {
                _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
                return;
            }
        }

        for (int i = 0; i < _deathOptionFocusables.Count; i++)
        {
            if (!_deathOptionFocusables[i].IsEnabled)
            {
                continue;
            }

            if (_deathOptionFocusables[i].WasActivated)
            {
                switch (i)
                {
                    case 0:
                        _simulation.RespawnFromStart();
                        break;
                    case 1 when _simulation.HasCheckpoint:
                        _simulation.RespawnFromCheckpoint();
                        break;
                    case 2:
                        _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
                        break;
                }

                return;
            }

            bool confirmed = _deathFocus.Focused == _deathOptionFocusables[i]
                && ((input.Navigation.IsKeyboardActive && input.KeyboardMenuConfirmPressed)
                    || (input.Navigation.IsGamepadActive && input.GamepadMenuConfirmPressed));
            if (confirmed)
            {
                switch (i)
                {
                    case 0:
                        _simulation.RespawnFromStart();
                        break;
                    case 1 when _simulation.HasCheckpoint:
                        _simulation.RespawnFromCheckpoint();
                        break;
                    case 2:
                        _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
                        break;
                }

                return;
            }
        }

        if (input.ExitPressed || input.MenuCancelPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
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

        _deathRespawnStartBounds = new Rectangle(x, firstY, buttonWidth, buttonHeight);
        _deathCheckpointBounds = new Rectangle(x, firstY + buttonHeight + gap, buttonWidth, buttonHeight);
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
        bool showFocus = _deathOptionFocusables.Count >= 3;
        DrawDeathButton(spriteBatch, pixel, _deathRespawnStartBounds, "RESPAWN START", enabled: true);
        DrawDeathButton(spriteBatch, pixel, _deathCheckpointBounds, "RESPAWN CHECKPOINT", enabled: hasCheckpoint);
        DrawDeathButton(spriteBatch, pixel, _deathQuitBounds, "QUIT", enabled: true);
        if (showFocus)
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
