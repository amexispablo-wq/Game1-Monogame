using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class UIFocusManager
{
    private const float StickRepeatDelaySeconds = 0.2f;

    private readonly List<IFocusable> _items = new();
    private int _focusedIndex = -1;
    private float _stickRepeatTimer;
    private bool _stickHeldLastFrame;
    private Point _lastPointerPosition;

    public IFocusable? Focused =>
        _focusedIndex >= 0 && _focusedIndex < _items.Count ? _items[_focusedIndex] : null;

    public int FocusedIndex => _focusedIndex;

    public void Clear()
    {
        _items.Clear();
    }

    public void ResetFocus()
    {
        _focusedIndex = -1;
        _stickRepeatTimer = 0f;
        _stickHeldLastFrame = false;
        _lastPointerPosition = Point.Zero;
    }

    public void Add(IFocusable item)
    {
        _items.Add(item);
    }

    public void Update(GameTime gameTime, InputManager input)
    {
        if (_items.Count == 0)
        {
            return;
        }

        InputNavigationService navigation = input.Navigation;
        Point pointer = input.UiPointerPosition;
        bool pointerMoved = pointer != _lastPointerPosition;
        _lastPointerPosition = pointer;

        if (navigation.AllowPointerHoverFocus && (pointerMoved || input.UiPointerPressed))
        {
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

        EnsureValidFocus();

        IFocusable? focused = Focused;
        if (focused is null)
        {
            UpdateItems(input, navigation);
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        bool movedFocus = false;

        if (navigation.IsKeyboardActive)
        {
            if (input.MenuTabBackwardPressed)
            {
                MoveFocus(-1);
                movedFocus = true;
            }
            else if (input.MenuTabPressed)
            {
                MoveFocus(1);
                movedFocus = true;
            }

            if (!movedFocus && (input.MenuMoveLeftPressed || input.MenuMoveRightPressed))
            {
                int direction = input.MenuMoveLeftPressed ? -1 : 1;
                if (!focused.CaptureHorizontalNavigation || !focused.OnHorizontal(direction))
                {
                    MoveFocusHorizontal(direction);
                }
            }
            else if (!movedFocus && input.MenuMoveUpPressed)
            {
                MoveFocus(-1);
            }
            else if (!movedFocus && input.MenuMoveDownPressed)
            {
                MoveFocus(1);
            }
        }
        else if (navigation.IsGamepadActive)
        {
            if (input.MenuMoveLeftPressed || input.MenuMoveRightPressed)
            {
                int direction = input.MenuMoveLeftPressed ? -1 : 1;
                if (!focused.CaptureHorizontalNavigation || !focused.OnHorizontal(direction))
                {
                    MoveFocusHorizontal(direction);
                }
            }
            else if (input.MenuMoveUpPressed)
            {
                MoveFocus(-1);
            }
            else if (input.MenuMoveDownPressed)
            {
                MoveFocus(1);
            }
            else
            {
                movedFocus |= UpdateStickNavigation(dt, input, focused);
            }
        }

        UpdateItems(input, navigation);

        focused = Focused;
        if (focused is not null && ShouldConfirm(input, navigation))
        {
            focused.OnConfirm();
        }
    }

    public void DrawFocusHighlights(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime, InputManager input)
    {
        if (!input.Navigation.ShouldDrawFocusHighlight())
        {
            return;
        }

        Focused?.DrawFocusHighlight(spriteBatch, pixel, gameTime);
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

    private void UpdateItems(InputManager input, InputNavigationService navigation)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].Update(input, navigation, i == _focusedIndex);
        }
    }

    private bool UpdateStickNavigation(float dt, InputManager input, IFocusable focused)
    {
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

        if (stickLeft || stickRight)
        {
            int direction = stickLeft ? -1 : 1;
            if (!focused.CaptureHorizontalNavigation || !focused.OnHorizontal(direction))
            {
                MoveFocusHorizontal(direction);
            }
        }
        else if (stickUp)
        {
            MoveFocus(-1);
        }
        else if (stickDown)
        {
            MoveFocus(1);
        }

        _stickRepeatTimer = StickRepeatDelaySeconds;
        _stickHeldLastFrame = true;
        return true;
    }

    private void MoveFocus(int direction)
    {
        if (_items.Count == 0)
        {
            return;
        }

        int start = _focusedIndex < 0 ? 0 : _focusedIndex;
        for (int step = 1; step <= _items.Count; step++)
        {
            int next = (start + direction * step + _items.Count) % _items.Count;
            if (_items[next].IsEnabled)
            {
                SetFocus(next);
                return;
            }
        }
    }

    private void MoveFocusHorizontal(int direction)
    {
        if (_items.Count <= 1 || _focusedIndex < 0)
        {
            return;
        }

        Rectangle current = _items[_focusedIndex].Bounds;
        Vector2 currentCenter = new(current.Center.X, current.Center.Y);
        int bestIndex = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < _items.Count; i++)
        {
            if (i == _focusedIndex || !_items[i].IsEnabled)
            {
                continue;
            }

            Rectangle candidate = _items[i].Bounds;
            Vector2 candidateCenter = new(candidate.Center.X, candidate.Center.Y);
            float dx = candidateCenter.X - currentCenter.X;
            float dy = candidateCenter.Y - currentCenter.Y;

            if (direction < 0 && dx >= -8f)
            {
                continue;
            }

            if (direction > 0 && dx <= 8f)
            {
                continue;
            }

            float score = MathF.Abs(dx) + MathF.Abs(dy) * 0.35f;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            SetFocus(bestIndex);
        }
    }

    private void SetFocus(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _focusedIndex = index;
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
