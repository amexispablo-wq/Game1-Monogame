using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game1_Monogame;

public sealed class EditorScene : IScene
{
    private const int GridSize = 32;
    private const int ResizeMargin = 8;
    private const int MinPlatformSize = GridSize; // Must be at least one grid cell

    private readonly Game1 _game;
    private readonly string _levelId;
    private readonly Level _level;
    private readonly Camera _camera;
    private readonly Button _backButton = new("Back to Menu") { TextScale = 2 };

    private Platform _selectedPlatform;
    private Platform _hoveredPlatform;
    private Goal _selectedGoal;
    private Goal _hoveredGoal;
    private ResizeHandle _activeHandle;
    private Rectangle _resizeStartBounds;
    private Point _resizeStartMouse;
    private readonly List<Platform> _selectedPlatforms = new();
    private readonly Dictionary<Platform, Rectangle> _dragStartBounds = new();
    private readonly List<PlatformData> _clipboard = new();
    private Point _clipboardOrigin;
    private int _pasteCount;
    private Point _dragStartMouse;
    private bool _isCreating;
    private bool _isDragging;
    private bool _isDraggingGoal;
    private bool _isDraggingGoalFromToolbar;
    private bool _isResizing;
    private bool _isPanningCamera;
    private Point _createStart;
    private Point _goalDragStartMouse;
    private Point _goalDragStartPosition;
    private Point _goalPreviewPosition;
    private Rectangle _previewBounds;
    private Rectangle _toolbarPanelBounds;
    private Rectangle _goalSlotBounds;
    private bool _snapToGrid = true;
    private GameColor _selectedColor = GameColor.Red;
    private bool _goalSlotHovered;
    private bool _isDirty;

    public EditorScene(Game1 game, string levelId = "level_1")
    {
        _game = game;
        _levelId = levelId;
        _level = LevelManager.LoadLevel(levelId);
        _camera = new Camera(new Vector2(game.Viewport.Width * 0.5f, game.Viewport.Height * 0.5f));
    }

