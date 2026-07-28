using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed partial class EditorScene
{
    private void LayoutPowerUpPanel()
    {
        if (!ShowPowerUpPanel)
        {
            _powerUpPanelBounds = Rectangle.Empty;
            return;
        }

        const int button = 26;
        const int check = 20;
        Rectangle panel = _propsPanelBounds;
        _powerUpPanelBounds = panel;
        int left = panel.X + 14;

        int row0 = panel.Y + 46;
        _powerUpTypeBounds = new Rectangle(left, row0, 120, button);
        _powerUpConsumableCheckBounds = new Rectangle(left + 140, row0 + 3, check, check);

        LayoutPropsNumericRow(1, out _powerUpDurationMinusBounds, out _powerDurationFieldBounds, out _powerUpDurationPlusBounds);
        LayoutPropsNumericRow(2, out _powerUpMultiplierMinusBounds, out _powerMultiplierFieldBounds, out _powerUpMultiplierPlusBounds);
        LayoutPropsNumericRow(3, out _powerUpRespawnMinusBounds, out _powerRespawnFieldBounds, out _powerUpRespawnPlusBounds);
    }

    private bool TryHandlePowerUpPanelPress()
    {
        if (!ShowPowerUpPanel || !IsPrimaryPressed())
        {
            return false;
        }

        if (_powerUpTypeBounds.Contains(UiPointer)
            || _powerUpDurationMinusBounds.Contains(UiPointer)
            || _powerUpDurationPlusBounds.Contains(UiPointer)
            || _powerUpMultiplierMinusBounds.Contains(UiPointer)
            || _powerUpMultiplierPlusBounds.Contains(UiPointer)
            || _powerUpRespawnMinusBounds.Contains(UiPointer)
            || _powerUpRespawnPlusBounds.Contains(UiPointer)
            || _powerUpConsumableCheckBounds.Contains(UiPointer))
        {
            ClearPropsValueEdit(commit: true);
        }

        if (_powerUpTypeBounds.Contains(UiPointer))
        {
            CycleSelectedPowerUpType();
            return true;
        }

        if (_powerUpDurationMinusBounds.Contains(UiPointer))
        {
            AdjustSelectedPowerUpDuration(-PowerUp.DurationStep);
            return true;
        }

        if (_powerUpDurationPlusBounds.Contains(UiPointer))
        {
            AdjustSelectedPowerUpDuration(PowerUp.DurationStep);
            return true;
        }

        if (_powerUpMultiplierMinusBounds.Contains(UiPointer))
        {
            AdjustSelectedPowerUpMultiplier(-PowerUp.MultiplierStep);
            return true;
        }

        if (_powerUpMultiplierPlusBounds.Contains(UiPointer))
        {
            AdjustSelectedPowerUpMultiplier(PowerUp.MultiplierStep);
            return true;
        }

        if (_powerUpRespawnMinusBounds.Contains(UiPointer))
        {
            AdjustSelectedPowerUpRespawn(-PowerUp.RespawnStep);
            return true;
        }

        if (_powerUpRespawnPlusBounds.Contains(UiPointer))
        {
            AdjustSelectedPowerUpRespawn(PowerUp.RespawnStep);
            return true;
        }

        if (_powerUpConsumableCheckBounds.Contains(UiPointer))
        {
            ToggleSelectedPowerUpConsumable();
            return true;
        }

        return _propsPanelBounds.Contains(UiPointer);
    }

    private void DrawPowerUpPanelContent(SpriteBatch spriteBatch, Texture2D pixel)
    {
        PowerUp primary = _selectedPowerUps[0];
        string typeLabel = primary.Type == PowerUpType.Speed ? "SPEED" : "JUMP";
        DrawPropsStepButton(spriteBatch, pixel, _powerUpTypeBounds, typeLabel);
        DrawMotionCheckbox(spriteBatch, pixel, _powerUpConsumableCheckBounds, primary.Consumable);
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            "USE",
            new Vector2(_powerUpConsumableCheckBounds.Right + 8, _powerUpConsumableCheckBounds.Y + 2),
            1,
            Color.White);

        DrawPropsLabeledValue(
            spriteBatch,
            pixel,
            "DUR",
            $"{primary.DurationSeconds:0.##}",
            _powerUpDurationMinusBounds,
            _powerDurationFieldBounds,
            _powerUpDurationPlusBounds,
            PropsNumericField.PowerDuration);
        DrawPropsLabeledValue(
            spriteBatch,
            pixel,
            "MUL",
            $"{primary.Multiplier:0.##}",
            _powerUpMultiplierMinusBounds,
            _powerMultiplierFieldBounds,
            _powerUpMultiplierPlusBounds,
            PropsNumericField.PowerMultiplier);
        DrawPropsLabeledValue(
            spriteBatch,
            pixel,
            "RSP",
            primary.Consumable
                ? $"{primary.RespawnSeconds:0.##}"
                : "-",
            _powerUpRespawnMinusBounds,
            _powerRespawnFieldBounds,
            _powerUpRespawnPlusBounds,
            PropsNumericField.PowerRespawn);
    }

    private void CycleSelectedPowerUpType()
    {
        if (_selectedPowerUps.Count == 0)
        {
            return;
        }

        BeginHistoryGesture();
        PowerUpType next = PowerUp.CycleType(_selectedPowerUps[0].Type);
        foreach (PowerUp powerUp in _selectedPowerUps)
        {
            powerUp.Type = next;
        }

        _isDirty = true;
        EndHistoryGesture();
    }

    private void AdjustSelectedPowerUpDuration(float delta)
    {
        if (_selectedPowerUps.Count == 0)
        {
            return;
        }

        SetSelectedPowerUpDuration(_selectedPowerUps[0].DurationSeconds + delta);
    }

    private void AdjustSelectedPowerUpMultiplier(float delta)
    {
        if (_selectedPowerUps.Count == 0)
        {
            return;
        }

        SetSelectedPowerUpMultiplier(_selectedPowerUps[0].Multiplier + delta);
    }

    private void AdjustSelectedPowerUpRespawn(float delta)
    {
        if (_selectedPowerUps.Count == 0)
        {
            return;
        }

        SetSelectedPowerUpRespawn(_selectedPowerUps[0].RespawnSeconds + delta);
    }

    private void SelectSinglePowerUp(PowerUp powerUp)
    {
        ClearSelection();
        ClearPropsValueEdit(commit: false);
        _selectedPowerUps.Add(powerUp);
        _selectedPowerUp = powerUp;
        _lavaSelected = false;
        _playerSpawnSelected = false;
    }

    private void ToggleSelection(PowerUp powerUp)
    {
        _lavaSelected = false;
        _playerSpawnSelected = false;
        if (_selectedPowerUps.Contains(powerUp))
        {
            _selectedPowerUps.Remove(powerUp);
            _selectedPowerUp = _selectedPowerUps.Count > 0 ? _selectedPowerUps[^1] : null;
            return;
        }

        _selectedPowerUps.Add(powerUp);
        _selectedPowerUp = powerUp;
    }

    private void StartResize(PowerUp powerUp, ResizeHandle handle, Point mouse)
    {
        SelectSinglePowerUp(powerUp);
        BeginHistoryGesture();
        _activeHandle = handle;
        _isResizing = true;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isDraggingPowerUp = false;
        _isCreating = false;
        _resizeStartBounds = powerUp.Bounds;
        _resizeStartMouse = _snapToGrid ? Snap(mouse) : mouse;
    }

    private void MoveSelectedPowerUps(Point mouse)
    {
        Point delta = GetDelta(mouse, _powerUpDragStartMouse);
        if (_snapToGrid)
        {
            delta = SnapDelta(delta);
        }

        bool movedAny = false;
        foreach (PowerUp selectedPowerUp in _selectedPowerUps)
        {
            if (!_powerUpDragStartBounds.TryGetValue(selectedPowerUp, out Rectangle startBounds))
            {
                continue;
            }

            Rectangle nextBounds = new(
                startBounds.X + delta.X,
                startBounds.Y + delta.Y,
                startBounds.Width,
                startBounds.Height);
            nextBounds = SnapRectangleToGrid(nextBounds);
            if (selectedPowerUp.Bounds == nextBounds)
            {
                continue;
            }

            selectedPowerUp.Bounds = nextBounds;
            movedAny = true;
        }

        if (movedAny)
        {
            _level.RecalculateWorldSize();
            _isDirty = true;
        }
    }

    private void ResizeSelectedPowerUp(Point mouse)
    {
        Point resizeMouse = _snapToGrid ? Snap(mouse) : mouse;
        Point delta = GetDelta(resizeMouse, _resizeStartMouse);
        Rectangle nextBounds = ResizeBounds(_resizeStartBounds, _activeHandle, delta);
        if (_snapToGrid)
        {
            nextBounds = SnapRectangleToGrid(nextBounds);
        }

        if (_selectedPowerUp.Bounds == nextBounds)
        {
            return;
        }

        _selectedPowerUp.Bounds = nextBounds;
        _level.RecalculateWorldSize();
        _isDirty = true;
    }

    private PowerUp FindPowerUpAt(Point point)
    {
        for (int i = _level.PowerUps.Count - 1; i >= 0; i--)
        {
            PowerUp powerUp = _level.PowerUps[i];
            if (powerUp.Bounds.Contains(point))
            {
                return powerUp;
            }
        }

        return null;
    }
}
