using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class GameScene : IScene
{
    private const float CompletionReturnDelaySeconds = 3f;
    private const float MaxSceneFrameTime = 0.25f;

    private readonly ColorBlocksGame _game;
    private readonly RopeGameplayMode _ropeGameplayMode;
    private readonly GameSession _session;
    private readonly Level _level;
    private readonly PlayerManager _playerManager;
    private readonly GameSimulation _simulation;
    private readonly Camera _camera;
    private readonly Button _backButton = new("Back to Menu") { TextScale = 2 };
    private bool _debugDraw;
    private bool _completionUiActive;
    private float _finalTime;
    private bool _newRecord;
    private float _completionReturnDelay;
    private float _completionUiElapsed;

    public GameScene(
        ColorBlocksGame game,
        string levelId = "level_1",
        RopeGameplayMode ropeGameplayMode = RopeGameplayMode.ColoredPhysics)
    {
        _game = game;
        _ropeGameplayMode = ropeGameplayMode;
        _session = GameSession.CreateLocalTest(levelId, ropeGameplayMode);
        _level = LevelManager.LoadLevel(levelId);
        _playerManager = new PlayerManager(_session, _level);
        _playerManager.SpawnLocalPlayers(_game.Input.ActiveProfiles);
        _simulation = new GameSimulation(_session, _level, _playerManager);
        _camera = new Camera(GetPlayersCenter());
    }

    private IReadOnlyList<Player> Players => _simulation.Players;
    private IReadOnlyList<Rope> Ropes => _simulation.Ropes;

    public void Update(GameTime gameTime)
    {
        LayoutBackButton();

        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxSceneFrameTime);

        if (_simulation.IsLevelComplete)
        {
            BeginCompletionUi();
            _completionReturnDelay = MathF.Max(0f, _completionReturnDelay - dt);
            _completionUiElapsed += dt;
            UpdateCamera(gameTime);

            if (CanReturnToMenu() && _backButton.Update(_game.Input))
            {
                _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
                return;
            }

            if (CanReturnToMenu() && _game.Input.ExitPressed)
            {
                _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
                return;
            }

            return;
        }

        if (_backButton.Update(_game.Input))
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
            return;
        }

        if (_game.Input.ExitPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
            return;
        }

        if (_game.Input.DebugTogglePressed)
        {
            _debugDraw = !_debugDraw;
        }

        HandlePlayerSelectionClick();

        _simulation.Advance(dt, _game.Input);
        BeginCompletionUi();

        UpdateCamera(gameTime);
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutBackButton();

        Viewport viewport = _game.Viewport;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_game.Pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(36, 41, 52));
        spriteBatch.End();

        spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _camera.GetTransform(viewport));

        _level.Draw(spriteBatch, _game.Pixel, _debugDraw);
        foreach (Rope rope in Ropes)
        {
            rope.Draw(spriteBatch, _game.Pixel, _debugDraw);
        }

        foreach (Player player in Players)
        {
            player.Draw(spriteBatch, _game.Pixel, _debugDraw);
        }

        spriteBatch.End();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawTimer(spriteBatch, _game.Pixel, viewport);
        DrawPlayerHud(spriteBatch, _game.Pixel, viewport);
        if (_debugDraw)
        {
            DrawDebugHud(spriteBatch, _game.Pixel, viewport);
        }

        if (_simulation.IsLevelComplete)
        {
            DrawCompletionUi(spriteBatch, _game.Pixel, viewport);
        }

        if (CanReturnToMenu())
        {
            _backButton.Draw(spriteBatch, _game.Pixel);
        }

        spriteBatch.End();
    }

    private void HandlePlayerSelectionClick()
    {
        if (!_game.Input.LeftMousePressed)
        {
            return;
        }

        Matrix inverseCameraTransform = Matrix.Invert(_camera.GetTransform(_game.Viewport));
        Vector2 worldPosition = Vector2.Transform(
            new Vector2(_game.Input.MousePosition.X, _game.Input.MousePosition.Y),
            inverseCameraTransform);

        for (int i = Players.Count - 1; i >= 0; i--)
        {
            Player player = Players[i];
            if (player.IsRemote)
            {
                continue;
            }

            if (!player.Bounds.Contains((int)MathF.Floor(worldPosition.X), (int)MathF.Floor(worldPosition.Y)))
            {
                continue;
            }

            SetKeyboardControlledPlayer(player);
            return;
        }
    }

    private void SetKeyboardControlledPlayer(Player controlledPlayer)
    {
        if (!controlledPlayer.IsLocal)
        {
            return;
        }

        _game.Input.SetKeyboardControlledPlayer(controlledPlayer.PlayerId);

        foreach (Player player in Players)
        {
            if (player.AssignedInput.DeviceType == InputDeviceType.Keyboard)
            {
                player.AssignedInput = InputDevice.None;
            }
        }

        controlledPlayer.AssignedInput = InputDevice.Keyboard;
    }

    private void UpdateCamera(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float smoothing = 1f - MathF.Exp(-6f * dt);
        _camera.Position = Vector2.Lerp(_camera.Position, GetPlayersCenter(), smoothing);
        _camera.SetZoom(MathHelper.Lerp(_camera.Zoom, GetTargetCameraZoom(_game.Viewport), smoothing));
    }

    private Vector2 GetPlayersCenter()
    {
        if (Players.Count == 0)
        {
            return _level.PlayerStart;
        }

        Vector2 total = Vector2.Zero;
        foreach (Player player in Players)
        {
            total += player.Position + (player.Size * 0.5f);
        }

        return total / Players.Count;
    }

    private float GetTargetCameraZoom(Viewport viewport)
    {
        if (Players.Count <= 1 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return 1f;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (Player player in Players)
        {
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

    private void BeginCompletionUi()
    {
        if (!_simulation.IsLevelComplete || _completionUiActive)
        {
            return;
        }

        _completionUiActive = true;
        _finalTime = _simulation.FinalTime;
        _newRecord = _simulation.NewRecord;
        _completionReturnDelay = CompletionReturnDelaySeconds;
        _completionUiElapsed = 0f;
    }

    private bool CanReturnToMenu()
    {
        return !_simulation.IsLevelComplete || _completionReturnDelay <= 0f;
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

    private void DrawPlayerHud(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int margin = Math.Max(8, (int)(Math.Min(viewport.Width, viewport.Height) * 0.022f));
        int scale = Math.Clamp(viewport.Height / 360, 1, 2);
        string playerCountText = $"PLAYERS {Players.Count}";
        Vector2 textPosition = new(margin, margin);

        SimpleTextRenderer.DrawString(spriteBatch, pixel, playerCountText, textPosition + new Vector2(1f, 1f), scale, Color.Black * 0.45f);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, playerCountText, textPosition, scale, Color.White);

        Point textSize = SimpleTextRenderer.MeasureString(playerCountText, scale);
        int swatchSize = Math.Clamp(viewport.Height / 34, 16, 24);
        int swatchGap = Math.Max(6, swatchSize / 3);
        int y = margin + textSize.Y + 8;

        for (int i = 0; i < Players.Count; i++)
        {
            Player player = Players[i];
            int x = margin + (i * (swatchSize + swatchGap));
            Rectangle border = new(x, y, swatchSize, swatchSize);
            Rectangle fill = new(x + 2, y + 2, swatchSize - 4, swatchSize - 4);

            spriteBatch.Draw(pixel, border, Color.Black);
            spriteBatch.Draw(pixel, fill, player.PlayerColor.ToXnaColor());

            string label = (player.PlayerIndex + 1).ToString();
            int numberScale = 1;
            Point labelSize = SimpleTextRenderer.MeasureString(label, numberScale);
            Vector2 labelPosition = new(
                border.Center.X - (labelSize.X * 0.5f),
                border.Center.Y - (labelSize.Y * 0.5f));

            SimpleTextRenderer.DrawString(spriteBatch, pixel, label, labelPosition + new Vector2(1f, 1f), numberScale, Color.Black);
            SimpleTextRenderer.DrawString(spriteBatch, pixel, label, labelPosition, numberScale, Color.White);
        }
    }

    private void DrawDebugHud(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int margin = Math.Max(8, (int)(Math.Min(viewport.Width, viewport.Height) * 0.022f));
        int scale = 1;
        int lineHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 3;
        SteamManager steam = _game.Steam;
        List<string> lines = new()
        {
            $"ROPE MODE {_ropeGameplayMode.ToDebugName()}",
            $"TICK {_simulation.CurrentTick.Value} RATE {_simulation.TickRate.TicksPerSecond}",
            $"SNAPS {_simulation.SnapshotCount} INPUT {_simulation.InputBuffer.FrameCount} DROPPED {_simulation.InputBuffer.DroppedFrameCount}",
            $"SESSION {_session.Role} OWNER {_session.LocalOwnerId} HOST {_session.HostOwnerId}",
            $"STEAM INITIALIZED: {FormatDebugBool(steam.IsInitialized)}",
            $"STEAM USERNAME: {steam.Username}",
            $"STEAMID: {steam.SteamId}",
            $"OVERLAY ENABLED: {FormatDebugBool(steam.IsOverlayEnabled)}",
            $"STEAM STATUS: {steam.Status}"
        };

        foreach (Player player in Players)
        {
            lines.Add($"P{player.PlayerIndex + 1} N{player.NetworkId} O{player.OwnerId} {GetNetworkRoleText(player)} {GetAuthorityText(player)}");
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

    private static string GetNetworkRoleText(INetworkEntity entity)
    {
        return entity.IsLocal ? "LOCAL" : "REMOTE";
    }

    private static string FormatDebugBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string GetAuthorityText(INetworkEntity entity)
    {
        return entity.IsHostControlled ? "HOST" : "REPLICA";
    }

    private void DrawCompletionUi(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        float fade = MathHelper.Clamp(_completionUiElapsed / 0.45f, 0f, 1f);
        int margin = Math.Max(12, (int)(viewport.Width * 0.08f));
        int panelWidth = Math.Min(620, Math.Max(1, viewport.Width - (margin * 2)));
        int panelHeight = Math.Min(260, Math.Max(150, (int)(viewport.Height * 0.38f)));
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
        int lineGap = Math.Max(20, panel.Height / 6);

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
    }

    private void LayoutBackButton()
    {
        Viewport viewport = _game.Viewport;
        int minDimension = Math.Min(viewport.Width, viewport.Height);
        int margin = Math.Max(8, (int)(minDimension * 0.022f));
        int width = Math.Min(180, Math.Max(1, viewport.Width - (margin * 2)));
        int height = Math.Clamp((int)(viewport.Height * 0.058f), 36, 44);

        _backButton.Bounds = new Rectangle(viewport.Width - width - margin, margin, width, height);
    }
}
