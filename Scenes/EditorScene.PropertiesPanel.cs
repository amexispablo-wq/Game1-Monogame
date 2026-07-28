using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

internal enum EditorPropsKind
{
    None,
    Platform,
    LaunchPad,
    PowerUp,
    Lava
}

internal enum PropsNumericField
{
    None,
    VerticalSpeed,
    VerticalDistance,
    HorizontalSpeed,
    HorizontalDistance,
    ColorPeriod,
    LaunchForce,
    LaunchRotation,
    PowerDuration,
    PowerMultiplier,
    PowerRespawn,
    LavaRise
}

public sealed partial class EditorScene
{
    private Rectangle _propsPanelBounds;
    private Rectangle _launchPadForceMinusBounds;
    private Rectangle _launchPadForcePlusBounds;
    private Rectangle _launchPadForceFieldBounds;
    private Rectangle _launchPadRotateMinusBounds;
    private Rectangle _launchPadRotatePlusBounds;
    private Rectangle _launchPadRotateFieldBounds;
    private Rectangle _powerUpConsumableCheckBounds;
    private Rectangle _vSpeedFieldBounds;
    private Rectangle _vDistFieldBounds;
    private Rectangle _hSpeedFieldBounds;
    private Rectangle _hDistFieldBounds;
    private Rectangle _colorPeriodFieldBounds;
    private Rectangle _powerDurationFieldBounds;
    private Rectangle _powerMultiplierFieldBounds;
    private Rectangle _powerRespawnFieldBounds;
    private Rectangle _lavaRiseFieldBounds;

    private readonly TextInputComponent _propsValueInput = new()
    {
        TextScale = 1,
        MaxLength = 10,
        Mode = TextInputMode.Decimal
    };

    private PropsNumericField _activePropsField = PropsNumericField.None;

    /// <summary>
    /// Enter/Esc used to finish props typing this frame — block chrome menu confirm/cancel.
    /// </summary>
    private bool _propsEditConsumedMenuKeys;

    private bool IsEditingPropsValue => _propsValueInput.IsFocused && _activePropsField != PropsNumericField.None;

    private EditorPropsKind GetPropsSelectionKind()
    {
        int kindCount = 0;
        EditorPropsKind kind = EditorPropsKind.None;

        void Add(EditorPropsKind next)
        {
            if (kindCount == 0)
            {
                kind = next;
            }

            kindCount++;
        }

        if (_selectedPlatforms.Count > 0)
        {
            Add(EditorPropsKind.Platform);
        }

        if (_selectedLaunchPads.Count > 0)
        {
            Add(EditorPropsKind.LaunchPad);
        }

        if (_selectedPowerUps.Count > 0)
        {
            Add(EditorPropsKind.PowerUp);
        }

        if (_lavaSelected)
        {
            Add(EditorPropsKind.Lava);
        }

        if (_selectedGoals.Count > 0
            || _selectedCheckpoints.Count > 0
            || _playerSpawnSelected)
        {
            return EditorPropsKind.None;
        }

        return kindCount == 1 ? kind : EditorPropsKind.None;
    }

    private bool ShowPropertiesPanel => GetPropsSelectionKind() != EditorPropsKind.None;
    private bool ShowMotionPanel => GetPropsSelectionKind() == EditorPropsKind.Platform;
    private bool ShowColorCyclePanel => ShowMotionPanel;
    private bool ShowPowerUpPanel => GetPropsSelectionKind() == EditorPropsKind.PowerUp;
    private bool ShowLaunchPadPanel => GetPropsSelectionKind() == EditorPropsKind.LaunchPad;
    private bool ShowLavaPropsPanel => GetPropsSelectionKind() == EditorPropsKind.Lava;

