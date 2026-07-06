using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class UIFocusManager
{
    private const float StickRepeatDelaySeconds = 0.2f;

    private readonly List<IFocusable> _items = new();
    private readonly List<string> _ids = new();
    private readonly NavigationGraph _navigation = new();
    private int _focusedIndex = -1;
    private string? _stickyFocusId;
    private float _stickRepeatTimer;
    private bool _stickHeldLastFrame;
    private string _lastValidatedScene = "";

    public NavigationGraph Navigation => _navigation;

    /// <summary>Optional display name for the debug panel (defaults to the active scene name).</summary>
    public string Name { get; set; } = "";

    public IFocusable? Focused =>
        _focusedIndex >= 0 && _focusedIndex < _items.Count ? _items[_focusedIndex] : null;

    public int FocusedIndex => _focusedIndex;

    public string? FocusedId => _focusedIndex >= 0 && _focusedIndex < _ids.Count ? _ids[_focusedIndex] : null;

    public bool IsCapturingNavigation => Focused?.CapturesNavigation == true;

    public void Clear()
    {
        _items.Clear();
        _ids.Clear();
        _navigation.Clear();
    }

    public void ResetFocus()
    {
        _focusedIndex = -1;
        _stickyFocusId = null;
        _stickRepeatTimer = 0f;
        _stickHeldLastFrame = false;
    }

    public int Add(IFocusable item)
    {
        return Add(item, DefaultId(item, _items.Count));
    }

    public int Add(IFocusable item, string id)
    {
        int index = _items.Count;
        _items.Add(item);
        _ids.Add(string.IsNullOrEmpty(id) ? DefaultId(item, index) : id);
        _navigation.AddNode();
        return index;
    }

    private static string DefaultId(IFocusable item, int index)
    {
        string name = item.GetType().Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        if (name.StartsWith("Focusable", StringComparison.Ordinal))
        {
            name = name["Focusable".Length..];
        }

        return $"{name}{index}";
    }

    public void Update(GameTime gameTime, InputManager input)
    {
        if (_items.Count == 0)
        {
            return;
        }

        HandleDebug(input);

        InputNavigationService navigation = input.Navigation;
        IFocusable? focused = Focused;
        bool capturing = focused?.CapturesNavigation == true;

        if (!capturing)
        {
            UpdatePointerFocus(input, navigation);
        }

        EnsureValidFocus();
        focused = Focused;
        if (focused is null)
        {
            UpdateItems(input, navigation);
            return;
        }

        if (ShouldCancel(input, navigation) && focused.OnCancel())
        {
            UpdateItems(input, navigation);
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        ProcessDirectionalInput(dt, input, navigation, focused);

        UpdateItems(input, navigation);

        focused = Focused;
        if (focused is not null && ShouldConfirm(input, navigation))
        {
            focused.OnConfirm();
        }
    }

    public void DrawFocusHighlights(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime, InputManager input)
    {
        if (NavigationDebug.Enabled)
        {
            DrawDebugOverlay(spriteBatch, pixel, input);
        }

        if (!input.Navigation.ShouldDrawFocusHighlight())
        {
            return;
        }

        Focused?.DrawFocusHighlight(spriteBatch, pixel, gameTime);
    }

    private void HandleDebug(InputManager input)
    {
        if (NavigationDebug.Enabled && NavigationDebug.CurrentScene != _lastValidatedScene)
        {
            _lastValidatedScene = NavigationDebug.CurrentScene;
            ValidateNavigation();
        }

        if (input.NavigationStepPressed && _items.Count > 0 && NavigationDebug.TryConsumeStep())
        {
            StepFocus();
        }
    }

    private void StepFocus()
    {
        int start = _focusedIndex;
        for (int offset = 1; offset <= _items.Count; offset++)
        {
            int candidate = ((start < 0 ? -1 : start) + offset) % _items.Count;
            if (candidate < 0)
            {
                candidate += _items.Count;
            }

            if (_items[candidate].IsEnabled)
            {
                SetFocus(candidate);
                break;
            }
        }

        PrintFocusInfo();
    }

    private void PrintFocusInfo()
    {
        string menu = string.IsNullOrEmpty(Name) ? NavigationDebug.CurrentScene : Name;
        NavigationDebug.Log("");
        NavigationDebug.Log($"[Navigation] Scene: {menu}");
        NavigationDebug.Log($"Current Widget: {IdAt(_focusedIndex)}");
        NavigationDebug.Log($"Up:    {NeighborId(_focusedIndex, NavigationDirection.Up)}");
        NavigationDebug.Log($"Down:  {NeighborId(_focusedIndex, NavigationDirection.Down)}");
        NavigationDebug.Log($"Left:  {NeighborId(_focusedIndex, NavigationDirection.Left)}");
        NavigationDebug.Log($"Right: {NeighborId(_focusedIndex, NavigationDirection.Right)}");
    }

    public void ValidateNavigation()
    {
        string scene = string.IsNullOrEmpty(Name) ? NavigationDebug.CurrentScene : Name;
        var directions = new[]
        {
            NavigationDirection.Up, NavigationDirection.Down,
            NavigationDirection.Left, NavigationDirection.Right
        };

        void Report(string widget, string problem)
        {
            NavigationDebug.Log("");
            NavigationDebug.Log("[Navigation]");
            NavigationDebug.Log($"Widget: {widget}");
            NavigationDebug.Log(problem);
            NavigationDebug.Log($"Scene: {scene}");
        }

        // Duplicate / repeated IDs.
        for (int i = 0; i < _ids.Count; i++)
        {
            for (int j = i + 1; j < _ids.Count; j++)
            {
                if (_ids[i] == _ids[j])
                {
                    Report(_ids[i], $"DUPLICATE ID (indices {i} and {j})");
                }
            }
        }

        // Track incoming edges to detect widgets with no entry.
        var incoming = new int[_items.Count];
        var editableIndices = new List<int>();

        for (int i = 0; i < _items.Count; i++)
        {
            if (IsEditableWidget(_items[i]))
            {
                editableIndices.Add(i);
            }

            bool hasExit = false;
            foreach (NavigationDirection dir in directions)
            {
                int? neighbor = _navigation.GetNeighbor(i, dir);
                if (neighbor is null)
                {
                    continue;
                }

                int n = neighbor.Value;
                if (n < 0 || n >= _items.Count)
                {
                    Report(IdAt(i), $"{dir} -> INVALID");
                    continue;
                }

                hasExit = true;
                incoming[n]++;

                if (n == i)
                {
                    Report(IdAt(i), $"{dir} -> SELF LOOP");
                }
            }

            if (!hasExit && _items.Count > 1)
            {
                Report(IdAt(i), "NO EXIT (cannot navigate away)");
            }
        }

        if (_items.Count > 1)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (incoming[i] == 0)
                {
                    Report(IdAt(i), "NO ENTRY (cannot be reached)");
                }
            }
        }

        foreach (int i in editableIndices)
        {
            if (!EditableWidgetSupportsCancel(_items[i]))
            {
                Report(IdAt(i), "EDITABLE WITHOUT B/CANCEL EXIT");
            }
        }
    }

    private static bool IsEditableWidget(IFocusable widget)
    {
        string name = widget.GetType().Name;
        return name.Contains("CycleSelector")
            || name.Contains("Slider")
            || name.Contains("Dropdown")
            || name.Contains("Resolution")
            || widget is FocusableTextInput;
    }

    private static bool EditableWidgetSupportsCancel(IFocusable widget)
    {
        return IsEditableWidget(widget);
    }

    /// <summary>
    /// Call after rebuilding the graph. Restores sticky focus by id, then applies default if still unset.
    /// </summary>
    public void FinalizeFocus(string? defaultFocusId = null)
    {
        if (_stickyFocusId is not null)
        {
            FocusById(_stickyFocusId);
        }

        if (_focusedIndex < 0 && defaultFocusId is not null)
        {
            SetDefaultFocus(defaultFocusId);
        }

        EnsureValidFocus();
    }

    private string IdAt(int index) =>
        index >= 0 && index < _ids.Count ? _ids[index] : "NONE";

    private string NeighborId(int index, NavigationDirection direction)
    {
        int? neighbor = _navigation.GetNeighbor(index, direction);
        return neighbor is int n ? IdAt(n) : "NONE";
    }

    private void DrawDebugOverlay(SpriteBatch spriteBatch, Texture2D pixel, InputManager input)
    {
        DrawF9TraversalChain(spriteBatch, pixel);

        // Connection lines first (under labels). Only Down/Right drawn; Up/Left are their reverse.
        for (int i = 0; i < _items.Count; i++)
        {
            Vector2 from = _items[i].Bounds.Center.ToVector2();
            DrawConnection(spriteBatch, pixel, from, i, NavigationDirection.Right, new Color(120, 200, 255, 180));
            DrawConnection(spriteBatch, pixel, from, i, NavigationDirection.Down, new Color(255, 200, 120, 180));
        }

        for (int i = 0; i < _items.Count; i++)
        {
            Rectangle b = _items[i].Bounds;
            bool focused = i == _focusedIndex;
            Color border = focused ? new Color(120, 255, 140) : new Color(90, 130, 170);
            DrawHelper.DrawBorder(spriteBatch, pixel, b, border, focused ? 3 : 1);

            // F9 traversal order (matches step order) shown top-right of each widget.
            string order = $"#{i + 1}";
            Point orderSize = SimpleTextRenderer.MeasureString(order, 2);
            SimpleTextRenderer.DrawString(spriteBatch, pixel, order, new Vector2(b.Right - orderSize.X - 2, b.Y + 2), 2, new Color(255, 235, 120));

            string header = focused ? $">{IdAt(i)}<" : IdAt(i);
            DrawLabel(spriteBatch, pixel, header, b.X + 2, b.Y + 2, focused ? new Color(150, 255, 170) : Color.White);
            DrawLabel(spriteBatch, pixel, $"U:{NeighborId(i, NavigationDirection.Up)}", b.X + 2, b.Y + 12, new Color(200, 210, 225));
            DrawLabel(spriteBatch, pixel, $"D:{NeighborId(i, NavigationDirection.Down)}", b.X + 2, b.Y + 20, new Color(200, 210, 225));
            DrawLabel(spriteBatch, pixel, $"L:{NeighborId(i, NavigationDirection.Left)}", b.X + 2, b.Y + 28, new Color(200, 210, 225));
            DrawLabel(spriteBatch, pixel, $"R:{NeighborId(i, NavigationDirection.Right)}", b.X + 2, b.Y + 36, new Color(200, 210, 225));
        }

        DrawDebugPanel(spriteBatch, pixel, input);
    }

    private void DrawF9TraversalChain(SpriteBatch spriteBatch, Texture2D pixel)
    {
        var stepOrder = new List<int>();
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].IsEnabled)
            {
                stepOrder.Add(i);
            }
        }

        for (int i = 0; i < stepOrder.Count - 1; i++)
        {
            Vector2 from = _items[stepOrder[i]].Bounds.Center.ToVector2();
            Vector2 to = _items[stepOrder[i + 1]].Bounds.Center.ToVector2();
            DrawLine(spriteBatch, pixel, from, to, new Color(220, 120, 255, 200), 3);
        }
    }

    private void DrawConnection(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, int index, NavigationDirection direction, Color color)
    {
        int? neighbor = _navigation.GetNeighbor(index, direction);
        if (neighbor is not int n || n < 0 || n >= _items.Count)
        {
            return;
        }

        DrawLine(spriteBatch, pixel, from, _items[n].Bounds.Center.ToVector2(), color, 2);
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 a, Vector2 b, Color color, int thickness)
    {
        Vector2 delta = b - a;
        float length = delta.Length();
        if (length < 1f)
        {
            return;
        }

        float angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            pixel,
            a,
            null,
            color,
            angle,
            new Vector2(0f, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private static void DrawLabel(SpriteBatch spriteBatch, Texture2D pixel, string text, int x, int y, Color color)
    {
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, new Vector2(x, y), 1, color);
    }

    private void DrawDebugPanel(SpriteBatch spriteBatch, Texture2D pixel, InputManager input)
    {
        string menu = string.IsNullOrEmpty(Name) ? NavigationDebug.CurrentScene : Name;
        IFocusable? focused = Focused;

        bool editSelector = false;
        bool editCombo = false;
        bool editSlider = false;
        if (focused is not null && focused.IsEditing)
        {
            string typeName = focused.GetType().Name;
            if (typeName.Contains("Cycle"))
            {
                editSelector = true;
            }
            else if (typeName.Contains("Dropdown") || typeName.Contains("Resolution"))
            {
                editCombo = true;
            }
            else if (typeName.Contains("Slider"))
            {
                editSlider = true;
            }
        }

        var lines = new List<string>
        {
            "NAVIGATION DEBUG (F8 off / F9 step)",
            $"Current Menu: {menu}",
            $"Current Widget: {IdAt(_focusedIndex)}",
            $"Input Mode: {input.Navigation.ActiveDevice}",
            $"Editing Selector: {(editSelector ? "Yes" : "No")}",
            $"Editing Combo: {(editCombo ? "Yes" : "No")}",
            $"Editing Slider: {(editSlider ? "Yes" : "No")}",
            BuildF9OrderLine()
        };

        int panelWidth = 360;
        int panelHeight = (lines.Count * 12) + 12;
        Rectangle panel = new(8, 8, panelWidth, panelHeight);
        spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 200));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(120, 255, 140), 1);

        for (int i = 0; i < lines.Count; i++)
        {
            DrawLabel(spriteBatch, pixel, lines[i], panel.X + 6, panel.Y + 6 + (i * 12), Color.White);
        }
    }

    private string BuildF9OrderLine()
    {
        if (_items.Count == 0)
        {
            return "F9 Order: (empty)";
        }

        var parts = new List<string>();
        int step = 1;
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].IsEnabled)
            {
                continue;
            }

            parts.Add($"{step}:{IdAt(i)}");
            step++;
        }

        return $"F9 Order: {string.Join(" > ", parts)}";
    }

    private void UpdatePointerFocus(InputManager input, InputNavigationService navigation)
    {
        if (!navigation.AllowPointerHoverFocus)
        {
            return;
        }

        // Moving the mouse alone must NOT move focus (avoids gamepad/mouse flicker).
        // Only an actual click repositions focus.
        if (!input.UiPointerPressed)
        {
            return;
        }

        Point pointer = input.UiPointerPosition;
        for (int i = 0; i < _items.Count; i++)
        {
            IFocusable item = _items[i];
            if (item.IsEnabled && item.Bounds.Contains(pointer))
            {
                SetFocus(i);
                break;
            }
        }
    }

    /// <summary>Sets focus to the given id only if nothing is focused yet (initial focus).</summary>
    public void SetDefaultFocus(string id)
    {
        if (_focusedIndex >= 0)
        {
            return;
        }

        FocusById(id);
        if (_focusedIndex >= 0)
        {
            _stickyFocusId = id;
        }
    }

    public void FocusById(string id)
    {
        int index = _ids.IndexOf(id);
        if (index >= 0 && index < _items.Count && _items[index].IsEnabled)
        {
            SetFocus(index);
        }
    }

    private void ProcessDirectionalInput(float dt, InputManager input, InputNavigationService navigation, IFocusable focused)
    {
        if (TryGetDirectionPress(input, navigation, out NavigationDirection direction))
        {
            if (focused.CapturesNavigation)
            {
                focused.HandleDirection(direction);
            }
            else
            {
                MoveFocus(direction);
            }

            return;
        }

        if (!navigation.IsGamepadActive)
        {
            return;
        }

        if (!TryGetStickDirection(dt, input, out NavigationDirection stickDirection))
        {
            return;
        }

        if (focused.CapturesNavigation)
        {
            focused.HandleDirection(stickDirection);
        }
        else
        {
            MoveFocus(stickDirection);
        }
    }

    private bool TryGetDirectionPress(InputManager input, InputNavigationService navigation, out NavigationDirection direction)
    {
        direction = NavigationDirection.Down;

        if (navigation.IsKeyboardActive)
        {
            if (input.MenuTabBackwardPressed)
            {
                direction = NavigationDirection.Up;
                return true;
            }

            if (input.MenuTabPressed)
            {
                direction = NavigationDirection.Down;
                return true;
            }
        }

        if (!navigation.IsKeyboardActive && !navigation.IsGamepadActive)
        {
            return false;
        }

        if (navigation.IsKeyboardActive)
        {
            if (input.KeyboardMenuMoveUpPressed)
            {
                direction = NavigationDirection.Up;
                return true;
            }

            if (input.KeyboardMenuMoveDownPressed)
            {
                direction = NavigationDirection.Down;
                return true;
            }

            if (input.KeyboardMenuMoveLeftPressed)
            {
                direction = NavigationDirection.Left;
                return true;
            }

            if (input.KeyboardMenuMoveRightPressed)
            {
                direction = NavigationDirection.Right;
                return true;
            }

            return false;
        }

        if (input.GamepadMenuMoveUpPressed)
        {
            direction = NavigationDirection.Up;
            return true;
        }

        if (input.GamepadMenuMoveDownPressed)
        {
            direction = NavigationDirection.Down;
            return true;
        }

        if (input.GamepadMenuMoveLeftPressed)
        {
            direction = NavigationDirection.Left;
            return true;
        }

        if (input.GamepadMenuMoveRightPressed)
        {
            direction = NavigationDirection.Right;
            return true;
        }

        return false;
    }

    private bool TryGetStickDirection(float dt, InputManager input, out NavigationDirection direction)
    {
        direction = NavigationDirection.Down;
        bool stickUp = input.MenuStickUpHeld;
        bool stickDown = input.MenuStickDownHeld;
        bool stickLeft = input.MenuStickLeftHeld;
        bool stickRight = input.MenuStickRightHeld;
        bool stickHeld = stickUp || stickDown || stickLeft || stickRight;

        if (!stickHeld)
        {
            _stickHeldLastFrame = false;
            _stickRepeatTimer = 0f;
            return false;
        }

        _stickRepeatTimer -= dt;
        if (_stickHeldLastFrame && _stickRepeatTimer > 0f)
        {
            _stickHeldLastFrame = true;
            return false;
        }

        if (stickLeft)
        {
            direction = NavigationDirection.Left;
        }
        else if (stickRight)
        {
            direction = NavigationDirection.Right;
        }
        else if (stickUp)
        {
            direction = NavigationDirection.Up;
        }
        else if (stickDown)
        {
            direction = NavigationDirection.Down;
        }

        _stickRepeatTimer = StickRepeatDelaySeconds;
        _stickHeldLastFrame = true;
        return true;
    }

    private void MoveFocus(NavigationDirection direction)
    {
        if (_focusedIndex < 0)
        {
            EnsureValidFocus();
            return;
        }

        if (TryMoveToEnabledNeighbor(_focusedIndex, direction))
        {
            return;
        }
    }

    private bool TryMoveToEnabledNeighbor(int fromIndex, NavigationDirection direction, int depth = 0)
    {
        if (depth > _items.Count)
        {
            return false;
        }

        int? neighbor = _navigation.GetNeighbor(fromIndex, direction);
        if (neighbor is not int index || index < 0 || index >= _items.Count)
        {
            return false;
        }

        if (_items[index].IsEnabled)
        {
            SetFocus(index);
            return true;
        }

        return TryMoveToEnabledNeighbor(index, direction, depth + 1);
    }

    private static bool ShouldConfirm(InputManager input, InputNavigationService navigation)
    {
        if (navigation.IsKeyboardActive)
        {
            return input.KeyboardMenuConfirmPressed;
        }

        if (navigation.IsGamepadActive)
        {
            return input.GamepadMenuConfirmPressed;
        }

        return false;
    }

    private static bool ShouldCancel(InputManager input, InputNavigationService navigation)
    {
        if (navigation.IsGamepadActive)
        {
            return input.GamepadMenuCancelPressed;
        }

        return navigation.IsKeyboardActive && input.KeyboardMenuCancelPressed;
    }

    private void UpdateItems(InputManager input, InputNavigationService navigation)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].Update(input, navigation, i == _focusedIndex);
        }
    }

    private void SetFocus(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _focusedIndex = index;
        _stickyFocusId = IdAt(index);
    }

    private void EnsureValidFocus()
    {
        if (_focusedIndex >= _items.Count)
        {
            _focusedIndex = _items.Count - 1;
        }

        if (_focusedIndex >= 0 && _focusedIndex < _items.Count && _items[_focusedIndex].IsEnabled)
        {
            return;
        }

        _focusedIndex = -1;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].IsEnabled)
            {
                _focusedIndex = i;
                return;
            }
        }
    }
}
