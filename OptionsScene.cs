#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game1_Monogame;

public sealed class OptionsScene : IScene
{
    private readonly Game1 _game;
    private ResolutionDropdown _resolutionDropdown = new();
    private Slider _volumeSlider = new("Music Volume", 0.75f, 0f, 1f);
    private Button _applyButton = new("Apply");
    private Button _cancelButton = new("Cancel");
    private Button _backButton = new("Back") { TextScale = 2 };

    private List<(string action, string keyName)> _controlBindings = new();

    private int? _rebindingIndex;
    private double _rebindingWaitTime;

    public OptionsScene(Game1 game)
    {
        _game = game;

        var pending = SettingsManager.PendingSettings;
        _resolutionDropdown.SelectedResolution = new Resolution(pending.ResolutionWidth, pending.ResolutionHeight);
        _volumeSlider.Value = pending.MusicVolume;

        // Load control bindings
        InitializeControlBindings();
    }

    private void InitializeControlBindings()
    {
        var actions = new[] { "MoveLeft", "MoveRight", "Jump", "FastFall", "Red", "Blue", "Green" };
        _controlBindings.Clear();

        foreach (string action in actions)
        {
            string keyName = SettingsManager.PendingSettings.Keybindings.ContainsKey(action)
                ? SettingsManager.PendingSettings.Keybindings[action]
                : "Unknown";
            _controlBindings.Add((action, keyName));
        }
    }

