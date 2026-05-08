using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class GameScene : IScene
{
    private const float CompletionReturnDelaySeconds = 3f;

    private readonly Game1 _game;
    private readonly Level _level;
    private readonly Player _player;
    private readonly Camera _camera;
    private readonly Button _backButton = new("Back to Menu") { TextScale = 2 };
    private float elapsedTime;
    private bool timerRunning;
    private bool _debugDraw;
    private bool _levelComplete;
    private float _finalTime;
    private bool _newRecord;
    private float _completionReturnDelay;
    private float _completionUiElapsed;

    public GameScene(Game1 game)
    {
        _game = game;
        _level = LevelStorage.LoadOrCreateDefault();
        _player = new Player(_level.PlayerStart);
        _camera = new Camera(GetPlayerCenter());
        timerRunning = true;
    }

    public void Update(GameTime gameTime)
    {
        LayoutBackButton();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_levelComplete)
        {
            _completionReturnDelay = MathF.Max(0f, _completionReturnDelay - dt);
            _completionUiElapsed += dt;
            UpdateCamera(gameTime);

            if (CanReturnToMenu() && _backButton.Update(_game.Input))
            {
                _game.ChangeScene(new MenuScene(_game));
                return;
            }

            if (CanReturnToMenu() && _game.Input.ExitPressed)
            {
                _game.ChangeScene(new MenuScene(_game));
                return;
            }

            return;
        }

        if (_backButton.Update(_game.Input))
        {
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_game.Input.ExitPressed)
        {
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_game.Input.DebugTogglePressed)
        {
            _debugDraw = !_debugDraw;
        }

        if (timerRunning)
        {
            elapsedTime += dt;
        }

        _player.Update(gameTime, _game.Input, _level);

        if (IsPlayerTouchingGoal())
        {
            CompleteLevel();
        }

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
        _player.Draw(spriteBatch, _game.Pixel, _debugDraw);

        spriteBatch.End();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawTimer(spriteBatch, _game.Pixel, viewport);

        if (_levelComplete)
        {
            DrawCompletionUi(spriteBatch, _game.Pixel, viewport);
        }

        if (CanReturnToMenu())
        {
            _backButton.Draw(spriteBatch, _game.Pixel);
        }

        spriteBatch.End();
    }

    private void UpdateCamera(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float smoothing = 1f - MathF.Exp(-6f * dt);
        _camera.Position = Vector2.Lerp(_camera.Position, GetPlayerCenter(), smoothing);
    }

    private Vector2 GetPlayerCenter()
    {
        return _player.Position + (_player.Size * 0.5f);
    }

    private bool IsPlayerTouchingGoal()
    {
        Rectangle playerBounds = _player.Bounds;
        foreach (Goal goal in _level.Goals)
        {
            if (playerBounds.Intersects(goal.TriggerBounds))
            {
                return true;
            }
        }

        return false;
    }

    private void CompleteLevel()
    {
        if (_levelComplete)
        {
            return;
        }

        timerRunning = false;
        _levelComplete = true;
        _finalTime = BestTimeStorage.RoundToCentiseconds(elapsedTime);
        elapsedTime = _finalTime;
        _newRecord = BestTimeStorage.SaveIfRecord(LevelStorage.LevelId, _finalTime);
        _completionReturnDelay = CompletionReturnDelaySeconds;
        _completionUiElapsed = 0f;
    }

    private bool CanReturnToMenu()
    {
        return !_levelComplete || _completionReturnDelay <= 0f;
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
        string timeText = FormatTime(elapsedTime);
        int margin = Math.Max(8, (int)(viewport.Width * 0.03f));
        int scale = FitTextScale(
            timeText,
            GetResponsiveTextScale(viewport, 180, 2, 6),
            viewport.Width - (margin * 2));
        int y = Math.Max(8, (int)(viewport.Height * 0.035f));

        DrawCenteredText(spriteBatch, pixel, timeText, viewport.Width / 2 + 2, y + 2, scale, Color.Black * 0.45f);
        DrawCenteredText(spriteBatch, pixel, timeText, viewport.Width / 2, y, scale, Color.White);
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
