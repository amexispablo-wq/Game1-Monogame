using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class EditorScene : IScene
{
    private const int GridSize = 32;
    private const int ResizeMargin = 8;
    private const int MinPlatformSize = GridSize; // Must be at least one grid cell

    private readonly ColorBlocksGame _game;
    private readonly string _levelId;
    private readonly Level _level;
    private readonly Camera _camera;
    private readonly Button _backButton = new("Back to Menu") { TextScale = 2 };
    private readonly Button _applyButton = new("Apply") { TextScale = 2 };

    private Platform _selectedPlatform;
    private Platform _hoveredPlatform;
    private Goal _selectedGoal;
    private Goal _hoveredGoal;
    private CheckpointFlag _selectedCheckpoint;
    private CheckpointFlag _hoveredCheckpoint;
    private LaunchPad _selectedLaunchPad;
    private LaunchPad _hoveredLaunchPad;
    private ResizeHandle _activeHandle;
    private Rectangle _resizeStartBounds;
    private Point _resizeStartMouse;
    private readonly List<Platform> _selectedPlatforms = new();
    private readonly List<CheckpointFlag> _selectedCheckpoints = new();
    private readonly List<LaunchPad> _selectedLaunchPads = new();
    private readonly Dictionary<Platform, Rectangle> _dragStartBounds = new();
    private readonly Dictionary<CheckpointFlag, Point> _checkpointDragStartPositions = new();
    private readonly Dictionary<LaunchPad, Rectangle> _launchPadDragStartBounds = new();
    private readonly List<EditorClipboardItem> _clipboard = new();
    private Point _clipboardOrigin;
    private int _pasteCount;
    private Point _dragStartMouse;
    private bool _isCreating;
    private bool _isDragging;
    private bool _isDraggingGoal;
    private bool _isDraggingCheckpoint;
    private bool _isDraggingLaunchPad;
    private bool _isResizing;
    private bool _isPanningCamera;
    private Point _createStart;
    private Point _goalDragStartMouse;
    private Point _goalDragStartPosition;
    private Point _checkpointDragStartMouse;
    private Point _launchPadDragStartMouse;
    private Point _goalPreviewPosition;
    private Point _objectPreviewPosition;
    private Rectangle _previewBounds;
    private Rectangle _toolbarPanelBounds;
    private Rectangle _goalSlotBounds;
    private Rectangle _checkpointSlotBounds;
    private Rectangle _launchPadSlotBounds;
    private EditorObjectKind _toolbarDragKind = EditorObjectKind.None;
    private EditorObjectKind _hoveredToolbarKind = EditorObjectKind.None;
    private bool _snapToGrid = true;
    private GameColor _selectedColor = GameColor.Red;
    private bool _goalSlotHovered;
    private bool _checkpointSlotHovered;
    private bool _launchPadSlotHovered;
    private bool _isDraggingGoalFromToolbar;
    private bool _isDirty;
    private bool _isDraggingLavaLine;
    private bool _lavaSelected;
    private bool _lavaHovered;
    private Point _lavaDragStartMouse;
    private int _lavaDragStartY;
    private Rectangle _lavaSpeedPanelBounds;
    private Rectangle _lavaSpeedMinusBounds;
    private Rectangle _lavaSpeedPlusBounds;

    private readonly VirtualCursor _virtualCursor = new();
    private readonly UIFocusManager _uiFocus = new();
    private readonly FocusableButton _backFocus;
    private readonly FocusableButton _applyFocus;
    private bool _gamepadPrimaryWasHeld;

    private bool IsPrimaryPressed() =>
        _game.Input.LeftMousePressed || (_virtualCursor.IsActive && _game.Input.MenuConfirmPressed);

    private bool IsPrimaryHeld() =>
        _game.Input.LeftMouseHeld || (_virtualCursor.IsActive && _game.Input.MenuConfirmHeld);

    private bool IsPrimaryReleased() =>
        _game.Input.LeftMouseReleased || (_virtualCursor.IsActive && _gamepadPrimaryWasHeld && !_game.Input.MenuConfirmHeld);

    private Point UiPointer => _game.Input.UiPointerPosition;

    private bool IsDraggingToolbarObject => _toolbarDragKind != EditorObjectKind.None;
    private bool HasSelection => _selectedPlatforms.Count > 0
        || _selectedGoal is not null
        || _selectedCheckpoints.Count > 0
        || _selectedLaunchPads.Count > 0;

    public EditorScene(ColorBlocksGame game, string levelId = "level_1")
    {
        _game = game;
        _levelId = levelId;
        _level = LevelManager.LoadLevel(levelId);
        if (_level.Lava is null)
        {
            // Every level gets a lava line so it can be positioned per level.
            // Persist it so the level keeps the line even if the designer only views it.
            _level.EnsureLava();
            _isDirty = true;
        }

        _camera = new Camera(new Vector2(game.Viewport.Width * 0.5f, game.Viewport.Height * 0.5f));
        _backFocus = new FocusableButton(_backButton);
        _applyFocus = new FocusableButton(_applyButton);
    }

    public void Update(GameTime gameTime)
    {
        UpdateGamepadUi(gameTime);
        LayoutBackButton();
        LayoutEditorToolbar();
        UpdateUiFocus(gameTime);

        if (_backFocus.WasActivated || _game.Input.MenuCancelPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        if (_applyFocus.WasActivated && _isDirty)
        {
            ApplyChanges();
            return;
        }

        if (_game.Input.ExitPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        HandleKeyboard();
        LayoutLavaSpeedPanel();

        if (_lavaSelected && _level.Lava is not null && (IsPrimaryPressed() || _game.Input.MenuConfirmPressed))
        {
            if (_lavaSpeedMinusBounds.Contains(UiPointer))
            {
                AdjustLavaRiseSpeed(-10f);
                _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
                return;
            }

            if (_lavaSpeedPlusBounds.Contains(UiPointer))
            {
                AdjustLavaRiseSpeed(10f);
                _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
                return;
            }
        }

        bool mouseOverToolbar = IsMouseOverToolbar();
        bool cameraBlockedByUi = _backButton.IsHovered
            || _applyButton.IsHovered
            || (mouseOverToolbar && !IsDraggingToolbarObject);
        HandleCameraInput(cameraBlockedByUi);

        Point mouse = GetMouseWorldPosition();
        UpdateHoverState(mouse);

        if (IsDraggingToolbarObject)
        {
            ContinueToolbarObjectDrag(mouse);
            if (IsPrimaryReleased())
            {
                EndToolbarObjectDrag(mouse, IsMouseOverUi());
            }

            _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
            return;
        }

        if (IsPrimaryPressed() && _hoveredToolbarKind != EditorObjectKind.None)
        {
            BeginToolbarObjectDrag(_hoveredToolbarKind, mouse);
            _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
            return;
        }

        bool canUseWorldMouse = !IsMouseOverUi()
            || _isCreating
            || _isDragging
            || _isResizing
            || _isDraggingGoal
            || _isDraggingCheckpoint
            || _isDraggingLaunchPad
            || _isDraggingLavaLine;
        if (!canUseWorldMouse)
        {
            _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
            return;
        }

        if (IsPrimaryPressed())
        {
            BeginMouseAction(mouse);
        }

        if (IsPrimaryHeld())
        {
            ContinueMouseAction(mouse);
        }

        if (IsPrimaryReleased())
        {
            EndMouseAction();
        }

        _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
    }

    private void UpdateGamepadUi(GameTime gameTime)
    {
        _virtualCursor.BeginFrame(_game.Viewport, _game.Input);
        _virtualCursor.Update(gameTime, _game.Input, _game.Viewport);
        _game.Input.SetUiPointerOverride(_virtualCursor.IsActive ? _virtualCursor.Position : null);
        TrySnapVirtualCursorToUi();
    }

    private void TrySnapVirtualCursorToUi()
    {
        if (!_virtualCursor.IsActive)
        {
            return;
        }

        Rectangle[] snapTargets =
        {
            _backButton.Bounds,
            _applyButton.Bounds,
            _toolbarPanelBounds,
            _lavaSpeedPanelBounds
        };

        foreach (Rectangle target in snapTargets)
        {
            if (target.Width > 0 && target.Height > 0 && target.Contains(_virtualCursor.Position))
            {
                _virtualCursor.SnapTo(target);
                return;
            }
        }
    }

    private void UpdateUiFocus(GameTime gameTime)
    {
        _uiFocus.Clear();
        _uiFocus.Add(_backFocus);
        _uiFocus.Add(_applyFocus);
        if (_lavaSelected)
        {
            _uiFocus.Add(new FocusableAction(_lavaSpeedMinusBounds, () =>
            {
                AdjustLavaRiseSpeed(-10f);
                return true;
            }));
            _uiFocus.Add(new FocusableAction(_lavaSpeedPlusBounds, () =>
            {
                AdjustLavaRiseSpeed(10f);
                return true;
            }));
        }

        _uiFocus.Update(gameTime, _game.Input);
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutBackButton();
        LayoutEditorToolbar();

        Texture2D pixel = _game.Pixel;
        Viewport viewport = _game.Viewport;
        Rectangle visibleWorldBounds = _camera.GetVisibleWorldRectangle(viewport, GridSize * 2);

        spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _camera.GetTransform(viewport));

        DrawEditorBackground(spriteBatch, pixel, visibleWorldBounds);
        DrawGrid(spriteBatch, pixel, visibleWorldBounds);
        _level.DrawPlatforms(spriteBatch, pixel, debugDraw: false);
        _level.DrawLaunchPads(spriteBatch, pixel, debugDraw: false, isEditorMode: true);
        _level.DrawGoals(spriteBatch, pixel, debugDraw: false);
        _level.DrawCheckpointFlags(spriteBatch, pixel, debugDraw: false);

        if (_level.Lava is not null)
        {
            int surfaceY = _level.Lava.SurfaceY;
            LavaLine.DrawEditorLine(spriteBatch, pixel, visibleWorldBounds, surfaceY);

            if (_lavaSelected || _lavaHovered)
            {
                Color highlight = _lavaSelected ? new Color(255, 220, 80) : Color.White;
                DrawHelper.DrawBorder(
                    spriteBatch,
                    pixel,
                    new Rectangle(visibleWorldBounds.Left, surfaceY - 3, visibleWorldBounds.Width, 8),
                    highlight,
                    GetWorldLineThickness(2));
            }

            if (_lavaSelected)
            {
                string hint = $"LAVA  rise {_level.Lava.RiseSpeed:0} px/s   [ , / . ]   drag = move up/down";
                SimpleTextRenderer.DrawCentered(
                    spriteBatch,
                    pixel,
                    hint,
                    new Rectangle((int)_camera.Position.X - 260, surfaceY - 30, 520, 20),
                    1,
                    new Color(255, 220, 80));
            }
        }

        if (_hoveredPlatform is not null && !_selectedPlatforms.Contains(_hoveredPlatform))
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _hoveredPlatform.Bounds, Color.White, GetWorldLineThickness(2));
        }

        if (_hoveredGoal is not null && _hoveredGoal != _selectedGoal)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _hoveredGoal.Bounds, Color.White, GetWorldLineThickness(2));
        }

        if (_hoveredCheckpoint is not null && !_selectedCheckpoints.Contains(_hoveredCheckpoint))
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _hoveredCheckpoint.Bounds, Color.White, GetWorldLineThickness(2));
        }

        if (_hoveredLaunchPad is not null && !_selectedLaunchPads.Contains(_hoveredLaunchPad))
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _hoveredLaunchPad.Bounds, Color.White, GetWorldLineThickness(2));
        }

        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, selectedPlatform.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
        }

        if (_selectedGoal is not null)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _selectedGoal.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
        }

        foreach (CheckpointFlag selectedCheckpoint in _selectedCheckpoints)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, selectedCheckpoint.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
        }

        foreach (LaunchPad selectedLaunchPad in _selectedLaunchPads)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, selectedLaunchPad.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
            // Draw rotation hint above the selected launch pad
            Vector2 hintPosition = new(selectedLaunchPad.Center.X, selectedLaunchPad.Bounds.Top - 24);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Q/E to rotate", new Rectangle((int)hintPosition.X - 60, (int)hintPosition.Y, 120, 20), 1, new Color(255, 220, 80));
        }

        if (_selectedPlatforms.Count == 1 && _selectedPlatform is not null)
        {
            DrawResizeHandles(spriteBatch, pixel, _selectedPlatform.Bounds);
        }

        if (_selectedLaunchPads.Count == 1 && _selectedLaunchPad is not null)
        {
            DrawResizeHandles(spriteBatch, pixel, _selectedLaunchPad.Bounds);
        }

        if (_isCreating && _previewBounds.Width > 0 && _previewBounds.Height > 0)
        {
            spriteBatch.Draw(pixel, _previewBounds, Color.White * 0.18f);
            DrawHelper.DrawBorder(spriteBatch, pixel, _previewBounds, _selectedColor.ToXnaColor(), GetWorldLineThickness(2));
        }

        if (IsDraggingToolbarObject)
        {
            DrawToolbarObjectPreview(spriteBatch, pixel, _toolbarDragKind, _objectPreviewPosition);
        }

        spriteBatch.End();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawEditorUi(spriteBatch, pixel);
        DrawToolbar(spriteBatch, pixel);
        DrawLavaSpeedPanel(spriteBatch, pixel);
        DrawApplyButton(spriteBatch, pixel);
        _backButton.Draw(spriteBatch, pixel);
        _uiFocus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);
        if (_virtualCursor.IsActive)
        {
            Rectangle cursor = new(UiPointer.X - 6, UiPointer.Y - 6, 12, 12);
            spriteBatch.Draw(pixel, cursor, new Color(255, 220, 80));
            DrawHelper.DrawBorder(spriteBatch, pixel, cursor, Color.White, 1);
        }

        spriteBatch.End();
    }

    public void OnExit()
    {
    }

    private void HandleKeyboard()
    {
        if (_game.Input.ControlHeld)
        {
            if (_game.Input.IsNewKeyPress(Keys.C))
            {
                CopySelectedObjects();
                return;
            }

            if (_game.Input.IsNewKeyPress(Keys.V))
            {
                PasteClipboard();
                return;
            }
        }

        if (_game.Input.IsNewKeyPress(Keys.T))
        {
            _snapToGrid = !_snapToGrid;
        }

        if (_game.Input.IsNewKeyPress(Keys.S))
        {
            if (_isDirty)
            {
                ApplyChanges();
            }
        }

        if (_game.Input.IsNewKeyPress(Keys.Q))
        {
            RotateSelectedLaunchPads(-15f);
        }

        if (_game.Input.IsNewKeyPress(Keys.E))
        {
            RotateSelectedLaunchPads(15f);
        }

        if (_game.Input.IsNewKeyPress(Keys.Delete) && HasSelection)
        {
            DeleteSelectedObjects();
            _isDragging = false;
            _isDraggingGoal = false;
            _isDraggingCheckpoint = false;
            _isDraggingLaunchPad = false;
            _isResizing = false;
            _activeHandle = ResizeHandle.None;
            return;
        }

        if (_lavaSelected && _level.Lava is not null)
        {
            if (_game.Input.IsNewKeyPress(Keys.OemComma) || _game.Input.IsNewKeyPress(Keys.OemMinus))
            {
                _level.Lava.RiseSpeed = Math.Max(LavaLine.MinRiseSpeed, _level.Lava.RiseSpeed - 10f);
                _isDirty = true;
            }

            if (_game.Input.IsNewKeyPress(Keys.OemPeriod) || _game.Input.IsNewKeyPress(Keys.OemPlus))
            {
                _level.Lava.RiseSpeed = Math.Min(LavaLine.MaxRiseSpeed, _level.Lava.RiseSpeed + 10f);
                _isDirty = true;
            }
        }

        if (!_game.Input.ControlHeld && _game.Input.RequestedColor is { } requestedColor)
        {
            _selectedColor = requestedColor;
            ApplyColorToSelection(requestedColor);
        }
    }

    private void HandleCameraInput(bool mouseOverUi)
    {
        bool canStartPanning = !_isCreating
            && !_isDragging
            && !_isDraggingGoal
            && !_isDraggingCheckpoint
            && !_isDraggingLaunchPad
            && !_isDraggingLavaLine
            && !_isResizing
            && !mouseOverUi;
        if (canStartPanning && (_game.Input.MiddleMousePressed || _game.Input.RightMousePressed))
        {
            _isPanningCamera = true;
        }

        if (_isPanningCamera)
        {
            if (_game.Input.MiddleMouseHeld || _game.Input.RightMouseHeld)
            {
                _camera.PanByScreenDelta(_game.Input.MouseDelta);
            }
            else
            {
                _isPanningCamera = false;
            }
        }

        if (!mouseOverUi && _game.Input.MouseWheelDelta != 0)
        {
            float zoomFactor = MathF.Pow(1.0015f, _game.Input.MouseWheelDelta);
            _camera.ZoomAt(zoomFactor, UiPointer, _game.Viewport);
        }

        if (!mouseOverUi && _game.Input.IsAnyGamepadConnected())
        {
            Vector2 stick = _game.Input.GetMenuLeftStick();
            if (stick.LengthSquared() > 0.04f)
            {
                Vector2 pan = stick * 14f;
                _camera.PanByScreenDelta(new Point((int)MathF.Round(pan.X), (int)MathF.Round(-pan.Y)));
            }

            if (_game.Input.EditorLeftTrigger > 0.1f)
            {
                float zoomFactor = MathF.Pow(1.02f, _game.Input.EditorLeftTrigger * 4f);
                _camera.ZoomAt(zoomFactor, UiPointer, _game.Viewport);
            }

            if (_game.Input.EditorRightTrigger > 0.1f)
            {
                float zoomFactor = MathF.Pow(0.98f, _game.Input.EditorRightTrigger * 4f);
                _camera.ZoomAt(zoomFactor, UiPointer, _game.Viewport);
            }
        }
    }

    private void BeginMouseAction(Point mouse)
    {
        if (_isPanningCamera)
        {
            return;
        }

        LaunchPad clickedLaunchPad = FindLaunchPadAt(mouse);
        CheckpointFlag clickedCheckpoint = FindCheckpointAt(mouse);
        Goal clickedGoal = FindGoalAt(mouse);
        Platform clickedPlatform = FindPlatformAt(mouse);

        if (_game.Input.ShiftHeld)
        {
            if (clickedLaunchPad is not null)
            {
                ToggleSelection(clickedLaunchPad);
            }
            else if (clickedCheckpoint is not null)
            {
                ToggleSelection(clickedCheckpoint);
            }
            else if (clickedPlatform is not null)
            {
                ToggleSelection(clickedPlatform);
            }
            else if (clickedGoal is not null)
            {
                SelectSingleGoal(clickedGoal);
            }

            return;
        }

        if (_selectedLaunchPads.Count == 1 && _selectedLaunchPad is not null)
        {
            ResizeHandle selectedHandle = GetResizeHandle(_selectedLaunchPad.Bounds, mouse);
            if (selectedHandle != ResizeHandle.None)
            {
                StartResize(_selectedLaunchPad, selectedHandle, mouse);
                return;
            }
        }

        if (_selectedPlatforms.Count == 1 && _selectedPlatform is not null)
        {
            ResizeHandle selectedHandle = GetResizeHandle(_selectedPlatform.Bounds, mouse);
            if (selectedHandle != ResizeHandle.None)
            {
                StartResize(_selectedPlatform, selectedHandle, mouse);
                return;
            }
        }

        if (clickedLaunchPad is not null)
        {
            if (!_selectedLaunchPads.Contains(clickedLaunchPad))
            {
                SelectSingleLaunchPad(clickedLaunchPad);
            }
            else
            {
                _selectedLaunchPad = clickedLaunchPad;
            }

            ResizeHandle clickedHandle = _selectedLaunchPads.Count == 1
                ? GetResizeHandle(clickedLaunchPad.Bounds, mouse)
                : ResizeHandle.None;
            if (clickedHandle != ResizeHandle.None)
            {
                StartResize(clickedLaunchPad, clickedHandle, mouse);
                return;
            }

            StartLaunchPadDrag(clickedLaunchPad, mouse);
            return;
        }

        if (clickedCheckpoint is not null)
        {
            if (!_selectedCheckpoints.Contains(clickedCheckpoint))
            {
                SelectSingleCheckpoint(clickedCheckpoint);
            }
            else
            {
                _selectedCheckpoint = clickedCheckpoint;
            }

            StartCheckpointDrag(clickedCheckpoint, mouse);
            return;
        }

        if (clickedGoal is not null)
        {
            SelectSingleGoal(clickedGoal);
            StartGoalDrag(clickedGoal, mouse);
            return;
        }

        if (clickedPlatform is not null)
        {
            if (!_selectedPlatforms.Contains(clickedPlatform))
            {
                SelectSinglePlatform(clickedPlatform);
            }
            else
            {
                _selectedPlatform = clickedPlatform;
            }

            ResizeHandle clickedHandle = _selectedPlatforms.Count == 1
                ? GetResizeHandle(clickedPlatform.Bounds, mouse)
                : ResizeHandle.None;
            if (clickedHandle != ResizeHandle.None)
            {
                StartResize(clickedPlatform, clickedHandle, mouse);
                return;
            }

            StartDrag(clickedPlatform, mouse);
            return;
        }

        if (_level.Lava is not null && _level.Lava.HitTest(mouse))
        {
            StartLavaDrag(mouse);
            return;
        }

        ClearSelection();
        _isCreating = true;
        _isDragging = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
        _createStart = Snap(mouse);
        _previewBounds = new Rectangle(_createStart.X, _createStart.Y, 0, 0);
    }

    private void ContinueMouseAction(Point mouse)
    {
        if (_isCreating)
        {
            _previewBounds = BuildRectangle(_createStart, Snap(mouse));
            return;
        }

        if (_isDragging && _selectedPlatforms.Count > 0)
        {
            MoveSelectedPlatforms(mouse);
            return;
        }

        if (_isDraggingGoal && _selectedGoal is not null)
        {
            MoveSelectedGoal(mouse);
            return;
        }

        if (_isDraggingCheckpoint && _selectedCheckpoints.Count > 0)
        {
            MoveSelectedCheckpoints(mouse);
            return;
        }

        if (_isDraggingLaunchPad && _selectedLaunchPads.Count > 0)
        {
            MoveSelectedLaunchPads(mouse);
            return;
        }

        if (_isDraggingLavaLine)
        {
            MoveLavaLine(mouse);
            return;
        }

        if (_isResizing && _selectedPlatform is not null && _activeHandle != ResizeHandle.None)
        {
            ResizeSelectedPlatform(mouse);
            return;
        }

        if (_isResizing && _selectedLaunchPad is not null && _activeHandle != ResizeHandle.None)
        {
            ResizeSelectedLaunchPad(mouse);
        }
    }

    private void EndMouseAction()
    {
        if (_isCreating && _previewBounds.Width >= MinPlatformSize && _previewBounds.Height >= MinPlatformSize)
        {
            Rectangle snappedBounds = SnapRectangleToGrid(_previewBounds);
            Platform platform = new(snappedBounds, _selectedColor);
            _level.AddPlatform(platform);
            SelectSinglePlatform(platform);
            _isDirty = true;
        }

        _isCreating = false;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isDraggingLavaLine = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
    }

    private void StartLavaDrag(Point mouse)
    {
        ClearSelection();
        _lavaSelected = true;
        _isDraggingLavaLine = true;
        _isCreating = false;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
        _lavaDragStartMouse = mouse;
        _lavaDragStartY = _level.Lava!.SurfaceY;
    }

    private void MoveLavaLine(Point mouse)
    {
        if (_level.Lava is null)
        {
            return;
        }

        // Vertical-only: horizontal mouse movement is ignored.
        int nextY = _lavaDragStartY + (mouse.Y - _lavaDragStartMouse.Y);
        if (_snapToGrid)
        {
            nextY = SnapToGrid(nextY);
        }

        if (_level.Lava.SurfaceY == nextY)
        {
            return;
        }

        _level.Lava.SurfaceY = nextY;
        _isDirty = true;
    }

    private void StartDrag(Platform platform, Point mouse)
    {
        if (!_selectedPlatforms.Contains(platform))
        {
            SelectSinglePlatform(platform);
        }
        else
        {
            _selectedPlatform = platform;
        }

        _isDragging = true;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isResizing = false;
        _isCreating = false;
        _dragStartMouse = mouse;
        _dragStartBounds.Clear();
        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            _dragStartBounds[selectedPlatform] = selectedPlatform.Bounds;
        }
    }

    private void StartResize(Platform platform, ResizeHandle handle, Point mouse)
    {
        SelectSinglePlatform(platform);
        _activeHandle = handle;
        _isResizing = true;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isCreating = false;
        _resizeStartBounds = platform.Bounds;
        _resizeStartMouse = _snapToGrid ? Snap(mouse) : mouse;
    }

    private void StartResize(LaunchPad launchPad, ResizeHandle handle, Point mouse)
    {
        SelectSingleLaunchPad(launchPad);
        _activeHandle = handle;
        _isResizing = true;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isCreating = false;
        _resizeStartBounds = launchPad.Bounds;
        _resizeStartMouse = _snapToGrid ? Snap(mouse) : mouse;
    }

    private void SelectSingleGoal(Goal goal)
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
        _selectedGoal = goal;
    }

    private void StartGoalDrag(Goal goal, Point mouse)
    {
        SelectSingleGoal(goal);
        _isDraggingGoal = true;
        _isDragging = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _goalDragStartMouse = mouse;
        _goalDragStartPosition = goal.Position;
    }

    private void SelectSingleCheckpoint(CheckpointFlag checkpoint)
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoal = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoints.Add(checkpoint);
        _selectedCheckpoint = checkpoint;
    }

    private void StartCheckpointDrag(CheckpointFlag checkpoint, Point mouse)
    {
        if (!_selectedCheckpoints.Contains(checkpoint))
        {
            SelectSingleCheckpoint(checkpoint);
        }

        _isDraggingCheckpoint = true;
        _isDraggingLaunchPad = false;
        _isDraggingGoal = false;
        _isDragging = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _checkpointDragStartMouse = mouse;
        _checkpointDragStartPositions.Clear();
        foreach (CheckpointFlag selectedCheckpoint in _selectedCheckpoints)
        {
            _checkpointDragStartPositions[selectedCheckpoint] = selectedCheckpoint.Position;
        }
    }

    private void SelectSingleLaunchPad(LaunchPad launchPad)
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPads.Add(launchPad);
        _selectedLaunchPad = launchPad;
    }

    private void StartLaunchPadDrag(LaunchPad launchPad, Point mouse)
    {
        if (!_selectedLaunchPads.Contains(launchPad))
        {
            SelectSingleLaunchPad(launchPad);
        }

        _isDraggingLaunchPad = true;
        _isDraggingCheckpoint = false;
        _isDraggingGoal = false;
        _isDragging = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _launchPadDragStartMouse = mouse;
        _launchPadDragStartBounds.Clear();
        foreach (LaunchPad selectedLaunchPad in _selectedLaunchPads)
        {
            _launchPadDragStartBounds[selectedLaunchPad] = selectedLaunchPad.Bounds;
        }
    }

    private void MoveSelectedGoal(Point mouse)
    {
        if (_selectedGoal is null)
        {
            return;
        }

        Point delta = GetDelta(mouse, _goalDragStartMouse);
        if (_snapToGrid)
        {
            delta = SnapDelta(delta);
        }

        Point nextPosition = new(
            _goalDragStartPosition.X + delta.X,
            _goalDragStartPosition.Y + delta.Y);

        if (_snapToGrid)
        {
            nextPosition = Snap(nextPosition);
        }

        if (_selectedGoal.Position == nextPosition)
        {
            return;
        }

        _selectedGoal.Position = nextPosition;
        _level.RecalculateWorldSize();
        _isDirty = true;
    }

    private void MoveSelectedCheckpoints(Point mouse)
    {
        Point delta = GetDelta(mouse, _checkpointDragStartMouse);
        if (_snapToGrid)
        {
            delta = SnapDelta(delta);
        }

        bool movedAnyCheckpoint = false;
        foreach (CheckpointFlag selectedCheckpoint in _selectedCheckpoints)
        {
            if (!_checkpointDragStartPositions.TryGetValue(selectedCheckpoint, out Point startPosition))
            {
                continue;
            }

            Point nextPosition = new(startPosition.X + delta.X, startPosition.Y + delta.Y);
            if (_snapToGrid)
            {
                nextPosition = Snap(nextPosition);
            }

            if (selectedCheckpoint.Position == nextPosition)
            {
                continue;
            }

            selectedCheckpoint.Position = nextPosition;
            movedAnyCheckpoint = true;
        }

        if (movedAnyCheckpoint)
        {
            _level.RecalculateWorldSize();
            _isDirty = true;
        }
    }

    private void MoveSelectedLaunchPads(Point mouse)
    {
        Point delta = GetDelta(mouse, _launchPadDragStartMouse);
        if (_snapToGrid)
        {
            delta = SnapDelta(delta);
        }

        bool movedAnyLaunchPad = false;
        foreach (LaunchPad selectedLaunchPad in _selectedLaunchPads)
        {
            if (!_launchPadDragStartBounds.TryGetValue(selectedLaunchPad, out Rectangle startBounds))
            {
                continue;
            }

            Rectangle nextBounds = new(
                startBounds.X + delta.X,
                startBounds.Y + delta.Y,
                startBounds.Width,
                startBounds.Height);

            if (_snapToGrid)
            {
                nextBounds = SnapRectangleToGrid(nextBounds);
            }

            if (selectedLaunchPad.Bounds == nextBounds)
            {
                continue;
            }

            selectedLaunchPad.Bounds = nextBounds;
            movedAnyLaunchPad = true;
        }

        if (movedAnyLaunchPad)
        {
            _level.RecalculateWorldSize();
            _isDirty = true;
        }
    }

    private void BeginToolbarObjectDrag(EditorObjectKind kind, Point mouse)
    {
        ClearSelection();
        _toolbarDragKind = kind;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isDragging = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _objectPreviewPosition = GetObjectPlacementPosition(mouse);
        _goalPreviewPosition = _objectPreviewPosition;
    }

    private void ContinueToolbarObjectDrag(Point mouse)
    {
        _objectPreviewPosition = GetObjectPlacementPosition(mouse);
        _goalPreviewPosition = _objectPreviewPosition;
    }

    private void EndToolbarObjectDrag(Point mouse, bool releaseOverUi)
    {
        _objectPreviewPosition = GetObjectPlacementPosition(mouse);
        _goalPreviewPosition = _objectPreviewPosition;

        if (!releaseOverUi)
        {
            switch (_toolbarDragKind)
            {
                case EditorObjectKind.Goal:
                    {
                        Goal goal = new(_objectPreviewPosition);
                        _level.AddGoal(goal);
                        SelectSingleGoal(goal);
                        _isDirty = true;
                        break;
                    }
                case EditorObjectKind.CheckpointFlag:
                    {
                        CheckpointFlag checkpoint = new(_objectPreviewPosition);
                        _level.AddCheckpointFlag(checkpoint);
                        SelectSingleCheckpoint(checkpoint);
                        _isDirty = true;
                        break;
                    }
                case EditorObjectKind.LaunchPad:
                    {
                        Rectangle bounds = new(_objectPreviewPosition.X, _objectPreviewPosition.Y, LaunchPad.DefaultWidth, LaunchPad.DefaultHeight);
                        bounds = SnapRectangleToGrid(bounds);
                        LaunchPad launchPad = new(bounds);
                        _level.AddLaunchPad(launchPad);
                        SelectSingleLaunchPad(launchPad);
                        _isDirty = true;
                        break;
                    }
            }
        }

        _toolbarDragKind = EditorObjectKind.None;
    }

    private void MoveSelectedPlatforms(Point mouse)
    {
        Point delta = GetDelta(mouse, _dragStartMouse);
        if (_snapToGrid)
        {
            delta = SnapDelta(delta);
        }

        bool movedAnyPlatform = false;
        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            if (!_dragStartBounds.TryGetValue(selectedPlatform, out Rectangle startBounds))
            {
                continue;
            }

            Rectangle nextBounds = new(
                startBounds.X + delta.X,
                startBounds.Y + delta.Y,
                startBounds.Width,
                startBounds.Height);

            // Snap position to grid to prevent drift
            nextBounds = SnapRectangleToGrid(nextBounds);

            if (selectedPlatform.Bounds == nextBounds)
            {
                continue;
            }

            selectedPlatform.Bounds = nextBounds;
            movedAnyPlatform = true;
        }

        if (movedAnyPlatform)
        {
            _level.RecalculateWorldSize();
            _isDirty = true;
        }
    }

    private void ResizeSelectedPlatform(Point mouse)
    {
        Point resizeMouse = _snapToGrid ? Snap(mouse) : mouse;
        Point delta = GetDelta(resizeMouse, _resizeStartMouse);

        Rectangle nextBounds = ResizeBounds(_resizeStartBounds, _activeHandle, delta);

        // Snap to grid to prevent drift
        nextBounds = SnapRectangleToGrid(nextBounds);

        if (_selectedPlatform.Bounds == nextBounds)
        {
            return;
        }

        _selectedPlatform.Bounds = nextBounds;
        _level.RecalculateWorldSize();
        _isDirty = true;
    }

    private void ResizeSelectedLaunchPad(Point mouse)
    {
        Point resizeMouse = _snapToGrid ? Snap(mouse) : mouse;
        Point delta = GetDelta(resizeMouse, _resizeStartMouse);
        Rectangle nextBounds = ResizeBounds(_resizeStartBounds, _activeHandle, delta);

        if (_snapToGrid)
        {
            nextBounds = SnapRectangleToGrid(nextBounds);
        }

        if (_selectedLaunchPad.Bounds == nextBounds)
        {
            return;
        }

        _selectedLaunchPad.Bounds = nextBounds;
        _level.RecalculateWorldSize();
        _isDirty = true;
    }

    private void SelectSinglePlatform(Platform platform)
    {
        _selectedPlatforms.Clear();
        _selectedPlatforms.Add(platform);
        _selectedPlatform = platform;
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
    }

    private void ToggleSelection(Platform platform)
    {
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;

        if (_selectedPlatforms.Contains(platform))
        {
            _selectedPlatforms.Remove(platform);
            _selectedPlatform = _selectedPlatforms.Count > 0 ? _selectedPlatforms[^1] : null;
            return;
        }

        _selectedPlatforms.Add(platform);
        _selectedPlatform = platform;
    }

    private void ToggleSelection(CheckpointFlag checkpoint)
    {
        _selectedGoal = null;
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;

        if (_selectedCheckpoints.Contains(checkpoint))
        {
            _selectedCheckpoints.Remove(checkpoint);
            _selectedCheckpoint = _selectedCheckpoints.Count > 0 ? _selectedCheckpoints[^1] : null;
            return;
        }

        _selectedCheckpoints.Add(checkpoint);
        _selectedCheckpoint = checkpoint;
    }

    private void ToggleSelection(LaunchPad launchPad)
    {
        _selectedGoal = null;
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;

        if (_selectedLaunchPads.Contains(launchPad))
        {
            _selectedLaunchPads.Remove(launchPad);
            _selectedLaunchPad = _selectedLaunchPads.Count > 0 ? _selectedLaunchPads[^1] : null;
            return;
        }

        _selectedLaunchPads.Add(launchPad);
        _selectedLaunchPad = launchPad;
    }

    private void ClearSelection()
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
        _lavaSelected = false;
    }

    private void DeleteSelectedObjects()
    {
        for (int i = _selectedPlatforms.Count - 1; i >= 0; i--)
        {
            _level.RemovePlatform(_selectedPlatforms[i]);
        }

        for (int i = _selectedCheckpoints.Count - 1; i >= 0; i--)
        {
            _level.RemoveCheckpointFlag(_selectedCheckpoints[i]);
        }

        for (int i = _selectedLaunchPads.Count - 1; i >= 0; i--)
        {
            _level.RemoveLaunchPad(_selectedLaunchPads[i]);
        }

        if (_selectedGoal is not null)
        {
            _level.RemoveGoal(_selectedGoal);
            _hoveredGoal = null;
        }

        _hoveredCheckpoint = null;
        _hoveredLaunchPad = null;
        ClearSelection();
        _isDirty = true;
    }

    private void ApplyColorToSelection(GameColor color)
    {
        bool changedAnyPlatform = false;
        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            if (selectedPlatform.PlatformColor == color)
            {
                continue;
            }

            selectedPlatform.PlatformColor = color;
            changedAnyPlatform = true;
        }

        if (changedAnyPlatform)
        {
            _isDirty = true;
        }
    }

    private void CopySelectedObjects()
    {
        _clipboard.Clear();
        if (!TryGetSelectionOrigin(out _clipboardOrigin))
        {
            return;
        }

        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            Rectangle bounds = selectedPlatform.Bounds;
            _clipboard.Add(new EditorClipboardItem(
                EditorObjectKind.Platform,
                bounds.X - _clipboardOrigin.X,
                bounds.Y - _clipboardOrigin.Y,
                bounds.Width,
                bounds.Height,
                selectedPlatform.PlatformColor,
                0f));
        }

        if (_selectedGoal is not null)
        {
            Rectangle bounds = _selectedGoal.Bounds;
            _clipboard.Add(new EditorClipboardItem(
                EditorObjectKind.Goal,
                bounds.X - _clipboardOrigin.X,
                bounds.Y - _clipboardOrigin.Y,
                bounds.Width,
                bounds.Height,
                GameColor.Red,
                0f));
        }

        foreach (CheckpointFlag selectedCheckpoint in _selectedCheckpoints)
        {
            Rectangle bounds = selectedCheckpoint.Bounds;
            _clipboard.Add(new EditorClipboardItem(
                EditorObjectKind.CheckpointFlag,
                bounds.X - _clipboardOrigin.X,
                bounds.Y - _clipboardOrigin.Y,
                bounds.Width,
                bounds.Height,
                GameColor.Red,
                0f));
        }

        foreach (LaunchPad selectedLaunchPad in _selectedLaunchPads)
        {
            Rectangle bounds = selectedLaunchPad.Bounds;
            _clipboard.Add(new EditorClipboardItem(
                EditorObjectKind.LaunchPad,
                bounds.X - _clipboardOrigin.X,
                bounds.Y - _clipboardOrigin.Y,
                bounds.Width,
                bounds.Height,
                GameColor.Red,
                selectedLaunchPad.RotationDegrees));
        }

        _pasteCount = 1;
    }

    private void PasteClipboard()
    {
        if (_clipboard.Count == 0)
        {
            return;
        }

        // Use GridSize for paste offset to maintain grid alignment
        Point pasteOffset = new(GridSize, GridSize);
        Point pasteOrigin = new(
            SnapToGrid(_clipboardOrigin.X + pasteOffset.X * _pasteCount),
            SnapToGrid(_clipboardOrigin.Y + pasteOffset.Y * _pasteCount));

        ClearSelection();
        foreach (EditorClipboardItem item in _clipboard)
        {
            Point position = new(pasteOrigin.X + item.X, pasteOrigin.Y + item.Y);
            if (_snapToGrid)
            {
                position = Snap(position);
            }

            switch (item.Kind)
            {
                case EditorObjectKind.Platform:
                    {
                        Rectangle platformBounds = new(position.X, position.Y, item.Width, item.Height);
                        platformBounds = SnapRectangleToGrid(platformBounds);
                        Platform platform = new(platformBounds, item.Color);
                        _level.AddPlatform(platform);
                        _selectedPlatforms.Add(platform);
                        _selectedPlatform = platform;
                        break;
                    }
                case EditorObjectKind.Goal:
                    {
                        Goal goal = new(position);
                        _level.AddGoal(goal);
                        _selectedGoal = goal;
                        break;
                    }
                case EditorObjectKind.CheckpointFlag:
                    {
                        CheckpointFlag checkpoint = new(position);
                        _level.AddCheckpointFlag(checkpoint);
                        _selectedCheckpoints.Add(checkpoint);
                        _selectedCheckpoint = checkpoint;
                        break;
                    }
                case EditorObjectKind.LaunchPad:
                    {
                        Rectangle launchPadBounds = new(position.X, position.Y, Math.Max(GridSize, item.Width), Math.Max(GridSize, item.Height));
                        if (_snapToGrid)
                        {
                            launchPadBounds = SnapRectangleToGrid(launchPadBounds);
                        }

                        LaunchPad launchPad = new(launchPadBounds, item.RotationDegrees);
                        _level.AddLaunchPad(launchPad);
                        _selectedLaunchPads.Add(launchPad);
                        _selectedLaunchPad = launchPad;
                        break;
                    }
            }
        }

        _pasteCount++;
        _isDirty = true;
    }

    private bool TryGetSelectionOrigin(out Point origin)
    {
        if (_selectedPlatforms.Count > 0)
        {
            origin = _selectedPlatforms[0].Bounds.Location;
            return true;
        }

        if (_selectedGoal is not null)
        {
            origin = _selectedGoal.Bounds.Location;
            return true;
        }

        if (_selectedCheckpoints.Count > 0)
        {
            origin = _selectedCheckpoints[0].Bounds.Location;
            return true;
        }

        if (_selectedLaunchPads.Count > 0)
        {
            origin = _selectedLaunchPads[0].Bounds.Location;
            return true;
        }

        origin = Point.Zero;
        return false;
    }

    private Point GetMouseWorldPosition()
    {
        Vector2 worldPosition = _camera.ScreenToWorld(UiPointer, _game.Viewport);
        return new Point((int)MathF.Round(worldPosition.X), (int)MathF.Round(worldPosition.Y));
    }

    private Point Snap(Point point)
    {
        if (!_snapToGrid)
        {
            return point;
        }

        int x = (int)MathF.Round(point.X / (float)GridSize) * GridSize;
        int y = (int)MathF.Round(point.Y / (float)GridSize) * GridSize;
        return new Point(x, y);
    }

    private Point GetObjectPlacementPosition(Point mouse)
    {
        return _snapToGrid ? Snap(mouse) : mouse;
    }

    private static Point SnapDelta(Point delta)
    {
        int x = (int)MathF.Round(delta.X / (float)GridSize) * GridSize;
        int y = (int)MathF.Round(delta.Y / (float)GridSize) * GridSize;
        return new Point(x, y);
    }

    private static int SnapToGrid(int value)
    {
        return (int)MathF.Round(value / (float)GridSize) * GridSize;
    }

    private static Rectangle SnapRectangleToGrid(Rectangle rect)
    {
        int snappedX = SnapToGrid(rect.X);
        int snappedY = SnapToGrid(rect.Y);
        int snappedWidth = Math.Max(GridSize, SnapToGrid(rect.Width));
        int snappedHeight = Math.Max(GridSize, SnapToGrid(rect.Height));

        return new Rectangle(snappedX, snappedY, snappedWidth, snappedHeight);
    }

    private Platform FindPlatformAt(Point point)
    {
        for (int i = _level.Platforms.Count - 1; i >= 0; i--)
        {
            Platform platform = _level.Platforms[i];
            if (platform.Bounds.Contains(point))
            {
                return platform;
            }
        }

        return null;
    }

    private Goal FindGoalAt(Point point)
    {
        for (int i = _level.Goals.Count - 1; i >= 0; i--)
        {
            Goal goal = _level.Goals[i];
            if (goal.Bounds.Contains(point))
            {
                return goal;
            }
        }

        return null;
    }

    private CheckpointFlag FindCheckpointAt(Point point)
    {
        for (int i = _level.CheckpointFlags.Count - 1; i >= 0; i--)
        {
            CheckpointFlag checkpoint = _level.CheckpointFlags[i];
            if (checkpoint.Bounds.Contains(point))
            {
                return checkpoint;
            }
        }

        return null;
    }

    private LaunchPad FindLaunchPadAt(Point point)
    {
        for (int i = _level.LaunchPads.Count - 1; i >= 0; i--)
        {
            LaunchPad launchPad = _level.LaunchPads[i];
            if (launchPad.Bounds.Contains(point))
            {
                return launchPad;
            }
        }

        return null;
    }

    private void UpdateHoverState(Point mouse)
    {
        bool objectActionActive = _isCreating || _isDragging || _isDraggingGoal || _isDraggingCheckpoint || _isDraggingLaunchPad || _isDraggingLavaLine || _isResizing || IsDraggingToolbarObject;
        if (objectActionActive)
        {
            _hoveredPlatform = (_isCreating || _isDragging || _isResizing) ? _selectedPlatform : null;
            _hoveredGoal = _isDraggingGoal ? _selectedGoal : null;
            _hoveredCheckpoint = _isDraggingCheckpoint && _selectedCheckpoints.Count > 0 ? _selectedCheckpoints[0] : null;
            _hoveredLaunchPad = _isDraggingLaunchPad && _selectedLaunchPads.Count > 0 ? _selectedLaunchPads[0] : null;
            _lavaHovered = _isDraggingLavaLine;
            return;
        }

        _hoveredGoal = FindGoalAt(mouse);
        _hoveredCheckpoint = _hoveredGoal is null ? FindCheckpointAt(mouse) : null;
        _hoveredLaunchPad = _hoveredGoal is null && _hoveredCheckpoint is null ? FindLaunchPadAt(mouse) : null;
        _hoveredPlatform = _hoveredGoal is null && _hoveredCheckpoint is null && _hoveredLaunchPad is null ? FindPlatformAt(mouse) : null;
        _lavaHovered = _hoveredGoal is null && _hoveredCheckpoint is null && _hoveredLaunchPad is null && _hoveredPlatform is null
            && _level.Lava is not null && _level.Lava.HitTest(mouse);
    }

    private bool IsMouseOverToolbar()
    {
        return _toolbarPanelBounds.Contains(UiPointer);
    }

    private bool IsMouseOverUi()
    {
        return _backButton.Bounds.Contains(UiPointer)
            || _applyButton.Bounds.Contains(UiPointer)
            || IsMouseOverToolbar()
            || (_lavaSelected && _lavaSpeedPanelBounds.Contains(UiPointer));
    }

    private void LayoutLavaSpeedPanel()
    {
        const int panelWidth = 330;
        const int panelHeight = 78;
        const int margin = 20;
        int top = 70;
        _lavaSpeedPanelBounds = new Rectangle(margin, top, panelWidth, panelHeight);

        int button = 42;
        int buttonY = top + panelHeight - button - 12;
        _lavaSpeedMinusBounds = new Rectangle(margin + 12, buttonY, button, button);
        _lavaSpeedPlusBounds = new Rectangle(margin + panelWidth - button - 12, buttonY, button, button);
    }

    private void AdjustLavaRiseSpeed(float delta)
    {
        if (_level.Lava is null)
        {
            return;
        }

        _level.Lava.RiseSpeed = MathHelper.Clamp(
            _level.Lava.RiseSpeed + delta,
            LavaLine.MinRiseSpeed,
            LavaLine.MaxRiseSpeed);
        _isDirty = true;
    }

    private void DrawLavaSpeedPanel(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (!_lavaSelected || _level.Lava is null)
        {
            return;
        }

        LayoutLavaSpeedPanel();
        Rectangle panel = _lavaSpeedPanelBounds;
        spriteBatch.Draw(pixel, panel, new Color(28, 22, 16));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(255, 150, 40), 2);

        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            "LAVA RISE SPEED",
            new Vector2(panel.X + 14, panel.Y + 10),
            1,
            new Color(255, 210, 150));

        string value = $"{_level.Lava.RiseSpeed:0} px/s";
        SimpleTextRenderer.DrawCentered(
            spriteBatch,
            pixel,
            value,
            new Rectangle(panel.X, _lavaSpeedMinusBounds.Y, panel.Width, _lavaSpeedMinusBounds.Height),
            2,
            Color.White);

        DrawLavaSpeedButton(spriteBatch, pixel, _lavaSpeedMinusBounds, "-");
        DrawLavaSpeedButton(spriteBatch, pixel, _lavaSpeedPlusBounds, "+");
    }

    private void DrawLavaSpeedButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label)
    {
        bool hovered = bounds.Contains(UiPointer);
        spriteBatch.Draw(pixel, bounds, hovered ? new Color(72, 40, 22) : new Color(48, 28, 18));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, hovered ? new Color(255, 180, 70) : new Color(210, 120, 50), 2);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, 3, Color.White);
    }

    private static Rectangle BuildRectangle(Point start, Point end)
    {
        int left = Math.Min(start.X, end.X);
        int top = Math.Min(start.Y, end.Y);
        int width = Math.Abs(end.X - start.X);
        int height = Math.Abs(end.Y - start.Y);

        return new Rectangle(left, top, width, height);
    }

    private static Rectangle ResizeBounds(Rectangle original, ResizeHandle handle, Point delta)
    {
        int left = original.Left;
        int right = original.Right;
        int top = original.Top;
        int bottom = original.Bottom;

        if (HasLeft(handle))
        {
            left += delta.X;
        }

        if (HasRight(handle))
        {
            right += delta.X;
        }

        if (HasTop(handle))
        {
            top += delta.Y;
        }

        if (HasBottom(handle))
        {
            bottom += delta.Y;
        }

        int width = right - left;
        int height = bottom - top;

        // Enforce minimum size
        if (width < MinPlatformSize)
        {
            if (HasLeft(handle))
            {
                left = right - MinPlatformSize;
            }
            else
            {
                right = left + MinPlatformSize;
            }
        }

        if (height < MinPlatformSize)
        {
            if (HasTop(handle))
            {
                top = bottom - MinPlatformSize;
            }
            else
            {
                bottom = top + MinPlatformSize;
            }
        }

        // Snap to grid to prevent fractional sizes and positions
        Rectangle result = new(left, top, right - left, bottom - top);
        return SnapRectangleToGrid(result);
    }

    private ResizeHandle GetResizeHandle(Rectangle bounds, Point mouse)
    {
        int margin = GetWorldResizeMargin();
        Rectangle inflated = bounds;
        inflated.Inflate(margin, margin);
        if (!inflated.Contains(mouse))
        {
            return ResizeHandle.None;
        }

        bool nearLeft = Math.Abs(mouse.X - bounds.Left) <= margin;
        bool nearRight = Math.Abs(mouse.X - bounds.Right) <= margin;
        bool nearTop = Math.Abs(mouse.Y - bounds.Top) <= margin;
        bool nearBottom = Math.Abs(mouse.Y - bounds.Bottom) <= margin;

        if (nearLeft && nearTop)
        {
            return ResizeHandle.TopLeft;
        }

        if (nearRight && nearTop)
        {
            return ResizeHandle.TopRight;
        }

        if (nearLeft && nearBottom)
        {
            return ResizeHandle.BottomLeft;
        }

        if (nearRight && nearBottom)
        {
            return ResizeHandle.BottomRight;
        }

        if (nearLeft)
        {
            return ResizeHandle.Left;
        }

        if (nearRight)
        {
            return ResizeHandle.Right;
        }

        if (nearTop)
        {
            return ResizeHandle.Top;
        }

        if (nearBottom)
        {
            return ResizeHandle.Bottom;
        }

        return ResizeHandle.None;
    }

    private void DrawEditorBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle visibleWorldBounds)
    {
        spriteBatch.Draw(pixel, visibleWorldBounds, new Color(36, 41, 52));

        int groundTop = Math.Max(560, visibleWorldBounds.Top);
        int groundHeight = visibleWorldBounds.Bottom - groundTop;
        if (groundHeight > 0)
        {
            spriteBatch.Draw(pixel, new Rectangle(visibleWorldBounds.Left, groundTop, visibleWorldBounds.Width, groundHeight), new Color(24, 29, 36));
        }
    }

    private void DrawGrid(SpriteBatch spriteBatch, Texture2D pixel, Rectangle visibleWorldBounds)
    {
        // Fixed 1 world-pixel lines; fade alpha when zoomed out so dense grid doesn't wash white.
        float zoomFade = MathHelper.Clamp(_camera.Zoom, 0.15f, 1f);
        int baseAlpha = _snapToGrid ? 20 : 10;
        byte alpha = (byte)MathHelper.Clamp((int)(baseAlpha * zoomFade), 4, 24);
        Color gridColor = new Color((byte)255, (byte)255, (byte)255, alpha);
        const int lineThickness = 1;

        int startX = FloorToGrid(visibleWorldBounds.Left);
        int endX = visibleWorldBounds.Right + GridSize;
        for (int x = startX; x <= endX; x += GridSize)
        {
            spriteBatch.Draw(pixel, new Rectangle(x, visibleWorldBounds.Top, lineThickness, visibleWorldBounds.Height), gridColor);
        }

        int startY = FloorToGrid(visibleWorldBounds.Top);
        int endY = visibleWorldBounds.Bottom + GridSize;
        for (int y = startY; y <= endY; y += GridSize)
        {
            spriteBatch.Draw(pixel, new Rectangle(visibleWorldBounds.Left, y, visibleWorldBounds.Width, lineThickness), gridColor);
        }
    }

    private void DrawResizeHandles(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        DrawHandle(spriteBatch, pixel, new Point(bounds.Left, bounds.Top));
        DrawHandle(spriteBatch, pixel, new Point(bounds.Right, bounds.Top));
        DrawHandle(spriteBatch, pixel, new Point(bounds.Left, bounds.Bottom));
        DrawHandle(spriteBatch, pixel, new Point(bounds.Right, bounds.Bottom));
    }

    private void DrawHandle(SpriteBatch spriteBatch, Texture2D pixel, Point point)
    {
        int handleSize = GetWorldHandleSize();
        int halfSize = handleSize / 2;
        Rectangle handleBounds = new(point.X - halfSize, point.Y - halfSize, handleSize, handleSize);
        spriteBatch.Draw(pixel, handleBounds, new Color(255, 220, 80));
        DrawHelper.DrawBorder(spriteBatch, pixel, handleBounds, Color.Black, GetWorldLineThickness(1));
    }

    private void DrawGoalPreview(SpriteBatch spriteBatch, Texture2D pixel, Point position)
    {
        Goal preview = new(position);
        preview.Draw(spriteBatch, pixel, debugDraw: false, alpha: 0.55f);
        DrawHelper.DrawBorder(spriteBatch, pixel, preview.Bounds, new Color(255, 220, 80) * 0.8f, GetWorldLineThickness(2));
    }

    private void DrawToolbarObjectPreview(SpriteBatch spriteBatch, Texture2D pixel, EditorObjectKind kind, Point position)
    {
        switch (kind)
        {
            case EditorObjectKind.Goal:
                DrawGoalPreview(spriteBatch, pixel, position);
                break;
            case EditorObjectKind.CheckpointFlag:
                {
                    CheckpointFlag preview = new(position);
                    preview.Draw(spriteBatch, pixel, debugDraw: false, alpha: 0.55f);
                    DrawHelper.DrawBorder(spriteBatch, pixel, preview.Bounds, new Color(255, 220, 80) * 0.8f, GetWorldLineThickness(2));
                    break;
                }
            case EditorObjectKind.LaunchPad:
                {
                    Rectangle previewBounds = new(position.X, position.Y, LaunchPad.DefaultWidth, LaunchPad.DefaultHeight);
                    LaunchPad preview = new(previewBounds);
                    preview.Draw(spriteBatch, pixel, debugDraw: false, alpha: 0.55f);
                    DrawHelper.DrawBorder(spriteBatch, pixel, preview.Bounds, new Color(255, 220, 80) * 0.8f, GetWorldLineThickness(2));
                    break;
                }
        }
    }

    private void RotateSelectedLaunchPads(float deltaRotation)
    {
        foreach (LaunchPad launchPad in _selectedLaunchPads)
        {
            launchPad.RotationDegrees = LaunchPad.NormalizeRotation(launchPad.RotationDegrees + deltaRotation);
        }

        if (_selectedLaunchPads.Count > 0)
        {
            _isDirty = true;
        }
    }

    private void DrawEditorUi(SpriteBatch spriteBatch, Texture2D pixel)
    {
        Viewport viewport = _game.Viewport;
        int minDimension = Math.Min(viewport.Width, viewport.Height);
        int margin = Math.Max(8, (int)(minDimension * 0.025f));
        int panelWidth = Math.Min(
            Math.Clamp((int)(viewport.Width * 0.2f), 190, 250),
            Math.Max(1, viewport.Width - (margin * 2)));
        int panelHeight = Math.Min(
            Math.Clamp((int)(viewport.Height * 0.065f), 38, 52),
            Math.Max(1, viewport.Height - (margin * 2)));
        Rectangle panel = new(margin, margin, panelWidth, panelHeight);
        int colorBoxSize = Math.Max(18, panel.Height - Math.Max(12, panel.Height / 3));
        Rectangle colorBox = new(panel.Left + margin / 2, panel.Center.Y - (colorBoxSize / 2), colorBoxSize, colorBoxSize);
        string label = $"COLOR {_selectedColor}";
        int labelX = colorBox.Right + Math.Max(8, panel.Width / 18);
        int labelScale = FitTextScale(label, 2, panel.Right - labelX - Math.Max(4, panel.Width / 24));
        Point labelSize = SimpleTextRenderer.MeasureString(label, labelScale);

        spriteBatch.Draw(pixel, panel, new Color(22, 26, 34, 220));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(134, 145, 166), 2);
        spriteBatch.Draw(pixel, colorBox, _selectedColor.ToXnaColor());
        DrawHelper.DrawBorder(spriteBatch, pixel, colorBox, Color.Black, 2);

        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            label,
            new Vector2(labelX, panel.Center.Y - (labelSize.Y * 0.5f)),
            labelScale,
            Color.White);
    }

    private void DrawToolbar(SpriteBatch spriteBatch, Texture2D pixel)
    {
        Color panelFill = new(22, 26, 34, 226);
        Color panelBorder = new(134, 145, 166);

        spriteBatch.Draw(pixel, _toolbarPanelBounds, panelFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, _toolbarPanelBounds, panelBorder, 2);

        // Draw Goal slot
        DrawToolbarSlot(spriteBatch, pixel, _goalSlotBounds, _goalSlotHovered, _toolbarDragKind == EditorObjectKind.Goal, "GOAL",
            (sb, px, bounds) => Goal.DrawIcon(sb, px, bounds));

        // Draw Checkpoint slot
        DrawToolbarSlot(spriteBatch, pixel, _checkpointSlotBounds, _checkpointSlotHovered, _toolbarDragKind == EditorObjectKind.CheckpointFlag, "CHECK",
            (sb, px, bounds) => CheckpointFlag.DrawIcon(sb, px, bounds));

        // Draw Launch Pad slot
        DrawToolbarSlot(spriteBatch, pixel, _launchPadSlotBounds, _launchPadSlotHovered, _toolbarDragKind == EditorObjectKind.LaunchPad, "LAUNCH",
            (sb, px, bounds) => LaunchPad.DrawIcon(sb, px, bounds));
    }

    private void DrawToolbarSlot(SpriteBatch spriteBatch, Texture2D pixel, Rectangle slotBounds, bool isHovered, bool isDragging, string label, Action<SpriteBatch, Texture2D, Rectangle> drawIcon)
    {
        Color slotFill = isHovered || isDragging
            ? new Color(82, 94, 118)
            : new Color(48, 57, 74);
        Color slotBorder = isHovered || isDragging
            ? new Color(255, 220, 80)
            : new Color(134, 145, 166);

        spriteBatch.Draw(pixel, slotBounds, slotFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, slotBounds, slotBorder, 2);

        int inset = Math.Max(5, slotBounds.Width / 8);
        int labelScale = slotBounds.Width >= 64 ? 2 : 1;
        labelScale = FitTextScale(label, labelScale, slotBounds.Width - (inset * 2));
        Point labelSize = SimpleTextRenderer.MeasureString(label, labelScale);
        Rectangle iconBounds = new(
            slotBounds.Left + inset,
            slotBounds.Top + inset,
            slotBounds.Width - (inset * 2),
            Math.Max(1, slotBounds.Height - (inset * 2) - labelSize.Y - 3));

        drawIcon(spriteBatch, pixel, iconBounds);
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            label,
            new Vector2(slotBounds.Center.X - (labelSize.X * 0.5f), slotBounds.Bottom - inset - labelSize.Y),
            labelScale,
            Color.White);
    }

    private void LayoutEditorToolbar()
    {
        Viewport viewport = _game.Viewport;
        int minDimension = Math.Min(viewport.Width, viewport.Height);
        int slotSize = Math.Clamp((int)(minDimension * 0.09f), 44, 72);
        int padding = Math.Clamp(slotSize / 4, 10, 18);
        int slotGap = Math.Clamp(slotSize / 8, 4, 8);
        int bottomMargin = Math.Max(8, (int)(viewport.Height * 0.025f));
        int totalWidth = (padding * 2) + (slotSize * 3) + (slotGap * 2);
        int panelHeight = (padding * 2) + slotSize;

        _toolbarPanelBounds = new Rectangle(
            (viewport.Width - totalWidth) / 2,
            Math.Max(0, viewport.Height - bottomMargin - panelHeight),
            totalWidth,
            panelHeight);

        _goalSlotBounds = new Rectangle(
            _toolbarPanelBounds.Left + padding,
            _toolbarPanelBounds.Top + padding,
            slotSize,
            slotSize);

        _checkpointSlotBounds = new Rectangle(
            _goalSlotBounds.Right + slotGap,
            _toolbarPanelBounds.Top + padding,
            slotSize,
            slotSize);

        _launchPadSlotBounds = new Rectangle(
            _checkpointSlotBounds.Right + slotGap,
            _toolbarPanelBounds.Top + padding,
            slotSize,
            slotSize);

        _goalSlotHovered = _goalSlotBounds.Contains(UiPointer);
        _checkpointSlotHovered = _checkpointSlotBounds.Contains(UiPointer);
        _launchPadSlotHovered = _launchPadSlotBounds.Contains(UiPointer);

        // Update hovered toolbar kind
        if (_goalSlotHovered)
        {
            _hoveredToolbarKind = EditorObjectKind.Goal;
        }
        else if (_checkpointSlotHovered)
        {
            _hoveredToolbarKind = EditorObjectKind.CheckpointFlag;
        }
        else if (_launchPadSlotHovered)
        {
            _hoveredToolbarKind = EditorObjectKind.LaunchPad;
        }
        else
        {
            _hoveredToolbarKind = EditorObjectKind.None;
        }
    }

    private void LayoutBackButton()
    {
        Viewport viewport = _game.Viewport;
        int minDimension = Math.Min(viewport.Width, viewport.Height);
        int margin = Math.Max(8, (int)(minDimension * 0.022f));
        int height = Math.Clamp((int)(viewport.Height * 0.058f), 36, 44);
        int gap = 10;
        int backWidth = Math.Min(180, Math.Max(1, viewport.Width - (margin * 2)));
        int applyWidth = Math.Min(120, Math.Max(90, backWidth - 40));

        _backButton.Bounds = new Rectangle(viewport.Width - backWidth - margin, margin, backWidth, height);
        _applyButton.Bounds = new Rectangle(_backButton.Bounds.X - gap - applyWidth, margin, applyWidth, height);
    }

    private void DrawApplyButton(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (_isDirty)
        {
            _applyButton.FillColor = new Color(52, 61, 80);
            _applyButton.HoverFillColor = new Color(74, 86, 110);
            _applyButton.BorderColor = new Color(255, 220, 80);
            _applyButton.HoverBorderColor = new Color(255, 235, 140);
            _applyButton.TextColor = Color.White;
            _applyButton.HoverTextColor = Color.White;
        }
        else
        {
            _applyButton.FillColor = new Color(34, 38, 48);
            _applyButton.HoverFillColor = new Color(34, 38, 48);
            _applyButton.BorderColor = new Color(70, 78, 92);
            _applyButton.HoverBorderColor = new Color(70, 78, 92);
            _applyButton.TextColor = new Color(110, 118, 132);
            _applyButton.HoverTextColor = new Color(110, 118, 132);
        }

        _applyButton.Draw(spriteBatch, pixel);
    }

    private void ApplyChanges()
    {
        SaveLevel(force: true);
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

    private void SaveLevel(bool force = false)
    {
        if (!force && !_isDirty)
        {
            return;
        }

        LevelManager.SaveLevel(_level, _levelId);
        LevelPreviewManager.GenerateAndSavePreview(_game.GraphicsDevice, _game.Pixel, _level, _levelId);
        _isDirty = false;
    }

    private int GetWorldResizeMargin()
    {
        return Math.Max(2, (int)MathF.Ceiling(ResizeMargin / _camera.Zoom));
    }

    private int GetWorldHandleSize()
    {
        return Math.Max(2, (int)MathF.Ceiling(8f / _camera.Zoom));
    }

    private int GetWorldLineThickness(int screenPixels)
    {
        return Math.Max(1, (int)MathF.Ceiling(screenPixels / _camera.Zoom));
    }

    private static Point GetDelta(Point current, Point start)
    {
        return new Point(current.X - start.X, current.Y - start.Y);
    }

    private static int FloorToGrid(int value)
    {
        return (int)MathF.Floor(value / (float)GridSize) * GridSize;
    }

    private static bool HasLeft(ResizeHandle handle)
    {
        return handle is ResizeHandle.Left or ResizeHandle.TopLeft or ResizeHandle.BottomLeft;
    }

    private static bool HasRight(ResizeHandle handle)
    {
        return handle is ResizeHandle.Right or ResizeHandle.TopRight or ResizeHandle.BottomRight;
    }

    private static bool HasTop(ResizeHandle handle)
    {
        return handle is ResizeHandle.Top or ResizeHandle.TopLeft or ResizeHandle.TopRight;
    }

    private static bool HasBottom(ResizeHandle handle)
    {
        return handle is ResizeHandle.Bottom or ResizeHandle.BottomLeft or ResizeHandle.BottomRight;
    }

    private enum ResizeHandle
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
