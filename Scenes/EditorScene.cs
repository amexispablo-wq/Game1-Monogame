using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class EditorScene : IScene
{
    private const int GridSize = 32;
    private const int ResizeMargin = 8;
    private const int MinPlatformSize = GridSize; // Must be at least one grid cell
    private const int MarqueeMinDragSize = 4;
    private const int MaxUndoDepth = 10;
    private static readonly JsonSerializerOptions UndoJsonOptions = new();

    private readonly ColorBlocksGame _game;
    private readonly string _levelId;
    private readonly Level _level;
    private readonly Camera _camera;
    private readonly Button _backButton = new("Back to Menu") { TextScale = 2 };
    private readonly Button _applyButton = new("Apply") { TextScale = 2 };
    private readonly Button _testButton = new("Test") { TextScale = 2 };

    private Platform _selectedPlatform;
    private Platform _hoveredPlatform;
    private Goal _selectedGoal;
    private Goal _hoveredGoal;
    private readonly List<Goal> _selectedGoals = new();
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
    private readonly Dictionary<Goal, Point> _goalDragStartPositions = new();
    private readonly List<EditorClipboardItem> _clipboard = new();
    private readonly List<LevelData> _undoStack = new();
    private readonly List<LevelData> _redoStack = new();
    private LevelData? _historyGestureBefore;
    private Point _clipboardOrigin;
    private int _pasteCount;
    private Point _dragStartMouse;
    private bool _isCreating;
    private bool _isMarqueeSelecting;
    private Point _marqueeStart;
    private Rectangle _marqueeBounds;
    private bool _isDragging;
    private bool _isDraggingGoal;
    private bool _isDraggingCheckpoint;
    private bool _isDraggingLaunchPad;
    private bool _isResizing;
    private bool _isPanningCamera;
    private Point _createStart;
    private Point _goalDragStartMouse;
    private Point _checkpointDragStartMouse;
    private Point _launchPadDragStartMouse;
    private Point _goalPreviewPosition;
    private Point _objectPreviewPosition;
    private Rectangle _previewBounds;
    private Rectangle _toolbarPanelBounds;
    private Rectangle _goalSlotBounds;
    private Rectangle _checkpointSlotBounds;
    private Rectangle _launchPadSlotBounds;
    private Rectangle _playerSpawnSlotBounds;
    private EditorObjectKind _toolbarDragKind = EditorObjectKind.None;
    private EditorObjectKind _hoveredToolbarKind = EditorObjectKind.None;
    private bool _snapToGrid = true;
    private GameColor _selectedColor = GameColor.Red;
    private bool _goalSlotHovered;
    private bool _checkpointSlotHovered;
    private bool _launchPadSlotHovered;
    private bool _playerSpawnSlotHovered;
    private bool _isDraggingPlayerSpawn;
    private bool _playerSpawnSelected;
    private bool _playerSpawnHovered;
    private Point _playerSpawnDragStartMouse;
    private Vector2 _playerSpawnDragStartPosition;
    private bool _isDirty;
    private bool _isDraggingLavaLine;
    private bool _lavaSelected;
    private bool _lavaHovered;
    private Point _lavaDragStartMouse;
    private int _lavaDragStartY;
    private Rectangle _lavaSpeedPanelBounds;
    private Rectangle _lavaSpeedMinusBounds;
    private Rectangle _lavaSpeedPlusBounds;
    private Rectangle _colorPanelBounds;
    private Rectangle _colorRedBounds;
    private Rectangle _colorGreenBounds;
    private Rectangle _colorBlueBounds;
    private Rectangle _colorWhiteBounds;

    private readonly VirtualCursor _virtualCursor = new();
    private readonly UIFocusManager _uiFocus = new();
    private readonly FocusableButton _backFocus;
    private readonly FocusableButton _applyFocus;
    private readonly FocusableButton _testFocus;
    private bool _gamepadPrimaryWasHeld;

    private bool IsGamepadCursorMode() =>
        _virtualCursor.IsActive && _game.Input.Navigation.IsGamepadActive;

    private bool IsPrimaryPressed() =>
        _game.Input.UiPointerPressed
        || (IsGamepadCursorMode() && _game.Input.GamepadMenuConfirmPressed);

    private bool IsPrimaryHeld() =>
        _game.Input.UiPointerHeld
        || (IsGamepadCursorMode() && _game.Input.MenuConfirmHeld);

    private bool IsPrimaryReleased() =>
        _game.Input.UiPointerReleased
        || (IsGamepadCursorMode() && _gamepadPrimaryWasHeld && !_game.Input.MenuConfirmHeld);

    private Point UiPointer => _game.Input.UiPointerPosition;

    private bool IsDraggingToolbarObject => _toolbarDragKind != EditorObjectKind.None;
    private bool HasSelection => _selectedPlatforms.Count > 0
        || _selectedGoals.Count > 0
        || _selectedCheckpoints.Count > 0
        || _selectedLaunchPads.Count > 0;

    public EditorScene(ColorBlocksGame game, string levelId = "level_1")
    {
        _game = game;
        _levelId = levelId;
        _level = LevelLibrary.LoadLevel(levelId);
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
        _testFocus = new FocusableButton(_testButton);
        _uiFocus.ResetFocus();
    }

    public void Update(GameTime gameTime)
    {
        UpdateGamepadUi(gameTime);
        LayoutColorPanel();
        LayoutBackButton();
        LayoutEditorToolbar();
        LayoutLavaSpeedPanel();

        if (TryHandleGamepadChromePress())
        {
            _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
            return;
        }

        if (!IsGamepadCursorMode())
        {
            UpdateUiFocus(gameTime);
        }

        if (_game.Input.KeyboardMenuCancelPressed || _game.Input.GamepadBackPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        if (!IsGamepadCursorMode() && _backFocus.WasActivated)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        if (!IsGamepadCursorMode() && _applyFocus.WasActivated && _isDirty)
        {
            ApplyChanges();
            return;
        }

        if (!IsGamepadCursorMode() && _testFocus.WasActivated)
        {
            StartEditorTest();
            return;
        }

        if (_game.Input.ExitPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        HandleKeyboard();

        if (TryApplyColorFromPointer())
        {
            _gamepadPrimaryWasHeld = _virtualCursor.IsActive && _game.Input.MenuConfirmHeld;
            return;
        }

        bool mouseOverToolbar = IsMouseOverToolbar();
        bool cameraBlockedByUi = _colorPanelBounds.Contains(UiPointer)
            || _backButton.IsHovered
            || _applyButton.IsHovered
            || _testButton.IsHovered
            || (mouseOverToolbar && !IsDraggingToolbarObject);
        HandleCameraInput(cameraBlockedByUi, gameTime);

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
            || _isMarqueeSelecting
            || _isDragging
            || _isResizing
            || _isDraggingGoal
            || _isDraggingCheckpoint
            || _isDraggingLaunchPad
            || _isDraggingPlayerSpawn
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
    }

    private bool TryHandleGamepadChromePress()
    {
        if (!IsGamepadCursorMode() || !_game.Input.GamepadMenuConfirmPressed)
        {
            return false;
        }

        if (_lavaSelected && _level.Lava is not null)
        {
            if (_lavaSpeedMinusBounds.Contains(UiPointer))
            {
                AdjustLavaRiseSpeed(-LavaLine.RiseSpeedStep);
                return true;
            }

            if (_lavaSpeedPlusBounds.Contains(UiPointer))
            {
                AdjustLavaRiseSpeed(LavaLine.RiseSpeedStep);
                return true;
            }
        }

        if (TryApplyColorFromPointer())
        {
            return true;
        }

        if (_applyButton.Bounds.Contains(UiPointer) && _isDirty)
        {
            ApplyChanges();
            return true;
        }

        if (_testButton.Bounds.Contains(UiPointer))
        {
            StartEditorTest();
            return true;
        }

        if (_backButton.Bounds.Contains(UiPointer))
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return true;
        }

        return false;
    }

    private void UpdateUiFocus(GameTime gameTime)
    {
        if (IsGamepadCursorMode())
        {
            return;
        }

        _uiFocus.Clear();
        int backIndex = _uiFocus.Add(_backFocus, "Back");
        int applyIndex = _uiFocus.Add(_applyFocus, "Apply");
        int testIndex = _uiFocus.Add(_testFocus, "Test");

        NavigationGraph nav = _uiFocus.Navigation;

        if (_lavaSelected)
        {
            int minusIndex = _uiFocus.Add(new FocusableAction(_lavaSpeedMinusBounds, () =>
            {
                AdjustLavaRiseSpeed(-LavaLine.RiseSpeedStep);
                return true;
            }), "LavaSpeedMinus");
            int plusIndex = _uiFocus.Add(new FocusableAction(_lavaSpeedPlusBounds, () =>
            {
                AdjustLavaRiseSpeed(LavaLine.RiseSpeedStep);
                return true;
            }), "LavaSpeedPlus");

            nav.LinkHorizontal(backIndex, minusIndex);
            nav.LinkHorizontal(minusIndex, plusIndex);
            nav.LinkHorizontal(plusIndex, applyIndex);
            nav.LinkHorizontal(applyIndex, testIndex);
        }
        else
        {
            nav.LinkHorizontal(backIndex, applyIndex);
            nav.LinkHorizontal(applyIndex, testIndex);
        }

        _uiFocus.FinalizeFocus("Back");
        _uiFocus.Update(gameTime, _game.Input);
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutColorPanel();
        LayoutBackButton();
        LayoutEditorToolbar();
        LayoutLavaSpeedPanel();

        Texture2D pixel = _game.Pixel;
        Viewport viewport = _game.Viewport;
        Rectangle visibleWorldBounds = _camera.GetVisibleWorldRectangle(viewport, GridSize * 2);

        spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _camera.GetTransform(viewport));

        DrawEditorBackground(spriteBatch, pixel, visibleWorldBounds);
        spriteBatch.End();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawGrid(spriteBatch, pixel, viewport, visibleWorldBounds);
        spriteBatch.End();

        spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _camera.GetTransform(viewport));

        _level.DrawPlatforms(spriteBatch, pixel, debugDraw: false);
        _level.DrawLaunchPads(spriteBatch, pixel, debugDraw: false, isEditorMode: true);
        _level.DrawGoals(spriteBatch, pixel, debugDraw: false);
        _level.DrawCheckpointFlags(spriteBatch, pixel, debugDraw: false);
        DrawPlayerSpawnMarker(spriteBatch, pixel, _level.PlayerStart, _level.PlayerStartColor, 1f);
        if (_playerSpawnSelected || _playerSpawnHovered)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, GetPlayerSpawnBounds(), new Color(255, 220, 80), GetWorldLineThickness(3));
        }

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

        if (_hoveredGoal is not null && !_selectedGoals.Contains(_hoveredGoal))
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

        foreach (Goal selectedGoal in _selectedGoals)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, selectedGoal.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
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

        if (_isMarqueeSelecting && _marqueeBounds.Width > 0 && _marqueeBounds.Height > 0)
        {
            spriteBatch.Draw(pixel, _marqueeBounds, new Color(120, 180, 255) * 0.14f);
            DrawHelper.DrawBorder(spriteBatch, pixel, _marqueeBounds, new Color(120, 180, 255), GetWorldLineThickness(1));
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
        UpdateEditorChromeButtonHover();
        DrawApplyButton(spriteBatch, pixel);
        _testButton.Draw(spriteBatch, pixel);
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
        _virtualCursor.Reset();
        _game.Input.SetUiPointerOverride(null);
        _game.Input.Navigation.PreferMouse();
    }

    private void HandleKeyboard()
    {
        if (_game.Input.ControlHeld)
        {
            if (_game.Input.IsNewKeyPress(Keys.Z))
            {
                TryUndo();
                return;
            }

            if (_game.Input.IsNewKeyPress(Keys.Y))
            {
                TryRedo();
                return;
            }

            if (_game.Input.IsNewKeyPress(Keys.C))
            {
                CopySelectedObjects();
                return;
            }

            if (_game.Input.IsNewKeyPress(Keys.V))
            {
                BeginHistoryGesture();
                PasteClipboard();
                EndHistoryGesture();
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
            BeginHistoryGesture();
            RotateSelectedLaunchPads(-15f);
            EndHistoryGesture();
        }

        if (_game.Input.IsNewKeyPress(Keys.E))
        {
            BeginHistoryGesture();
            RotateSelectedLaunchPads(15f);
            EndHistoryGesture();
        }

        if (_game.Input.IsNewKeyPress(Keys.Delete) && HasSelection)
        {
            BeginHistoryGesture();
            DeleteSelectedObjects();
            EndHistoryGesture();
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
                AdjustLavaRiseSpeed(-LavaLine.RiseSpeedStep);
            }

            if (_game.Input.IsNewKeyPress(Keys.OemPeriod) || _game.Input.IsNewKeyPress(Keys.OemPlus))
            {
                AdjustLavaRiseSpeed(LavaLine.RiseSpeedStep);
            }
        }

        if (!_game.Input.ControlHeld && _game.Input.TryGetEditorColorRequest(out GameColor requestedColor))
        {
            SetSelectedColor(requestedColor);
        }
    }

    private void SetSelectedColor(GameColor color)
    {
        _selectedColor = color;
        BeginHistoryGesture();
        ApplyColorToSelection(color);
        EndHistoryGesture();
    }

    private bool TryApplyColorFromPointer()
    {
        if (!IsPrimaryPressed())
        {
            return false;
        }

        if (_colorRedBounds.Contains(UiPointer))
        {
            SetSelectedColor(GameColor.Red);
            return true;
        }

        if (_colorGreenBounds.Contains(UiPointer))
        {
            SetSelectedColor(GameColor.Green);
            return true;
        }

        if (_colorBlueBounds.Contains(UiPointer))
        {
            SetSelectedColor(GameColor.Blue);
            return true;
        }

        if (_colorWhiteBounds.Contains(UiPointer))
        {
            SetSelectedColor(GameColor.White);
            return true;
        }

        return false;
    }

    private void LayoutColorPanel()
    {
        Viewport viewport = _game.Viewport;
        int minDimension = Math.Min(viewport.Width, viewport.Height);
        int margin = Math.Max(8, (int)(minDimension * 0.025f));
        int panelHeight = Math.Min(
            Math.Clamp((int)(viewport.Height * 0.065f), 38, 52),
            Math.Max(1, viewport.Height - (margin * 2)));
        int swatchGap = 6;
        int swatchPad = Math.Max(8, margin / 2);
        int swatchSize = Math.Max(18, panelHeight - Math.Max(12, panelHeight / 3));
        int panelWidth = (swatchPad * 2) + (swatchSize * 4) + (swatchGap * 3);
        _colorPanelBounds = new Rectangle(margin, margin, panelWidth, panelHeight);

        int swatchY = _colorPanelBounds.Center.Y - (swatchSize / 2);
        int swatchX = _colorPanelBounds.Left + swatchPad;
        _colorRedBounds = new Rectangle(swatchX, swatchY, swatchSize, swatchSize);
        _colorGreenBounds = new Rectangle(_colorRedBounds.Right + swatchGap, swatchY, swatchSize, swatchSize);
        _colorBlueBounds = new Rectangle(_colorGreenBounds.Right + swatchGap, swatchY, swatchSize, swatchSize);
        _colorWhiteBounds = new Rectangle(_colorBlueBounds.Right + swatchGap, swatchY, swatchSize, swatchSize);
    }

    private void HandleCameraInput(bool mouseOverUi, GameTime gameTime)
    {
        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.05f);
        bool canStartPanning = !_isCreating
            && !_isMarqueeSelecting
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

        if (!mouseOverUi && _game.Input.Navigation.IsGamepadActive && _game.Input.IsAnyGamepadConnected())
        {
            Vector2 stick = _game.Input.GetEditorLeftStick();
            if (stick.LengthSquared() > GamepadDefaults.EditorPanStickThreshold * GamepadDefaults.EditorPanStickThreshold)
            {
                Vector2 screenPan = new Vector2(-stick.X, stick.Y)
                    * (GamepadDefaults.EditorPanSpeedPixelsPerSecond * dt);
                _camera.PanByScreenDelta(screenPan);
            }

            if (_game.Input.EditorLeftTrigger > 0.1f)
            {
                float zoomFactor = MathF.Exp(
                    GamepadDefaults.EditorZoomRatePerSecond * _game.Input.EditorLeftTrigger * dt);
                _camera.ZoomAt(zoomFactor, UiPointer, _game.Viewport);
            }

            if (_game.Input.EditorRightTrigger > 0.1f)
            {
                float zoomFactor = MathF.Exp(
                    -GamepadDefaults.EditorZoomRatePerSecond * _game.Input.EditorRightTrigger * dt);
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
        bool clickedPlayerSpawn = HitTestPlayerSpawn(mouse);

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
            else if (!clickedPlayerSpawn && (_level.Lava is null || !_level.Lava.HitTest(mouse)))
            {
                StartMarqueeSelection(mouse);
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

        if (clickedPlayerSpawn)
        {
            ClearSelection();
            _playerSpawnSelected = true;
            _selectedColor = Level.NormalizePlayerStartColor(_level.PlayerStartColor);
            StartPlayerSpawnDrag(mouse);
            return;
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

            StartSelectionDrag(mouse);
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

            StartSelectionDrag(mouse);
            return;
        }

        if (clickedGoal is not null)
        {
            if (!_selectedGoals.Contains(clickedGoal))
            {
                SelectSingleGoal(clickedGoal);
            }
            else
            {
                _selectedGoal = clickedGoal;
            }

            StartSelectionDrag(mouse);
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

            StartSelectionDrag(mouse);
            return;
        }

        if (_level.Lava is not null && _level.Lava.HitTest(mouse))
        {
            StartLavaDrag(mouse);
            return;
        }

        ClearSelection();
        BeginHistoryGesture();
        _isCreating = true;
        _isDragging = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
        _createStart = Snap(mouse);
        _previewBounds = new Rectangle(_createStart.X, _createStart.Y, 0, 0);
    }

    private void ContinueMouseAction(Point mouse)
    {
        if (_isMarqueeSelecting)
        {
            _marqueeBounds = BuildRectangle(_marqueeStart, mouse);
            return;
        }

        if (_isCreating)
        {
            _previewBounds = BuildRectangle(_createStart, Snap(mouse));
            return;
        }

        if (_isDragging && _selectedPlatforms.Count > 0)
        {
            MoveSelectedPlatforms(mouse);
        }

        if (_isDraggingGoal && _selectedGoals.Count > 0)
        {
            MoveSelectedGoals(mouse);
        }

        if (_isDraggingCheckpoint && _selectedCheckpoints.Count > 0)
        {
            MoveSelectedCheckpoints(mouse);
        }

        if (_isDraggingLaunchPad && _selectedLaunchPads.Count > 0)
        {
            MoveSelectedLaunchPads(mouse);
        }

        if (_isDraggingPlayerSpawn)
        {
            MovePlayerSpawn(mouse);
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
        if (_isMarqueeSelecting)
        {
            ApplyMarqueeSelection();
            _isMarqueeSelecting = false;
            _marqueeBounds = Rectangle.Empty;
            return;
        }

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
        _isDraggingPlayerSpawn = false;
        _isDraggingLavaLine = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
        EndHistoryGesture();
    }

    private void StartMarqueeSelection(Point mouse)
    {
        _isMarqueeSelecting = true;
        _isCreating = false;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isDraggingPlayerSpawn = false;
        _isDraggingLavaLine = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
        _marqueeStart = mouse;
        _marqueeBounds = Rectangle.Empty;
    }

    private void ApplyMarqueeSelection()
    {
        if (_marqueeBounds.Width < MarqueeMinDragSize && _marqueeBounds.Height < MarqueeMinDragSize)
        {
            ClearSelection();
            return;
        }

        ClearSelection();
        foreach (Platform platform in _level.Platforms)
        {
            if (_marqueeBounds.Intersects(platform.Bounds))
            {
                _selectedPlatforms.Add(platform);
            }
        }

        foreach (CheckpointFlag checkpoint in _level.CheckpointFlags)
        {
            if (_marqueeBounds.Intersects(checkpoint.Bounds))
            {
                _selectedCheckpoints.Add(checkpoint);
            }
        }

        foreach (LaunchPad launchPad in _level.LaunchPads)
        {
            if (_marqueeBounds.Intersects(launchPad.Bounds))
            {
                _selectedLaunchPads.Add(launchPad);
            }
        }

        foreach (Goal goal in _level.Goals)
        {
            if (_marqueeBounds.Intersects(goal.Bounds))
            {
                _selectedGoals.Add(goal);
            }
        }

        _selectedPlatform = _selectedPlatforms.Count > 0 ? _selectedPlatforms[^1] : null;
        _selectedCheckpoint = _selectedCheckpoints.Count > 0 ? _selectedCheckpoints[^1] : null;
        _selectedLaunchPad = _selectedLaunchPads.Count > 0 ? _selectedLaunchPads[^1] : null;
        _selectedGoal = _selectedGoals.Count > 0 ? _selectedGoals[^1] : null;
    }

    private void StartLavaDrag(Point mouse)
    {
        ClearSelection();
        BeginHistoryGesture();
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

    private void StartSelectionDrag(Point mouse)
    {
        BeginHistoryGesture();
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;

        _isDragging = _selectedPlatforms.Count > 0;
        _isDraggingGoal = _selectedGoals.Count > 0;
        _isDraggingCheckpoint = _selectedCheckpoints.Count > 0;
        _isDraggingLaunchPad = _selectedLaunchPads.Count > 0;

        _dragStartMouse = mouse;
        _goalDragStartMouse = mouse;
        _checkpointDragStartMouse = mouse;
        _launchPadDragStartMouse = mouse;

        _dragStartBounds.Clear();
        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            _dragStartBounds[selectedPlatform] = selectedPlatform.Bounds;
        }

        _goalDragStartPositions.Clear();
        foreach (Goal selectedGoal in _selectedGoals)
        {
            _goalDragStartPositions[selectedGoal] = selectedGoal.Position;
        }

        _checkpointDragStartPositions.Clear();
        foreach (CheckpointFlag selectedCheckpoint in _selectedCheckpoints)
        {
            _checkpointDragStartPositions[selectedCheckpoint] = selectedCheckpoint.Position;
        }

        _launchPadDragStartBounds.Clear();
        foreach (LaunchPad selectedLaunchPad in _selectedLaunchPads)
        {
            _launchPadDragStartBounds[selectedLaunchPad] = selectedLaunchPad.Bounds;
        }
    }

    private void StartResize(Platform platform, ResizeHandle handle, Point mouse)
    {
        SelectSinglePlatform(platform);
        BeginHistoryGesture();
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
        BeginHistoryGesture();
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
        _selectedGoals.Clear();
        _selectedGoals.Add(goal);
        _selectedGoal = goal;
    }

    private void SelectSingleCheckpoint(CheckpointFlag checkpoint)
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoals.Clear();
        _selectedGoal = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoints.Add(checkpoint);
        _selectedCheckpoint = checkpoint;
    }

    private void SelectSingleLaunchPad(LaunchPad launchPad)
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoals.Clear();
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPads.Add(launchPad);
        _selectedLaunchPad = launchPad;
    }

    private void MoveSelectedGoals(Point mouse)
    {
        Point delta = GetDelta(mouse, _goalDragStartMouse);
        if (_snapToGrid)
        {
            delta = SnapDelta(delta);
        }

        bool movedAnyGoal = false;
        foreach (Goal selectedGoal in _selectedGoals)
        {
            if (!_goalDragStartPositions.TryGetValue(selectedGoal, out Point startPosition))
            {
                continue;
            }

            Point nextPosition = new(startPosition.X + delta.X, startPosition.Y + delta.Y);
            if (_snapToGrid)
            {
                nextPosition = Snap(nextPosition);
            }

            if (selectedGoal.Position == nextPosition)
            {
                continue;
            }

            selectedGoal.Position = nextPosition;
            movedAnyGoal = true;
        }

        if (movedAnyGoal)
        {
            _level.RecalculateWorldSize();
            _isDirty = true;
        }
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
        BeginHistoryGesture();
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
                case EditorObjectKind.PlayerSpawn:
                    {
                        _level.PlayerStart = new Vector2(_objectPreviewPosition.X, _objectPreviewPosition.Y);
                        _playerSpawnSelected = true;
                        _level.RecalculateWorldSize();
                        _isDirty = true;
                        break;
                    }
            }
        }

        _toolbarDragKind = EditorObjectKind.None;
        EndHistoryGesture();
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
        _selectedGoals.Clear();
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
    }

    private void ToggleSelection(Platform platform)
    {
        _selectedGoals.Clear();
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
        _selectedGoals.Clear();
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
        _selectedGoals.Clear();
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
        _selectedGoals.Clear();
        _selectedGoal = null;
        _selectedCheckpoints.Clear();
        _selectedCheckpoint = null;
        _selectedLaunchPads.Clear();
        _selectedLaunchPad = null;
        _lavaSelected = false;
        _playerSpawnSelected = false;
    }

    private Rectangle GetPlayerSpawnBounds()
    {
        return new Rectangle((int)_level.PlayerStart.X, (int)_level.PlayerStart.Y, 40, 40);
    }

    private bool HitTestPlayerSpawn(Point mouse) => GetPlayerSpawnBounds().Contains(mouse);

    private void StartPlayerSpawnDrag(Point mouse)
    {
        BeginHistoryGesture();
        _isDraggingPlayerSpawn = true;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _playerSpawnDragStartMouse = mouse;
        _playerSpawnDragStartPosition = _level.PlayerStart;
    }

    private void MovePlayerSpawn(Point mouse)
    {
        Point delta = GetDelta(mouse, _playerSpawnDragStartMouse);
        Vector2 next = _playerSpawnDragStartPosition + delta.ToVector2();
        if (_snapToGrid)
        {
            next = new Vector2(SnapToGrid((int)next.X), SnapToGrid((int)next.Y));
        }

        if (_level.PlayerStart == next)
        {
            return;
        }

        _level.PlayerStart = next;
        _level.RecalculateWorldSize();
        _isDirty = true;
    }

    private static void DrawPlayerSpawnMarker(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, GameColor color, float alpha)
    {
        Rectangle bounds = new((int)position.X, (int)position.Y, 40, 40);
        spriteBatch.Draw(pixel, bounds, color.ToXnaColor() * alpha);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 2);
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

        for (int i = _selectedGoals.Count - 1; i >= 0; i--)
        {
            _level.RemoveGoal(_selectedGoals[i]);
        }

        _hoveredGoal = null;

        _hoveredCheckpoint = null;
        _hoveredLaunchPad = null;
        ClearSelection();
        _isDirty = true;
    }

    private void ApplyColorToSelection(GameColor color)
    {
        bool changedAny = false;
        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            if (selectedPlatform.PlatformColor == color)
            {
                continue;
            }

            selectedPlatform.PlatformColor = color;
            changedAny = true;
        }

        if (_playerSpawnSelected && color != GameColor.White)
        {
            GameColor spawnColor = Level.NormalizePlayerStartColor(color);
            if (_level.PlayerStartColor != spawnColor)
            {
                _level.PlayerStartColor = spawnColor;
                changedAny = true;
            }
        }

        if (changedAny)
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

        foreach (Goal selectedGoal in _selectedGoals)
        {
            Rectangle bounds = selectedGoal.Bounds;
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
                        _selectedGoals.Add(goal);
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

        if (_selectedGoals.Count > 0)
        {
            origin = _selectedGoals[0].Bounds.Location;
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
        bool objectActionActive = _isCreating || _isMarqueeSelecting || _isDragging || _isDraggingGoal || _isDraggingCheckpoint || _isDraggingLaunchPad || _isDraggingPlayerSpawn || _isDraggingLavaLine || _isResizing || IsDraggingToolbarObject;
        if (objectActionActive)
        {
            _hoveredPlatform = (_isCreating || _isDragging || _isResizing) ? _selectedPlatform : null;
            _hoveredGoal = _isDraggingGoal ? _selectedGoal : null;
            _hoveredCheckpoint = _isDraggingCheckpoint && _selectedCheckpoints.Count > 0 ? _selectedCheckpoints[0] : null;
            _hoveredLaunchPad = _isDraggingLaunchPad && _selectedLaunchPads.Count > 0 ? _selectedLaunchPads[0] : null;
            _playerSpawnHovered = _isDraggingPlayerSpawn;
            _lavaHovered = _isDraggingLavaLine;
            return;
        }

        _hoveredGoal = FindGoalAt(mouse);
        _hoveredCheckpoint = _hoveredGoal is null ? FindCheckpointAt(mouse) : null;
        _hoveredLaunchPad = _hoveredGoal is null && _hoveredCheckpoint is null ? FindLaunchPadAt(mouse) : null;
        _hoveredPlatform = _hoveredGoal is null && _hoveredCheckpoint is null && _hoveredLaunchPad is null ? FindPlatformAt(mouse) : null;
        _playerSpawnHovered = _hoveredGoal is null && _hoveredCheckpoint is null && _hoveredLaunchPad is null && _hoveredPlatform is null
            && HitTestPlayerSpawn(mouse);
        _lavaHovered = _hoveredGoal is null && _hoveredCheckpoint is null && _hoveredLaunchPad is null && _hoveredPlatform is null && !_playerSpawnHovered
            && _level.Lava is not null && _level.Lava.HitTest(mouse);
    }

    private bool IsMouseOverToolbar()
    {
        return _toolbarPanelBounds.Contains(UiPointer);
    }

    private bool IsPointerOverEditorChrome()
    {
        return _backButton.Bounds.Contains(UiPointer)
            || _applyButton.Bounds.Contains(UiPointer)
            || _testButton.Bounds.Contains(UiPointer)
            || (_lavaSelected && _lavaSpeedPanelBounds.Contains(UiPointer));
    }

    private bool IsMouseOverUi()
    {
        return _colorPanelBounds.Contains(UiPointer)
            || _backButton.Bounds.Contains(UiPointer)
            || _applyButton.Bounds.Contains(UiPointer)
            || _testButton.Bounds.Contains(UiPointer)
            || IsMouseOverToolbar()
            || (_lavaSelected && _lavaSpeedPanelBounds.Contains(UiPointer));
    }

    private void LayoutLavaSpeedPanel()
    {
        const int panelWidth = 330;
        const int panelHeight = 78;
        const int gap = 10;
        int margin = _colorPanelBounds.Left;
        int top = _backButton.Bounds.Bottom + gap;
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

        BeginHistoryGesture();
        _level.Lava.RiseSpeed = MathHelper.Clamp(
            _level.Lava.RiseSpeed + delta,
            LavaLine.MinRiseSpeed,
            LavaLine.MaxRiseSpeed);
        _isDirty = true;
        EndHistoryGesture();
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

    private void DrawGrid(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, Rectangle visibleWorldBounds)
    {
        // Fixed 32px world cells. Zoom out → coarser visual grid (3x then 9x).
        float screenCellSize = GridSize * _camera.Zoom;
        int stepCells = 1;
        if (screenCellSize < 10f)
        {
            stepCells = 9;
        }
        else if (screenCellSize < 18f)
        {
            stepCells = 3;
        }

        int worldStep = GridSize * stepCells;
        float screenStep = worldStep * _camera.Zoom;
        const float fullVisibilityCellSize = 26f;
        const float hiddenCellSize = 4f;
        if (screenStep <= hiddenCellSize)
        {
            return;
        }

        float fade = (screenStep - hiddenCellSize) / (fullVisibilityCellSize - hiddenCellSize);
        fade = MathHelper.Clamp(fade, 0f, 1f);
        fade *= fade;

        int baseAlpha = _snapToGrid ? 32 : 16;
        byte alpha = (byte)MathHelper.Clamp((int)MathF.Round(baseAlpha * fade), 0, 36);
        if (alpha == 0)
        {
            return;
        }

        Color gridColor = new Color((byte)255, (byte)255, (byte)255, alpha);

        int startX = FloorToMultiple(visibleWorldBounds.Left, worldStep);
        int endX = visibleWorldBounds.Right + worldStep;
        int lastScreenX = int.MinValue;
        for (int worldX = startX; worldX <= endX; worldX += worldStep)
        {
            int screenX = (int)MathF.Round(_camera.WorldToScreen(new Vector2(worldX, 0f), viewport).X);
            if (screenX < 0 || screenX >= viewport.Width || screenX == lastScreenX)
            {
                continue;
            }

            lastScreenX = screenX;
            spriteBatch.Draw(pixel, new Rectangle(screenX, 0, 1, viewport.Height), gridColor);
        }

        int startY = FloorToMultiple(visibleWorldBounds.Top, worldStep);
        int endY = visibleWorldBounds.Bottom + worldStep;
        int lastScreenY = int.MinValue;
        for (int worldY = startY; worldY <= endY; worldY += worldStep)
        {
            int screenY = (int)MathF.Round(_camera.WorldToScreen(new Vector2(0f, worldY), viewport).Y);
            if (screenY < 0 || screenY >= viewport.Height || screenY == lastScreenY)
            {
                continue;
            }

            lastScreenY = screenY;
            spriteBatch.Draw(pixel, new Rectangle(0, screenY, viewport.Width, 1), gridColor);
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
            case EditorObjectKind.PlayerSpawn:
                DrawPlayerSpawnMarker(spriteBatch, pixel, new Vector2(position.X, position.Y), _level.PlayerStartColor, 0.55f);
                break;
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
        LayoutColorPanel();
        Rectangle panel = _colorPanelBounds;
        spriteBatch.Draw(pixel, panel, new Color(22, 26, 34, 220));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(134, 145, 166), 2);
        DrawColorSwatch(spriteBatch, pixel, _colorRedBounds, GameColor.Red);
        DrawColorSwatch(spriteBatch, pixel, _colorGreenBounds, GameColor.Green);
        DrawColorSwatch(spriteBatch, pixel, _colorBlueBounds, GameColor.Blue);
        DrawColorSwatch(spriteBatch, pixel, _colorWhiteBounds, GameColor.White);
    }

    private void DrawColorSwatch(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, GameColor color)
    {
        bool selected = _selectedColor == color;
        bool hovered = bounds.Contains(UiPointer);
        spriteBatch.Draw(pixel, bounds, color.ToXnaColor());
        Color border = selected
            ? new Color(255, 220, 80)
            : hovered
                ? Color.White
                : Color.Black;
        int thickness = selected ? 3 : 2;
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, border, thickness);
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

        DrawToolbarSlot(spriteBatch, pixel, _playerSpawnSlotBounds, _playerSpawnSlotHovered, _toolbarDragKind == EditorObjectKind.PlayerSpawn, "SPAWN",
            (sb, px, bounds) => sb.Draw(pixel, bounds, Color.Red));
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
        int totalWidth = (padding * 2) + (slotSize * 4) + (slotGap * 3);
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

        _playerSpawnSlotBounds = new Rectangle(
            _launchPadSlotBounds.Right + slotGap,
            _toolbarPanelBounds.Top + padding,
            slotSize,
            slotSize);

        _goalSlotHovered = _goalSlotBounds.Contains(UiPointer);
        _checkpointSlotHovered = _checkpointSlotBounds.Contains(UiPointer);
        _launchPadSlotHovered = _launchPadSlotBounds.Contains(UiPointer);
        _playerSpawnSlotHovered = _playerSpawnSlotBounds.Contains(UiPointer);

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
        else if (_playerSpawnSlotHovered)
        {
            _hoveredToolbarKind = EditorObjectKind.PlayerSpawn;
        }
        else
        {
            _hoveredToolbarKind = EditorObjectKind.None;
        }
    }

    private void LayoutBackButton()
    {
        Viewport viewport = _game.Viewport;
        int height = Math.Clamp((int)(viewport.Height * 0.058f), 36, 44);
        int gap = 10;
        int backWidth = Math.Min(180, Math.Max(120, (int)(viewport.Width * 0.14f)));
        int applyWidth = Math.Min(120, Math.Max(90, backWidth - 40));
        int testWidth = Math.Min(100, Math.Max(80, applyWidth - 10));
        int x = _colorPanelBounds.Left;
        int y = _colorPanelBounds.Bottom + gap;

        _applyButton.Bounds = new Rectangle(x, y, applyWidth, height);
        _testButton.Bounds = new Rectangle(x + applyWidth + gap, y, testWidth, height);
        _backButton.Bounds = new Rectangle(x, y + height + gap, backWidth, height);
    }

    private void UpdateEditorChromeButtonHover()
    {
        if (IsGamepadCursorMode())
        {
            _applyButton.SetPointerHover(_applyButton.Bounds.Contains(UiPointer));
            _testButton.SetPointerHover(_testButton.Bounds.Contains(UiPointer));
            _backButton.SetPointerHover(_backButton.Bounds.Contains(UiPointer));
            return;
        }

        _applyButton.Update(_game.Input, _game.Input.Navigation);
        _testButton.Update(_game.Input, _game.Input.Navigation);
        _backButton.Update(_game.Input, _game.Input.Navigation);
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

    private void StartEditorTest()
    {
        if (_isDirty)
        {
            SaveLevel(force: true);
        }

        Level testLevel = Level.FromData(_level.ToData());
        RopeGameplayMode ropeMode = LevelRules.ClampRopeMode(testLevel, RopeGameplayMode.ColoredPhysics);
        _game.ChangeScene(new GameScene(
            _game,
            _levelId,
            ropeMode,
            lavaRiseEnabled: testLevel.LavaRise,
            ghostBestRunEnabled: false,
            playerCollisionEnabled: testLevel.PlayerCollision,
            editorTestMode: true,
            levelOverride: testLevel));
    }

    private void BeginHistoryGesture()
    {
        if (_historyGestureBefore is not null)
        {
            return;
        }

        _historyGestureBefore = _level.ToData();
    }

    private void EndHistoryGesture()
    {
        if (_historyGestureBefore is null)
        {
            return;
        }

        LevelData before = _historyGestureBefore;
        _historyGestureBefore = null;

        LevelData after = _level.ToData();
        if (LevelDataEquals(before, after))
        {
            return;
        }

        _undoStack.Add(before);
        while (_undoStack.Count > MaxUndoDepth)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
    }

    private void TryUndo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        CancelActiveEditGesture();
        LevelData current = _level.ToData();
        LevelData previous = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(current);
        while (_redoStack.Count > MaxUndoDepth)
        {
            _redoStack.RemoveAt(0);
        }

        RestoreLevelFromHistory(previous);
    }

    private void TryRedo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        CancelActiveEditGesture();
        LevelData current = _level.ToData();
        LevelData next = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(current);
        while (_undoStack.Count > MaxUndoDepth)
        {
            _undoStack.RemoveAt(0);
        }

        RestoreLevelFromHistory(next);
    }

    private void RestoreLevelFromHistory(LevelData data)
    {
        _historyGestureBefore = null;
        _level.ReplaceFromData(data);
        ClearSelection();
        _hoveredPlatform = null;
        _hoveredGoal = null;
        _hoveredCheckpoint = null;
        _hoveredLaunchPad = null;
        _isDirty = true;
    }

    private void CancelActiveEditGesture()
    {
        _historyGestureBefore = null;
        _isCreating = false;
        _isMarqueeSelecting = false;
        _marqueeBounds = Rectangle.Empty;
        _isDragging = false;
        _isDraggingGoal = false;
        _isDraggingCheckpoint = false;
        _isDraggingLaunchPad = false;
        _isDraggingPlayerSpawn = false;
        _isDraggingLavaLine = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
        _toolbarDragKind = EditorObjectKind.None;
        _previewBounds = Rectangle.Empty;
    }

    private static bool LevelDataEquals(LevelData left, LevelData right)
    {
        string leftJson = JsonSerializer.Serialize(left, UndoJsonOptions);
        string rightJson = JsonSerializer.Serialize(right, UndoJsonOptions);
        return leftJson == rightJson;
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

        if (!LevelLibrary.CanSaveLevel(_levelId))
        {
            return;
        }

        if (!LevelLibrary.SaveLevel(_level, _levelId))
        {
            return;
        }
        BestTimeStorage.InvalidateOfficialOnLevelEdit(_levelId);
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

    private static int FloorToMultiple(int value, int multiple)
    {
        if (multiple <= 0)
        {
            return value;
        }

        return (int)MathF.Floor(value / (float)multiple) * multiple;
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