    public void Update(GameTime gameTime)
    {
        LayoutUI();

        // Handle rebinding
        if (_rebindingIndex.HasValue)
        {
            HandleRebinding(gameTime);
            return;
        }

        _resolutionDropdown.Update(_game.Input);
        _volumeSlider.Update(_game.Input);

        if (_backButton.Update(_game.Input) || _game.Input.ExitPressed)
        {
            SettingsManager.RevertPendingChanges();
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_cancelButton.Update(_game.Input))
        {
            SettingsManager.RevertPendingChanges();
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_applyButton.Update(_game.Input))
        {
            ApplySettings();
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        // Handle control binding clicks
        for (int i = 0; i < _controlBindings.Count; i++)
        {
            int yOffset = 350 + 40 + (i * 35);
            Rectangle bindingBounds = new(30, yOffset, 400, 30);
            if (_game.Input.LeftMousePressed && bindingBounds.Contains(_game.Input.MousePosition))
            {
                _rebindingIndex = i;
                _rebindingWaitTime = 0;
            }
        }

        // Update pending settings
        SettingsManager.PendingSettings.MusicVolume = _volumeSlider.Value;
        if (_resolutionDropdown.SelectedResolution != null)
        {
            SettingsManager.PendingSettings.ResolutionWidth = _resolutionDropdown.SelectedResolution.Width;
            SettingsManager.PendingSettings.ResolutionHeight = _resolutionDropdown.SelectedResolution.Height;
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutUI();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        Viewport viewport = _game.Viewport;
        Texture2D pixel = _game.Pixel;

        // Draw background
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(29, 34, 45));

        // Draw title
        Rectangle titleBounds = new(20, 20, viewport.Width - 40, 50);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "OPTIONS", titleBounds, 4, Color.White);

        // Draw sections
        DrawDisplaySettings(spriteBatch, pixel, viewport);
        DrawAudioSettings(spriteBatch, pixel, viewport);
        DrawControlSettings(spriteBatch, pixel, viewport);

        // Draw bottom panel and buttons
        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - 100, viewport.Width, 100), new Color(22, 26, 34));
        _backButton.Draw(spriteBatch, pixel);
        _applyButton.Draw(spriteBatch, pixel);
        _cancelButton.Draw(spriteBatch, pixel);

        spriteBatch.End();

        // Draw rebinding prompt if active
        if (_rebindingIndex.HasValue)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            DrawRebindingPrompt(spriteBatch, pixel, viewport);
            spriteBatch.End();
        }
    }

    private void DrawDisplaySettings(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int startY = 90;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "DISPLAY", new Vector2(30, startY), 3, Color.Yellow);

        _resolutionDropdown.Draw(spriteBatch, pixel);
    }

    private void DrawAudioSettings(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int startY = 250;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "AUDIO", new Vector2(30, startY), 3, Color.Yellow);

        _volumeSlider.Draw(spriteBatch, pixel);
    }

    private void DrawControlSettings(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int startY = 350;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "CONTROLS", new Vector2(30, startY), 3, Color.Yellow);

        int yOffset = startY + 40;
        for (int i = 0; i < _controlBindings.Count; i++)
        {
            var (action, keyName) = _controlBindings[i];
            Rectangle bindingBounds = new(30, yOffset + (i * 35), 400, 30);

            Color bgColor = _rebindingIndex == i ? new Color(100, 50, 50) : new Color(50, 60, 80);
            spriteBatch.Draw(pixel, bindingBounds, bgColor);
            DrawHelper.DrawBorder(spriteBatch, pixel, bindingBounds, new Color(80, 90, 110), 1);

            string displayAction = action switch
            {
                "MoveLeft" => "MOVE LEFT",
                "MoveRight" => "MOVE RIGHT",
                "Jump" => "JUMP",
                "FastFall" => "FAST FALL",
                "Red" => "RED",
                "Blue" => "BLUE",
                "Green" => "GREEN",
                _ => action
            };

            SimpleTextRenderer.DrawString(spriteBatch, pixel, $"{displayAction}:", new Vector2(bindingBounds.X + 10, bindingBounds.Y + 8), 2, Color.White);
            SimpleTextRenderer.DrawRight(spriteBatch, pixel, keyName, new Vector2(bindingBounds.Right - 10, bindingBounds.Y + 8), 2, Color.LightGray);
        }
    }

    private void DrawRebindingPrompt(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int width = 400;
        int height = 150;
        int x = (viewport.Width - width) / 2;
        int y = (viewport.Height - height) / 2;
        Rectangle promptBg = new(x, y, width, height);

        Color overlayColor = new Color(0, 0, 0, 150);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), overlayColor);

        spriteBatch.Draw(pixel, promptBg, new Color(35, 41, 55));
        DrawHelper.DrawBorder(spriteBatch, pixel, promptBg, new Color(100, 110, 130), 3);

        Rectangle textBounds = new(x + 20, y + 30, width - 40, 50);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "PRESS ANY KEY", textBounds, 3, Color.Yellow);

        Rectangle subBounds = new(x + 20, y + 85, width - 40, 40);
        string actionName = _rebindingIndex.HasValue && _rebindingIndex.Value < _controlBindings.Count
            ? _controlBindings[_rebindingIndex.Value].action
            : "Unknown";
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, $"({actionName})", subBounds, 2, Color.LightGray);
    }

    private void HandleRebinding(GameTime gameTime)
    {
        _rebindingWaitTime += gameTime.ElapsedGameTime.TotalMilliseconds;

        if (_rebindingWaitTime < 100)
            return;

        var keyboard = Keyboard.GetState();
        Keys[] pressedKeys = keyboard.GetPressedKeys();

        if (pressedKeys.Length > 0)
        {
            Keys newKey = pressedKeys[0];
            if (_rebindingIndex.HasValue && _rebindingIndex.Value < _controlBindings.Count)
            {
                var (action, _) = _controlBindings[_rebindingIndex.Value];
                _controlBindings[_rebindingIndex.Value] = (action, newKey.ToString());

                SettingsManager.PendingSettings.Keybindings[action] = newKey.ToString();
            }

            _rebindingIndex = null;
            _rebindingWaitTime = 0;
        }
    }

    private void LayoutUI()
    {
        Viewport viewport = _game.Viewport;

        // Resolution dropdown
        _resolutionDropdown.Bounds = new Rectangle(30, 135, 300, 40);

        // Volume slider
        _volumeSlider.Bounds = new Rectangle(30, 285, 300, 60);

        // Buttons at bottom
        const int buttonHeight = 50;
        const int buttonGap = 15;
        const int bottomMargin = 25;

        int buttonsY = viewport.Height - buttonHeight - bottomMargin;
        _backButton.Bounds = new Rectangle(25, buttonsY, 120, buttonHeight);

        var layout = ButtonRowLayout.Create(
            new[] { "Apply", "Cancel" },
            viewport.Width, viewport.Height,
            buttonHeight, 16, 12, buttonGap, bottomMargin);

        if (layout.ButtonBounds.Length >= 2)
        {
            _applyButton.Bounds = layout.ButtonBounds[0];
            _cancelButton.Bounds = layout.ButtonBounds[1];
        }
    }

    private void ApplySettings()
    {
        SettingsManager.SaveSettings(SettingsManager.PendingSettings);

        // Apply graphics changes
        var settings = SettingsManager.CurrentSettings;
        _game.ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight);
    }

    public void OnExit()
    {
    }
}
