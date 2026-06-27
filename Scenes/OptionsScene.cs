#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class OptionsScene : IScene
{
    private readonly ColorBlocksGame _game;

    // Display settings
    private ResolutionDropdown _resolutionDropdown = new();
    private CycleSelector<DisplayMode> _displayModeSelector;
    private CycleSelector<int> _fpsLimitSelector;

    // Audio settings
    private Slider _volumeSlider = new("Music Volume", 0.75f, 0f, 1f);

    // Buttons
    private Button _applyButton = new("Apply");
    private Button _backButton = new("Back");
    private readonly UIFocusManager _focus = new();
    private readonly FocusableCycleSelector<DisplayMode> _displayModeFocus;
    private readonly FocusableCycleSelector<int> _fpsLimitFocus;
    private readonly FocusableSlider _volumeFocus;
    private readonly FocusableResolutionDropdown _resolutionFocus;
    private readonly FocusableButton _applyFocus;
    private readonly FocusableButton _backFocus;

    // Control bindings
    private List<(string action, string keyName, string gamepadName)> _controlBindings = new();
    private readonly List<FocusableAction> _bindingFocusables = new();
    private int? _rebindingIndex;
    private RebindKind _rebindingKind = RebindKind.Keyboard;
    private double _rebindingWaitTime;

    private enum RebindKind
    {
        Keyboard,
        Gamepad
    }

    private const int MinPanelWidth = 920;
    private const int MaxPanelWidth = 1180;
    private const int MaxPanelHeight = 820;
    private const int TitleHeight = 44;
    private const int TopSectionHeight = 246;
    private const int CompactTopSectionHeight = 232;
    private const int RowHeight = 46;
    private const int CompactRowHeight = 44;
    private const int KeyRowHeight = 42;
    private const int CompactKeyRowHeight = 40;
    private const int KeyRowGap = 8;
    private const int CompactKeyRowGap = 6;
    private const int LabelToControlGap = 18;
    private const int TopSectionGap = 28;
    private const int ButtonHeight = 46;
    private const int ButtonGap = 18;
    private const int SectionTitleScale = 3;
    private const int LabelScale = 2;

    private static readonly Color BackgroundTop = new(18, 23, 33);
    private static readonly Color BackgroundBottom = new(13, 16, 24);
    private static readonly Color PanelFill = new(30, 38, 53, 242);
    private static readonly Color PanelBorder = new(101, 116, 143);
    private static readonly Color SectionFill = new(38, 48, 67, 228);
    private static readonly Color SectionBorder = new(76, 91, 119);
    private static readonly Color Accent = new(255, 226, 122);
    private static readonly Color LabelColor = new(217, 225, 238);
    private static readonly Color MutedLabelColor = new(167, 181, 204);

    public int ControlWidth { get; private set; }

    public OptionsScene(ColorBlocksGame game)
    {
        _game = game;

        var displayModes = new List<DisplayMode>
        {
            DisplayMode.Fullscreen,
            DisplayMode.Windowed,
            DisplayMode.BorderlessWindowed
        };
        _displayModeSelector = new CycleSelector<DisplayMode>(displayModes, FormatDisplayMode);

        var fpsOptions = new List<int> { -1, 0, 30, 60, 120, 144, 240 };
        _fpsLimitSelector = new CycleSelector<int>(fpsOptions, FormatFpsLimit);

        var pending = SettingsManager.PendingSettings;
        _resolutionDropdown.SelectedResolution = new Resolution(pending.ResolutionWidth, pending.ResolutionHeight);
        _resolutionDropdown.Label = string.Empty;
        _volumeSlider.Label = string.Empty;
        _volumeSlider.Value = pending.MusicVolume;

        _backButton.TextScale = 3;
        _applyButton.TextScale = 3;
        _applyButton.FillColor = new Color(74, 111, 93);
        _applyButton.HoverFillColor = new Color(94, 140, 116);
        _applyButton.BorderColor = new Color(154, 213, 181);
        _applyButton.HoverBorderColor = new Color(232, 255, 238);

        string displayMode = pending.DisplayMode.ToLower();
        DisplayMode initialMode = displayMode switch
        {
            "fullscreen" => DisplayMode.Fullscreen,
            "windowed" => DisplayMode.Windowed,
            "borderless" or "borderlesswindowed" => DisplayMode.BorderlessWindowed,
            _ => DisplayMode.BorderlessWindowed
        };
        _displayModeSelector.CurrentOption = initialMode;
        _fpsLimitSelector.CurrentOption = pending.FpsLimit;

        _displayModeFocus = new FocusableCycleSelector<DisplayMode>(_displayModeSelector);
        _fpsLimitFocus = new FocusableCycleSelector<int>(_fpsLimitSelector);
        _volumeFocus = new FocusableSlider(_volumeSlider);
        _resolutionFocus = new FocusableResolutionDropdown(_resolutionDropdown);
        _resolutionDropdown.RefreshSupportedResolutions(_game.GraphicsDevice);
        _applyFocus = new FocusableButton(_applyButton);
        _backFocus = new FocusableButton(_backButton);

        _focus.ResetFocus();
        InitializeControlBindings();
    }

    private void InitializeControlBindings()
    {
        var actions = new[] { "MoveLeft", "MoveRight", "Jump", "Respawn", "PullRope", "FastFall", "Red", "Blue", "Green" };
        _controlBindings.Clear();

        foreach (string action in actions)
        {
            string keyName = SettingsManager.PendingSettings.Keybindings.ContainsKey(action)
                ? SettingsManager.PendingSettings.Keybindings[action]
                : "Unknown";
            string gamepadName = Enum.TryParse(action, out GameplayInputAction gameplayAction)
                ? GamepadDefaults.GetGamepadDisplayName(gameplayAction, SettingsManager.PendingSettings.GamepadBindings)
                : "—";
            _controlBindings.Add((action, keyName, gamepadName));
        }
    }

    public void Update(GameTime gameTime)
    {
        LayoutUI();

        if (_rebindingIndex.HasValue)
        {
            HandleRebinding(gameTime);
            return;
        }

        if ((_game.Input.ExitPressed || _game.Input.MenuCancelPressed) && !_focus.IsCapturingNavigation)
        {
            SettingsManager.RevertPendingChanges();
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        RebuildFocus(gameTime);

        if (_backFocus.WasActivated)
        {
            SettingsManager.RevertPendingChanges();
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_applyFocus.WasActivated)
        {
            SyncPendingSettings();
            ApplySettings();
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        SyncPendingSettings();
    }

    private void RebuildFocus(GameTime gameTime)
    {
        LayoutMetrics layout = GetLayoutMetrics(_game.Viewport);
        _bindingFocusables.Clear();
        _focus.Clear();

        // Display column (visual order: MODE, RESOLUTION, FPS LIMIT)
        int displayModeIndex = _focus.Add(_displayModeFocus, "DisplayMode");
        int resolutionIndex = _focus.Add(_resolutionFocus, "Resolution");
        int fpsIndex = _focus.Add(_fpsLimitFocus, "FpsLimit");

        // Audio column (right of display)
        int volumeIndex = _focus.Add(_volumeFocus, "MusicVolume");

        // Control bindings: two focusable cells per action (keyboard, gamepad)
        var keyIndices = new List<int>();
        var padIndices = new List<int>();
        for (int i = 0; i < _controlBindings.Count; i++)
        {
            int captured = i;
            string action = _controlBindings[i].action;

            var keyFocus = new FocusableAction(
                () => GetBindingCellBounds(layout, captured, BindingCell.Keyboard),
                () => BeginRebind(captured, RebindKind.Keyboard));
            var padFocus = new FocusableAction(
                () => GetBindingCellBounds(layout, captured, BindingCell.Gamepad),
                () => BeginRebind(captured, RebindKind.Gamepad));

            _bindingFocusables.Add(keyFocus);
            _bindingFocusables.Add(padFocus);

            keyIndices.Add(_focus.Add(keyFocus, $"{action}Keyboard"));
            padIndices.Add(_focus.Add(padFocus, $"{action}Gamepad"));
        }

        int applyIndex = _focus.Add(_applyFocus, "Apply");
        int backIndex = _focus.Add(_backFocus, "Back");

        NavigationGraph nav = _focus.Navigation;

        // Display column: MODE -> RESOLUTION -> FPS -> AUDIO -> CONTROLS
        nav.LinkVertical(displayModeIndex, resolutionIndex);
        nav.LinkVertical(resolutionIndex, fpsIndex);
        nav.LinkVertical(fpsIndex, volumeIndex);
        nav.LinkHorizontal(resolutionIndex, volumeIndex);

        if (keyIndices.Count > 0)
        {
            nav.LinkVertical(volumeIndex, keyIndices[0]);

            for (int i = 0; i < keyIndices.Count; i++)
            {
                nav.LinkHorizontal(keyIndices[i], padIndices[i]);

                if (i < keyIndices.Count - 1)
                {
                    nav.LinkVertical(keyIndices[i], keyIndices[i + 1]);
                    nav.LinkVertical(padIndices[i], padIndices[i + 1]);
                }
            }

            int last = keyIndices.Count - 1;
            nav.LinkVertical(keyIndices[last], applyIndex);
            nav.LinkVertical(padIndices[last], applyIndex);
            nav.Link(applyIndex, NavigationDirection.Up, keyIndices[last]);
            nav.Link(backIndex, NavigationDirection.Up, padIndices[last]);
        }
        else
        {
            nav.LinkVertical(volumeIndex, applyIndex);
        }

        nav.LinkHorizontal(backIndex, applyIndex);

        _focus.FinalizeFocus("DisplayMode");
        _focus.Update(gameTime, _game.Input);
    }

    private bool BeginRebind(int index, RebindKind kind)
    {
        if (kind == RebindKind.Gamepad)
        {
            if (!Enum.TryParse(_controlBindings[index].action, out GameplayInputAction action)
                || !GamepadDefaults.IsButtonRebindable(action))
            {
                return false;
            }
        }

        _rebindingIndex = index;
        _rebindingKind = kind;
        _rebindingWaitTime = 0;
        return true;
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutUI();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        Viewport viewport = _game.Viewport;
        Texture2D pixel = _game.Pixel;
        LayoutMetrics layout = GetLayoutMetrics(viewport);

        DrawBackground(spriteBatch, pixel, viewport);
        DrawMainPanel(spriteBatch, pixel, layout.PanelBounds);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "OPTIONS", layout.TitleBounds, layout.TitleScale, Color.White);

        DrawDisplaySettings(spriteBatch, pixel, layout);
        DrawAudioSettings(spriteBatch, pixel, layout);
        DrawControlSettings(spriteBatch, pixel, layout);

        DrawButtonTray(spriteBatch, pixel, layout);
        _backButton.Draw(spriteBatch, pixel);
        _applyButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        if (_resolutionDropdown.IsExpanded)
        {
            _resolutionDropdown.Draw(spriteBatch, pixel);
        }

        spriteBatch.End();

        if (_rebindingIndex.HasValue)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            DrawRebindingPrompt(spriteBatch, pixel, viewport);
            spriteBatch.End();
        }
    }

    private void DrawBackground(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), BackgroundBottom);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height / 2), BackgroundTop);

        int bandHeight = Math.Max(2, viewport.Height / 160);
        for (int y = viewport.Height / 5; y < viewport.Height; y += Math.Max(40, viewport.Height / 14))
        {
            spriteBatch.Draw(pixel, new Rectangle(0, y, viewport.Width, bandHeight), new Color(46, 58, 80, 38));
        }
    }

    private void DrawMainPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panel)
    {
        spriteBatch.Draw(pixel, new Rectangle(panel.X + 10, panel.Y + 12, panel.Width, panel.Height), new Color(2, 4, 8, 135));
        spriteBatch.Draw(pixel, panel, PanelFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, PanelBorder, 3);
        DrawHelper.DrawBorder(spriteBatch, pixel, new Rectangle(panel.X + 7, panel.Y + 7, panel.Width - 14, panel.Height - 14), new Color(53, 65, 88), 1);

        spriteBatch.Draw(pixel, new Rectangle(panel.X + 3, panel.Y + 3, panel.Width - 6, 2), Accent);
        int cornerLength = Math.Min(42, panel.Width / 12);
        spriteBatch.Draw(pixel, new Rectangle(panel.X + 10, panel.Y + 10, cornerLength, 3), new Color(151, 173, 207));
        spriteBatch.Draw(pixel, new Rectangle(panel.Right - 10 - cornerLength, panel.Y + 10, cornerLength, 3), new Color(151, 173, 207));
    }

    private void DrawDisplaySettings(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout)
    {
        DrawSection(spriteBatch, pixel, layout, layout.DisplaySectionBounds, "DISPLAY SETTINGS");

        DrawSettingLabel(spriteBatch, pixel, "MODE", layout.DisplayLabelBounds, layout.DisplayModeBounds);
        DrawSettingLabel(spriteBatch, pixel, "RESOLUTION", layout.DisplayLabelBounds, layout.ResolutionBounds);
        DrawSettingLabel(spriteBatch, pixel, "FPS LIMIT", layout.DisplayLabelBounds, layout.FpsLimitBounds);

        _displayModeSelector.Draw(spriteBatch, pixel);
        _fpsLimitSelector.Draw(spriteBatch, pixel);

        if (!_resolutionDropdown.IsExpanded)
        {
            _resolutionDropdown.Draw(spriteBatch, pixel);
        }
    }

    private void DrawAudioSettings(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout)
    {
        DrawSection(spriteBatch, pixel, layout, layout.AudioSectionBounds, "AUDIO SETTINGS");
        DrawSettingLabel(spriteBatch, pixel, "MUSIC VOLUME", layout.AudioLabelBounds, layout.VolumeBounds);
        _volumeSlider.Draw(spriteBatch, pixel);
    }

    private void DrawControlSettings(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout)
    {
        DrawSection(spriteBatch, pixel, layout, layout.ControlSectionBounds, "CONTROL SETTINGS");
        DrawControlTableHeader(spriteBatch, pixel, layout);

        bool allowHover = _game.Input.Navigation.AllowPointerHoverVisual;
        Point pointer = _game.Input.UiPointerPosition;

        for (int i = 0; i < _controlBindings.Count; i++)
        {
            Rectangle rowBounds = GetControlBindingBounds(layout, i);
            GetBindingCellRects(layout, i, out Rectangle labelBounds, out Rectangle keyBounds, out Rectangle padBounds);

            bool isAlternate = i % 2 == 1;
            bool rebindingKey = _rebindingIndex == i && _rebindingKind == RebindKind.Keyboard;
            bool rebindingPad = _rebindingIndex == i && _rebindingKind == RebindKind.Gamepad;
            bool rowHovered = allowHover && rowBounds.Contains(pointer);

            Color rowFill = isAlternate ? new Color(35, 44, 62) : new Color(39, 50, 70);
            spriteBatch.Draw(pixel, rowBounds, rowFill);
            if (rowHovered)
            {
                DrawHelper.DrawBorder(spriteBatch, pixel, rowBounds, Accent, 2);
            }

            DrawColumnDivider(spriteBatch, pixel, layout, rowBounds.Y, rowBounds.Height);

            string keyText = rebindingKey ? "PRESS KEY" : FormatKeyName(_controlBindings[i].keyName);
            string padText = rebindingPad ? "PRESS BTN" : _controlBindings[i].gamepadName;
            DrawBindingBox(spriteBatch, pixel, keyBounds, rebindingKey, keyText);
            DrawBindingBox(spriteBatch, pixel, padBounds, rebindingPad, padText);

            string displayAction = FormatActionName(_controlBindings[i].action);
            DrawFittedLeft(spriteBatch, pixel, displayAction, labelBounds, 2, LabelColor);
        }
    }

    private static void DrawControlTableHeader(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout)
    {
        Rectangle header = layout.ControlHeaderBounds;
        spriteBatch.Draw(pixel, header, new Color(32, 42, 58));
        DrawHelper.DrawBorder(spriteBatch, pixel, header, new Color(91, 108, 139), 1);

        Rectangle actionHeader = GetColumnHeaderBounds(layout, 0);
        Rectangle keyboardHeader = GetColumnHeaderBounds(layout, 1);
        Rectangle gamepadHeader = GetColumnHeaderBounds(layout, 2);

        DrawFittedLeft(spriteBatch, pixel, "ACTION", actionHeader, 1, MutedLabelColor);
        DrawFittedCentered(spriteBatch, pixel, "KEYBOARD", keyboardHeader, 1, MutedLabelColor);
        DrawFittedCentered(spriteBatch, pixel, "GAMEPAD", gamepadHeader, 1, MutedLabelColor);

        DrawColumnDivider(spriteBatch, pixel, layout, header.Y, header.Height);
    }

    private static void DrawColumnDivider(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout, int y, int height)
    {
        int x1 = layout.ControlRowsArea.X + layout.ControlActionColumnWidth + (layout.ControlColumnGap / 2);
        int x2 = x1 + layout.ControlKeyboardColumnWidth + layout.ControlColumnGap;
        int dividerWidth = Math.Max(1, layout.ControlColumnGap / 3);
        Color color = new Color(75, 89, 116, 120);
        spriteBatch.Draw(pixel, new Rectangle(x1, y + 2, dividerWidth, Math.Max(1, height - 4)), color);
        spriteBatch.Draw(pixel, new Rectangle(x2, y + 2, dividerWidth, Math.Max(1, height - 4)), color);
    }

    private static Rectangle GetColumnHeaderBounds(LayoutMetrics layout, int column)
    {
        GetColumnBounds(layout, layout.ControlHeaderBounds.Y, layout.ControlHeaderBounds.Height, column, out Rectangle bounds);
        int padH = layout.ControlCellPaddingH;
        int padV = layout.ControlCellPaddingV;
        return new Rectangle(bounds.X + padH, bounds.Y + padV, Math.Max(1, bounds.Width - (padH * 2)), Math.Max(1, bounds.Height - (padV * 2)));
    }

    private static void GetColumnBounds(LayoutMetrics layout, int rowY, int rowHeight, int column, out Rectangle bounds)
    {
        int x = layout.ControlRowsArea.X;
        int gap = layout.ControlColumnGap;
        int actionW = layout.ControlActionColumnWidth;
        int keyboardW = layout.ControlKeyboardColumnWidth;
        int gamepadW = layout.ControlGamepadColumnWidth;

        bounds = column switch
        {
            1 => new Rectangle(x + actionW + gap, rowY, keyboardW, rowHeight),
            2 => new Rectangle(x + actionW + gap + keyboardW + gap, rowY, gamepadW, rowHeight),
            _ => new Rectangle(x, rowY, actionW, rowHeight)
        };
    }

    private static void DrawBindingBox(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        bool highlighted,
        string value)
    {
        spriteBatch.Draw(pixel, bounds, highlighted ? new Color(121, 76, 78) : new Color(25, 32, 47));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, highlighted ? Accent : new Color(106, 122, 154), highlighted ? 2 : 1);

        int padX = Math.Max(10, bounds.Width / 10);
        int padY = Math.Max(6, bounds.Height / 6);
        Rectangle valueBounds = new(
            bounds.X + padX,
            bounds.Y + padY,
            Math.Max(1, bounds.Width - (padX * 2)),
            Math.Max(1, bounds.Height - (padY * 2)));
        DrawFittedCentered(spriteBatch, pixel, value, valueBounds, 2, highlighted ? Accent : Color.White);
    }

    private void DrawButtonTray(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout)
    {
        int trayPaddingX = 18;
        int trayPaddingY = 10;
        Rectangle trayBounds = new(
            layout.BackButtonBounds.X - trayPaddingX,
            layout.BackButtonBounds.Y - trayPaddingY,
            layout.ApplyButtonBounds.Right - layout.BackButtonBounds.X + (trayPaddingX * 2),
            layout.BackButtonBounds.Height + (trayPaddingY * 2));

        spriteBatch.Draw(pixel, trayBounds, new Color(22, 29, 42, 205));
        DrawHelper.DrawBorder(spriteBatch, pixel, trayBounds, new Color(63, 78, 104), 2);
    }

    private void DrawSection(SpriteBatch spriteBatch, Texture2D pixel, LayoutMetrics layout, Rectangle bounds, string title)
    {
        spriteBatch.Draw(pixel, bounds, SectionFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, SectionBorder, 2);

        Vector2 titlePosition = new(bounds.X + layout.SectionPadding, bounds.Y + layout.SectionPadding);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, title, titlePosition, SectionTitleScale, Accent);

        int dividerY = bounds.Y + layout.SectionPadding + layout.SectionTitleHeight + (layout.SectionTitleSpacing / 2);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X + layout.SectionPadding, dividerY, bounds.Width - (layout.SectionPadding * 2), 2), new Color(91, 108, 139));
    }

    private void DrawSettingLabel(SpriteBatch spriteBatch, Texture2D pixel, string text, Rectangle labelColumn, Rectangle rowBounds)
    {
        Rectangle textBounds = new(labelColumn.X, rowBounds.Y, labelColumn.Width, rowBounds.Height);
        DrawFittedLeft(spriteBatch, pixel, text, textBounds, LabelScale, MutedLabelColor);
    }

    private void DrawRebindingPrompt(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int width = Math.Clamp(viewport.Width / 3, 400, 560);
        int height = 166;
        int x = (viewport.Width - width) / 2;
        int y = (viewport.Height - height) / 2;
        Rectangle promptBg = new(x, y, width, height);

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 165));
        spriteBatch.Draw(pixel, new Rectangle(promptBg.X + 8, promptBg.Y + 9, promptBg.Width, promptBg.Height), new Color(2, 4, 8, 150));
        spriteBatch.Draw(pixel, promptBg, new Color(35, 43, 60));
        DrawHelper.DrawBorder(spriteBatch, pixel, promptBg, Accent, 3);

        Rectangle textBounds = new(x + 24, y + 34, width - 48, 46);
        string prompt = _rebindingKind == RebindKind.Gamepad ? "PRESS A BUTTON" : "PRESS ANY KEY";
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, prompt, textBounds, 3, Accent);

        Rectangle subBounds = new(x + 24, y + 94, width - 48, 34);
        string actionName = _rebindingIndex.HasValue && _rebindingIndex.Value < _controlBindings.Count
            ? FormatActionName(_controlBindings[_rebindingIndex.Value].action)
            : "UNKNOWN";
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, actionName, subBounds, 2, LabelColor);
    }

    private void HandleRebinding(GameTime gameTime)
    {
        _rebindingWaitTime += gameTime.ElapsedGameTime.TotalMilliseconds;

        if (_rebindingWaitTime < 100)
            return;

        if (!_rebindingIndex.HasValue || _rebindingIndex.Value >= _controlBindings.Count)
        {
            _rebindingIndex = null;
            _rebindingWaitTime = 0;
            return;
        }

        // Escape always cancels the current rebind without changing anything.
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            _rebindingIndex = null;
            _rebindingWaitTime = 0;
            return;
        }

        if (_rebindingKind == RebindKind.Keyboard)
        {
            HandleKeyboardRebinding();
        }
        else
        {
            HandleGamepadRebinding();
        }
    }

    private void HandleKeyboardRebinding()
    {
        Keys[] pressedKeys = Keyboard.GetState().GetPressedKeys();
        if (pressedKeys.Length == 0)
        {
            return;
        }

        Keys newKey = pressedKeys[0];
        var (action, _, gamepadName) = _controlBindings[_rebindingIndex!.Value];
        _controlBindings[_rebindingIndex.Value] = (action, newKey.ToString(), gamepadName);
        SettingsManager.PendingSettings.Keybindings[action] = newKey.ToString();

        _rebindingIndex = null;
        _rebindingWaitTime = 0;
    }

    private void HandleGamepadRebinding()
    {
        var (action, keyName, _) = _controlBindings[_rebindingIndex!.Value];
        if (!Enum.TryParse(action, out GameplayInputAction gameplayAction))
        {
            _rebindingIndex = null;
            _rebindingWaitTime = 0;
            return;
        }

        for (int device = 0; device < InputManager.MaxLocalPlayers; device++)
        {
            if (!_game.Input.IsGamepadConnected(device))
            {
                continue;
            }

            foreach (Buttons button in GamepadDefaults.CaptureButtons)
            {
                if (!_game.Input.WasGamepadPressed(device, button))
                {
                    continue;
                }

                SettingsManager.PendingSettings.GamepadBindings[action] = button.ToString();
                _controlBindings[_rebindingIndex.Value] =
                    (action, keyName, GamepadDefaults.GetGamepadDisplayName(gameplayAction, SettingsManager.PendingSettings.GamepadBindings));

                _rebindingIndex = null;
                _rebindingWaitTime = 0;
                return;
            }
        }
    }

    private void LayoutUI()
    {
        LayoutMetrics layout = GetLayoutMetrics(_game.Viewport);

        _displayModeSelector.Bounds = layout.DisplayModeBounds;
        _resolutionDropdown.Bounds = layout.ResolutionBounds;
        _resolutionDropdown.OpenUpwards = false;
        _fpsLimitSelector.Bounds = layout.FpsLimitBounds;
        _volumeSlider.Bounds = layout.VolumeBounds;

        ControlWidth = layout.ControlSectionBounds.Width;

        _backButton.Bounds = layout.BackButtonBounds;
        _applyButton.Bounds = layout.ApplyButtonBounds;
    }

    private LayoutMetrics GetLayoutMetrics(Viewport viewport)
    {
        int outerMarginX = Math.Clamp(viewport.Width / 32, 32, 96);
        int outerMarginY = Math.Clamp(viewport.Height / 18, 28, 72);
        int maxWidth = Math.Max(1, Math.Min(MaxPanelWidth, viewport.Width - (outerMarginX * 2)));
        int minWidth = Math.Min(MinPanelWidth, maxWidth);
        int preferredWidth = (int)(viewport.Width * 0.88f);
        int panelWidth = Math.Clamp(preferredWidth, minWidth, maxWidth);

        int maxPanelHeight = Math.Max(480, viewport.Height - (outerMarginY * 2));
        int bindingCount = Math.Max(1, _controlBindings.Count);

        // Pass 1: estimate compactness from viewport before final panel height is known.
        bool compact = viewport.Height < 760;
        int panelPadding = Math.Clamp(panelWidth / 44, compact ? 24 : 30, compact ? 30 : 38);
        int sectionPadding = compact ? 18 : 22;
        int sectionTitleHeight = SimpleTextRenderer.MeasureString("DISPLAY SETTINGS", SectionTitleScale).Y;
        int sectionTitleSpacing = compact ? 16 : 24;
        int sectionGap = compact ? 16 : 28;
        int titleToSectionsGap = compact ? 16 : 28;
        int rowHeight = compact ? CompactRowHeight : RowHeight;
        int keyRowGap = compact ? CompactKeyRowGap : KeyRowGap;
        int topSectionHeight = compact ? CompactTopSectionHeight : TopSectionHeight;
        int titleScale = compact ? 4 : 5;
        int preferredRowHeight = compact ? CompactKeyRowHeight : KeyRowHeight;

        int controlHeaderHeight = compact ? 26 : 30;
        int controlHeaderGap = compact ? 4 : 6;
        int controlChromeHeight = (sectionPadding * 2) + sectionTitleHeight + sectionTitleSpacing
            + controlHeaderHeight + controlHeaderGap;

        int topBlockHeight = TitleHeight + titleToSectionsGap + topSectionHeight + sectionGap;
        int buttonBlockHeight = ButtonHeight + sectionGap;

        int comfortableDataHeight = (bindingCount * preferredRowHeight) + ((bindingCount - 1) * keyRowGap);
        int desiredContentHeight = topBlockHeight + controlChromeHeight + comfortableDataHeight + buttonBlockHeight;
        int desiredPanelHeight = desiredContentHeight + (panelPadding * 2);
        int panelHeight = Math.Clamp(desiredPanelHeight, 520, maxPanelHeight);

        // Pass 2: if viewport caps panel height, shrink row height so table always fits inside section.
        int contentHeight = panelHeight - (panelPadding * 2);
        int availableControlHeight = contentHeight - topBlockHeight - buttonBlockHeight;
        int dataAreaHeight = Math.Max(1, availableControlHeight - controlChromeHeight);
        int keyRowHeight = (dataAreaHeight - ((bindingCount - 1) * keyRowGap)) / bindingCount;
        keyRowHeight = Math.Min(preferredRowHeight, Math.Max(1, keyRowHeight));

        int actualDataHeight = (bindingCount * keyRowHeight) + ((bindingCount - 1) * keyRowGap);
        int controlSectionHeight = controlChromeHeight + actualDataHeight;

        int panelX = (viewport.Width - panelWidth) / 2;
        int panelY = (viewport.Height - panelHeight) / 2;
        Rectangle panelBounds = new(panelX, panelY, panelWidth, panelHeight);

        Rectangle contentBounds = new(
            panelBounds.X + panelPadding,
            panelBounds.Y + panelPadding,
            panelBounds.Width - (panelPadding * 2),
            contentHeight);

        Rectangle titleBounds = new(contentBounds.X, contentBounds.Y, contentBounds.Width, TitleHeight);
        int topSectionY = titleBounds.Bottom + titleToSectionsGap;
        int topSectionWidth = (contentBounds.Width - TopSectionGap) / 2;
        Rectangle displaySectionBounds = new(contentBounds.X, topSectionY, topSectionWidth, topSectionHeight);
        Rectangle audioSectionBounds = new(displaySectionBounds.Right + TopSectionGap, topSectionY, topSectionWidth, topSectionHeight);

        int controlSectionY = displaySectionBounds.Bottom + sectionGap;
        Rectangle controlSectionBounds = new(
            contentBounds.X,
            controlSectionY,
            contentBounds.Width,
            controlSectionHeight);

        int buttonWidth = Math.Clamp(contentBounds.Width / 5, 128, 150);
        int buttonTotalWidth = (buttonWidth * 2) + ButtonGap;
        int buttonY = controlSectionBounds.Bottom + sectionGap;
        int buttonX = panelBounds.Center.X - (buttonTotalWidth / 2);
        Rectangle backButtonBounds = new(buttonX, buttonY, buttonWidth, ButtonHeight);
        Rectangle applyButtonBounds = new(backButtonBounds.Right + ButtonGap, buttonY, buttonWidth, ButtonHeight);

        CreateSettingColumnLayout(displaySectionBounds, sectionPadding, out Rectangle displayLabelBounds, out Rectangle displayControlBounds);
        CreateSettingColumnLayout(audioSectionBounds, sectionPadding, out Rectangle audioLabelBounds, out Rectangle audioControlBounds);

        int rowStartY = displaySectionBounds.Y + sectionPadding + sectionTitleHeight + sectionTitleSpacing;
        Rectangle displayModeBounds = new(displayControlBounds.X, rowStartY, displayControlBounds.Width, rowHeight);
        Rectangle resolutionBounds = new(displayControlBounds.X, rowStartY + rowHeight + 10, displayControlBounds.Width, rowHeight);
        Rectangle fpsLimitBounds = new(displayControlBounds.X, rowStartY + ((rowHeight + 10) * 2), displayControlBounds.Width, rowHeight);
        Rectangle volumeBounds = new(audioControlBounds.X, rowStartY, audioControlBounds.Width, rowHeight);

        int controlRowsY = controlSectionBounds.Y + sectionPadding + sectionTitleHeight + sectionTitleSpacing;
        int controlRowsHeight = controlHeaderHeight + controlHeaderGap + actualDataHeight;
        int controlInnerWidth = controlSectionBounds.Width - (sectionPadding * 2);

        int tableWidth = controlInnerWidth;
        int controlColumnGap = Math.Max(8, tableWidth / 64);
        int columnsWidth = Math.Max(1, tableWidth - (controlColumnGap * 2));
        int controlActionColumnWidth = (int)(columnsWidth * 0.45f);
        int controlKeyboardColumnWidth = (int)(columnsWidth * 0.27f);
        int controlGamepadColumnWidth = columnsWidth - controlActionColumnWidth - controlKeyboardColumnWidth;
        int controlCellPaddingH = Math.Clamp(tableWidth / 80, 8, 14);
        int controlCellPaddingV = Math.Clamp(keyRowHeight / 8, 3, 6);

        Rectangle controlRowsArea = new(controlSectionBounds.X + sectionPadding, controlRowsY, tableWidth, controlRowsHeight);
        Rectangle controlHeaderBounds = new(
            controlRowsArea.X,
            controlRowsArea.Y,
            controlRowsArea.Width,
            controlHeaderHeight);

        int controlColumns = 1;
        int controlRowsPerColumn = bindingCount;
        int controlColumnWidth = tableWidth;

        return new LayoutMetrics(
            panelBounds,
            contentBounds,
            titleBounds,
            titleScale,
            sectionPadding,
            sectionTitleHeight,
            sectionTitleSpacing,
            displaySectionBounds,
            audioSectionBounds,
            controlSectionBounds,
            displayLabelBounds,
            displayControlBounds,
            audioLabelBounds,
            audioControlBounds,
            displayModeBounds,
            resolutionBounds,
            fpsLimitBounds,
            volumeBounds,
            controlRowsArea,
            controlColumns,
            controlRowsPerColumn,
            controlColumnWidth,
            controlColumnGap,
            keyRowHeight,
            keyRowGap,
            controlHeaderBounds,
            controlHeaderGap,
            controlActionColumnWidth,
            controlKeyboardColumnWidth,
            controlGamepadColumnWidth,
            controlCellPaddingH,
            controlCellPaddingV,
            backButtonBounds,
            applyButtonBounds);
    }

    private static void CreateSettingColumnLayout(Rectangle sectionBounds, int sectionPadding, out Rectangle labelBounds, out Rectangle controlBounds)
    {
        int innerX = sectionBounds.X + sectionPadding;
        int innerWidth = sectionBounds.Width - (sectionPadding * 2);
        int labelWidth = Math.Clamp((int)(innerWidth * 0.38f), 142, 178);
        int controlX = innerX + labelWidth + LabelToControlGap;
        int controlWidth = Math.Max(120, sectionBounds.Right - sectionPadding - controlX);

        labelBounds = new Rectangle(innerX, sectionBounds.Y, labelWidth, sectionBounds.Height);
        controlBounds = new Rectangle(controlX, sectionBounds.Y, controlWidth, sectionBounds.Height);
    }

    private Rectangle GetControlBindingBounds(LayoutMetrics layout, int index)
    {
        int row = index % layout.ControlRowsPerColumn;
        int dataY = layout.ControlHeaderBounds.Bottom + layout.ControlHeaderGap;
        int y = dataY + (row * (layout.KeyRowHeight + layout.KeyRowGap));
        return new Rectangle(layout.ControlRowsArea.X, y, layout.ControlRowsArea.Width, layout.KeyRowHeight);
    }

    private enum BindingCell
    {
        Label,
        Keyboard,
        Gamepad
    }

    private void GetBindingCellRects(LayoutMetrics layout, int index, out Rectangle label, out Rectangle keyboard, out Rectangle gamepad)
    {
        Rectangle row = GetControlBindingBounds(layout, index);
        int padH = layout.ControlCellPaddingH;
        int padV = layout.ControlCellPaddingV;

        GetColumnBounds(layout, row.Y, row.Height, 0, out Rectangle actionCell);
        GetColumnBounds(layout, row.Y, row.Height, 1, out Rectangle keyboardCell);
        GetColumnBounds(layout, row.Y, row.Height, 2, out Rectangle gamepadCell);

        label = new Rectangle(
            actionCell.X + padH,
            actionCell.Y + padV,
            Math.Max(1, actionCell.Width - (padH * 2)),
            Math.Max(1, actionCell.Height - (padV * 2)));

        keyboard = new Rectangle(
            keyboardCell.X + padH,
            keyboardCell.Y + Math.Max(2, padV - 1),
            Math.Max(1, keyboardCell.Width - (padH * 2)),
            Math.Max(1, keyboardCell.Height - (Math.Max(2, padV - 1) * 2)));

        gamepad = new Rectangle(
            gamepadCell.X + padH,
            gamepadCell.Y + Math.Max(2, padV - 1),
            Math.Max(1, gamepadCell.Width - (padH * 2)),
            Math.Max(1, gamepadCell.Height - (Math.Max(2, padV - 1) * 2)));
    }

    private Rectangle GetBindingCellBounds(LayoutMetrics layout, int index, BindingCell cell)
    {
        GetBindingCellRects(layout, index, out Rectangle label, out Rectangle keyboard, out Rectangle gamepad);
        return cell switch
        {
            BindingCell.Keyboard => keyboard,
            BindingCell.Gamepad => gamepad,
            _ => label
        };
    }

    private void SyncPendingSettings()
    {
        SettingsManager.PendingSettings.MusicVolume = _volumeSlider.Value;
        SettingsManager.PendingSettings.FpsLimit = _fpsLimitSelector.CurrentOption;
        SettingsManager.PendingSettings.DisplayMode = _displayModeSelector.CurrentOption.ToString();
        if (_resolutionDropdown.SelectedResolution != null)
        {
            SettingsManager.PendingSettings.ResolutionWidth = _resolutionDropdown.SelectedResolution.Width;
            SettingsManager.PendingSettings.ResolutionHeight = _resolutionDropdown.SelectedResolution.Height;
        }
    }

    private static void DrawFittedLeft(SpriteBatch spriteBatch, Texture2D pixel, string text, Rectangle bounds, int preferredScale, Color color)
    {
        int scale = GetFittedScale(text, bounds, preferredScale);
        Point size = SimpleTextRenderer.MeasureString(text, scale);
        Vector2 position = new(bounds.X, bounds.Center.Y - (size.Y / 2));
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, position, scale, color);
    }

    private static void DrawFittedCentered(SpriteBatch spriteBatch, Texture2D pixel, string text, Rectangle bounds, int preferredScale, Color color)
    {
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, text, bounds, GetFittedScale(text, bounds, preferredScale), color);
    }

    private static int GetFittedScale(string text, Rectangle bounds, int preferredScale)
    {
        int scale = Math.Max(1, preferredScale);
        int maxWidth = Math.Max(1, bounds.Width - 16);
        int maxHeight = Math.Max(1, bounds.Height - 10);

        while (scale > 1)
        {
            Point size = SimpleTextRenderer.MeasureString(text, scale);
            if (size.X <= maxWidth && size.Y <= maxHeight)
            {
                break;
            }

            scale--;
        }

        return scale;
    }

    private static string FormatFpsLimit(int fpsLimit) => fpsLimit switch
    {
        < 0 => "VSync",
        0 => "Unlimited",
        _ => $"{fpsLimit} FPS"
    };

    private static string FormatDisplayMode(DisplayMode mode) => mode switch
    {
        DisplayMode.Fullscreen => "Fullscreen",
        DisplayMode.Windowed => "Windowed",
        DisplayMode.BorderlessWindowed => "Borderless",
        _ => "Unknown"
    };

    private static string FormatActionName(string action) => action switch
    {
        "MoveLeft" => "MOVE LEFT",
        "MoveRight" => "MOVE RIGHT",
        "Jump" => "JUMP",
        "Respawn" => "RESPAWN",
        "PullRope" => "PULL ROPE",
        "FastFall" => "FAST FALL",
        "Red" => "RED",
        "Blue" => "BLUE",
        "Green" => "GREEN",
        _ => action
    };

    private static string FormatKeyName(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return "UNKNOWN";
        }

        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
        {
            return keyName[1].ToString();
        }

        return keyName switch
        {
            "Space" => "SPACE",
            "LeftShift" => "LEFT SHIFT",
            "RightShift" => "RIGHT SHIFT",
            "LeftControl" => "LEFT CTRL",
            "RightControl" => "RIGHT CTRL",
            "LeftAlt" => "LEFT ALT",
            "RightAlt" => "RIGHT ALT",
            "OemComma" => "COMMA",
            "OemPeriod" => "PERIOD",
            "OemMinus" => "MINUS",
            "OemPlus" => "PLUS",
            _ => keyName
        };
    }

    private void ApplySettings()
    {
        SettingsManager.SaveSettings(SettingsManager.PendingSettings);
        _game.Input.ReloadKeyboardBindings();

        var settings = SettingsManager.CurrentSettings;
        _game.ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode);
        _game.ApplyFrameSettings(settings.FpsLimit);
    }

    public void OnExit()
    {
    }

    private readonly record struct LayoutMetrics(
        Rectangle PanelBounds,
        Rectangle ContentBounds,
        Rectangle TitleBounds,
        int TitleScale,
        int SectionPadding,
        int SectionTitleHeight,
        int SectionTitleSpacing,
        Rectangle DisplaySectionBounds,
        Rectangle AudioSectionBounds,
        Rectangle ControlSectionBounds,
        Rectangle DisplayLabelBounds,
        Rectangle DisplayControlBounds,
        Rectangle AudioLabelBounds,
        Rectangle AudioControlBounds,
        Rectangle DisplayModeBounds,
        Rectangle ResolutionBounds,
        Rectangle FpsLimitBounds,
        Rectangle VolumeBounds,
        Rectangle ControlRowsArea,
        int ControlColumns,
        int ControlRowsPerColumn,
        int ControlColumnWidth,
        int ControlColumnGap,
        int KeyRowHeight,
        int KeyRowGap,
        Rectangle ControlHeaderBounds,
        int ControlHeaderGap,
        int ControlActionColumnWidth,
        int ControlKeyboardColumnWidth,
        int ControlGamepadColumnWidth,
        int ControlCellPaddingH,
        int ControlCellPaddingV,
        Rectangle BackButtonBounds,
        Rectangle ApplyButtonBounds);
}