    private void LayoutPropertiesPanel()
    {
        EditorPropsKind kind = GetPropsSelectionKind();
        if (kind == EditorPropsKind.None)
        {
            ClearPropsValueEdit(commit: false);
            _propsPanelBounds = Rectangle.Empty;
            _motionPanelBounds = Rectangle.Empty;
            _colorCyclePanelBounds = Rectangle.Empty;
            _powerUpPanelBounds = Rectangle.Empty;
            _lavaSpeedPanelBounds = Rectangle.Empty;
            ClearPropsNumericFieldBounds();
            return;
        }

        Viewport viewport = _game.Viewport;
        const int panelWidth = 312;
        const int margin = 12;
        Rectangle partyHud = PartyHudOverlay.GetPanelBounds(viewport, _game.Party);
        int top = partyHud.IsEmpty ? margin : partyHud.Bottom + 10;
        int height = kind switch
        {
            EditorPropsKind.Platform => 420,
            EditorPropsKind.LaunchPad => 168,
            EditorPropsKind.PowerUp => 248,
            EditorPropsKind.Lava => 112,
            _ => 140
        };

        int maxBottom = viewport.Height - margin - 96;
        if (top + height > maxBottom)
        {
            height = Math.Max(120, maxBottom - top);
        }

        _propsPanelBounds = new Rectangle(
            Math.Max(margin, viewport.Width - panelWidth - margin),
            top,
            panelWidth,
            height);

        // Drop stale hitboxes from other props kinds (ghost fields were stealing clicks).
        ClearPropsNumericFieldBounds();

        switch (kind)
        {
            case EditorPropsKind.Platform:
                LayoutMotionPanel();
                LayoutColorCyclePanel();
                break;
            case EditorPropsKind.LaunchPad:
                LayoutLaunchPadPanel();
                break;
            case EditorPropsKind.PowerUp:
                LayoutPowerUpPanel();
                break;
            case EditorPropsKind.Lava:
                LayoutLavaSpeedPanel();
                break;
        }

        SyncPropsValueInputBounds();
    }

    private void ClearPropsNumericFieldBounds()
    {
        _vSpeedFieldBounds = Rectangle.Empty;
        _vDistFieldBounds = Rectangle.Empty;
        _hSpeedFieldBounds = Rectangle.Empty;
        _hDistFieldBounds = Rectangle.Empty;
        _colorPeriodFieldBounds = Rectangle.Empty;
        _launchPadForceFieldBounds = Rectangle.Empty;
        _launchPadRotateFieldBounds = Rectangle.Empty;
        _powerDurationFieldBounds = Rectangle.Empty;
        _powerMultiplierFieldBounds = Rectangle.Empty;
        _powerRespawnFieldBounds = Rectangle.Empty;
        _lavaRiseFieldBounds = Rectangle.Empty;
    }

    private void UpdatePropertiesPanelInput(GameTime gameTime)
    {
        _propsEditConsumedMenuKeys = false;

        if (!ShowPropertiesPanel)
        {
            ClearPropsValueEdit(commit: false);
            return;
        }

        // Click outside active field commits before chrome buttons see the press.
        if (IsEditingPropsValue
            && IsPrimaryPressed()
            && !_propsValueInput.Bounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
        }

        if (!IsEditingPropsValue)
        {
            return;
        }

        _propsValueInput.Update(gameTime, _game.Input);

        if (_game.Input.IsNewKeyPress(Keys.Enter)
            || _game.Input.KeyboardMenuConfirmPressed
            || _game.Input.GamepadMenuConfirmPressed)
        {
            ClearPropsValueEdit(commit: true);
            _propsEditConsumedMenuKeys = true;
            return;
        }

        if (_game.Input.IsNewKeyPress(Keys.Escape)
            || _game.Input.KeyboardMenuCancelPressed
            || _game.Input.GamepadMenuCancelPressed
            || _game.Input.GamepadBackPressed)
        {
            ClearPropsValueEdit(commit: false);
            _propsEditConsumedMenuKeys = true;
        }
    }

