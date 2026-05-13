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

    // Display settings
    private ResolutionDropdown _resolutionDropdown = new();
    private CycleSelector<DisplayMode> _displayModeSelector;

    // Audio settings
    private Slider _volumeSlider = new("Music Volume", 0.75f, 0f, 1f);

    // Buttons
    private Button _applyButton = new("Apply");
    private Button _cancelButton = new("Cancel");
    private Button _backButton = new("Back") { TextScale = 2 };

    // Control bindings
    private List<(string action, string keyName)> _controlBindings = new();
    private int? _rebindingIndex;
    private double _rebindingWaitTime;

    // Layout constants (tuned for tighter, centered composition)
    private const int LeftMargin = 30;
    private const int RightMargin = 30;
    private const int TopMargin = 108;
    private const int RowHeight = 32;
    private const int RowSpacing = 6;
    private const int SectionSpacing = 20;
    private const int MinContentWidth = 480;
    private const int MaxContentWidth = 820;
    private const int LabelWidth = 180;
    private const int ColumnGap = 18;
    private const int HeaderToContentSpacing = 16;
    private const int HeaderTopOffset = 34;
    private const int BottomPanelHeight = 86;
    private const int BottomMargin = 18;
    private const int ActionButtonWidth = 140;
    private const int ActionButtonGap = 18;
    private const int BackButtonWidth = 120;
    private const int ButtonHeight = 42;

    public int ControlWidth { get; private set; }

    public OptionsScene(Game1 game)
    {
        _game = game;

        // Initialize display mode cycle selector
        var displayModes = new List<DisplayMode>
        {
            DisplayMode.Fullscreen,
            DisplayMode.Windowed,
            DisplayMode.BorderlessWindowed
        };
        _displayModeSelector = new CycleSelector<DisplayMode>(displayModes, mode => mode.ToString());

        var pending = SettingsManager.PendingSettings;
        _resolutionDropdown.SelectedResolution = new Resolution(pending.ResolutionWidth, pending.ResolutionHeight);
        _resolutionDropdown.Label = string.Empty;
        _volumeSlider.Label = "MUSIC VOLUME";
        _volumeSlider.Value = pending.MusicVolume;

        // Initialize display mode from settings
        string displayMode = pending.DisplayMode.ToLower();
        DisplayMode initialMode = displayMode switch
        {
            "fullscreen" => DisplayMode.Fullscreen,
            "windowed" => DisplayMode.Windowed,
            "borderless" or "borderlesswindowed" => DisplayMode.BorderlessWindowed,
            _ => DisplayMode.BorderlessWindowed
        };
        _displayModeSelector.CurrentOption = initialMode;

        // Load control bindings
        InitializeControlBindings();
    }

    private void InitializeControlBindings()
    {
        var actions = new[] { "MoveLeft", "MoveRight", "Jump", "PullRope", "FastFall", "Red", "Blue", "Green" };
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

        _displayModeSelector.Update(_game.Input);
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
        LayoutMetrics layout = GetLayoutMetrics(_game.Viewport);
        for (int i = 0; i < _controlBindings.Count; i++)
        {
            int yOffset = layout.ControlListY + (i * (RowHeight + RowSpacing));
            Rectangle bindingBounds = new(layout.ContentX, yOffset, ControlWidth, RowHeight);
            if (_game.Input.LeftMousePressed && bindingBounds.Contains(_game.Input.MousePosition))
            {
                _rebindingIndex = i;
                _rebindingWaitTime = 0;
            }
        }

        // Update pending settings
        SettingsManager.PendingSettings.MusicVolume = _volumeSlider.Value;
        SettingsManager.PendingSettings.DisplayMode = _displayModeSelector.CurrentOption.ToString();
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
        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - BottomPanelHeight, viewport.Width, BottomPanelHeight), new Color(22, 26, 34));
        _backButton.Draw(spriteBatch, pixel);
        _applyButton.Draw(spriteBatch, pixel);
        _cancelButton.Draw(spriteBatch, pixel);

        // Draw resolution dropdown on top if expanded (for layering)
        if (_resolutionDropdown.IsExpanded)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 80));
            _resolutionDropdown.Draw(spriteBatch, pixel);
        }

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
        LayoutMetrics layout = GetLayoutMetrics(viewport);

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "DISPLAY SETTINGS", new Vector2(layout.ContentX, layout.DisplayHeaderY), 3, Color.Yellow);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "MODE", new Vector2(layout.LabelX, layout.DisplayModeY + 9), 2, Color.White);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "RESOLUTION", new Vector2(layout.LabelX, layout.ResolutionY + 9), 2, Color.White);

        // Draw the controls (aligned to control column)
        _displayModeSelector.Draw(spriteBatch, pixel);

        // Draw resolution dropdown header (body drawn on top if expanded)
        if (!_resolutionDropdown.IsExpanded)
        {
            _resolutionDropdown.Draw(spriteBatch, pixel);
        }
        else
        {
            Rectangle headerBounds = new(_resolutionDropdown.Bounds.X, _resolutionDropdown.Bounds.Y, _resolutionDropdown.Bounds.Width, 40);
            Color headerBg = new Color(62, 71, 90);
            spriteBatch.Draw(pixel, headerBounds, headerBg);
            DrawHelper.DrawBorder(spriteBatch, pixel, headerBounds, new Color(80, 90, 110), 2);

            string displayText = _resolutionDropdown.SelectedResolution?.ToString() ?? "Select...";
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, displayText, headerBounds, 2, Color.White);
            SimpleTextRenderer.DrawRight(spriteBatch, pixel, "▲", new Vector2(headerBounds.Right - 10, headerBounds.Y + 10), 2, Color.LightGray);
        }
    }

    private void DrawAudioSettings(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        LayoutMetrics layout = GetLayoutMetrics(viewport);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "AUDIO SETTINGS", new Vector2(layout.ContentX, layout.AudioHeaderY), 3, Color.Yellow);
        _volumeSlider.Draw(spriteBatch, pixel);
    }

    private void DrawControlSettings(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        LayoutMetrics layout = GetLayoutMetrics(viewport);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "CONTROL SETTINGS", new Vector2(layout.ContentX, layout.ControlHeaderY), 3, Color.Yellow);

        int yOffset = layout.ControlListY;
        for (int i = 0; i < _controlBindings.Count; i++)
        {
            var (action, keyName) = _controlBindings[i];
            int bindingY = yOffset + (i * (RowHeight + RowSpacing));
            Rectangle bindingBounds = new(layout.ContentX, bindingY, ControlWidth, RowHeight);

            Color bgColor = _rebindingIndex == i ? new Color(100, 50, 50) : new Color(50, 60, 80);
            spriteBatch.Draw(pixel, bindingBounds, bgColor);
            DrawHelper.DrawBorder(spriteBatch, pixel, bindingBounds, new Color(80, 90, 110), 1);

            string displayAction = action switch
            {
                "MoveLeft" => "MOVE LEFT",
                "MoveRight" => "MOVE RIGHT",
                "Jump" => "JUMP",
                "PullRope" => "PULL ROPE",
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
        LayoutMetrics layout = GetLayoutMetrics(viewport);

        _displayModeSelector.Bounds = new Rectangle(layout.ControlX, layout.DisplayModeY, layout.ControlWidth, RowHeight);
        _resolutionDropdown.Bounds = new Rectangle(layout.ControlX, layout.ResolutionY, layout.ControlWidth, RowHeight);
        _volumeSlider.Bounds = new Rectangle(layout.ControlX, layout.VolumeY, layout.ControlWidth, RowHeight + 18);

        ControlWidth = layout.ContentWidth;

        int buttonsY = viewport.Height - ButtonHeight - BottomMargin;
        _backButton.Bounds = new Rectangle(layout.ContentX, buttonsY, BackButtonWidth, ButtonHeight);
        _applyButton.Bounds = new Rectangle(layout.ActionButtonsX, buttonsY, ActionButtonWidth, ButtonHeight);
        _cancelButton.Bounds = new Rectangle(layout.ActionButtonsX + ActionButtonWidth + ActionButtonGap, buttonsY, ActionButtonWidth, ButtonHeight);
    }

    private LayoutMetrics GetLayoutMetrics(Viewport viewport)
    {
        int contentWidth = Math.Clamp(viewport.Width - LeftMargin - RightMargin, MinContentWidth, MaxContentWidth);
        int contentX = (viewport.Width - contentWidth) / 2;
        int labelX = contentX;
        int controlX = contentX + LabelWidth + ColumnGap;
        int controlWidth = contentWidth - LabelWidth - ColumnGap;

        int displayHeaderY = TopMargin - HeaderTopOffset;
        int displayModeY = displayHeaderY + HeaderToContentSpacing;
        int resolutionY = displayModeY + RowHeight + RowSpacing;

        int audioHeaderY = resolutionY + RowHeight + SectionSpacing;
        int volumeY = audioHeaderY + HeaderToContentSpacing;

        int controlHeaderY = volumeY + RowHeight + 18 + SectionSpacing;
        int controlListY = controlHeaderY + HeaderToContentSpacing;

        int actionsTotalWidth = (ActionButtonWidth * 2) + ActionButtonGap;
        int actionButtonsX = contentX + ((contentWidth - actionsTotalWidth) / 2);

        return new LayoutMetrics(
            contentX,
            contentWidth,
            labelX,
            controlX,
            controlWidth,
            displayHeaderY,
            displayModeY,
            resolutionY,
            audioHeaderY,
            volumeY,
            controlHeaderY,
            controlListY,
            actionButtonsX);
    }

    private readonly record struct LayoutMetrics(
        int ContentX,
        int ContentWidth,
        int LabelX,
        int ControlX,
        int ControlWidth,
        int DisplayHeaderY,
        int DisplayModeY,
        int ResolutionY,
        int AudioHeaderY,
        int VolumeY,
        int ControlHeaderY,
        int ControlListY,
        int ActionButtonsX);

    private void ApplySettings()
    {
        SettingsManager.SaveSettings(SettingsManager.PendingSettings);
        _game.Input.ReloadProfilesFromSettings();

        // Apply graphics changes
        var settings = SettingsManager.CurrentSettings;
        _game.ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode);
    }

    public void OnExit()
    {
    }
}