    public void Update(GameTime gameTime)
    {
        LayoutBackButton();
        LayoutEditorToolbar();

        if (_backButton.Update(_game.Input))
        {
            SaveLevel();
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        if (_game.Input.ExitPressed)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
            return;
        }

        HandleKeyboard();
        bool mouseOverToolbar = IsMouseOverToolbar();
        bool cameraBlockedByUi = _backButton.IsHovered || (mouseOverToolbar && !_isDraggingGoalFromToolbar);
        HandleCameraInput(cameraBlockedByUi);

        Point mouse = GetMouseWorldPosition();
        UpdateHoverState(mouse);

        if (_isDraggingGoalFromToolbar)
        {
            ContinueToolbarGoalDrag(mouse);
            if (_game.Input.LeftMouseReleased)
            {
                EndToolbarGoalDrag(mouse, IsMouseOverUi());
            }

            return;
        }

        if (_game.Input.LeftMousePressed && _goalSlotHovered)
        {
            BeginToolbarGoalDrag(mouse);
            return;
        }

        bool canUseWorldMouse = !IsMouseOverUi() || _isCreating || _isDragging || _isResizing || _isDraggingGoal;
        if (!canUseWorldMouse)
        {
            return;
        }

        if (_game.Input.LeftMousePressed)
        {
            BeginMouseAction(mouse);
        }

        if (_game.Input.LeftMouseHeld)
        {
            ContinueMouseAction(mouse);
        }

        if (_game.Input.LeftMouseReleased)
        {
            EndMouseAction();
        }
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
        _level.DrawGoals(spriteBatch, pixel, debugDraw: false);

        if (_hoveredPlatform is not null && !_selectedPlatforms.Contains(_hoveredPlatform))
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _hoveredPlatform.Bounds, Color.White, GetWorldLineThickness(2));
        }

        if (_hoveredGoal is not null && _hoveredGoal != _selectedGoal)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _hoveredGoal.Bounds, Color.White, GetWorldLineThickness(2));
        }

        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, selectedPlatform.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
        }

        if (_selectedGoal is not null)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, _selectedGoal.Bounds, new Color(255, 220, 80), GetWorldLineThickness(3));
        }

        if (_selectedPlatforms.Count == 1 && _selectedPlatform is not null)
        {
            DrawResizeHandles(spriteBatch, pixel, _selectedPlatform.Bounds);
        }

        if (_isCreating && _previewBounds.Width > 0 && _previewBounds.Height > 0)
        {
            spriteBatch.Draw(pixel, _previewBounds, Color.White * 0.18f);
            DrawHelper.DrawBorder(spriteBatch, pixel, _previewBounds, _selectedColor.ToXnaColor(), GetWorldLineThickness(2));
        }

        if (_isDraggingGoalFromToolbar)
        {
            DrawGoalPreview(spriteBatch, pixel, _goalPreviewPosition);
        }

        spriteBatch.End();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawEditorUi(spriteBatch, pixel);
        DrawToolbar(spriteBatch, pixel);
        _backButton.Draw(spriteBatch, pixel);
        spriteBatch.End();
    }

    public void OnExit()
    {
        SaveLevel();
    }

    private void HandleKeyboard()
    {
        if (_game.Input.ControlHeld)
        {
            if (_game.Input.IsNewKeyPress(Keys.C))
            {
                CopySelectedPlatforms();
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
            SaveLevel(force: true);
        }

        if (_game.Input.IsNewKeyPress(Keys.Delete) && (_selectedPlatforms.Count > 0 || _selectedGoal is not null))
        {
            if (_selectedGoal is not null)
            {
                _level.RemoveGoal(_selectedGoal);
                _selectedGoal = null;
                _hoveredGoal = null;
                _isDirty = true;
            }

            if (_selectedPlatforms.Count > 0)
            {
                DeleteSelectedPlatforms();
            }

            _isDragging = false;
            _isDraggingGoal = false;
            _isResizing = false;
            _activeHandle = ResizeHandle.None;
            return;
        }

        if (!_game.Input.ControlHeld && _game.Input.RequestedColor is { } requestedColor)
        {
            _selectedColor = requestedColor;
            ApplyColorToSelection(requestedColor);
        }
    }

    private void HandleCameraInput(bool mouseOverUi)
    {
        bool canStartPanning = !_isCreating && !_isDragging && !_isDraggingGoal && !_isResizing && !mouseOverUi;
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
            _camera.ZoomAt(zoomFactor, _game.Input.MousePosition, _game.Viewport);
        }
    }

    private void BeginMouseAction(Point mouse)
    {
        if (_isPanningCamera)
        {
            return;
        }

        Goal clickedGoal = FindGoalAt(mouse);
        Platform clickedPlatform = FindPlatformAt(mouse);

        if (_game.Input.ShiftHeld)
        {
            if (clickedPlatform is not null)
            {
                ToggleSelection(clickedPlatform);
            }
            else if (clickedGoal is not null)
            {
                SelectSingleGoal(clickedGoal);
            }

            return;
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

        if (_isResizing && _selectedPlatform is not null && _activeHandle != ResizeHandle.None)
        {
            ResizeSelectedPlatform(mouse);
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
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
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
        _isCreating = false;
        _resizeStartBounds = platform.Bounds;
        _resizeStartMouse = _snapToGrid ? Snap(mouse) : mouse;
    }

    private void SelectSingleGoal(Goal goal)
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoal = goal;
    }

    private void StartGoalDrag(Goal goal, Point mouse)
    {
        SelectSingleGoal(goal);
        _isDraggingGoal = true;
        _isDragging = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _goalDragStartMouse = mouse;
        _goalDragStartPosition = goal.Position;
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

    private void BeginToolbarGoalDrag(Point mouse)
    {
        ClearSelection();
        _isDraggingGoalFromToolbar = true;
        _isDraggingGoal = false;
        _isDragging = false;
        _isResizing = false;
        _isCreating = false;
        _activeHandle = ResizeHandle.None;
        _goalPreviewPosition = GetGoalPlacementPosition(mouse);
    }

    private void ContinueToolbarGoalDrag(Point mouse)
    {
        _goalPreviewPosition = GetGoalPlacementPosition(mouse);
    }

    private void EndToolbarGoalDrag(Point mouse, bool releaseOverUi)
    {
        _goalPreviewPosition = GetGoalPlacementPosition(mouse);

        if (!releaseOverUi)
        {
            Goal goal = new(_goalPreviewPosition);
            _level.AddGoal(goal);
            SelectSingleGoal(goal);
            _isDirty = true;
        }

        _isDraggingGoalFromToolbar = false;
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

    private void SelectSinglePlatform(Platform platform)
    {
        _selectedPlatforms.Clear();
        _selectedPlatforms.Add(platform);
        _selectedPlatform = platform;
        _selectedGoal = null;
    }

    private void ToggleSelection(Platform platform)
    {
        _selectedGoal = null;

        if (_selectedPlatforms.Contains(platform))
        {
            _selectedPlatforms.Remove(platform);
            _selectedPlatform = _selectedPlatforms.Count > 0 ? _selectedPlatforms[^1] : null;
            return;
        }

        _selectedPlatforms.Add(platform);
        _selectedPlatform = platform;
    }

    private void ClearSelection()
    {
        _selectedPlatforms.Clear();
        _selectedPlatform = null;
        _selectedGoal = null;
    }

    private void DeleteSelectedPlatforms()
    {
        for (int i = _selectedPlatforms.Count - 1; i >= 0; i--)
        {
            _level.RemovePlatform(_selectedPlatforms[i]);
        }

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

    private void CopySelectedPlatforms()
    {
        _clipboard.Clear();
        if (_selectedPlatforms.Count == 0)
        {
            return;
        }

        Platform originPlatform = _selectedPlatforms[0];
        _clipboardOrigin = originPlatform.Bounds.Location;
        foreach (Platform selectedPlatform in _selectedPlatforms)
        {
            Rectangle bounds = selectedPlatform.Bounds;
            _clipboard.Add(new PlatformData
            {
                X = bounds.X - _clipboardOrigin.X,
                Y = bounds.Y - _clipboardOrigin.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Color = selectedPlatform.PlatformColor
            });
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

        _selectedGoal = null;
        _selectedPlatforms.Clear();
        foreach (PlatformData data in _clipboard)
        {
            Rectangle platformBounds = new(
                pasteOrigin.X + data.X,
                pasteOrigin.Y + data.Y,
                data.Width,
                data.Height);

            // Snap the pasted platform to grid to prevent offset
            platformBounds = SnapRectangleToGrid(platformBounds);

            Platform platform = new(platformBounds, data.Color);
            _level.AddPlatform(platform);
            _selectedPlatforms.Add(platform);
            _selectedPlatform = platform;
        }

        _pasteCount++;
        _isDirty = true;
    }

    private Point GetMouseWorldPosition()
    {
        Vector2 worldPosition = _camera.ScreenToWorld(_game.Input.MousePosition, _game.Viewport);
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

    private Point GetGoalPlacementPosition(Point mouse)
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

    private void UpdateHoverState(Point mouse)
    {
        bool objectActionActive = _isCreating || _isDragging || _isDraggingGoal || _isResizing || _isDraggingGoalFromToolbar;
        if (objectActionActive)
        {
            _hoveredPlatform = (_isCreating || _isDragging || _isResizing) ? _selectedPlatform : null;
            _hoveredGoal = _isDraggingGoal ? _selectedGoal : null;
            return;
        }

        _hoveredGoal = FindGoalAt(mouse);
        _hoveredPlatform = _hoveredGoal is null ? FindPlatformAt(mouse) : null;
    }

    private bool IsMouseOverToolbar()
    {
        return _toolbarPanelBounds.Contains(_game.Input.MousePosition);
    }

    private bool IsMouseOverUi()
    {
        return _backButton.Bounds.Contains(_game.Input.MousePosition) || IsMouseOverToolbar();
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
        Color gridColor = _snapToGrid ? new Color(255, 255, 255, 36) : new Color(255, 255, 255, 16);
        int lineThickness = GetWorldLineThickness(1);

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
        Color slotFill = _goalSlotHovered || _isDraggingGoalFromToolbar
            ? new Color(82, 94, 118)
            : new Color(48, 57, 74);
        Color slotBorder = _goalSlotHovered || _isDraggingGoalFromToolbar
            ? new Color(255, 220, 80)
            : new Color(134, 145, 166);

        spriteBatch.Draw(pixel, _toolbarPanelBounds, panelFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, _toolbarPanelBounds, panelBorder, 2);
        spriteBatch.Draw(pixel, _goalSlotBounds, slotFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, _goalSlotBounds, slotBorder, 2);

        int inset = Math.Max(5, _goalSlotBounds.Width / 8);
        int labelScale = _goalSlotBounds.Width >= 64 ? 2 : 1;
        labelScale = FitTextScale("GOAL", labelScale, _goalSlotBounds.Width - (inset * 2));
        Point labelSize = SimpleTextRenderer.MeasureString("GOAL", labelScale);
        Rectangle iconBounds = new(
            _goalSlotBounds.Left + inset,
            _goalSlotBounds.Top + inset,
            _goalSlotBounds.Width - (inset * 2),
            Math.Max(1, _goalSlotBounds.Height - (inset * 2) - labelSize.Y - 3));

        Goal.DrawIcon(spriteBatch, pixel, iconBounds);
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            "GOAL",
            new Vector2(_goalSlotBounds.Center.X - (labelSize.X * 0.5f), _goalSlotBounds.Bottom - inset - labelSize.Y),
            labelScale,
            Color.White);
    }

    private void LayoutEditorToolbar()
    {
        Viewport viewport = _game.Viewport;
        int minDimension = Math.Min(viewport.Width, viewport.Height);
        int slotSize = Math.Clamp((int)(minDimension * 0.09f), 44, 72);
        int padding = Math.Clamp(slotSize / 4, 10, 18);
        int bottomMargin = Math.Max(8, (int)(viewport.Height * 0.025f));
        int panelWidth = (padding * 2) + slotSize;
        int panelHeight = (padding * 2) + slotSize;

        _toolbarPanelBounds = new Rectangle(
            (viewport.Width - panelWidth) / 2,
            Math.Max(0, viewport.Height - bottomMargin - panelHeight),
            panelWidth,
            panelHeight);
        _goalSlotBounds = new Rectangle(
            _toolbarPanelBounds.Left + padding,
            _toolbarPanelBounds.Top + padding,
            slotSize,
            slotSize);
        _goalSlotHovered = _goalSlotBounds.Contains(_game.Input.MousePosition);
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