    private bool TryHandlePropertiesPanelPress()
    {
        if (!ShowPropertiesPanel)
        {
            return false;
        }

        if (!IsPrimaryPressed())
        {
            return _propsPanelBounds.Contains(UiPointer);
        }

        // Click outside active field commits.
        if (IsEditingPropsValue && !_propsValueInput.Bounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
        }

        if (TryBeginPropsValueEditFromPointer())
        {
            return true;
        }

        return GetPropsSelectionKind() switch
        {
            EditorPropsKind.Platform => TryHandleMotionPanelPress() || TryHandleColorCyclePanelPress(),
            EditorPropsKind.LaunchPad => TryHandleLaunchPadPanelPress(),
            EditorPropsKind.PowerUp => TryHandlePowerUpPanelPress(),
            EditorPropsKind.Lava => TryHandleLavaPropsPanelPress(),
            _ => false
        } || _propsPanelBounds.Contains(UiPointer);
    }

    private bool TryBeginPropsValueEditFromPointer()
    {
        return GetPropsSelectionKind() switch
        {
            EditorPropsKind.Platform =>
                TryFocusPropsField(PropsNumericField.VerticalSpeed, _vSpeedFieldBounds)
                || TryFocusPropsField(PropsNumericField.VerticalDistance, _vDistFieldBounds)
                || TryFocusPropsField(PropsNumericField.HorizontalSpeed, _hSpeedFieldBounds)
                || TryFocusPropsField(PropsNumericField.HorizontalDistance, _hDistFieldBounds)
                || TryFocusPropsField(PropsNumericField.ColorPeriod, _colorPeriodFieldBounds),
            EditorPropsKind.LaunchPad =>
                TryFocusPropsField(PropsNumericField.LaunchForce, _launchPadForceFieldBounds)
                || TryFocusPropsField(PropsNumericField.LaunchRotation, _launchPadRotateFieldBounds),
            EditorPropsKind.PowerUp =>
                TryFocusPropsField(PropsNumericField.PowerDuration, _powerDurationFieldBounds)
                || TryFocusPropsField(PropsNumericField.PowerMultiplier, _powerMultiplierFieldBounds)
                || TryFocusPropsField(PropsNumericField.PowerRespawn, _powerRespawnFieldBounds),
            EditorPropsKind.Lava =>
                TryFocusPropsField(PropsNumericField.LavaRise, _lavaRiseFieldBounds),
            _ => false
        };
    }

    private bool TryFocusPropsField(PropsNumericField field, Rectangle bounds)
    {
        if (bounds.IsEmpty || !bounds.Contains(UiPointer))
        {
            return false;
        }

        BeginPropsValueEdit(field);
        return true;
    }

    private void BeginPropsValueEdit(PropsNumericField field)
    {
        if (field == PropsNumericField.None)
        {
            return;
        }

        if (_activePropsField == field && IsEditingPropsValue)
        {
            SyncPropsValueInputBounds();
            return;
        }

        if (_activePropsField != field && IsEditingPropsValue)
        {
            ClearPropsValueEdit(commit: true);
        }

        _activePropsField = field;
        _propsValueInput.Mode = field is PropsNumericField.VerticalDistance
            or PropsNumericField.HorizontalDistance
            or PropsNumericField.LaunchRotation
            ? TextInputMode.Integer
            : TextInputMode.Decimal;
        _propsValueInput.Text = GetPropsFieldDisplayValue(field);
        _propsValueInput.SetFocus(true);
        SyncPropsValueInputBounds();
    }

    private void ClearPropsValueEdit(bool commit)
    {
        if (_activePropsField == PropsNumericField.None)
        {
            _propsValueInput.SetFocus(false);
            return;
        }

        PropsNumericField field = _activePropsField;
        string text = _propsValueInput.Text;
        _activePropsField = PropsNumericField.None;
        _propsValueInput.SetFocus(false);
        _propsValueInput.Text = string.Empty;

        if (commit)
        {
            ApplyPropsFieldText(field, text);
        }
    }

