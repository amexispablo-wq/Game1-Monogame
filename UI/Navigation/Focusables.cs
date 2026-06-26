using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class FocusableButton : IFocusable
{
    private readonly Button _button;

    public FocusableButton(Button button)
    {
        _button = button;
    }

    public Rectangle Bounds => _button.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => false;
    public bool WasActivated { get; private set; }

    public bool OnHorizontal(int direction) => false;

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        WasActivated = true;
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        WasActivated = false;
        bool pointerOver = Bounds.Contains(input.UiPointerPosition);
        if (pointerOver && input.UiPointerPressed && IsEnabled)
        {
            WasActivated = true;
        }

        _button.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableCycleSelector<T> : IFocusable where T : notnull
{
    private readonly CycleSelector<T> _selector;

    public FocusableCycleSelector(CycleSelector<T> selector)
    {
        _selector = selector;
    }

    public Rectangle Bounds => _selector.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => true;

    public bool OnHorizontal(int direction)
    {
        if (!IsEnabled || _selector.Options.Count == 0)
        {
            return false;
        }

        int current = _selector.Options.IndexOf(_selector.CurrentOption);
        int next = (current + direction + _selector.Options.Count) % _selector.Options.Count;
        _selector.CurrentOption = _selector.Options[next];
        return true;
    }

    public bool OnConfirm() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _selector.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableCheckbox : IFocusable
{
    private readonly Checkbox _checkbox;

    public FocusableCheckbox(Checkbox checkbox)
    {
        _checkbox = checkbox;
    }

    public Rectangle Bounds => _checkbox.Bounds;
    public bool IsEnabled => _checkbox.IsEnabled;
    public bool CaptureHorizontalNavigation => false;

    public bool OnHorizontal(int direction) => false;

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        _checkbox.IsChecked = !_checkbox.IsChecked;
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _checkbox.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableSlider : IFocusable
{
    private readonly Slider _slider;

    public FocusableSlider(Slider slider)
    {
        _slider = slider;
    }

    public Rectangle Bounds => _slider.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => true;

    public bool OnHorizontal(int direction)
    {
        if (!IsEnabled)
        {
            return false;
        }

        float range = _slider.MaxValue - _slider.MinValue;
        float step = range * 0.05f;
        _slider.Value = MathHelper.Clamp(_slider.Value + (direction * step), _slider.MinValue, _slider.MaxValue);
        return true;
    }

    public bool OnConfirm() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _slider.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableAction : IFocusable
{
    private readonly Func<bool> _onConfirm;

    public FocusableAction(Rectangle bounds, Func<bool> onConfirm)
    {
        Bounds = bounds;
        _onConfirm = onConfirm;
    }

    public Rectangle Bounds { get; }
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => false;
    public bool WasActivated { get; private set; }

    public bool OnHorizontal(int direction) => false;

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        WasActivated = _onConfirm();
        return WasActivated;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        WasActivated = false;
        if (Bounds.Contains(input.UiPointerPosition) && input.UiPointerPressed)
        {
            OnConfirm();
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableCycleMemberInput : IFocusable
{
    private readonly PartyMember _member;
    private readonly Action<PartyMember, int> _cycle;

    public FocusableCycleMemberInput(Rectangle bounds, PartyMember member, Action<PartyMember, int> cycle)
    {
        Bounds = bounds;
        _member = member;
        _cycle = cycle;
    }

    public Rectangle Bounds { get; }
    public bool IsEnabled => _member.IsLocallyOwned;
    public bool CaptureHorizontalNavigation => true;

    public bool OnHorizontal(int direction)
    {
        if (!IsEnabled)
        {
            return false;
        }

        _cycle(_member, direction);
        return true;
    }

    public bool OnConfirm() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        if (navigation.AllowPointerHoverVisual
            && Bounds.Contains(input.UiPointerPosition)
            && input.UiPointerPressed
            && IsEnabled)
        {
            _cycle(_member, 1);
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableResolutionDropdown : IFocusable
{
    private readonly ResolutionDropdown _dropdown;

    public FocusableResolutionDropdown(ResolutionDropdown dropdown)
    {
        _dropdown = dropdown;
    }

    public Rectangle Bounds => _dropdown.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => _dropdown.IsExpanded;

    public bool OnHorizontal(int direction)
    {
        if (!_dropdown.IsExpanded || _dropdown.Resolutions.Count == 0)
        {
            return false;
        }

        int current = _dropdown.HighlightedIndex ?? _dropdown.Resolutions.FindIndex(
            r => _dropdown.SelectedResolution is not null && r.Equals(_dropdown.SelectedResolution));
        if (current < 0)
        {
            current = 0;
        }

        int next = (current + direction + _dropdown.Resolutions.Count) % _dropdown.Resolutions.Count;
        _dropdown.HighlightedIndex = next;
        return true;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (!_dropdown.IsExpanded)
        {
            _dropdown.IsExpanded = true;
            _dropdown.HighlightedIndex = _dropdown.Resolutions.FindIndex(
                r => _dropdown.SelectedResolution is not null && r.Equals(_dropdown.SelectedResolution));
            if (_dropdown.HighlightedIndex < 0)
            {
                _dropdown.HighlightedIndex = 0;
            }

            return true;
        }

        if (_dropdown.HighlightedIndex is int index && index >= 0 && index < _dropdown.Resolutions.Count)
        {
            _dropdown.SelectedResolution = _dropdown.Resolutions[index];
            _dropdown.IsExpanded = false;
            _dropdown.HighlightedIndex = null;
        }

        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        if (isFocused && input.MenuCancelPressed && _dropdown.IsExpanded)
        {
            _dropdown.IsExpanded = false;
            _dropdown.HighlightedIndex = null;
        }
        else if (isFocused && (input.MenuMoveUpPressed || input.MenuMoveDownPressed) && _dropdown.IsExpanded)
        {
            int direction = input.MenuMoveUpPressed ? -1 : 1;
            OnHorizontal(direction);
        }

        _dropdown.Update(input);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableDropdown<T> : IFocusable where T : notnull
{
    private readonly Dropdown<T> _dropdown;

    public FocusableDropdown(Dropdown<T> dropdown)
    {
        _dropdown = dropdown;
    }

    public Rectangle Bounds => _dropdown.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => _dropdown.IsExpanded;

    public bool OnHorizontal(int direction)
    {
        if (!_dropdown.IsExpanded || _dropdown.Options.Count == 0)
        {
            return false;
        }

        int current = _dropdown.HighlightedIndex ?? Math.Max(0, _dropdown.Options.IndexOf(_dropdown.SelectedOption!));
        int next = (current + direction + _dropdown.Options.Count) % _dropdown.Options.Count;
        _dropdown.HighlightedIndex = next;
        return true;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (!_dropdown.IsExpanded)
        {
            _dropdown.IsExpanded = true;
            return true;
        }

        if (_dropdown.HighlightedIndex is int index && index >= 0 && index < _dropdown.Options.Count)
        {
            _dropdown.SelectedOption = _dropdown.Options[index];
            _dropdown.IsExpanded = false;
            _dropdown.HighlightedIndex = null;
        }

        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        if (isFocused && input.MenuCancelPressed && _dropdown.IsExpanded)
        {
            _dropdown.IsExpanded = false;
            _dropdown.HighlightedIndex = null;
        }

        _dropdown.Update(input);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableTextInput : IFocusable
{
    private readonly TextInputComponent _input;

    public FocusableTextInput(TextInputComponent input)
    {
        _input = input;
    }

    public Rectangle Bounds => _input.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => _input.IsFocused;

    public bool OnHorizontal(int direction) => false;

    public bool OnConfirm()
    {
        _input.IsFocused = true;
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        if (isFocused)
        {
            _input.IsFocused = true;
        }

        if (input.UiPointerPressed && Bounds.Contains(input.UiPointerPosition))
        {
            _input.IsFocused = true;
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        if (_input.IsFocused)
        {
            FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
        }
    }
}

public sealed class FocusableGridCell : IFocusable
{
    private readonly Func<bool> _onConfirm;

    public FocusableGridCell(Rectangle bounds, Func<bool> onConfirm)
    {
        Bounds = bounds;
        _onConfirm = onConfirm;
    }

    public Rectangle Bounds { get; }
    public bool IsEnabled { get; set; } = true;
    public bool CaptureHorizontalNavigation => false;
    public bool WasActivated { get; private set; }

    public bool OnHorizontal(int direction) => false;

    public bool OnConfirm()
    {
        WasActivated = _onConfirm();
        return WasActivated;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        WasActivated = false;
        if (Bounds.Contains(input.UiPointerPosition) && input.UiPointerPressed)
        {
            OnConfirm();
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}