    private void SyncPropsValueInputBounds()
    {
        _propsValueInput.Bounds = _activePropsField switch
        {
            PropsNumericField.VerticalSpeed => _vSpeedFieldBounds,
            PropsNumericField.VerticalDistance => _vDistFieldBounds,
            PropsNumericField.HorizontalSpeed => _hSpeedFieldBounds,
            PropsNumericField.HorizontalDistance => _hDistFieldBounds,
            PropsNumericField.ColorPeriod => _colorPeriodFieldBounds,
            PropsNumericField.LaunchForce => _launchPadForceFieldBounds,
            PropsNumericField.LaunchRotation => _launchPadRotateFieldBounds,
            PropsNumericField.PowerDuration => _powerDurationFieldBounds,
            PropsNumericField.PowerMultiplier => _powerMultiplierFieldBounds,
            PropsNumericField.PowerRespawn => _powerRespawnFieldBounds,
            PropsNumericField.LavaRise => _lavaRiseFieldBounds,
            _ => Rectangle.Empty
        };
    }

    private string GetPropsFieldDisplayValue(PropsNumericField field)
    {
        return field switch
        {
            PropsNumericField.VerticalSpeed when _selectedPlatforms.Count > 0 =>
                _selectedPlatforms[0].VerticalSpeed.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.VerticalDistance when _selectedPlatforms.Count > 0 =>
                _selectedPlatforms[0].VerticalDistanceBlocks.ToString(CultureInfo.InvariantCulture),
            PropsNumericField.HorizontalSpeed when _selectedPlatforms.Count > 0 =>
                _selectedPlatforms[0].HorizontalSpeed.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.HorizontalDistance when _selectedPlatforms.Count > 0 =>
                _selectedPlatforms[0].HorizontalDistanceBlocks.ToString(CultureInfo.InvariantCulture),
            PropsNumericField.ColorPeriod when _selectedPlatforms.Count > 0 =>
                _selectedPlatforms[0].ColorChangePeriodSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.LaunchForce when _selectedLaunchPads.Count > 0 =>
                _selectedLaunchPads[0].LaunchForce.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.LaunchRotation when _selectedLaunchPads.Count > 0 =>
                LaunchPad.NormalizeRotation(_selectedLaunchPads[0].RotationDegrees).ToString("0", CultureInfo.InvariantCulture),
            PropsNumericField.PowerDuration when _selectedPowerUps.Count > 0 =>
                _selectedPowerUps[0].DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.PowerMultiplier when _selectedPowerUps.Count > 0 =>
                _selectedPowerUps[0].Multiplier.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.PowerRespawn when _selectedPowerUps.Count > 0 =>
                _selectedPowerUps[0].RespawnSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            PropsNumericField.LavaRise when _level.Lava is not null =>
                _level.Lava.RiseSpeed.ToString("0.###", CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    private void ApplyPropsFieldText(PropsNumericField field, string text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0 || text == "-" || text == "." || text == "-.")
        {
            return;
        }

        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            && !float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return;
        }

        switch (field)
        {
            case PropsNumericField.VerticalSpeed:
                SetSelectedVerticalSpeed(value);
                break;
            case PropsNumericField.VerticalDistance:
                SetSelectedVerticalDistanceBlocks((int)MathF.Round(value));
                break;
            case PropsNumericField.HorizontalSpeed:
                SetSelectedHorizontalSpeed(value);
                break;
            case PropsNumericField.HorizontalDistance:
                SetSelectedHorizontalDistanceBlocks((int)MathF.Round(value));
                break;
            case PropsNumericField.ColorPeriod:
                SetSelectedColorChangePeriod(value);
                break;
            case PropsNumericField.LaunchForce:
                SetSelectedLaunchPadForce(value);
                break;
            case PropsNumericField.LaunchRotation:
                SetSelectedLaunchPadRotation(value);
                break;
            case PropsNumericField.PowerDuration:
                SetSelectedPowerUpDuration(value);
                break;
            case PropsNumericField.PowerMultiplier:
                SetSelectedPowerUpMultiplier(value);
                break;
            case PropsNumericField.PowerRespawn:
                SetSelectedPowerUpRespawn(value);
                break;
            case PropsNumericField.LavaRise:
                SetLavaRiseSpeed(value);
                break;
        }
    }

    private void SetSelectedVerticalSpeed(float value)
    {
        if (_selectedPlatforms.Count == 0) return;
        BeginHistoryGesture();
        float next = Platform.ClampSpeed(value);
        foreach (Platform platform in _selectedPlatforms) platform.VerticalSpeed = next;
        _level.RecalculateWorldSize();
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedHorizontalSpeed(float value)
    {
        if (_selectedPlatforms.Count == 0) return;
        BeginHistoryGesture();
        float next = Platform.ClampSpeed(value);
        foreach (Platform platform in _selectedPlatforms) platform.HorizontalSpeed = next;
        _level.RecalculateWorldSize();
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedVerticalDistanceBlocks(int value)
    {
        if (_selectedPlatforms.Count == 0) return;
        BeginHistoryGesture();
        int next = Platform.ClampDistanceBlocks(value);
        foreach (Platform platform in _selectedPlatforms) platform.VerticalDistanceBlocks = next;
        _level.RecalculateWorldSize();
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedHorizontalDistanceBlocks(int value)
    {
        if (_selectedPlatforms.Count == 0) return;
        BeginHistoryGesture();
        int next = Platform.ClampDistanceBlocks(value);
        foreach (Platform platform in _selectedPlatforms) platform.HorizontalDistanceBlocks = next;
        _level.RecalculateWorldSize();
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedColorChangePeriod(float value)
    {
        if (_selectedPlatforms.Count == 0) return;
        BeginHistoryGesture();
        float next = Platform.ClampColorChangePeriod(value);
        foreach (Platform platform in _selectedPlatforms) platform.ColorChangePeriodSeconds = next;
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedLaunchPadForce(float value)
    {
        if (_selectedLaunchPads.Count == 0) return;
        BeginHistoryGesture();
        float next = LaunchPad.ClampLaunchForce(value);
        foreach (LaunchPad launchPad in _selectedLaunchPads) launchPad.LaunchForce = next;
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedLaunchPadRotation(float value)
    {
        if (_selectedLaunchPads.Count == 0) return;
        BeginHistoryGesture();
        float next = LaunchPad.NormalizeRotation(value);
        foreach (LaunchPad launchPad in _selectedLaunchPads) launchPad.RotationDegrees = next;
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedPowerUpDuration(float value)
    {
        if (_selectedPowerUps.Count == 0) return;
        BeginHistoryGesture();
        float next = PowerUp.ClampDuration(value);
        foreach (PowerUp powerUp in _selectedPowerUps) powerUp.DurationSeconds = next;
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedPowerUpMultiplier(float value)
    {
        if (_selectedPowerUps.Count == 0) return;
        BeginHistoryGesture();
        float next = PowerUp.ClampMultiplier(value);
        foreach (PowerUp powerUp in _selectedPowerUps) powerUp.Multiplier = next;
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetSelectedPowerUpRespawn(float value)
    {
        if (_selectedPowerUps.Count == 0) return;
        BeginHistoryGesture();
        float next = PowerUp.ClampRespawn(value);
        foreach (PowerUp powerUp in _selectedPowerUps) powerUp.RespawnSeconds = next;
        _isDirty = true;
        EndHistoryGesture();
    }

    private void SetLavaRiseSpeed(float value)
    {
        if (_level.Lava is null) return;
        BeginHistoryGesture();
        _level.Lava.RiseSpeed = LavaLine.ClampRiseSpeed(value);
        _isDirty = true;
        EndHistoryGesture();
    }

    private bool TryHandleLavaPropsPanelPress()
    {
        if (!ShowLavaPropsPanel || _level.Lava is null)
        {
            return false;
        }

        if (_lavaSpeedMinusBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
            AdjustLavaRiseSpeed(-LavaLine.RiseSpeedStep);
            return true;
        }

        if (_lavaSpeedPlusBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
            AdjustLavaRiseSpeed(LavaLine.RiseSpeedStep);
            return true;
        }

        return _propsPanelBounds.Contains(UiPointer);
    }

    private void LayoutLaunchPadPanel()
    {
        LayoutPropsNumericRow(0, out _launchPadForceMinusBounds, out _launchPadForceFieldBounds, out _launchPadForcePlusBounds);
        LayoutPropsNumericRow(1, out _launchPadRotateMinusBounds, out _launchPadRotateFieldBounds, out _launchPadRotatePlusBounds);
    }

    private bool TryHandleLaunchPadPanelPress()
    {
        if (!ShowLaunchPadPanel)
        {
            return false;
        }

        if (_launchPadForceMinusBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
            AdjustSelectedLaunchPadForce(-LaunchPad.LaunchForceStep);
            return true;
        }

        if (_launchPadForcePlusBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
            AdjustSelectedLaunchPadForce(LaunchPad.LaunchForceStep);
            return true;
        }

        if (_launchPadRotateMinusBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
            BeginHistoryGesture();
            RotateSelectedLaunchPads(-15f);
            EndHistoryGesture();
            return true;
        }

        if (_launchPadRotatePlusBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
            BeginHistoryGesture();
            RotateSelectedLaunchPads(15f);
            EndHistoryGesture();
            return true;
        }

        return _propsPanelBounds.Contains(UiPointer);
    }

    private void AdjustSelectedLaunchPadForce(float delta)
    {
        if (_selectedLaunchPads.Count == 0) return;
        SetSelectedLaunchPadForce(_selectedLaunchPads[0].LaunchForce + delta);
    }

    private void LayoutPropsNumericRow(int rowIndex, out Rectangle minus, out Rectangle field, out Rectangle plus)
    {
        const int button = 26;
        const int fieldWidth = 96;
        Rectangle panel = _propsPanelBounds;
        int y = panel.Y + 46 + (rowIndex * 44);
        int plusX = panel.Right - 14 - button;
        int fieldX = plusX - 6 - fieldWidth;
        int minusX = fieldX - 6 - button;
        minus = new Rectangle(minusX, y, button, button);
        field = new Rectangle(fieldX, y, fieldWidth, button);
        plus = new Rectangle(plusX, y, button, button);
    }

    private void DrawPropertiesPanel(SpriteBatch spriteBatch, Texture2D pixel)
    {
        EditorPropsKind kind = GetPropsSelectionKind();
        if (kind == EditorPropsKind.None)
        {
            return;
        }

        LayoutPropertiesPanel();
        Rectangle panel = _propsPanelBounds;

        spriteBatch.Draw(pixel, panel, new Color(14, 18, 26, 236));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(72, 86, 110), 1);
        spriteBatch.Draw(pixel, new Rectangle(panel.X, panel.Y, 3, panel.Height), new Color(90, 170, 220));

        string title = kind switch
        {
            EditorPropsKind.Platform => "PLATFORM",
            EditorPropsKind.LaunchPad => "LAUNCH PAD",
            EditorPropsKind.PowerUp => "POWER-UP",
            EditorPropsKind.Lava => "LAVA",
            _ => "PROPERTIES"
        };
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            title,
            new Vector2(panel.X + 14, panel.Y + 12),
            1,
            new Color(230, 236, 245));
        spriteBatch.Draw(
            pixel,
            new Rectangle(panel.X + 12, panel.Y + 32, panel.Width - 24, 1),
            new Color(55, 68, 88));

        switch (kind)
        {
            case EditorPropsKind.Platform:
                DrawMotionPanelContent(spriteBatch, pixel);
                DrawColorCyclePanelContent(spriteBatch, pixel);
                break;
            case EditorPropsKind.LaunchPad:
                DrawLaunchPadPanelContent(spriteBatch, pixel);
                break;
            case EditorPropsKind.PowerUp:
                DrawPowerUpPanelContent(spriteBatch, pixel);
                break;
            case EditorPropsKind.Lava:
                DrawLavaPropsPanelContent(spriteBatch, pixel);
                break;
        }

        if (IsEditingPropsValue)
        {
            _propsValueInput.Draw(spriteBatch, pixel);
        }
    }

    private void DrawLaunchPadPanelContent(SpriteBatch spriteBatch, Texture2D pixel)
    {
        LaunchPad sample = _selectedLaunchPads[0];
        DrawPropsLabeledValue(
            spriteBatch,
            pixel,
            "FORCE",
            $"{sample.LaunchForce:0}",
            _launchPadForceMinusBounds,
            _launchPadForceFieldBounds,
            _launchPadForcePlusBounds,
            PropsNumericField.LaunchForce);
        DrawPropsLabeledValue(
            spriteBatch,
            pixel,
            "ROT",
            $"{LaunchPad.NormalizeRotation(sample.RotationDegrees):0}",
            _launchPadRotateMinusBounds,
            _launchPadRotateFieldBounds,
            _launchPadRotatePlusBounds,
            PropsNumericField.LaunchRotation);
    }

    private void DrawLavaPropsPanelContent(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (_level.Lava is null)
        {
            return;
        }

        DrawPropsLabeledValue(
            spriteBatch,
            pixel,
            "RISE",
            $"{_level.Lava.RiseSpeed:0}",
            _lavaSpeedMinusBounds,
            _lavaRiseFieldBounds,
            _lavaSpeedPlusBounds,
            PropsNumericField.LavaRise);
    }

    private void DrawPropsLabeledValue(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        string label,
        string value,
        Rectangle minusBounds,
        Rectangle fieldBounds,
        Rectangle plusBounds,
        PropsNumericField field)
    {
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            label,
            new Vector2(_propsPanelBounds.X + 14, minusBounds.Y + 6),
            1,
            new Color(170, 188, 210));

        DrawPropsStepButton(spriteBatch, pixel, minusBounds, "-");
        if (_activePropsField != field)
        {
            DrawPropsValueField(spriteBatch, pixel, fieldBounds, value, fieldBounds.Contains(UiPointer));
        }

        DrawPropsStepButton(spriteBatch, pixel, plusBounds, "+");
    }

    private void DrawPropsValueField(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string value, bool hovered)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        spriteBatch.Draw(pixel, bounds, hovered ? new Color(34, 44, 58) : new Color(24, 30, 40));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, hovered ? new Color(120, 180, 220) : new Color(64, 78, 98), 1);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, value, bounds, 1, Color.White);
    }

    private void DrawPropsStepButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label)
    {
        bool hovered = bounds.Contains(UiPointer);
        spriteBatch.Draw(pixel, bounds, hovered ? new Color(48, 62, 82) : new Color(30, 38, 52));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, hovered ? new Color(140, 190, 230) : new Color(70, 88, 112), 1);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, 2, Color.White);
    }

    private void DrawPropsSectionHeader(SpriteBatch spriteBatch, Texture2D pixel, string title, int y)
    {
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            title,
            new Vector2(_propsPanelBounds.X + 14, y),
            1,
            new Color(120, 170, 210));
        spriteBatch.Draw(
            pixel,
            new Rectangle(_propsPanelBounds.X + 12, y + 16, _propsPanelBounds.Width - 24, 1),
            new Color(45, 58, 76));
    }

    private void ToggleSelectedPowerUpConsumable()
    {
        if (_selectedPowerUps.Count == 0)
        {
            return;
        }

        BeginHistoryGesture();
        bool next = !_selectedPowerUps[0].Consumable;
        foreach (PowerUp powerUp in _selectedPowerUps)
        {
            powerUp.Consumable = next;
        }

        _isDirty = true;
        EndHistoryGesture();
    }
}
